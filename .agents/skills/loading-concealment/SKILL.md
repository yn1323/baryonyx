---
name: loading-concealment
description: >
  画面の切り替え・画面を開く・操作したときの読み込みの待ち時間を、プレイヤーに見せない形を選んで作る・直す・レビューするときに使う。
  シーンの切り替えの幕（暗転・シャッター）、サーバーからの読み込み、保存の待ち、一覧の初回表示を扱い、
  「一瞬初期値が見える」「一瞬だけ駒しか出ない」「開くまで遅い」「読み込み中の文字が出る」「待たされる」という指摘、
  新しい画面やシーンを足すとき、楽観的更新・前回の内容の先出し・幕を閉じたまま待つ・カードを伏せる、のどれにするか迷うときにも当てはめる。
  保存先の決定は data-storage、APIの往復を減らすサーバー側の作業は server-api-authoring、演出の見た目そのものは vfx-authoring の担当とする。
---

# 読み込みの待ち時間を見せない

画面を開くとき・操作したときに生じる待ち時間を、プレイヤーから見えない形にする進め方を扱う。
どの形をどの順に選ぶか、守ること、各画面の決まりの正本は[読み込みの待ち時間を見せない](../../../doc/rules/ui-design.md#読み込みの待ち時間を見せない)であり、このスキルには写さない。
Webで調べた一般的な目安と根拠は [references/research-sources.md](references/research-sources.md) にある。

## 使う場面

- 新しいシーン・画面・一覧を足すとき、そこへ移る切り替えを作るとき
- サーバーへの読み込み・保存を、画面を開く処理や操作に足すとき
- 「一瞬〇〇が見える」「開くのが遅い」「読み込み中が出る」と指摘されたとき
- 画面の切り替えや読み込みを含む差分をレビューするとき

## 手順

1. **待ちを書き出す**：何を待っているか（シーンの読み込み、サーバーの読み込み、保存の返事、初めて使う重い絵やシェーダー）と、その結果がないと何が描けないかを並べる。サーバーの往復の時間は、開発用ビルドの計測のログ（[GuidePanelLoad](../../../client/Assets/Baryonyx/Shared/UI/GuideMenu/GuidePanelLoad.cs) が開いてから描き終えるまでを、[ServerApi](../../../client/Assets/Baryonyx/Shared/Networking/ServerApi.cs) が要求ごとの時間を出す）で、描く時間と読む時間を分けて見る。
2. **見えてしまう姿を書き出す**：待っている間に何が見えるかを、初めて開いたときと開き直したときで分けて書く。読む前の既定値、初期の位置、途中まで描いた姿（駒だけ・地図の外の黒）、空白、ちらつきが対象である。指摘があれば、その言葉をそのまま書く。
3. **形を選ぶ**：[正本の表](../../../doc/rules/ui-design.md#読み込みの待ち時間を見せない)を上から順に当てはめ、最初に使える形を選ぶ。待ちを削れる（同時に送る、往復を減らす）なら、隠す前に削る。サーバーの往復を減らすのは [server-api-authoring](../server-api-authoring/SKILL.md) の手順で行う。
4. **今ある部品で作る**：下の「使う部品」から選び、新しい仕組みを足さない。形が表にないときは、作る前にユーザーに確かめる。
5. **確かめる**：下の「確かめること」を行う。
6. **記録する**：その画面の仕様に、待つ間の見え方と上限を書く。表にない新しい形を採用したら、正本の表と例を同じ変更で直す。
7. **報告する**：選んだ形と理由、確かめた通信の状態、確かめていない状態（実機の重さなど）を分けて書く。

## 使う部品

| 形 | 部品 | 使い方の例 |
|---|---|---|
| シーンの切り替え | [SceneLoader](../../../client/Assets/Baryonyx/Shared/UI/SceneTransition/SceneLoader.cs)・[SceneTransitionController](../../../client/Assets/Baryonyx/Shared/UI/SceneTransition/SceneTransitionController.cs) | 遷移元の `Exit` が閉じ方と開き方を決める（[画面遷移の演出](../../../doc/features/screens/top-home.md#画面遷移の演出)） |
| 幕を閉じたまま待つ | `HoldReveal()`（Awakeで呼ぶ）→ 描き終えたら `Reveal()`。上限つきで待つなら `IsCovered` を見て `PlayIn()` | [ExplorationBootstrap](../../../client/Assets/Baryonyx/App/Runtime/ExplorationBootstrap.cs)（描き終えてから開く）、[WorldMapBootstrap](../../../client/Assets/Baryonyx/App/Runtime/WorldMapBootstrap.cs)（3秒の上限） |
| 楽観的更新 | [OptimisticState](../../../client/Assets/Baryonyx/Shared/Networking/OptimisticState.cs)・[SaveQueue](../../../client/Assets/Baryonyx/Shared/Networking/SaveQueue.cs)・[RuneBalance](../../../client/Assets/Baryonyx/Shared/Economy/RuneBalance.cs) の取り置き | 装備の変更、世界地図のマスの支払い |
| 前に読んだ内容の先出し | 各セッションの `Known` と指紋（`Fingerprint`）、[GuidePanel](../../../client/Assets/Baryonyx/Shared/UI/GuideMenu/GuidePanel.cs) の `StartLoad(readAgain: true)` | 編成・装備・ライブラリ |
| カードを伏せてめくる | [GlassCardBack](../../../client/Assets/Baryonyx/Shared/UI/GlassCard/GlassCardBack.cs) | 編成・商会・ライブラリの一覧（[カードの裏面](../../../doc/features/screens/guide.md#カードの裏面)） |

## 判断の観点

作るときとレビューのときに確かめる。

- [ ] 初めて開いたとき、読む前の既定値・仮データ・初期の位置が一瞬も見えない。
- [ ] 開き直したとき、前に読んだ内容を出せるのに、裏面や「読み込み中…」を出していない。
- [ ] 幕を閉じたまま待つなら、上限があり、超えたとき・読めなかったとき・別のシーンへ移るときの幕の動きが決まっている。
- [ ] 演出の間に読むなら、演出が先に終わったときに何を見せるかが決まっている（演出を延ばしたり繰り返したりして待たせない）。
- [ ] 楽観的更新なら、失敗したときに表示を戻して知らせる道がある（[サーバーに置くときの作り方](../../../doc/rules/data-storage.md#サーバーに置くときの作り方)）。
- [ ] 待つ間の表示を1つに絞っている。幕・裏面・文字を重ねて出していない。
- [ ] 待ちを隠しただけで終えず、削れる待ち（直列の読み込み、余分な往復）を削った。

## これまでの指摘と直し方

同じ指摘が来たときの当たりにする。経緯は各仕様の記録にある。

| 指摘 | 原因 | 直し方 |
|---|---|---|
| 世界地図で「一瞬初期値にスポーンしてる」 | 開いてから今いるマスを読み、読み終えて飛んでいた | 読み終えるまで暗転のまま待ち、今いるマスに置いてから開く。3秒を超えたら開いて「読み込み中…」 |
| ダンジョンで「一瞬ポーンだけが表示されてからマスとルートが表示される」 | 幕をすぐ開けて、冒険を読む間に駒だけが見えていた | `HoldReveal` で幕を閉じたまま読み、部屋を描いてから `Reveal`。ホームへ戻るときは開けずに移る |
| 「キャラのスキル表示に1秒ちょっとかかる」 | 開くたびに読み直しを待っていた | 前に読んだ内容をすぐ出して裏で読み直し、変わったときだけ描き直す。互いに依存しない読み込みを同時に送る |
| 「ワールドマップ移動時にルーンの消費が直列になっている」 | 跳ぶ前に支払いの返事を待っていた | 楽観的更新にし、断られた支払いとその後を取り消して駒を戻す |
| 一覧の「読み込み中…」の文字 | 初めて読む間に見せる物がなかった | 見える所をカードの裏で埋め、読み終えたら一斉にめくる |

## 確かめること

- **通信の状態**：Homeのデバッグルームの一覧の「通信」で、ふつう・遅い（1秒）・とても遅い（3秒）・失敗を切り替え、初めて開いたときと開き直したときを見る。とても遅いときは、幕の上限を超えた動きも見る。
- **テスト**：幕を閉じたまま待つ画面は、読み終える前に幕が開いていないことと、上限・失敗で開くことをPlayModeテストで確かめる（例：[WorldMapOpeningTests](../../../client/Assets/Baryonyx/Features/WorldMap/Tests/PlayMode/WorldMapOpeningTests.cs)、[AdventureLabTests](../../../client/Assets/Baryonyx/App/Tests/PlayMode/AdventureLabTests.cs)）。前に読んだ内容の先出しは、EditModeでセッションの読み直しを、PlayModeで画面がすぐ出ることを確かめる（例：[LibrarySessionReadAgainTests](../../../client/Assets/Baryonyx/Features/Library/Tests/EditMode/LibrarySessionReadAgainTests.cs)、[LibraryReadAgainTests](../../../client/Assets/Baryonyx/App/Tests/PlayMode/LibraryReadAgainTests.cs)）。実行は[作業中に実行するPlayModeテスト](../../../doc/rules/client-testing.md#作業中に実行するplaymodeテスト)に従う。
- **見た目**：一瞬の表示はスクリーンショットでは写りにくいため、切り替えの前後を [unity-video-recording](../unity-video-recording/SKILL.md) で録画し、コマ送りで確かめる。
- **実機の重さ**：シーンを読み込んだ直後の重いフレームや、初めて使うシェーダーの引っかかりはEditorでは出にくい。気になるときはユーザーにエミュレーターでの確認を頼み、確かめていなければ未確認として報告する。
