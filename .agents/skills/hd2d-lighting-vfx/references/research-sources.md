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
