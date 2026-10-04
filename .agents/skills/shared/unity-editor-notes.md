# Unity EditorとCLIの操作・撮影で得た知見

[hd2d-stage-set](../hd2d-stage-set/SKILL.md)・[vfx-authoring](../vfx-authoring/SKILL.md)・[hd2d-lighting-vfx](../hd2d-lighting-vfx/SKILL.md) の作業で、Unity CLIから接続したEditorを操作・撮影したときにつまずいた点と、その避け方である。
確かめたのは Unity `6000.6.0f1` のEditorで、版が変わったら挙動を確かめ直す。

Editorの札、テストの実行と件数の確かめ方、未保存のシーンの扱い、撮影画像の保存先はこの文書に写さない。
それぞれ[UnityのテストとCI](../../../doc/rules/client-testing.md)と [client/AGENTS.md](../../../client/AGENTS.md) の「Unityの操作と検証」に従う。

## Editorの状態

**Editorが最前面にないと時間が進まない。**
CLIからPlay Modeに入っても、Editorが背面にあるとフレームが進まず、2回撮影しても粒子の位置まで同じ画像になった。
停止中の `[ExecuteAlways]` の `Update` も呼ばれず、停止中のプレビュー用の子オブジェクトが作られていないように見えた。
`EditorApplication.delayCall` に積んだ処理も、Editorが背面にある間たまり続けた（コマンドから数えて77件）。`EditorApplication.QueuePlayerLoopUpdate` を呼んでも `Update` は呼ばれなかった。

- 撮影の前にEditorを前面に出す。
- Play Modeでは `editor_pause` で一時停止し、`EditorApplication.Step()` で1フレームずつ進めてから撮影する。Editorが背面のまま進めるときは、`Step` をリフレクションで固定の刻みで呼ぶ。
- 動きの確認は、フレームを進めながらUVのずれや透明度を読み出し、値が設定どおりに変わることで確かめる。
- 停止中の表示やシーンを開いたときの処理は、イベントの中ですぐ行う。確かめるときは、各部品の `Update`、またはInspectorの変更時と同じ再構築のメソッド（`RebuildLayers` など）を直接呼ぶ。

**Play Modeの止め方と次の操作の間をあける。**
Play Modeを止めた直後に `EditorSceneManager.OpenScene` を呼ぶと、Play Mode中として拒否されることがある。
`editor_status` の `playMode` が `stopped` になってから操作する。
作業の途中で利用者がEditorをPlay Modeにしていることもある。止めてから作業し、止めたことを利用者に伝える。

**作り直すシーンを開いたままでは、シーンの作り直しが止まる。**
`ScreenScenes.Rebuild` は、対象のシーンが開いていると「Close Home.unity before rebuilding it」で止まる。
開いているシーンに変更があれば保存し、別の保存済みのシーンを開いてから作り直す。

**一時シーンで確かめるときは、元のシーンを閉じることになる。**
保存しない新しいシーン（`NewScene` の `Single`）を開くと、開いていたシーンが閉じる。
開く前に未保存の変更がないことを確かめ、終わったら元のシーンを開き直す。
保存しないシーンを開いたままでは、別のシーンを加算で作り直す処理（`ScreenScenes.Rebuild`）が「untitled scene unsaved」で止まる。作り直す前に保存済みのシーンを開く。

## Unity CLIのコマンド

**Editorが忙しいとコマンドが時間切れになるが、処理は続くことがある。**
コンパイルや読み込みの直後、または時間がかかる処理（舞台の作り直しとシーンへの配置）を `eval` で送ると、「Main thread operation timed out after 5000ms」の応答が返った。
応答が時間切れでも、処理はEditorの中で続いて完了していることが多い。
ただし、再コンパイルの直後に送った1回は実行されず、古いPrefabのまま撮影していた。
処理の最後に印のファイルを書かせて終わりを待つか、生成物の更新時刻や中身（Prefabに部品の名前があるかなど）で、実行されたかを確かめてから次へ進む。

**`AssetDatabase.Refresh` の直後の `editor_status` は、コンパイル前の「ready」を返す。**
スクリプトを変えて読み込み直した直後に状態を見ると、まだコンパイルが始まっておらず「ready」だった。
十数秒待ってから状態を見直し、`typeof(新しい型)` を返す `eval` で、新しいコードが読み込まれたことを確かめる。

**コンソールの記録には古いエラーも残る。**
`console` は過去のエントリーも返すため、作業の前に最後の番号（seq）を控え、それより後のエントリーだけを見る。
コマンドの時間切れの記録と、カメラの撮影時に1度だけ出たGPU Resident Drawerのエラー（「A BatchDrawCommand was submitted with an invalid Batch」）は、ゲームのコードとは関係がなかった。

## 撮影

| 撮り方 | 写るもの | 使いどころ |
|---|---|---|
| `capture_game_view`（カメラ） | 指定したカメラと、そのカメラで描くCanvas。手前のUI（Overlay）は写らない | 停止中の3Dの舞台とキャラ、ぼけ、色を確かめる |
| `capture_game_view --source screen` | 手前のUIを含む画面全体 | Play Mode中だけ使える。`--width`・`--height` を付けるとGameビューの解像度が変わるため、同じ指定で2回撮り、2回目を使う |
| 手前のCanvasを一時的にカメラで描かせて撮る | 停止中の画面全体（手前のUIにもポストプロセスが掛かる） | 停止中とPlay Mode中の見た目を比べる。撮ったらすぐ戻し、シーンが変更扱いになっていないことを確かめる |
| `capture_scene_view` | Sceneタブのカメラの絵と、カメラで描くCanvas | Sceneタブの見た目を確かめる。視点を合わせたあと、描き直しを待ってから撮る |

- **解像度を指定した撮影では、カメラ側のCanvasの再レイアウトが1フレーム遅れる**：`capture_game_view` に幅と高さを指定すると、`Screen Space - Camera` のCanvasだけが直前の解像度のまま描かれ、背景が拡大されて端が切れた画像になった。同じ解像度で一度撮影し、数フレーム進めてから撮り直す。実機では画面の大きさが変わらないため、撮影時だけの現象である。
- **撮影した画像の一部が赤い**：新しいシェーダーのバリアントのコンパイル中は、その物が赤一色で描かれた。数秒待って撮り直す。
- GameビューのRender Texture（`PlayModeView.m_TargetTexture`）をリフレクションで描かせて読む方法も試したが、Gameビュー自身の描画の外では黒い画像になった。
- 一時カメラやRender Textureを作って撮ったときは、`finally` で必ず破棄する。カメラが残るとGameビューを塗りつぶす。

**変化が小さい効果は、切り出して並べて比べる。**
見た目の変化が小さい効果（手前と奥のぼけ、弱い光など）は、全体を縮小した比較では見分けられなかった。
変更前後の撮影から、同じ範囲（光源の周り、画面の下半分など）を元の解像度で切り出して並べて比べる。

## テストの実行

テストの選び方、絞り込みの指定、0件のときのやり直し、完了と件数の確かめ方は、[UnityのテストとCI](../../../doc/rules/client-testing.md)に従う。
そのうえで、次のことに気を付ける。

- `run_tests` の `--filter` に「A|B」のような正規表現を渡すと、0件になった。クラスごとに実行する。
- コマンドの出力に含まれる結果のJSONは、件数が多いと途中で切れて読めなかった。結果は `test_status` か `Temp/pipeline_test_status.json` から読む。
- PlayModeテストの実行中は、`Features/*/Tests/PlayMode/Resources` がGitの未追跡ファイルとして現れ、テスト終了時に片付けられる。コミットの対象に含めない。

## ファイルの扱い

`client/AGENTS.md`・`.agents/skills/shared/scripts/codex_image.py` など、改行がCRLFのファイルがある。`git ls-files --eol <ファイル>` で確かめる。
スクリプトで書き換えるときはバイト列で読み、改行をCRLFのまま書き戻す。LFに変えると、全行が差分になる。
