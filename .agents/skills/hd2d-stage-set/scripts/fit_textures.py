#!/usr/bin/env python3
"""Codex CLI が描いた舞台の絵を、置く大きさ（m）とドットの細かさ（ドット/m）から決めた寸法のドット絵にする。

hd2d-stage-set の「テクスチャを作る」で使う。切り抜く物は、マゼンタ以外の範囲（物の外接矩形）の縦横比を測ってから
寸法を決める。Codex の出力は依頼した縦横比どおりにならず、物の周りの余白も絵ごとに違うため、
決め打ちの寸法で --crop すると絵がゆがむ。変換そのものは同じフォルダーの stage_texture.py が行う。

使い方（リポジトリ直下で）:
  uv run --no-project --with numpy --with pillow python .agents/skills/hd2d-stage-set/scripts/fit_textures.py SIZES.json output/<作業名>

SIZES.json は物ごとの配列（raw/<name>.png を読み、pixel/<name>.png と -preview.png を書く）:
  [{"name": "GladeTuft", "side": "w", "metres": 0.8, "dots_per_metre": 75, "form": "cutout"},
   {"name": "StarlitStatue", "side": "h", "metres": 2.6, "dots_per_metre": 45, "form": "cutout"},
   {"name": "GladeCrate", "side": "w", "metres": 0.8, "dots_per_metre": 60, "form": "panel"},
   {"name": "DuskSand", "side": "w", "metres": 1.8, "dots_per_metre": 38, "form": "decal", "erode": 10}]
- side: metres が幅（"w"）か高さ（"h"）か。もう一方は物の縦横比から決める。
- form: "cutout"・"decal" はマゼンタを抜いて物の範囲に切り詰める。"tile" は継ぎ目をなじませる。"panel" はそのまま縮める。
- erode: マゼンタのにじみを消すため、縮める前に物の縁を削る画素数（既定3）。
- extra: stage_texture.py へそのまま渡す追加の引数（例 ["--bright-colors", "8", "--bright-threshold", "200"]）。

結果の寸法と変換の指定は conversions.json に書く。client/ArtSource/Stages/prompts.json の "conversion" に写す。
"""
import json
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image

REPO = Path(__file__).resolve().parents[4]
STAGE_TEXTURE = Path(__file__).resolve().parent / "stage_texture.py"
sys.path.insert(0, str(STAGE_TEXTURE.parent))
from stage_texture import erode, magenta_mask  # noqa: E402


def fit(item, work):
    src = work / "raw" / f"{item['name']}.png"
    rgb = np.asarray(Image.open(src).convert("RGB"))
    form = item["form"]
    keyed = form in ("cutout", "decal")
    width_px = item.get("erode", 3)
    if keyed:
        mask = erode(magenta_mask(rgb), width_px)
        ys, xs = np.nonzero(mask)
        w, h = xs.max() + 1 - xs.min(), ys.max() + 1 - ys.min()
    else:
        h, w = rgb.shape[:2]
    aspect = h / w
    length = round(item["metres"] * item["dots_per_metre"])
    if item["side"] == "w":
        size = (length, max(1, round(length * aspect)))
    else:
        size = (max(1, round(length / aspect)), length)
    args = ["--size", f"{size[0]}x{size[1]}"]
    if keyed:
        args += ["--key", "--crop"]
        if "erode" in item:
            args += ["--erode", str(item["erode"])]
    if form == "tile":
        args.append("--tile")
    args += item.get("extra", [])
    out = work / "pixel" / f"{item['name']}.png"
    result = subprocess.run(
        [sys.executable, str(STAGE_TEXTURE), str(src), str(out), *args, "--preview", "4"],
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        raise SystemExit(f"{item['name']}: {result.stderr.strip()}")
    print(f"{item['name']}: 物の範囲 {w}x{h}（縦横比 {aspect:.2f}）-> {size[0]}x{size[1]}  {result.stdout.strip()}")
    return {
        "size": list(size),
        "conversion": STAGE_TEXTURE.relative_to(REPO).as_posix() + " " + " ".join(args),
    }


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(2)
    items = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    work = Path(sys.argv[2])
    results = {item["name"]: fit(item, work) for item in items}
    (work / "conversions.json").write_text(json.dumps(results, ensure_ascii=False, indent=1), encoding="utf-8")


if __name__ == "__main__":
    main()
