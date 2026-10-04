// 3Dの舞台に立てる草・シダ・茂み・枝葉の切り抜きの板のシェーダー。
// 葉は薄く光を透かすため、カメラを向く板の裏から当たる月明かりや松明の光も弱めて受ける（Simple Litでは裏の光が届かず、黒い塊に見えた）。
// 主光源の影と木漏れ日のクッキー、点光源、環境光、霧を受け、穂先（または垂れた枝先）を風でゆっくり揺らす。
// 不透明の列で深度を書くため、舞台のレンズで地面と同じようにぼける。
Shader "Baryonyx/HD2D/Stage Foliage"
{
    Properties
    {
        [MainTexture] _BaseMap ("Picture", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _Translucency ("Translucency", Range(0, 1)) = 0.5
        _Sway ("Sway (m)", Float) = 0.03
        _SwaySpeed ("Sway Speed", Float) = 1.3
        _HangDown ("Hangs Down", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="AlphaTest"
            "RenderType"="TransparentCutout"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half _Translucency;
            float _Sway;
            float _SwaySpeed;
            half _HangDown;
        CBUFFER_END

        // 板の上端（垂れる枝葉は下端）ほど大きく、場所ごとに位相をずらした2つの波で横へ揺らす。
        float3 Swayed(float3 positionWS, float2 uv)
        {
            float tip = lerp(uv.y, 1.0 - uv.y, _HangDown);
            float phase = dot(positionWS.xz, float2(0.37, 0.23));
            float t = _Time.y * _SwaySpeed;
            float wave = sin(t + phase) + 0.35 * sin(t * 2.3 + phase * 1.7);
            positionWS.x += wave * _Sway * tip;
            positionWS.z += 0.3 * wave * _Sway * tip;
            return positionWS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = Swayed(TransformObjectToWorld(input.positionOS.xyz), input.uv);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // 表から当たる光はそのまま、裏から当たる光は Translucency の分だけ透かして受ける。
            half Leaf(half3 normalWS, half3 lightDirection)
            {
                half facing = dot(normalWS, lightDirection);
                return facing >= 0.0h ? facing * 0.6h + 0.4h : (0.4h + facing * 0.4h) * _Translucency * 2.0h;
            }

            half3 OtherLights(float3 positionWS, half3 normalWS, float4 positionCS)
            {
                half3 sum = 0;
            #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
                uint lightCount = GetAdditionalLightsCount();
            #if USE_CLUSTER_LIGHT_LOOP
                UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    Light light = GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1));
                    sum += light.color * light.distanceAttenuation * Leaf(normalWS, light.direction);
                }
            #endif
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1));
                    sum += light.color * light.distanceAttenuation * Leaf(normalWS, light.direction);
                LIGHT_LOOP_END
            #endif
                return sum;
            }

            half4 Frag(Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(texel.a - _Cutoff);
                half3 normalWS = normalize(input.normalWS) * (frontFace ? 1.0h : -1.0h);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, half4(1, 1, 1, 1));
                // 草は空も周りも見ているため、環境光は板の向きと真上の間の向きで受ける。
                half3 light = SampleSH(normalize(normalWS + half3(0, 0.8h, 0)));
                light += mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation
                    * Leaf(normalWS, mainLight.direction);
                light += OtherLights(input.positionWS, normalWS, input.positionCS);

                half3 color = texel.rgb * _BaseColor.rgb * light;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                float3 positionWS = Swayed(TransformObjectToWorld(input.positionOS.xyz), input.uv);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(positionCS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

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

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                float3 positionWS = Swayed(TransformObjectToWorld(input.positionOS.xyz), input.uv);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - _Cutoff);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}
