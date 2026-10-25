// TempLabの試作のエフェクトの式（TempLabVolume.shader・TempLabGlow.shader から読み込む）。
// ハッシュと値ノイズは戦闘のエフェクトと同じものを使い（BattleVfxShapes.cginc の Hash・Noise）、ここには
// 重ね方と色だけを置く。画像は使わない。
// - Fbm5：大きなうねりから細かな揺らぎまで5段。段ごとに座標を回し、格子の向きが見えないようにする。
// - WarpedFbm：座標そのものを別のノイズでゆがめてから読む（Domain Warping）。炎の舌のうねりと裂け目を作る。
// - Spread：fBmは段を重ねるほど値が0.5に寄り、縁の揺れや削れ方が弱くなる。0〜1へ広げ直して使う。
// - FireColor：温度（0〜1）を、暗い赤→橙→黄→白の発光色（HDR）にする。黒体の色の並びを近似した手調整の値。
#ifndef BARYONYX_TEMPLAB_VFX
#define BARYONYX_TEMPLAB_VFX

#include "../../Combat/Vfx/Shaders/BattleVfxShapes.cginc"

// 0〜1の値。段ごとに回して周波数を約2倍、振幅を半分にする（gain 0.5、lacunarity 約2）。
float Fbm5(float3 p)
{
    const float3x3 turn = float3x3(0.00, 0.80, 0.60, -0.80, 0.36, -0.48, -0.60, -0.48, 0.64);
    float sum = 0;
    float amplitude = 0.5;
    float total = 0;
    [unroll]
    for (int i = 0; i < 5; i++)
    {
        sum += amplitude * Noise(p);
        total += amplitude;
        p = mul(turn, p) * 2.02 + 7.3;
        amplitude *= 0.5;
    }
    return sum / total;
}

// 座標を別の3つのノイズ（-1〜1）でゆがめてから Fbm5 を読む。warp は座標の単位でのずれの大きさ。
float WarpedFbm(float3 p, float warp)
{
    float3 offset = float3(
        Noise(p * 0.55 + float3(3.1, 0.0, 5.7)),
        Noise(p * 0.55 + float3(11.9, 4.2, 1.3)),
        Noise(p * 0.55 + float3(7.4, 9.8, 13.6))) * 2 - 1;
    return Fbm5(p + offset * warp);
}

// fBmの値（0.5のまわりに寄る）を、contrast倍に広げて0〜1に収める。
float Spread(float n, float contrast)
{
    return saturate((n - 0.5) * contrast + 0.5);
}

// 温度0は光らない。0.25で暗い赤、0.5で橙、0.75で黄、1で白に近い。値は1を超えてBloomでにじむ。
half3 FireColor(float temperature)
{
    float t = saturate(temperature);
    half3 c = lerp(half3(0, 0, 0), half3(0.55, 0.05, 0.01), smoothstep(0.05, 0.3, t));
    c = lerp(c, half3(2.6, 0.75, 0.12), smoothstep(0.25, 0.55, t));
    c = lerp(c, half3(4.2, 2.6, 0.7), smoothstep(0.5, 0.8, t));
    c = lerp(c, half3(7.0, 6.2, 4.6), smoothstep(0.78, 1.0, t));
    return c;
}

#endif
