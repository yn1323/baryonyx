---
id: client-showcase
type: specification
status: 一部確定
updated: 2026-11-02
---

# クライアントアセット展示室

[機能索引](README.md)

部品の調整と画面ごとの確かめ方は、次のファイルに分けて置く（2026-11-02に分けた。読むときは、必要なファイルだけを開く）。

| 内容 | ファイル | 主な内容 |
|---|---|---|
| HD-2Dの部品の調整 | [hd2d-tuning.md](showcase/hd2d-tuning.md) | 光芒・霧・揺らぐ光・火の粉の調整項目、ポストプロセス、疑似ティルトシフト、塵ときらめきの表示 |
| 横画面と戦闘・デバッグルームの確かめ方 | [battle-checks.md](showcase/battle-checks.md) | 横画面UI、戦闘画面のモック、カード・HPバー・スキルの演出、デバッグルーム、プレビューの縮尺 |
| 案内人の画面・冒険・地図の確かめ方 | [screen-checks.md](showcase/screen-checks.md) | 編成・商会・神殿、召喚の演出、冒険の画面、街道の世界地図、入れ子の地図 |

## 目的

クライアント側で作成したアセットを、ゲームを進行させずに一覧から確認する。

展示室はゲームシーンとして実装し、Unity Editorでシーンを開いてPlay Modeで確認できる。実機でも確認できるよう、Build Settingsへ常時追加して通常のビルドにも含める。

Play Modeを使わずに一覧と基本プレビューを確認するため、Unity Editorの `Baryonyx > Showcase > Open Preview Window` も提供する。音声の再生やシーンの実行時表示など、ゲーム内の挙動が必要な確認は展示室シーンで行う。

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

分類はアセットのパスと種類から自動推定する（[カタログ生成](../../client/Assets/Baryonyx/Features/Showcase/Editor/ShowcaseCatalogBuilder.cs) の `InferCategory`）。
パスを小文字にして、次の表の語を上から順に探し、最初に当たった分類にする。
当たらなければ種類で決める（Sprite・Texture2Dは画像、Prefabはその他、AudioClipは音声・音楽、AnimationClipはアニメーション、Material・Shaderはマテリアル・シェーダー、ScriptableObjectはデータ）。
シーンはいつもシーンに入る。

| パスに含む語 | 分類 |
|---|---|
| `character`・`/enemies/`・`adventurer`・`player` | キャラクター（敵の分類はないため、敵もここに入る） |
| `background`・`environment`・`/stages/`・`forest`・`mine` | 背景・環境（`Shared/Art/Stages/` の下の、探索する場所の背景と[3Dの舞台](../art/hd2d-stage.md)のPrefab・テクスチャ・マテリアルなど） |
| `weapon`・`sword`・`bow` | 武器 |
| `item`・`potion`・`icon`・`/armor/`・`/accessories/` | アイテム |
| `/ui/`・`button`・`panel`・`screen` | UI |
| `vfx`・`effect`・`particle` | VFX・エフェクト |

`/Editor/`・`/Tests/`・`/Showcase/`・`/DevCaptures/` を含むパスは一覧に載せない。

## 表示と操作

展示室にはカテゴリ一覧、アセット一覧、プレビュー領域を置く。

画像・Spriteは画像プレビュー、Prefabはプレビュー用カメラ、Canvasを持つUI Prefabはカメラへ接続したCanvas、Materialはサンプル形状への適用結果、音声は再生ボタン、シーンはシーン読み込みボタンを表示する。
Materialのサンプル形状は球とし、シェーダーの `PreviewType` タグが `Plane` のもの（板に描くエフェクトやUIのシェーダー）だけ、Inspectorのプレビューと同じく四角い板にする。

キャラクターなどのPrefabにAnimatorがある場合は、カタログ生成時に最初のAnimationClipとステートを候補として登録し、選択時に再生する。手動登録するShowcaseEntryでは、プレビュー用Prefab、PreviewAnimation、AnimationStateNameを指定できる。

共通の `SceneTransition` Prefabを選ぶと、プレビュー下部の再生ボタンで Fade、横ワイプ、上下シャッター、左右シャッターを順に再生できる。各演出は閉じる・開くを続けて再生し、展示室からシーンを進行させずに見た目を確認する。WipeとShutterは初期値の毎秒25コマのコマ送りで再生する。

VFXカテゴリには `Hd2dLightShaft`、`Hd2dFog`、`Hd2dFlickerLight`、`Hd2dEmberEmitter`、`Hd2dLightingVfx`、`Hd2dParticleField`、`Hd2dVfxLightPoint` の各Prefabを登録する。
これらは画像を使わず、霧のシェーダー `Baryonyx/HD2D/UI Fog`（マテリアル `Hd2dUiFog`）と、光・粒のシェーダー `Baryonyx/HD2D/UI Ambient` で計算して描く。
光・粒のマテリアルは形ごとに分かれ、`Hd2dUiGlowCore`（芯）・`Hd2dUiGlowHalo`（周りを照らす光）・`Hd2dUiLightPool`（床の光だまり）・`Hd2dUiLightShaft`（光芒）・`Hd2dUiLightRay`（小さな光の筋）・`Hd2dUiDust`（塵）・`Hd2dUiSparkle`（四芒星のきらめき）・`Hd2dUiEmber`（火の粉）・`Hd2dUiPuff`（ルーンのもや）が、VFX・エフェクトのカテゴリに入る（`Shared/VFX/HD2D/Materials/`）。
マテリアル単体のプレビューは四角い板で、光芒の塵やゆらぎのように時間で動く部分は止まって見える。形の動きはPrefabのプレビューかPlay Modeで確かめる。
ポストプロセスの `Hd2dPostProcess` はVFX・エフェクトのカテゴリに入る。

[3Dの舞台](../art/hd2d-stage.md)は、背景・環境のカテゴリの `StarlitGateStage`（Topの星空の夜の山の門）・`ForestGladeStage`（Homeの昼の森の野営地）・`DuskHighlandStage`（戦闘の夕暮れの高原の環状列石）のPrefabと、それぞれのテクスチャ（`Starlit〈名前〉`・`Glade〈名前〉`・`Dusk〈名前〉` の `.aseprite`）とマテリアルで確認する。舞台ごとのポストプロセス（`StarlitGateLook`・`ForestGladeLook`・`DuskHighlandLook`）も背景・環境のカテゴリに入る。舞台の小物・草の房・地面の汚し（`GladeCrate`・`StarlitStatue`・`DuskBones` など）のテクスチャとマテリアル、光を受ける面の法線マップ（`〈名前〉Normal.png`）も同じカテゴリに入る。草と葉のシェーダー `Baryonyx/HD2D/Stage Foliage` と、小物の足元の陰のマテリアル `Hd2dPropShade` はVFX・エフェクトのカテゴリに入る。草の揺れはPlay Modeで確認する。今は画面で使っていない `DungeonHallStage`（広間）・`ForestRuinsStage`（森の遺跡）と、そのテクスチャ・Profileも同じカテゴリに残る。空と遠景のシェーダー `Hd2dPaintedDistance` はVFX・エフェクトのカテゴリに入る。舞台のPrefabのプレビューは舞台全体を収めるため、夜空の2枚目の月も見える（画面では1つだけ写る）。光の筋のシェーダーとマテリアル `Hd2dLightBeam`、光だまりのシェーダー `Hd2dLightPool`、煙のシェーダー `Hd2dStageSmoke` とマテリアル `Hd2dSmoke`、木漏れ日と雲の影のマスク（コードから作った生成テクスチャ `Hd2dLeafCookie`・`Hd2dCloudCookie`、`Shared/VFX/HD2D/Generated/`）、舞台のレンズのシェーダー `Hd2dStageFocus` はVFX・エフェクトのカテゴリに入る。戦闘の背景ごとの光だまり（`〈接頭辞〉〈名前〉Pool` など）・火山の噴気（`VolcanoFumes`）のマテリアルと、Topの松明・夕暮れの高原のかがり火の足元の光だまり（`StarlitFirePool`・`DuskFirePool`）は、その舞台のフォルダーにあり背景・環境のカテゴリに入る。煙・光だまりは時間で形が変わるため、流れと明滅はシーンのPlay Modeで確かめる。舞台のレンズ・木漏れ日・光の筋は、HomeやTopのシーンを開くと停止中のGameビューでも確認でき、光の筋の揺らぎと塵の動きはPlay Modeで確認する。
キャラの板・接地影・炎・火の粉・光の粒のシェーダーとマテリアル（`Hd2dStageSprite`・`Hd2dContactShadow`・`Hd2dFlame`・`Hd2dGlowParticle`・`Hd2dEmberGlow`・`Hd2dMoteGlow`・`Hd2dFireflyGlow`・`Hd2dSnowFlake`）と、Homeの焚き火の炎と煙のマテリアル `CampfireFlame`・`CampfireSmoke` はVFXのカテゴリに入る。
Homeの焚き火の画像は、3Dの舞台に立てる薪の `CampfireLogs` と、3Dの舞台がないときだけその上に重ねる炎の `CampfireFlame` に分かれ、UIのカテゴリに入る。
焚き火の炎の揺らぎと火の粉は、シーンのカテゴリから `Home` を開いて確認する。
Prefabのプレビューは舞台全体を遠くから写すだけで、画面ごとのカメラ・霧・キャラは映らない。
[戦闘の背景](../art/hd2d-stage/battle-backgrounds.md#戦闘の背景)の10種類（`MeadowRoadStage`・`MistyWoodsStage`・`MineTunnelStage`・`CrystalCavernStage`・`RockyShoreStage`・`MistySwampStage`・`SnowFieldStage`・`VolcanoCraterStage`・`MoonlitGraveyardStage`・`CastleHallStage`）も、Prefab・テクスチャ（`Meadow〈名前〉` など舞台ごとの接頭辞の `.aseprite`）・マテリアル・法線マップ・ポストプロセス（`〈舞台〉Look`）が背景・環境のカテゴリに入る。
キャラが立った見た目、光と影、カメラの動きは、シーンのカテゴリから `Top`・`Home`・`BattleLab` を開いて確認する。
戦闘の背景をキャラと一緒に見比べるときは、シーンのカテゴリから `Battle` を開く。展示室から開いたシーンには背景を選ぶInspectorがないため、背景の切り替えはUnity Editorで `Battle.unity` を開き、`BattleStages` の `Stage` を変えて確かめる。
ポストプロセスはカメラの描画に掛かるため、単体のプレビューではなく、シーンカテゴリの `Top` を開いて確認する。
親Canvasの全面に伸ばすUI Prefabは、展示室では1920×1080の領域を用意してプレビューする。
行き先ごとの地図の地面（`GroundForestRuins` など8枚、`Shared/Art/Stages/Exploration/`）と、2026-10-22まで地図に立てていた木・下草・遺跡（`MapTrees`・`MapTreesFar`・`MapUndergrowth`・`MapUndergrowthFar`・`MapRuin`。今は使っていない）は背景・環境のカテゴリに入る。探索画面の `ExplorationScreen` を選ぶと、決まった種の見本の地図を入口から見た形で並べる（[冒険の画面](screens/adventure.md#冒険の画面)）。Play Modeで金色の縁のマスの札を押すと、4人がそのマスまで歩いて地図が追いかける様子を、1本指で左右に引くと道の先を、右下の「？」で凡例を確かめられる（見本のマスは出来事を終えた扱いで、着くと次の札が出る）。冒険を進めた地図は、シーンのカテゴリから開いた `Exploration` ではなく、Homeから冒険に出て確かめる。

## カタログ更新

Assets/Baryonyx/Features/Showcase/Data/ShowcaseCatalog.asset が一覧の実行時データである。

Unity Editor起動時に `Assets/Baryonyx` 以下のアセットを検索し、カタログを更新する。今後のクライアントアセットはこの配下へ配置すると展示室の対象になる。

対象アセットの追加・移動・削除後も、AssetDatabaseの更新に合わせて自動更新する。

`.aseprite` は、読み込み時に追加したキャンバスと同じ大きさのSpriteが、フレームごとに画像として並ぶ（[読み込みの仕組み](../rules/frontend-design.md#ゲーム内の対象の画像)）。
Importerが作る切り取り済みのSpriteは非表示にしているため、一覧に出ない。

手動で更新する場合は、Unity Editorの Baryonyx > Showcase > Refresh Catalog を実行する。
Build Settingsへの展示室のシーンの追加も確かめ直すときは `Baryonyx > Showcase > Refresh Catalog and Build Settings` を実行する。展示室のシーンがないときは `Baryonyx > Showcase > Create Scene` で作り、開くときは `Baryonyx > Showcase > Open Scene` を使う。

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

Playせずに確認する場合は、Unity Editorの `Baryonyx > Showcase > Open Preview Window` を実行する。カタログを左側の一覧に出し、カテゴリ選択、検索、画像・Prefab・マテリアルなどのEditorプレビュー、元アセットの選択、シーンのオープンをこのウィンドウから行える。

新しく追加したアセットが表示されない場合は、Play Modeを停止して[カタログ更新](#カタログ更新)の手動の更新をしてから、もう一度シーンを再生する。

## 実装状況

展示室シーン、カテゴリ一覧、アセット自動検出、画像・Prefab・音声・シーンの表示、Play不要のEditorプレビューを実装済み。

プレビュー用Prefabの自動生成、UI状態のStory定義、VFXの再生条件、複数AnimationClipの切り替えは今後拡張する。
