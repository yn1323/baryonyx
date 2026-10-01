// 衝撃波の輪を、画像を使わずに計算で描くuGUI用シェーダー（VfxShockwave.pngとの比較用の試作）。
// 四角の中心からの距離と向きで、次を重ねる。
// - 輪の本体：先頭に沿う柔らかい光と、内側の薄い尾。外側の縁は鋭く、広がるほど細くなる。
// - 先頭の芯：外縁の細い線。場所ごとに明るさを変え、1を超える明るさでHDRのBloomににじませる。
// - すじ：輪に沿って走る細い線の濃淡。半径方向に細かく、向きに沿ってゆっくり変える。
// - 途切れ：後半から、向きに沿ったノイズで弧に分かれて消える。一様には薄くしない。
// - 粒：輪の前後に散らした小さな光。広がるにつれて外へ流れ、またたいて消える。
// 頂点色のアルファを「残り」（1で出たばかり、0で消えた）として使う。BattleSkillVfxのRingが
// 画像の輪に与えるアルファ（1 - 経過）と同じ値を受け取れば、そのまま置き換えられる。
// ノイズも計算で作るため、テクスチャを1枚も読まない。
Shader "Baryonyx/Combat/Battle Vfx Ring"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1.3
        _Radius ("Front Radius", Range(0.3, 0.95)) = 0.74
        _Width ("Trail Width", Range(0.02, 0.5)) = 0.1
        _Edge ("Front Softness", Range(0.002, 0.1)) = 0.016
        _Core ("Core Brightness", Float) = 1.4
        _CoreWidth ("Core Width", Range(0.002, 0.05)) = 0.01
        _Streaks ("Streak Amount", Range(0, 1)) = 0.7
        _Breakup ("Breakup", Range(0, 1)) = 0.75
        _Sparks ("Spark Amount", Range(0, 1)) = 0.8
        _Wobble ("Front Wobble", Range(0, 0.2)) = 0.05

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        // Additive, keeping the destination alpha, as the other effect materials do.
        Blend SrcAlpha One, Zero One
        ColorMask [_ColorMask]

        Pass
        {
            Name "BattleVfxRing"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // fwidth keeps the thin lines at least a pixel wide while the ring is still small.
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                half4 color     : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 mask     : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half4 _Color;
            half _Intensity;
            float _Radius;
            float _Width;
            float _Edge;
            half _Core;
            float _CoreWidth;
            half _Streaks;
            half _Breakup;
            half _Sparks;
            float _Wobble;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

            static const float Tau = 6.2831853;

            // Sparks are scattered one to a slice of the ring, this many slices around it.
            static const float SparkSlices = 64;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.vertex = vPosition;

                float2 pixelSize = vPosition.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                OUT.texcoord = v.texcoord.xy;
                OUT.mask = float4(
                    v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                    v.color.rgb = UIGammaToLinear(v.color.rgb);
                OUT.color = v.color;
                return OUT;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            // Smooth value noise (0 to 1). Sampled with the direction around the ring (cos, sin)
            // as two of its axes, so it joins up all the way round with no seam.
            float Noise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(
                    lerp(
                        lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
                        lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x),
                        f.y),
                    lerp(
                        lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                        lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x),
                        f.y),
                    f.z);
            }

            half4 frag(v2f IN) : SV_Target
            {
                float2 p = IN.texcoord * 2 - 1;
                float r = length(p);
                float2 dir = p / max(r, 1e-4);
                float aa = fwidth(r);

                // The vertex alpha is what is left: 1 just out, 0 gone.
                half life = IN.color.a;
                half t = 1 - life;

                // The front is never a perfect circle: it bulges a little here and there.
                float front = _Radius + (Noise(float3(dir * 1.7, 3.1 + t * 1.5)) - 0.5) * _Wobble;
                float d = r - front;

                // A soft halo hugging the front, a little wider inside, and a faint trail inside
                // it; both thin as the ring spreads. Sharp outside, so it reads as a wave front.
                float thin = lerp(1, 0.45, t);
                float outer = 1 - smoothstep(0, max(_Edge, aa), d - _Edge);
                float haloWidth = (d < 0 ? 0.04 : 0.025) * thin;
                float halo = exp(-(d * d) / (haloWidth * haloWidth)) * 0.45;
                float trail = exp(min(d, 0) / (_Width * thin)) * 0.15;

                // Streaks running along the ring: fine across it, slow around it.
                float streak = Noise(float3(dir * 3, r * 38 + t * 3));
                float band = outer * (halo + trail)
                    * lerp(1, 0.1 + 1.8 * smoothstep(0.35, 0.85, streak), _Streaks);

                // The hot line at the front, at least a pixel wide, hotter in some places than
                // others so it does not read as an even drawn outline.
                float coreWidth = max(_CoreWidth * thin, aa * 1.2);
                float hot = 0.45 + 0.8 * Noise(float3(dir * 3.3, 5.9 + t * 2));
                float core = exp(-(d * d) / (coreWidth * coreWidth)) * _Core * hot;

                // From halfway it breaks into arcs: a pattern around the ring, leaning a little
                // across it so the gaps are not cut straight, eaten from its low parts with
                // soft ends as the threshold rises.
                float arcs = Noise(float3(dir * 2, 7.7 + r * 4)) * 0.7
                    + Noise(float3(dir * 4.5, 1.3 + r * 8)) * 0.3;
                float cut = saturate((t - 0.3) / 0.7) * _Breakup;
                half keep = smoothstep(cut - 0.12, cut + 0.12, arcs);

                // Sparks: one to a slice of the ring, some slices empty, drifting outward.
                float around = atan2(p.y, p.x) / Tau + 0.5;
                float slice = floor(around * SparkSlices);
                float h1 = Hash(float3(slice, 1.3, 0.7));
                float h2 = Hash(float3(slice, 7.1, 2.9));
                float h3 = Hash(float3(slice, 4.2, 5.3));
                float sparkAround = (slice + 0.35 + 0.3 * h2) / SparkSlices;
                // Some ride ahead of the front, most are left behind it; all drift outward.
                float sparkR = front + (h3 - 0.65) * 0.2 + t * (0.04 + 0.12 * h2);
                float da = (around - sparkAround) * Tau * r;
                float dr = r - sparkR;
                float sparkSize = max(0.005 + 0.005 * h3, aa);
                float twinkle = 0.6 + 0.4 * sin(t * 24 + h1 * 20);
                float spark = step(1 - _Sparks * 0.8, h1)
                    * exp(-(da * da + dr * dr) / (sparkSize * sparkSize))
                    * twinkle * saturate(1.2 - t * 1.2);

                half3 tint = IN.color.rgb * _Color.rgb;
                half3 light = tint * band * keep
                    + lerp(tint, 1, 0.5) * core * keep
                    + lerp(tint, 1, 0.6) * spark * 2.5;
                // The whole ring dims as it goes, a little slower than the picture's even fade,
                // since the breaking up takes away the rest.
                half fade = saturate(life * 1.25);
                half4 color = half4(light * _Intensity, fade * _Color.a);

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                color.a *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
