# 外部画像なしで質感を補う技術

章番号は元のガイド「RPG戦闘VFX：専門用語・計算式・Claudeへの指示ガイド」（2026-10-26）の番号である。第1章・第7章・第8章は [SKILL.md](../SKILL.md)、第2〜3章は [glossary.md](glossary.md)、第4章（F1〜F12）は [formulas.md](formulas.md)、第5章は [recipes.md](recipes.md)、第6章は [prompts.md](prompts.md)、第9章は [sources.md](sources.md)、第10章（P1〜P6）は [procedural.md](procedural.md)、第11章は [lessons.md](lessons.md) にある。[S1] などの記号は [sources.md](sources.md) の資料を指す。
試作で見つけた失敗と直し方は [計算で描くときのつまずき](lessons.md#計算で描くときのつまずき) にある。

この章と第1章の方針を優先する。第2章の画像関連用語は一般知識として残すが、外部の画像を用意する指示ではない。第5章の法線・亀裂・泡・紋様もすべて計算で生成する。

## 外部画像なしの構成

| 必要な情報 | コードによる代替 | 選び方 |
|---|---|---|
| 円・帯・弧・星・柔らかい粒 | 距離関数とsmoothstep | 主形状を鮮明に制御する |
| 炎・煙のまだらな濃淡 | Perlin／Simplex／Value NoiseとfBm | まず大きな流れ、次に微細な変化 |
| うねり・複雑な輪郭 | Domain Warping | ノイズを読む座標自体を別ノイズで歪める |
| 霧や火の粉の流れ | Curl Noise・解析的ベクトル場 | 流れる方向と局所的な渦を両立する |
| 泡・霜・亀裂の模様 | Worley／Voronoi、セル境界距離 | セル中心とセル境界の距離を混同しない |
| 水・氷の凹凸 | 高さ関数の微分から法線を生成 | 実際のメッシュの曲面・座標系と整合させる |
| 立体的な煙・毒霧・炎 | 3D密度場＋ボリューム積分 | 平面の重なりで不足する技に使用 |
| 氷柱・岩・矢・蔓 | C#で頂点・面・法線を生成 | SDFですべてを描こうとせず、実体にはメッシュも使う |
| 流動・波紋の履歴 | Compute Shader／RenderTextureの状態更新 | 単なる周期ノイズで不足する場合の発展案 |
| 頻繁に使うノイズや密度 | コード生成した2D／3Dテクスチャへキャッシュ | 外部画像を買う・描く工程は不要。直接評価でもよい |

Unity公式には、座標から形やGradient Noise・Simple Noise・Voronoiを生成するノードがある。[P-S1] これらは画像ファイルを読み込まずに模様を作る入口になる。ノードの次元数には制約があるため、3Dノイズが必要なら対応するHLSL関数や生成バッファを実装する。

## P1：SDF・距離マスクで形を作る

SDFは表面からの符号付き距離。内側を負、外側を正とする例：

```hlsl
// 2Dの円
float sdCircle(float2 p, float radius) { return length(p) - radius; }

// 2Dの線分。aとbが同じでも除算が発散しないよう保護
float sdSegment(float2 p, float2 a, float2 b, float radius) {
    float2 pa = p - a;
    float2 ba = b - a;
    float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-6));
    return length(pa - ba*h) - radius;
}

// 距離dから柔らかな輪郭を生成
float coverage = 1 - smoothstep(-softness, softness, d);
```

softnessは正数。鋭い輪郭のジャギー対策には、`max(fwidth(d), minSoftness)`等で画面上の変化量も考慮する。`fwidth`を使う例はフラグメントシェーダー向けで、Compute Shaderへそのまま移さない。

用途：火花、光輪、魔法陣、矢の尾、斬撃の芯。弧は円環の距離と角度範囲を組み合わせる。ノイズで歪めた距離関数は厳密なSDFとは限らず、SDFの距離をそのままレイの安全な進行量に使えるとは限らない。

## P2：fBmで単調な濃淡を避ける

```text
fbm(p) = Σ(i=0..n-1) amplitude_i * noise(frequency_i*p + offset_i)
amplitude_i = gain^i
frequency_i = baseFrequency * lacunarity^i
```

最初の仮設定はn=4〜6、gain=0.5、lacunarity=2程度。0〜1の値が必要なら振幅の合計で正規化する。各noiseが0〜1か-1〜1かを明示する。細かなノイズだけ増やすとザラザラになり、炎や煙の大きな巻き流れは生まれない。

UnityのGradient NoiseとSimple Noiseは異なる種類。Perlin／Simplex／Valueは滑らかな変化、Worley／Voronoiはセル構造に向く。実装例の学習先は[P-S3]。ノイズを使うだけで流体計算やリアルな炎になるわけではない。

## P3：Domain WarpingとCurl Noise

```text
q = p + warpStrength * float3(noiseA(p,t), noiseB(p,t), noiseC(p,t))
densityPattern = fbm(q - upwardVelocity*t)
```

noiseA/B/Cは異なるオフセットを使い、必要なら-1〜1へ変換する。座標の歪みを大きくしすぎると形の方向性が消えるため、炎の上昇・水の飛翔など大きな動きは別に保持する。

3DのCurl Noiseは、滑らかなベクトルポテンシャルA=(Ax,Ay,Az)に対して：

```text
curl(A) = (∂Az/∂y - ∂Ay/∂z,
           ∂Ax/∂z - ∂Az/∂x,
           ∂Ay/∂x - ∂Ax/∂y)
```

偏微分は解析式または中心差分で近似する。2Dならスカラーψから`v=(∂ψ/∂y,-∂ψ/∂x)`も使える。密度模様を歪める処理と粒子を移流する処理は別。巻き流れを作る手法であり、浮力・圧力・衝突・燃焼を含む流体ソルバーの代わりと断言しない。

## P4：Voronoiで泡・霜・亀裂を生成

近傍セルにシード点を生成し、位置pから各点への距離を調べる。最小距離F1と2番目F2の差を境界付近の模様へ利用できるが、F2-F1は一般にセル境界までの厳密な距離ではない。

```text
boundaryApprox = F2 - F1
crack = 1 - smoothstep(crackWidth, crackWidth + feather, boundaryApprox)
```

crackWidthは0以上、featherは正数。正確な幅が必要なら、最近傍点との二等分面から境界距離を求める実装へ進む。Shader GraphのVoronoi出力だけでF2や境界距離が得られると仮定せず、必要な関数をHLSLで作る。

亀裂：細い境界を、局所的な広がりマスクで時間展開。泡：セルごとの大きさと出現を変化。霜：細かな粒状模様と粗さを増やす。単に氷へセル模様を貼るだけでリアルになるわけではなく、透明部と表面反射の差も必要。

## P5：3D密度場とボリュームレイマーチング

厚い煙・毒霧・爆発では、平面を重ねる代わりに空間内の密度ρを計算し、視線に沿って積み重ねる手法を検討する。[P-S2]

```text
rho(p,t) = shapeEnvelope(p,t)
           * max(0, fbm(warp(p,t)) - densityThreshold)
```

shapeEnvelopeで円錐・楕円体・膨張する塊などの全体形状を作る。均一な球にノイズを加えただけでは、毎回同じ丸い雲に見えやすい。発生点・上昇・裂け方も時間変化させる。

吸収と発光を簡略化した前方合成の例：

```text
transmittance = 1
color = 0
for 各ステップ（ボリューム入口から出口まで）:
    rho = density(position, time)
    extinction = max(0, rho * extinctionScale)
    stepAlpha = 1 - exp(-extinction * stepLength)
    color += transmittance * stepAlpha * localSourceColor
    transmittance *= 1 - stepAlpha
alpha = 1 - transmittance
```

これはlocalSourceColorを消散係数に比例する発光・散乱源の色として扱う簡略モデル。吸収と独立した発光係数jを扱う場合は、区間寄与を`j*(1-exp(-extinction*stepLength))/extinction`とし、extinction→0の極限では`j*stepLength`へ切り替える。

出力colorはすでに透過率で重み付けされた値なので、通常α合成でさらにαを掛けない。背景を別に合成するならPremultiplied Alphaとの整合を取る。背景までシェーダーで合成済みなら二重合成を避ける。

立体感を上げるには光源方向の遮蔽や散乱を追加する。発光だけなら煙の陰影は付かない。SDF表面で最初のヒットを探すSphere Tracingと、煙の密度を積分するボリュームレイマーチングは目的が違う。深度による遮蔽、カメラが内部に入る場合、正投影カメラのレイ生成も考慮する。

負荷優先で省略する必要はないが、すべての技に導入する必要もない。視覚上の厚みが必要なレイヤーへ使う。サンプル数・ステップ幅は調整可能にし、ステップ幅を変えても見た目が大きく変わらない減衰式を使う。

## P6：画像なしの法線・立体メッシュ

高さ関数h(x,z,t)で定義した水面なら：

```text
h(x,z,t) = Σ amplitude_i * sin(dot(waveVector_i,(x,z)) - omega_i*t + phase_i)
normal = normalize((-∂h/∂x, 1, -∂h/∂z))
```

上記はyを高さとする平面用。微分は解析式か中心差分で求める。波長・速度・振幅・方向を複数重ねる。飛ぶ水流の曲面では、メッシュの接線空間などへ変換する。表面法線だけ動かす方法と、頂点を変形させてシルエットを変える方法は異なる。

氷柱・岩・破片は、頂点をコード生成して面構成と法線を制御する。氷の面の反射を保つなら必要に応じて頂点を分離してFlat Shadingを行う。冷気や小粒は解析的なマスクで別に作る。既存の外部Normal Mapは使わない。

## 調査からの判断と限界

外部画像なしでも、ノイズ・幾何・材質・時間制御の組み合わせで多くのRPG VFXを構築できる。[P-S1][P-S2][P-S3] ただし「画像をなくすだけで高品質になる」「ノイズを増やせば写真のような炎になる」とは保証できない。シルエット、密度の流れ、光との関係を設計する作業は残る。

まずは2.5Dの生成メッシュ＋手続き的シェーダーで形を作り、厚みが足りない煙・炎のレイヤーにボリューム手法を追加する。この順序は実装の見通しを保つための提案で、スマホ負荷を理由に品質を制限するものではない。
