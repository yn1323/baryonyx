"""ドット絵のPNGを、doc/art/direction.md の「ドット絵の制作規格」と照らし合わせて検査する。

Pillowを使うので、リポジトリ直下から次のように実行する。

    uv run --no-project --with pillow python \
      .agents/skills/pixel-art-standards/scripts/check_pixel_art.py --kind character --standing a.png

検査すること：
- 画像の大きさが種類ごとの規格と合うか（背景は未決のため大きさを表示するだけ）
- 半透明のピクセルがないか（背景側へのアンチエイリアスやぼかしの検出）
- 不透明な色が32色以内か
- 拡大済みの画像ではないか（同じ色の並びがすべてk倍になっていないか）
- 味方キャラの立ち姿が48x48以内で左右中央にあるか、複数枚で足元の高さが揃っているか
- 敵の本体が画像いっぱいに描かれているか

規格を変えたら、direction.md と同じ変更でこのファイルの値も直す。
規格との差が1つでもあれば終了コード1、注意だけなら0で終わる。
"""

import argparse
import sys
from math import gcd
from pathlib import Path

from PIL import Image

MAX_COLORS = 32
STANDING_BODY = 48
KINDS = {
    "character": 64,
    "enemy-small": 64,
    "enemy-medium": 96,
    "enemy-large": 128,
    "item": 32,
    "ui-icon": 24,
    "background": None,
}


def opaque_bbox(img):
    alpha = img.getchannel("A").point(lambda a: 255 if a > 0 else 0)
    return alpha.getbbox()


def run_gcd(img):
    """同じ色が続く長さの最大公約数。1より大きければ、その倍率で拡大された画像と考えられる。"""
    w, h = img.size
    px = img.load()
    g = 0
    for y in range(h):
        run = 1
        for x in range(1, w + 1):
            if x < w and px[x, y] == px[x - 1, y]:
                run += 1
            else:
                g = gcd(g, run)
                run = 1
                if g == 1:
                    return 1
    for x in range(w):
        run = 1
        for y in range(1, h + 1):
            if y < h and px[x, y] == px[x, y - 1]:
                run += 1
            else:
                g = gcd(g, run)
                run = 1
                if g == 1:
                    return 1
    return g


def check(path, kind, standing):
    errors, notes = [], []
    img = Image.open(path).convert("RGBA")
    w, h = img.size
    expected = KINDS[kind]

    if expected is None:
        notes.append(f"大きさ {w}x{h}（背景・タイルの寸法は未決のため検査しない）")
    elif (w, h) != (expected, expected):
        errors.append(f"大きさが {w}x{h}。{kind} の規格は {expected}x{expected}")

    pixels = list(getattr(img, "get_flattened_data", img.getdata)())
    semi = sum(1 for p in pixels if 0 < p[3] < 255)
    if semi:
        errors.append(f"半透明のピクセルが {semi} 個ある。アンチエイリアスは輪郭の内側に不透明の中間色で置く")

    colors = {p[:3] for p in pixels if p[3] > 0}
    if len(colors) > MAX_COLORS:
        errors.append(f"色数が {len(colors)} 色。規格は {MAX_COLORS} 色＋透明まで")
    else:
        notes.append(f"色数 {len(colors)} 色")

    scale = run_gcd(img)
    if scale > 1:
        errors.append(f"同じ色の並びがすべて {scale} の倍数。{scale} 倍に拡大した画像の可能性がある。1ドット1ピクセルで書き出す")

    bbox = opaque_bbox(img)
    baseline = None
    if bbox is None:
        errors.append("不透明なピクセルがない")
    elif kind == "character":
        left, top, right, bottom = bbox
        baseline = bottom
        notes.append(f"本体の範囲 {right - left}x{bottom - top}、足元の行 y={bottom - 1}")
        if standing:
            if right - left > STANDING_BODY or bottom - top > STANDING_BODY:
                errors.append(f"立ち姿が {right - left}x{bottom - top}。{STANDING_BODY}x{STANDING_BODY} の範囲に収める")
            offset = (left + right) / 2 - w / 2
            if abs(offset) > 1:
                errors.append(f"立ち姿の中心が左右中央から {offset:+.1f} ドットずれている")
    elif kind.startswith("enemy"):
        left, top, right, bottom = bbox
        if max(right - left, bottom - top) < expected - 2:
            notes.append(f"注意：本体が {right - left}x{bottom - top} で、画像 {w}x{h} より小さい。敵の画像は余白を設けない")

    return errors, notes, baseline


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--kind", required=True, choices=sorted(KINDS))
    parser.add_argument("--standing", action="store_true", help="味方キャラの立ち姿として48x48・左右中央も検査する")
    parser.add_argument("files", nargs="+")
    args = parser.parse_args()

    failed = False
    baselines = {}
    for name in args.files:
        path = Path(name)
        errors, notes, baseline = check(path, args.kind, args.standing)
        status = "差あり" if errors else "OK"
        print(f"[{status}] {path}")
        for e in errors:
            print(f"  - {e}")
        for n in notes:
            print(f"  ・{n}")
        failed |= bool(errors)
        if baseline is not None:
            baselines[path] = baseline

    if len(set(baselines.values())) > 1:
        print("注意：足元の行がファイルごとに異なる。武器などが足元より下に出ていないか確かめ、足元の高さを揃える")
        for path, b in baselines.items():
            print(f"  ・{path}: y={b - 1}")

    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
