# VFX制作の調査資料

調査日は2026-10-01。
URLと内容は更新されるため、実装へ反映するときは、プロジェクトのUnity `6000.6.0f1` とURP `17.6.0` に対応する公式マニュアルを確かめ直す。

「取得」の欄は、ページ本文を読んで確かめたもの（本文）、検索結果の要約だけで確かめたもの（要約）、読めなかったもの（未取得）を区別する。
要約と未取得の資料は、本文で裏付けが取れるまで、内容を断定の根拠にしない。

## 設計の原則

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

## 技法

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

## HD-2Dとの組み合わせ

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

## 画面効果と手応え

| 資料 | 種類 | 使った内容 | 取得 |
|---|---|---|---|
| [Math for Game Programmers: Juicing Your Cameras With Math（GDC 2016、Squirrel Eiserloh氏）の紹介](https://www.gamedeveloper.com/programming/video-sprucing-up-cameras-with-math) | 一次（講演動画の紹介） | トラウマ値とノイズによる画面揺れ | 要約 |
| [Where Does Game Feel Come From（eastondev）](https://eastondev.com/blog/en/posts/dev/20260521-game-feedback-feel/) | 二次 | 「Juice it or lose it」（GDC 2012）の紹介、ヒットストップの目安 | 要約 |

## 安全

| 資料 | 種類 | 使った内容 | 取得 |
|---|---|---|---|
| [Xbox Accessibility Guideline 118（Microsoft）](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/118) | 一次 | 明滅の頻度（約3回/秒）、面積（約20%）、赤の明滅、縞模様の基準、Harding FPAテスト | 本文 |
| [Understanding SC 2.3.1 Three Flashes or Below Threshold（W3C）](https://www.w3.org/WAI/WCAG21/Understanding/three-flashes-or-below-threshold.html) | 一次 | 1秒間に3回を超える明滅を避ける基準 | 未取得（既知の基準として参照） |

## Unityの公式資料

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

## プラグイン

| 資料 | 使った内容 | 取得 |
|---|---|---|
| [ParticleEffectForUGUI（mob-sakai）](https://github.com/mob-sakai/ParticleEffectForUGUI) | MITライセンス、uGUIでParticle Systemを描く、URP対応、すべてのRender Mode、マスクと並び順、Unity 6の明記なし | 本文（README） |

## 読み方

Unityの公式資料は、API、対応条件、性能上の制約の根拠に使う。
講演とスタイルガイドは、設計の原則の根拠に使う。
フォーラムの投稿と解説記事は、技法の具体例として使い、採用前にUnityの対象バージョンで確かめる。
特定の作品の内部実装は、開発者が公開した範囲を超えて推測しない。
