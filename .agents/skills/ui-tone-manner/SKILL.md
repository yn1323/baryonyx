---
name: ui-tone-manner
description: >
  画面・窓（ダイアログ・モーダル）・決めるボタン・分類の切り替え・持ち物の札を作る・直す・レビューするとき、
  画面どうしの見た目（トンマナ）をそろえるときに使う。新しい画面やパネル、窓を足すとき、既存の画面を共通の部品へ置き換えるとき、
  「この画面だけ雰囲気が違う」「窓の形がばらばら」「横のタブは押しづらい」「全画面を撮って見比べて」という依頼にも当てはめる。
  画面の構成やUX全般の判断は ui-advisor、読み込みの待ちの見せ方は loading-concealment、演出の見た目は vfx-authoring の担当とする。
---

# UIのトンマナをそろえる

画面ごとに窓・ボタン・分類・札を作り分けず、共通の部品から組んで、どの画面も同じ見た目と押し方にする進め方を扱う。
画面の型、窓の種類、決めるボタン、分類、持ち物の札、読み込み中と失敗の出し方の正本は [共通の部品](../../../doc/rules/ui-design.md#共通の部品) であり、このスキルには写さない。
色・文字の大きさ・余白の値の正本は、[基本の値](../../../doc/rules/ui-design.md#基本の値)の定数（`UiPalette`・`UiType`・`UiSpace`）である。

## 使う場面

- 新しい画面・パネル・窓を足すとき
- 既存の画面に、窓・ボタン・分類・持ち物の一覧を足す・直すとき
- 「この画面だけ違う」「窓・ボタンがばらばら」と言われたとき、全画面のトンマナを見直すとき
- 画面の生成処理（`*Assets.cs`）を含む差分をレビューするとき

## 手順

1. **テンプレートを選ぶ**：作る画面が [画面の型](../../../doc/rules/ui-design.md#画面の型) のどれ（A〜F、何かを1つ選ばせるなら「1つを選ぶ画面」）かを決め、そのテンプレートから骨組みを作る。窓なら [窓](../../../doc/rules/ui-design.md#窓) の表のどれか、ボタンなら [決めるボタン](../../../doc/rules/ui-design.md#決めるボタン) のどれかを先に決める。どれにも当たらないときは、作る前にユーザーに見本を見せて相談する。
2. **見本の画面を見る**：同じ型の今ある画面を、停止中の表示の切り替えで撮って見る（下の「撮って見比べる」）。新しい形を考える前に、見本に合わせる。
3. **共通の部品で組む**：テンプレートが返した場所に、下の「使う部品」で中身を入れる。色・文字の大きさ・余白は基本の値から選ぶ。暗い幕の色・窓の縁・×・題・決めるボタン・縦の分類を機能の中で作り直さない。部品に足りない形が要るときは、部品の側に引数を足して、ほかの画面も使えるようにする。
4. **撮って見比べる**：作った画面と見本の画面を、同じ大きさで並べた1枚にし、違いを確かめる。前の版があれば、前と後も並べる。
5. **確かめる**：下の「確かめること」を行う。
6. **記録する**：画面の仕様に見た目を書き、共通の部品に当たる所は [共通の部品](../../../doc/rules/ui-design.md#共通の部品) へのリンクにする。決まりを足した・変えたときは、正本の節を同じ変更で直す。
7. **報告する**：どの画面をどの見本に合わせたか、前と後の画像、合わせなかった所とその理由、確かめていないことを分けて書く。合わせ方の判断が分かれるときは、作業の前に「どの画面をどの画面に合わせるか」の対応表を見せて了承を得る（ユーザーの希望）。

## 使う部品

部品は、基本の値・部品・テンプレートの3つの層でできている（[共通の部品](../../../doc/rules/ui-design.md#共通の部品)）。

| 層 | 形 | 部品 | 例 |
|---|---|---|---|
| 基本の値 | 色・文字の大きさ・余白と大きさ | `UiPalette`（役割の名前）・`UiType`・`UiSpace` | `UiPalette.Veil`・`UiType.Body`・`UiSpace.Edge` |
| テンプレート | 格子と詳細（A） | `GuidePanelTemplate.GridDetail` | 商会の買う・売る、ライブラリの各分類 |
| テンプレート | 行と詳細（B） | `GuidePanelTemplate.RowsDetail` | 商会の合成・品質強化 |
| テンプレート | 全幅の一覧（C） | `GuidePanelTemplate.FullList` | 商会のスキル覚醒、ライブラリの日々の歩数 |
| テンプレート | 冒険者のシート（D） | 分け目は `UiSpace.SheetSplit` か `SheetSplitWide` | 育成・装備・突破／スキル |
| テンプレート | 窓（E） | `GuideFrameAssets.MessageWindow`・`ListWindow`・`CenteredWindow`（幅は `WindowSize`） | ダイアログと連携、お宝、レベルアップ |
| 部品 | 窓の縁 | `GuideFrameAssets.WindowFrame`・`Window` | [AdventureResultAssets](../../../client/Assets/Baryonyx/Features/Adventure/Editor/AdventureResultAssets.cs) の冒険の結果、地図の出来事の窓 |
| 部品 | 知らせる窓 | [GameDialogAssets](../../../client/Assets/Baryonyx/Shared/UI/Dialog/Editor/GameDialogAssets.cs) と [GameDialog](../../../client/Assets/Baryonyx/Shared/UI/Dialog/GameDialog.cs) | 冒険のメニュー、全滅、失敗 |
| 部品 | 窓のボタン | `GuideFrameAssets.WindowButton`（決める方は `primary: true`。並べる窓では大きさだけを渡す版）と `ButtonRow` | レベルアップ、もう一度、連携のモーダル |
| 部品 | × | `GuideFrameAssets.CloseButton`（置いた矩形に作るときは `CloseMark`） | 一覧の窓、戦闘の山札 |
| 部品 | 外す札の面 | `GuideFrameAssets.RemoveFace` | 編成のスキル、冒険者の一覧 |
| 部品 | 縦の分類 | `GuideFrameAssets.SideTabs`（準備中は `locked`） | 商会、ライブラリの装備 |
| 部品 | 状態の文字 | `GuideFrameAssets.ListStatus` | 一覧の読み込み・失敗・空 |
| 部品 | ルーンの数 | `GuideFrameAssets.RuneAmount` | 値段、報酬、レベルアップの費用 |
| 部品 | パネルの上の帯 | `GuideFrameAssets.FramedPanel` | 商会・編成・ライブラリの各項目 |
| 部品 | 持ち物の札 | 型は `ItemTile` の1つ。品質・数・右上の印・「？」は `ItemTileAssets` | 商会・ライブラリ・お宝・召喚・冒険の結果の札 |
| 部品 | 読み込み中 | 札の裏面（`GlassCardAssets.AddBack`） | 商会・ライブラリ・編成の一覧 |

## 判断の観点

作るときとレビューのときに確かめる。

- [ ] 画面の生成処理の中で、色・文字の大きさ・窓の幅・分け目の新しい値を決めていない（基本の値から選んでいる）。
- [ ] 一覧の画面をテンプレートから作っている。テンプレートにない並びが要るときは、テンプレートの側に足している。
- [ ] 幕・窓・×が[窓](../../../doc/rules/ui-design.md#窓)のとおりで、機能の中で別の色や形の窓を作っていない。
- [ ] 1つの画面の決めるボタンが、[決めるボタン](../../../doc/rules/ui-design.md#決めるボタン)の1つの形にそろっている。
- [ ] 分類が[分類の切り替え](../../../doc/rules/ui-design.md#分類の切り替え)のとおりに並んでいる。
- [ ] 持ち物の札の中身の位置と、まだ見つけていない物の伏せ方が[持ち物の札](../../../doc/rules/ui-design.md#持ち物の札)のとおりである。
- [ ] 説明の小見出しや注釈を足さず、配置で意味が分かる（[AGENTS.md](../../../AGENTS.md)）。
- [ ] 同じ見た目の生成処理を、機能の中に書き写していない。
- [ ] 停止中の表示の切り替えに、足した窓・状態が入っている（[停止中の表示の切り替え](../../../doc/rules/ui-design.md#停止中の表示の切り替え)）。

## 撮って見比べる

- 停止中の表示の切り替え（`IEditModePreview`）で、状態ごとに撮る。撮り方とつまずきは [Unity EditorとCLIの知見](../shared/unity-editor-notes.md) にある。Play中にしか作られない画面（世界地図など）は、Play中に撮る。
- 全画面を見直すときは、[eval/inventory.cs](scripts/eval/inventory.cs) に並べたシーン（`App/Scenes/Debug/` を除く）を開き、`IEditModePreview` を持つ部品の状態をすべて撮って、シーンごとに4列の一覧画像にする。窓・パネル・決めるボタンは、型ごとに並べた見比べ用の画像も作る。
- 撮影の前に、Editorの日本語が化けていないかを確かめる。化けていたら、フォントと影付きの材質を読み直してから撮る（[フォントのメモリだけの字化け](../shared/unity-editor-notes.md#フォントのメモリだけの字化け)）。
- 撮影のあとは、`git status` で撮影の副作用（シーン・フォント・`ProjectSettings` の差分）を確かめて戻す。

### 撮影の台本（scripts/）

作業用の置き場は `output/ui-audit/work/`（gitに入れない）である。
Unityの評価（`eval/*.cs`）は `unity --format json command --caller plugin --skill unity-cli eval_file --file <台本>` で呼び、呼ぶ前に `client/ci/editor-lock.py` で札を取る。
評価の応答が時間切れになったときの扱いは[Unity CLIのコマンド](../shared/unity-editor-notes.md#unity-cliのコマンド)に従う。

| 順 | 台本 | すること |
|---|---|---|
| 1 | [eval/font-reload.cs](scripts/eval/font-reload.cs) | 字化けを直す。結果の「覚」の位置が `KiwiMaru.asset` の値と同じかを見る |
| 2 | [eval/inventory.cs](scripts/eval/inventory.cs) | ラボ以外のシーンの、停止中の表示の状態を `work/inventory.txt` に書く |
| 3 | [make_specs.py](scripts/make_specs.py) | 撮る状態の一覧 `work/specs.txt` を作る。画像の名前は「シーン-部品-状態の番号」で、撮り直しても変わらない |
| 4 | [capture.sh](scripts/capture.sh) `0 999`（範囲は specs.txt の0から数えた行番号） | 状態を1つずつ表示して撮り、`work/shots/` に置く（[eval/show.cs](scripts/eval/show.cs) と [eval/shot.cs](scripts/eval/shot.cs) を交互に呼ぶ）。全部で約30分 |
| 5 | [eval/play-enter.cs](scripts/eval/play-enter.cs) → [play-step.cs](scripts/eval/play-step.cs) と [play-shot.cs](scripts/eval/play-shot.cs) → [play-exit.cs](scripts/eval/play-exit.cs) | Play中にしか作られない世界地図を撮る。`work/play-step.txt` に `open|verde`・`event|Meet|0`・`hide`・`toll` を書いて呼び、`work/play-name.txt` に画像の名前を書いて撮る |
| 6 | [sheets.py](scripts/sheets.py) | シーンごとの4列の一覧 `work/sheets/` を作る |
| 7 | [before_after.py](scripts/before_after.py) `前のフォルダー 後のフォルダー` | 画素が違う画面だけ、前と後を左右に並べた画像を `work/before-after/` に作る |

撮影の台本は、最初に作業中のシーン（名前のあるもの）だけを保存し（[eval/prepare.cs](scripts/eval/prepare.cs)）、撮影中はシーンを保存せずに替える。
停止中の表示はシーンにPrefabの古い上書き（窓の高さ0、アンカー0など）を残すことがあり、保存するとそれがシーンに入るためである。
名前のないシーンの扱いは [client/AGENTS.md](../../../client/AGENTS.md#unityの操作と検証) に従い、生成の評価のあとは名前のあるシーンを開いておく。
画面を組み直したあとは、組み直した画面を含むシーンを撮り直してから前後を並べる（ホームはPrefabだけを組み直すとシーンの上書きと合わなくなることがある）。

## 確かめること

- **見た目**：変えた画面と見本の画面を並べた画像、前と後を並べた画像を作って確かめ、ユーザーに送る。
- **展示室**：変えた画面のPrefabを展示室で開き、停止中の表示の切り替えで同じ見た目になっていることを確かめる（[クライアントアセット展示室](../../../AGENTS.md#クライアントアセット展示室)）。
- **テスト**：変えた画面のPlayModeテストを、[作業中に実行するPlayModeテスト](../../../doc/rules/client-testing.md#作業中に実行するplaymodeテスト) に従って実行する。分類やボタンの部品を差し替えたときは、それを押すテスト（編成・ライブラリ・商会・案内所）を含める。
- **実機**：文字の大きさと押しやすさはEditorだけでは分からない。確かめていなければ未確認として報告する。
