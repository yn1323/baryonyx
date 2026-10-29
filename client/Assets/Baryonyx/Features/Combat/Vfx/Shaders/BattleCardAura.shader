// 選んでいるカードの後ろに立つ「光の霞の後光」を、計算で描くuGUI用のシェーダー（覚醒先を選ぶ画面で使う）。
// 2026-10-29、ユーザーがCodexの5案から「光の霞の後光」を選んだ。線・筋・輪は描かず、霞の面だけで見せる。
// カードより大きい板に掛け、板の中央にカードの形（角の四角い長方形）を置く。カードの真後ろに、カードの
// 約2倍の楕円の霞を置き、カードに近いほど明るい白金、外へ行くほど琥珀色に沈め、縁はちぎれた霧のように
// ほどけて背景へ溶ける。どの色の枠のカードにも合うよう、色は白金から琥珀の中立な暖色に限る。
// 動きは流れを持たず、霞の形がゆっくり変わり続け、全体がその場で呼吸するように膨らんで縮む。
// 層（すべて加算。カードの奥に描き、カードの絵と説明文には掛からない）：
// - 霞（Haze）：楕円の霞。中心ほど明るく、白金から金、琥珀へと色を変えながら外へ薄れる。
// - 縁のにじみ（Rim）：カードの縁のすぐ外の、白金のやわらかいにじみ。線に見えないよう、幅と明るさを揺らす。
// - 外のにじみ（Spill）：霞の縁の外へ、琥珀色がわずかに漏れて背景に溶ける。
// ノイズは画素ごとに計算せず、コードで一度だけ作る小さなノイズの絵（BattleCardAuraNoise）を数回読む。
// カードに隠れる内側は描かない（スマホで重くならないように。2026-10-29）。頂点色のアルファで全体を出し入れする。
Shader "Baryonyx/Combat/Battle Card Aura"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        // コードで作るノイズの絵（BattleCardAuraNoise。R・G・B：ふくらみ、A：細かな切れ端）。
        [NoScaleOffset] _Noise ("ノイズ", 2D) = "gray" {}

        _Size ("板の大きさ（px）", Vector) = (1274, 1480, 0, 0)
        _CardHalf ("カードの半分の大きさ（px）", Vector) = (257, 360, 0, 0)

        _GlowCore ("カードに近い霞の色（白金）", Color) = (1, 0.92, 0.7, 1)
        _GlowMid ("中ほどの霞の色（金）", Color) = (1, 0.72, 0.3, 1)
        _GlowOuter ("外の霞の色（琥珀）", Color) = (0.9, 0.42, 0.12, 1)
        _GlowIntensity ("光の強さ", Range(0, 3)) = 1.5
        _GlowRadius ("楕円の半径（カードの半分の大きさに対する倍率）", Vector) = (1.75, 1.38, 0, 0)
        _GlowFalloff ("霞の薄れ方（大きいほど中心に寄る）", Range(0.5, 4)) = 1.7
        _GlowTorn ("縁のちぎれ", Range(0, 1.5)) = 0.45
        _GlowFine ("細かな切れ端", Range(0, 1)) = 0.15
        _GlowWarp ("ゆがみの強さ", Range(0, 3)) = 1.4
        _GlowScale ("ふくらみの大きさ（px）", Float) = 230
        _GlowEvolve ("形の変わる速さ", Float) = 0.16
        _GlowBreath ("呼吸の大きさ（半径の割合）", Range(0, 0.3)) = 0.06
        _GlowBreathSpeed ("呼吸の速さ（rad/秒）", Float) = 1.4
        _RimLight ("縁のにじみの明るさ", Range(0, 2)) = 0.55
        _RimReach ("縁のにじみの届く距離（px）", Float) = 26
        _Spill ("外のにじみの明るさ", Range(0, 1)) = 0.12
        _Seed ("模様の開始位置", Vector) = (0, 0, 0, 0)

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
            half4 _GlowCore;
            half4 _GlowMid;
            half4 _GlowOuter;
            half _GlowIntensity;
            float4 _GlowRadius;
            float _GlowFalloff;
            float _GlowTorn;
            float _GlowFine;
            float _GlowWarp;
            float _GlowScale;
            float _GlowEvolve;
            float _GlowBreath;
            float _GlowBreathSpeed;
            half _RimLight;
            float _RimReach;
            half _Spill;
            float4 _Seed;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.uv = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            // The noise square, read without mip levels.
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

            half4 frag(v2f IN) : SV_Target
            {
                float t = Hd2dTime();
                // Pixels from the middle of the card, y up.
                float2 p = (IN.uv - 0.5) * _Size.xy;
                float d = CardDistance(p);
                // The card covers its own inside: nothing to draw there.
                if (d <= 0.0)
                    return half4(0.0, 0.0, 0.0, 0.0);

                // Large slow puffs (warped), and finer torn wisps; no flow, only change: two layers
                // drifting apart, warped by a third, cross-fade as they go.
                float2 uv = p / (_GlowScale * 4.0) + _Seed.xy * 0.13;
                float e = t * _GlowEvolve * 0.35;
                float2 warp = (Noise(uv * 0.7 + float2(e, -e * 0.6)).gb - 0.5) * _GlowWarp * 0.3;
                half4 one = Noise(uv + warp + float2(e * 0.8, e * 0.3));
                half4 two = Noise(uv - warp * 0.6 + float2(-e * 0.5, e * 0.9) + 0.37);
                float mix = 0.5 + 0.5 * sin(t * _GlowEvolve * 2.0 + _Seed.z);
                float puff = Hd2dSpread(lerp(one.r, two.r, mix), 1.25);
                float wisp = Hd2dSpread(lerp(one.a, two.a, 1.0 - mix), 1.2);

                // The oval, breathing where it is: 1 on its rim, which the puffs push out and in,
                // more the further out, so the haze hugs the card and its rim comes apart like mist.
                float breath = 1.0 + _GlowBreath * sin(t * _GlowBreathSpeed + _Seed.x);
                float2 radii = _CardHalf.xy * _GlowRadius.xy * breath;
                float r = length(p / radii);
                float outer = saturate(r * r);
                float edge = r - (puff - 0.5) * _GlowTorn * (0.15 + outer)
                    - (wisp - 0.5) * _GlowFine * outer;
                float body = saturate(1.0 - edge);
                float haze = pow(body, _GlowFalloff);
                // Platinum near the card, gold in the middle, amber where it thins out.
                half3 color = lerp(_GlowOuter.rgb, _GlowMid.rgb, smoothstep(0.0, 0.45, body));
                color = lerp(color, _GlowCore.rgb, smoothstep(0.4, 0.85, body));

                // A soft platinum bloom just off the card's edge, its width and light shifting with
                // the wisps so it never reads as a line; and amber spilling past the oval's rim.
                float rim = exp(-d / (_RimReach * lerp(0.7, 1.4, wisp))) * lerp(0.55, 1.0, puff);
                float spill = exp(-max(edge - 1.0, 0.0) * 9.0) * smoothstep(0.8, 1.0, edge)
                    * lerp(0.4, 1.0, puff);

                half3 light = color * haze + _GlowCore.rgb * rim * _RimLight
                    + _GlowOuter.rgb * spill * _Spill;
                light *= _GlowIntensity;

                // Nothing reaches the board's own edge.
                float2 border = min(IN.uv, 1.0 - IN.uv) * _Size.xy;
                half keep = saturate(min(border.x, border.y) / 80.0);

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
