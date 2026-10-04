# HD-2DライティングとVFXの調査資料

調査日は2026-09-22。
URLと仕様は更新されるため、UnityのAPIやInspector名を実装へ反映するときは、プロジェクトのUnity `6000.6.0f1`とURP `17.6.0`に対応する公式マニュアルを再確認する。

## HD-2Dの定義と設計上の扱い

- [SQUARE ENIX「HD-2D」](https://www.jp.square-enix.com/octopathtraveler/about/)
  - ドット絵へ3DCGの画面効果を加えた表現として紹介されている。
  - Unity固有の実装方式ではないため、奥行き、光、影、空気、カメラ、ポストプロセスへ分解して採用する。
- [SQUARE ENIX「The Birth and Evolution of HD-2D」](https://www.youtube.com/watch?v=r7_sWDIJOUo)
  - 開発者がピクセルアートと3D、光と影、雪などの画面表現を説明する公式動画。
  - 動画の印象をUnityの機能へ置き換える場合は、特定作品の内部実装と推測を混同しない。

## Unity 6の2Dライティング

- [Create a 2D light in URP](https://docs.unity.com/en-us/engine/6000.0/manual/unity2d/2d-urp/2d-index/2d-light-properties-explained)
  - 2D Lightが対応する2D GameObjectを照らし、`Sprite-Lit-Default`、対象Sorting Layer、Volumetric、Freeform形状の注意点を説明する。
- [URPの2Dライティング](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/2d-index.html)
  - 2D Lightの種類、Normal/Mask Map、Shadow Caster 2D、ブレンド、最適化の入口。
- [2Dライトを最適化する](https://docs.unity3d.com/ja/6000.0/Manual/urp/2d-lights-optimize-methods.html)
  - ブレンドスタイル、ライトテクスチャ、バッチングなど、モバイルでの確認項目を示す。
- [2Dライトバッチ処理の概要](https://docs.unity3d.com/ja/6000.0/Manual/urp/2d-light-batching-intro.html)
  - 同じライト・Shadow Caster対象を共有するSorting Layerをバッチングする考え方を説明する。
- [Scale and rotate pixel art precisely in URP](https://docs.unity.com/en-us/engine/6000.0/manual/unity2d/2d-urp/2d-pixelperfect)
  - Pixel Perfect Cameraで解像度変更や移動時のぼけを抑える方法を説明する。

## Unity 6のポストプロセス

- [Introduction to post-processing in URP](https://docs.unity.com/en-us/engine/6000.6/manual/post-processing-and-full-screen-effects/urp/integration-with-post-processing)
  - Volumeを使うURPのポストプロセス、モバイル向けの負荷、DoFの選択肢を説明する。
- [Add post-processing in URP](https://docs.unity.com/en-us/engine/6000.5/manual/post-processing-and-full-screen-effects/urp/add-post-processing)
  - CameraのPost Processing、Volume、Volume Profile、Overrideの設定手順を説明する。
- [Bloom Volume Override](https://docs.unity.com/en-us/engine/6000.7/manual/post-processing-and-full-screen-effects/urp/effect-list/bloom)
  - BloomのThreshold、Intensity、Scatter、Filter、Downscale、Max Iterationsとモバイル向けの調整項目を説明する。
- [Color Adjustments Volume Override](https://docs.unity.com/en-us/engine/6000.0/manual/post-processing-and-full-screen-effects/urp/effect-list/color-adjustments)
  - Exposure、Contrast、Color Filter、Hue、Saturationで全体の色調を調整する方法を説明する。
- [Vignette Volume Override](https://docs.unity.com/en-us/engine/6000.3/manual/post-processing-and-full-screen-effects/urp/effect-list/vignette)
  - 画面端を暗くして中央へ視線を寄せる効果と主要パラメータを説明する。

## Unity 6の粒子と発光素材

- [Choosing Your Particle System](https://docs.unity.com/en-us/engine/6000.3/manual/visual-effects/particle-systems/choosing-your-particle-system)
  - Built-in Particle SystemはC#から粒子を制御しやすく数千規模、VFX GraphはCompute Shader環境で大規模なGPU粒子を扱うという比較を示す。
- [Particle System module component reference](https://docs.unity.com/en-us/engine/6000.0/manual/visual-effects/particle-systems/particle-system-modules)
  - Emission、Shape、Noise、Force/Velocity over Lifetime、Color/Size over Lifetime、Trails、Lightsなどのモジュール一覧。
- [Change particle color](https://docs.unity.com/en-us/engine/6000.0/manual/particle-color)
  - Color by SpeedとColor over Lifetimeを、火の粉や魔法の発光の制御に使う例を説明する。
- [URPのブレンドモード](https://docs.unity3d.com/ja/6000.0/Manual/urp/blending-modes.html)
  - Alpha、Premultiply、Additive、Multiplyの合成式と見え方の違いを説明する。
- [Particles Unlit shader material reference](https://docs.unity.com/en-us/engine/6000.7/manual/materials-and-shaders/built-in/shaders-in-universalrp/reference/particles-unlit-shader)
  - Particle用URPマテリアルのAlpha/AdditiveとColor Modeの設定を確認する入口。

## HD-2Dの構成要素（2026-09-23追加）

- [HD-2D（Wikipedia）](https://en.wikipedia.org/wiki/HD-2D)
  - 動的ライティング、被写界深度、ティルトシフト、Bloom、ボリュメトリックな光と霧、粒子、パララックスの組み合わせとして説明されている。二次資料のため、構成要素の洗い出しにだけ使う。
- [Octopath Traveler II builds a bigger, bolder world in its stunning HD-2D style（Unreal Engine）](https://www.unrealengine.com/en-US/developer-interviews/octopath-traveler-ii-builds-a-bigger-bolder-world-in-its-stunning-hd-2d-style)
  - エフェクトと同時に点光源を置き、キャラクターの影を環境へ落とした事例がある。
- [Canvas | uGUI 2.6.0](https://docs.unity3d.com/Packages/com.unity.ugui@2.6/manual/class-Canvas.html)
  - Canvasの各Render Modeの描画のされ方。ポストプロセスの適用範囲を判断する根拠に使う。
- [Add a normal map or a mask map to a sprite in URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/SecondaryTextures.html)
  - Sprite Editorの `_NormalMap` と `_MaskTex` の設定方法。
- [Parallax Shaders & Depth Maps（Alan Zucconi）](https://www.alanzucconi.com/2019/01/01/parallax-shader/)
  - 深度マップで1枚絵をずらす2.5Dパララックスの原理。現時点では採用しない。

## 読み方

Unity公式資料はAPI、対応条件、性能上の制約の根拠として使う。
SQUARE ENIXの資料はHD-2Dの意図と画面上の要素を理解する参考に使う。
「キャラクター紹介でキラキラが舞う」という呼称は公式の技術用語ではないため、粒子、発光、光芒、Bloom、タイミング演出を組み合わせた設計上の分類として扱う。

## HD-2Dの3Dステージ

調査日は2026-10-02。
3Dの地面と背景にドット絵のキャラを立たせる構成（[hd2d-3d-stage.md](hd2d-3d-stage.md)）を作るときに確かめた資料である。
特定作品の内部実装は公開されていないため、開発者の発言と、Unityで実現した方法を区別する。

- [「オクトパストラベラー」開発者インタビュー（GAME Watch）](https://game.watch.impress.co.jp/docs/interview/1128111.html)
  - 背景は3Dモデルにテクスチャを貼る3Dゲームの作り方に近く、テクスチャの解像度は細かすぎると写実的に、粗すぎるとドット感が強すぎるため、感覚で釣り合わせたと述べている。
  - 壁に光源が反射する表現を使い、写実に寄りすぎないよう動きを抑えた部分があるという。
- [HD-2D（Wikipedia）](https://en.wikipedia.org/wiki/HD-2D)
  - 動的なライティング、被写界深度、ティルトシフトを組み合わせ、ジオラマのような見た目を作る表現として説明している。
- [Octopath Traveler II builds a bigger, bolder world in its stunning HD-2D style（Unreal Engine）](https://www.unrealengine.com/en-US/developer-interviews/octopath-traveler-ii-builds-a-bigger-bolder-world-in-its-stunning-hd-2d-style)
  - IIでは3Dのカメラワークを多く使い、画風を崩さずにどこまでカメラを回せるかが課題だったとされる。1回目の調査では本文を取得できず（HTTP 403）、2回目にブラウザーで日本語版を読んだ（[2回目の調査](#hd-2dのライティングカメラポストプロセス2回目)）。
- [Octopath Traveler II Devs Aimed To Make Its HD-2D Visuals 'Picture-Perfect'（Nintendo Life）](https://www.nintendolife.com/news/2022/10/octopath-traveler-ii-devs-aimed-to-make-its-hd-2d-visuals-picture-perfect)
  - 背景の解像度を上げるだけではキャラが浮いて見えるため、複数の手当てが必要だったという。いつ撮っても絵になる画面を目指したと述べている。
- [How do games like Octopath Traveler handle the angle of shadows（Unity Discussions）](https://discussions.unity.com/t/how-do-games-like-octopath-traveler-handle-the-angle-of-shadows-and-is-this-even-possible-in-unity-urp/246459)
  - 見下ろしのカメラでスプライトをカメラへ傾けると影の形が変わる問題と、影だけを描く傾けない板、シェーダーでの向き直し、カメラの投影行列の工夫といった対策が議論されている。本作は、見える板と影の板を分け、見える板が影を受けない形にした。
- [Use built-in shader methods for additional lights（Unity 6 マニュアル）](https://docs.unity3d.com/Manual/urp/use-built-in-shader-methods-additional-lights-fplus.html)
  - 自作シェーダーで追加のライトを受けるときのキーワード（`_ADDITIONAL_LIGHTS`、`_CLUSTER_LIGHT_LOOP`）と、`LIGHT_LOOP_BEGIN` に渡す `InputData` の書き方。Unity 6.1以降は `_FORWARD_PLUS` ではなく `_CLUSTER_LIGHT_LOOP` を使う。
- [Depth of Field volume override（Unity 6 マニュアル）](https://docs.unity3d.com/Manual/urp/depth-of-field-volume-override-reference.html)
  - Gaussianは奥のぼかしだけで軽く、Bokehは手前と奥をぼかすが重い。どちらもポストプロセスの段で全体に掛かり、深度を書かない舞台のUIまでぼかす。1回目は画面の高さで決める疑似ティルトシフトを使い、2回目に、半透明の列の前に舞台だけをぼかす処理（`Hd2dStageFocus`）を作った。
- [Shadows optimization（Unity 6 マニュアル）](https://docs.unity3d.com/Manual/shadows-optimization.html)
  - 点光源の影は6方向を描き、スポットライト6つ分の負荷になる。本作は影を落とす点光源をHomeの焚き火1つに絞った。

## HD-2Dのライティング・カメラ・ポストプロセス（2回目）

調査日は2026-10-02。
ホームを昼の森に変え、停止中もHD-2Dに見せる作業で、「オクトパストラベラー 作り方」「HD-2D 作り方」などで調べ直した資料である。
開発者の発言（何を狙ったか）と、再現例・技術記事（どう作れるか）と、本作での判断を区別する。

開発者の発言：

- [『オクトパストラベラーⅡ』は、その見事な HD-2D スタイルで、より大きく、より大胆な世界を創り上げる（Unreal Engine）](https://www.unrealengine.com/ja/developer-interviews/octopath-traveler-ii-builds-a-bigger-bolder-world-in-its-stunning-hd-2d-style)
  - ドット絵のゲームに見えるぎりぎりの高い密度で絵を作り、昼・夜などの動的ライティングでグラフィックの進化を見せたという。
  - キャラは厚みのない紙のような状態で、背景はほぼ3Dのため、キャラと親和性の高い背景表現が難しいと述べている。
  - 「ライティングでウソをつかないとキャラクターと背景のマッチングが破綻する」箇所があり、2D・3Dの両方から検討したという。本作は、キャラの板の光を底上げし、木漏れ日のクッキーをキャラに当てない形にした。
  - 時間帯の変化、イベントのカメラワーク、バトルのアビリティをシーケンサーで作り、90度近い・180度近いカメラ回転まで攻めたという。
- [Octopath Traveler's "HD-2D" art style and story make for a JRPG dream come true（Unreal Engine）](https://www.unrealengine.com/en-US/spotlights/octopath-traveler-s-hd-2d-art-style-and-story-make-for-a-jrpg-dream-come-true)
  - 戦闘で普通のエフェクトだけでは物足りず、エフェクトと同時に点光源を置き、キャラの影を背景に落としたという。
  - PlayStation時代の、3Dの背景や1枚絵の上に2Dのキャラを置く作品を参考にしたという。
- [Nintendo Switch向け『オクトパストラベラー』が上質なクオリティに辿り着くまでの軌跡【Unreal Fest East 2018】（AUTOMATON）](https://automaton-media.com/devlog/report/20181023-78326)
  - 動的な光源でドット絵を照らし、環境光を入れて建物の影が真っ黒にならないようにしたという。
  - コマンド入力待ちではカメラをゆらゆら揺らして画面の退屈さを防ぎ、ブーストなど山場ではカメラを固定してメリハリを付けたという。本作のホームとタイトルの揺れの根拠にした。
  - 大技でキャラの真下から照らす演出、レンズフレアも使ったという。
- [「プロジェクト オクトパストラベラー」の記事（4Gamer.net、2017年9月23日）](https://www.4gamer.net/games/368/G036845/20170923027/)
  - 被写界深度で奥行きを出し、ダンジョンでランタンを持つと光源がぼんやり処理される、と紹介している。
- [Project Octopath Traveler Developers Answer How The Project Started, And The Troubles In Developing HD-2D（Siliconera）](https://www.siliconera.com/project-octopath-traveler-developers-answer-project-started-troubles-developing-hd-2d/)
  - 最初は奥行きが足りずに面白くなく、逆に解像度と彩度を上げすぎるとドット絵の魅力を失ったという。本作の色調を、彩度を少し上げる程度にとどめた根拠にした。
  - 大きな画面ではスプライトが寂しく見えたため、タイルの種類と色を増やして密度を上げたという。
- [Triangle Strategy producers talk HD-2D（Nintendo Life）](https://www.nintendolife.com/news/2022/05/triangle-strategy-producers-talk-hd-2d-and-why-other-devs-havent-used-it)
  - デフォルメしたキャラに写実的な光を当てることで、ジオラマのような見た目になると述べている。

再現例・技術記事：

- [【2022年】オクトパストラベラっぽいHD-2Dの世界を再現する（note）](https://note.com/game_shiori/n/n8e3870afb515)
  - Unityで、テクスチャを低解像度・Pointで読み、Bloom、被写界深度、Color Grading、Vignette、光の筋と粒子を組み合わせて再現している。被写界深度の焦点は、カメラからキャラまでの距離に合わせている。
- [How do I make a game that looks like Octopath Traveler?（RPG Maker Forums）](https://forums.rpgmakerweb.com/threads/how-do-i-make-a-game-that-looks-like-octopath-traveler.173962/)
  - 画面の中央だけにピントが合うティルトシフト、強いボケとコントラスト、HDRのBloomが見た目の要だと議論している。深度の情報がないと距離に応じたぼかしにならない点も指摘している。
- [Creating an HD-2D Rendering Pipeline on DX12（DEV Community）](https://dev.to/gaurav_de/creating-an-hd-2d-rendering-pipeline-on-dx12-205k)
  - 被写界深度で「小さなおもちゃの世界」に見せる。手前のくっきりした物に奥のぼけがにじまないよう、深度で重みを付けて集める方法を説明している。本作の舞台のレンズの「奥の点は手前のくっきりした点に掛からない」重み付けの参考にした。
- [HD-2D（Wikipedia）](https://en.wikipedia.org/wiki/HD-2D)
  - ティルトシフト、被写界深度、Bloom、ボリュームライト、霧、粒子、パララックスを組み合わせる表現として説明している。

Unityの資料：

- [Split Toning（Unity 6 マニュアル）](https://docs.unity3d.com/Manual/urp/Post-Processing-Split-Toning.html)
  - 明るさに応じて影と光に別の色を付ける。色の明るさ（Value）を変えると画面全体の明るさが変わるため、色相と彩度だけを変えるよう勧めている。
- [Light component reference for URP（Unity 6 マニュアル）](https://docs.unity3d.com/Manual/urp/light-component.html)
  - 平行光のCookieはRGBのテクスチャを模様の光として投影し、Cookie Sizeで軸ごとの大きさ、Cookie Offsetで位置を決める（平行光だけ）。本作のURP 17.6では、コードから `UniversalAdditionalLightData.lightCookieSize` で大きさを決める。
- [Details of disabling scene reload（Unity 6 マニュアル）](https://docs.unity3d.com/Manual/configurable-enter-play-mode-details.html)
  - シーンの再読み込みを省くと、シーンを読み込み直すときの初期化が省かれる。本作では、停止中に動かした部品の `Awake` がPlay Modeで呼ばれないことを確かめ、登録を `OnEnable` へ移した。

## HD-2Dの光芒と光だまり（3回目）

調査日は2026-10-03。
戦闘の背景10種類で「ライティングが足りない、もっとHD-2Dっぽく光を当て、光芒を作る」との依頼を受け、「HD-2D lighting god rays」「Unity URP god rays」「ドラクエ3 HD-2D ライティング」などで調べた資料である。
開発者の発言・解説（何を狙ったか）と、技術記事（どう作れるか）と、本作での判断を区別する。

開発者の発言と解説：

- [HD-2D（Wikipedia）](https://en.wikipedia.org/wiki/HD-2D)
  - 場面に点光源を置き、キャラと物に影を落とすと説明している。浅野智也プロデューサーは、光と影でドット絵の環境に光がどう当たるかを見せることに力を入れたという。本作は、光芒が落ちる所とランタン・燭台の足元に光だまりを敷き、光の当たる場所を見せた。
  - 動的なライティング、被写界深度、ティルトシフト、Bloom、ボリュームライト、霧、粒子を組み合わせる表現として挙げている。
- [Octopath Traveler II devs on the game's evolved use of HD-2D（Nintendo Everything）](https://nintendoeverything.com/octopath-traveler-ii-devs-on-the-games-evolved-use-of-hd-2d-and-more/)
  - いつスクリーンショットを撮っても絵になる密度を目指したという。ライティングの具体的な方法は述べていない。
- [HD-2D版『ドラクエ3』新たな比較動画（Game*Spark）](https://www.gamespark.jp/article/2024/10/17/146090.html)・[ピラミッドの比較動画（Game*Spark）](https://www.gamespark.jp/article/2024/10/18/146136.html)
  - 洞窟とピラミッドで、暗い中に小さな明かりを置く照明表現を紹介している。本作の坑道・水晶の洞窟・古城で、暗い舞台に光源の色の光だまりを置く根拠にした。

技術記事：

- [How to create godrays in Unity URP（Game Developer）](https://www.gamedeveloper.com/production/how-to-create-godrays-in-unity-urp)
  - URPでは本物のボリュームライトを作れないため、加算の粒子を細長く伸ばし、透明度を時間で上げ下げし、ノイズで揺らして光芒に見せている。本作は、既存の光の筋の板（カメラへ向く加算の板、縞の揺らぎ）を、色と向きを舞台ごとに変えて使った。
- [Volumetric Light Scattering as a Custom Renderer Feature in URP（Kodeco）](https://www.kodeco.com/22027819-volumetric-light-scattering-as-a-custom-renderer-feature-in-urp)・[Corgi God Rays（Unity Discussions）](https://discussions.unity.com/t/corgi-god-rays-volumetric-lighting-for-urp-released/877854)
  - 画面空間で光源から放射状にぼかす方法と、影のマップを使う有料アセットである。画面全体の追加の描画が要り、スマートフォンの負荷が大きいため採らなかった。
- [Lighting basics that transform your game's look（Bugnet）](https://bugnet.io/blog/lighting-basics-that-transform-your-games-look)・[Game lighting design（GamineAI）](https://gamineai.com/blog/game-lighting-design-creating-atmosphere-and-mood-in-your-games)
  - 主光源・補助光・縁の光の3点の照明、明るさの差で視線を導くこと、注目させたい所に光だまりを置くこと、暖色の光と寒色の影を離すことを勧めている。本作は、光芒を戦場の中央や奥の見せ場に落とし、暗い舞台は寒色の天井の光と暖色のランタンを対比させた。

本作での判断：

- 光芒は、加算の板を光の向きに傾け、舞台ごとの色と強さのマテリアルで描く。画面空間のボリュームライトは使わない。
- 光芒が落ちた所には、加算の光だまりを地面に敷く。光芒の板は地面の近くで消えるため、光だまりがないと光が地面に届いて見えない。
- 主光源の色は中立に近く保ち、場所の色は光芒・光だまり・点光源・描いた遠景に任せる。

## 小物と汚し・法線マップ

調査日は2026-10-03。
3つの舞台に小物と汚しを足し、光を受ける面に法線マップを付ける作業で調べた資料である（hd2d-stage-set の[小物と汚しで密度を上げる](../../hd2d-stage-set/references/set-dressing.md)）。
密度を上げる根拠は、[2回目](#hd-2dのライティングカメラポストプロセス2回目)の開発者の発言（大きな画面で寂しく見えたためタイルの種類と色を増やした、ドット絵に見えるぎりぎりの密度で描いた）を使った。

- [Simple Lit shader material Inspector window reference for URP（Unity マニュアル）](https://docs.unity3d.com/Manual/urp/simple-lit-shader.html)
  - Simple Litは法線マップで凹凸・傷・溝を加えられ、強さを0〜1で調整できると説明している。本作のURP 17.6では、`SimpleLit.shader` が `BUMP_SCALE_NOT_SUPPORTED` を定義して強さを無視することを、パッケージのソースで確かめた。強さは画像に焼き込んだ。
- [Mesh.tangents（Unity Scripting API）](https://docs.unity3d.com/ScriptReference/Mesh-tangents.html)
  - 接線はUVの横方向に沿う向きで、法線マップを使うシェーダーが使う。コードで作るメッシュは自分で計算する必要がある。本作は `MeshStore.Add` で `RecalculateTangents` を呼んだ。
- [Composition in level design（Game Developer）](https://www.gamedeveloper.com/design/composition-in-level-design)
  - 主役を最初に目に入る位置に置いて明るく照らし、ほかは影に置く、手前で枠を作り奥は控えめな色と描き込みにする、描き込みの多い所と空いた所で対比を作る、物の大きさを変えて単調さを避ける、と説明している。hd2d-stage-set の「ごちゃごちゃ具合の決め方」（休ませる所を先に決め、それ以外を描き込む、大・中・小・面を重ねる）の根拠にした。
- [Octopath Traveler II builds a bigger, bolder world in its stunning HD-2D style（Unreal Engine）](https://www.unrealengine.com/en-US/developer-interviews/octopath-traveler-ii-builds-a-bigger-bolder-world-in-its-stunning-hd-2d-style)
  - 続編ではマップの解像度を上げ、一枚一枚に有機的なドット絵の見え方を持たせ、その上に昼夜の動的な光を当てたという。本作は、小物と汚しのテクスチャを地面と近い細かさで描き、細い葉や刃だけを細かくした。

## 炎の作り方

調査日は2026-10-03。
Topの松明の炎がろうそくのような1枚の涙形で物足りないと相談を受け、炎を作り直すときに調べた資料である（[炎を実物に近づける](hd2d-3d-stage.md#舞台の組み方)）。
実物の炎の性質（何がそう見せるか）と、ゲームの炎の作り方（どう描けるか）と、本作での判断を区別する。

実物の炎の性質：

- [Flame（Wikipedia）](https://en.wikipedia.org/wiki/Flame)
  - ろうそくの炎は、芯の近くに燃える前の気体の暗い部分があり、その上で細かい煤が熱で光って黄色く見える。温度が上がるほど、色は赤から橙・黄・白へ移る。本作は、芯を黄白く、外側と先を橙から暗い赤にし、器の穴から見える根元を暗い橙にした。
- [A review of research and experimental study on the pulsation of buoyant diffusion flames and pool fires（Loughborough University）](https://repository.lboro.ac.uk/articles/journal_contribution/A_review_of_research_and_experimental_study_on_the_pulsation_of_buoyant_diffusion_flames_and_pool_fires/9547682)
  - 浮力で燃える炎の揺らぎ（ふくらみの周期）は、燃える面の直径Dの平方根に反比例し、f＝1.68×D^-0.5（Hz）がよく合うとまとめている。本文ではなく検索結果の要旨で確かめた。本作は炎の幅から周期を決め（係数1.6）、幅0.38mのTopの松明は約2.6回/秒、幅0.7mの戦闘のかがり火は約1.9回/秒で揺れる。
- [ME researchers unveiled the secret of the flicking of diffusion flames（香港理工大学）](https://polyu.edu.hk/me/news-and-events/news/2018/me-researchers-unveiled-the-secret-of-the-flicking-of-diffusion-flames)
  - 揺らぎは、浮力で炎の面に沿って生まれるドーナツ状の渦が、周期的にでき、はがれていくことで起きると説明している。本作は、根元から昇るふくらみとくびれが先をちぎる動きとして描いた。

ゲームの炎の作り方：

- [Creating a Campfire VFX in UE4（80 Level）](https://80.lv/articles/creating-a-campfire-vfx-in-ue4)
  - 焚き火を、炎・煙・火の粉・熱のゆらぎ・燃える薪・偽の影の6つのシェーダーで組んでいる。炎は、形のマスクを、流れるノイズでゆがめ、別のノイズで削って作る。本作は、形を画像ではなく計算の勾配で持ち、同じ考え方で削った。
- [Create a candle flame（Game Inspired）](https://gameinspired.substack.com/p/create-a-candle-flame)
  - 炎の形を、伸ばしたノイズ（大きな動き）と通常のノイズ（細部）の2つを流してゆがめ、連番画像を使わずに揺らしている。加算・ライティングなしで描く。
- [Capstone: a living fire（Wayline）](https://www.wayline.io/learn/shaders/19)
  - ノイズを重ねたfBMの座標を、さらにノイズでゆがめる（ドメインワーピング）と、渦を巻く炎になると説明している。本作は、大きな渦のノイズで、舌を削るノイズの座標をゆがめた。
- [Procedural Torch & Candle Shader（Godot Shaders）](https://godotshaders.com/shader/procedural-torch-candle-shader-fire-smoke-sparks/)
  - 粒を上へ流して炎・煙・火花を描き、上ほど横の揺れを大きくし、色を下の熱い色から上の煙の色へ移している。本作は、根元を動かさず上ほど大きく振る形を取り入れた。
