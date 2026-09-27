#!/usr/bin/env python3
"""PixelLabでHD-2D風の2D背景を、PixFluxの下書き4案からProの仕上げまで生成する。

  drafts: PixFlux（/create-image-pixflux-background）で400x224の下書きを4案作り、一覧ページを出力する。
  final:  選んだ下書きを構図の参考画像にして、Pro（/generate-image-v2）で640x360に描き直す。
  fetch:  待ち時間を超えたジョブを、出力先の request.json から取り出し直す。

APIトークンは環境変数 PIXELLAB_API_TOKEN、なければ ~/.claude.json に登録された
pixellab MCPサーバーの Authorization ヘッダーから読む。トークンは表示しない。
"""

import argparse
import base64
import html
import json
import os
import random
import struct
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

API = "https://api.pixellab.ai/v2"
DRAFT_W, DRAFT_H = 400, 224  # PixFluxは縦横とも4の倍数が必要
FINAL_W, FINAL_H = 640, 360
DRAFT_STYLE = {
    "shading": "highly detailed shading",
    "detail": "highly detailed",
    "outline": "selective outline",
    "view": "side",
}
REFERENCE_USAGE = (
    "Draft of this exact scene. Keep its composition, camera angle, layer layout and the placement "
    "of the main elements, but redraw everything with much richer detail and cleaner pixel clusters."
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


def wait_image(job_id, deadline):
    """完了したジョブの最初の画像をPNGのバイト列で返す。未完了ならNone。"""
    while True:
        job = request("GET", f"/background-jobs/{job_id}")
        status = job.get("status")
        if status == "completed":
            res = job.get("last_response") or {}
            images = res.get("images") or ([res["image"]] if res.get("image") else [])
            if not images:
                sys.exit(f"ジョブ {job_id} は完了したが画像がありません: {list(res)}")
            return base64.b64decode(images[0]["base64"].split(",", 1)[-1])
        if status == "failed":
            sys.exit(f"ジョブ {job_id} が失敗しました: {json.dumps(job, ensure_ascii=False)[:500]}")
        if time.time() >= deadline:
            return None
        time.sleep(8)


def png_size(data):
    return struct.unpack(">II", data[16:24])


# ---- 出力 ---------------------------------------------------------------

def read_description(args):
    if args.description_file:
        return Path(args.description_file).read_text().strip()
    if args.description:
        return args.description.strip()
    sys.exit("--description か --description-file を指定してください。")


def data_uri(data):
    return "data:image/png;base64," + base64.b64encode(data).decode()


def write_page(out, meta):
    """下書きと仕上げを最近傍で拡大表示する一覧ページを書き出す。"""
    cards = []
    for n, job in enumerate(meta.get("drafts", []), start=1):
        path = out / f"draft-{n:02d}.png"
        body = (f'<img src="{data_uri(path.read_bytes())}" width="{DRAFT_W * 3}">' if path.exists()
                else '<p class="wait">未完成（fetch で取り出し直す）</p>')
        cards.append(f'<figure>{body}<figcaption>案{n}　seed {job["seed"]}　{path.name}</figcaption></figure>')
    final = meta.get("final")
    if final:
        path = out / "final.png"
        body = (f'<img src="{data_uri(path.read_bytes())}" width="{FINAL_W * 2}">' if path.exists()
                else '<p class="wait">未完成（fetch で取り出し直す）</p>')
        cards.insert(0, f'<figure>{body}<figcaption>Pro仕上げ（2倍表示）　参考にした下書き：'
                        f'{html.escape(Path(final["draft"]).name)}　final.png</figcaption></figure>')
    drafts_desc = html.escape(meta.get("draft_description", ""))
    final_desc = html.escape(final["description"]) if final else ""
    page = f"""<!doctype html><meta charset="utf-8"><title>PixelLab背景</title>
<style>
body{{background:#1b1b1f;color:#ddd;font:14px/1.6 sans-serif;margin:16px}}
img{{image-rendering:pixelated;max-width:100%;height:auto;display:block}}
figure{{margin:0 0 24px}} figcaption{{margin-top:4px;color:#aaa}}
pre{{white-space:pre-wrap;background:#26262b;padding:8px;color:#bbb}} .wait{{color:#e99}}
</style>
{''.join(cards)}
<h3>下書きのDescription（PixFlux {DRAFT_W}x{DRAFT_H}、3倍表示）</h3><pre>{drafts_desc}</pre>
{f'<h3>仕上げのDescription（Pro {FINAL_W}x{FINAL_H}）</h3><pre>{final_desc}</pre>' if final else ''}
"""
    (out / "index.html").write_text(page)


def load_meta(out):
    path = out / "request.json"
    return json.loads(path.read_text()) if path.exists() else {}


def save_meta(out, meta):
    (out / "request.json").write_text(json.dumps(meta, ensure_ascii=False, indent=2))


def collect(out, meta, wait):
    """未取得のジョブを待って保存する。すべて保存できたらTrue。"""
    deadline = time.time() + wait
    done = True
    targets = [(job["job_id"], out / f"draft-{n:02d}.png") for n, job in enumerate(meta.get("drafts", []), start=1)]
    if meta.get("final"):
        targets.append((meta["final"]["job_id"], out / "final.png"))
    for job_id, path in targets:
        if path.exists():
            continue
        data = wait_image(job_id, deadline)
        if data is None:
            print(f"未完成: {path.name}（job {job_id}）", file=sys.stderr)
            done = False
            continue
        path.write_bytes(data)
        w, h = png_size(data)
        print(f"保存: {path}（{w}x{h}）")
    write_page(out, meta)
    print(out / "index.html")
    return done


# ---- commands ----------------------------------------------------------

def cmd_drafts(args):
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    description = read_description(args)
    meta = {"draft_description": description, "drafts": []}
    for _ in range(args.count):
        seed = random.randrange(2 ** 31)
        body = {
            "description": description,
            "image_size": {"width": DRAFT_W, "height": DRAFT_H},
            "no_background": False,
            "seed": seed,
            **DRAFT_STYLE,
        }
        res = request("POST", "/create-image-pixflux-background", body)
        meta["drafts"].append({"job_id": res["background_job_id"], "seed": seed})
    save_meta(out, meta)
    print(f"下書き{args.count}案を送信しました（生成枠 各1回分）")
    sys.exit(0 if collect(out, meta, args.wait) else 2)


def cmd_final(args):
    out = Path(args.out)
    meta = load_meta(out)
    description = read_description(args)
    draft = Path(args.draft)
    data = draft.read_bytes()
    w, h = png_size(data)
    body = {
        "description": description,
        "image_size": {"width": FINAL_W, "height": FINAL_H},
        "no_background": False,
        "reference_images": [{
            # data: URI の接頭辞を付けると、エラーにならずに画像が無視されることがある
            "image": {"type": "base64", "base64": base64.b64encode(data).decode(), "format": "png"},
            "size": {"width": w, "height": h},
            "usage_description": REFERENCE_USAGE,
        }],
    }
    if args.seed is not None:
        body["seed"] = args.seed
    res = request("POST", "/generate-image-v2", body)
    meta["final"] = {"job_id": res["background_job_id"], "draft": str(draft), "description": description}
    (out / "final.png").unlink(missing_ok=True)
    save_meta(out, meta)
    print("Proの仕上げを送信しました（生成枠 約40回分）")
    sys.exit(0 if collect(out, meta, args.wait) else 2)


def cmd_fetch(args):
    out = Path(args.out)
    meta = load_meta(out)
    if not meta:
        sys.exit(f"{out / 'request.json'} がありません。")
    sys.exit(0 if collect(out, meta, args.wait) else 2)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)

    p = sub.add_parser("drafts", help="PixFluxで下書きを作る")
    p.add_argument("--description")
    p.add_argument("--description-file")
    p.add_argument("--out", required=True)
    p.add_argument("--count", type=int, default=4)
    p.add_argument("--wait", type=int, default=300, help="完成を待つ秒数")
    p.set_defaults(func=cmd_drafts)

    p = sub.add_parser("final", help="選んだ下書きをもとにProで仕上げる")
    p.add_argument("--description")
    p.add_argument("--description-file")
    p.add_argument("--draft", required=True, help="選んだ下書きのPNG")
    p.add_argument("--out", required=True, help="下書きと同じ出力先フォルダー")
    p.add_argument("--seed", type=int)
    p.add_argument("--wait", type=int, default=600, help="完成を待つ秒数")
    p.set_defaults(func=cmd_final)

    p = sub.add_parser("fetch", help="待ち時間を超えたジョブを取り出し直す")
    p.add_argument("--out", required=True)
    p.add_argument("--wait", type=int, default=600)
    p.set_defaults(func=cmd_fetch)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
