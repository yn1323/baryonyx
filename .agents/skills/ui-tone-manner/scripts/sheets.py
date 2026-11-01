#!/usr/bin/env python3
"""撮った画像（work/shots/<画像の名前>.png）を、シーンごとに4列・16枚までの一覧画像 work/sheets/<シーン>-<n>.jpg にまとめる。

各画像の上に「番号 状態の名前」を付ける。ImageMagick（magick）を使う。
使い方（リポジトリ直下）: python3 .agents/skills/ui-tone-manner/scripts/sheets.py
"""
import collections
import subprocess
from pathlib import Path

WORK = Path(__file__).resolve().parents[4] / "output/ui-audit/work"
FONT = "/System/Library/Fonts/ヒラギノ角ゴシック W6.ttc"


def tile(src, label, out):
    subprocess.run(["magick", str(src), "-resize", "480x270", "-background", "#1a1a1a", "-fill", "white",
                    "-font", FONT, "-pointsize", "18", f"label:{label}", "+swap", "-gravity", "center",
                    "-append", str(out)], check=True)


def main():
    groups = collections.OrderedDict()
    for line in (WORK / "specs.txt").read_text(encoding="utf-8").splitlines():
        head, name = line.split("\t")
        scene, _, _, _, number = head.split("|")
        groups.setdefault(Path(scene).stem, []).append((number, name))
    (WORK / "sheets").mkdir(exist_ok=True)
    (WORK / "tiles").mkdir(exist_ok=True)
    for scene, items in groups.items():
        for part in range(0, len(items), 16):
            tiles = []
            for number, name in items[part:part + 16]:
                shot = WORK / "shots" / f"{number}.png"
                if not shot.exists():
                    continue
                out = WORK / "tiles" / f"{number}.png"
                tile(shot, f"{number} {name}", out)
                tiles.append(str(out))
            if tiles:
                sheet = WORK / "sheets" / f"{scene}-{part // 16 + 1}.jpg"
                subprocess.run(["magick", "montage", "-font", FONT, *tiles, "-tile", "4x", "-geometry", "+4+4",
                                "-background", "#111", "-quality", "85", str(sheet)], check=True)
                print(sheet.name, len(tiles))


if __name__ == "__main__":
    main()
