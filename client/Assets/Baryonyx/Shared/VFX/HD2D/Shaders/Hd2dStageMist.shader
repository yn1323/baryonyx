// 沼・朝の森・雪原・夜の墓地のように、現実に霧やもやが生じる舞台に置く、地面を這う霧の板。画像を使わず計算で描く。
// 現実の地面の霧は、冷えた地面や水面の上に溜まり、低いほど濃く、風でゆっくり流れながら形を変える。
// 上側は空と日の光を受けて明るく、下側と濃い塊の奥は暗く、光の当たる側の縁が明るく光る。
// 霧の中の灯り（鬼火・溶岩・ランタン）は、まわりの霧をその色に照らす。木々の影の中の霧は暗い。
// 煙・霧の4層（lessons.md）を、1枚の中で速さと透け方の違う場として重ねる。
// - 暗い本体：2回ゆがめた5段のfBm。広く柔らかい塊で、最も濃い所でも奥が半分ほど透ける。最も遅く流れる。
// - 盛り上がり：本体の濃淡を、光の来る側へずらして読み直した差で、光の側の縁を明るく、くびれと底を暗くする。
// - ちぎれた切れ端：細かいfBmが本体の縁を速く削り、ほとんどが穴の細い切れ端を残す。
// - 中の光：主光源（影を受ける）と点光源の光を、霧の濃さに応じて散らして足す。
// 加えて、光を吸う暗い筋（尾根のように細いノイズ）を入れる。
// 板は地面に寝かせて高さを変えて重ねる（低い板ほど濃く遅い）か、奥に立てる。
// 地面や物と交わる所は、深度との差で柔らかく消す（線を見せない）。薄く広い霧の階調の段差は、画素ごとのごく弱い揺らぎで散らす。
Shader "Baryonyx/HD2D/Stage Mist"
{
    Properties
    {
        _Color ("Mist Colour", Color) = (0.75, 0.8, 0.82, 1)
        _Density ("Density", Range(0, 1)) = 0.5
        _Coverage ("Coverage", Range(0, 1)) = 0.55
        _Scale ("Scale (1/m)", Range(0.02, 2)) = 0.22
        _Stretch ("Stretch Along The Wind", Range(1, 4)) = 2
        _Wind ("Wind (x, z m/s)", Vector) = (0.25, 0.05, 0, 0)
        _Evolve ("Change Speed", Range(0, 0.5)) = 0.05
        _Wisps ("Wisps", Range(0, 1)) = 0.5
        _Streaks ("Dark Streaks", Range(0, 1)) = 0.35
        _Ambient ("Ambient Light", Range(0, 3)) = 1.2
        _SunLight ("Sun Light", Range(0, 3)) = 0.8
        _RimLight ("Rim Light", Range(0, 3)) = 0.9
        _LampLight ("Lamp Light", Range(0, 3)) = 1
        _Softness ("Soft Edge (m)", Range(0.01, 3)) = 0.3
        _HeightFade ("Height Fade (m, standing boards)", Range(0.1, 10)) = 1.6
        _Seed ("Seed", Float) = 0
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
            Name "Mist"
            Tags { "LightMode"="UniversalForward" }

            // 色は透明度を掛けてから出す（Premultiplied）。
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Hd2dStageAir.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Density;
                half _Coverage;
                half _Scale;
                half _Stretch;
                float4 _Wind;
                half _Evolve;
                half _Wisps;
                half _Streaks;
                half _Ambient;
                half _SunLight;
                half _RimLight;
                half _LampLight;
                half _Softness;
                half _HeightFade;
                float _Seed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 screen : TEXCOORD2;
                half4 color : COLOR;
                // x：寝かせた板なら1、立てた板なら0。y：板の根元の高さ（m）。z：霧の係数。
                float3 board : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.screen = ComputeScreenPos(output.positionCS);
                output.uv = input.uv;
                output.color = input.color;
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float lying = step(0.7, abs(normalWS.y));
                float baseHeight = TransformObjectToWorld(float3(0, 0, 0)).y;
                output.board = float3(lying, baseHeight, ComputeFogFactor(output.positionCS.z));
                return output;
            }

            // 霧の本体の濃さ（0〜1）。p は風で流した、霧の模様の座標。
            float Body(float3 p, out float3 swirl)
            {
                return Hd2dWarpedFbm(p, 1.7, swirl);
            }

            half3 Lamps(float3 positionWS, float4 positionCS)
            {
                half3 sum = 0;
            #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.normalWS = half3(0, 1, 0);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
                uint lightCount = GetAdditionalLightsCount();
            #if USE_CLUSTER_LIGHT_LOOP
                UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    Light light = GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1));
                    sum += light.color * light.distanceAttenuation;
                }
            #endif
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1));
                    sum += light.color * light.distanceAttenuation;
                LIGHT_LOOP_END
            #endif
                return sum;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = Hd2dTime();
                float3 w = input.positionWS;
                float lying = input.board.x;

                // 霧の模様の座標：風で流し、風の向きに伸ばす。高さの方向は細かく（霧は横に層をなす）。
                float2 wind = _Wind.xy;
                float3 flow = float3(w.x - wind.x * t, w.y, w.z - wind.y * t);
                float3 p = float3(flow.x / _Stretch, flow.y * 2.2, flow.z) * _Scale;
                p += float3(_Seed, _Seed * 0.37, t * _Evolve);

                // 暗い本体：広く柔らかい塊。
                float3 swirl;
                // fBmは0.5のまわりに寄るため、広げてから閾値を切る（塊と晴れ間をはっきり分ける）。
                float body = Hd2dSpread(Body(p, swirl), 1.8);
                float mass = smoothstep(1.0 - _Coverage - 0.25, 1.0 - _Coverage + 0.35, body);
                mass *= mass * (3.0 - 2.0 * mass);

                // ちぎれた切れ端：細かいノイズが本体の縁を速く削る。縁ほど強く削り、芯は残す。
                float3 fine = float3(flow.x / _Stretch, flow.y * 2.2, flow.z) * _Scale * 4.3
                    + swirl * 0.6 + float3(-wind.x, 0.0, -wind.y) * t * _Scale * 2.0 + float3(0, 0, t * _Evolve * 3.0);
                float wisp = Hd2dFbm(fine, 3);
                float edge = 1.0 - abs(mass * 2.0 - 1.0);
                mass = saturate(mass - (1.0 - wisp) * _Wisps * (0.35 + 0.65 * edge));

                // 光を吸う暗い筋：尾根のように細いノイズ。
                float ridge = 1.0 - abs(Hd2dFbm(p * 2.7 + swirl * 0.8 + 3.3, 3) * 2.0 - 1.0);
                float streak = smoothstep(0.86, 0.97, ridge) * _Streaks * smoothstep(0.1, 0.5, mass);

                // 盛り上がり：光の来る側へずらして読み直した濃さとの差。光の側の縁が明るく、奥とくびれが暗い。
                float4 shadowCoord = TransformWorldToShadowCoord(w);
                Light sun = GetMainLight(shadowCoord, w, half4(1, 1, 1, 1));
                float3 towardSun = sun.direction;
                float3 lit = float3(towardSun.x / _Stretch, towardSun.y * 2.2, towardSun.z) * _Scale * 0.9;
                float3 swirlLit;
                float bodyLit = Hd2dSpread(Body(p + lit, swirlLit), 1.8);
                float relief = saturate(0.5 + (body - bodyLit) * 4.0);

                // 高さ：立てた板は上ほど薄い。寝かせた板は高さごとの濃さを頂点色で受ける。
                float height = w.y - input.board.y;
                float rise = lerp(exp(-max(height, 0.0) / _HeightFade), 1.0, lying);

                // 板の縁で0にして、四角い板の縁を見せない。
                float2 window2 = smoothstep(0.0, 0.18, input.uv) * smoothstep(0.0, 0.18, 1.0 - input.uv);
                float window = window2.x * window2.y;
                // 地面や物と交わる所は柔らかく消す（深度との差）。カメラのすぐ前も消す。
                float2 screenUV = input.screen.xy / input.screen.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float eye = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                float soft = saturate((sceneEye - eye) / _Softness);
                float near = saturate((eye - 1.5) / 3.0);

                // 寝かせた板は、視線が浅く横切るほど霧の中を長く通るため濃く見える（遠くほど濃く、足元は薄い）。
                float3 view = GetWorldSpaceNormalizeViewDir(w);
                float slant = lerp(1.0, clamp(0.34 / max(abs(view.y), 0.05), 0.45, 1.8), lying);

                float density = mass * rise * window * soft * near * slant * input.color.a;
                float alpha = saturate(density * _Density * (1.0 + streak * 0.6));

                // 中の光：空の光（環境光）、影を受ける主光源、光の向きの縁、灯り。
                half3 sky = SampleSH(half3(0, 1, 0));
                half3 sunColor = sun.color * sun.distanceAttenuation * sun.shadowAttenuation;
                // 光の方を向くほど、霧は前へ散らす光で明るく見える（前方散乱）。
                float forward = pow(saturate(dot(-view, towardSun) * 0.5 + 0.5), 4.0);
                half3 light = sky * _Ambient * lerp(0.7, 1.15, relief)
                    + sunColor * _SunLight * (0.35 + 0.65 * relief + forward * 0.8)
                    + sunColor * _RimLight * smoothstep(0.55, 0.9, relief) * (1.0 - mass) * 2.0
                    + Lamps(w, input.positionCS) * _LampLight * (0.6 + 0.6 * body);
                half3 mist = _Color.rgb * light * (1.0 - streak * 0.55);

                half3 color = mist * alpha;
                float grain = Hd2dDither(input.positionCS.xy);
                color += grain * saturate(alpha * 30.0);
                alpha = saturate(alpha + grain * saturate(alpha * 30.0));
                color = MixFogColor(color, unity_FogColor.rgb * alpha, input.board.z);
                return half4(max(color, 0.0), alpha);
            }
            ENDHLSL
        }
    }
}
