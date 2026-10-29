// 選んでいるカードの後ろに立つ白金の後光の靄を、画像を使わずに計算で描くuGUI用のシェーダー（覚醒先を選ぶ画面で使う）。
// カードより一回り大きい板に掛け、板の中央にカードの形（角の四角い長方形）を置いて、その縁からの距離で靄の濃さを決める。
// どの色の枠のカードにも合うよう、色は白に近い淡い金に限り、向きの偏りを付けずに全周へ均等に広げる。
// 靄の輪郭は2回ゆがめたfBm（Hd2dWarpedFbm）のふくらみと細かな切れ端で崩し、外ほど大きく崩して四角く見せない。
// 動きは流れを持たず、形がゆっくり変わり続け、全体がその場で呼吸するように膨らんで縮む。
// 層（すべて加算。カードの奥に描き、カードの絵と説明文には掛からない）：
// - 後光（Halo）：遠くまで薄く届く琥珀色のにじみ。いちばん柔らかく、いちばん暗い。
// - 靄（Haze）：縁から外へ薄れていく淡い金の雲。輪郭がもくもくと崩れる主役。
// - 芯（Core）：縁のすぐ外の白に近い光。ノイズで太さと明るさを揺らし、まっすぐな線に見せない。
// - 光の粒（Motes）：靄の中をゆっくり昇って瞬く、まばらな点。
// 頂点色のアルファで全体を出し入れする。
Shader "Baryonyx/Combat/Battle Card Aura"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

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

            half4 frag(v2f IN) : SV_Target
            {
                float t = Hd2dTime();
                // Pixels from the middle of the card, y up.
                float2 p = (IN.uv - 0.5) * _Size.xy;
                float d = CardDistance(p);

                // Large slow puffs (warped), and finer torn wisps over them; no flow, only change.
                float3 at = float3(p / _Scale, t * _Evolve) + _Seed.xyz;
                float puff = Hd2dSpread(Hd2dWarpedFbm(at, _Warp), 1.5);
                float wisp = Hd2dSpread(Hd2dFbm(at * float3(2.4, 2.4, 1.7) + 9.1, 3), 1.6);

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

                half3 light = _Halo.rgb * halo + _Haze.rgb * haze * 0.7 + _Core.rgb * core * 0.8;
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
