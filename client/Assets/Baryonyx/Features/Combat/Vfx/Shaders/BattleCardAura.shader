// 選んでいるカードの後ろに立つ白金の後光の靄と絹の光の帯を、画像を使わずに計算で描くuGUI用のシェーダー（覚醒先を選ぶ画面で使う）。
// カードより一回り大きい板に掛け、板の中央にカードの形（角の四角い長方形）を置いて、その縁からの距離で靄の濃さを決める。
// どの色の枠のカードにも合うよう、色は白に近い淡い金に限り、向きの偏りを付けずに全周へ均等に広げる。
// 靄の輪郭はゆがめたふくらみと細かな切れ端で崩し、外ほど大きく崩して四角く見せない。
// ノイズは画素ごとに計算せず、コードで一度だけ作る小さなノイズの絵（BattleCardAuraNoise）を数回読む。
// 画素ごとにfBmを40回近く計算していた前の作りは、スマホでは重すぎた（2026-10-29）。カードに隠れる内側も描かない。
// 動きは流れを持たず、形がゆっくり変わり続け、全体がその場で呼吸するように膨らんで縮む。
// 層（すべて加算。カードの奥に描き、カードの絵と説明文には掛からない）：
// - 後光（Halo）：遠くまで薄く届く琥珀色のにじみ。いちばん柔らかく、いちばん暗い。
// - 靄（Haze）：縁から外へ薄れていく淡い金の雲。輪郭がもくもくと崩れ、光の帯の居場所になる。
// - 光の帯（Ribbons）：カードのまわりを絹のようにたなびき、ゆっくり巡る細い光の筋。
//   カードの輪郭から少しずつ離した数本の線を、輪郭に沿った向きのノイズでうねらせ、巡る向きに流す。
//   画面の画素で決めた細さの白い芯と、その外のにじみで描き、別のノイズで途切れさせて先が細く消える切れ端にする。
// - 芯（Core）：縁のすぐ外の白に近い光。ノイズで太さと明るさを揺らし、まっすぐな線に見せない。
// - 光の粒（Motes）：靄の中をゆっくり昇って瞬く、まばらな点。
// 頂点色のアルファで全体を出し入れする。
Shader "Baryonyx/Combat/Battle Card Aura"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        // コードで作るノイズの絵（BattleCardAuraNoise。R・G・B：ふくらみ、A：細かな切れ端）。
        [NoScaleOffset] _Noise ("ノイズ", 2D) = "gray" {}

        _Size ("板の大きさ（px）", Vector) = (975, 1180, 0, 0)
        _CardHalf ("カードの半分の大きさ（px）", Vector) = (257, 360, 0, 0)

        _Core ("芯の色", Color) = (1, 0.98, 0.9, 1)
        _Haze ("靄の色", Color) = (1, 0.88, 0.6, 1)
        _Halo ("後光の色", Color) = (1, 0.7, 0.32, 1)
        _Intensity ("光の強さ", Range(0, 3)) = 1.1
        _CoreWidth ("芯の太さ（靄の届く距離に対する割合）", Range(0.05, 0.8)) = 0.24
        _HaloReach ("後光の届く距離（靄に対する倍率）", Range(1, 5)) = 1.5
        _HaloAmount ("後光の明るさ", Range(0, 1)) = 0.14

        _Reach ("靄の届く距離（px）", Float) = 36
        _Soft ("靄の厚み（届く距離に対する倍率）", Range(0.5, 4)) = 2.4
        _Break ("輪郭の崩れ", Range(0, 2)) = 1.9
        _Wisps ("細かな切れ端", Range(0, 1)) = 0.6
        _Warp ("ゆがみの強さ", Range(0, 3)) = 1.5
        _Scale ("ふくらみの大きさ（px）", Float) = 105
        _Evolve ("形の変わる速さ", Float) = 0.16
        _Breath ("呼吸の大きさ（距離の割合）", Range(0, 0.4)) = 0.12
        _BreathSpeed ("呼吸の速さ（rad/秒）", Float) = 1.6
        _Seed ("模様の開始位置", Vector) = (0, 0, 0, 0)

        _HazeAmount ("靄の明るさ", Range(0, 2)) = 0.45

        _RibbonCore ("光の帯の芯の色", Color) = (1, 0.97, 0.88, 1)
        _RibbonGlow ("光の帯のにじみの色", Color) = (1, 0.78, 0.4, 1)
        _Ribbon ("光の帯の明るさ", Range(0, 3)) = 1.4
        _RibbonCount ("光の帯の本数（1〜4）", Range(1, 4)) = 3
        _RibbonWidth ("光の帯の芯の太さ（画面の画素）", Float) = 2
        _RibbonBlur ("光の帯のにじみの幅（画面の画素）", Float) = 14
        _RibbonBody ("光の帯の体の幅（画面の画素）", Float) = 7
        _RibbonRound ("外の帯ほど丸くなる割合", Range(0, 1)) = 0.7
        _RibbonGap ("光の帯どうしの間（px）", Float) = 24
        _RibbonWave ("光の帯のうねりの大きさ（px）", Float) = 70
        _RibbonWaves ("ひと回りのうねりの数", Float) = 1.4
        _RibbonSpin ("光の帯の巡る速さ（rad/秒）", Float) = 0.22
        _RibbonLift ("上ほど離れる割合", Range(0, 3)) = 0.8
        _RibbonEvolve ("光の帯の形の変わる速さ", Float) = 0.15
        _RibbonBreak ("光の帯の途切れ（0で途切れない）", Range(0, 1)) = 0.75

        _Motes ("光の粒の多さ", Range(0, 1)) = 0.25
        _MoteRise ("光の粒の昇る速さ（px/秒）", Float) = 14

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        // Light is added; the destination alpha stays as the other UI materials leave it.
        Blend One One, Zero One
        ColorMask [_ColorMask]

        Pass
        {
            Name "BattleCardAura"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "../../../../Shared/VFX/HD2D/Shaders/Hd2dStageAir.hlsl"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            fixed4 _Color;
            float4 _ClipRect;
            sampler2D _Noise;
            float4 _Size;
            float4 _CardHalf;
            half4 _Core;
            half4 _Haze;
            half4 _Halo;
            half _Intensity;
            half _CoreWidth;
            half _HaloReach;
            half _HaloAmount;
            float _Reach;
            float _Soft;
            float _Break;
            float _Wisps;
            float _Warp;
            float _Scale;
            float _Evolve;
            float _Breath;
            float _BreathSpeed;
            float4 _Seed;
            half _HazeAmount;
            half4 _RibbonCore;
            half4 _RibbonGlow;
            half _Ribbon;
            float _RibbonCount;
            float _RibbonWidth;
            float _RibbonBlur;
            float _RibbonBody;
            float _RibbonRound;
            float _RibbonGap;
            float _RibbonWave;
            float _RibbonWaves;
            float _RibbonSpin;
            float _RibbonLift;
            float _RibbonEvolve;
            half _RibbonBreak;
            half _Motes;
            float _MoteRise;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.uv = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            // The noise square, read without mip levels (also inside loops).
            half4 Noise(float2 uv)
            {
                return tex2Dlod(_Noise, float4(uv, 0.0, 0.0));
            }

            // How far outside the card (px; 0 inside).
            float CardDistance(float2 p)
            {
                float2 q = abs(p) - _CardHalf.xy;
                return length(max(q, 0.0));
            }

            // Sparse points of light drifting up through the haze, each twinkling at its own pace.
            float Motes(float2 p, float t)
            {
                const float cell = 46.0;
                float2 m = (p + float2(0.0, -t * _MoteRise)) / cell + _Seed.xy;
                float2 id = floor(m);
                float h = Hd2dHash(float3(id, 11.0));
                float2 at = float2(h, Hd2dHash(float3(id, 23.0))) * 0.6 + 0.2;
                float r = length(frac(m) - at) * cell;
                float twinkle = 0.5 + 0.5 * sin(t * (1.5 + h * 2.0) + h * 40.0);
                float shown = step(1.0 - _Motes * 0.4, h);
                return shown * twinkle * twinkle * (smoothstep(2.6, 0.0, r) + 0.3 * smoothstep(8.0, 0.0, r));
            }

            // The silky ribbons: a few lines set out from the card's outline one after another,
            // waving by a noise read round the card (on a circle, so it has no seam) and carried
            // round it, each its own way. Each has a thin white core (pixels on screen, so it
            // stays thin at any size) inside a wider glow, and is torn into strands whose ends
            // fade, the strands further out fainter.
            // A rounder distance (a superellipse round the card), so the outer strands sweep in
            // ovals instead of following the card's square corners.
            float Rounded(float2 p)
            {
                float2 q = abs(p) / _CardHalf.xy;
                q *= q;
                return (sqrt(sqrt(q.x * q.x + q.y * q.y)) - 1.0) * (_CardHalf.x + _CardHalf.y) * 0.5;
            }

            void Ribbons(float2 p, float d, float rounded, float2 steps, float t, out float core, out float glow)
            {
                core = 0.0;
                glow = 0.0;
                float angle = atan2(p.y / _CardHalf.y, p.x / _CardHalf.x);
                float top = saturate(p.y / _CardHalf.y * 0.5 + 0.5);
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    if (i >= (int)_RibbonCount)
                        break;
                    float way = i % 2 == 0 ? 1.0 : -0.7;
                    float turn = angle + t * _RibbonSpin * way * (1.0 + i * 0.25);
                    // The noise is read round a circle in the noise square (no seam), the circle
                    // drifting slowly so the waves change shape.
                    float2 round = float2(cos(turn), sin(turn)) * _RibbonWaves;
                    float2 drift = t * _RibbonEvolve * float2(0.11, 0.07) + _Seed.xy * 0.1 + i * float2(0.31, 0.17);
                    float wave = (Noise(round * 0.25 + drift).g - 0.5) * 2.0 * _RibbonWave;
                    float set = _RibbonGap * (0.6 + i) * (1.0 + _RibbonLift * top * top);
                    float k = _RibbonRound * (0.4 + 0.2 * i);
                    float path = lerp(d, rounded, k);
                    float px = abs(path - set - wave) / max(lerp(steps.x, steps.y, k), 1e-3);
                    float strand = Noise(round * 0.42 - drift * 1.3 + 0.5).b;
                    float kept = lerp(1.0, smoothstep(0.42, 0.62, strand), _RibbonBreak);
                    float fade = kept / (1.0 + i * 0.6);
                    // Thickest in the middle of a strand, thinning to its ends.
                    float width = _RibbonWidth * (0.4 + 0.8 * kept);
                    core += (1.0 - smoothstep(width * 0.5, width, px)) * fade;
                    core += exp(-px * px / (_RibbonBody * _RibbonBody)) * 0.45 * fade;
                    glow += exp(-px / _RibbonBlur) * fade;
                }
            }

            half4 frag(v2f IN) : SV_Target
            {
                float t = Hd2dTime();
                // Pixels from the middle of the card, y up.
                float2 p = (IN.uv - 0.5) * _Size.xy;
                float d = CardDistance(p);
                float rounded = Rounded(p);
                // How far a screen pixel steps in each distance, for the ribbons' widths in
                // pixels (taken before any pixel stops, so the neighbours agree).
                float2 steps = float2(fwidth(d), fwidth(rounded));
                // The card covers its own inside: nothing to draw there.
                if (d <= 0.0)
                    return half4(0.0, 0.0, 0.0, 0.0);

                // Large slow puffs (warped), and finer torn wisps over them; no flow, only change:
                // two layers drifting apart, warped by a third, cross-fade as they go.
                float2 uv = p / (_Scale * 4.0) + _Seed.xy * 0.13;
                float e = t * _Evolve * 0.35;
                float2 warp = (Noise(uv * 0.7 + float2(e, -e * 0.6)).gb - 0.5) * _Warp * 0.3;
                half4 one = Noise(uv + warp + float2(e * 0.8, e * 0.3));
                half4 two = Noise(uv - warp * 0.6 + float2(-e * 0.5, e * 0.9) + 0.37);
                float mix = 0.5 + 0.5 * sin(t * _Evolve * 2.0 + _Seed.z);
                float puff = Hd2dSpread(lerp(one.r, two.r, mix), 1.25);
                float wisp = Hd2dSpread(lerp(one.a, two.a, 1.0 - mix), 1.2);

                // The whole haze breathes in and out where it is.
                float reach = _Reach * (1.0 + _Breath * sin(t * _BreathSpeed + _Seed.x));
                // Distance in reaches, pushed out and in by the puffs: more the further out, so the
                // haze hugs the card and its outline billows.
                float far = d / reach;
                float edge = far - (puff - 0.5) * _Break * (0.35 + far * 0.65)
                    - (wisp - 0.5) * _Wisps * far;
                // Full near the card, thinning to nothing _Soft reaches out along the billowing edge.
                float haze = saturate(1.0 - max(edge, 0.0) / _Soft);
                haze = haze * haze * lerp(0.75, 1.1, puff);
                float halo = exp(-d / (reach * _HaloReach)) * _HaloAmount * lerp(0.6, 1.2, puff);
                float core = exp(-d / (reach * _CoreWidth)) * lerp(0.5, 1.0, wisp);

                float ribbon;
                float ribbonGlow;
                Ribbons(p, d, rounded, steps, t, ribbon, ribbonGlow);

                half3 light = _Halo.rgb * halo + _Haze.rgb * haze * 0.7 * _HazeAmount + _Core.rgb * core * 0.8;
                light += (_RibbonGlow.rgb * ribbonGlow * 0.45 + _RibbonCore.rgb * ribbon) * _Ribbon;
                light += _Core.rgb * Motes(p, t) * saturate(haze * 1.4 + halo);
                light *= _Intensity;

                // Nothing reaches the board's own edge.
                float2 border = min(IN.uv, 1.0 - IN.uv) * _Size.xy;
                half keep = saturate(min(border.x, border.y) / 60.0);

                half a = IN.color.a * keep;
                #ifdef UNITY_UI_CLIP_RECT
                a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                half3 rgb = light * IN.color.rgb * a + Hd2dDither(IN.vertex.xy);
                return half4(max(rgb, 0.0), 0.0);
            }
        ENDCG
        }
    }
}
