// 弱点のアイコンが開示される瞬間の光（キラーン）を、アイコンの上に計算で描くuGUI用シェーダー。
// アイコンと同じ絵・同じ大きさの板に掛け、絵からは不透明度だけを読む。光はアイコンの形の内側にだけ出る。
// 光の帯は右上がりの斜めの縞で、アイコンのドットの格子にそろえ、ドット単位で左から右へ進む（ドット絵の光沢に見える）。
// 1枚ずつの値（追加のUV1。BattleVfxImage.Shapeで渡す）。Canvasの追加のシェーダーチャンネルにTexCoord1が要る。
// - x 全体の光（0〜1）：アイコン全体を白く光らせる割合。
// - y 光の帯の進み具合（0で左の外、1で右の外）。
// - w 明るさの倍率（0は1とみなす）。1を超える明るさにして、HDRのBloomで光だけをにじませる。
// 頂点色のrgbで光を染め、アルファで全体を薄める。合成は加算。
Shader "Baryonyx/Combat/Battle Weakness Glint"
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
        // Light is added; the destination alpha stays as the other UI materials leave it.
        Blend SrcAlpha One, Zero One
        ColorMask [_ColorMask]

        Pass
        {
            Name "BattleWeaknessGlint"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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
            float4 _MainTex_ST;
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
                OUT.texcoord = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
                OUT.params = v.params;
                OUT.mask = float4(
                    v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                    v.color.rgb = UIGammaToLinear(v.color.rgb);
                OUT.color = v.color;
                return OUT;
            }

            // The band of light at a dot: a wide stripe with a hot middle, and a thin one trailing
            // after it, the way a glint runs over a polished face. d is the dot's distance across
            // the diagonal from the band's front (in the icon's 0 to 1 space).
            float Band(float d)
            {
                float core = step(abs(d), 0.05);
                float edge = step(abs(d), 0.095) * 0.45;
                float trail = step(abs(d + 0.19), 0.03) * 0.55;
                return max(core, max(edge, trail));
            }

            half4 frag(v2f IN) : SV_Target
            {
                // Only the icon's shape; its edge is smoothed as the icon's own is.
                half cover = SamplePixelArt(_MainTex, IN.texcoord, _MainTex_TexelSize).a;

                // The middle of the dot this pixel is in, so the band steps a whole dot at a time.
                float2 cell = (floor(IN.texcoord * _MainTex_TexelSize.zw) + 0.5) * _MainTex_TexelSize.xy;
                // Across a steep stripe leaning right (/): 0 at the left edge, 1 at the right.
                float across = (cell.x - cell.y * 0.5 + 0.5) / 1.5;
                float front = lerp(-0.3, 1.3, saturate(IN.params.y));
                float band = IN.params.y > 0 ? Band(across - front) : 0;

                float fill = saturate(IN.params.x);
                float light = fill + band * (1 - fill);
                float bright = IN.params.w > 0 ? IN.params.w : 1;
                half4 color = half4(
                    IN.color.rgb * _Color.rgb * (light * bright),
                    cover * IN.color.a * _Color.a);

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
