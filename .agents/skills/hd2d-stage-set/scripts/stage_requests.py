#!/usr/bin/env python3
"""3Dの舞台のテクスチャを Codex CLI に頼む依頼文を、固定の段落と物の説明から組み立て、並行生成のスクリプトを書き出す。

hd2d-stage-set の「テクスチャを作る」で使う。依頼文は1枚ごとにファイルへ書き、
.agents/skills/shared/scripts/codex_image.py を1ファイルずつ呼ぶシェルスクリプトを xargs で並行に回す
（依頼文をまとめて引数に渡すと、コマンドラインが長すぎて失敗した）。

固定の段落は、doc/art/hd2d-stage.md の「テクスチャ」と doc/art/direction.md の
「ドット絵の制作規格」「例外（3Dの舞台のテクスチャ）」を正本とする。正本を変えたら、同じ変更でこの段落も直す。
「■ドット絵の規格」のうち、タッチと背景の色味の行は shared/prompts/pixel-style.ja.txt の touch・background を読み込む。
色数の行は、pixel-style.ja.txt の colors からアンチエイリアスの文を除いた形でここに持つ。

使い方（リポジトリ直下で）:
  python3 .agents/skills/hd2d-stage-set/scripts/stage_requests.py SPEC.json output/<作業名>
  output/<作業名>/run_all.sh    # 生成（6〜8並列で1枚2〜3分）。生成物は output/<作業名>/raw/

SPEC.json は物ごとの配列:
  [{"name": "GladeCrate", "light": "day", "form": "panel", "subject": "正面から見た（遠近なし）木の補給箱の1つの面。…"}]
- light: "day"（昼の森の舞台。日光と木漏れ日をゲーム側で付ける）か "night"（夜・夕暮れ。昼の明るさで描かせる）、
  森以外の昼や屋内も含めて時間帯を問わない "plain"（昼の明るさで描かせる）。
  form が "painted" のときは使わない（時間帯の色を絵に描き込む）。
- form:
  - "cutout": 切り抜く物（周りと穴をマゼンタで塗らせる）
  - "decal": 地面に敷く汚し（真上から見た切り抜き。周りがまばらに終わる）
  - "tile": 床・壁の継ぎ目のないタイル
  - "panel": 面いっぱいの1枚絵（箱の面、1枚絵の床）
  - "painted": 光を当てない描いた遠景（空、遠い山並み、遠い荒野）
- subject の書き出しで、見る向き（「真上から見下ろした（正射影、遠近なし）」「正面から見た（遠近なし）」）、
  縦横比（「横長（横が縦の約2倍）」）、下端が地面に接するかを書く。
"""
import json
import stat
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "shared" / "scripts"))
import pixel_style  # noqa: E402

SPEC = "\n".join([
    "■ドット絵の規格",
    pixel_style.compose("ja", "touch"),
    "色数は32色以内。陰影は段を分けて塗り、色の境目にディザ（2色の市松模様）を入れる。背景側へのぼかしや滑らかなグラデーションは使わない。",
    pixel_style.compose("ja", "background"),
    "細部まで多めに描き込む。実物を観察して描いたような自然な形と質感（風化、汚れ、欠け、木目、錆、苔）にし、記号的・おもちゃのような形にしない。",
])

LIGHT = {
    "day": """■3Dのテクスチャとしての条件
3Dのゲームで面に貼るテクスチャ（色の素材）として使う。昼の森として、ゲーム側で日光・木漏れ日・影を付けるため、強い光源の向き・投げ影・強いハイライト・暗い周辺減光を描き込まない。明るさは全体で均一にし、凹凸に左上から当たる控えめな陰影だけを付ける。
文字・ロゴ・枠・署名・UI・人物・動物は入れない。""",
    "night": """■3Dのテクスチャとしての条件
3Dのゲームで面に貼るテクスチャ（色の素材）として使う。夜や夕暮れの光はゲーム側で付けるため、昼間の自然な明るさで均一に描き、強い光源の向き・投げ影・強いハイライト・暗い周辺減光を描き込まない。凹凸に左上から当たる控えめな陰影だけを付ける。
文字・ロゴ・枠・署名・UI・人物・動物は入れない。""",
    "plain": """■3Dのテクスチャとしての条件
3Dのゲームで面に貼るテクスチャ（色の素材）として使う。時間帯の光と影はゲーム側で付けるため、昼間の自然な明るさで均一に描き、強い光源の向き・投げ影・強いハイライト・暗い周辺減光を描き込まない。凹凸に左上から当たる控えめな陰影だけを付ける。
文字・ロゴ・枠・署名・UI・人物・動物は入れない。""",
}

PAINTED = """■遠景としての条件
3Dのゲームの奥に、光を当てずにそのまま表示する遠景の絵として使う。時間帯の色と明るさを絵の中で描き込む。
文字・ロゴ・枠・署名・UI・人物・動物は入れない。"""

CUTOUT = """■背景の透過
物の周りと穴の部分は、すべて単色のマゼンタ（#FF00FF）で塗りつぶす。マゼンタの部分には影・光・ぼかし・中間色を一切入れず、物の輪郭はマゼンタとはっきり分ける。物の中にマゼンタに近い色を使わない。"""

DECAL = """■地面の汚し（デカール）としての条件
平らな地面の上に重ねて敷く汚しとして使う。物の影や地面そのものは描かず、散らばる物だけを描く。外周は不規則にまばらになって終わり、四角い縁や円い縁を作らない。"""

TILE = """■継ぎ目なく並べる
上下左右に繰り返して並べても継ぎ目が見えない、シームレスなタイルにする。端で模様が切れず、反対側の端とつながるように描く。画面いっぱいに模様だけを描き、余白や縁取りを付けない。"""

PANEL = """■面いっぱいに描く
画像いっぱいに面だけを描き、余白・背景・縁取りを付けない。"""

GEN_ONE = """#!/bin/bash
# 依頼文1つを Codex CLI で画像にする（hd2d-stage-set の stage_requests.py が書き出した）。
cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
n=$(basename "$1" .md)
python3 .agents/skills/shared/scripts/codex_image.py --out-dir "{out}/raw" --name "$n" --request "$1" --timeout 1500 > "{out}/raw-$n.log" 2>&1
echo "done $n $?"
"""

RUN_ALL = """#!/bin/bash
# 3Dの舞台のテクスチャを Codex CLI で並行生成する（hd2d-stage-set の stage_requests.py が書き出した）。
cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
ls "{out}"/requests/*.md | xargs -P {parallel} -n 1 "{out}/gen_one.sh"
"""


def request(item):
    form = item["form"]
    parts = [item["subject"].strip(), SPEC]
    if form == "painted":
        parts.append(PAINTED)
    else:
        parts.append(LIGHT[item["light"]])
    parts += {
        "cutout": [CUTOUT],
        "decal": [CUTOUT, DECAL],
        "tile": [TILE],
        "panel": [PANEL],
        "painted": [],
    }[form]
    return "\n\n".join(parts)


def write_script(path, text):
    path.write_text(text, encoding="utf-8")
    path.chmod(path.stat().st_mode | stat.S_IXUSR | stat.S_IXGRP | stat.S_IXOTH)


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(2)
    spec = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    out = Path(sys.argv[2])
    parallel = int(sys.argv[3]) if len(sys.argv) > 3 else 8
    (out / "requests").mkdir(parents=True, exist_ok=True)
    (out / "raw").mkdir(exist_ok=True)
    records = []
    for item in spec:
        text = request(item)
        (out / "requests" / f"{item['name']}.md").write_text(text + "\n", encoding="utf-8")
        records.append({**item, "request": text})
    (out / "requests.json").write_text(json.dumps(records, ensure_ascii=False, indent=1), encoding="utf-8")
    write_script(out / "gen_one.sh", GEN_ONE.format(out=out.as_posix()))
    write_script(out / "run_all.sh", RUN_ALL.format(out=out.as_posix(), parallel=parallel))
    print(f"{len(records)} requests -> {out}/requests, run {out}/run_all.sh")


if __name__ == "__main__":
    main()
