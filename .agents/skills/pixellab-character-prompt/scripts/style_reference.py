#!/usr/bin/env python3
"""PixelLabのCreate from Style Reference (Pro)でキャラクター候補を生成し、一覧HTMLを作る。

標準ライブラリだけで動く。PixelLab MCPにはこの機能のツールがないため、REST APIを直接呼ぶ。

  submit  : 生成を依頼し、ジョブIDを表示する
  fetch   : 完成したジョブの候補をPNGで保存し、一覧HTMLを作る（--waitで完成まで待つ）

APIトークンは環境変数 PIXELLAB_API_TOKEN、なければ ~/.claude.json に登録された
pixellab MCPサーバーの Authorization ヘッダーから読む。トークンは表示しない。
"""
import argparse
import base64
import json
import os
import struct
import sys
import time
import urllib.error
import urllib.request
import zlib
from html import escape
from pathlib import Path

API = "https://api.pixellab.ai/v2"
SKILL_DIR = Path(__file__).resolve().parent.parent
DEFAULT_REFERENCE = SKILL_DIR / "assets" / "style-reference.png"
DEFAULT_STYLE_DESCRIPTION = "32x48に収まるように。全身絵。4頭身。右向き。"
TARGET_W, TARGET_H = 32, 48
SCALE = 3


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


# ---- PNG ---------------------------------------------------------------

def load_png(data):
    p, idat, plte, trns = 8, b"", None, None
    while p < len(data):
        length, kind = struct.unpack(">I4s", data[p:p + 8])
        chunk = data[p + 8:p + 8 + length]
        p += 12 + length
        if kind == b"IHDR":
            w, h, depth, color = struct.unpack(">IIBB", chunk[:10])
        elif kind == b"IDAT":
            idat += chunk
        elif kind == b"PLTE":
            plte = chunk
        elif kind == b"tRNS":
            trns = chunk
    if depth != 8:
        raise ValueError("8bit以外のPNGには対応していません")
    raw = zlib.decompress(idat)
    bpp = {6: 4, 2: 3, 3: 1, 4: 2, 0: 1}[color]
    stride = w * bpp
    prev, i, pixels = bytearray(stride), 0, []
    for _ in range(h):
        f = raw[i]
        row = bytearray(raw[i + 1:i + 1 + stride])
        i += 1 + stride
        for x in range(stride):
            a = row[x - bpp] if x >= bpp else 0
            b = prev[x]
            c = prev[x - bpp] if x >= bpp else 0
            if f == 1:
                row[x] = (row[x] + a) & 255
            elif f == 2:
                row[x] = (row[x] + b) & 255
            elif f == 3:
                row[x] = (row[x] + (a + b) // 2) & 255
            elif f == 4:
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                row[x] = (row[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        prev = row
        line = []
        for x in range(w):
            if color == 6:
                line.append(tuple(row[x * 4:x * 4 + 4]))
            elif color == 2:
                line.append(tuple(row[x * 3:x * 3 + 3]) + (255,))
            elif color == 3:
                k = row[x]
                alpha = trns[k] if trns and k < len(trns) else 255
                line.append(tuple(plte[k * 3:k * 3 + 3]) + (alpha,))
            elif color == 4:
                line.append((row[x * 2],) * 3 + (row[x * 2 + 1],))
            else:
                line.append((row[x],) * 3 + (255,))
        pixels.append(line)
    return w, h, pixels


def encode_png(w, h, pixels):
    raw = b"".join(b"\0" + bytes(v for px in row for v in px) for row in pixels)

    def chunk(kind, body):
        return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body) & 0xFFFFFFFF)

    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def remove_corner_labels(w, h, pixels):
    """左上隅だけにある独立した塊（生成AIが描き込む「C2」などの番号）を消し、消したドット数を返す。"""
    seen, removed = set(), 0
    for sy in range(h):
        for sx in range(w):
            if pixels[sy][sx][3] == 0 or (sx, sy) in seen:
                continue
            component, stack = [], [(sx, sy)]
            seen.add((sx, sy))
            while stack:
                x, y = stack.pop()
                component.append((x, y))
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        nx, ny = x + dx, y + dy
                        if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in seen and pixels[ny][nx][3] > 0:
                            seen.add((nx, ny))
                            stack.append((nx, ny))
            if all(x < 20 and y < 16 for x, y in component):
                for x, y in component:
                    pixels[y][x] = (0, 0, 0, 0)
                removed += len(component)
    return removed


def bbox(w, h, pixels):
    xs = [x for y in range(h) for x in range(w) if pixels[y][x][3] > 0]
    ys = [y for y in range(h) for x in range(w) if pixels[y][x][3] > 0]
    if not xs:
        return None
    return max(xs) - min(xs) + 1, max(ys) - min(ys) + 1


# ---- commands ----------------------------------------------------------

def cmd_submit(args):
    ref = Path(args.reference)
    ref_data = ref.read_bytes()
    w, h, _ = load_png(ref_data)
    body = {
        "style_images": [{"image": {"base64": "data:image/png;base64," + base64.b64encode(ref_data).decode()},
                          "width": w, "height": h}],
        "description": args.description,
        "style_description": args.style_description,
        "no_background": True,
    }
    if args.seed is not None:
        body["seed"] = args.seed
    res = request("POST", "/generate-with-style-v2", body)
    job_id = res["background_job_id"]
    if args.out:
        out = Path(args.out)
        out.mkdir(parents=True, exist_ok=True)
        meta = {k: v for k, v in body.items() if k != "style_images"}
        meta.update(job_id=job_id, reference=str(ref))
        (out / "request.json").write_text(json.dumps(meta, ensure_ascii=False, indent=2))
    print(job_id)


def cmd_fetch(args):
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    deadline = time.time() + args.wait
    while True:
        job = request("GET", f"/background-jobs/{args.job_id}")
        status = job.get("status")
        if status == "completed":
            break
        if status == "failed" or time.time() >= deadline:
            print(f"status: {status}", file=sys.stderr)
            sys.exit(2)
        time.sleep(10)

    meta_path = out / "request.json"
    meta = json.loads(meta_path.read_text()) if meta_path.exists() else {}
    ref_path = Path(meta.get("reference", DEFAULT_REFERENCE))
    figures = []
    if ref_path.exists():
        rw, rh, rpx = load_png(ref_path.read_bytes())
        size = bbox(rw, rh, rpx)
        figures.append(("参照", ref_path.read_bytes(), f"参照画像 {size[0]}x{size[1]}", "", True))

    fits = 0
    for n, image in enumerate(job["last_response"]["images"], start=1):
        data = base64.b64decode(image["base64"].split(",", 1)[-1])
        w, h, pixels = load_png(data)
        removed = remove_corner_labels(w, h, pixels)
        if removed:
            data = encode_png(w, h, pixels)
        name = f"{n:02d}"
        (out / f"{name}.png").write_bytes(data)
        size = bbox(w, h, pixels)
        notes = []
        if size[0] > TARGET_W:
            notes.append(f"幅{TARGET_W}超え")
        if size[1] > TARGET_H:
            notes.append(f"高さ{TARGET_H}超え")
        if not notes:
            fits += 1
        if removed:
            notes.append("左上の文字を除去")
        figures.append((name, data, f"{name}（{size[0]}x{size[1]}）", "・".join(notes), False))

    title = f"{meta.get('description', args.job_id)}"
    cells = []
    for name, data, caption, note, is_ref in figures:
        ok = "" if is_ref or "超え" in note else f'<span class="ok">{TARGET_W}x{TARGET_H}以内</span> '
        cells.append(
            f'<figure class="{"ref" if is_ref else ""}"><img alt="{escape(name)}" '
            f'src="data:image/png;base64,{base64.b64encode(data).decode()}">'
            f'<figcaption>{escape(caption)}<br>{ok}<span class="w">{escape(note)}</span></figcaption></figure>')
    px = 64 * SCALE
    html = f"""<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>{escape(title)} 候補</title><style>
:root{{--bg:#f4f1ea;--fg:#222;--cell:#e3ddd0;--warn:#c0392b;--ok:#2e7d32}}
@media (prefers-color-scheme:dark){{:root{{--bg:#1c1c1c;--fg:#eee;--cell:#2d2d2d;--warn:#ff7b6b;--ok:#7bd88f}}}}
body{{background:var(--bg);color:var(--fg);font-family:system-ui,sans-serif;margin:16px}}
h1{{font-size:18px}}p{{font-size:13px}}
.grid{{display:grid;grid-template-columns:repeat(auto-fill,minmax({px + 12}px,1fr));gap:10px}}
figure{{margin:0;background:var(--cell);border-radius:6px;padding:6px;text-align:center}}
img{{width:{px}px;max-width:100%;height:auto;aspect-ratio:1;image-rendering:pixelated}}
figcaption{{font-size:13px}}.w{{color:var(--warn)}}.ok{{color:var(--ok)}}.ref{{outline:2px solid #c80}}
</style>
<h1>{escape(title)}（{SCALE}倍表示）</h1>
<p>Description: {escape(meta.get('description', ''))}<br>Style description: {escape(meta.get('style_description', ''))}<br>
{TARGET_W}x{TARGET_H}以内: {fits} / {len(figures) - (1 if ref_path.exists() else 0)}枚　ジョブID: {escape(args.job_id)}</p>
<div class="grid">{''.join(cells)}</div>
"""
    (out / "index.html").write_text(html)
    print(f"{out / 'index.html'}  ({fits}/{len(job['last_response']['images'])} fit {TARGET_W}x{TARGET_H}, "
          f"usage: {job.get('usage')})")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)

    submit = sub.add_parser("submit", help="生成を依頼してジョブIDを表示する")
    submit.add_argument("--description", required=True)
    submit.add_argument("--style-description", default=DEFAULT_STYLE_DESCRIPTION)
    submit.add_argument("--reference", default=str(DEFAULT_REFERENCE))
    submit.add_argument("--seed", type=int)
    submit.add_argument("--out", help="request.json を保存するフォルダー（fetchと同じ場所を指定する）")
    submit.set_defaults(func=cmd_submit)

    fetch = sub.add_parser("fetch", help="候補をPNGで保存し、一覧HTMLを作る")
    fetch.add_argument("job_id")
    fetch.add_argument("--out", required=True)
    fetch.add_argument("--wait", type=int, default=0, help="完成まで待つ最大秒数（0なら待たない）")
    fetch.set_defaults(func=cmd_fetch)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
