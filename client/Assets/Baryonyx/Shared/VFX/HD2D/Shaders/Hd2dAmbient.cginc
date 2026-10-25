// 描いた2D背景の常設演出（霧・光芒・揺らぐ光・火の粉・塵・きらめき）を、画像を使わずに計算で描く共通の関数。
// uGUIの板（Image・RawImage）に貼るシェーダー（Hd2dUiFog・Hd2dUiAmbient）から読み込む。
// 頂点色・RectMask2D・Maskの扱いは UI/Default とそろえる。
#ifndef BARYONYX_HD2D_AMBIENT
#define BARYONYX_HD2D_AMBIENT

#include "UnityCG.cginc"
#include "UnityUI.cginc"

// The clock of the ambient effects in seconds. Hd2dAmbientClock publishes it from C#, so the
// effects stand still outside Play Mode and a capture can show any moment.
float _Hd2dAmbientTime;

fixed4 _Color;
float4 _ClipRect;
float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
int _UIVertexColorAlwaysGammaSpace;

struct AmbientAppData
{
    float4 vertex : POSITION;
    float4 color : COLOR;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct AmbientV2F
{
    float4 vertex : SV_POSITION;
    half4 color : COLOR;
    float2 uv : TEXCOORD0;
    float4 mask : TEXCOORD1;
    float4 screen : TEXCOORD2;
    UNITY_VERTEX_OUTPUT_STEREO
};

AmbientV2F AmbientVert(AmbientAppData v)
{
    AmbientV2F OUT;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
    float4 position = UnityObjectToClipPos(v.vertex);
    OUT.vertex = position;
    OUT.screen = ComputeScreenPos(position);

    float2 pixelSize = position.w;
    pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
    float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
    OUT.mask = float4(
        v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
        0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

    OUT.uv = v.texcoord;
    if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
        v.color.rgb = UIGammaToLinear(v.color.rgb);
    OUT.color = v.color * _Color;
    return OUT;
}

// How much of the pixel a RectMask2D keeps (1 without one).
half AmbientClip(AmbientV2F IN)
{
    #ifdef UNITY_UI_CLIP_RECT
    half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
    return m.x * m.y;
    #else
    return 1;
    #endif
}

// About 1/255 of noise per pixel (interleaved gradient noise), so thin wide glows do not band.
half AmbientDither(float4 screen)
{
    float2 pixel = screen.xy / max(screen.w, 1e-5) * _ScreenParams.xy;
    return (frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715)))) - 0.5) / 255.0;
}

// --- Hashes and noise --------------------------------------------------------------------------

// Hash without sine (Dave Hoskins), 0 to 1.
float AmbientHash(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// Smooth value noise, 0 to 1.
float AmbientNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * f * (f * (f * 6 - 15) + 10);
    float a = AmbientHash(i);
    float b = AmbientHash(i + float2(1, 0));
    float c = AmbientHash(i + float2(0, 1));
    float d = AmbientHash(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// Each octave is turned a little, so the cells of the value noise never line up into a grid.
static const float2x2 AmbientOctave = float2x2(1.6, -1.2, 1.2, 1.6);

// Two octaves of noise (fBm), 0 to 1. Cheap: used to warp where the finer noise is read.
float AmbientFbm2(float2 p)
{
    float sum = AmbientNoise(p) * 0.667;
    p = mul(AmbientOctave, p) + float2(3.1, 7.7);
    sum += AmbientNoise(p) * 0.333;
    return sum;
}

// Three octaves of noise (fBm), 0 to 1.
float AmbientFbm3(float2 p)
{
    float sum = AmbientNoise(p) * 0.571;
    p = mul(AmbientOctave, p) + float2(3.1, 7.7);
    sum += AmbientNoise(p) * 0.286;
    p = mul(AmbientOctave, p) + float2(-5.3, 1.9);
    sum += AmbientNoise(p) * 0.143;
    return sum;
}

// Size of the board in screen pixels along its own axes (also when the board is rotated).
float2 AmbientBoardPixels(float2 uv)
{
    float2 du = float2(ddx(uv.x), ddy(uv.x));
    float2 dv = float2(ddx(uv.y), ddy(uv.y));
    return 1 / max(float2(length(du), length(dv)), 1e-6);
}

#endif
