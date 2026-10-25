// 木々や雲のすき間・天井の割れ目・窓から差す光の柱（光芒、ゴッドレイ）。画像を使わず計算で描き、加算で足す。
// 現実の光芒は、光の通り道に浮かぶ塵や水滴が光を散らして見える。塵の濃い所ほど明るく、塵は風でゆっくり流れる。
// そのため、光の柱を役割の違う層に分けて重ねる。
// - にじみ（ハロー）：帯の外へ広く薄く散った光。帯の幅とは別の幅で、根元へ行くほど少し広がる。
// - 帯（本体）：柔らかい縁の光の帯。中を、2回ゆがめた5段のfBmで作った塵の濃淡が、風の向きにゆっくり流れる。
// - 筋：葉や雲の縁で分かれた細い光の筋。光の向きに沿って伸び、ゆっくり形を変える。
// - 塵の粒：光の中だけで光る小さな粒。粒ごとに位相をずらして、向きが合った瞬間だけきらりと光る。
// 光が地面に落ちた所は、別の板の光だまり（Baryonyx/HD2D/Light Pool）で見せる。
// 板は光の軸のまわりでカメラへ向き、帯は板の中央の _Band の幅に収まる（残りはにじみ）。
// 薄く広い光の階調の段差は、画素ごとのごく弱い揺らぎで散らす。霧の中では消える。
Shader "Baryonyx/HD2D/Light Beam"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.92, 0.7, 1)
        _Intensity ("Intensity", Range(0, 4)) = 0.6
        _Band ("Band Width (of the board)", Range(0.2, 1)) = 0.55
        _EdgeSoftness ("Band Edge Softness", Range(0.05, 1)) = 0.6
        _Halo ("Halo", Range(0, 1)) = 0.35
        _Density ("Dust Density Contrast", Range(0, 1)) = 0.6
        _DensityScale ("Dust Cloud Size (1/m)", Range(0.1, 4)) = 0.7
        _StripeScale ("Streak Scale (1/m)", Range(0, 12)) = 3
        _StripeAmount ("Streak Amount", Range(0, 1)) = 0.45
        _Speed ("Drift Speed (m/s)", Range(0, 2)) = 0.12
        _Dust ("Dust Specks", Range(0, 1)) = 0.35
        _DustBrightness ("Dust Speck Brightness", Range(0, 8)) = 3
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Hd2dStageAir.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _Intensity;
            half _Band;
            half _EdgeSoftness;
            half _Halo;
            half _Density;
            half _DensityScale;
            half _StripeScale;
            half _StripeAmount;
            half _Speed;
            half _Dust;
            half _DustBrightness;
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
                // x：板の中央からの横（m）、y：根元からの長さ（m）、z：板の幅（m）、w：板の長さ（m）
                float4 meters : TEXCOORD1;
                float seed : TEXCOORD2;
                float facing : TEXCOORD3;
                half fog : TEXCOORD4;
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
                float beamLength = max(length(axis), 0.01);
                output.meters = float4((input.uv.x - 0.5) * width, input.uv.y * beamLength, width, beamLength);
                // 置いた位置から種を決め、同じマテリアルの光芒どうしが同じ模様にならないようにする。
                // 種は小さな範囲のずらしにする（大きな値を座標へ足すと、細かな段で精度が足りず模様が崩れる）。
                output.seed = frac(_Seed * 0.137 + dot(center, float3(0.131, 0.717, 0.373))) * 8.0;
                // Looking along the beam, it would pile up into a bright blot; fade it there.
                output.facing = saturate(acrossLength * 1.4);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // 光の中を漂う塵の粒。格子のセルごとに1粒まで置き、粒の大きさとずれはセルの4分の1に収める（セルの境で欠けない）。
            float DustSpecks(float2 m, float t, float seed, float pixel)
            {
                const float cell = 0.24;
                // 塵は風でゆっくり横へ流れ、わずかに沈む。
                float2 drift = float2(t * _Speed * 0.6, -t * 0.03);
                float2 g = (m - drift) / cell + seed * 13.0;
                float2 id = floor(g);
                float2 f = g - id;
                float h = Hd2dHash(float3(id, seed));
                if (h > _Dust)
                    return 0.0;
                float2 at = 0.5 + (float2(Hd2dHash(float3(id, seed + 1.7)), Hd2dHash(float3(id, seed + 3.1))) - 0.5) * 0.5;
                // 画面で1画素より細くならないよう、半径の最小を画素の大きさから決める。
                float radius = max(0.035 / cell, pixel * 0.9 / cell);
                float d = length(f - at) / radius;
                float speck = exp(-d * d * 2.0);
                // 小さな薄片が回り、光を返す向きになった瞬間だけ強く光る。粒ごとに位相と速さをずらす。
                float phase = Hd2dHash(float3(id, seed + 5.3));
                float spin = 0.5 + 0.5 * sin(t * (0.9 + 1.6 * phase) + phase * 6.2832);
                float glint = 0.1 + 0.9 * spin * spin * spin * spin;
                return speck * glint;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = Hd2dTime();
                float2 uv = input.uv;
                float2 m = input.meters.xy;
                float v = uv.y;

                // 帯の横の位置：帯の縁が±1。にじみは板の縁（±1/_Band）まで広がる。
                float x = (uv.x * 2.0 - 1.0) / _Band;
                float ax = abs(x);
                // 帯：中央は平らで、縁は柔らかく消える。
                float band = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, ax);
                band *= band;
                // にじみ：帯の外まで広く薄い光。根元（地面に近い側）ほど散った光が少し広がる。
                float spread = lerp(0.75, 1.15, 1.0 - v);
                float haloEdge = 1.0 / _Band;
                float halo = exp(-ax * ax / (2.0 * spread * spread)) * (1.0 - smoothstep(haloEdge * 0.7, haloEdge, ax));

                // 上（光の入る所）の少し下で最も明るく、地面に届く前に消える。
                float ends = smoothstep(0.0, 0.42, v) * (1.0 - smoothstep(0.78, 1.0, v));

                // 塵の濃淡：風で流れる、2回ゆがめた5段のfBm。光の柱に沿って少し長く伸ばす。
                float3 p = float3(m.x * _DensityScale - t * _Speed * _DensityScale,
                                  m.y * _DensityScale * 0.45,
                                  t * 0.035) + input.seed;
                float cloud = Hd2dSpread(Hd2dWarpedFbm(p, 1.6), 2.2);
                float density = lerp(1.0, 0.25 + 1.5 * cloud, _Density);

                // 筋：光の向きに沿って伸びた細い明暗。横は細かく、縦はほとんど変わらない。
                float3 s = float3(m.x * _StripeScale, m.y * 0.05, t * 0.04 + input.seed);
                float streak = Hd2dSpread(Hd2dFbm(s, 3), 2.0);
                float streaks = lerp(1.0, 0.35 + 1.3 * streak, _StripeAmount);

                // 前の版（縞で揺れる一様な帯）と、光の柱1本あたりの明るさの平均をそろえる係数。
                const float level = 0.62;
                float body = band * density * streaks;
                float glow = (body + halo * _Halo * lerp(0.7, 1.3, cloud)) * ends * level;

                // 塵の粒は帯の中でだけ光る（光の当たらない所の塵は見えない）。
                float pixel = max(fwidth(m.y), fwidth(m.x));
                float specks = DustSpecks(m, t, input.seed, pixel) * band * ends * _DustBrightness;

                half3 color = _Color.rgb * _Intensity * (glow + specks) * input.facing;
                // 薄い光の階調の段差を散らす。光のない所は暗くしない。
                color += Hd2dDither(input.positionCS.xy) * saturate(glow * 40.0);
                color = max(color, 0.0);
                // Additive light fades into the fog instead of taking its colour.
                color = MixFogColor(color, half3(0, 0, 0), input.fog);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
