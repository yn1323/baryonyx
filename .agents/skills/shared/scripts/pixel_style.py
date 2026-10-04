#!/usr/bin/env python3
"""ドット絵の画風の固定文（shared/prompts/pixel-style.<言語>.txt）を部品ごとに読み、組み合わせて出力する。

固定文の正本は doc/art/direction.md の「ドット絵の制作規格」で、文そのものは shared/prompts/ の1か所にだけ置く。
pixellab-item-prompt の item_icon.py と hd2d-stage-set の stage_requests.py がこのモジュールを読み込む。

使い方（リポジトリ直下で）:
  python3 .agents/skills/shared/scripts/pixel_style.py ja touch light colors detail character
  python3 .agents/skills/shared/scripts/pixel_style.py en touch core limits background
日本語は部品を改行でつなぎ、英語は「, 」でつなぐ。部品名を省くと、使える部品の一覧を出す。
"""
import sys
from pathlib import Path

PROMPTS = Path(__file__).resolve().parent.parent / "prompts"
JOINERS = {"ja": "\n", "en": ", "}


def parts(lang):
    """部品名から文への辞書を返す。「#」の行は説明として読み飛ばす。"""
    result = {}
    name = None
    for line in (PROMPTS / f"pixel-style.{lang}.txt").read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        if line.startswith("[") and line.endswith("]"):
            name = line[1:-1]
            continue
        if name is None or name in result:
            raise ValueError(f"pixel-style.{lang}.txt の部品は、区切りの次の1行にしてください: {line}")
        result[name] = line
    return result


def compose(lang, *names):
    """指定した部品を順に並べた文を返す。"""
    table = parts(lang)
    missing = [n for n in names if n not in table]
    if missing:
        raise KeyError(f"pixel-style.{lang}.txt にない部品です: {', '.join(missing)}")
    return JOINERS[lang].join(table[n] for n in names)


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    if len(sys.argv) < 2 or sys.argv[1] not in JOINERS:
        sys.exit(__doc__)
    lang, names = sys.argv[1], sys.argv[2:]
    if not names:
        print("\n".join(f"[{n}] {t}" for n, t in parts(lang).items()))
        return
    print(compose(lang, *names))


if __name__ == "__main__":
    main()
