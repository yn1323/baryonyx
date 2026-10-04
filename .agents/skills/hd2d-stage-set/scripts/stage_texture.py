#!/usr/bin/env python3
"""Codex CLIが描いた3Dステージ用の画像を、1ドット1ピクセル・32色以内のドット絵テクスチャ（PNG）にする。

hd2d-stage-set の「テクスチャを作る」で使う（ふつうは fit_textures.py が寸法を決めて呼ぶ）。
生成画像は高解像度の「ドット絵風のイラスト」で格子も色数も規格に合わないため、
面積平均で目的の大きさへ縮めて格子を作り直し、色を減らす（格子を1画素ずつ拾うと塗りのノイズが点で残る）。

- マゼンタ（#FF00FF）の背景は透明にする。縁のマゼンタのにじみは、縮める前に物の側を少し削って取り除く。
- --crop で物の範囲（透明でない部分）に切り詰めてから縮める。
- --tile で、上下左右に並べたときの継ぎ目を目立たなくする（端の帯を反対側の端と混ぜる）。
- --bright-colors で、明るい画素（月・星）用の色を別に確保する。夜空のように暗い色が画面の大半を占める絵では、
  そのまま減色すると明るい物の色が暗い色に吸われ、月が平らな灰色になる。
- 出力したPNGは採用前の候補として output/ に置き、採用したら pixel-art-standards の png_to_aseprite.lua で .aseprite にする。

使い方（リポジトリ直下で）:
  uv run --no-project --with numpy --with pillow python .agents/skills/hd2d-stage-set/scripts/stage_texture.py SRC OUT --size 128x128 [--key] [--crop] [--tile] [--colors 32] [--bright-colors N --bright-threshold 170] [--preview N]
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter


def magenta_mask(rgb):
    """物の画素をTrueにする。マゼンタと、マゼンタに寄ったにじみを背景とみなす。"""
    r, g, b = rgb[..., 0].astype(int), rgb[..., 1].astype(int), rgb[..., 2].astype(int)
    distance = np.sqrt((r - 255) ** 2 + g ** 2 + (b - 255) ** 2)
    pinkish = (r > 150) & (b > 150) & (g < 0.6 * np.minimum(r, b))
    return ~((distance < 150) | pinkish)


def erode(mask, radius):
    if radius <= 0:
        return mask
    image = Image.fromarray((mask * 255).astype(np.uint8))
    image = image.filter(ImageFilter.MinFilter(radius * 2 + 1))
    return np.asarray(image) > 127


def area_resize(rgb, mask, size):
    """物の画素だけで面積平均し、半分以上が物なら不透明にする。"""
    width, height = size
    weight = Image.fromarray((mask * 255).astype(np.uint8)).resize(size, Image.BOX)
    weight = np.asarray(weight).astype(np.float32) / 255.0
    premultiplied = rgb.astype(np.float32) * mask[..., None]
    channels = [
        np.asarray(Image.fromarray(premultiplied[..., c].clip(0, 255).astype(np.uint8)).resize(size, Image.BOX)).astype(np.float32)
        for c in range(3)
    ]
    color = np.stack(channels, axis=-1) / np.maximum(weight[..., None], 1e-3)
    alpha = weight >= 0.5
    return color.clip(0, 255).astype(np.uint8), alpha


def blend_seams(rgb, band):
    """端の帯を、反対側の端から続く模様と混ぜる。上下左右に並べたときに継ぎ目が出にくくなる。"""
    image = rgb.astype(np.float32)
    height, width = image.shape[:2]
    for axis, length in ((1, width), (0, height)):
        rolled = np.roll(image, length // 2, axis=axis)
        ramp = np.ones(length, dtype=np.float32)
        edge = np.linspace(0.0, 1.0, band, dtype=np.float32)
        ramp[:band] = edge
        ramp[-band:] = edge[::-1]
        ramp = ramp.reshape((1, length, 1) if axis == 1 else (length, 1, 1))
        image = image * ramp + rolled * (1.0 - ramp)
    return image.clip(0, 255).astype(np.uint8)


def palette_of(pixels, colors):
    """画素の集まりを colors 色へ減らしたときの色の一覧（N×3）。"""
    strip = Image.fromarray(pixels.reshape(1, -1, 3).astype(np.uint8))
    palette_image = strip.quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    used = len(set(np.asarray(palette_image).reshape(-1).tolist()))
    return np.array(palette_image.getpalette()[: used * 3], dtype=np.int32).reshape(-1, 3)


def quantize(rgb, alpha, colors, bright_colors=0, bright_threshold=170):
    """不透明な画素の色だけで、ディザなしで colors 色へ減らす。
    bright_colors を指定すると、最も明るいチャンネルが bright_threshold 以上の画素からその数の色を選び、
    残りの画素から残りの色を選んでから、各画素を最も近い色に置き換える。"""
    opaque = rgb[alpha]
    if len(opaque) == 0:
        return rgb
    if bright_colors <= 0:
        strip = Image.fromarray(opaque.reshape(1, -1, 3))
        palette_image = strip.quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
        full = Image.fromarray(rgb).quantize(palette=palette_image, dither=Image.Dither.NONE).convert("RGB")
        return np.asarray(full)
    bright = opaque.max(axis=1) >= bright_threshold
    parts = []
    if bright.any():
        parts.append(palette_of(opaque[bright], bright_colors))
    parts.append(palette_of(opaque[~bright] if (~bright).any() else opaque, colors - bright_colors))
    palette = np.concatenate(parts)
    flat = rgb.reshape(-1, 3).astype(np.int32)
    nearest = np.argmin(((flat[:, None, :] - palette[None, :, :]) ** 2).sum(axis=2), axis=1)
    return palette[nearest].reshape(rgb.shape).astype(np.uint8)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("src")
    ap.add_argument("out")
    ap.add_argument("--size", required=True, help="出力の大きさ（例 128x128）。--crop では物の範囲をこの大きさへ縮める")
    ap.add_argument("--key", action="store_true", help="マゼンタの背景を透明にする")
    ap.add_argument("--erode", type=int, default=3, help="縮める前に物の縁を削る画素数（--key のとき）")
    ap.add_argument("--crop", action="store_true", help="物の範囲に切り詰めてから縮める")
    ap.add_argument("--tile", action="store_true", help="上下左右の継ぎ目を目立たなくする")
    ap.add_argument("--colors", type=int, default=32)
    ap.add_argument("--bright-colors", type=int, default=0, help="明るい画素（月・星）用に別に確保する色の数")
    ap.add_argument("--bright-threshold", type=int, default=170, help="明るい画素とみなす、最も明るいチャンネルの値（0〜255）")
    ap.add_argument("--preview", type=int, default=0, help="最近傍でN倍した確認用PNGも書き出す（タイルは2×2に並べる）")
    args = ap.parse_args()

    width, height = (int(v) for v in args.size.lower().split("x"))
    rgb = np.asarray(Image.open(args.src).convert("RGB"))
    mask = magenta_mask(rgb) if args.key else np.ones(rgb.shape[:2], dtype=bool)
    if args.key:
        mask = erode(mask, args.erode)
    if args.crop:
        ys, xs = np.nonzero(mask)
        top, bottom, left, right = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
        rgb, mask = rgb[top:bottom, left:right], mask[top:bottom, left:right]

    color, alpha = area_resize(rgb, mask, (width, height))
    if args.tile:
        color = blend_seams(color, max(2, width // 10))
    color = quantize(color, alpha, args.colors, args.bright_colors, args.bright_threshold)
    rgba = np.dstack([color, (alpha * 255).astype(np.uint8)])
    rgba[~alpha] = 0

    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(rgba, "RGBA").save(out)
    used = len({tuple(c) for c in color[alpha]})
    print(f"saved {out} ({width}x{height}, {used} colors, {int(alpha.sum())} opaque px)")

    if args.preview:
        preview = Image.fromarray(rgba, "RGBA")
        if args.tile:
            tiled = Image.new("RGBA", (width * 2, height * 2))
            for x in (0, width):
                for y in (0, height):
                    tiled.paste(preview, (x, y))
            preview = tiled
        preview = preview.resize((preview.width * args.preview, preview.height * args.preview), Image.NEAREST)
        preview.save(out.with_name(out.stem + "-preview.png"))


if __name__ == "__main__":
    main()
