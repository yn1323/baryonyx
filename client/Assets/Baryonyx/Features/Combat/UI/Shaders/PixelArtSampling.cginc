// ドット絵を、画素の端数の位置でも滑らかに動かす標本化。
// ドットの内側は最も近いテクセルの色（Pointと同じくっきりした絵）のままにし、ドットの境目にかかる1画素だけを、
// 境目が画素のどこを通るかに応じて両隣の色で混ぜる（画素の面積で平均した色になる）。
// Pointで描くと絵は画素単位で跳んで動き、拡大率が整数でない背景ではドットの幅の不ぞろいが移ってちらつく。
// 戦場はカメラの中心の円運動で常に端数の位置にあるため、戦場のドット絵はこれで描く。
#ifndef BARYONYX_PIXEL_ART_SAMPLING_INCLUDED
#define BARYONYX_PIXEL_ART_SAMPLING_INCLUDED

// texelSize は Unity が渡す <テクスチャ名>_TexelSize（x, y: 1テクセルのUV、z, w: 幅と高さ）。
// テクスチャ自体はPointのまま読み、混ぜる計算をここで行う（Pointの絵に別の補間の設定を使えない端末でも同じ結果になる）。
half4 SamplePixelArt(sampler2D tex, float2 uv, float4 texelSize)
{
    float2 texel = uv * texelSize.zw;
    // 1画素にかかるテクセルの数。拡大表示では1未満になる。
    float2 perPixel = max(fwidth(texel), 1e-5);
    // 最も近いドットの境目と、そこから見たこの画素の中心の位置（画素単位、-0.5〜0.5の外は混ぜない）。
    float2 seam = floor(texel + 0.5);
    float2 after = saturate((texel - seam) / perPixel + 0.5);

    float2 uvBefore = (seam - 0.5) * texelSize.xy;
    float2 uvAfter = (seam + 0.5) * texelSize.xy;
    half4 a = tex2Dlod(tex, float4(uvBefore.x, uvBefore.y, 0, 0));
    half4 b = tex2Dlod(tex, float4(uvAfter.x, uvBefore.y, 0, 0));
    half4 c = tex2Dlod(tex, float4(uvBefore.x, uvAfter.y, 0, 0));
    half4 d = tex2Dlod(tex, float4(uvAfter.x, uvAfter.y, 0, 0));
    // 透明なテクセルの色が縁に混ざらないよう、不透明度を掛けてから混ぜて戻す。
    a.rgb *= a.a;
    b.rgb *= b.a;
    c.rgb *= c.a;
    d.rgb *= d.a;
    half4 color = lerp(lerp(a, b, after.x), lerp(c, d, after.x), after.y);
    color.rgb /= max(color.a, 1e-4);
    return color;
}

#endif
