// 3Dの舞台の被写界深度。不透明の舞台だけを、カメラからの距離に応じた半径でぼかす。
// 半分の解像度で横・縦の2回ぼかし（各点が自分の半径まで広がる「散らして集める」方式）、元の解像度で合成する。
// Hd2dStageFocus.EvaluateBlurRadius と同じ式でぼかしの半径を決める。
Shader "Hidden/Baryonyx/HD2D/StageFocus"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // x: focus distance, y: half width of the sharp range, z: intensity.
        float4 _Hd2dStageFocusParams;
        // x: near falloff, y: far falloff (m), z: near radius, w: far radius (full-size pixels).
        float4 _Hd2dStageFocusFalloff;
        // x: 1 / width, y: 1 / height, z: width, w: height of the full-size picture.
        float4 _Hd2dStageFocusTexelSize;
        // The blurred picture at half size: colour, and in alpha how much the blur of nearer
        // things covers the point.
        TEXTURE2D_X(_Hd2dStageFocusBlur);

        #define TAPS 6

        float SceneDepth(float2 uv)
        {
            return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
        }

        // The blur radius in full-size pixels at a depth. It grows along a smooth step, so the
        // blur creeps in from the sharp range instead of starting with a crease.
        float BlurRadius(float depth)
        {
            float offset = depth - _Hd2dStageFocusParams.x;
            float far = smoothstep(0.0, 1.0, (offset - _Hd2dStageFocusParams.y) / max(_Hd2dStageFocusFalloff.y, 1e-4));
            float near = smoothstep(0.0, 1.0, (-offset - _Hd2dStageFocusParams.y) / max(_Hd2dStageFocusFalloff.x, 1e-4));
            return max(far * _Hd2dStageFocusFalloff.w, near * _Hd2dStageFocusFalloff.z) * saturate(_Hd2dStageFocusParams.z);
        }

        half4 SampleSource(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel);
        }

        // One direction of the blur. Every tap is a point whose own blur may reach the centre:
        // it counts if its radius is at least its distance, spread thin over its radius. A point
        // behind the centre reaches no farther than the centre's own blur, so the blurred ground
        // never spills over a sharp tree in front of it, while a blurred bush in front spills
        // softly over the sharp ground behind it (alpha keeps how much it covers).
        half4 Blur(float2 uv, float2 direction, bool carryCoverage)
        {
            float depth = SceneDepth(uv);
            float radius = BlurRadius(depth);
            half4 center = SampleSource(uv);
            float maxRadius = max(_Hd2dStageFocusFalloff.z, _Hd2dStageFocusFalloff.w);
            float stepPixels = max(maxRadius / TAPS, 1.0);
            float centerWeight = 1.0 / max(radius, stepPixels);
            half3 color = center.rgb * centerWeight;
            float total = centerWeight;
            float coverage = carryCoverage ? center.a : 0.0;
            [unroll]
            for (int i = 1; i <= TAPS; i++)
            {
                float distance = stepPixels * i;
                float2 offset = direction * _Hd2dStageFocusTexelSize.xy * distance;
                [unroll]
                for (int side = -1; side <= 1; side += 2)
                {
                    float2 tapUV = uv + offset * side;
                    float tapDepth = SceneDepth(tapUV);
                    float tapRadius = BlurRadius(tapDepth);
                    bool nearer = tapDepth < depth - 0.05;
                    float reach = nearer ? tapRadius : min(tapRadius, radius);
                    float covers = saturate((reach - distance) / stepPixels + 0.5);
                    float weight = covers / max(reach, stepPixels);
                    color += SampleSource(tapUV).rgb * weight;
                    total += weight;
                    if (nearer && tapRadius > radius + 1.0)
                        coverage = max(coverage, covers);
                }
            }
            return half4(color / total, coverage);
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
                return Blur(input.texcoord, float2(1.0, 0.0), false);
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
                return Blur(input.texcoord, float2(0.0, 1.0), true);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite

            // A small blur at full size for the onset: a disc of the point's own radius, so the
            // dots soften little by little. The half-size picture is already soft at any radius,
            // so mixing it straight into the sharp one read as the blur starting all at once.
            half3 SmallBlur(float2 uv, float radius)
            {
                float2 texel = _Hd2dStageFocusTexelSize.xy;
                half3 color = SampleSource(uv).rgb;
                float total = 1.0;
                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * (PI / 4.0);
                    float2 direction = float2(cos(angle), sin(angle));
                    color += SampleSource(uv + direction * texel * radius).rgb;
                    color += SampleSource(uv + direction * texel * (radius * 0.5)).rgb;
                    total += 2.0;
                }
                return color / total;
            }

            // Sharp where the point itself is in focus and nothing nearer spills over it; the
            // blurred half-size picture where the blur is large. The point's own radius is read
            // at full size, so a sharp edge against the blurred distance stays crisp. In between,
            // a small full-size blur of the point's radius carries the onset, and gives way to
            // the half-size picture once the radius is a few pixels.
            half4 FragComposite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 sharp = SampleSource(uv);
                half4 blurred = SAMPLE_TEXTURE2D_X_LOD(_Hd2dStageFocusBlur, sampler_LinearClamp, uv, 0);
                float radius = BlurRadius(SceneDepth(uv));
                half3 near = sharp.rgb;
                if (radius > 0.05)
                    near = SmallBlur(uv, min(radius, 4.0));
                float blend = max(smoothstep(2.5, 5.0, radius), saturate(blurred.a * 1.25));
                return half4(lerp(near, blurred.rgb, blend), sharp.a);
            }
            ENDHLSL
        }
    }
}
