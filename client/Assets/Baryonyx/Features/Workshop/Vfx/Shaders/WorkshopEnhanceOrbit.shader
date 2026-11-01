// 品質強化の演出（裏返して現れる）で、回るカードのまわりを巡る光の軌跡を、画像を使わずに計算で描くuGUI用シェーダー
// （doc/features/enhancement.md 品質強化の演出）。板に内接する楕円の上を、光の頭が反時計回りに進み、後ろに尾を引く。
// 楕円は板の座標の円なので、板を平たくすると楕円になる。線の太さは画面の画素で決め、平たくしても太さがそろう。
// カードを囲んで見せるため、楕円の奥の半分（上側）と手前の半分（下側）を、マテリアルの _Half で別の板に描き分け、
// 奥はカードの奥、手前はカードの手前に置く。2枚の板は同じ値を受け取り、境目で重みを足すと1になる。
// UI/Defaultと同じ頂点色・RectMask2D・Maskの扱いに、次を足す。
// - 1枚ずつの値（追加のUV1）：x 乱数の種、y 頭の角度（周の割合。0で右端、0.25で奥の上端、-0.25で手前の下端）、
//   z 尾の長さ（周の割合）、w 明るさの倍率（0は1とみなす）。
// - 頂点色：rgbで線を染め、アルファで全体を薄める。
// 加算（SrcAlpha One）で描く。ハッシュ・ノイズ・線の式は戦闘の形のシェーダーと共通（BattleVfxShapes.cginc）。
Shader "Baryonyx/Workshop/Enhance Orbit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1
        _Half ("Half (0 back, 1 front)", Float) = 1
        _Width ("Line Width (px at 1080)", Float) = 2.2
        _Halo ("Halo Width (px at 1080)", Float) = 9

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
        Blend SrcAlpha One, Zero One
        ColorMask [_ColorMask]

        Pass
        {
            Name "WorkshopEnhanceOrbit"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // ddx・ddyで、線の太さを画面の画素で決める。
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "Assets/Baryonyx/Features/Combat/Vfx/Shaders/BattleVfxShapes.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 params   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                half4 color     : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 mask     : TEXCOORD1;
                float4 params   : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half4 _Color;
            half _Intensity;
            half _Half;
            float _Width;
            float _Halo;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

            // 楕円の線の中心の半径（板の座標）。外側のにじみが板からはみ出さないよう、少し内側に置く。
            static const float Radius = 0.86;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.vertex = vPosition;

                float2 pixelSize = vPosition.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                OUT.texcoord = v.texcoord.xy;
                OUT.params = v.params;
                OUT.mask = float4(
                    v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                    v.color.rgb = UIGammaToLinear(v.color.rgb);
                OUT.color = v.color;
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                float2 p = IN.texcoord * 2 - 1;
                float seed = IN.params.x;
                float head = IN.params.y;
                float tailLength = max(IN.params.z, 1e-3);
                float bright = IN.params.w > 0 ? IN.params.w : 1;
                half3 tint = IN.color.rgb * _Color.rgb;

                // 線からの距離を画面の画素に直す（陰関数を、その画面上の傾きの大きさで割る）。
                float f = length(p) - Radius;
                float2 grad = float2(ddx(f), ddy(f));
                float dist = abs(f) / max(length(grad), 1e-5);
                // 太さは1080の高さのときの画素で決め、画面の大きさに合わせる。芯は1画素より細くしない。
                float scale = _ScreenParams.y / 1080.0;
                float width = max(_Width * scale, 1.0);
                float halo = _Halo * scale;

                // 頭からの後ろへの離れ（周の割合。0で頭、増えるほど後ろ）。頭は反時計回りに進む。
                float theta = atan2(p.y, p.x) / Tau;
                float behind = frac(head - theta);
                float ahead = 1 - behind;
                float tail = behind < tailLength ? pow(1 - behind / tailLength, 1.6) : 0;
                // 尾は先へ行くほど、ところどころ薄れる。
                float n = Noise(float3(behind * 26.0, seed * 7.0, seed * 3.0));
                tail *= lerp(1, smoothstep(0.2, 0.6, n), saturate(behind / tailLength) * 0.7);
                // 頭は尾より太く白く光り、進む先へわずかににじむ。
                float near = min(behind, ahead) * Tau * Radius;
                float hot = Gauss(near, 0.05) + Gauss(ahead * Tau * Radius, 0.025) * 0.5;

                float body = Gauss(dist, width) * tail;
                float glow = Gauss(dist, halo) * (tail * 0.35 + hot * 0.6);
                float core = Gauss(dist, width * 0.6) * hot;
                half3 light = tint * (body * 0.9 + glow) + lerp(tint, 1, 0.85) * core * 1.8;

                // 奥の半分（上側）と手前の半分（下側）を描き分ける。境目で2枚の重みを足すと1になる。
                float back = smoothstep(-0.04, 0.04, p.y);
                light *= _Half < 0.5 ? back : 1 - back;
                light *= _Intensity * bright;

                half fade = IN.color.a * _Color.a;
                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                fade *= m.x * m.y;
                #endif

                half4 color = half4(light, fade);
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
        ENDCG
        }
    }
}
