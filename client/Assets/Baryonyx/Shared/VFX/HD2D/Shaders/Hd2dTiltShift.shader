// 疑似ティルトシフト。奥行きを使わず、画面の高さに応じて半径を変える2パスのガウスぼかし。
// Hd2dTiltShift.EvaluateBlurAmount と同じ式でぼかしの強さを決める。
Shader "Hidden/Baryonyx/HD2D/TiltShift"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // x: center of the sharp band, y: half height of the band, z: falloff, w: intensity.
        float4 _Hd2dTiltShiftParams;
        // Blur radius in pixels of the current target at full strength.
        float _Hd2dTiltShiftRadius;
        // x: 1 / width, y: 1 / height, z: width, w: height of the source texture.
        float4 _Hd2dTiltShiftTexelSize;

        float BlurAmount(float height01)
        {
            float distance = abs(height01 - _Hd2dTiltShiftParams.x) - _Hd2dTiltShiftParams.y;
            return saturate(distance / max(_Hd2dTiltShiftParams.z, 1e-4)) * saturate(_Hd2dTiltShiftParams.w);
        }

        half4 SampleSource(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel);
        }

        half4 Blur(float2 uv, float2 direction)
        {
            float amount = BlurAmount(uv.y);
            half4 center = SampleSource(uv);
            if (amount <= 0.0)
                return center;

            // Nine taps spread over the radius; the weights follow a Gaussian curve.
            float2 stepUV = direction * _Hd2dTiltShiftTexelSize.xy * (_Hd2dTiltShiftRadius * amount / 4.0);
            half4 color = center * 0.2270270;
            color += (SampleSource(uv + stepUV) + SampleSource(uv - stepUV)) * 0.1945946;
            color += (SampleSource(uv + stepUV * 2.0) + SampleSource(uv - stepUV * 2.0)) * 0.1216216;
            color += (SampleSource(uv + stepUV * 3.0) + SampleSource(uv - stepUV * 3.0)) * 0.0540541;
            color += (SampleSource(uv + stepUV * 4.0) + SampleSource(uv - stepUV * 4.0)) * 0.0162162;
            return color;
        }
        ENDHLSL

        Pass
        {
            Name "Horizontal"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragHorizontal

            half4 FragHorizontal(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return Blur(input.texcoord, float2(1.0, 0.0));
            }
            ENDHLSL
        }

        Pass
        {
            Name "Vertical"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragVertical

            half4 FragVertical(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return Blur(input.texcoord, float2(0.0, 1.0));
            }
            ENDHLSL
        }
    }
}
