---
name: unity-video-recording
description: ユーザーから「この場面を録画して」「この動きの動画を撮って」「スキルの演出を動画で見せて」と頼まれたとき、Unity EditorのPlay中の画面をUnity Recorderで動画（MP4）に撮り、確かめてからユーザーへ送る。デバッグルームの演出、探索の駒の移動、戦闘のスキル、画面の切り替わりなど、動きを動画で見せる依頼に当てはめる。静止画の撮影だけの作業、VFXの見た目を作る・直す作業（vfx-authoring）、実機やエミュレーターでの画面収録には使わない。
---

# Unity Editorの画面を動画で録画する

Play中のGameビューを Unity Recorder で録画し、中身を確かめてからユーザーへ送る。
解像度・形式・保存先は [動画の録画](../../../doc/rules/client-testing.md#動画の録画) を正本とし、ここには写さない。
ユーザーが別の解像度や長さを指定したときは、その指定に従う。

Editorの状態を変える操作は、すべてEditorの札を取ってから行う（[複数のチャットで1台のEditorを使うとき](../../../doc/rules/client-testing.md#複数のチャットで1台のeditorを使うとき)）。
Unity CLIの書き方とEditorの癖は [Unity EditorとCLIの操作・撮影で得た知見](../shared/unity-editor-notes.md) にある。

## 1. 場面と始め方を決める

- 録る場面は、まず [デバッグルーム](../../../doc/features/screens/debug-rooms.md) で再生できるかを探す。ボタン1つで同じ動きを何度でも再生でき、毎回同じ状態から録れる。
- 録る動きを始める処理（メソッドの呼び出し）を、コードから特定する。例：探索の駒の移動は `ExplorationMomentLab.Run(ExplorationMoment.Walk)`。
- 録画に映さない物（デバッグルームの操作盤など）のCanvasの名前を控える。
- 場面がどれか分からないとき、または始める処理がないときは、推測で組まずにユーザーへ聞く。

## 2. Editorを用意する

1. 札を取る。札が `busy` なら、取れるまで待つ。札の取得と次の操作を `;` で1行につながない。
2. `python3 client/ci/run-editor-tests.py --compile-only --token <token>` でコンパイルが通ることを確かめる。エラーが別の作業のものなら、直さずにユーザーへ伝えて待つ。
3. 開いているシーンの名前を控えて、`EditorSceneManager.SaveOpenScenes()` で保存してから、録るシーンを開く。
4. `editor_play` でPlayに入り、`editor_status` が `playing` になったことを確かめる。続けて、`eval` で `Application.runInBackground = true` にする。
5. デバッグルームは、開いた直後に前回の演出を自動で再生する。5秒ほど待ってから録る。

## 3. 録画する

1. [scripts/start_recording.cs](scripts/start_recording.cs) を作業用の場所（スクラッチパッド）へ写す。
2. 台本の「ここを書き換える」の3か所を直す。名前と長さ、隠すCanvas、始める処理の3つで、それ以外は変えない。
   名前は録画ごとに新しくする。`Temp/Recordings/` の古いファイルは消さずに残す（ワイルドカードでの削除は止められる）。
3. `unity --format json command --caller plugin --skill unity-cli eval_file --file <写した台本>` で1回実行する。戻り値が `recording ...` なら録画が始まっている。
4. `client/Temp/Recordings/<名前>.done` ができるまで待つ。中身が `stopped early` なら、途中でPlayが止まっている。
5. `ffprobe` で、MP4の解像度・コマ数・長さが正本どおりかを確かめる。

録画を止めずにPlayを抜けると、MP4の終わりの情報が書かれず再生できないことがある。台本は時間がたつと自分で止まるので、`.done` ができる前にPlayを止めない。

## 4. 中身を確かめる

コマを並べた画像を小さく作って、1回だけ見る。
動画そのものはClaudeが読まないため、使用量に効くのはこの確認画像である。大きな画像やコマの多い画像を作らない。

```bash
ffmpeg -v error -y -i <録画.mp4> -vf "select='not(mod(n\,13))',scale=240:-1,tile=3x3" -vsync vfr -frames:v 1 <スクラッチパッド>/sheet.png
```

- 見せたい動きが写っているか、映さない物が消えているか、画面の端で切れていないかを見る。
- 冒頭の切り替わりや、動きの後の止まった時間が長いときは、`ffmpeg -ss <開始秒> -t <長さ> -i <録画.mp4> -c:v libx264 -crf 23 -pix_fmt yuv420p -movflags +faststart <出力.mp4>` で切り出す。
- 撮り直すときは、台本の名前を変えて手順3からやり直す。

## 5. 片付けて送る

1. `editor_stop` でPlayを止め、`playMode` が `stopped` になってから、控えておいたシーンを開き直す。Recorderが変えたGameビューの大きさを、正本どおりに戻す（`PlayModeWindow.GetRenderingResolution` で確かめる）。
2. 札を返す。失敗して途中でやめたときも返す。
3. `SendUserFile` で動画を送る（`display: "render"`）。添える一言には、場面・解像度・長さを書く。
4. 報告には、録った場面と始めた処理、切り落とした部分、元に戻していない設定を書く。

## 気を付けること

- 隠す物は Canvas の `enabled` だけを切る。GameObject ごと無効にすると、そこに付いた部品（デバッグルームの操作）の処理やコルーチンまで止まる。
- 録画中、ゲームの時間は1コマ1/30秒で進む。実時間で進む演出は、実際と違う速さで写ることがある。速さが気になる動画では、そのことを報告に書く（[動画の録画](../../../doc/rules/client-testing.md#動画の録画)）。
- `eval` で足した物や変えた値は、Playを止めても残ることがある。録画のあとで戻ったかを確かめる。
