#!/bin/zsh
# work/specs.txt の行を、0から数えた行番号の from〜to の範囲で、1つずつ表示して撮る（work/shots/<画像の名前>.png）。結果は work/run.log。
# Editorの札は15分までしか持てないため、10枚ごとに返して取り直す。撮る前に eval/font-reload.cs で字化けを直しておく。
# 最初に eval/prepare.cs で作業中のシーンだけを保存し、撮影中はシーンを保存せずに替える（停止中の表示の上書きをシーンに残さない）。
# 最後に eval/finish.cs で今のシーンをファイルから開き直し、停止中の表示の変更を捨てる。
# 使い方（リポジトリ直下）: .agents/skills/ui-tone-manner/scripts/capture.sh 0 999
ROOT=${0:A:h:h:h:h:h}
S=${0:A:h}
W=$ROOT/output/ui-audit/work
cd $ROOT/client
from=${1:-0}; to=${2:-999}; i=0
ev() { unity --format json command --caller plugin --skill unity-cli eval_file --file "$1" 2>&1 | grep -o '"result": "[^"]*"\|"message": "[^"]*"' | head -1; }
lock() {
  [ -f $W/token ] && python3 ci/editor-lock.py release $(cat $W/token) >/dev/null 2>&1
  python3 ci/editor-lock.py acquire --owner "ui-capture" --wait 600 --lease 900 | sed -n 's/.*token=\([^ ]*\).*/\1/p' > $W/token
}
lock
echo "prepare | $(ev $S/eval/prepare.cs)" >> $W/run.log
while IFS= read -r line; do
  if [ $i -ge $from ] && [ $i -le $to ]; then
    printf '%s' "$line" > $W/cur.txt
    r1=$(ev $S/eval/show.cs)
    sleep 1
    r2=$(ev $S/eval/shot.cs)
    echo "$i | $r1 | $r2 | ${line##*	}" >> $W/run.log
    [ $((i % 10)) -eq 0 ] && lock
  fi
  i=$((i+1))
done < $W/specs.txt
echo "finish | $(ev $S/eval/finish.cs)" >> $W/run.log
python3 ci/editor-lock.py release $(cat $W/token) >/dev/null 2>&1
echo "done $from-$to" >> $W/run.log
