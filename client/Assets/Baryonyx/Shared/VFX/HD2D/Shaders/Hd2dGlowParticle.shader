// 3Dの舞台の光の粒（火の粉、光の中の塵、蛍、雪）と、灯りのまわりの光（ランタン・門の光）。画像を使わず計算で描く。
// 粒は、小さく明るい芯（Core）と、広く薄い色のにじみ（Halo）を別々の幅と強さで描く（F2）。芯だけを1より明るくしてBloomでにじませる。
// 明滅は粒ごとに位相をずらす。Particle Systemが頂点の追加の値（Stable Random）を渡すと、粒ごとに違う乱数で明滅する
// （火の粉は速くちらつき、蛍はゆっくり明滅し、塵は向きが合った瞬間だけ光る。速さと深さはマテリアルで決める）。
// 明滅は芯で深く、にじみで浅くする。
// _Occlude を上げると、粒の形の分だけ奥を隠して描く（雪のように光らない白い粒）。0なら加算だけ。
// 色はParticle Systemの頂点色（または頂点色の白）に _Color と _Intensity を掛ける。
Shader "Baryonyx/HD2D/Glow Particle"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.7, 0.35, 1)
        _Intensity ("Intensity", Range(0, 8)) = 2
        _Core ("Core Size", Range(0.01, 1)) = 0.18
        _CoreBoost ("Core Brightness", Range(0, 6)) = 2
        _Halo ("Halo", Range(0, 2)) = 1
        _HaloSize ("Halo Size", Range(0.1, 0.6)) = 0.3
        _Twinkle ("Twinkle Depth", Range(0, 1)) = 0
        _TwinkleSpeed ("Twinkle Speed", Range(0, 20)) = 2
        _Occlude ("Occlude", Range(0, 1)) = 0
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

            // 透明度は奥を隠す量。0なら加算と同じになる。
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Hd2dStageAir.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Core;
                half _CoreBoost;
                half _Halo;
                half _HaloSize;
                half _Twinkle;
                half _TwinkleSpeed;
                half _Occlude;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                // xy：板の中の位置、zw：Particle Systemの Stable Random（なければ0）
                float4 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float radius = length(input.uv.xy * 2.0 - 1.0);
                // 板の縁より内側で0にして、四角い板の縁を見せない。
                float window = 1.0 - smoothstep(0.72, 1.0, radius);
                // にじみ：広く薄い、色の付いた光。
                float halo = exp(-radius * radius / (2.0 * _HaloSize * _HaloSize)) * window;
                // 芯：小さく明るい点。幅はにじみと別に決める。
                float coreSize = max(_Core * 0.5, 0.005);
                float core = exp(-radius * radius / (2.0 * coreSize * coreSize));

                // 粒ごとの明滅。乱数は時刻の位相と、速さのばらつきに使う。
                float seed = input.uv.z * 37.0 + input.uv.w * 11.0;
                float rate = _TwinkleSpeed * (0.7 + 0.6 * frac(input.uv.w * 7.31));
                float wave = Hd2dNoise(float2(Hd2dTime() * rate + seed, seed * 0.37));
                float flicker = saturate(wave * 1.8 - 0.35);
                float coreTwinkle = lerp(1.0, flicker * 1.6, _Twinkle);
                float haloTwinkle = lerp(1.0, 0.45 + 0.75 * flicker, _Twinkle);

                half3 tint = _Color.rgb * input.color.rgb * _Intensity * input.color.a;
                half3 light = tint * (halo * _Halo * haloTwinkle + core * _CoreBoost * coreTwinkle);
                float cover = saturate(halo * 1.6) * _Occlude * input.color.a;
                return half4(light, cover);
            }
            ENDHLSL
        }
    }
}
