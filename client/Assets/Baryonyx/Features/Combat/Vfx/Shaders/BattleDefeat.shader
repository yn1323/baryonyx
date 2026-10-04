// 撃破した敵の絵を、ドットごとに削って消すuGUI用シェーダー。
// 絵は UiPixelArt と同じく PixelArtSampling.cginc の標本化で描き、ドットが消える順番を _OrderTex（絵と同じ大きさ、
// rに0〜1）から読む。順番はC#で、当たった側と上から、まだらに進むように決める（BattleSkillVfx.Defeat.cs）。
// 1枚ずつの値は追加のUV1で受け取る（BattleVfxImage）。
// - x：乱数の種（使わない）
// - y：冷め具合。0で白熱した光の塊、1で元の色。
// - z：削れた割合。0で欠けていない、1で全部消えた。削れの先頭から _Band の幅のドットは白熱して光る。
// - w：白熱した部分の明るさの倍率（0は1とみなす）。_Intensity と掛けて1を超え、HDRのBloomでにじむ。
// 消えたドットは、C#が同じ順番で同じ位置から粒（BattleVfxShape.Mote）として飛ばす。
Shader "Baryonyx/Combat/Battle Defeat"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [NoScaleOffset] _OrderTex ("Order", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _HotColor ("Hot Color", Color) = (1, 0.95, 0.86, 1)
        _Band ("Edge Band", Range(0.01, 0.5)) = 0.12
        _Intensity ("Intensity", Float) = 1.6

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
        // Keep the destination alpha unchanged, as the other effects do.
        Blend SrcAlpha OneMinusSrcAlpha, Zero One
        ColorMask [_ColorMask]

        Pass
        {
            Name "BattleDefeat"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // fwidth in the pixel-art sampling.
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "../../UI/Shaders/PixelArtSampling.cginc"

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

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _OrderTex;
            half4 _Color;
            half4 _HotColor;
            float _Band;
            half _Intensity;
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
                half4 picture = SamplePixelArt(_MainTex, IN.texcoord, _MainTex_TexelSize);
                // Each dot goes as a whole, at its turn in the order.
                float2 dotUv = (floor(IN.texcoord * _MainTex_TexelSize.zw) + 0.5) * _MainTex_TexelSize.xy;
                float order = tex2Dlod(_OrderTex, float4(dotUv, 0, 0)).r;

                float cool = saturate(IN.params.y);
                float eaten = saturate(IN.params.z);
                // The front runs a band past the last dot, so every dot is gone at 1.
                float front = eaten * (1 + _Band);
                float kept = step(front, order);
                float edge = eaten > 0 ? 1 - saturate((order - front) / _Band) : 0;
                float heat = max(1 - cool, edge);

                float bright = IN.params.w > 0 ? IN.params.w : 1;
                half3 hot = _HotColor.rgb * IN.color.rgb * (_Intensity * bright);
                half4 color = half4(
                    lerp(picture.rgb, hot, heat),
                    picture.a * kept * IN.color.a * _Color.a);

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                color.a *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
