#!/usr/bin/env python3
"""アイテムアイコンの候補づくりの道具。Codex CLIの画像生成（shared/scripts/codex_image.py）の前後に使う。

  style-ref  既存のアイテムの .aseprite を書き出し、4倍にして横に並べた画風見本を作る
  cut        生成画像（ドット絵風の大きなイラスト、灰色の背景）を列×行の格子に分け、
             マスごとに32×32・透明背景・32色以内のアイコンにする
  page       候補のアイコンを4倍で並べた一覧ページ（index.html）を作る

使い方（リポジトリ直下で）:
  uv run --no-project --with pillow python .agents/skills/item-icon/scripts/item_icons.py style-ref --out DIR/style-ref.png [ID...]
  uv run --no-project --with pillow python .agents/skills/item-icon/scripts/item_icons.py cut IMAGE COLS ROWS PREFIX
  uv run --no-project --with pillow python .agents/skills/item-icon/scripts/item_icons.py page --out DIR/index.html --group 見出し=PNG,PNG,... [--group ...]

cut は、背景を四辺からの塗りつぶしで抜き、マスの絵の範囲を切り出して長辺を SIZE ドットに合わせる。
1ドットに当たる範囲でいちばん多い色をそのドットの色にする（平均しないので、境目がぼけない）。
"""
import argparse
import base64
import html
import subprocess
import sys
import tempfile
from collections import Counter, deque
from pathlib import Path

from PIL import Image

SIZE = 32  # アイテムの寸法。正本は doc/art/direction.md の「画面と寸法」
COLORS = 32  # 色数の上限。同じ正本の「ドット絵の制作規格」
SCALE = 4  # 確認用の拡大率
TOL = 40  # 背景の灰色からの距離（RGBの各差の合計）

ART_ROOT = Path("client/Assets/Baryonyx/Shared/Art")
ASEPRITE = "/Applications/Aseprite.app/Contents/MacOS/aseprite"
# 画風見本の既定。色と材質がばらけるように、宝石・瓶・金属・植物・粉を選んだ。
DEFAULT_REFS = [
    "Accessories/DewPendant",
    "Items/DewVial",
    "Accessories/GoldEarring",
    "Items/GuardianSeed",
    "Accessories/KrakenPearl",
    "Items/SeaSalt",
]


def style_ref(out, refs):
    images = []
    with tempfile.TemporaryDirectory() as tmp:
        for ref in refs:
            png = Path(tmp) / (Path(ref).name + ".png")
            subprocess.run(
                [ASEPRITE, "-b", str(ART_ROOT / f"{ref}.aseprite"), "--save-as", str(png)],
                check=False,
                capture_output=True,
            )
            if not png.exists():
                sys.exit(f"書き出せません: {ref}")
            images.append(Image.open(png).convert("RGBA"))
    gap = 16
    sheet = Image.new(
        "RGBA",
        (len(images) * (SIZE * SCALE + gap) + gap, SIZE * SCALE + gap * 2),
        (200, 200, 200, 255),
    )
    for i, im in enumerate(images):
        big = im.resize((im.width * SCALE, im.height * SCALE), Image.NEAREST)
        sheet.alpha_composite(big, (gap + i * (SIZE * SCALE + gap), gap))
    sheet.save(out)
    print(out, sheet.size)


def remove_background(im):
    rgb = im.convert("RGB")
    w, h = rgb.size
    px = rgb.load()
    corners = [px[2, 2], px[w - 3, 2], px[2, h - 3], px[w - 3, h - 3]]
    bg = tuple(sorted(c[i] for c in corners)[1] for i in range(3))
    seen = bytearray(w * h)
    q = deque((x, y) for x in range(w) for y in (0, h - 1))
    q.extend((x, y) for y in range(h) for x in (0, w - 1))
    while q:
        x, y = q.popleft()
        i = y * w + x
        if seen[i]:
            continue
        c = px[x, y]
        if abs(c[0] - bg[0]) + abs(c[1] - bg[1]) + abs(c[2] - bg[2]) > TOL:
            continue
        seen[i] = 1
        if x > 0:
            q.append((x - 1, y))
        if x < w - 1:
            q.append((x + 1, y))
        if y > 0:
            q.append((x, y - 1))
        if y < h - 1:
            q.append((x, y + 1))
    alpha = Image.frombytes("L", (w, h), bytes(0 if s else 255 for s in seen))
    out = rgb.convert("RGBA")
    out.putalpha(alpha)
    return out


def to_icon(cell):
    box = cell.getchannel("A").getbbox()
    if box is None:
        return None
    art = cell.crop(box)
    # 大きいまま減色してから、ドットごとに多い色を選ぶ。
    rgb = Image.new("RGB", art.size, (0, 0, 0))
    rgb.paste(art.convert("RGB"), mask=art.getchannel("A"))
    pal = rgb.quantize(colors=COLORS, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    palette = pal.getpalette()
    idx = pal.load()
    alpha = art.getchannel("A").load()
    w, h = art.size
    scale = max(w, h) / SIZE
    ow, oh = max(1, round(w / scale)), max(1, round(h / scale))
    out = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    ox, oy = (SIZE - ow) // 2, (SIZE - oh) // 2
    for y in range(oh):
        for x in range(ow):
            x0, x1 = int(x * scale), max(int(x * scale) + 1, int((x + 1) * scale))
            y0, y1 = int(y * scale), max(int(y * scale) + 1, int((y + 1) * scale))
            votes = Counter()
            opaque = total = 0
            for sy in range(y0, min(y1, h)):
                for sx in range(x0, min(x1, w)):
                    total += 1
                    if alpha[sx, sy] >= 128:
                        opaque += 1
                        votes[idx[sx, sy]] += 1
            if total == 0 or opaque * 2 < total:
                continue
            i = votes.most_common(1)[0][0]
            out.putpixel((ox + x, oy + y), tuple(palette[i * 3 : i * 3 + 3]) + (255,))
    return out


def cut(image, cols, rows, prefix):
    im = remove_background(Image.open(image))
    w, h = im.size
    for r in range(rows):
        for c in range(cols):
            cell = im.crop((c * w // cols, r * h // rows, (c + 1) * w // cols, (r + 1) * h // rows))
            icon = to_icon(cell)
            if icon is None:
                print(f"空のマス: 行{r + 1} 列{c + 1}")
                continue
            path = Path(f"{prefix}-{r * cols + c + 1}.png")
            icon.save(path)
            print(path)


def page(out, groups):
    def img(path):
        data = base64.b64encode(Path(path).read_bytes()).decode()
        px = SIZE * SCALE
        return f'<img src="data:image/png;base64,{data}" width="{px}" height="{px}">'

    sections = []
    for group in groups:
        title, _, files = group.partition("=")
        figures = "".join(
            f"<figure>{img(f)}<figcaption>{html.escape(Path(f).parent.name)}/{html.escape(Path(f).stem)}</figcaption></figure>"
            for f in files.split(",")
            if f
        )
        sections.append(f"<h2>{html.escape(title)}</h2><div class=row>{figures}</div>")
    Path(out).write_text(
        f"""<!doctype html><meta charset="utf-8"><title>アイコンの候補</title>
<style>body{{background:#262838;color:#eee;font-family:sans-serif;padding:16px}}
img{{image-rendering:pixelated;background:#3c4058}}.row{{display:flex;flex-wrap:wrap;gap:16px}}
figure{{margin:0;text-align:center}}figcaption{{margin-top:6px;font-size:13px}}</style>
<p>{SIZE}×{SIZE}のドット絵を{SCALE}倍で表示している。</p>
{"".join(sections)}""",
        encoding="utf-8",
    )
    print(out)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    s = sub.add_parser("style-ref")
    s.add_argument("--out", required=True)
    s.add_argument("refs", nargs="*", default=DEFAULT_REFS)
    c = sub.add_parser("cut")
    c.add_argument("image")
    c.add_argument("cols", type=int)
    c.add_argument("rows", type=int)
    c.add_argument("prefix")
    p = sub.add_parser("page")
    p.add_argument("--out", required=True)
    p.add_argument("--group", action="append", required=True)
    args = parser.parse_args()
    if args.command == "style-ref":
        style_ref(args.out, args.refs)
    elif args.command == "cut":
        cut(args.image, args.cols, args.rows, args.prefix)
    else:
        page(args.out, args.group)


if __name__ == "__main__":
    main()
