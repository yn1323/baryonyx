---
id: client-showcase
type: specification
status: 一部確定
updated: 2026-09-24
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

共通の `SceneTransition` Prefabを選ぶと、プレビュー下部の再生ボタンで Fade、横ワイプ、上下シャッター、左右シャッターを順に再生できる。各演出は閉じる・開くを続けて再生し、展示室からシーンを進行させずに見た目を確認する。WipeとShutterは初期値の毎秒25コマのコマ送りで再生する。

VFXカテゴリには `Hd2dLightShaft`、`Hd2dFog`、`Hd2dFlickerLight`、`Hd2dEmberEmitter` の各Prefabと、光芒・霧のノイズ画像を登録する。
加算合成の `Hd2dUiAdditive` はマテリアル・シェーダーのカテゴリ、ポストプロセスの `Hd2dPostProcess` はデータのカテゴリに入る。
ポストプロセスはカメラの描画に掛かるため、単体のプレビューではなく、シーンカテゴリの `Top` を開いて確認する。
親Canvasの全面に伸ばすUI Prefabは、展示室では1920×1080の領域を用意してプレビューする。

### 光芒の調整

Topでは、Hierarchyの `TopBackdropCanvas > TopHd2dLightShaft` を選ぶと専用Inspectorで調整できる。
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

### 霧の調整

Topでは、Hierarchyの `TopBackdropCanvas > TopHd2dFog` を選ぶと専用Inspectorで調整できる。
霧は背景の直上に置き、光芒と塵は霧の手前に描く。
「霧のレイヤー」の各要素が1つの霧の範囲で、要素を追加・削除すると範囲が増減する。

| Inspectorの項目 | 効果 |
| --- | --- |
| 全体の濃さ | 全レイヤーの濃さに掛ける倍率。0で霧を消す |
| 色（Aが濃さ） | 層ごとの色と濃さ。Topは松明の暖色と対比させる青灰色 |
| 範囲の左下・右上 | 親の正規化座標で範囲を決める。0未満や1超えで画面の外まで広げ、縁を画面に見せない |
| 縁のぼかし（px） | 範囲の縁を内側へぼかす幅。Xが左右、Yが上下 |
| 模様の大きさ（px） | ノイズ模様1枚分の大きさ。横長にすると流れる霧、縦横を近づけると漂う霞に見える |
| 流れる速さ（px/秒） | 流れの向きと速さ。奥の層ほど遅く、手前ほど速くすると奥行きが出る |
| 逆向きの細かい模様 | 逆向きに流れる細かいノイズの濃さ。0にすると1枚の画像が滑るだけに見える |
| 濃さの増減・増減の速さ | 濃さがゆっくり変わる量と速さ |

Prefabの既定は床霧の奥（`FloorMistFar`）と手前（`FloorMistNear`）の2層である。
Topではシーンのインスタンスで、アーチ奥の霞（`DeepHaze`）を末尾に追加している。
Prefabの既定の層を変えると、上書きしていないTopの床霧にも反映される。
霧はEditorでは静止表示し、流れと濃さの変化はPlay Modeで確認する。
プレビュー用の子オブジェクトはシーンやPrefabに保存しない。

### 揺らぐ光の調整

Topでは、Hierarchyの `TopBackdropCanvas > TopHd2dFlickerLight` を選ぶと専用Inspectorで調整できる。
「光源」の各要素が1つの光源で、光源ごとに芯、周りを照らす広い光、床の照り返しの3枚を加算合成で重ねる。
Topの `TopHd2dFlickerLight` は背景と同じ `ResponsiveBackground` を持ち、背景画像と同じ範囲に広がる。
このため光源の位置は背景画像上の正規化座標（左下が原点）で指定し、画面比率が変わっても描かれた松明に重なる。
霧の手前、光芒の奥に描く。

| Inspectorの項目 | 効果 |
| --- | --- |
| 全体の明るさ | 全光源の明るさに掛ける倍率。0で光を消す |
| 光源の位置 | 芯と周りの光の中心 |
| 色 | 足す光の色。Topの松明は橙色 |
| 芯の明るさ・大きさ | 光源のすぐ周りの明るい部分 |
| 周りを照らす明るさ・大きさ | 壁や柱を照らす広い光 |
| 照り返しの明るさ・位置・大きさ | 床に落ちた光。横長の楕円にする。明るさ0で非表示 |
| 揺らぎの量・速さ | 明るさの変化量と速さ。炎は2〜3、魔法の光は0.5前後が目安 |
| 大きさの揺らぎ | 明るさに合わせて光の大きさが変わる割合 |

照り返しは炎より少し遅れて、控えめに揺らす。
加算合成は背景の色へ光を足すため、濃さを上げすぎると白く飛ぶ。
背景画像に光が描き込まれている場合は、芯を小さく、周りの光を弱くする。
光はEditorでは静止表示し、揺らぎはPlay Modeで確認する。
プレビュー用の子オブジェクトはシーンやPrefabに保存しない。

### 火の粉の調整

Topでは、Hierarchyの `TopBackdropCanvas > TopHd2dEmberEmitter` を選ぶと専用Inspectorで調整できる。
「発生源」の各要素が1か所の発生位置で、そこから指定した向きへ粒子を出す。
Topでは4つの松明の炎の上から火の粉を出し、揺らぐ光の手前、光芒の奥に描く。
揺らぐ光と同じく背景画像と同じ範囲に広がり、位置は背景画像上の正規化座標で指定する。

| Inspectorの項目 | 効果 |
| --- | --- |
| 全体の濃さ | 全粒子の濃さに掛ける倍率。0で粒子を消す |
| 発生位置・広がり | 粒子が出る中心と、その周りのランダムな範囲 |
| 同時に出す数 | 表示する粒子の数 |
| 出し続ける | オンで消えた粒子がすぐ出直す。オフでは `Burst` を呼んだときだけ出る |
| 寿命の範囲 | 1粒が消えるまでの秒数 |
| 向き・向きのばらつき | 出る方向（90で上）と広がり。360で全方向 |
| 初速・上向きの加速 | 出る速さと、浮き上がる（負の値で落ちる）加速 |
| 横ゆれの幅・速さ | 左右へのゆれ |
| 大きさの範囲 | 四角い点の大きさ。整数に丸める |
| 出たときの色・消える直前の色 | 寿命に沿って色が変わる。消える直前の色のAを0にすると消えていく |
| ちらつき | 明るさの細かな変化 |

報酬や画面遷移の瞬間だけ出す場合は、「出し続ける」をオフにした発生源を用意し、スクリプトから `Burst(発生源の番号, 数)` を呼ぶ。
発生源の番号は、空の要素を除いたリストの順番である。
Editorでは再生開始時と同じ配置を静止表示し、動きはPlay Modeで確認する。
プレビュー用の子オブジェクトはシーンやPrefabに保存しない。

### ポストプロセスの調整

Topでは、背景とHD-2Dの演出を `TopBackdropCanvas`（Screen Space - Camera）に置き、`TopCamera` のポストプロセスを通す。
タイトルと開始操作を持つ `TopScreen` は `TopCanvas`（Screen Space - Overlay）に残し、文字をにじませない。
`ScreenSpaceOverlay` のUIにはカメラのポストプロセスが掛からないため、この2つのCanvasに分けている。
2つのCanvasは同じCanvasScalerの設定を持ち、座標の単位を揃える。

`TopPostProcessVolume` は全体に効くVolumeで、共通の `Shared/VFX/HD2D/Profiles/Hd2dPostProcess.asset` を使う。
Profileを選ぶとInspectorで各効果を調整できる。

| 効果 | 主な項目 | Topでの役割 |
| --- | --- | --- |
| Bloom | Threshold 0.8、Intensity 1.6、Scatter 0.65 | 炎、加算の光、光芒など明るい部分だけをにじませる。Thresholdを下げると石壁までにじむ |
| Vignette | Intensity 0.28、Smoothness 0.45 | 画面の端を暗くし、中央のタイトルと入口へ視線を集める |
| Color Adjustments | Contrast 8、Saturation 6 | 明暗と彩度を少し強め、松明の暖色と光芒の青白さを引き立てる |

| HD-2D Tilt Shift | Intensity 1、Focus Center 0.5、Focus Half Height 0.26、Falloff 0.3、Max Radius 8 | 中央の帯をくっきり残し、天井と手前の床をぼかしてジオラマのように見せる |

Profileは共通アセットなので、変更すると同じProfileを使う全画面に反映される。
画面ごとに変える場合は、Profileを複製してその画面のVolumeへ設定する。
BloomはAndroid端末での負荷が大きい効果である。発熱やフレーム落ちがある場合は、Intensityより先にBloomのDownscaleとMax Iterationsで負荷を下げる。

### 疑似ティルトシフト

`HD-2D Tilt Shift` は、1枚絵の背景に奥行きの情報がないため、画面の高さでぼかしの強さを決める疑似ティルトシフトである。
中央の帯（Focus Center ± Focus Half Height）はぼかさず、帯の外はFalloffの高さをかけて最大のぼかし半径へ近づく。
Max Radiusは画面の高さ1080px基準の半径で、ドット絵の1粒より大きくしないと効果が見えない。Topでは8にしている。
描画は `Mobile_Renderer` と `PC_Renderer` に登録した `Hd2dTiltShift` Renderer Featureが行い、Intensityが0の画面ではぼかしの処理そのものを行わない。
Bloomより前に処理するため、ぼけた光もBloomでにじむ。
タイトルと開始操作はOverlayのCanvasにあるため、ぼけない。
ぼかしは横と縦の2回の全画面処理で、Android端末では負荷が増える。発熱やフレーム落ちがある場合は、Intensityを0にして止める。

### 塵ときらめきの表示

`Hd2dLightingVfx` と `Hd2dParticleField` の塵ときらめきも、「Editorプレビュー」の `PreviewInEditor` を有効にすると停止中に表示する。
Topでは、Hierarchyの `TopBackdropCanvas > TopHd2dLightingVfx` で確認できる。
停止中は、配置パターン（`RandomSeed`）で決まる再生開始時と同じ配置を静止表示し、Inspectorの変更を反映する。
粒子の移動、明滅、消えた粒子の再配置はPlay Modeで確認する。
プレビュー用の粒子はシーンやPrefabに保存しない。

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

横画面のレスポンシブ対応は、UIカテゴリの `HomeScreen` を選び、背景が比率を保って表示されることを確認する。
タイトル画面はシーンカテゴリの `Top` を開き、`TopCanvas > TopScreen > TopSafeArea` 配下のタイトルと開始操作が画面端から離れていることを確認する。
展示室のカタログには `HomeScreen` Prefab、Health Connectの連携モーダル `HealthLinkModal` Prefab、`Top` シーンを登録済みである。
連携モーダルはUIカテゴリで、未許可の状態の文言を表示する。
実機のノッチ・非対称Safe AreaはUnity EditorのGameビューだけでは確定できないため、端末確認時に追加で確認する。

## 実装状況

展示室シーン、カテゴリ一覧、アセット自動検出、画像・Prefab・音声・シーンの表示、Play不要のEditorプレビューを実装済み。

実機でもアセットを確認するため、展示室は意図して通常のビルドへ含める。

プレビュー用Prefabの自動生成、UI状態のStory定義、VFXの再生条件、複数AnimationClipの切り替えは今後拡張する。
