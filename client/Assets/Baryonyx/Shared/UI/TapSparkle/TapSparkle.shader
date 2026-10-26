// 押した位置のキラキラ（TapSparkleLayer）を、画像を使わずに計算で描くuGUI用シェーダー。
// 1つのGraphic（TapSparkleMarks）が、形ごとの小さな板を毎フレーム並べる。板ごとの値は頂点で渡す。
// - UV0：板の中の位置（設計座標の画素、形の中心が原点）。
// - UV1：x 形の番号（0 にじみ、1 点）、y 合成（1で加算、0で通常の重ね）、z 半径。
// - 頂点色：rgbで形を染め、アルファで濃さを掛ける。
// にじみは中心で1、半径の0.3で0.42、縁で0になる柔らかい光、点は縁だけをなめらかにした丸。
// 乗算済みアルファ（One OneMinusSrcAlpha）で描く。加算の形はアルファ0、通常の重ねは覆う割合をアルファに書く。
// すべての画面より手前に出すため Screen Space - Overlay のCanvasに置き、Bloomは掛からない。光のにじみは形の中で描く。
// 実行時に Shader.Find で探すため、GraphicsSettings の Always Included Shaders に入れる（TapSparkleAssets）。
Shader "Baryonyx/UI/Tap Sparkle"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

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
        // Keep the destination alpha unchanged, as the other UI materials do.
        Blend One OneMinusSrcAlpha, Zero One
        ColorMask [_ColorMask]

        Pass
        {
            Name "TapSparkle"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 shape    : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                half4 color     : COLOR;
                float2 p        : TEXCOORD0;
                float4 mask     : TEXCOORD1;
                float4 shape    : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half4 _Color;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

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
                OUT.p = v.texcoord.xy;
                OUT.shape = v.shape;
                OUT.mask = float4(
                    v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                    v.color.rgb = UIGammaToLinear(v.color.rgb);
                OUT.color = v.color;
                return OUT;
            }

            // 中心で1、半径の0.3で0.42、縁で0（ブラウザの試作の放射グラデーションに近い形）。
            float SoftGlow(float x)
            {
                x = saturate(x);
                float near = lerp(1, 0.42, x / 0.3);
                float far = 0.42 * (1 - (x - 0.3) / 0.7);
                return x < 0.3 ? near : far * far / 0.42;
            }

            half4 frag(v2f IN) : SV_Target
            {
                float r = length(IN.p);
                float radius = max(IN.shape.z, 1e-3);
                float aa = max(fwidth(r), 1e-3);
                float shape = IN.shape.x < 0.5
                    ? SoftGlow(r / radius)
                    : 1 - smoothstep(radius - aa * 0.5, radius + aa * 0.5, r);

                half fade = IN.color.a * _Color.a;
                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                fade *= m.x * m.y;
                #endif

                half cover = saturate(shape * fade);
                half additive = step(0.5, IN.shape.y);
                half4 color = half4(IN.color.rgb * _Color.rgb * cover, cover * (1 - additive));

                // 8ビットの1段に満たないノイズで、薄く広いにじみの帯を散らす。
                float dither = frac(52.9829189 * frac(dot(IN.vertex.xy, float2(0.06711056, 0.00583715)))) - 0.5;
                color.rgb = max(0, color.rgb + dither / 255.0 * saturate(max(color.r, max(color.g, color.b)) * 64));

                #ifdef UNITY_UI_ALPHACLIP
                clip(cover - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
