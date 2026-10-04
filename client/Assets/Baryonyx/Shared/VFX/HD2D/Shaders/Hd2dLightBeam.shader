// 木々のすき間から差す光の筋（ゴッドレイ）。板を光の軸のまわりでカメラへ向け、
// 軸に沿った両端と幅の両側を柔らかく消し、ゆっくり流れる縞で揺らす。加算で描く。
Shader "Baryonyx/HD2D/Light Beam"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.92, 0.7, 1)
        _Intensity ("Intensity", Range(0, 4)) = 0.6
        _EdgeSoftness ("Edge Softness", Range(0.05, 1)) = 0.6
        _StripeScale ("Stripe Scale", Range(0, 12)) = 4
        _StripeAmount ("Stripe Amount", Range(0, 1)) = 0.45
        _Speed ("Speed", Range(0, 2)) = 0.12
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _Intensity;
            half _EdgeSoftness;
            half _StripeScale;
            half _StripeAmount;
            half _Speed;
            float _Seed;
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
                float seed : TEXCOORD1;
                float facing : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                // The object's up axis runs along the beam (its length is the up scale), and the
                // board turns round it to face the camera, so the beam never shows its edge.
                float3 center = TransformObjectToWorld(float3(0, 0, 0));
                float3 axis = mul((float3x3)UNITY_MATRIX_M, float3(0, 1, 0));
                float width = length(mul((float3x3)UNITY_MATRIX_M, float3(1, 0, 0)));
                float3 along = normalize(axis);
                float3 toCamera = normalize(GetCameraPositionWS() - center);
                float3 across = cross(along, toCamera);
                float acrossLength = length(across);
                across = acrossLength > 1e-4 ? across / acrossLength : float3(1, 0, 0);
                float3 positionWS = center + across * input.positionOS.x * width + axis * input.positionOS.y;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.seed = _Seed + frac(dot(center, float3(1.31, 7.17, 3.73))) * 41.0;
                // Looking along the beam, it would pile up into a bright blot; fade it there.
                output.facing = saturate(acrossLength * 1.4);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float across = abs(uv.x * 2.0 - 1.0);
                half side = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, across);
                // Brightest a little below the top (where the light comes through the leaves),
                // fading out before it reaches the ground.
                half ends = smoothstep(0.0, 0.45, uv.y) * (1.0 - smoothstep(0.75, 1.0, uv.y));
                float t = _Time.y * _Speed + input.seed;
                half stripes = 0.5 + 0.5 * sin(uv.x * _StripeScale * 6.2832 + t * 2.0)
                    * sin(uv.x * _StripeScale * 2.7 - t * 1.3 + 1.7);
                half shimmer = 0.75 + 0.25 * sin(t * 3.1 + input.seed);
                half strength = side * ends * lerp(1.0, stripes, _StripeAmount) * shimmer * input.facing;
                half3 color = _Color.rgb * _Intensity * strength;
                // Additive light fades into the fog instead of taking its colour.
                color = MixFogColor(color, half3(0, 0, 0), input.fog);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
