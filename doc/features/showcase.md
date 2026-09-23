---
id: client-showcase
type: specification
status: 一部確定
updated: 2026-09-23
---

# クライアントアセット展示室

## 目的

クライアント側で作成したアセットを、ゲームを進行させずに一覧から確認する。

展示室はゲームシーンとして実装し、Unity Editorでシーンを開いてPlay Modeで確認できる。実機でも確認できるよう、Build Settingsへ常時追加して通常のビルドにも含める。

Play Modeを使わずに一覧と基本プレビューを確認するため、Unity Editorの `Baryonyx > Showcase > Open Preview Window` も提供する。このウィンドウはカタログを左側の一覧に表示し、画像、Prefab、マテリアルなどのEditorプレビューと、元アセットの場所を確認できる。音声の再生やシーンの実行時表示など、ゲーム内の挙動が必要な確認は展示室シーンで行う。

UnityのGameタブは、シーンに保存されたカメラやUIであれば停止中にも確認できる。ただし展示室の一覧UIは `Start` でカタログから動的生成するため、現在のGameタブにはPlay Mode前の一覧は表示されない。停止中の一覧確認はPreview Window、ゲーム内とビルドの確認は展示室シーンという役割分担にする。

## 一覧の分類

カタログは次の分類を持つ。

- 画像：背景、キャラクター、武器、アイテム、アイコン、タイル、ポートレート
- キャラクター
- 背景・環境
- 武器
- アイテム
- VFX・エフェクト
- アニメーション
- 音声・音楽
- UI
- シーン
- マテリアル・シェーダー
- データ
- その他

分類はアセットのパスと種類から自動推定する。

## 表示と操作

展示室にはカテゴリ一覧、アセット一覧、プレビュー領域を置く。

画像・Spriteは画像プレビュー、Prefabはプレビュー用カメラ、Canvasを持つUI Prefabはカメラへ接続したCanvas、Materialはサンプル形状への適用結果、音声は再生ボタン、シーンはシーン読み込みボタンを表示する。

キャラクターなどのPrefabにAnimatorがある場合は、カタログ生成時に最初のAnimationClipとステートを候補として登録し、選択時に再生する。手動登録するShowcaseEntryでは、プレビュー用Prefab、PreviewAnimation、AnimationStateNameを指定できる。

共通の `SceneTransition` Prefabを選ぶと、プレビュー下部の再生ボタンで Fade、横ワイプ、上下シャッター、左右シャッターを順に再生できる。各演出は閉じる・開くを続けて再生し、展示室からシーンを進行させずに見た目を確認する。

VFXカテゴリには `Hd2dLightShaft` Prefabと光芒の画像を登録する。
親Canvasの全面に伸ばすUI Prefabは、展示室では1920×1080の領域を用意してプレビューする。

### 光芒の調整

Topでは、Hierarchyの `TopCanvas > TopHd2dLightShaft` を選ぶと専用Inspectorで調整できる。
「Editorで表示」を有効にすると、停止中も光芒を表示し、変更とUndoを反映する。

| Inspectorの項目 | 効果 |
| --- | --- |
| 光の濃さ（0で透明）・光の色 | 濃さと色。濃さを小さくするほど背景が透ける。Topは松明の暖色と対比させる青白い光 |
| 太さの範囲・長さの範囲 | Xが最小、Yが最大。個々の光芒には「細い方 / 太い方の倍率」も掛かる |
| 開始位置（X: 左右 / Y: 上下） | Xを小さくすると左へ移る。Yを1より大きくすると画面の上から入る |
| 開始位置の広がり | 複数本の開始位置を左右に並べる幅。0にすると同じ位置から出る |
| 角度の範囲（度）・光芒の本数 | 伸びる方向と本数 |
| 1本ごとの濃さの倍率 | 光芒ごとの濃さの差。差を付けると筋を見分けやすい |
| 床の光だまり | 光芒が床に届く位置の明るい楕円。中心位置・大きさ・濃さを調整し、濃さ0で非表示 |
| 光の中の埃 | 光芒に沿って流れる四角い点。1本あたりの数・色・大きさ・速さ・横ゆれを調整し、数0で非表示 |
| ばらつき・ゆらぎ | 配置パターン、明るさや位置の変化量と速度 |

Editorプレビューは静止表示で、ゆらぎはPlay Modeで確認する。
保存する調整は再生を停止した状態で行い、シーンを保存する。
Play Mode中の変更は停止時に戻る。
生成したプレビュー用の子オブジェクトはシーンやPrefabに保存せず、元の設定から再生成する。
光芒画像は根元が明るく、先へ行くほど広がりながら消える形で、ここでの数値調整は濃さや形状の大きさを変える。
埃の明るさも同じ明るさの分布に従い、光芒の外では光らない。

## カタログ更新

Assets/Baryonyx/Features/Showcase/Data/ShowcaseCatalog.asset が一覧の実行時データである。

Unity Editor起動時に `Assets/Baryonyx` 以下のアセットを検索し、カタログを更新する。今後のクライアントアセットはこの配下へ配置すると展示室の対象になる。

対象アセットの追加・移動・削除後も、AssetDatabaseの更新に合わせて自動更新する。

手動で更新する場合は、Unity Editorの Baryonyx > Showcase > Refresh Catalog を実行する。

自動推定だけでは表示方法を定義できないアセットは、ShowcaseEntry を手動作成して Entries 配下へ置き、PreviewPrefab、PreviewAnimation、AnimationStateName を設定する。

## 実装入口

- [展示室シーン](../../client/Assets/Baryonyx/App/Scenes/Showcase.unity)
- [実行時ビュー](../../client/Assets/Baryonyx/Features/Showcase/Runtime/ShowcaseRuntimeView.cs)
- [カタログ](../../client/Assets/Baryonyx/Features/Showcase/Runtime/ShowcaseCatalog.cs)
- [エントリ](../../client/Assets/Baryonyx/Features/Showcase/Runtime/ShowcaseEntry.cs)
- [カタログ生成](../../client/Assets/Baryonyx/Features/Showcase/Editor/ShowcaseCatalogBuilder.cs)
- [Editorプレビュー](../../client/Assets/Baryonyx/Features/Showcase/Editor/ShowcasePreviewWindow.cs)

## Unityでの確認手順

1. `Assets/Baryonyx/App/Scenes/Showcase.unity` を開く。
2. Unity EditorのPlayボタンを押す。
3. 左のカテゴリとアセットを選び、右側のプレビューを確認する。

Playせずに確認する場合は、Unity Editorの `Baryonyx > Showcase > Open Preview Window` を実行する。カテゴリ選択、検索、アセットプレビュー、元アセットの選択、シーンのオープンをこのウィンドウから行える。

新しく追加したアセットが表示されない場合は、Play Modeを停止して `Baryonyx > Showcase > Refresh Catalog` を実行してから、もう一度シーンを再生する。

## 横画面UIの確認

横画面のレスポンシブ対応は、UIカテゴリの `WireframeScreen` を選び、`BattlefieldAmbient` の背景が比率を保って表示されることを確認する。
タイトル画面はシーンカテゴリの `Top` を開き、`TopSafeArea` 配下のタイトルと開始操作が画面端から離れていることを確認する。
展示室のカタログには既存の `WireframeScreen` Prefabと `Top` シーンを登録済みで、今回の共通コンポーネント追加後も同じエントリからプレビューできる。
実機のノッチ・非対称Safe AreaはUnity EditorのGameビューだけでは確定できないため、端末確認時に追加で確認する。

## 実装状況

展示室シーン、カテゴリ一覧、アセット自動検出、画像・Prefab・音声・シーンの表示、Play不要のEditorプレビューを実装済み。

実機でもアセットを確認するため、展示室は意図して通常のビルドへ含める。

プレビュー用Prefabの自動生成、UI状態のStory定義、VFXの再生条件、複数AnimationClipの切り替えは今後拡張する。
