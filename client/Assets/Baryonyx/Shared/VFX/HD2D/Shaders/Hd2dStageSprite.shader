// 3Dの舞台に立たせるドット絵の板（ビルボード）のシェーダー。Hd2dUiBillboard が使う。
// 見える板はカメラの面と平行に置くため、薄っぺらく見えない。光は環境光・主光源・追加のライトから受けるが、
// 影は受けない（板の影を落とすのは別に立てる垂直の影の板で、見える板が自分の影で暗くならないようにする）。
// 板は地面に半分めり込まないよう、画面上の位置を変えずにカメラへ少し引き寄せて深度を書く。
Shader "Baryonyx/HD2D/Stage Sprite"
{
    Properties
    {
        [MainTexture] _BaseMap ("Sprite", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0, 1)) = 0
        _AmbientScale ("Ambient Scale", Range(0, 4)) = 1.4
        _MainLightScale ("Main Light Scale", Range(0, 4)) = 0.8
        _PointLightScale ("Point Light Scale", Range(0, 4)) = 1
        _Directionality ("Directionality", Range(0, 1)) = 0.45
        _Lift ("Lift", Color) = (0.08, 0.08, 0.1, 0)
        _DepthPull ("Depth Pull", Float) = 0.6
        _Unlit ("Unlit", Range(0, 1)) = 0
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

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half4 _FlashColor;
            half _FlashAmount;
            half _AmbientScale;
            half _MainLightScale;
            half _PointLightScale;
            half _Directionality;
            half4 _Lift;
            float _DepthPull;
            half _Unlit;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
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
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                // 視点からの線に沿って縮めると、画面上の位置を変えずに深度だけをカメラへ寄せられる。
                float3 positionVS = TransformWorldToView(positionWS);
                float depth = positionVS.z;
                float pulled = min(depth + _DepthPull, -0.05);
                if (depth < -0.06)
                    positionVS *= pulled / depth;
                output.positionCS = TransformWViewToHClip(positionVS);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // 板は正面を向くため、裏から当たる光も弱めて足す（Directionalityが0なら向きを無視する）。
            half Facing(half3 normalWS, half3 lightDirection)
            {
                half wrapped = saturate(dot(normalWS, lightDirection) * 0.5h + 0.5h);
                return lerp(1.0h, wrapped, _Directionality);
            }

            half3 PointLights(float3 positionWS, half3 normalWS, float4 positionCS)
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
                    sum += light.color * light.distanceAttenuation * Facing(normalWS, light.direction);
                }
            #endif
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1));
                    sum += light.color * light.distanceAttenuation * Facing(normalWS, light.direction);
                LIGHT_LOOP_END
            #endif
                return sum;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(texel.a - _Cutoff);
                half3 normalWS = normalize(input.normalWS);

                Light mainLight = GetMainLight();
                half3 light = SampleSH(normalWS) * _AmbientScale;
                light += mainLight.color * Facing(normalWS, mainLight.direction) * _MainLightScale;
                light += PointLights(input.positionWS, normalWS, input.positionCS) * _PointLightScale;
                light += _Lift.rgb;

                // Things that give light themselves (a campfire) keep their own colours.
                light = lerp(light, half3(1, 1, 1), _Unlit);
                half3 color = texel.rgb * _BaseColor.rgb * light;
                color = MixFog(color, input.fogFactor);
                // 狙われたときの発光は光の計算の後に混ぜ、暗い場所でも白く光らせる。
                color = lerp(color, _FlashColor.rgb, _FlashAmount);
                return half4(color, _BaseColor.a);
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
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(positionCS);
                output.uv = input.uv;
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
