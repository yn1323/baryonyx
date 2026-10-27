#!/bin/sh
# 使い方: run_tiles.sh <区画のフォルダー> <依頼文> <横> <縦>
W=/private/tmp/claude-501/-Users-natani-work-baryonyx/ced0e0b1-9d4d-4dc6-95a8-0e9a5189147c/scratchpad/wide
D="$1"; REQ="$2"; C="$3"; R="$4"
cd /Users/natani/work/baryonyx
todo=$(cd "$D"; j=0; while [ $j -lt $R ]; do i=0; while [ $i -lt $C ]; do k=$(printf "%02d%02d" $j $i); [ -f "tile_$k.png" ] || printf "$k "; i=$((i+1)); done; j=$((j+1)); done)
for round in 1 2 3; do
  [ -z "$todo" ] && break
  echo "$D round $round: $(echo $todo | wc -w) tiles"
  echo $todo | tr ' ' '\n' | grep . | xargs -P ${PARALLEL:-5} -I{} $W/gen_generic.sh "$D" "$REQ" {} > /dev/null
  if [ $round = 3 ]; then todo=$($W/venv/bin/python $W/verify2.py "$D" $C $R last); else todo=$($W/venv/bin/python $W/verify2.py "$D" $C $R); fi
  echo "left: $todo"
done
