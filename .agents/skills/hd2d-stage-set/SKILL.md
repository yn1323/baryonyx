---
name: hd2d-stage-set
description: >
  3Dの地面と背景にドット絵のキャラを立たせる3Dの舞台（HD-2D）を作る・直す・レビューするときに必ず使う。
  カメラとその動き、キャラの板と影、昼・夜・夕暮れの光、霧、光芒と光だまり、舞台のレンズ（被写界深度）と色調、停止中とSceneタブの表示、
  地面・遠景・書き割り・小物・草・汚しとそのマテリアル・テクスチャ、密度（ごちゃごちゃ具合）、配置の計算、撮影での点検を扱う。
  背景が寂しい・平ら・作り物っぽい・光が足りない、新しい場所や画面を3Dの舞台で組む、といった依頼に当てはめる。
  技・被弾・報酬などのエフェクトは vfx-authoring、3Dの舞台がない描いた2D背景の演出は hd2d-lighting-vfx の担当とする。
---

# 3Dの舞台（HD-2D）を作る

3Dの地面と背景にドット絵のキャラを立たせ、3Dの光と影とカメラで見せる舞台を作り、HD-2Dらしい密度とリアルな見え方にするためのスキルである。
値と配置の正本は[3Dの舞台の仕様](../../../doc/art/hd2d-stage.md)、部品と実装の対応は仕様の「実装との対応」とし、このスキルには写さない。
どの画面が3Dの舞台かは、`client/Assets/Baryonyx/App/Scenes/` のシーンに `Hd2dStageCamera` があるかで確かめる。

| 参照文書 | 扱うこと |
|---|---|
| [camera-light.md](references/camera-light.md) | カメラと配置の計算、キャラの板と影、描く順番、炎とライト、昼・夜・夕暮れの光、光芒と光だまり、レンズと色、カメラの動き、自作シェーダーとRender Graph、停止中とSceneタブの表示 |
| [stage-building.md](references/stage-building.md) | 床・壁・遠景・書き割りの組み方、マテリアルの選び方、テクスチャの作り方、法線マップ、草と葉のシェーダー |
| [set-dressing.md](references/set-dressing.md) | 小物・草の房・地面の汚し・足元の陰の密度の決め方、置き方の手順、撮影での点検 |
| [Unity EditorとCLIの知見](../shared/unity-editor-notes.md) | Editorの状態、CLIのコマンドの時間切れ、撮影の方法、テストの注意 |
| [HD-2Dの調査資料](../hd2d-lighting-vfx/references/research-sources.md) | オクトパストラベラーの開発者の発言、再現例、光と炎の資料 |

## 最初に確認すること

1. 対象の画面の舞台と、その組み立てのコード（舞台ごとの生成処理と小物の配置処理）を読む。仕様の「画面ごとの構成」と「小物と汚し」で、今ある物と置き方の決まりを確かめる。
2. カメラの位置・見下ろし角・縦の画角、主光源の向き、点光源の位置を読む。
3. Editorの状態を確かめる。Enter Play Mode Optionsでシーンの再読み込みを省いているか（省くと、停止中に起きた部品はPlay Modeで `Awake` が呼ばれない）、EditorがPC用とモバイル用のどちらのURP設定で描いているか（`GraphicsSettings.currentRenderPipeline`。影の柔らかさなどが違う）、その画面に効くVolumeとProfileはどれかを確かめる。Unityの版は `client/ProjectSettings/ProjectVersion.txt`、URPの版は `client/Packages/manifest.json` で確かめ、記憶で断定しない。
4. 停止中のシーンを撮り（`capture_game_view`）、Play Modeで手前のUIごと撮る（`capture_game_view --source screen`）。どこが平らで、どこが暗く、どこが休ませる所かを画面で見る。
5. Editorの状態を変える操作は、[複数のチャットで1台のEditorを使うとき](../../../doc/rules/client-testing.md#複数のチャットで1台のeditorを使うとき)の札を取ってから行う。

## 作業の流れ

1. **直す層を決める**：カメラ・光・レンズの問題か、物と材質の問題か、密度の問題かを分ける。光が足りず平らに見えるなら [camera-light.md](references/camera-light.md) の光芒と光だまり・昼の光・夜と夕暮れの屋外、物が寂しいなら [set-dressing.md](references/set-dressing.md) のごちゃごちゃ具合の決め方と画面の帯ごとの置き場に沿って、休ませる所を先に決め、何を足すかを決める。
2. **物の一覧を作る**：場所ごとに大・中・小・面の物と、その場で起きたことを見せる物を選び、物ごとに大きさ（m）・作り方・マテリアルを決める（[マテリアルの選び方](references/stage-building.md#マテリアルの選び方)）。
3. **テクスチャを作る**：[テクスチャの作り方](references/stage-building.md#テクスチャの作り方)に従い、[stage_requests.py](scripts/stage_requests.py) で依頼文を作ってCodex CLIで生成し、[fit_textures.py](scripts/fit_textures.py) で縮めて、`.aseprite` にする。[pixel-art-standards](../pixel-art-standards/SKILL.md) と一緒に使う。
4. **配置を計算する**：カメラの位置、手前の物、ピントの範囲、ドットの細かさ、描いた遠景の月、光芒の上端は、撮影の繰り返しで探さず、[配置を計算で決める](references/camera-light.md#配置を計算で決める)と [stage_view.py](scripts/stage_view.py) で先に求め、画面に収まり、キャラの絵に重ならないかを確かめる。
5. **組む**：舞台の生成処理と小物の配置処理に、物・草・汚し・陰・光・Profileを書く。光を受ける面には法線マップを付け、草と葉は草と葉のシェーダーにする（[法線マップ](references/stage-building.md#法線マップ)、[草と葉のシェーダー](references/stage-building.md#草と葉のシェーダー)）。自作のURPシェーダーとRender Graphのパスは、[自作シェーダーとRender Graphの注意](references/camera-light.md#自作シェーダーとrender-graphの注意)を読んでから書く。決めた値はコードに書き、Inspectorで直さない。
6. **作り直して撮る**：開いているシーンを閉じてから舞台を作り直し、関係する画面を停止中とPlay Modeで撮る。[camera-light.md の検証](references/camera-light.md#検証)と[set-dressing.md の撮影での点検](references/set-dressing.md#撮影での点検)の項目で確かめ、直して撮り直す。
7. **テストと展示室**：小物がキャラの足元を避けていること、光を受ける材質に法線マップがあることをEditModeテストで確かめ、関係する画面のPlayModeテストを実行する（[作業中に実行するPlayModeテスト](../../../doc/rules/client-testing.md#作業中に実行するplaymodeテスト)）。展示室はルートの AGENTS.md の[クライアントアセット展示室](../../../AGENTS.md#クライアントアセット展示室)に従って更新し、新しいテクスチャ・マテリアル・シェーダー・Prefabが一覧に入ったことを確かめる。
8. **文書を更新する**：仕様の該当する節（画面ごとの構成、光と炎、レンズと色、小物と汚し、テクスチャの表）と「変更と判断の記録」、展示室の仕様、`client/ArtSource/Stages/prompts.json` を同じ変更で直す。作業で得た手順や注意はこのスキルの参照文書へ、守るべき規則や値は仕様へ書く（[docとスキルの使い分け](../../../doc/rules/documentation-policy.md#docとスキルの使い分け)）。

## よくある失敗

撮影で何度も見つかった失敗である。
詳しい原因と直し方は参照文書に書く。

- 光が足りず平らに見える（光芒と光だまりがなく、光の当たる場所が分からない）
- キャラの影が見えない（主光源の向きでHPバーやキャラ自身の板に隠れる、ソフトシャドウで薄い）
- 逆光で崖や門が黒い影絵になる
- 小物が黒い塊に見える（主光源の影の中、暗く描かれた絵、裏からしか光が当たらない板）
- 草・枯れ枝・剣がかたまりに見える（ドットが粗すぎて細い線が消えた）
- ぼけた遠くに、足元の陰がくっきりした線として浮く
- 手前の物が手前のUIに隠れる、または画面の下端でぼけた暗い帯になる
- 開けた所の彩度の高い汚しが、しみに見える
- 置いたつもりの物・月・光芒が画面の外や描いた遠景の奥にあって写らない（計算せずに置いた）
- 停止中の表示を変えたら、シーンが変更扱いになった、またはPrefabの上書きが増えた

## 道具

| スクリプト | 使いどころ |
|---|---|
| [stage_view.py](scripts/stage_view.py) | 世界の点の画面の位置、画面の点の地面の位置、箱の画面の範囲を求める |
| [stage_requests.py](scripts/stage_requests.py) | Codex CLIへの依頼文を固定の段落と物の説明から組み立て、並行生成のスクリプトを書き出す |
| [fit_textures.py](scripts/fit_textures.py) | 物の範囲の縦横比を測り、大きさ（m）とドットの細かさから寸法を決めて変換する |
| [stage_texture.py](scripts/stage_texture.py) | 面積平均で格子を作り直し、減色する（fit_textures.py が呼ぶ） |

## 完了報告

次を日本語で報告する。

- 画面ごとに変えたカメラ・光・レンズと色、足した物と、休ませる所として空けた場所
- 新しいテクスチャ（生成した枚数、ドットの細かさを変えた物）とマテリアル・シェーダーの種類
- 撮影で見つけて直した問題
- テスト・展示室・文書の更新と、その結果
- 確かめていないこと（Android端末での負荷、スマートフォン用の描画設定での見え方、展示室のプレビューの見た目など）
- ユーザーの判断が要ること（規格からの例外、PNGで置いた生成データなど）
