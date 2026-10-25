// TempLabの試作のエフェクトの光・筋・地面の跡。画像を使わず、板の中の位置から距離とノイズで形を描く。
// 板はC#（TempLabBoard）が毎フレームまとめて1つのメッシュに作り、頂点の値で形と進み具合を渡す。
// - 頂点色：rgb 光の色（1を超えてBloomでにじむ）、a 全体の濃さ。
// - UV0：xy 板の中の位置（-1〜1。筋の形は x が尾0〜頭1、y が横-1〜1）、z 形の番号、w 乱数の種。
// - UV1：形ごとの値（下の各形を参照）。
// 形：0 芯とにじみ、1 火の粉の筋、2 地面を走る衝撃の輪、3 焦げ跡と熾火、4 帯（斬撃・稲妻の芯とにじみ）、
//     5 ちぎれた細い筋（ウィスプ）、6 刃の光る星。
// 合成は乗算済みアルファ（One OneMinusSrcAlpha）。光の形はアルファ0で色を足すだけにし、
// 焦げ跡だけがアルファで地面を暗くする（加算では暗くできないため）。
Shader "Baryonyx/TempLab/Glow"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 8)) = 1
        // 光と筋はキャラの板の手前に描く（Always）。地面の跡は舞台の物に隠す（LessEqual）。
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
        }

        Pass
        {
            Name "Glow"
            Tags { "LightMode"="UniversalForward" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TempLabVfx.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                return output;
            }

            // 0 芯とにじみ：UV1 x 芯の幅、y にじみの幅、z 芯の強さ、w にじみの強さ（F2）。
            half4 CoreAndHalo(float2 p, float4 v)
            {
                float rr = dot(p, p);
                float core = exp(-rr / (2 * v.x * v.x));
                float halo = exp(-rr / (2 * v.y * v.y));
                return half4((core * v.z + halo * v.w).xxx, 0);
            }

            // 1 火の粉：UV1 y 熱さ（0〜1。白い芯の強さ）、z 頭の半径（板の長さに対する割合。TempLabBoard が渡す）。
            // 丸く光る頭に、細く短い尾が付く（彗星の形）。頭から尾まで幅を直線で細らせると三角のくさびに見える。
            half4 Ember(float2 p, float4 v)
            {
                float s = saturate(p.x);
                float ratio = clamp(v.z, 0.02, 0.5);
                float headAt = 1 - ratio;
                // 頭の中心からの距離を、横幅の半分を1とした単位で測る（縦横の比をそろえて丸くする）。
                float2 d = float2((s - headAt) / ratio, p.y);
                float rr = dot(d, d);
                float head = exp(-rr * 2.6);
                float tailWidth = lerp(0.12, 0.38, saturate(s / headAt));
                float tail = exp(-(p.y * p.y) / (tailWidth * tailWidth))
                    * pow(saturate(s / headAt), 1.6) * 0.55 * step(s, headAt);
                half3 white = half3(1.0, 0.92, 0.75) * exp(-rr * 9) * v.y;
                return half4((head + tail).xxx + white, 0);
            }

            // 2 衝撃の輪：UV1 x 輪の半径（板の半分を1）、y 輪の幅、z 途切れ方、w 時刻（F9）。
            // 輪は角度で途切れさせ、きれいな円の線にしない。内側には薄い照り返しを残す。
            half4 Shock(float2 p, float4 v, float seed)
            {
                float r = length(p);
                float a = atan2(p.y, p.x);
                float d = (r - v.x) / max(v.y, 1e-3);
                float ring = exp(-d * d);
                float3 around = float3(cos(a) * 2.2, sin(a) * 2.2, seed * 9.1 + v.w * 1.5);
                float breakup = smoothstep(0.3, 0.7, Spread(Fbm5(around + r * 1.7), 2.2));
                float inner = exp(-r * r / max(v.x * v.x, 1e-3) * 1.6) * 0.18;
                float edge = 1 - smoothstep(0.88, 1.0, r);
                return half4(((ring * lerp(1 - v.z, 1, breakup) + inner) * edge).xxx, 0);
            }

            // 3 焦げ跡：UV1 x 熾火の熱さ（0〜1）、y 煤の濃さ（0〜1）、z 時刻、w 未使用。
            // 縁をノイズで崩した黒い焦げの中に、ちらちらと冷えていく熾火の点を残す。
            half4 Scorch(float2 p, float4 v, float seed)
            {
                float r = length(p);
                float3 q = float3(p * 2.4, seed * 13.7);
                float ragged = r + (Spread(Fbm5(q), 2.2) - 0.5) * 0.55;
                float mark = 1 - smoothstep(0.35, 0.95, ragged);
                float soot = mark * v.y * 0.72;
                float cells = Spread(Fbm5(float3(p * 6.5, seed * 3.1 + v.z * 0.6)), 2.2);
                float embers = smoothstep(0.62, 0.85, cells) * (1 - smoothstep(0.1, 0.75, ragged));
                half3 glow = FireColor(0.3 + 0.45 * v.x) * embers * v.x;
                return half4(glow, soot);
            }

            // 4 帯：UV1 x 芯の幅（横の半分を1とした割合）、y にじみの強さ、z 削れ（0で削れない、1で消える）、
            // w 模様の流れ。芯は細く鋭く、にじみは帯いっぱいに柔らかく広げる（F2）。削れは尾から進み（F5）、
            // ノイズの低い所から欠けるので、帯全体が一様に薄くならない。
            half4 Band(float2 p, float4 v, float seed)
            {
                float along = saturate(p.x);
                float across = abs(p.y);
                float core = exp(-(across * across) / max(v.x * v.x, 1e-4));
                float halo = exp(-across * across * 2.4) * v.y;
                // 進む向きに流れる細かな明暗で、のっぺりした棒にしない。流れ w が0の帯には付けない
                // （稲妻に付けると、段の並んだ梯子のように見える）。
                float grain = abs(v.w) > 1e-4
                    ? 0.78 + 0.22 * Noise(float3(along * 26 - v.w * 6, p.y * 2.0, seed * 31))
                    : 1.0;
                float keep = 1;
                if (v.z > 0)
                {
                    float n = Spread(Fbm5(float3(along * 6.5 - v.w * 2.5, p.y * 1.4, seed * 11)), 2.2);
                    float threshold = lerp(-0.2, 1.15, v.z);
                    keep = smoothstep(threshold, threshold + 0.16, n * 0.55 + along * 0.45);
                }
                return half4(((core + halo) * grain * keep).xxx, 0);
            }

            // 5 ちぎれた細い筋：UV1 x 太さ、y 未使用、z 削れ（0〜1）、w 模様の流れ。
            // 筋になったノイズだけを残し、太さのそろわない糸のように見せる（Wisps）。
            half4 Wisp(float2 p, float4 v, float seed)
            {
                float along = saturate(p.x);
                float n = Spread(Fbm5(float3(along * 4.2 - v.w * 2.5, p.y * 2.3, seed * 7)), 2.4);
                float strands = smoothstep(0.5, 0.82, n);
                float body = exp(-p.y * p.y / max(v.x * v.x, 1e-3));
                float threshold = lerp(-0.2, 1.15, v.z);
                float keep = smoothstep(threshold, threshold + 0.2, n * 0.6 + along * 0.4);
                return half4((strands * body * keep * lerp(0.35, 1.0, along)).xxx, 0);
            }

            // 6 刃の光る星：UV1 x 芯の強さ、y 光条の強さ、z 光条の細さ、w 未使用。縦横の細い光条と小さな芯。
            half4 Glint(float2 p, float4 v)
            {
                float rr = dot(p, p);
                float thin = max(v.z, 1);
                float rays = exp(-abs(p.y) * thin) * exp(-abs(p.x) * 3.0)
                    + exp(-abs(p.x) * thin) * exp(-abs(p.y) * 3.0);
                float core = exp(-rr * 60);
                return half4((core * v.x + rays * v.y).xxx, 0);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv0.xy;
                int shape = (int)round(input.uv0.z);
                float seed = input.uv0.w;
                half4 shaped;
                if (shape == 0)
                    shaped = CoreAndHalo(p, input.uv1);
                else if (shape == 1)
                    shaped = Ember(p, input.uv1);
                else if (shape == 2)
                    shaped = Shock(p, input.uv1, seed);
                else if (shape == 3)
                    shaped = Scorch(p, input.uv1, seed);
                else if (shape == 4)
                    shaped = Band(p, input.uv1, seed);
                else if (shape == 5)
                    shaped = Wisp(p, input.uv1, seed);
                else
                    shaped = Glint(p, input.uv1);

                // 板の外周では必ず0にし、四角い縁を見せない。筋の形は x が0〜1なので-1〜1に直して測る。
                bool lengthwise = shape == 1 || shape == 4 || shape == 5;
                float2 box = abs(float2(lengthwise ? p.x * 2 - 1 : p.x, p.y));
                float frame = (1 - smoothstep(lengthwise ? 0.97 : 0.9, 1.0, box.x))
                    * (1 - smoothstep(0.9, 1.0, box.y));
                half3 rgb = shaped.rgb * input.color.rgb * _Intensity;
                return half4(rgb, shaped.a) * input.color.a * frame;
            }
            ENDHLSL
        }
    }
}
