// 炎や溶岩から立ち上る煙・噴気の粒（Particle System）。画像を使わず、ノイズで崩した塊を計算で描く。
// 現実の煙は、熱い気体と一緒に昇りながら広がって冷え、薄くなって周りの空気に溶ける。火のすぐ上では下から照らされて赤く光る。
// - 暗い本体：粒の中央ほど濃い塊を、2回ゆがめたfBmで崩す。濃い所でも奥が半分ほど透ける。
// - 盛り上がり：濃淡の勾配で、上側（空の光を受ける側）を明るく、くびれを暗くする。
// - ちぎれた切れ端：寿命が進むほど、低い値の所から削れて消える（F5のDissolve。一様に薄くならない）。
// - 中の光：生まれたばかりの粒の下側を、火の色で照らす（加算）。昇るほど冷えて消える。
// Particle Systemは、頂点の追加の値に Stable Random（TEXCOORD0.zw）と、寿命の進み（TEXCOORD1.x）を渡す（Hd2dStageKit.Smoke）。
Shader "Baryonyx/HD2D/Stage Smoke"
{
    Properties
    {
        _Color ("Lit Colour", Color) = (0.42, 0.4, 0.4, 1)
        _ShadeColor ("Shade Colour", Color) = (0.12, 0.11, 0.12, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.5
        [HDR] _GlowColor ("Glow From Below", Color) = (1.6, 0.55, 0.15, 1)
        _Glow ("Glow", Range(0, 4)) = 1
        _GlowFade ("Glow Fade (per life)", Range(0.5, 20)) = 6
        _Detail ("Detail Size", Range(0.5, 6)) = 2.2
        _Erode ("Erode Over Life", Range(0, 1.2)) = 0.85
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
            Name "Smoke"
            Tags { "LightMode"="UniversalForward" }

            // 色は透明度を掛けてから出す（Premultiplied）。中の光は透明度に関係なく足す。
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Hd2dStageAir.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _ShadeColor;
                half _Opacity;
                half4 _GlowColor;
                half _Glow;
                half _GlowFade;
                half _Detail;
                half _Erode;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 uv : TEXCOORD0;
                float4 custom : TEXCOORD1;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                float age : TEXCOORD1;
                half4 color : COLOR;
                half fog : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.age = saturate(input.custom.x);
                output.color = input.color;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = Hd2dTime();
                float age = input.age;
                float seed = (input.uv.z + input.uv.w) * 4.0;

                // 板の縁の円の外、削れ切った所、まだ・もう見えない粒は何も描かない。
                // 濃さは縁の円（outer）の1.3倍を超えないため、ノイズを読む前に0と分かる所を飛ばす（結果は同じ）。
                float outer = 1.0 - smoothstep(0.75, 1.0, length(input.uv.xy * 2.0 - 1.0));
                float threshold = lerp(-0.1, _Erode, age * age);
                if (outer <= 0.0 || outer * 1.3 <= threshold || age <= 0.0 || input.color.a <= 0.0)
                    return half4(0.0, 0.0, 0.0, 0.0);

                // 昇る流れに沿って模様もゆっくり巻く。粒ごとに模様をずらす。
                float3 p = float3(input.uv.xy * _Detail + seed, age * 1.4 + t * 0.12 + seed);
                float3 swirl;
                float n = Hd2dSpread(Hd2dWarpedFbm(p, 0.9, swirl), 1.9);
                // 塊の形：輪郭そのものを渦でゆがめ、丸い玉に見せない。中央ほど濃く、板の縁より内側で0にする。
                float2 d = input.uv.xy * 2.0 - 1.0 + swirl.xy * 0.45;
                float radius = length(d);
                float shape = (1.0 - smoothstep(0.05, 0.9, radius)) * outer;
                float density = shape * (0.15 + 1.15 * n);

                // 寿命が進むほど、薄い所から削れて切れ端になり、消える（F5）。threshold は上で求めた。
                // 濃さは中ほどまでなだらかに残し、削れる縁だけを切る（平らな円盤に見せない）。
                float body = saturate(density * 1.2) * smoothstep(threshold, threshold + 0.25, density);
                // 生まれたてはぼんやり現れる。
                body *= smoothstep(0.0, 0.12, age);

                // 盛り上がり：上側と濃い塊の縁を明るく、くびれと下側を暗くする。
                float top = saturate(0.5 + 0.5 * (input.uv.y - 0.5) * 2.0 + (n - 0.5) * 0.8);
                half3 smoke = lerp(_ShadeColor.rgb, _Color.rgb, top) * input.color.rgb;

                float alpha = body * _Opacity * input.color.a;
                // 中の光：火のすぐ上の若い煙の下側が、火の色に照らされる。濃淡で斑にする。
                // 粒は回さない（板の下が常に下を向く）。
                float below = saturate(1.15 - input.uv.y);
                half3 glow = _GlowColor.rgb * _Glow * exp(-age * _GlowFade) * below * body * (0.4 + 0.9 * n)
                    * input.color.a;

                half3 color = smoke * alpha + glow;
                color += Hd2dDither(input.positionCS.xy) * saturate(alpha * 20.0);
                color = MixFogColor(color, unity_FogColor.rgb * alpha, input.fog);
                return half4(max(color, 0.0), alpha);
            }
            ENDHLSL
        }
    }
}
