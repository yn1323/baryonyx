---
name: hd2d-lighting-vfx
description: >
  手動呼び出し専用（Claude Code では /hd2d-lighting-vfx、Codex では $hd2d-lighting-vfx）。
  3Dの舞台がない、描いた2D背景の画面（案内人の画面、展示室で見る2Dの見た目など）に、光芒・霧・揺らぐ光・火の粉・Bloom・疑似ティルトシフトを重ねる
  HD-2D風の演出と、キャラクター紹介・報酬・画面遷移のキラキラ粒子を、Unity 6・URPで設計・実装・レビューする。
  3Dの舞台のカメラ・光・霧・レンズ・物は hd2d-stage-set、技・被弾・報酬のエフェクトの作り方は vfx-authoring の担当とする。
disable-model-invocation: true
---

# 描いた2D背景のHD-2D演出とキラキラ粒子

昔ながらのドット絵の背景に、現代的な光、影、奥行き、空気感を重ねる表現を、Unityの画面構造とモバイル性能に合わせて設計するために使う。
「HD-2D」はUnityの機能名ではなく、スクウェア・エニックスがドット絵と3DCGの画面効果を組み合わせた表現として説明している呼称である。[公式の説明](https://www.jp.square-enix.com/octopathtraveler/about/)をそのまま再現できると約束せず、目的に必要な要素へ分解して扱う。

担当の境界は次のとおりである。

| 作業 | 使うスキル |
|---|---|
| 3Dの舞台（`Hd2dStageCamera` がある画面）のカメラ・光・霧・光芒・レンズと色・停止中の表示・地面・小物 | [hd2d-stage-set](../hd2d-stage-set/SKILL.md) |
| 技・被弾・撃破・カットイン・報酬やUIの演出の形・時間・層 | [vfx-authoring](../vfx-authoring/SKILL.md) |
| 描いた2D背景に常に出しておく光・霧・粒と、2D背景向けのポストプロセス。キラキラ粒子の構成 | このスキル |

## 最初に確認すること

実装やレビューの前に、次を調べて現在の描画経路を確定する。

1. Unityのバージョン、URPのバージョン、使用中のRendererがUniversal Rendererか2D Rendererか、HDRとポストプロセスの有効状態を確認する。Unityの版は `client/ProjectSettings/ProjectVersion.txt`、URPの版は `client/Packages/manifest.json` で確かめ、記憶で断定しない。
2. 対象シーンのカメラ、CanvasのRender Mode、Sorting Layer、背景の型（`RawImage`、`Image`、`SpriteRenderer`、Tilemap）と、UIがカメラのポストプロセスを通るかを確認する。シーンに `Hd2dStageCamera` があれば3Dの舞台であり、hd2d-stage-set の担当になる。3Dの舞台の画面でも、展示室で画面のPrefabを単体で見るときなどは、2Dのときだけの部品（`Hd2dFlatOnly`）がこのスキルの担当として表示される。
3. 基準解像度、ピクセル密度、Filter Mode、整数スケール、画面方向、Safe Areaは[UI設計ルール](../../../doc/rules/ui-design.md)に従う。
4. 表現の目的を「背景の奥行き」「環境の光」「キャラクター紹介の見せ場」「操作中のフィードバック」「画面遷移」のどれかに分け、常時演出と短い演出を区別する。
5. 公式Unity資料と対象バージョンが一致しない場合は、対象バージョンのマニュアルを再確認してからAPI名や設定値を断定する。調査の入口は [references/research-sources.md](references/research-sources.md) に置く。

描いた背景を `RawImage` で表示し、文字と操作をSafe Area配下に置く画面では、次の点に注意する。
この構造のままでは、ワールド用の `Light 2D` を追加しても `RawImage` が自動で照明を受けるとは限らない。
まず背景をSprite-Lit対応のワールド描画へ移す価値があるかを判断し、移行しない場合は、光のテクスチャ、透過オーバーレイ、Shader Graph、またはカメラ側のVolumeで表現する。

## 表現を分解して選ぶ

HD-2D風の見た目を一つのエフェクトで作ろうとせず、次の層を必要な分だけ組み合わせる。
光・霧・粒の形を新しく作る・作り直すときは、[エフェクトの描き方](../../../doc/art/direction.md#エフェクトの描き方)に従い、画像を貼らずに計算で描く。式の組み立ては [vfx-authoringの計算で形を描く](../vfx-authoring/references/procedural-shapes.md) を使う。
画像で作った既存の部品は、作り直すまで現行のまま扱う。
技法ごとの前提、難易度、採用状況は [references/technique-catalog.md](references/technique-catalog.md) にまとめる。

| 層 | 役割 | 第一候補 | 採用条件・注意点 |
| --- | --- | --- | --- |
| 絵の基礎 | 元のドット絵の色、輪郭、構図 | 既存背景、Sprite、Tilemap | 元画像の比率とピクセル密度を保つ。背景を画面比率に合わせて引き伸ばさない |
| 奥行き | 遠景・中景・前景の距離感 | レイヤー分割、控えめなパララックス、前景シルエット | 素材分割がない場合は霧、影、前景粒子で代替する。カメラ移動を大きくしない |
| 環境光 | 全体の色温度と焦点 | 青い環境光、暖色の局所光、暗幕、色調整 | 2色程度の役割を決め、彩度とコントラストで文字の可読性を壊さない |
| 局所光 | 描かれた灯り、魔法、窓、宝箱などの視線誘導 | `Light 2D`、発光テクスチャ、Shader Graph、ライト用オーバーレイ | `Light 2D`は対応Rendererと対応マテリアルが必要。UIの`RawImage`には別経路を用いる |
| 影・遮蔽 | 形状と距離の手掛かり | 疑似の投影影（キャラ画像を黒く半透明にして足元から傾ける）、焼き込み影、暗いグラデーション、霧 | `Shadow Caster 2D`は真上から見た平面で遮蔽を計算するため、横・正面の構図では使わない。動く影は少数に絞り、背景全体を毎フレーム再計算しない |
| 空気 | 静止画の平坦さを崩す | 霧、光芒（こうぼう、霧や埃の中で見える光の筋）、埃、灰、火の粉、薄い煙 | 速度、サイズ、透明度を深度別に変える。文字の上へ常時重ねない |
| 仕上げ | 焦点と発光のまとまり | Bloom、Vignette、Color Adjustments、疑似ティルトシフト | モバイルでは弱く始め、品質設定とモーション低減で切り替えられるようにする |

描いた背景の上に演出を重ねるときは、次の順に足す。

1. 暗い青の環境グラデーションと、描かれた光源の位置の暖色グローを背景の上へ置く。
2. 遠景用の薄い霧、中景用の小さな埃、前景用の少数の火の粉または光点を別レイヤーで動かす。
3. 描かれた光源から斜めに伸びる光芒または薄い煙を追加し、文字の背後をわずかに暗くする。
4. BloomとVignetteを弱く加え、文字と操作の入口へ視線を集める。

このリポジトリには、次の共通Prefabがある。
各Prefabの調整項目、画面での値、置く順番は[展示室の仕様](../../../doc/features/showcase.md)の各節を正本とし、このスキルには値を写さない。
共通アセットの生成と、特定のシーンへの配置を書く場所は[配置を増やすときの基準](../../../doc/rules/frontend-design.md#配置を増やすときの基準)に従う。

| 表現 | Prefab | 使うときの要点 | 調整項目の正本 |
| --- | --- | --- | --- |
| 天井や窓から差す光 | `Hd2dLightShaft` | 根元が明るく先へ広がる光芒を、画面外から床へ斜めに通す。太さと濃さの違う複数本を間を空けて並べ、床の光だまりと光の中の埃で出どころと着地点を示す。画面固有の開始位置と光だまりはシーンのPrefabインスタンスで上書きする | [光芒の調整](../../../doc/features/showcase.md#光芒の調整) |
| 霧・霞 | `Hd2dFog` | 背景の直上（光芒と粒子より下）に置く。奥ほど遅く淡く、手前ほど速く濃くする。画面固有の層はPrefabの既定の層を上書きせず、リストの末尾へ足す | [霧の調整](../../../doc/features/showcase.md#霧の調整) |
| 背景に描かれた光源の揺らぎ | `Hd2dFlickerLight` | 親を背景と同じ `ResponsiveBackground` で背景画像に揃え、光源を背景画像上の正規化座標で置く。画面座標で置くと、画面比率が変わったときに絵の光源からずれる。光が描き込まれた背景では、芯を小さく周りの光を弱くして白飛びを避ける | [揺らぐ光の調整](../../../doc/features/showcase.md#揺らぐ光の調整) |
| 火の粉・魔法の粒・報酬のキラキラ | `Hd2dEmberEmitter` | 常時出す発生源と、報酬や画面遷移の瞬間だけ `Burst` で出す発生源を分ける。背景の光源から出す場合は親を背景画像に揃える | [火の粉の調整](../../../doc/features/showcase.md#火の粉の調整) |
| にじみ・周辺減光・色調 | `Hd2dPostProcess`（Volume Profile） | ポストプロセスを掛ける背景と演出を `Screen Space - Camera` のCanvasへ分け、文字と操作は `ScreenSpaceOverlay` のCanvasに残す。両方のCanvasScalerは同じ設定にする。3Dの舞台は舞台ごとのProfileを使う（hd2d-stage-set） | [ポストプロセスの調整](../../../doc/features/showcase.md#ポストプロセスの調整) |
| 画面の上下のぼかし | `HD-2D Tilt Shift`（同じProfile） | ドット絵の1粒より大きい半径にしないと見えず、大きすぎると輪郭が濁る。Android端末で負荷を確かめる。3Dの舞台は舞台のレンズを使う（hd2d-stage-set） | [疑似ティルトシフト](../../../doc/features/showcase.md#疑似ティルトシフト) |
| 画面全体の埃ときらめき | `Hd2dLightingVfx`・`Hd2dParticleField` | 数は多めでも一粒を目立たせない | [塵ときらめきの表示](../../../doc/features/showcase.md#塵ときらめきの表示) |

## キラキラ粒子の構成

「キラキラ」は同じ粒子を大量に出す表現ではなく、次の役割を混ぜる。

- **フィールド粒子**：低コントラストの埃、霧、灰。数は多めでも一粒を目立たせない。
- **アクセント粒子**：四芒星、短い線、火の粉、魔法の点。数を絞り、寿命の途中だけ明るくする。
- **グロー層**：発光する円、ぼかした光斑、細い光芒。アクセント粒子の背後に置く。
- **見せ場のバースト**：キャラクター紹介、報酬、画面遷移の瞬間だけ発生させ、常時演出と分ける。

これらを同じ速度、同じ大きさ、同じ色で動かすと平面のノイズになる。
深度ごとに速度・サイズ・透明度・発生位置を変え、`Color over Lifetime`で出現と消滅を制御する。

## Unityの技法を選ぶ

### 2Dライティング

- ワールドのSpriteやTilemapを照らす場合は、URPの2D Renderer、`Sprite-Lit-Default`または対応Shader、Sorting Layer、必要ならNormal/Mask Mapを確認する。2D Rendererは3Dのライトと同じ描画では使えず、Rendererは全画面で共通なので、切り替えは3Dの舞台の画面にも影響する。
- 灯りにはPointまたはFreeform系の2D Light、月明かりや画面全体の基調にはGlobal系の光、光芒にはVolumetric 2D Lightまたは専用の透過メッシュを候補にする。
- 2D Lightを複数のSorting Layerへ無制限に向けない。対象レイヤーを分け、Light Batching DebuggerとProfilerでテクスチャ・ドローコールの増加を確認する。
- 静止した背景画像へ直接光を当てる目的で、いきなり現在のCanvasをワールド描画へ置き換えない。`RawImage`を維持するなら、光の形を持つ透過画像、マテリアル、Shader Graph、またはカメラに適用するVolumeを使う。
- 2D LightのVolumetricは空間に見える光を作れるが、強度を上げるほど画面全体の明るさと負荷が増える。文字や重要な操作の背後では低い値から調整する。

### ポストプロセス

- URPのCameraでPost Processingを有効にし、Volume Profileを画面またはシーン単位で所有する。
- Bloomの仕組み、`Screen Space - Overlay` のUIにポストプロセスが掛からないこと、Bloomを通らない経路でグローで代えることは、[vfx-authoringの明るさと光のにじみ](../vfx-authoring/references/techniques.md#明るさと光のにじみ)に従う。発光素材を少数に絞り、Threshold、Intensity、Scatter、Downscale、Qualityを調整する。ドットの輪郭と文字がにじむ場合は、発光用レイヤーを分離するか強度を下げる。
- Color Adjustmentsで環境の明暗・色温度・彩度を揃える。Vignetteは中央へ視線を集めるために弱く使う。
- 描いた背景には深度がないため、URPのDepth of Fieldではなく、画面の位置でぼかす疑似ティルトシフトを使う。

### パーティクル

- 通常の環境粒子と数千程度までのキラキラは、組み込みのParticle Systemを第一候補にする。Emission、Shape、Velocity/Force over Lifetime、Noise、Color/Size over Lifetime、Rendererを役割ごとに設定する。VFX Graphを使う条件は[vfx-authoringのUnityでの実装経路](../vfx-authoring/references/techniques.md#unityでの実装経路)に従う。
- 透明な埃や霧はAlphaまたはPremultiply、星やグローはAdditiveを候補にする。Additiveは暗い背景では映えるが、重ねすぎると白飛びするため上限を決める。
- 四芒星は単一の巨大な形で描かず、中心点、縦横の短い光、ぼかした円を組み合わせる。新しく作る場合は、それぞれを計算で描く。少数をランダムに大きくし、全粒子の周期を揃えない。
- `Simulation Space`は、背景の埃や霧はWorld、キャラクター紹介に追従する粒子はLocal、画面に固定する光点はUIまたはCustomを選ぶ。粒子の目的と追従対象を一致させる。
- `randomSeed`を固定できる構成にし、スクリーンショット比較と再現性を確保する。ランタイムの見た目を毎回変える場合も、テスト用の固定シードを用意する。
- Particle SystemのLightsモジュールで全粒子へ実ライトを付けない。発光スプライトとBloomのほうが、環境粒子の数を増やしたときの制御がしやすい。

## 画面全体で再利用する構成

複数画面で使うときは、見た目と設定を次のように分ける。

- **共通の表現部品**：発光スプライト、四芒星、埃、霧、光芒、暗幕、色調整、粒子の再生制御。
- **画面固有の配置**：描かれた光源の位置、キャラクター紹介の焦点、報酬画面のバースト位置、文字の遮蔽範囲。
- **品質設定**：高・標準・低、モーション低減、粒子停止、Bloomの強度、同時粒子数、更新頻度。
- **データ**：色、発生範囲、寿命、速度、サイズ、Sorting Layer、再生条件をPrefabまたはProfileへ寄せ、コードへ直接書き込まない。

共有するかどうかと置き場所は[配置を増やすときの基準](../../../doc/rules/frontend-design.md#配置を増やすときの基準)に従う。2つ目の画面が使うまでは、画面固有のPrefabを無理に共通化しない。
画像・VFX・Prefabなどを追加・変更したら、ルートの AGENTS.md の[クライアントアセット展示室](../../../AGENTS.md#クライアントアセット展示室)に従って展示室を更新する。

## 演出の制約

- 重要な文字、ボタン、HPや報酬などの状態表示へ粒子を重ねない。必要ならマスク、Sorting Layer、局所暗幕で読みやすさを守る。
- 明滅・閃光・画面揺れ・色だけで状態を伝えないことは、[vfx-authoringの安全と読みやすさ](../vfx-authoring/SKILL.md#安全と読みやすさ)に従う。常時の色収差やモーションブラーを既定値にしない。モーション低減では粒子数・速度・明滅を下げ、停止しても意味が失われない見た目を用意する。
- ドット絵の輪郭をぼかすことを「高品質化」とみなさない。BloomやDoFを使う場合は、元のピクセル密度、整数スケール、文字の可読性を先に確認する。
- 常時の粒子数、透明オーバードロー、ライトブレンドスタイル、Volume解像度を小さく始め、Android端末でProfilerとFrame Debuggerを使って確認する。Editorだけで性能を合格にしない。

## 検証の順序

実装や検証の前に、2Dの演出でつまずいた点（`AddBlitPass` のテクセルサイズ、Renderer Featureの登録、画面座標で置いた光のずれなど）と部品設計の工夫を [references/implementation-notes.md](references/implementation-notes.md) で、Editorの状態と撮影のつまずきを [Unity EditorとCLIの知見](../shared/unity-editor-notes.md) で読み、同じ問題を避ける。

1. **静的確認**：Renderer、Canvas、Material、Sorting Layer、Volume、粒子Prefab、設定の参照切れを確認する。
2. **Editor確認**：[UI設計ルール](../../../doc/rules/ui-design.md)の機種差を確認する条件の画面比率とSafe Areaで、背景の歪み、文字のコントラスト、粒子のクリップ、UIへの重なりを確認する。
3. **Play Mode確認**：再入場、画面の遷移、開始前後の再生条件、停止・再生、固定シード、Consoleの例外を確認する。
4. **性能確認**：粒子数、Overdraw、ライトの対象Sorting Layer、BloomとVolumeの負荷、GC、フレーム時間を記録する。
5. **端末確認**：ルートの AGENTS.md の[デバッグ環境](../../../AGENTS.md#デバッグ環境)に従い、ユーザーが起動したAndroidエミュレーターまたは指定端末で、表示・発熱・フレーム時間・モーション低減を確認する。

撮影と画像の保存先は [client/AGENTS.md](../../../client/AGENTS.md) の「Unityの操作と検証」に従う。
Editorで再生できたこと、APKで動いたこと、実機で見たことを同じ証拠として扱わず、未確認の段階を報告に残す。

## 完了報告

実装またはレビューの完了時は、変更した表現層、選択したUnity経路、確認した画面条件、性能の証拠、展示室の登録状況、未確認の端末・Renderer・ライティング条件を日本語で報告する。
Webで調査した場合は、公式資料と設計上の推論を分け、対象Unityバージョンと参照日を添える。
