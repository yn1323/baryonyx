# 参考資料

章番号は元のガイド「RPG戦闘VFX：専門用語・計算式・Claudeへの指示ガイド」（2026-10-26）の番号である。第1章・第7章・第8章は [SKILL.md](../SKILL.md)、第2〜3章は [glossary.md](glossary.md)、第4章（F1〜F12）は [formulas.md](formulas.md)、第5章は [recipes.md](recipes.md)、第6章は [prompts.md](prompts.md)、第9章は [sources.md](sources.md)、第10章（P1〜P6）は [procedural.md](procedural.md)、第11章は [lessons.md](lessons.md) にある。[S1] などの記号は [sources.md](sources.md) の資料を指す。

2026年10月26日に確認。公式資料の機能説明と、作者本人の制作事例を分けている。リンク先のバージョン・対応パイプラインが現在のプロジェクトと一致するとは限らない。以下の資料が本書のすべてのレシピや数式を保証するわけではない。

## Unity公式：描画と実装

- [S1：ShaderLabのブレンドモード](https://docs.unity.com/en-us/engine/6000.6/manual/materials-and-shaders/shaders/writing-custom/shader-writing/sl/writing-shader-render-state-commands/blending-modes) — 通常α・Premultiplied・加算・乗算のBlend設定。
- [S2：Particle System Noise](https://docs.unity3d.com/2020.1/Documentation/Manual/PartSysNoiseModule.html) — 粒子の乱れ、Curl Noiseベースの説明。旧バージョン資料のため現環境で設定を確認する。
- [S3：Particle System Trails API](https://docs.unity3d.com/ja/2022.3/ScriptReference/ParticleSystem.TrailModule.html) — 軌跡、色、幅などの制御。
- [S4：Fresnel Effectノード](https://docs.unity3d.com/ja/Packages/com.unity.shadergraph@10.0/manual/Fresnel-Effect-Node.html) — 視線と法線の角度に応じた値、リム表現。
- [S5：URP AssetのOpaque Texture](https://docs.unity.com/en-us/engine/6000.5/manual/render-pipelines/universal-render-pipeline/urp-reference/universalrp-asset) — 透明描画前の背景取得、屈折や熱波への利用。
- [S6：Texture Sheet Animation](https://docs.unity3d.com/6000.2/Documentation/Manual/PartSysTexSheetAnimModule.html) — 用語の背景資料。今回の方針では外部の連続画像素材を採用しない。
- [S8：URP Bloom](https://docs.unity.com/en-us/engine/6000.6/manual/post-processing-and-full-screen-effects/urp/effect-list/bloom) — にじみの閾値や強度など。
- [HDRP：屈折マテリアルの作成](https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.high-definition/Documentation~/create-a-refractive-material.md) — HDRP固有の方法。URPの手順として扱わない。
- [Unity公式：高度なVFXのガイド](https://create.unity.com/definitive-guide-to-creating-advanced-visual-effects) — UnityのVFX制作ツールの概要。

## 作者本人の解説・作例

- [S7：Discharge Strike — Illusorisch](https://realtimevfx.com/t/discharge-strike-vfx-breakdown/10878) — 雷のメッシュ、流れるテクスチャ、タイミング、ライト、火花、歪み、衝撃波の分解。Unreal／Houdini使用で、Unityの操作手順ではない。
- [S9：Blood and Poison — Gabriel Aguiar](https://gabrielaguiarproductions.artstation.com/projects/nQO44o) — Photoshop素材を使う例のため、見た目・構成のみ参考にする。素材制作手順は今回の方針に採用しない。
- [S10：Weapon Trails — Gabriel Aguiar](https://gabrielaguiarproductions.artstation.com/projects/wDr9Z) — Unityの剣の軌跡と斬撃の作例・チュートリアル。
- [S11：Healing Spell FX](https://realtimevfxstore.com/products/healing-spell-fx) — 魔法の輪、上昇する光、粒子への展開。商品作例であり詳細な解説記事ではない。掲載対応はBuilt-in。
- [S12：Shield Effect — Gabriel Aguiar](https://gabrielaguiarproductions.artstation.com/projects/owb1O) — Unityの結界の作例。古い制作環境の例。
- [Gabriel Aguiar：公式サイト](https://www.gabrielaguiarprod.com/) — 属性・武器・魔法などの制作例。教材やプロジェクトファイルには有料のものもある。

## AIへの指示・制作ワークフロー

- [Coplay：We Taught AI to Build VFX in Unity. It Kinda Worked.](https://coplay.dev/blog/ai-vfx) — AIによるシェーダー選択や構成の問題を、制作レシピ・テクスチャ・視覚フィードバックで改善した開発記録。
- [Unity＋Claude Codeで炎を制作する日本語記事](https://qitz-plus.x0.com/unity-claude-code-%E3%81%A7-particlesystem%EF%BC%88%E7%82%8E%E3%82%A8%E3%83%95%E3%82%A7%E3%82%AF%E3%83%88%EF%BC%89%E3%82%92%E4%BD%9C%E6%88%90%E3%81%99%E3%82%8B%E6%96%B9%E6%B3%95/) — 外部スプライトを使う以前の調査例。今回の実装手順には採用せず、コードへ制御を任せる例としてのみ残す。

## 外部画像なしの技術（P-S）

- [P-S1：Unity Shader Graph Proceduralノード](https://docs.unity3d.com/ja/Packages/com.unity.shadergraph@10.0/manual/Procedural-Nodes.html) — 座標からの形、Gradient Noise、Simple Noise、Voronoi。記載版は古いため現在のパッケージで確認する。
- [P-S2：Alan Zucconi — Volumetric Rendering](https://www.alanzucconi.com/2016/07/01/volumetric-rendering/) — Unityでのボリューム描画、レイマーチング、距離関数の導入。古いパイプライン用コードはそのまま移植せず、現環境に合わせる。本書の密度積分式は設計用の補足であり、リンク先のコードの転載ではない。
- [P-S3：Catlike Coding — Pseudorandom Noise](https://catlikecoding.com/unity/tutorials/pseudorandom-noise/) — ハッシュ、Value、Perlin、Voronoi、Simplexノイズの実装を学べる作者のシリーズ。
- [補助資料：Texture Distortion](https://catlikecoding.com/unity/tutorials/flow/texture-distortion/) — 流れによる座標変形の考え方。元記事は画像を使うため、その素材手順は採用しない。座標変形の原理だけを手続き的ノイズへ応用する。

本資料は指示・設計ガイド。実際のプロジェクトに対する実装・動作確認・描画品質や性能の検証はまだ行っていない。

## これまでの調査（2026-10-01〜10-23）

旧版のスキルで原則・技法・HD-2Dとの組み合わせ・AIでの制作の根拠にした資料である。[lessons.md](lessons.md) の経験の背景として残す。
「取得」の欄は、ページ本文を読んで確かめたもの（本文）、検索結果の要約だけで確かめたもの（要約）、読めなかったもの（未取得）を区別する。要約と未取得の資料は、本文で裏付けが取れるまで、内容を断定の根拠にしない。

### 設計の原則

| 資料 | 種類 | 使った内容 | 取得 |
|---|---|---|---|
| [Dev: League's VFX Style Guide（Riot Games, 2017）](https://nexus.leagueoflegends.com/en-us/2017/10/dev-leagues-vfx-style-guide/)、[公開版PDF](https://nexus.leagueoflegends.com/wp-content/uploads/2017/10/VFX_Styleguide_final_public_hidpjqwx7lqyx0pjj3ss.pdf) | 一次 | 視覚的な重要度、主要素と副要素、明度の幅、彩度、補色、手描きの形、動きの表現、時間、長さ | 未取得（公式サイトのトップへ転送された） |
| [10 Design Tips from the League of Legends VFX Style Guide（VFX Apprentice）](https://www.vfxapprentice.com/blog/10-league-of-legends-vfx-design-tips) | 二次 | 上のスタイルガイドの10項目の要約 | 本文 |
| [Dev: Clarity in League（Riot Games）](https://riotgames.com/en/news/dev-clarity-in-league) | 一次 | ゲームの読みやすさとエフェクトの関係 | 未取得（検索で存在のみ確認） |
| [The Soul of Effects: What is Timing in VFX?（VFX Apprentice）](https://www.vfxapprentice.com/blog/the-soul-of-effects-what-is-timing-in-vfx) | 二次 | 予備動作・発生・余韻、単純な形で時間を決める、速い要素と遅い要素、音との合わせ方 | 本文 |
| [Visual Effects Bootcamp: Artistic Principles of VFX（GDC 2017、Jason Keyser氏・Hadidjah Chamberlain氏）の聴講メモ](https://3dreference.notion.site/Visual-Effects-Bootcamp-Artistic-Principles-of-VFX-GDC-2017-fcd0698c31cf4621a70ed8a7621e17da) | 二次 | 形と効果範囲、コントラストと焦点、色と個性、時間と脅威度という分類 | 未取得（本文が表示されず、検索結果の要約のみ） |
| [Art Directing VFX for Stylized Games（GDC 2017）](https://gdcvault.com/play/1024715/Art-Directing-VFX-for-Stylized)、[講演の紹介記事（Game Developer）](https://gamedeveloper.com/design/video-tips-on-art-directing-vfx-for-stylized-games) | 一次・二次 | エフェクトの柱を決め、ゲームのアート方針を解釈して形・色・時間をそろえる | 紹介記事の本文のみ |
| [VFXデザイナー 安達義博氏の解説（CGWORLD）](https://cgworld.jp/special/masterclass-online/vol20/page/adachi.html) | 二次 | 体験から逆算する設計、形・色・タイミングの決め方、感覚を言葉にする | 本文 |
| [CEDEC 2019「こっそり教えます！エフェクトデザインのイ・ロ・ハ」の講演レポート（gamebiz）](https://gamebiz.jp/news/248246) | 二次（講演の報告） | 円と線、不完全にする、誇張した形、粒の4属性、緩急、消え方の5つの方法、最悪の背景で作る | 本文 |
| [CEDEC 2011「アニメのエフェクト、ゲームのエフェクト」の講演レポート（GIGAZINE）](http://gigazine.net/news/20111019_anime_effect_cedec2011/) | 二次（講演の報告） | シルエットの変化、密度の差、物理を曲げる、時間の違う要素を混ぜる、火花の流れ、煙の濃淡 | 本文 |
| [ポリゴン・マジックのエフェクト制作の連載 第5回（CGWORLD）](https://cgworld.jp/regular/1410-polygonmagic05.html) | 二次 | 白飛びの失敗と対策、手前と奥の配置、照り返し | 本文 |
| [FX Timing Principles（VFX Apprentice）](https://www.vfxapprentice.com/courses/fx-timing-principles) | 二次（講座の紹介） | フォロースルーとオーバーラップ、副要素も含めて全体を弧に乗せる | 本文（2026-10-23） |
| [Give me your Feedback and Critique Please（realtimevfx）](https://realtimevfx.com/t/give-me-your-feedback-and-critique-please/23059) | 二次（講評） | 要素が一瞬で現れて消えるのは現実ではまれ、透明度やノイズで溶かす、雷は消えたあとも像が残る | 本文（2026-10-23） |
| [色相のずらし方の議論（Pixel Joint）](https://pixeljoint.com/forum/forum_posts.asp?TID=15138) | 二次（フォーラム） | 色の段階で色相をずらしてコントラストを上げ、単調さを避ける。明るい側と暗い側を補色に寄せる | 本文（2026-10-23） |

### 技法

| 資料 | 種類 | 使った内容 | 取得 |
|---|---|---|---|
| [Exploring and Modernizing The VFX Methods of Diablo 3（JangaFX）](https://jangafx.com/insights/diablo-3-vfx-experiments) | 二次（GDC 2013のJulian Love氏の講演の検証） | ノイズの掛け合わせ、UVスクロールの速さと拡大率、Scale by Mids、グラデーションによる陰影、光る要素の後ろに黒を敷く合成 | 本文 |
| [Diablo 3 – Resource Bubbles（Simon Trümpler）](https://simonschreibt.de/gat/diablo-3-resource-bubbles) | 二次 | テクスチャを掛け合わせて動きを作る例 | 要約 |
| [Small VFX tips（realtimevfx）](https://realtimevfx.com/t/small-vfx-tips/923) | 二次（制作者の投稿） | 乗算済みアルファで加算と通常を1つのシェーダーにまとめる、Custom Vertex Streamsで値を渡す | 本文 |
| [Realtime VFX Fundamentals（realtimevfx）](https://realtimevfx.com/t/realtime-vfx-fundamentals/1503) | 二次 | カメラを向く板を重ねて立体感を作る基礎 | 本文 |
| [Stylized Frag Launcher Explosion（realtimevfx）](https://realtimevfx.com/t/stylized-frag-launcher-explosion-overwatch-inspiration-breakdown-posted/1894) | 二次（制作者の投稿） | 爆発の要素数、衝撃の輪・閃光・煙・中心の粒の順番と役割、改良の過程 | 本文（層の順番は2ページ目の要約） |
| [Anatomy of a League of Legends missile - Part 2（realtimevfx）](https://realtimevfx.com/t/anatomy-of-a-league-of-legends-missile-part-2-what-are-we-communicating-with-our-vfx/13062) | 二次 | 飛び道具のエフェクトが何を伝えるか | 未取得（検索で存在のみ確認） |
| [Alpha erosion（vfxdoc）](https://vfxdoc.readthedocs.io/en/latest/shaders/alpha-erosion/) | 二次 | 閾値で薄い部分から削る仕組みと、一様なフェードとの違い | 要約 |
| [Unity - Custom Vertex Stream and Shadergraph（realtimevfx）](https://realtimevfx.com/t/unity-custom-vertex-stream-and-shadergraph-amplify-shader/9524/13) | 二次 | Custom DataとCustom Vertex Streamsで粒ごとの値をシェーダーへ渡す | 要約 |
| [5 years ago I was asking this forum how to make sword slash trails（realtimevfx）](https://realtimevfx.com/t/5-years-ago-i-was-asking-this-forum-how-to-make-sword-slash-trails-in-unity-now-i-built-a-tool-for-it-and-id-love-your-feedback/31306) | 二次 | リボンのメッシュに端を固定した素材を流す斬撃の軌跡 | 要約 |
| [Free VFX image sequences and flipbooks（Unity公式ブログ）](https://unity.com/blog/engine-platform/free-vfx-image-sequences-flipbooks) | 一次 | Unity Labsが公開した連番素材 | 要約 |
| [How to fix screen space heat haze distortion not visible（bugnet）](https://bugnet.io/blog/how-to-fix-screen-space-heat-haze-distortion-not-visible) | 二次 | URPのOpaque Textureを使う歪みの手順 | 要約 |
| [PreMulAlpha（GitHub）](https://github.com/dtrebilco/PreMulAlpha) | 二次 | すべての粒を乗算済みアルファにすると描画をまとめやすい | 要約（2026-10-23） |
| [Premultiplied blend modeの質問（realtimevfx）](https://realtimevfx.com/t/im-having-a-hard-time-understanding-how-premultiplied-blend-mode-is-suppose-to-work-in-unity/17552) | 二次（フォーラム） | URPの一部の版で、Shader Graphの乗算済みアルファが通常のアルファ合成と同じに振る舞った | 本文（2026-10-23） |
| [Are transparent quads ok on mobile?（Unity Discussions）](https://discussions.unity.com/t/are-transparent-quads-ok-on-mobile/857421) | 二次（フォーラム） | 透明な板の重さは、数より重なりの度合いと画面に占める大きさで決まる | 本文（2026-10-23） |
| [How to fix perf overdraw from stacked transparent shader fillrate（bugnet）](https://bugnet.io/blog/how-to-fix-perf-overdraw-from-stacked-transparent-shader-fillrate) | 二次 | 透明な板を見える形に合わせて小さくする | 要約（2026-10-23） |

### 計算で描く形

| 資料 | 種類 | 使った内容 | 取得 |
|---|---|---|---|
| [2D distance functions（Inigo Quilez）](https://iquilezles.org/articles/distfunctions2d/)、[distance functions](https://iquilezles.org/articles/distfunctions) | 二次（技法の定番の解説） | 基本の形の距離関数、和集合・共通部分・滑らかな最小値での組み合わせ | 要約 |
| [Fractal Brownian Motion（The Book of Shaders）](https://thebookofshaders.com/13/) | 二次 | 細かさを2倍ずつ上げたノイズを強さを下げて足すfBM | 要約 |
| [Procedural Nodes（Shader Graph）](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.1/manual/Procedural-Nodes.html) | 一次 | 円・多角形・角丸の四角をUVから描くノード | 要約 |
| [Canvas.additionalShaderChannels](https://docs.unity3d.com/ScriptReference/Canvas-additionalShaderChannels.html) | 一次 | uGUIの頂点にUV1以降を含める設定 | 要約 |
| [Shock Wave Shader（Godot Shaders）](https://godotshaders.com/shader/shock-wave-shader/) | 二次（制作者の投稿） | 極座標とノイズで衝撃波を描く例 | 要約 |
| [Arm Mali Best Practices（Arm）](https://developer.arm.com/community/arm-community-blogs/b/mobile-graphics-and-gaming-blog/posts/arm-mali-best-practices) | 一次 | GPUごとに計算と画像の読み込みの性能比が違う | 要約 |
| [Aspect correct coordinates（GameDev.net）](https://gamedev.net/shaderlab/16-aspect-correct-coordinates/) | 二次 | 横の座標に縦横比を掛けて、縦横の単位をそろえてから円を描く | 未取得（403）。検索結果の要約で確認（2026-10-23） |
| [Domain warped noise（GameDev.net）](https://gamedev.net/shaderlab/23-domain-warped-noise/) | 二次 | 座標をゆがめてからノイズを読むと、一様な見え方が崩れる | 未取得（403）。検索結果の要約で確認（2026-10-23） |
| [shader-noise（skills.sh）](https://skills.sh/bbeierle12/skill-mcp-claude/shader-noise) | 二次 | 値ノイズは最も軽いが、ブロック状に見えやすい | 要約（2026-10-23） |
| [Fix Unity Shader Graph procedural UV noise banding（bugnet）](https://bugnet.io/blog/fix-unity-shader-graph-procedural-uv-noise-banding) | 二次 | ノイズの四角い帯を、勾配ノイズ・fBM・座標のゆがみで消す | 要約。本文は記事に届かなかった（2026-10-23） |
| [Help with procedural planets（Godot Forum）](https://forum.godotengine.org/t/help-with-procedural-planets/139951) | 二次（フォーラム） | 種をそのまま座標に足すと、fBMの細かい段で座標が大きくなり、精度不足で角ばって崩れる。種をハッシュして0〜8のずらしにした | 本文（2026-10-23） |
| [EStampes: Sketch #15 WIP（realtimevfx）](https://realtimevfx.com/t/estampes-sketch-15-wip/5388) | 二次（講評） | 浸食が一様に濃い面で止まり、縁が硬くなる。値を0〜1になだらかに分布させる | 本文（2026-10-23） |
| [Gradient banding issue on mobile（Unity Discussions）](https://discussions.unity.com/t/gradient-banding-issue-on-mobile/810994) | 二次（フォーラム） | 8ビットの段差をごく弱いノイズで散らす。ノイズを画面のピクセルに対応させる。圧縮で細かいノイズが消える | 本文（2026-10-23） |

### HD-2Dとの組み合わせ

| 資料 | 種類 | 使った内容 | 取得 |
|---|---|---|---|
| [HD-2D（SQUARE ENIX）](https://www.jp.square-enix.com/octopathtraveler/about/) | 一次 | ドット絵に3DCGの画面効果を加えた表現という定義 | 本文 |
| [Octopath Traveler's HD-2D art style and story（Unreal Engine）](https://www.unrealengine.com/en-US/spotlights/octopath-traveler-s-hd-2d-art-style-and-story-make-for-a-jrpg-dream-come-true) | 一次（開発者インタビュー） | エフェクトと同時に点光源を置き、キャラの影を環境に落とした | 未取得（403）。検索結果の要約で確認 |
| [オクトパストラベラーII 開発者インタビュー（Unreal Engine）](https://www.unrealengine.com/ja/developer-interviews/octopath-traveler-ii-builds-a-bigger-bolder-world-in-its-stunning-hd-2d-style) | 一次 | 背景のほぼ全面3D化と、キャラとのなじませ方 | 未取得（403）。検索結果の要約で確認 |
| [『オクトパストラベラー』が上質なクオリティに辿り着くまでの軌跡【Unreal Fest East 2018】（AUTOMATON）](https://automaton-media.com/devlog/report/20181023-78326) | 二次（アクワイアの講演の報告） | 大技で真下から照らす、動的な光源でドット絵の見え方を変える、待機中のカメラの揺れと山場の固定、ブレイクのスローモーション、環境光、レンズフレア | 本文 |
| [The Fusion of Nostalgia and Novelty in the Development of Octopath Traveler（Unreal Fest Europe 2019）](https://www.unrealengine.com/events/unreal-fest-europe-2019/the-fusion-of-nostalgia-and-novelty-in-the-development-of-octopath-traveler) | 一次（講演） | ドット絵の進化とHD-2Dの技術 | 未取得（403）。検索結果の要約のみ |
| [オクトパストラベラーIIのプレビュー（ファミ通）](https://www.famitsu.com/news/202302/09292097.html) | 二次（記者の体験記事） | ブーストを最大まで溜めたアビリティでカメラワークが切り替わる | 本文 |
| [オクトパストラベラーIIの開発者インタビュー（電撃オンライン）](https://dengekionline.com/articles/151745/) | 一次（インタビュー） | 等身を上げた理由、アビリティごとのモーション、イベントでのカメラ操作 | 本文 |
| [HD-2D（Wikipedia）](https://en.wikipedia.org/wiki/HD-2D) | 二次 | HD-2Dの構成要素の一覧（動的な光、被写界深度、ティルトシフト、Bloom、ボリュメトリックな光と霧、粒、カメラワーク） | 本文 |
| [Triangle Strategy and the art of HD-2D with Unreal Engine 4（foro3d）](https://foro3d.com/en/2026/mayo/triangle-strategy-y-el-arte-hd-2d-con-unreal-engine-4.html) | 二次（解説記事） | 被写界深度で強調したい要素を際立たせる、動的な光と柔らかい影でスプライトを地面に固定する | 要約 |
| [Digital Foundryの技術レビューの紹介（Nintendo Life）](https://www.nintendolife.com/news/2022/03/digital-foundry-shares-its-tech-review-of-triangle-strategy) | 二次 | トライアングルストラテジーの被写界深度と環境の陰影 | 未取得（検索で存在のみ確認） |
| [Sea of Stars プレスキット（Sabotage Studio）](https://sabotagestudio.com/presskits/sea-of-stars) | 一次 | スプライトの動的な光（法線マップ）、影、ボリュメトリックな光、高さの霧、Bloom | 要約 |
| [Sea of Stars: pixel art de 16 bits con iluminación dinámica en Unity（foro3d）](https://foro3d.com/2026/julio/sea-of-stars-pixel-art-de-16-bits-con-iluminacion-dinamica-en-unity.html) | 二次（解説記事） | 光を上に重ねる層ではなく、奥行きの見え方を決めるものとして扱う | 本文 |
| [半透明エフェクトにDepth of Fieldを適用する（Zenn、Happy Elements）](https://zenn.dev/happy_elements/articles/82acdf2785d23d) | 二次（開発者の技術記事） | URPの標準の被写界深度は半透明の深度を使わず、半透明のエフェクトにボケが掛からない。Unity 2022.3、URP 14での検証 | 本文 |
| [URP 2D renderer and 3D lights, is it possible?（Unity Discussions）](https://discussions.unity.com/t/urp-2d-renderer-and-3d-lights-is-it-possible/917603/2) | 二次（フォーラム） | URPの2D Rendererと3Dのライトは同じ描画で使えない | 本文 |
| [2D Lighting for Pixel Art（Unity Learn）](https://learn.unity.com/course/2d-lighting-for-pixel-art) | 一次 | Unity 6.3でのドット絵向けの2D Light、法線マップ、発光、影、ポストプロセス | 本文 |
| [Mini rant: stop using mixels（Flowlab Community）](https://community.flowlab.io/t/mini-rant-stop-using-mixels/38287)、[Pixel Art Glossary（pixnote）](https://pixnote.net/en/learn/glossary) | 二次 | ミクセル（ドットの大きさが違う絵の混在）が避けられる理由 | 要約 |

HD-2Dの作品の光と奥行きの構成要素は、[hd2d-lighting-vfxの調査資料](../../hd2d-lighting-vfx/references/research-sources.md) にもまとめている。

### 画面効果と手応え

| 資料 | 種類 | 使った内容 | 取得 |
|---|---|---|---|
| [Math for Game Programmers: Juicing Your Cameras With Math（GDC 2016、Squirrel Eiserloh氏）の紹介](https://www.gamedeveloper.com/programming/video-sprucing-up-cameras-with-math) | 一次（講演動画の紹介） | トラウマ値とノイズによる画面揺れ | 要約 |
| [Where Does Game Feel Come From（eastondev）](https://eastondev.com/blog/en/posts/dev/20260521-game-feedback-feel/) | 二次 | 「Juice it or lose it」（GDC 2012）の紹介、ヒットストップの目安 | 要約 |

### 安全

| 資料 | 種類 | 使った内容 | 取得 |
|---|---|---|---|
| [Xbox Accessibility Guideline 118（Microsoft）](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/118) | 一次 | 明滅の頻度（約3回/秒）、面積（約20%）、赤の明滅、縞模様の基準、Harding FPAテスト | 本文 |
| [Understanding SC 2.3.1 Three Flashes or Below Threshold（W3C）](https://www.w3.org/WAI/WCAG21/Understanding/three-flashes-or-below-threshold.html) | 一次 | 1秒間に3回を超える明滅を避ける基準 | 未取得（既知の基準として参照） |

### Unityの公式資料

| 資料 | 使った内容 | 取得 |
|---|---|---|
| [Bloom（URP）](https://docs.unity3d.com/Manual/urp/post-processing-bloom.html) | Thresholdより明るい部分にだけBloomが掛かる | 要約 |
| [Canvas（uGUI 2.6）](https://docs.unity3d.com/Packages/com.unity.ugui@2.6/manual/class-Canvas.html) | Render Modeごとの描かれ方 | hd2d-lighting-vfxの調査で確認 |
| [How to post-process after Screenspace Overlay Canvas?（Unity Discussions）](https://discussions.unity.com/t/how-to-post-process-after-screenspace-overlay-canvas/674743) | OverlayのUIはポストプロセスの後に描かれる | 要約 |
| [URPのブレンドモード](https://docs.unity3d.com/ja/6000.0/Manual/urp/blending-modes.html) | Alpha・Premultiply・Additive・Multiplyの合成式 | hd2d-lighting-vfxの調査で確認 |
| [ParticleSystem.TextureSheetAnimationModule](https://docs.unity3d.com/ScriptReference/ParticleSystem.TextureSheetAnimationModule.html) | 連番の再生、速さ、繰り返し、コマの進み方の曲線 | 要約 |
| [Flipbook Node（Shader Graph）](https://docs.unity3d.com/Packages/com.unity.shadergraph@12.1/manual/Flipbook-Node.html) | UVを指定したコマへずらす | 要約（版が古いため、対象版で再確認する） |
| [Canvas Shader Graph（URP）](https://docs.unity3d.com/Manual/urp/canvas-shader.html) | uGUI用のShader Graph、頂点の処理が使えない制約 | 要約 |
| [Canvas.additionalShaderChannels](https://docs.unity.cn/ScriptReference/Canvas-additionalShaderChannels.html) | uGUIのメッシュに追加のUVチャンネルを含める | 要約 |
| [Choosing Your Particle System](https://docs.unity.com/en-us/engine/6000.3/manual/visual-effects/particle-systems/choosing-your-particle-system) | Particle SystemとVFX Graphの規模と前提の違い | hd2d-lighting-vfxの調査で確認 |
| [Change particle color](https://docs.unity.com/en-us/engine/6000.0/manual/particle-color) | Color over LifetimeとColor by Speed | hd2d-lighting-vfxの調査で確認 |
| [Practical guide to optimization for mobiles](https://docs.unity3d.com/2017.3/Documentation/Manual/MobileOptimisation.html) | 描画の負荷とオーバードロー、粒には単純なシェーダー | 要約（旧版の資料） |
| [The definitive guide to creating advanced visual effects in Unity（Unity 6 edition）](https://unity.com/resources/creating-advanced-vfx-unity6) | VFX Graphを使う場合の入口 | 要約 |

### プラグイン

| 資料 | 使った内容 | 取得 |
|---|---|---|
| [ParticleEffectForUGUI（mob-sakai）](https://github.com/mob-sakai/ParticleEffectForUGUI) | MITライセンス、uGUIでParticle Systemを描く、URP対応、すべてのRender Mode、マスクと並び順、Unity 6の明記なし | 本文（README） |

### 現象（雷と雲）

| 資料 | 種類 | 使った内容 | 取得 |
|---|---|---|---|
| [What is lightning?（米国気象局）](https://www.weather.gov/mlb/what_is_lightning) | 一次 | 先駆放電、主放電、20〜50ミリ秒の間を置いて同じ経路を降りる後続の放電、後続ほど弱く短い、ちらつきの理由 | 本文（2026-10-23） |
| [Stolzenburg et al.（NSSL、ICAE 2014の予稿）](https://www.nssl.noaa.gov/users/mansell/icae2014/preprints/Stolzenburg_267.pdf) | 一次（研究） | 後続の放電の多くは、直前に使った通り道を通る | 要約（2026-10-23） |
| [Cumulonimbus clouds（Met Office）](https://weather.metoffice.gov.uk/learn-about/weather/types-of-weather/clouds/low-level-clouds/cumulonimbus) | 一次 | 積乱雲の底は平らで、暗い | 未取得（404）。検索結果の要約で確認（2026-10-23） |
| [Cumulonimbus（オクラホマ大学）](https://weather.ou.edu/~broberts/METR2011/clouds/types/cb.html) | 二次（講義資料） | 真下から見ると底は黒に近い灰色。カリフラワーのような上部 | 要約（2026-10-23） |

### AIで作るとき

| 資料 | 種類 | 使った内容 | 取得 |
|---|---|---|---|
| [AI-generated GLSL shaders（14islands）](https://www.14islands.com/journal/ai-generated-glsl-shaders) | 二次（制作会社の実験記事） | AIは結果を見ないため美的判断が弱い、計画を先に作らせると質が上がりエラーが減る、同じ指示でも結果がぶれる、重くなりやすい | 本文（2026-10-23） |
| [AI VFX（Coplay）](https://coplay.dev/blog/ai-vfx) | 二次（ツール開発会社の記事） | AIは空間の推論が弱い、静止画では動きと時間を判断できない、レシピと描画して見る工程が効いた | 本文（2026-10-23） |

