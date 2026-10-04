// 3Dの舞台の松明・かがり火の炎。画像を使わず、ノイズで削った炎の舌と炎の周りの光を計算で描き、加算で足す。
// 実際の炎の見え方を、ゲームの炎の作り方（勾配の形をノイズで削る、ノイズで座標をゆがめる、上へ流す）で組み立てる。
// - 形：根元は太い塊で、上ほど強くノイズで削り、複数の舌に割る。舌の先はちぎれて燃え尽きる。
// - 流れ：熱い気体は昇るほど速くなるため、ノイズを高さの平方根の座標で上へ流す。上の模様ほど速く、縦に伸びる。
// - 揺らぎ：根元から昇るふくらみが先をちぎる周期は、炎の幅の平方根に反比例する（幅0.3mで約3回/秒）。
//   板の大きさから周期と昇る速さを決めるため、大きな炎ほどゆっくり揺れる。
// - 色：温度の高い芯ほど黄白く、強く光る。外側と先は温度が低く、橙から暗い赤になる。芯だけが1を超えてBloomでにじむ。
// 四角形はオブジェクトの原点を中心にカメラへ向け直す。炎の根元は板の下から _Base の高さにあり、
// 炎は板の中央の幅 _FlameWidth・高さ _FlameHeight（板に対する割合）に収まる。上の残りには、ちぎれた舌の先と周りの光を描く（置き方は Hd2dStageKit.Flame）。
// 揺れ方は置いた位置から決まるため、同じマテリアルの炎どうしも揃って揺れない。
Shader "Baryonyx/HD2D/Flame"
{
    Properties
    {
        _CoreColor ("Core Color", Color) = (1, 0.92, 0.62, 1)
        _MidColor ("Mid Color", Color) = (1, 0.52, 0.16, 1)
        _EdgeColor ("Edge Color", Color) = (0.85, 0.16, 0.05, 1)
        _HaloColor ("Halo Color", Color) = (1, 0.45, 0.15, 1)
        _Intensity ("Intensity", Range(0, 8)) = 1.6
        _Halo ("Halo", Range(0, 2)) = 0.3
        _Turbulence ("Turbulence", Range(0, 2)) = 1
        _Speed ("Rise Speed", Range(0, 4)) = 2.2
        _Puff ("Puffing", Range(0, 4)) = 1.6
        _Seed ("Seed", Float) = 0
        _Base ("Root Height", Range(0, 0.5)) = 0.1
        _FlameHeight ("Flame Height", Range(0.1, 1)) = 0.5
        _FlameWidth ("Flame Width", Range(0.1, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "DisableBatching"="True"
            "PreviewType"="Plane"
        }

        Pass
        {
            Name "Flame"
            Tags { "LightMode"="UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _MidColor;
                half4 _EdgeColor;
                half4 _HaloColor;
                half _Intensity;
                half _Halo;
                half _Turbulence;
                half _Speed;
                half _Puff;
                float _Seed;
                half _Base;
                half _FlameHeight;
                half _FlameWidth;
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
                // x：時刻のずれ（秒）、y：炎の幅÷高さ、z：ふくらみの周期（回/秒）、w：昇る速さ（炎の高さ/秒）
                float4 motion : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                float2 scale = float2(
                    length(UNITY_MATRIX_M._m00_m10_m20),
                    length(UNITY_MATRIX_M._m01_m11_m21));
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 positionWS = origin
                    + right * input.positionOS.x * scale.x
                    + up * input.positionOS.y * scale.y;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                // 炎そのものの幅と高さ（m）。大きな炎ほど、ふくらみの周期が長く、高さに対して昇るのが遅い。
                float2 size = max(scale * float2(_FlameWidth, _FlameHeight), 0.01);
                output.motion = float4(
                    _Seed + frac(dot(origin, float3(1.31, 7.17, 3.73))) * 41.0,
                    size.x / size.y,
                    _Puff * rsqrt(size.x),
                    _Speed * rsqrt(size.y));
                return output;
            }

            // 格子点ごとの乱数（PCG2D）。整数で計算するため、座標が大きくなっても模様が崩れない。
            float Hash(float2 cell)
            {
                uint2 v = (uint2)(int2)cell * 1664525u + 1013904223u;
                v.x += v.y * 1664525u;
                v.y += v.x * 1664525u;
                v ^= v >> 16u;
                v.x += v.y * 1664525u;
                v.y += v.x * 1664525u;
                v ^= v >> 16u;
                return (v.x ^ v.y) * (1.0 / 4294967295.0);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm2(float2 p)
            {
                return Noise(p) * 0.667 + Noise(p * 2.03 + 17.1) * 0.333;
            }

            float Fbm3(float2 p)
            {
                return Noise(p) * 0.571 + Noise(p * 2.03 + 17.1) * 0.286 + Noise(p * 4.07 + 41.3) * 0.143;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 長く起動していても乱数の座標の精度を保つよう、時刻を20分で巻き戻す。
                float t = fmod(_Time.y, 1200.0) + input.motion.x;
                float aspect = input.motion.y;
                float puffRate = input.motion.z;
                float scroll = t * input.motion.w;

                // 炎の座標：xは炎の幅を1とした横（縁が±0.5）、yは炎の高さを1とした根元からの高さ。
                float2 f = float2((input.uv.x - 0.5) / _FlameWidth, (input.uv.y - _Base) / _FlameHeight);
                float h = saturate(f.y);
                // 浮力で昇るほど速くなる流れ。高さの平方根の座標でノイズを流す。
                float flow = sqrt(max(f.y + 0.25, 0.0));

                // 大きな渦と、ゆっくり変わる風で炎を曲げる。根元は動かさず、上ほど大きく振る。
                float eddy = Fbm2(float2(f.x * aspect * 1.6, flow * 2.2 - scroll * 0.55)) - 0.5;
                float lean = (Noise(float2(t * 0.45, input.motion.x)) - 0.5) * 0.35;
                f.x += (eddy * 0.9 * _Turbulence + lean) * h * (0.35 + 0.65 * h);

                // ふくらみ：根元から昇るふくらみとくびれが、一定の周期で先をちぎる。
                float puff = sin(6.2831853 * (flow * 1.8 - t * puffRate));

                // 炎の塊：根元が太く、上へ細くなる。根元の下は丸く閉じる。幅はふくらみに合わせて息をする。
                float halfWidth = 0.5 * max(sqrt(saturate(1.0 - f.y * 0.8)), 0.18);
                halfWidth *= sqrt(smoothstep(-0.3, 0.12, f.y));
                halfWidth *= 1.0 + 0.2 * puff * smoothstep(0.05, 0.8, f.y);
                // 断面は中央を平らにして、幅の広い炎では舌が横に並んで立つようにする。
                float across = abs(f.x) / max(halfWidth, 0.02);
                float body = 1.0 - across * across;

                // 舌：縦に伸びた乱流のノイズで塊を削る。根元は少しだけ削り、上ほど強く削って舌に割る。
                float n = Fbm3(float2(f.x * aspect * 5.0 + eddy * 0.8, flow * 5.0 - scroll * 1.6));
                float heat = body - (1.0 - n) * lerp(0.3, 1.25, h) * _Turbulence;
                // 先へ行くほど冷えて燃え尽きる。ノイズの山だけが、ちぎれた舌として少し上に残る。
                heat -= smoothstep(0.5, 1.45, f.y) * 0.9;

                // 炎の面の縁は鋭く、内側ほど温度が高い。上ほど冷えて赤くなる。
                // 根元は燃える前の気体が多くまだ暗いため、器の穴から見える根元は暗い橙にする。
                float edge = max(fwidth(heat) * 1.5, 0.02) + 0.015;
                float flame = smoothstep(0.0, edge, heat);
                float temperature = saturate(heat * 1.4)
                    * lerp(1.0, 0.6, smoothstep(0.25, 1.1, f.y))
                    * lerp(0.4, 1.0, smoothstep(-0.2, 0.15, f.y));
                half3 color = lerp(_EdgeColor.rgb, _MidColor.rgb, smoothstep(0.02, 0.4, temperature));
                color = lerp(color, _CoreColor.rgb, smoothstep(0.55, 0.95, temperature));
                // 熱い気体ほど急に明るく光るため、芯だけがBloomのしきい値を超える。
                // 内側も乱流で明暗の筋が昇っていくよう、舌を削るノイズで明るさにむらを付ける。
                half3 fire = color * flame * _Intensity * (0.4 + 1.5 * temperature * temperature);
                fire *= lerp(1.0, 0.7 + 0.6 * n, smoothstep(0.0, 0.5, f.y));

                // 板の縁で0にして、四角い板の縁を見せない。
                float2 window = smoothstep(0.0, 0.2, input.uv) * smoothstep(0.0, 0.2, 1.0 - input.uv);
                float fade = window.x * window.y;

                // 炎の周りの空気が照らされた光。ふくらみに合わせて明るさが揺れる。
                float2 g = input.uv - float2(0.5, _Base + _FlameHeight * 0.35);
                float halo = exp(-(g.x * g.x * 14.0 + g.y * g.y * 9.0)) * fade;
                halo *= 0.85 + 0.15 * sin(6.2831853 * (0.9 - t * puffRate));

                return half4(fire + _HaloColor.rgb * halo * _Halo, 1);
            }
            ENDHLSL
        }
    }
}
