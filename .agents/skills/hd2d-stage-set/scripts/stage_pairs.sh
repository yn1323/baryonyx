#!/bin/zsh
# stage_shots.sh で撮った2組の画像を、画面ごとに左右へ並べる（左が <前の名前>、右が <後の名前>）。
# 2画面ずつ上下に重ねた1920x1080の画像を <出力フォルダー>/<番号>_<画面>_<画面>.png に書く。
# 全体を縮めると小さな差は見えないため、光源の周りなどは同じ範囲を等倍で切り出して別に比べる
# （例: magick <画像> -crop 700x500+0+0 +repage <切り出し>）。
#
# 使い方: stage_pairs.sh <前の名前> <後の名前> <出力フォルダー> [画面...]
#   画面を省くと、<後の名前> で撮った画面のうち <前の名前> にもあるもの全部。ImageMagick（magick）を使う。
ROOT=${0:A:h:h:h:h:h}
A=$1; B=$2; OUT=${3:A}
[ -z "$OUT" ] && { echo "usage: stage_pairs.sh <before> <after> <out dir> [screen...]"; exit 2; }
shift 3
DIR=$ROOT/client/Assets/DevCaptures/StageShots
SCREENS=("$@")
if [ ${#SCREENS} -eq 0 ]; then
  for f in $DIR/${B}_*.png(N); do
    name=${${f:t:r}#${B}_}
    [ -f $DIR/${A}_$name.png ] && SCREENS+=($name)
  done
fi
mkdir -p $OUT
n=1
while [ ${#SCREENS} -gt 0 ]; do
  first=$SCREENS[1]; second=$SCREENS[2]
  if [ -n "$second" ]; then
    magick \( $DIR/${A}_$first.png $DIR/${B}_$first.png +append \) \( $DIR/${A}_$second.png $DIR/${B}_$second.png +append \) -append -resize 1920x1080 $OUT/${n}_${first}_${second}.png
    echo "$OUT/${n}_${first}_${second}.png"
    SCREENS=(${SCREENS[3,-1]})
  else
    magick $DIR/${A}_$first.png $DIR/${B}_$first.png +append -resize 1920x $OUT/${n}_${first}.png
    echo "$OUT/${n}_${first}.png"
    SCREENS=()
  fi
  n=$((n+1))
done
