// 3Dの舞台（HD-2D）の空気の部品（光芒・光だまり・光の粒・炎・煙・霧）が共有する、画像を使わない計算の部品。
// - Hd2dHash：格子点ごとの乱数（PCG3D）。整数で計算するため、座標が大きくなっても模様が崩れない。
// - Hd2dNoise：値ノイズ（0〜1）。3次元目に時刻を入れると、模様が流れるだけでなく形も変わり続ける。
// - Hd2dFbm：大きなうねりから細かな揺らぎまでを重ねる（gain 0.5、lacunarity 約2）。段ごとに座標を回し、格子の向きを見せない。
// - Hd2dWarpedFbm：読む座標そのものを別のfBmで2回ゆがめてから読む（Domain Warping）。霧・煙の縁と暗い筋を作る。
// - Hd2dSpread：fBmは段を重ねるほど0.5に寄るため、0〜1へ広げ直す。
// - Hd2dDither：画素ごとの1/255前後の揺らぎ。薄く広い光や霧の階調の段差（バンディング）を散らす。
// - Hd2dTime：時刻（秒）。撮影のときは _Hd2dStageClock をずらして、同じ時刻のコマを何度でも描く。
#ifndef BARYONYX_HD2D_STAGE_AIR
#define BARYONYX_HD2D_STAGE_AIR

// 撮影用のずらし（秒）。ゲームでは0のまま。
float _Hd2dStageClock;

float Hd2dTime()
{
    // 長く起動していても乱数の座標の精度を保つよう、1時間で巻き戻す。
    return fmod(_Time.y, 3600.0) + _Hd2dStageClock;
}

float Hd2dHash(float3 cell)
{
    uint3 v = (uint3)(int3)cell * 1664525u + 1013904223u;
    v.x += v.y * v.z;
    v.y += v.z * v.x;
    v.z += v.x * v.y;
    v ^= v >> 16u;
    v.x += v.y * v.z;
    v.y += v.z * v.x;
    v.z += v.x * v.y;
    return (v.x ^ v.y ^ v.z) * (1.0 / 4294967295.0);
}

float Hd2dHash(float2 cell)
{
    return Hd2dHash(float3(cell, 0.0));
}

float Hd2dNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = p - i;
    float3 u = f * f * (3.0 - 2.0 * f);
    float n000 = Hd2dHash(i);
    float n100 = Hd2dHash(i + float3(1, 0, 0));
    float n010 = Hd2dHash(i + float3(0, 1, 0));
    float n110 = Hd2dHash(i + float3(1, 1, 0));
    float n001 = Hd2dHash(i + float3(0, 0, 1));
    float n101 = Hd2dHash(i + float3(1, 0, 1));
    float n011 = Hd2dHash(i + float3(0, 1, 1));
    float n111 = Hd2dHash(i + float3(1, 1, 1));
    return lerp(
        lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y),
        lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y),
        u.z);
}

float Hd2dNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = p - i;
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = Hd2dHash(i);
    float b = Hd2dHash(i + float2(1, 0));
    float c = Hd2dHash(i + float2(0, 1));
    float d = Hd2dHash(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// 0〜1の値。octaves は呼ぶ側の定数（コンパイル時に展開される）。
float Hd2dFbm(float3 p, int octaves)
{
    const float3x3 turn = float3x3(0.00, 0.80, 0.60, -0.80, 0.36, -0.48, -0.60, -0.48, 0.64);
    float sum = 0.0;
    float amplitude = 0.5;
    float total = 0.0;
    for (int i = 0; i < octaves; i++)
    {
        sum += amplitude * Hd2dNoise(p);
        total += amplitude;
        p = mul(turn, p) * 2.02 + float3(7.3, 1.9, 4.1);
        amplitude *= 0.5;
    }
    return sum / total;
}

// 座標を2回ゆがめてから5段のfBmを読む。warp は座標の単位でのずれの大きさ。
// 1回目は大きく緩いうねり、2回目はそのうねりに沿った細かな巻き込みになる。
// x・y・zのずれは、それぞれ別のずらし量のノイズから作る（同じノイズだとずれが斜めの1方向にそろう）。
float Hd2dWarpedFbm(float3 p, float warp, out float3 swirl)
{
    float3 q = float3(
        Hd2dFbm(p + float3(0.0, 0.0, 0.0), 2),
        Hd2dFbm(p + float3(5.2, 1.3, 2.8), 2),
        Hd2dFbm(p + float3(1.7, 9.2, 6.1), 2)) * 2.0 - 1.0;
    float3 r = float3(
        Hd2dFbm(p + warp * q + float3(1.7, 9.2, 3.4), 3),
        Hd2dFbm(p + warp * q + float3(8.3, 2.8, 7.7), 3),
        Hd2dFbm(p + warp * q + float3(4.6, 6.5, 0.9), 3)) * 2.0 - 1.0;
    swirl = r;
    return Hd2dFbm(p + warp * r, 5);
}

float Hd2dWarpedFbm(float3 p, float warp)
{
    float3 swirl;
    return Hd2dWarpedFbm(p, warp, swirl);
}

// fBmの値（0.5のまわりに寄る）を、contrast倍に広げて0〜1に収める。
float Hd2dSpread(float n, float contrast)
{
    return saturate((n - 0.5) * contrast + 0.5);
}

// -0.5〜0.5 を255で割った揺らぎ（Interleaved Gradient Noise）。画素の位置から決まり、時刻でずらす。
float Hd2dDither(float2 pixel)
{
    pixel += frac(Hd2dTime() * 7.13) * float2(47.0, 17.0);
    return (frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715)))) - 0.5) / 255.0;
}

#endif
