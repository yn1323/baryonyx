// 3Dの舞台の光の粒（火の粉、埃のきらめき）と、門や魔法の光だまり。画像を使わず、中心から外へ消える丸を計算で描き、加算で足す。
// 色はParticle Systemの頂点色（または頂点色の白）に _Color と _Intensity を掛ける。1より明るくするとBloomでにじむ。
Shader "Baryonyx/HD2D/Glow Particle"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.7, 0.35, 1)
        _Intensity ("Intensity", Range(0, 8)) = 2
        _Core ("Core Size", Range(0.01, 1)) = 0.18
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

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Core;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
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
                // 小さく明るい芯と、外へ二乗で消える光。
                float radius = length(input.uv * 2.0 - 1.0);
                half glow = saturate(1.0 - radius);
                glow *= glow;
                half core = saturate(1.0 - radius / _Core);
                half3 color = _Color.rgb * input.color.rgb * (glow + core * 2.0) * _Intensity * input.color.a;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
