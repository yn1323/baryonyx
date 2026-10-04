// 3Dの舞台でドット絵の板の足元に敷く接地影。地面に寝かせた四角形の中に、楕円の影を計算で描く。
// 光源から落ちる影（垂直の影の板）とは別に、立っている位置を地面へ結び付ける。
Shader "Baryonyx/HD2D/Contact Shadow"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0.02, 0.02, 0.05, 1)
        _Strength ("Strength", Range(0, 1)) = 0.55
        _Softness ("Softness", Range(0.01, 1)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "ContactShadow"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
                half _Strength;
                half _Softness;
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
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 中心が濃く、縁へ向かって消える楕円。頂点色のAで板の透け方を足元の影にも移す。
                float radius = length(input.uv * 2.0 - 1.0);
                half alpha = 1.0h - smoothstep(1.0h - _Softness, 1.0h, radius);
                return half4(_ShadowColor.rgb, alpha * _Strength * input.color.a);
            }
            ENDHLSL
        }
    }
}
