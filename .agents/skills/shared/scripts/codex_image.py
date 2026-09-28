#!/usr/bin/env python3
"""Codex CLIの組み込み画像生成（image_gen）で画像を1枚作り、生成されたPNGを加工せずに出力先へコピーする。

character-sprite-sheet と character-illustration の両スキルが使う。

Codexは読み取り専用のサンドボックスで動かし、画像の生成だけを頼む。
Codexに保存や縮小をさせると、ドット絵を勝手に縮めて潰すことがあるため、
生成物は `$CODEX_HOME/generated_images/<セッションID>/` からこのスクリプトが回収する。

WindowsとmacOSの両方で動く。ファイルと標準入出力はOSの既定の文字コードによらずUTF-8で扱い、
Windowsでnpmが入れる `codex.cmd` も解決して起動する。

使い方（リポジトリ直下で、PowerShell・bash・zshのどれでも1行で実行する）:
  uv run --no-project python .agents/skills/shared/scripts/codex_image.py --out-dir DIR --name NAME --request FILE --profile FILE [--image FILE --image-role TEXT]... [--model MODEL] [--reasoning-effort LEVEL] [--timeout SEC] [--dry-run]
"""
import argparse
import filecmp
import json
import os
import shutil
import struct
import subprocess
import sys
import time
from pathlib import Path

CODEX_HOME = Path(os.environ.get("CODEX_HOME", Path.home() / ".codex"))


def png_size(path):
    with open(path, "rb") as f:
        head = f.read(24)
    if head[:8] != b"\x89PNG\r\n\x1a\n":
        return None
    return struct.unpack(">II", head[16:24])


def build_instruction(request, profile, image_roles):
    lines = [
        "組み込みの画像生成ツール（image_gen）で、下の「依頼」の画像を1枚だけ生成してください。",
        "",
        "- 生成したら作業は終わりです。ファイルの作成・コピー・縮小・減色・切り抜きなどの後処理はしないでください。生成された画像はこちらで回収します。",
        "- 画像生成のCLIのフォールバック（scripts/image_gen.py）は使わないでください。",
        "- 「キャラ設定（全文）」は人物を理解するための参考情報です。描く内容・構成・画風は「依頼」に従ってください。",
        "- 最後に、画像生成に使った最終的なプロンプトを報告してください。",
    ]
    if image_roles:
        lines.append("- 添付画像の扱い：")
        for i, role in enumerate(image_roles, start=1):
            lines.append(f"  - 画像{i}：{role}")
    lines += ["", "# 依頼", "", request.strip(), "", "# キャラ設定（全文）", "", profile.strip(), ""]
    return "\n".join(lines)


def find_thread_id(events_path):
    for line in events_path.read_text(encoding="utf-8", errors="replace").splitlines():
        try:
            event = json.loads(line)
        except json.JSONDecodeError:
            continue
        stack = [event]
        while stack:
            item = stack.pop()
            if isinstance(item, dict):
                for key in ("thread_id", "session_id"):
                    if isinstance(item.get(key), str):
                        return item[key]
                stack.extend(item.values())
            elif isinstance(item, list):
                stack.extend(item)
    return None


def collect_images(thread_id, started):
    root = CODEX_HOME / "generated_images"
    if thread_id and (root / thread_id).is_dir():
        candidates = (root / thread_id).glob("*.png")
    else:
        candidates = root.glob("*/*.png")
    return sorted((p for p in candidates if p.stat().st_mtime >= started), key=lambda p: p.stat().st_mtime)


def free_path(out_dir, name):
    path = out_dir / f"{name}.png"
    version = 2
    while path.exists():
        path = out_dir / f"{name}-v{version}.png"
        version += 1
    return path


def main():
    # Windowsでパイプへ出力するとcp932になり、Codexの返答に含まれる文字で止まることがあるため
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8", errors="replace")

    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out-dir", required=True, help="生成物を置くフォルダー")
    ap.add_argument("--name", required=True, help="出力ファイル名（拡張子なし）。既存なら -v2 などを付ける")
    ap.add_argument("--request", required=True, help="画像の依頼文のファイル")
    ap.add_argument("--profile", required=True, help="キャラ設定の全文のファイル")
    ap.add_argument("--image", action="append", default=[], help="添付画像。--image-role と同じ順で指定する")
    ap.add_argument("--image-role", action="append", default=[], help="添付画像の役割の説明")
    ap.add_argument("--model", help="Codexのモデル。省略時は ~/.codex/config.toml の設定")
    ap.add_argument("--reasoning-effort", help="Codexの推論の強さ（low・medium・high など）。省略時は ~/.codex/config.toml の設定")
    ap.add_argument("--timeout", type=int, default=900, help="Codexの実行を待つ秒数")
    ap.add_argument("--dry-run", action="store_true", help="依頼文とコマンドを書き出すだけで、Codexを呼ばない")
    args = ap.parse_args()

    if len(args.image_role) != len(args.image):
        sys.exit("--image と --image-role は同じ数だけ指定してください")
    # Windowsのnpmは codex.cmd を置く。subprocessは拡張子を補わないので、見つけたパスで起動する
    codex = shutil.which("codex")
    if not codex:
        sys.exit("codex コマンドが見つかりません。`npm install -g @openai/codex` でCodex CLIを入れ、`codex login` でログインしてください")

    out_dir = Path(args.out_dir).resolve()
    out_dir.mkdir(parents=True, exist_ok=True)

    attach_paths = [Path(image).resolve() for image in args.image]
    for path in attach_paths:
        if not path.is_file():
            sys.exit(f"添付画像がありません: {path}")

    instruction = build_instruction(Path(args.request).read_text(encoding="utf-8"),
                                    Path(args.profile).read_text(encoding="utf-8"), args.image_role)
    (out_dir / f"{args.name}.codex-prompt.md").write_text(instruction, encoding="utf-8")

    events = out_dir / f"{args.name}.codex-events.jsonl"
    last_message = out_dir / f"{args.name}.codex-reply.md"
    cmd = [codex, "exec", "--sandbox", "read-only", "--skip-git-repo-check", "--cd", str(out_dir),
           "--json", "--output-last-message", str(last_message)]
    if args.model:
        cmd += ["--model", args.model]
    if args.reasoning_effort:
        cmd += ["-c", f'model_reasoning_effort="{args.reasoning_effort}"']
    for path in attach_paths:
        cmd += ["--image", str(path)]
    # 依頼文は標準入力から渡す（引数に置くと --image の値と誤認されるおそれがあるため）

    if args.dry_run:
        print("dry-run: " + " ".join(cmd) + f" < {out_dir / (args.name + '.codex-prompt.md')}")
        return

    print(f"Codexに依頼しています（数分かかります）: {args.name}", file=sys.stderr)
    started = time.time() - 1
    with open(events, "w", encoding="utf-8") as out:
        try:
            result = subprocess.run(cmd, input=instruction, encoding="utf-8", errors="replace",
                                    stdout=out, stderr=subprocess.PIPE, timeout=args.timeout)
        except subprocess.TimeoutExpired:
            sys.exit(f"{args.timeout}秒待っても終わりませんでした。{CODEX_HOME / 'generated_images'} を確認してください")
    if result.returncode != 0:
        print(result.stderr[-2000:], file=sys.stderr)

    images = collect_images(find_thread_id(events), started)
    if not images:
        reply = last_message.read_text(encoding="utf-8", errors="replace") if last_message.exists() else ""
        sys.exit(f"生成画像が見つかりませんでした（終了コード {result.returncode}）。Codexの返答:\n{reply[-2000:]}")

    for image in images:
        # Codexが指示に反して出力先へ保存していることがあるので、同じ画像は二重にコピーしない
        dst = next((p for p in sorted(out_dir.glob(f"{args.name}*.png")) if filecmp.cmp(p, image, shallow=False)), None)
        if not dst:
            dst = free_path(out_dir, args.name)
            shutil.copy2(image, dst)
        size = png_size(dst)
        print(f"saved {dst}" + (f" ({size[0]}x{size[1]})" if size else "") + f" <- {image}")


if __name__ == "__main__":
    main()
