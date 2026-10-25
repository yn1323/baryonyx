# 共通の計算式

章番号は元のガイド「RPG戦闘VFX：専門用語・計算式・Claudeへの指示ガイド」（2026-10-26）の番号である。第1章・第7章・第8章は [SKILL.md](../SKILL.md)、第2〜3章は [glossary.md](glossary.md)、第4章（F1〜F12）は [formulas.md](formulas.md)、第5章は [recipes.md](recipes.md)、第6章は [prompts.md](prompts.md)、第9章は [sources.md](sources.md)、第10章（P1〜P6）は [procedural.md](procedural.md)、第11章は [lessons.md](lessons.md) にある。[S1] などの記号は [sources.md](sources.md) の資料を指す。
式を当てはめたときのつまずきは [計算で描くときのつまずき](lessons.md#計算で描くときのつまずき) にある。

## 記法

以下はHLSL風の式・擬似コード。コピーするだけで動く完成シェーダーではない。`noise`、`fbm`、`SampleSceneColor`は説明用関数名で、実際のノイズ関数やテクスチャ取得処理の実装が必要。

- `t`：秒、`T`：寿命（正数）、`u = saturate(t / T)`：寿命を0〜1に正規化。
- `saturate(x)`：0〜1に制限。`lerp(a,b,u)`：補間。`smoothstep(a,b,x)`：aからbの間を滑らかに0〜1へ変換。必ずa < bで使う。
- 距離の単位やUVのスケールを統一する。色の合成は原則リニア空間で扱う。

## F1：合成方式

```text
通常のα合成：     Cout = Cs * a + Cd * (1 - a)
αを使う加算合成： Cout = Cs * a + Cd
純粋な加算合成：  Cout = Cs + Cd
乗算合成：        Cout = Cs * Cd
```

`Cs`はエフェクトのRGB、`Cd`は背景RGB。対応するShaderLabの例：

```text
Blend SrcAlpha OneMinusSrcAlpha   // 通常α
Blend SrcAlpha One               // α付き加算
Blend One One                    // 加算。消すならシェーダー側のRGBを減衰
Blend One OneMinusSrcAlpha       // RGBを事前にα倍したPremultiplied Alpha
Blend DstColor Zero              // 乗算
```

重要：`Blend One One`ではαを下げるだけで消えるとは限らない。RGBへの減衰が必要。Premultiplied Alphaもαの二重乗算を避ける。黒は加算ではほぼ寄与せず、加算だけで背景を暗くすることはできない。[S1]

## F2：外周の柔らかさとコア／ハロー

```hlsl
float r = length((uv - center) * aspectCorrection);
float softMask = 1 - smoothstep(innerRadius, outerRadius, r);
float core = exp(-r*r / (2 * sigmaCore*sigmaCore));
float halo = exp(-r*r / (2 * sigmaHalo*sigmaHalo));
float3 emission = coreColor * core * coreIntensity
                + haloColor * halo * haloIntensity;
```

`sigmaHalo > sigmaCore > 0`、ハローの強度は通常コアより低くする。画像全体をぼかすのではなく、硬い主形状と柔らかい光を独立させる。式は円形の例で、斬撃なら線・弧からの距離に置き換える。

## F3：立ち上がりと消散

```hlsl
float envelope = smoothstep(0, attackEnd, u)
               * (1 - smoothstep(fadeStart, 1, u));
```

`0 < attackEnd < fadeStart < 1`。発光RGBやαにこの値を適用。瞬間的な技はattackEndを小さく、回復は大きめにする。着弾フラッシュ、煙、破片に同じ寿命を使わない。

## F4：大小のノイズと模様の歪み

```hlsl
float n = 0.65 * noise(uv * lowFrequency + velocity1*t)
        + 0.25 * noise(uv * midFrequency + velocity2*t)
        + 0.10 * noise(uv * highFrequency + velocity3*t);
float2 flowNoise = float2(noise(p1), noise(p2)); // 異なる座標から取得
float2 warpedUV = uv + (flowNoise * 2 - 1) * distortionAmount;
```

大きな流れと細かな模様を別の周波数・速度で重ねる。単なる白色ノイズの点滅とは異なる。粒子位置に対する乱流と、模様のUVの歪みも別々に調整する。UnityのParticle SystemにはNoiseモジュールがある。[S2]

## F5：侵食して消えるDissolve

```hlsl
float threshold = lerp(-edgeWidth, 1 + edgeWidth, u);
float mask = smoothstep(threshold, threshold + edgeWidth, noiseValue);
float boundary = mask * (1 - mask) * 4;
```

`edgeWidth > 0`、noiseValueは0〜1。進行に合わせて低い値の部分から消える。boundaryを必要なときだけ発光へ足す。消散する霧まで必ず発光させる必要はない。

## F6：移動と飛翔

```text
等速：      p(t) = p0 + v*t
放物運動：  p(t) = p0 + v0*t + 0.5*g*t²
補間飛翔：  p(u) = (1-u)²*p0 + 2*(1-u)*u*p1 + u²*p2
```

最後は二次ベジェ曲線で、見た目の曲線指定。物理的な等速運動ではなく、uを等速で進めても軌道上の速さは一定にならない。弓の弾道と誘導弾の演出を区別する。

## F7：軌跡の先細りと減衰

```hlsl
// s = 0 が古い尾、s = 1 が新しい先端
float width = maxWidth * pow(s, taperPower);
float alpha = pow(s, fadePower) * envelope;
```

指数は正数。剣では刃の根元と先端を時系列で記録したリボンメッシュも候補。TrailRendererを剣先1点へ付けただけの細い線で不足するなら、刃幅を持つ専用メッシュを使う。[S3]

## F8：螺旋・渦

```text
x(t) = cx + r(t)*cos(ω*t + phase)
y(t) = cy + riseSpeed*t
z(t) = cz + r(t)*sin(ω*t + phase)
```

上昇速度・半径・位相を少しずつ変える。吸引なら半径r(t)を縮小。2Dの画面に使う場合は、ゲーム平面や奥行きの扱いに合わせて軸を置き換える。

## F9：波紋・衝撃波

```hlsl
float R = initialRadius + expansionSpeed*t;
float ring = 1 - smoothstep(halfWidth, halfWidth + softness, abs(r - R));
float amplitude = exp(-decay*t);
```

幅・softness・decayは正数。水の波紋なら弱く繰り返し、衝撃波なら短く急拡大。画面全体の歪みと地面のリングは別レイヤーにする。

## F10：フレネルと背景の歪み

```hlsl
float fresnel = pow(1 - saturate(dot(normalize(N), normalize(V))), power);
float2 refractedUV = screenUV + normalOffsetXY * refractionStrength;
float3 background = SampleSceneColor(refractedUV);
```

Nは面法線、Vは表面からカメラへの方向、powerは正数。後半は画面空間の屈折風近似であり、Snellの法則による厚み付き光線追跡ではない。歪みの強さを上げすぎると、画面端や手前の物体の取り込みが目立つ。[S4][S5]

URPのOpaque Textureは基本的に透明描画前の画像。SpriteRendererなど透明物が自動的に含まれる前提にしない。2D RendererではCamera Sorting Layer Texture等、現在のレンダラーに適した取得経路を検討する。HDRPのRefraction設定をURPへそのまま移植しない。

## F11：明滅・バースト

```hlsl
float pulse = baseIntensity + amplitude * (0.5 + 0.5*sin(omega*t + phase));
float burst = exp(-falloff * max(t - triggerTime, 0)); // triggerTime以降のみ適用
```

回復は滑らかなpulse、雷は不規則なtriggerTimeの短いburstを使用。毎フレーム完全に別の乱数へ変えるとチラつくだけになりやすい。枝の形は短い区間保持し、更新時に切り替える。

## F12：氷・水の厚みによる色の減衰

```hlsl
float3 transmittance = exp(-absorptionCoefficient * thickness);
float3 transmittedColor = background * transmittance;
```

Beer–Lambert則を使う簡略モデル。厚い部分ほど透過光が減る。thicknessを単なる画面深度差で代用する場合、実際の物体内部の厚みとは一致しない。反射・表面ハイライトは別に扱う。
