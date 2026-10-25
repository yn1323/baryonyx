// 光芒が地面に落ちた所、ランタンや燭台の足元、溶岩・水晶・鬼火のまわりに敷く光だまり。地面に寝かせた板に、画像を使わず計算で描き、加算で足す。
// 現実の光だまりは、光源の形と、光の通り道で光を遮る物（葉、雲、塵、炎の揺れ）の影で、縁が不規則に崩れ、明るさがゆっくり揺れる。
// - にじみ：広く薄い光。縁はノイズでゆがめ、まん丸の円に見せない。
// - 芯：中ほどの明るい所。にじみとは別の幅と強さにする（F2）。
// - まだら：地面の位置で決まる、2回ゆがめたfBmの明暗が、光の通り道の揺れとしてゆっくり流れる。
// 板ごとの強さは頂点色の透明度（Hd2dStageKit.Decals）。薄く広い光の階調の段差は、画素ごとのごく弱い揺らぎで散らす。
Shader "Baryonyx/HD2D/Light Pool"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.9, 0.7, 1)
        _Intensity ("Intensity", Range(0, 4)) = 0.3
        _Core ("Core", Range(0, 2)) = 0.45
        _Mottle ("Mottle", Range(0, 1)) = 0.5
        _MottleScale ("Mottle Size (1/m)", Range(0.1, 4)) = 0.9
        _EdgeBreak ("Edge Break", Range(0, 1)) = 0.35
        _Speed ("Shimmer Speed", Range(0, 2)) = 0.15
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
            Name "Pool"
            Tags { "LightMode"="UniversalForward" }

            Blend One One
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
                half _Intensity;
                half _Core;
                half _Mottle;
                half _MottleScale;
                half _EdgeBreak;
                half _Speed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : COLOR;
                half fog : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.color = input.color;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = Hd2dTime();
                float2 ground = input.positionWS.xz * _MottleScale;
                float3 swirl;
                float n = Hd2dWarpedFbm(float3(ground, t * _Speed), 1.3, swirl);

                // 縁をゆがめ、円ではなく崩れた形にする。
                float2 d = input.uv * 2.0 - 1.0;
                d += swirl.xy * _EdgeBreak * 0.35;
                float radius = length(d);
                float window = 1.0 - smoothstep(0.7, 0.98, length(input.uv * 2.0 - 1.0));
                float halo = exp(-radius * radius / (2.0 * 0.3 * 0.3)) * window;
                float core = exp(-radius * radius / (2.0 * 0.14 * 0.14));

                float mottle = lerp(1.0, 0.45 + 1.1 * Hd2dSpread(n, 1.8), _Mottle);
                float light = (halo + core * _Core) * mottle * input.color.a;
                half3 color = _Color.rgb * _Intensity * light;
                color += Hd2dDither(input.positionCS.xy) * saturate(light * 40.0);
                color = max(color, 0.0);
                color = MixFogColor(color, half3(0, 0, 0), input.fog);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
