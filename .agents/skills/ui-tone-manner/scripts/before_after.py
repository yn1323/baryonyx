#!/usr/bin/env python3
"""前と後の撮影を左右に並べた画像を作る。

前の画像のフォルダーと後の画像のフォルダーに同じ名前の画像があるものを、work/before-after/<名前>.jpg にする。
画素が少しでも違う組だけを作り、違いのない組は飛ばす（見た目の変わった画面だけを送るため）。
使い方（リポジトリ直下）:
  python3 .agents/skills/ui-tone-manner/scripts/before_after.py 前のフォルダー 後のフォルダー [名前=見出し ...]
  例: ... before_after.py output/ui-audit/shots output/ui-audit/work/shots 006="Top 連携モーダル"
"""
import subprocess
import sys
from pathlib import Path

WORK = Path(__file__).resolve().parents[4] / "output/ui-audit/work"
FONT = "/System/Library/Fonts/ヒラギノ角ゴシック W6.ttc"


def differs(a, b):
    result = subprocess.run(["magick", "compare", "-metric", "AE", "-fuzz", "2%", str(a), str(b), "null:"],
                            capture_output=True, text=True)
    value = (result.stderr or "0").split()[0]
    try:
        return float(value) > 0
    except ValueError:
        return True


def main():
    before, after = Path(sys.argv[1]), Path(sys.argv[2])
    titles = dict(arg.split("=", 1) for arg in sys.argv[3:])
    out_dir = WORK / "before-after"
    out_dir.mkdir(parents=True, exist_ok=True)
    for shot in sorted(after.glob("*.png")):
        old = before / shot.name
        if not old.exists() or not differs(old, shot):
            continue
        title = titles.get(shot.stem, shot.stem)
        out = out_dir / f"{shot.stem}.jpg"
        subprocess.run(["magick", "(", str(old), "-resize", "900x", ")", "(", str(shot), "-resize", "900x", ")",
                        "+append", "-background", "#1a1a1a", "-fill", "white", "-font", FONT, "-pointsize", "30",
                        f"label:{title}　（左：前　右：後）", "+swap", "-gravity", "center", "-append",
                        "-quality", "88", str(out)], check=True)
        print(out.name)


if __name__ == "__main__":
    main()
