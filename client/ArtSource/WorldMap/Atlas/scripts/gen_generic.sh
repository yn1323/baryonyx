#!/bin/sh
# 使い方: gen_generic.sh <区画のフォルダー> <依頼文> <区画の番号>
cd /Users/natani/work/baryonyx
D="$1"; REQ="$2"; k="$3"
uv run --no-project python .agents/skills/shared/scripts/codex_image.py --out-dir "$D" --name "tile_$k" --request "$REQ" --image "$D/guide_$k.png" --image-role "描き直す区画の下絵。地形と道の位置をこのまま保つ" --timeout 1800 > "$D/log_$k.txt" 2>&1
echo "$k exit $?"
