#!/bin/zsh
# 3Dの舞台の画面を、停止中のEditorで順に撮る（Top・Home と、Battle シーンの戦闘の背景）。
# 光・色調・光芒を変える前後で同じ名前を付けて撮り、stage_pairs.sh で左右に並べて見比べる。
# 保存先は client/Assets/DevCaptures/StageShots/<名前>_<画面>.png（Gitの管理外）。
#
# 使い方（どのフォルダーからでもよい）:
#   stage_shots.sh <名前> [画面...]
#     画面を省くと全部。Top / Home / 戦闘の背景の名前（BattleStage の列挙名。例: MineTunnel）
#   TRY=<evalするC#のファイル> stage_shots.sh <名前> ...
#     撮る直前に、開いたシーンへそのファイルを eval する（保存しない試し。eval/scale_lights.cs の写しなど）
#
# Editorの札を取ってから撮り、舞台を替えるたびに延ばす。開いていたシーンは、変更があれば保存してから
# 閉じ、終わったら開き直す。撮影のために替えた背景や TRY の値は、シーンを開き直して捨てる。
ROOT=${0:A:h:h:h:h:h}
TAG=$1
[ -z "$TAG" ] && { echo "usage: stage_shots.sh <name> [screen...]"; exit 2; }
shift
ALL=(Top Home DuskHighland MeadowRoad MistyWoods MineTunnel CrystalCavern RockyShore MistySwamp SnowField VolcanoCrater MoonlitGraveyard CastleHall)
SCREENS=("$@")
[ ${#SCREENS} -eq 0 ] && SCREENS=($ALL)
OUT=Assets/DevCaptures/StageShots
SCENES=Assets/Baryonyx/App/Scenes
cd $ROOT/client

u() { unity --format json command --caller plugin --skill unity-cli "$@"; }
ev() { u eval --code "$1" --timeout 20000 2>&1 | grep -o '"result": "[^"]*"\|"message": "[^"]*"' | head -1; }
shot() {
  [ -n "$TRY" ] && echo "  try: $(u eval_file --file "$TRY" --timeout 20000 2>&1 | grep -o '"result": "[^"]*"\|"message": "[^"]*"' | head -1)"
  # 解像度を指定した1枚目は、カメラ側のCanvasの配置が前の解像度のまま写るため、2枚目を使う。
  u capture_game_view --width 1920 --height 1080 --save_path "$OUT/_warm.png" >/dev/null 2>&1
  sleep 1
  u capture_game_view --width 1920 --height 1080 --save_path "$OUT/${TAG}_$1.png" 2>&1 | grep -q '"success": true' && echo "  saved $OUT/${TAG}_$1.png" || echo "  FAILED $1"
}

tok=""
for i in 1 2 3 4 5 6; do
  out=$(python3 ci/editor-lock.py acquire --owner "stage-shots" --wait 80 --lease 600)
  case "$out" in acquired*) tok=$(echo "$out" | sed -n 's/.*token=\([^ ]*\).*/\1/p'); break;; esac
done
[ -z "$tok" ] && { echo "the Editor is busy"; exit 3; }
mkdir -p $OUT
state=$(ev 'if (UnityEditor.EditorApplication.isPlaying) return "playing"; Baryonyx.Editor.EditorScenes.SaveNamed(); return Baryonyx.Editor.EditorScenes.HasUntitledChanges() ? "untitled" : UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;')
orig=$(echo "$state" | sed 's/"result": "\(.*\)"/\1/')
case "$orig" in
  playing|untitled|"") echo "stopped: $state"; python3 ci/editor-lock.py release $tok; exit 4;;
esac

battle=0
for screen in $SCREENS; do
  echo "$screen"
  if [ "$screen" = Top ] || [ "$screen" = Home ]; then
    ev "UnityEditor.SceneManagement.EditorSceneManager.OpenScene(\"$SCENES/$screen.unity\"); return \"ok\";" >/dev/null
    battle=0
    sleep 2
  else
    if [ $battle -eq 0 ]; then
      ev "UnityEditor.SceneManagement.EditorSceneManager.OpenScene(\"$SCENES/Battle.unity\"); return \"ok\";" >/dev/null
      battle=1
      sleep 2
    fi
    r=$(ev "var sel = UnityEngine.Object.FindFirstObjectByType<Baryonyx.Combat.Presentation.BattleStageSelector>(); sel.Stage = Baryonyx.Combat.Presentation.BattleStage.$screen; sel.Apply(); return sel.Shown != null ? \"ok\" : \"none\";")
    case "$r" in *ok*) ;; *) echo "  no such stage: $r"; continue;; esac
    sleep 1
  fi
  shot $screen
  python3 ci/editor-lock.py renew $tok >/dev/null 2>&1
done

# 替えた背景と試しの値を捨てるため、保存せずに元のシーンを開き直す。
ev "UnityEditor.SceneManagement.EditorSceneManager.OpenScene(\"$orig\"); return \"ok\";" >/dev/null
rm -f $OUT/_warm.png $OUT/_warm.png.meta
python3 ci/editor-lock.py release $tok >/dev/null
echo "done: $OUT/${TAG}_*.png"
