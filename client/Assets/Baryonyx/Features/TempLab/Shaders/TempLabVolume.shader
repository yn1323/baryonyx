// TempLabの試作のエフェクトの炎と煙の本体（ファイアの炎と煙、雷の土ぼこり）。画像を使わず、空間の密度（3Dのノイズ）を視線に沿って積み重ねて描く
// （ボリュームのレイマーチ）。カメラに向けた板1枚を、球の範囲を覆うように置き、画素ごとに球の中を進む。
// 同じ式で、熱い炎（温度で発光、背景を少しだけ隠す）から冷えた煤の煙（光を遮って暗く覆う）まで描く。
// 値は1つずつの描画（MaterialPropertyBlock）で渡す。
// - _Center：xyz 中心（ワールド）、w 半径（m）。
// - _Flow：xyz 進む向き（単位ベクトル）、w 尾の伸び（0で球、2で後ろへ3倍に伸びる）。
// - _Look：x 時刻（秒）、y 乱数の種、z 温度（0〜1）、w 煤（0〜1）。
// - _Body：x 濃さ、y 消えた割合（0〜1。ノイズの低い所から削れる）、z ゆがみ、w 炎が奥を隠す割合。
// - _Glow：xyz 中の火の位置、w 強さ（煙を内側から照らす）。
// - _Opacity：全体の濃さ。
// 合成は乗算済みアルファ（One OneMinusSrcAlpha）。色は発光と煙の照り返しを足したもので、アルファで奥を隠す。
// 地面や舞台の物に隠れる所は、深度のテクスチャで光線を止める（URPアセットの Depth Texture が要る）。
Shader "Baryonyx/TempLab/Volume"
{
    Properties
    {
        _Center ("Center (xyz), Radius (w)", Vector) = (0, 1, 0, 0.5)
        _Flow ("Flow Direction (xyz), Tail Stretch (w)", Vector) = (1, 0, 0, 0)
        _Look ("Time, Seed, Heat, Soot", Vector) = (0, 0, 1, 0)
        _Body ("Density, Erode, Warp, Fire Occlusion", Vector) = (1, 0, 0.8, 0.35)
        _Glow ("Inner Fire Position (xyz), Strength (w)", Vector) = (0, 1, 0, 0)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Steps ("Ray Steps", Range(8, 96)) = 40
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
            Name "FireVolume"
            Tags { "LightMode"="UniversalForward" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            // 板の手前・奥は光線の中で深度と比べて決める。板の平面で切ると、地面に近い炎の下半分が消える。
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "TempLabVfx.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Center;
                float4 _Flow;
                float4 _Look;
                float4 _Body;
                float4 _Glow;
                float _Opacity;
                float _Steps;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screen : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.screen = ComputeScreenPos(output.positionCS);
                return output;
            }

            // 半径1にそろえた座標 q での濃さ。temperature に、その点の温度を返す。
            float Density(float3 q, float time, float seed, out float temperature)
            {
                float3 flow = _Flow.xyz;
                float stretch = _Flow.w;
                float along = dot(q, flow);
                // 進む側は少し押し潰して前の面を張らせ、後ろは尾として伸ばす。
                float squeezed = along > 0 ? along * 1.25 : along / (1 + stretch);
                float3 shaped = q - flow * along + flow * squeezed;
                float r = length(shaped);

                // 模様は、飛ぶ炎では尾の方へ、止まった炎では上へ（熱い気体は昇る）流れる。
                float3 drift = -flow * (stretch > 0.01 ? 3.2 : 0.0) + float3(0, 1.5, 0);
                float3 np = q * 2.1 - drift * time + seed * 17.13;
                float n = Spread(WarpedFbm(np, _Body.z), 2.4);
                float fine = Noise(q * 7.3 - drift * time * 1.7 + seed * 5.1);

                // 縁はノイズで揺らし、丸い玉の輪郭を崩す。細かい揺らぎは縁ほど強く効かせる。
                // 煤は炎よりちぎれやすいので、縁の揺れを大きくする。
                float ragged = 0.95 + 0.75 * _Look.w;
                float edge = 1.0 - r + (n - 0.5) * ragged + (fine - 0.5) * 0.3 * r;
                // 炎は上の縁ほど伸ばし、舌のように立ち上がらせる。
                edge += saturate(q.y) * (n - 0.45) * 0.6 * (1 - _Look.w);
                float body = saturate(edge * 2.2);
                // 消えるときは、ノイズの低い所から削る（全体を一様に薄めない）。
                float threshold = lerp(-0.15, 1.05, _Body.y);
                float keep = smoothstep(threshold, threshold + 0.18, n * 0.85 + fine * 0.15);

                float tail = saturate(-along / (1 + stretch));
                temperature = _Look.z * saturate((1.0 - r * 0.85) * 1.35 + (n - 0.5) * 0.7);
                temperature *= lerp(1.0, 0.5, tail);
                return _Body.x * body * keep;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 center = _Center.xyz;
                float radius = max(_Center.w, 1e-3);
                // 尾と、ノイズで外へ揺れた縁まで入る外側の球。小さいと縁が丸く切れて円盤に見える。
                float bound = radius * (1.75 + _Flow.w);

                float3 origin = _WorldSpaceCameraPos;
                float3 dir = normalize(input.positionWS - origin);
                float3 oc = origin - center;
                float b = dot(oc, dir);
                float c = dot(oc, oc) - bound * bound;
                float h = b * b - c;
                if (h <= 0)
                    discard;
                h = sqrt(h);
                float tNear = max(-b - h, 0);
                float tFar = -b + h;

                // 地面や舞台の物の手前で止める。
                float2 uv = input.screen.xy / input.screen.w;
                float eye = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float3 forward = -UNITY_MATRIX_V[2].xyz;
                tFar = min(tFar, eye / max(dot(dir, forward), 1e-3));
                if (tFar <= tNear)
                    discard;

                int steps = (int)_Steps;
                float stepLength = (tFar - tNear) / steps;
                // 光線ごとに始まりをずらし、段の縞を細かいざらつきに変える（画素ごとに固定してちらつかせない）。
                float jitter = Hash(float3(input.positionCS.xy, 3.7));
                float t = tNear + stepLength * jitter;

                float time = _Look.x;
                float seed = _Look.y;
                float soot = _Look.w;
                Light sun = GetMainLight();
                half3 smokeAlbedo = half3(0.12, 0.1, 0.095);

                half3 color = 0;
                float transmittance = 1;
                [loop]
                for (int i = 0; i < steps; i++)
                {
                    float3 p = origin + dir * t;
                    float3 q = (p - center) / radius;
                    float temperature;
                    float density = Density(q, time, seed, temperature);
                    if (density > 0.001)
                    {
                        // 濃さは半径1あたり。ワールドの長さに直して、段の長さを変えても見た目を変えない。
                        float extinction = density * 7.0 / radius;
                        float alpha = 1 - exp(-extinction * stepLength);

                        half3 fire = FireColor(temperature);
                        // 煙は夕日と空から淡く、中の火から暖かく照らされる。上ほど明るい。
                        float3 toGlow = _Glow.xyz - p;
                        float glow = _Glow.w / (1 + dot(toGlow, toGlow) * 2.5);
                        float up = saturate(q.y * 0.5 + 0.6);
                        half3 smoke = smokeAlbedo
                            * (half3(0.52, 0.42, 0.38) * up + sun.color * 0.3
                                + half3(2.6, 0.9, 0.25) * glow);
                        half3 local = fire * (1 - soot * 0.85) + smoke * soot;

                        color += transmittance * alpha * local;
                        transmittance *= 1 - alpha;
                        if (transmittance < 0.01)
                            break;
                    }
                    t += stepLength;
                }

                float covered = 1 - transmittance;
                float occlusion = lerp(_Body.w, 0.82, soot);
                return half4(color * _Opacity, covered * occlusion * _Opacity);
            }
            ENDHLSL
        }
    }
}
