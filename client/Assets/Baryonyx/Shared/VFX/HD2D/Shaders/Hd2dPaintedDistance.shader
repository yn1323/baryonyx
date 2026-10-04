// 光を受けず霧もかけずに、描いた色のまま見せる遠景（夜空・夕焼けの空・遠くの山並み）。
// まわりより明るい1〜2ドットの点を星とみなし、明るさを白より上げてBloomで光らせ、点ごとにずらして瞬かせる。
// 月のように広く明るい所も、白より上げてBloomで光らせる。
// 透明な部分（山並みの上の空）は切り抜く。
Shader "Baryonyx/HD2D/Painted Distance"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _StarThreshold ("Star Brightness", Range(0, 1)) = 0.7
        _StarContrast ("Star Contrast", Range(0, 1)) = 0.3
        _StarBoost ("Star Boost", Range(1, 6)) = 1
        _Twinkle ("Twinkle", Range(0, 1)) = 0
        _TwinkleSpeed ("Twinkle Speed", Range(0, 8)) = 2
        _GlowThreshold ("Glow Brightness", Range(0, 1)) = 0.8
        _GlowBoost ("Glow Boost", Range(1, 4)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        float4 _BaseMap_TexelSize;
        half4 _BaseColor;
        float _Cutoff;
        float _StarThreshold;
        float _StarContrast;
        float _StarBoost;
        float _Twinkle;
        float _TwinkleSpeed;
        float _GlowThreshold;
        float _GlowBoost;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            return output;
        }

        float Brightness(float3 color)
        {
            return max(color.r, max(color.g, color.b));
        }
        ENDHLSL

        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(texel.a - _Cutoff);
                half3 color = texel.rgb * _BaseColor.rgb;
                float glow = saturate((Brightness(texel.rgb) - _GlowThreshold) / 0.1);
                color *= lerp(1.0, _GlowBoost, glow);

                // A star is a dot brighter than the dots around it: the moon and lit clouds are
                // bright all over, so they stay as painted.
                if (_StarBoost > 1.0 || _Twinkle > 0.0)
                {
                    float2 texelStep = _BaseMap_TexelSize.xy;
                    float around = 0.25 * (
                        Brightness(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv + float2(texelStep.x, 0)).rgb)
                        + Brightness(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv - float2(texelStep.x, 0)).rgb)
                        + Brightness(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv + float2(0, texelStep.y)).rgb)
                        + Brightness(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv - float2(0, texelStep.y)).rgb));
                    float bright = Brightness(texel.rgb);
                    float star = (bright >= _StarThreshold && bright - around >= _StarContrast) ? 1.0 : 0.0;
                    float2 cell = floor(input.uv * _BaseMap_TexelSize.zw);
                    float seed = frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
                    float wave = 0.5 + 0.5 * sin(_Time.y * _TwinkleSpeed * (0.6 + seed) + seed * 6.2832);
                    float twinkle = lerp(1.0, 0.35 + 0.65 * wave, _Twinkle);
                    color *= lerp(1.0, _StarBoost * twinkle, star);
                }
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // In the camera's depth (when a depth prepass draws it), so the lens sees it far away.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepth

            half FragDepth(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - _Cutoff);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}
