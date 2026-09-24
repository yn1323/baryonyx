#!/usr/bin/env python3
"""Build a self-contained animation preview page from PNG frames.

The page uses CSS animation only (no JavaScript), because some viewers such as the
Claude desktop file preview do not run scripts. Frames are embedded as data URIs
and drawn with nearest-neighbor scaling.

One action:
  preview_animation.py frames/f*.png --out walk.html --title "海賊05 歩行" \
      --play-frames 8 --note "First Frame・Last Frameとも待機の姿"

All actions of a character on one page (JSON manifest):
  preview_animation.py --manifest actions.json --out pirate05.html --title "海賊05"

  actions.json:
  [{"name": "歩行", "frames": ["walk/f0.png", ...], "play_frames": 8,
    "loop": true, "note": "...", "fps": [6, 8, 12]}, ...]

`fps` is optional per action and may be below 1 (0.5 shows each frame for 2 s).
`sheet_skip` (optional) leaves that many leading frames out of the sprite sheet,
for example the first frame of a release animation, which only repeats the last
frame of its preparation.

With --manifest the script also writes a sprite sheet: one row per action, one
cell per frame (the frames that play, minus `sheet_skip`), transparent, at 1x.
`<out>.sheet.png` holds the image and `<out>.sheet.json` the row, frame count,
fps, and loop flag of every action. The page shows the sheet with download links.

`play_frames` drops trailing frames from playback (for example, a frame pinned to
the idle pose by a last-frame input). `loop: false` marks a single-playback
action; it still repeats in the preview so it can be watched, with a pause on the
last frame.
"""
import argparse
import base64
import json
import struct
import sys
from html import escape
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from pixminimax import encode_png, load_png  # noqa: E402

HOLD_FRAMES = 4  # extra frame-times shown on the last pose of single-playback actions


def png_size(data):
    return struct.unpack(">II", data[16:24])


def b64(data):
    return base64.b64encode(data).decode()


def section(idx, spec, speeds, scale):
    frames = [Path(f).read_bytes() for f in spec["frames"]]
    total = len(frames)
    play = min(spec.get("play_frames") or total, total)
    loop = spec.get("loop", True)
    w, h = png_size(frames[0])
    var = lambda k: f"--s{idx}f{k}"

    def keyframes(name, count):
        slots = count + (0 if loop else HOLD_FRAMES)
        steps = "".join(f"{k * 100 / slots:.4f}%{{background-image:var({var(k)})}}" for k in range(count))
        return f"@keyframes {name}{{{steps}}}", slots

    variables = "".join(f"{var(k)}:url(data:image/png;base64,{b64(d)});" for k, d in enumerate(frames))
    kinds = [("p", play)] + ([("a", total)] if play < total else [])
    rules, players = [], []
    for kind, count in kinds:
        name = f"s{idx}{kind}"
        rule, slots = keyframes(name, count)
        rules.append(rule)
        own = [float(v) for v in spec.get("fps", speeds)]
        fps_list = own if kind == "p" else [own[len(own) // 2]]
        for fps in fps_list:
            cls = f"{name}x{str(fps).replace('.', '_')}"
            rules.append(f".{cls}{{animation:{name} {slots / fps:.4f}s step-end infinite;background-image:var({var(0)})}}")
            speed = f"{fps:g}fps" if fps >= 1 else f"1コマ{1 / fps:g}秒"
            label = f"コマ1〜{count}・{speed}" + ("（比較用）" if kind == "a" else "")
            players.append(f'<figure><div class="anim {cls}" style="width:{w * scale}px;aspect-ratio:{w}/{h}" '
                           f'role="img" aria-label="{escape(label)}"></div><figcaption>{escape(label)}</figcaption></figure>')
    strip = "".join(
        f'<figure><img alt="コマ{k + 1}" style="width:{w * scale}px" src="data:image/png;base64,{b64(d)}">'
        f"<figcaption>コマ{k + 1}</figcaption></figure>" for k, d in enumerate(frames))
    mode = "ループ" if loop else "1回再生（確認用に最後のコマで少し止めて繰り返す）"
    note = spec.get("note", "")
    body = (f'<section id="s{idx}"><h2>{escape(spec["name"])}</h2>'
            f'<p>{total}コマ・{w}×{h}・{mode}{"。" + escape(note) if note else ""}</p>'
            f'<div class="row">{"".join(players)}</div>'
            f'<details><summary>全コマを見る</summary><div class="row">{strip}</div></details></section>')
    return variables, "".join(rules), body


def write_sheet(specs, out, scale):
    rows = []
    for spec in specs:
        frames = [load_png(Path(f).read_bytes()) for f in spec["frames"]]
        play = min(spec.get("play_frames") or len(frames), len(frames))
        rows.append((spec, frames[spec.get("sheet_skip", 0):play]))
    cw = max(f[0] for _, fr in rows for f in fr)
    ch = max(f[1] for _, fr in rows for f in fr)
    cols = max(len(fr) for _, fr in rows)
    pixels = [[(0, 0, 0, 0)] * (cw * cols) for _ in range(ch * len(rows))]
    meta = {"cell_width": cw, "cell_height": ch, "columns": cols, "actions": []}
    for r, (spec, frames) in enumerate(rows):
        for c, (w, h, px) in enumerate(frames):
            for y in range(h):
                pixels[r * ch + y][c * cw:c * cw + w] = px[y]
        fps = [float(v) for v in spec.get("fps", [8])]
        meta["actions"].append({"name": spec["name"], "row": r, "frames": len(frames),
                                "fps": fps[len(fps) // 2], "loop": spec.get("loop", True)})
    png = encode_png(cw * cols, ch * len(rows), pixels)
    stem = out.with_suffix("")
    png_path, json_path = Path(f"{stem}.sheet.png"), Path(f"{stem}.sheet.json")
    png_path.write_bytes(png)
    text = json.dumps(meta, ensure_ascii=False, indent=2)
    json_path.write_text(text)
    rows_html = "".join(f"<li>{r + 1}行目：{escape(a['name'])}（{a['frames']}コマ）</li>"
                        for r, a in enumerate(meta["actions"]))
    return (f'<section id="sheet"><h2>スプライトシート</h2>'
            f'<p>1マス{cw}×{ch}、{cols}列×{len(rows)}行、背景透明、等倍。'
            f'<a download="{escape(png_path.name)}" href="data:image/png;base64,{b64(png)}">PNGを保存</a>　'
            f'<a download="{escape(json_path.name)}" href="data:application/json;base64,{b64(text.encode())}">JSONを保存</a><br>'
            f'ファイル：{escape(str(png_path))}</p><ol>{rows_html}</ol>'
            f'<img alt="スプライトシート" src="data:image/png;base64,{b64(png)}" '
            f'style="width:{cw * cols * scale}px;max-width:100%;height:auto;image-rendering:pixelated;'
            f'background:repeating-conic-gradient(#0002 0 25%,transparent 0 50%) 0 0/16px 16px"></section>')


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("frames", nargs="*", help="PNG frames in playback order (single action)")
    parser.add_argument("--manifest", help="JSON list of actions (see above)")
    parser.add_argument("--out", required=True)
    parser.add_argument("--title", required=True)
    parser.add_argument("--play-frames", type=int, help="single action: number of leading frames to play")
    parser.add_argument("--single", action="store_true", help="single action: not a loop")
    parser.add_argument("--fps", default="6,8,12", help="comma-separated playback speeds")
    parser.add_argument("--scale", type=int, default=2)
    parser.add_argument("--note", default="", help="single action: short Japanese note")
    args = parser.parse_args()

    if args.manifest:
        base = Path(args.manifest).parent
        specs = json.loads(Path(args.manifest).read_text())
        for spec in specs:
            spec["frames"] = [str(base / f) for f in spec["frames"]]
    elif args.frames:
        specs = [{"name": args.title, "frames": args.frames, "play_frames": args.play_frames,
                  "loop": not args.single, "note": args.note}]
    else:
        parser.error("frames or --manifest is required")

    speeds = [float(s) for s in args.fps.split(",") if s.strip()]
    parts = [section(i, spec, speeds, args.scale) for i, spec in enumerate(specs)]
    sheet_html = write_sheet(specs, Path(args.out), args.scale) if args.manifest else ""
    nav = "".join(f'<a href="#s{i}">{escape(s["name"])}</a>' for i, s in enumerate(specs)) if len(specs) > 1 else ""
    if args.manifest:
        nav += '<a href="#sheet">スプライトシート</a>'
    html = f"""<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>{escape(args.title)}</title><style>
:root{{--bg:#f4f1ea;--fg:#222;--cell:#e3ddd0;--acc:#c80;{''.join(p[0] for p in parts)}}}
@media (prefers-color-scheme:dark){{:root{{--bg:#1c1c1c;--fg:#eee;--cell:#2d2d2d}}}}
body{{background:var(--bg);color:var(--fg);font-family:system-ui,sans-serif;margin:16px}}
h1{{font-size:18px}}h2{{font-size:15px;margin:20px 0 4px}}p,summary{{font-size:13px}}
nav{{display:flex;flex-wrap:wrap;gap:6px 12px;font-size:13px}}nav a{{color:var(--acc)}}
.row{{display:flex;flex-wrap:wrap;gap:10px;margin-top:6px}}
figure{{margin:0;background:var(--cell);border-radius:6px;padding:6px;text-align:center}}figcaption{{font-size:12px}}
.anim{{max-width:80vw;background-size:100% 100%;image-rendering:pixelated;outline:1px dashed var(--acc)}}
.row img{{max-width:80vw;height:auto;image-rendering:pixelated;outline:1px dashed var(--acc)}}
{''.join(p[1] for p in parts)}
</style>
<h1>{escape(args.title)}（{args.scale}倍表示）</h1><p>点線は画像の枠。</p><nav>{nav}</nav>
{''.join(p[2] for p in parts)}
{sheet_html}
"""
    Path(args.out).write_text(html)
    print(args.out)


if __name__ == "__main__":
    main()
