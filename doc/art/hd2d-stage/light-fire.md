---
id: art-hd2d-stage-light-fire
type: specification
status: 一部確定
updated: 2026-11-02
---

# 3Dの舞台：光と炎、実装との対応

[3Dの舞台（HD-2D）](../hd2d-stage.md) / 根拠：[アート方針](../direction.md)

## 光と炎

| 項目 | 内容 |
|---|---|
| 月明かり（Top） | 門の背後から差す冷色（0.62, 0.72, 1）の平行光。強さ0.75、見下ろし30度、ハードシャドウで濃さ0.9 |
| 補助光（Top） | 手前左から差す弱い冷色（0.42, 0.52, 0.9）の平行光。強さ0.32、影なし。月明かりの逆光で真っ黒になる崖と門の正面を読めるようにする（ライティングでのウソ） |
| 夕日（戦闘） | 左のやや手前から差す暖色（1, 0.62, 0.38）の平行光。強さ1.15、見下ろし30度、ハードシャドウで濃さ0.85 |
| 環境光（Top・戦闘） | 夜は空（0.16, 0.2, 0.34）・周り（0.1, 0.12, 0.2）・地面（0.04, 0.04, 0.06）、霧（0.07, 0.09, 0.17）18〜70m。夕暮れは空（0.34, 0.3, 0.5）・周り（0.4, 0.28, 0.3）・地面（0.14, 0.1, 0.09）、霧（0.42, 0.3, 0.36）22〜75m |
| 蛍・砂ぼこり | 漂う光の粒を、Topは黄緑で道の上に約36粒、戦闘は橙で石舞台の上に約44粒。蛍は1粒ずつゆっくりふくらんで消え（`Hd2dFireflyGlow`）、砂ぼこりは向きが合った瞬間だけ光る（`Hd2dMoteGlow`） |
| 主光源（広間・森の遺跡） | 左上から差す冷色の平行光。広間は強さ0.6、森の遺跡は1.1。影の濃さ0.85〜0.9（今はどの画面でも使わない舞台） |
| 太陽（Home） | 左上の背後から差す暖色（1, 0.93, 0.78）の平行光。強さ1.45、見下ろし42度、影はハードで濃さ0.85（スマートフォンの設定はソフトシャドウを使わないため、Editorでも合わせる） |
| 木漏れ日 | 太陽のライトクッキーに、木漏れ日のマスク（`Hd2dLeafCookie`、256×256、種2718）を12mごとに敷く。葉の塊の影は2回ゆがめた5段のfBm、葉のすき間はピンホールのように太陽の丸い像（半径12〜28cm）を落とす。最も暗い所で光の4割を残す。Play中は葉の揺れで12mの0.6%（約7cm）ずつ揺れる（`Hd2dCookieDrift`）。キャラの板は受けない |
| 雲の影 | 草原と浜は、太陽のライトクッキーに雲の影のマスク（`Hd2dCloudCookie`、256×256、種3141）を34〜40mごとに敷き、最も暗い所で光の45%を残す。Play中は風で流す（0.5〜0.7m/秒） |
| クッキーのマスク | 画像を使わず、コード（`Hd2dStageKit.LeafCookieMask`・`CloudCookieMask`）と種から作り、生成したテクスチャ（`Shared/VFX/HD2D/Generated/〈名前〉.asset`）として保存する。端がつながるよう、ノイズの格子をタイルの周期で折り返す |
| 環境光（Home） | 空（0.5, 0.6, 0.74）・木々（0.36, 0.45, 0.36）・地面（0.2, 0.21, 0.14）のグラデーション。霧は（0.62, 0.74, 0.72）、16〜62m |
| 光の筋 | `Baryonyx/HD2D/Light Beam`。光の通り道に浮かぶ塵が光を散らして見える現象から、次の層に分ける。帯（板の幅の55%、縁は柔らかい）の中を、2回ゆがめた5段のfBmの塵の濃淡が風で0.12m/秒で流れる。光の向きに沿った細い筋が明暗を付ける。帯の外に薄いにじみ（強さ0.35）が広がる。帯の中でだけ、塵の粒（0.24m四方に35%の割合）が粒ごとに位相をずらしてきらりと光る。板は光の軸のまわりでカメラへ向け、上の少し下で最も明るく、地面に届く前に消える。加算で描き、霧の中では消える。薄い光の階調の段差は画素ごとのごく弱い揺らぎで散らす |
| 光だまり | `Baryonyx/HD2D/Light Pool`。地面に寝かせた板に、にじみと芯（強さ0.45）を別の幅で描き、縁をノイズでゆがめ、地面の位置で決まる2回ゆがめたfBmの明暗（まだら0.5）を光の通り道の揺れとしてゆっくり流す |
| 漂う塵 | Particle Systemと `Baryonyx/HD2D/Glow Particle`。空き地の上の15m×3.6m×10mの箱に約70粒を、5〜9秒かけて漂わせる。粒は小さく明るい芯と薄いにじみを別の幅で描き（F2）、粒ごとの乱数（Particle Systemの Stable Random）で位相をずらして明滅させる |
| 点光源 | 松明・かがり火は暖色、強さ2.4〜3.4、範囲6.5〜9m。[Hd2dLightFlicker](../../../client/Assets/Baryonyx/Shared/VFX/HD2D/Runtime/Hd2dLightFlicker.cs) で明るさを約2割揺らす |
| 焚き火 | Homeのシーンに、UIの焚き火の薪の足元の地面を基準に、計算の炎・火の粉・光をまとめて置く（`Campfire`）。炎は根元を地面から5cm上・薪の板の6cm奥に置き、薪が根元を隠す。光は地面から0.6m上で、昼なので強さ1.6・範囲4.5mの小さな光にし、影は太陽に任せる |
| 炎 | `Baryonyx/HD2D/Flame` が、根元の太い塊を上ほど強くノイズ（時刻で形も変わる4段のfBm）で削って複数の舌に割り、上ほど速く昇らせ、温度で色と明るさを決めて描く。本体の外側には、より細かく速く揺れる暗い赤橙の薄い炎の舌（`_Veil` 0.55）を柔らかく重ねる。煙と火の粉は別の粒で昇らせる。画像を使わない（[エフェクトの描き方](../direction.md#エフェクトの描き方)）。根元は器の縁より少し下・器の板の6cm奥に置き、器が根元を隠す。Topの松明は幅0.38m・高さ0.62m、戦闘のかがり火は幅0.7m・高さ1.15m、Homeの焚き火は幅0.56m・高さ0.7m（`Flame` に渡す大きさ）。揺らぎの周期は炎の幅から決まり、Topは約2.6回/秒、戦闘は約1.9回/秒。炎は加算で描くため、昼の明るい地面の上では地面の色と一緒に白く飛び、形が読めなくなる。Homeの焚き火のマテリアル（[CampfireFlame](../../../client/Assets/Baryonyx/Features/Home/Vfx/CampfireFlame.mat)）は、熱い所だけ奥を75%隠してから足し（`_Cover`）、色を赤く寄せて周りの光を弱くする。`_Cover` の既定は0で、夜の松明・かがり火は加算だけで描く |
| 火の粉 | Particle Systemと `Baryonyx/HD2D/Glow Particle`（`Hd2dEmberGlow`）。熱い気体に乗って0.5〜1.3m/秒で飛び出し、抜けると減速して渦でさまよい、白黄から橙・暗い赤へ冷えて0.9〜2.1秒で消える。速い粒は短い筋に伸ばし、粒ごとに速くちらつかせる |
| 煙 | Particle Systemと `Baryonyx/HD2D/Stage Smoke`。炎の上から炎の数倍の高さまで昇らせ、広がりながら減速し、風下へ流れ、薄い所から削れて消える（F5）。粒は2回ゆがめたfBmで崩した塊で、上側を明るく、くびれを暗くし、生まれたての下側を炎の色で照らす。燭台・かがり火は灰色（`Hd2dSmoke`）、Homeの焚き火は昼の空に淡い灰色（`CampfireSmoke`）、火山の噴気は灰と蒸気の色を溶岩の橙で照らす（`VolcanoFumes`） |
| 雪 | Particle Systemと `Baryonyx/HD2D/Glow Particle`（`Hd2dSnowFlake`）。光らない白い粒として奥を隠して描き（`_Occlude` 1）、風で流してひらひら揺らす |
| 遠景 | カメラを向く遠景の板は奥からの主光源を受けないため、テクスチャを弱く自己発光させる |
| 草と葉 | `Baryonyx/HD2D/Stage Foliage`。表から当たる光は向きに応じて4〜10割、裏から当たる光も最大で4割（`_Translucency` 0.5。Homeの枝葉は0.35）を受け、環境光は板の向きと真上の間の向きで受ける。`Simple Lit` のままでは、月や松明が奥にあるTopの草の房が黒い塊に見えた。主光源の影・木漏れ日のクッキー・点光源・霧を受け、上端（Homeの枝葉は下端）を2.5〜6cm揺らす。不透明の列で深度を書くため、舞台のレンズで地面と同じくぼける |

モバイル用のURP設定（[Mobile_RPAsset](../../../client/Assets/Settings/Mobile_RPAsset.asset)）では、焚き火に影を付けていたときに追加ライトの影を有効にした。
主光源の影の距離は、キャラの影を細かくするため両方のURP設定で40mにした。
描画の解像度の倍率（Render Scale）は、両方のURP設定で1にする（2026-11-10）。それまでモバイル用だけがテンプレートの初期値の0.8で、Editor（PC用の設定、1）で確かめたドット絵と細い光の線が、端末では0.8倍で描かれてぼやけるおそれがあった。倍率を上げた分の重さは、端末でまだ測っていない。

## 実装との対応

| 対象 | 実装 |
|---|---|
| 舞台の組み立て | [StageSetAssets](../../../client/Assets/Baryonyx/Shared/Art/Stages/Editor/StageSetAssets.cs)（メニュー `Baryonyx > Stages > Create 3D Stage Assets`） |
| 舞台の部品 | [Hd2dStageKit](../../../client/Assets/Baryonyx/Shared/VFX/HD2D/Editor/Hd2dStageKit.cs) |
| シーンへの配置 | [Hd2dStageSceneSetup](../../../client/Assets/Baryonyx/App/Editor/Hd2dStageSceneSetup.cs)。TopはTopHomeSceneSetupの `EnsureTopStage`、HomeとBattleLabは各シーンの作り直し |
| カメラ | [Hd2dStageCamera](../../../client/Assets/Baryonyx/Shared/VFX/HD2D/Runtime/Hd2dStageCamera.cs)、計算は [Hd2dStageMath](../../../client/Assets/Baryonyx/Shared/VFX/HD2D/Runtime/Hd2dStageMath.cs) |
| キャラの板 | [Hd2dUiBillboard](../../../client/Assets/Baryonyx/Shared/VFX/HD2D/Runtime/Hd2dUiBillboard.cs)、シェーダー `Baryonyx/HD2D/Stage Sprite`・`Baryonyx/HD2D/Contact Shadow` |
| 2Dのときだけの部品 | [Hd2dFlatOnly](../../../client/Assets/Baryonyx/Shared/VFX/HD2D/Runtime/Hd2dFlatOnly.cs)（HomeとBattleLabの描いた背景と、その上の光・霧・火の粉） |
| 舞台のレンズ | [Hd2dStageFocus](../../../client/Assets/Baryonyx/Shared/VFX/HD2D/Runtime/Hd2dStageFocus.cs)、[Hd2dStageFocusRendererFeature](../../../client/Assets/Baryonyx/Shared/VFX/HD2D/Runtime/Hd2dStageFocusRendererFeature.cs)、シェーダー `Hidden/Baryonyx/HD2D/StageFocus`。Renderer Featureは `Hd2dAssets` が両方のURP Rendererへ加える |
| 光の筋・塵・木漏れ日・雲の影 | `Hd2dStageKit` の `LightBeam`・`SetBeam`・`Motes`・`LeafCookie`・`CloudCookie`、Play中のクッキーの動きは [Hd2dCookieDrift](../../../client/Assets/Baryonyx/Shared/VFX/HD2D/Runtime/Hd2dCookieDrift.cs) |
| 光だまり | シェーダー `Baryonyx/HD2D/Light Pool`、`Hd2dStageKit.PoolMaterial`、戦闘の背景は `BattleStageSets.Builder.Pools` |
| 炎・火の粉・煙 | シェーダー `Baryonyx/HD2D/Flame`・`Baryonyx/HD2D/Glow Particle`・`Baryonyx/HD2D/Stage Smoke`、`Hd2dStageKit` の `Flame`・`Embers`・`Smoke`・`SmokeMaterial`。Homeの焚き火は `HomeSceneSetup.AddCampfire` |
| 空気の部品の共通の計算 | [Hd2dStageAir.hlsl](../../../client/Assets/Baryonyx/Shared/VFX/HD2D/Shaders/Hd2dStageAir.hlsl)（ハッシュ、値ノイズ、fBm、2回のDomain Warping、ディザ、撮影用の時刻 `_Hd2dStageClock`） |
| 停止中とSceneタブの表示 | [Hd2dParticlePreview](../../../client/Assets/Baryonyx/Shared/VFX/HD2D/Runtime/Hd2dParticlePreview.cs)、[Hd2dSceneViewSync](../../../client/Assets/Baryonyx/Shared/VFX/HD2D/Editor/Hd2dSceneViewSync.cs) |
| 描いた遠景 | シェーダー `Baryonyx/HD2D/Painted Distance`、`Hd2dStageKit.PaintedMaterial` |
| テクスチャの変換 | [stage_texture.py](../../../.agents/skills/hd2d-stage-set/scripts/stage_texture.py) |
| 小物と汚し | [StageDressing](../../../client/Assets/Baryonyx/Shared/Art/Stages/Editor/StageDressing.cs)。部品は `Hd2dStageKit` の `Boards`・`Decals`・`Shades`・`Scatter` |
| 法線マップ | `Hd2dStageKit.ReliefMap`、計算は [Hd2dRelief](../../../client/Assets/Baryonyx/Shared/VFX/HD2D/Editor/Hd2dRelief.cs) |
| 草と葉 | シェーダー `Baryonyx/HD2D/Stage Foliage`、`Hd2dStageKit.FoliageMaterial` |
| 戦闘の背景 | [BattleStageSets](../../../client/Assets/Baryonyx/Shared/Art/Stages/Editor/BattleStageSets.cs)（メニュー `Baryonyx > Stages > Create Battle Stage Assets`） |
| 戦闘の背景の選択 | [BattleStageSelector](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleStageSelector.cs)・[BattleStage](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleStage.cs)、シーンは [BattleSceneSetup](../../../client/Assets/Baryonyx/App/Editor/BattleSceneSetup.cs)（メニュー `Baryonyx > App > Create Battle Scene`） |

停止中のGameビューとSceneタブでも、3Dの舞台・キャラ・影・レンズと色・粒子を確認できる。
カメラの揺れとすべり込み、点光源の揺らぎは、Play Modeで確認する。

## 未決事項

| 項目 | 状態 |
|---|---|
| Android端末での負荷（影・Bloom・舞台のレンズ・点光源の数） | 未確認。発熱やフレーム落ちがあれば、舞台のレンズの半径、Bloom、影の解像度の順に下げる |
| 光芒・光だまり・煙のノイズの計算量 | 見た目を優先し、1画素で値ノイズを数十回読む。Android端末での負荷は未確認。重いときは削る前にユーザーと相談する |
| 熱のゆらぎ（背景の屈折） | 描いていない。描くなら、舞台のカメラでOpaque Textureを作る設定にする必要があり、スマートフォンでの負荷と合わせて判断する |
| エフェクトと同時の点光源 | 必要時に判断。技の演出と同時に点光源を置き、キャラの影を背景に落とす表現。戦闘の演出はUIで描いているため、舞台の点光源と同期する仕組みが要る |
| 同じ画面での時間帯の切り替え | 必要時に判断。今は画面ごとに時間帯を固定している |
| 使っていない舞台 | 広間と森の遺跡は、ダンジョンの中や森の遺跡の場面に使えるよう残している。使わないと決めたら削除する |
| 案内人の画面（編成・商会・神殿・旅の案内所・ライブラリ）の3D化 | 今回の対象外。2Dのまま |
| 探索・マップ移動の舞台 | 移動しない前提で作った。移動を加えるときは、床の分割や光の数を見直す |
| Battleシーンの読み込みの重さ | 11種類の舞台を1つのシーンに置き、選んでいない舞台は無効にしている。無効でもテクスチャとメッシュは読み込まれる。Android端末でのメモリと読み込み時間は未確認。実際の戦闘で使うときは、選んだ舞台だけを読み込む作りに改める |
| BattleとBattleLabの関係 | BattleはBattleLabと同じ戦闘画面のPrefabを使う。BattleLabはデバッグルームの一覧から開く |
