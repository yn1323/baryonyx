#!/usr/bin/env python3
"""PixelLab Proで、アイテム・武器・防具の32x32アイコン候補を64枚作り、選んだ候補を切り出す。

  submit: 画風の見本（assets/style-reference.png）を渡して、Pro（/generate-image-v2）に64候補を依頼する。
  fetch:  候補を保存し、切り分け前の256x256の一覧（sheet.png）と一覧ページ（index.html）を作る。
  crop:   選んだ候補を、一覧から32x32の透明キャンバスへ切り出す。

Pillowを使うため、`uv run --no-project --with pillow python item_icon.py ...` で実行する。
APIトークンは環境変数 PIXELLAB_API_TOKEN、なければ ~/.claude.json に登録された
pixellab MCPサーバーの Authorization ヘッダーから読む。トークンは表示しない。
"""

import argparse
import base64
import io
import json
import os
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

from PIL import Image

SKILL_DIR = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(SKILL_DIR.parent / "shared" / "scripts"))
import pixel_style  # noqa: E402

API = "https://api.pixellab.ai/v2"
STYLE_REFERENCE = SKILL_DIR / "assets" / "style-reference.png"
SIZE = 32  # アイテムの寸法。正本は doc/art/direction.md の「画面と寸法」
GRID = 8  # 32x32のProは、8x8のマス目に64候補を描いてから切り分けて返す
SCALE = 4  # 確認用の拡大率。同じ正本の表示倍率に合わせる
# 画風の固定文。ドット絵の規格の部分（pixel-style.en.txt の core）は shared/prompts/ の1か所にだけ置き、
# 前後にアイテム用の語を付ける。規格の正本は doc/art/direction.md の「ドット絵の制作規格」。
STYLE_TAIL = (
    " Richly detailed, densely shaded late SNES era JRPG item sprite, "
    + pixel_style.compose("en", "core")
    + ", filling the whole canvas, clear silhouette."
)


# ---- API ---------------------------------------------------------------

def auth_header():
    token = os.environ.get("PIXELLAB_API_TOKEN")
    if token:
        return f"Bearer {token}"
    config = Path.home() / ".claude.json"
    if config.exists():
        data = json.loads(config.read_text())
        candidates = [data.get("mcpServers", {})]
        candidates += [p.get("mcpServers", {}) for p in data.get("projects", {}).values()]
        for servers in candidates:
            value = servers.get("pixellab", {}).get("headers", {}).get("Authorization")
            if value:
                return value
    sys.exit("PixelLabのAPIトークンが見つかりません。PIXELLAB_API_TOKEN を設定するか、pixellab MCPサーバーを登録してください。")


def request(method, path, body=None):
    req = urllib.request.Request(
        API + path,
        method=method,
        data=json.dumps(body).encode() if body is not None else None,
        headers={"Authorization": auth_header(), "Content-Type": "application/json"},
    )
    try:
        with urllib.request.urlopen(req, timeout=60) as res:
            return json.loads(res.read())
    except urllib.error.HTTPError as e:
        sys.exit(f"PixelLab APIエラー {e.code}: {e.read().decode(errors='replace')[:500]}")


def b64_image(data):
    # data: URI の接頭辞を付けると、エラーにならずに画像が無視されることがある
    return {"type": "base64", "base64": base64.b64encode(data).decode(), "format": "png"}


# ---- 一覧 ---------------------------------------------------------------

def joined_cells(sheet):
    """隣のマスと絵がつながっている（切り分けで割れた可能性がある）マスの番号を返す。"""
    alpha = sheet.getchannel("A").load()
    flagged = set()
    for row in range(GRID):
        for col in range(GRID):
            n = row * GRID + col + 1
            x0, y0 = col * SIZE, row * SIZE
            if row + 1 < GRID:
                y = y0 + SIZE - 1
                if sum(alpha[x, y] > 0 and alpha[x, y + 1] > 0 for x in range(x0, x0 + SIZE)) >= 3:
                    flagged.update({n, n + GRID})
            if col + 1 < GRID:
                x = x0 + SIZE - 1
                if sum(alpha[x, y] > 0 and alpha[x + 1, y] > 0 for y in range(y0, y0 + SIZE)) >= 3:
                    flagged.update({n, n + 1})
    return sorted(flagged)


def write_page(out, description, flagged):
    buf = io.BytesIO()
    Image.open(out / "sheet.png").save(buf, "PNG")
    src = "data:image/png;base64," + base64.b64encode(buf.getvalue()).decode()
    cell = SIZE * SCALE
    labels = []
    for n in range(1, GRID * GRID + 1):
        row, col = divmod(n - 1, GRID)
        cls = "cell warn" if n in flagged else "cell"
        labels.append(f'<div class="{cls}" style="left:{col * cell}px;top:{row * cell}px">{n}</div>')
    warn = (f"<p class=\"note\">赤枠のマス（{', '.join(map(str, flagged))}）は隣のマスと絵がつながっている。"
            "切り分けで割れた可能性があるので、cropの --box で切り出す。</p>" if flagged else "")
    page = f"""<!doctype html><meta charset="utf-8"><title>PixelLabアイテム候補</title>
<style>
body{{background:#1b1b1f;color:#ddd;font:14px/1.6 sans-serif;margin:16px}}
.sheet{{position:relative;width:{cell * GRID}px;height:{cell * GRID}px;background:#2a2a31 url({src}) 0 0/{cell * GRID}px auto no-repeat;image-rendering:pixelated}}
.cell{{position:absolute;width:{cell}px;height:{cell}px;box-sizing:border-box;border:1px solid #ffffff14;font-size:11px;color:#aaa;padding:0 3px}}
.warn{{border:2px solid #e55}} .note{{color:#f99}}
pre{{white-space:pre-wrap;background:#26262b;padding:8px;color:#bbb;max-width:{cell * GRID}px}}
</style>
<p>64候補を切り分け前の並び（8x8）で{SCALE}倍表示している。番号はマスの番号。</p>
{warn}
<div class="sheet">{''.join(labels)}</div>
<h3>Description</h3><pre>{description}</pre>
"""
    (out / "index.html").write_text(page)


# ---- commands ----------------------------------------------------------

def cmd_submit(args):
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    if args.description_file:
        description = Path(args.description_file).read_text().strip()
    elif args.description:
        description = args.description.strip()
    else:
        sys.exit("--description か --description-file を指定してください。")
    if not description.endswith(STYLE_TAIL.strip()):
        description += STYLE_TAIL
    style = STYLE_REFERENCE.read_bytes()
    w, h = Image.open(STYLE_REFERENCE).size
    body = {
        "description": description,
        "image_size": {"width": SIZE, "height": SIZE},
        "no_background": True,
        "style_image": {"image": b64_image(style), "size": {"width": w, "height": h}},
        "style_options": {"color_palette": True, "outline": True, "detail": True, "shading": True},
    }
    res = request("POST", "/generate-image-v2", body)
    job_id = res["background_job_id"]
    (out / "request.json").write_text(json.dumps({"job_id": job_id, "description": description},
                                                 ensure_ascii=False, indent=2))
    print(job_id)


def cmd_fetch(args):
    out = Path(args.out)
    meta = json.loads((out / "request.json").read_text())
    deadline = time.time() + args.wait
    while True:
        job = request("GET", f"/background-jobs/{meta['job_id']}")
        status = job.get("status")
        if status == "completed":
            break
        if status == "failed" or time.time() >= deadline:
            print(f"status: {status}", file=sys.stderr)
            sys.exit(2)
        time.sleep(10)

    images = job["last_response"]["images"]
    sheet = Image.new("RGBA", (SIZE * GRID, SIZE * GRID), (0, 0, 0, 0))
    for n, image in enumerate(images):
        tile = Image.open(io.BytesIO(base64.b64decode(image["base64"].split(",", 1)[-1]))).convert("RGBA")
        tile.save(out / f"cand-{n + 1:02d}.png")
        sheet.alpha_composite(tile, ((n % GRID) * SIZE, (n // GRID) * SIZE))
    sheet.save(out / "sheet.png")
    flagged = joined_cells(sheet)
    write_page(out, meta["description"], flagged)
    print(f"{len(images)}候補を保存しました。つながっているマス: {flagged or 'なし'}")
    print(out / "index.html")


def cmd_crop(args):
    out = Path(args.out)
    sheet = Image.open(out / "sheet.png").convert("RGBA")
    if args.box:
        x, y, w, h = args.box
    else:
        row, col = divmod(args.cell - 1, GRID)
        x, y, w, h = col * SIZE, row * SIZE, SIZE, SIZE
    piece = sheet.crop((x, y, x + w, y + h))
    bbox = piece.getbbox()
    if bbox is None:
        sys.exit("切り出した範囲に絵がありません。")
    piece = piece.crop(bbox)
    if piece.width > SIZE or piece.height > SIZE:
        print(f"注意: 絵が{piece.width}x{piece.height}で、{SIZE}x{SIZE}に収まりません。そのまま保存します。",
              file=sys.stderr)
        canvas = piece
    else:
        canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
        canvas.alpha_composite(piece, ((SIZE - piece.width) // 2, (SIZE - piece.height) // 2))
    path = out / f"{args.name}.png"
    canvas.save(path)
    canvas.resize((canvas.width * SCALE, canvas.height * SCALE), Image.NEAREST).save(out / f"{args.name}-x{SCALE}.png")
    print(f"{path}（{canvas.width}x{canvas.height}、絵は{piece.width}x{piece.height}）")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)

    p = sub.add_parser("submit", help="Proに64候補を依頼する")
    p.add_argument("--description")
    p.add_argument("--description-file")
    p.add_argument("--out", required=True)
    p.set_defaults(func=cmd_submit)

    p = sub.add_parser("fetch", help="候補を保存して一覧を作る")
    p.add_argument("--out", required=True)
    p.add_argument("--wait", type=int, default=540, help="完成を待つ秒数")
    p.set_defaults(func=cmd_fetch)

    p = sub.add_parser("crop", help="選んだ候補を切り出す")
    p.add_argument("--out", required=True)
    target = p.add_mutually_exclusive_group(required=True)
    target.add_argument("--cell", type=int, help="一覧のマスの番号（1〜64）")
    target.add_argument("--box", type=int, nargs=4, metavar=("X", "Y", "W", "H"),
                        help="sheet.png上の範囲（割れた候補を切り出すとき）")
    p.add_argument("--name", default="selected", help="保存するファイル名（拡張子なし）")
    p.set_defaults(func=cmd_crop)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
