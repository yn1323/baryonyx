#!/usr/bin/env python3
"""PixelLabのAnimate with Text（PixMiniMax）でアニメーションを生成する補助スクリプト。

標準ライブラリだけで動く。MCPのツールは画像をbase64で引数に書き込む必要があり、
出力量の上限に達しやすいため、同じ機能のREST API `/v2/animate-pixminimax` を直接呼ぶ。

  base    : 立ち絵を100x100の透明キャンバスの左右中央・下寄せに置く（拡大しない）
  submit  : 生成を依頼し、ジョブIDを <name>.job に保存する
  fetch   : 完成を待ってコマを <name>/f0.png, f1.png ... に保存する（複数ジョブを同時に待てる）

APIトークンは環境変数 PIXELLAB_API_TOKEN、なければ ~/.claude.json の pixellab MCPサーバーの
Authorization ヘッダーから読む。トークンは表示しない。
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
from pathlib import Path

API = "https://api.pixellab.ai/v2"
MAX_DESCRIPTION = 1000


def auth_header():
    token = os.environ.get("PIXELLAB_API_TOKEN")
    if token:
        return f"Bearer {token}"
    config = Path.home() / ".claude.json"
    if config.exists():
        data = json.loads(config.read_text())
        groups = [data.get("mcpServers", {})] + [p.get("mcpServers", {}) for p in data.get("projects", {}).values()]
        for servers in groups:
            value = servers.get("pixellab", {}).get("headers", {}).get("Authorization")
            if value:
                return value
    sys.exit("PixelLabのAPIトークンが見つかりません。PIXELLAB_API_TOKEN を設定するか、pixellab MCPサーバーを登録してください。")


def request(method, path, body=None):
    req = urllib.request.Request(API + path, method=method,
                                 data=json.dumps(body).encode() if body is not None else None,
                                 headers={"Authorization": auth_header(), "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=60) as res:
            return json.loads(res.read())
    except urllib.error.HTTPError as e:
        return {"http_error": e.code, "detail": e.read().decode(errors="replace")[:500]}


# ---- PNG（RGBA 8bitのみ書き出し、読み込みは主要形式） -------------------------

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
    raw = zlib.decompress(idat)
    bpp = {6: 4, 2: 3, 3: 1, 4: 2, 0: 1}[color]
    stride, prev, i, pixels = w * bpp, bytearray(w * bpp), 0, []
    for _ in range(h):
        f, row = raw[i], bytearray(raw[i + 1:i + 1 + stride])
        i += 1 + stride
        for x in range(stride):
            a = row[x - bpp] if x >= bpp else 0
            b, c = prev[x], (prev[x - bpp] if x >= bpp else 0)
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
                line.append(tuple(plte[k * 3:k * 3 + 3]) + ((trns[k] if trns and k < len(trns) else 255),))
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


# ---- commands ------------------------------------------------------------

def cmd_base(args):
    w, h, px = load_png(Path(args.sprite).read_bytes())
    pts = [(x, y) for y in range(h) for x in range(w) if px[y][x][3] > 0]
    x0, x1 = min(p[0] for p in pts), max(p[0] for p in pts)
    y1 = max(p[1] for p in pts)
    size = args.size
    dx = size // 2 - (x0 + x1 + 1) // 2
    dy = size - 1 - y1
    out = [[(0, 0, 0, 0)] * size for _ in range(size)]
    for x, y in pts:
        X, Y = x + dx, y + dy
        if not (0 <= X < size and 0 <= Y < size):
            sys.exit("キャラクターがキャンバスに収まりません。--size を大きくしてください。")
        out[Y][X] = px[y][x]
    Path(args.out).write_bytes(encode_png(size, size, out))
    print(f"{args.out}  ({size}x{size}, 描画範囲 x{x0 + dx}-{x1 + dx} y{min(p[1] for p in pts) + dy}-{y1 + dy})")


def image_field(path):
    return {"type": "base64", "base64": base64.b64encode(Path(path).read_bytes()).decode(), "format": "png"}


def cmd_submit(args):
    work = Path(args.dir)
    description = (work / f"{args.name}.txt").read_text().strip()
    if len(description) > MAX_DESCRIPTION:
        sys.exit(f"説明文が{len(description)}文字です。PixMiniMaxの上限は{MAX_DESCRIPTION}文字です。")
    if args.frames % 4 or not 4 <= args.frames <= 40:
        sys.exit("生成枚数は4〜40の4の倍数にしてください。")
    body = {"first_frame": image_field(args.first), "description": description,
            "frame_count": args.frames, "no_background": True}
    if args.last:
        body["last_frame"] = image_field(args.last)
    res = request("POST", "/animate-pixminimax", body)
    job = res.get("background_job_id")
    if not job:
        sys.exit(f"{args.name}: 受け付けられませんでした {json.dumps(res, ensure_ascii=False)[:400]}")
    (work / f"{args.name}.job").write_text(job)
    print(f"{args.name} {job}")


def cmd_fetch(args):
    work = Path(args.dir)
    pending, start, failed = list(args.names), time.time(), []
    while pending:
        for name in list(pending):
            job = request("GET", f"/background-jobs/{(work / f'{name}.job').read_text().strip()}")
            status = job.get("status")
            if status == "completed":
                out = work / name
                out.mkdir(exist_ok=True)
                for old in out.glob("f*.png"):
                    old.unlink()
                images = job["last_response"]["images"]
                for i, image in enumerate(images):
                    (out / f"f{i}.png").write_bytes(base64.b64decode(image["base64"].split(",", 1)[-1]))
                print(f"{time.strftime('%H:%M:%S')} {name} completed frames={len(images)} usage={job.get('usage')}", flush=True)
                pending.remove(name)
                if args.on_complete:
                    os.system(args.on_complete)
            elif status == "failed" or "http_error" in job:
                detail = (job.get("last_response") or {}).get("detail") or job.get("detail") or job.get("error")
                print(f"{time.strftime('%H:%M:%S')} {name} FAILED {detail}", flush=True)
                pending.remove(name)
                failed.append(name)
        if pending:
            if time.time() - start > args.wait:
                print("まだ完成していないジョブ: " + " ".join(pending))
                sys.exit(2)
            time.sleep(15)
    sys.exit(1 if failed else 0)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)

    base = sub.add_parser("base", help="立ち絵を100x100の中央下に置く")
    base.add_argument("sprite")
    base.add_argument("--out", required=True)
    base.add_argument("--size", type=int, default=100)
    base.set_defaults(func=cmd_base)

    submit = sub.add_parser("submit", help="生成を依頼する（説明文は <dir>/<name>.txt）")
    submit.add_argument("name")
    submit.add_argument("--dir", required=True, help="作業フォルダー")
    submit.add_argument("--frames", type=int, required=True, help="生成枚数（4の倍数）")
    submit.add_argument("--first", required=True, help="First FrameのPNG")
    submit.add_argument("--last", help="Last FrameのPNG（省略可）")
    submit.set_defaults(func=cmd_submit)

    fetch = sub.add_parser("fetch", help="完成を待ってコマを保存する")
    fetch.add_argument("names", nargs="+")
    fetch.add_argument("--dir", required=True)
    fetch.add_argument("--wait", type=int, default=1500, help="待つ最大秒数")
    fetch.add_argument("--on-complete", help="1件完成するたびに実行するコマンド（確認ページの作り直しなど）")
    fetch.set_defaults(func=cmd_fetch)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
