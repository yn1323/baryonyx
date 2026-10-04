// 戦闘のエフェクトを、画像を使わずに計算で描くuGUI用シェーダー。
// 形はマテリアルのキーワード（_Shape）で選び、式は BattleVfxShapes.cginc に置く。
// UI/Defaultと同じ頂点色・RectMask2D・Maskの扱いに、次を足す。
// - 1枚ずつの値（追加のUV1）：x 乱数の種、y 進み具合、z 欠けた割合（0で欠けていない）、w 明るさの倍率（0は1とみなす）。
//   Canvasの追加のシェーダーチャンネルにTexCoord1がないと0になり、欠けていない・倍率1の形として描く。
// - 頂点色：rgbで形を染め、アルファで全体を薄める。
// - 明るさの倍率（_Intensity）：1を超える明るさにして、HDRのBloomで光だけをにじませる。
// - 合成（_SrcBlend・_DstBlend）：光は加算（SrcAlpha One）、煙や氷は通常合成（SrcAlpha OneMinusSrcAlpha）。
Shader "Baryonyx/Combat/Battle Vfx Shape"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1
        [KeywordEnum(Glow, Ring, Star, SlashArc, SlashCross, Fireball, IceSpear, Lightning, Pillar, Dome, MagicCircle, Streak, Spark, Sparkle, Shard, Flame, FlameTongue, Smoke, Scorch, Frost, IceSpike, Chip, CutInStreaks, Beam, Arrow, Crack, Tornado, Bubble, Leaf, Chevron, Snowflake, Crest, Reticle, Mote)] _Shape ("Shape", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1

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
        Blend [_SrcBlend] [_DstBlend], Zero One
        ColorMask [_ColorMask]

        Pass
        {
            Name "BattleVfxShape"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // fwidth keeps the thin lines at least a pixel wide while a shape is still small.
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "BattleVfxShapes.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma shader_feature_local _SHAPE_GLOW _SHAPE_RING _SHAPE_STAR _SHAPE_SLASHARC _SHAPE_SLASHCROSS _SHAPE_FIREBALL _SHAPE_ICESPEAR _SHAPE_LIGHTNING _SHAPE_PILLAR _SHAPE_DOME _SHAPE_MAGICCIRCLE _SHAPE_STREAK _SHAPE_SPARK _SHAPE_SPARKLE _SHAPE_SHARD _SHAPE_FLAME _SHAPE_FLAMETONGUE _SHAPE_SMOKE _SHAPE_SCORCH _SHAPE_FROST _SHAPE_ICESPIKE _SHAPE_CHIP _SHAPE_CUTINSTREAKS _SHAPE_BEAM _SHAPE_ARROW _SHAPE_CRACK _SHAPE_TORNADO _SHAPE_BUBBLE _SHAPE_LEAF _SHAPE_CHEVRON _SHAPE_SNOWFLAKE _SHAPE_CREST _SHAPE_RETICLE _SHAPE_MOTE

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
                ShapeIn s;
                s.uv = IN.texcoord;
                s.p = IN.texcoord * 2 - 1;
                s.r = length(s.p);
                s.dir = s.p / max(s.r, 1e-4);
                s.aa = fwidth(s.r);
                s.seed = IN.params.x;
                s.k = IN.params.y;
                s.rem = saturate(1 - IN.params.z);
                s.tint = IN.color.rgb * _Color.rgb;

                half4 shape;
                #if defined(_SHAPE_RING)
                shape = ShapeRing(s);
                #elif defined(_SHAPE_STAR)
                shape = ShapeStar(s);
                #elif defined(_SHAPE_SLASHARC)
                shape = ShapeSlashArc(s);
                #elif defined(_SHAPE_SLASHCROSS)
                shape = ShapeSlashCross(s);
                #elif defined(_SHAPE_FIREBALL)
                shape = ShapeFireball(s);
                #elif defined(_SHAPE_ICESPEAR)
                shape = ShapeIceSpear(s);
                #elif defined(_SHAPE_LIGHTNING)
                shape = ShapeLightning(s);
                #elif defined(_SHAPE_PILLAR)
                shape = ShapePillar(s);
                #elif defined(_SHAPE_DOME)
                shape = ShapeDome(s);
                #elif defined(_SHAPE_MAGICCIRCLE)
                shape = ShapeMagicCircle(s);
                #elif defined(_SHAPE_STREAK)
                shape = ShapeStreak(s);
                #elif defined(_SHAPE_SPARK)
                shape = ShapeSpark(s);
                #elif defined(_SHAPE_SPARKLE)
                shape = ShapeSparkle(s);
                #elif defined(_SHAPE_SHARD)
                shape = ShapeShard(s);
                #elif defined(_SHAPE_FLAME)
                shape = ShapeFlame(s);
                #elif defined(_SHAPE_FLAMETONGUE)
                shape = ShapeFlameTongue(s);
                #elif defined(_SHAPE_SMOKE)
                shape = ShapeSmoke(s);
                #elif defined(_SHAPE_SCORCH)
                shape = ShapeScorch(s);
                #elif defined(_SHAPE_FROST)
                shape = ShapeFrost(s);
                #elif defined(_SHAPE_ICESPIKE)
                shape = ShapeIceSpike(s);
                #elif defined(_SHAPE_CHIP)
                shape = ShapeChip(s);
                #elif defined(_SHAPE_CUTINSTREAKS)
                shape = ShapeCutInStreaks(s);
                #elif defined(_SHAPE_BEAM)
                shape = ShapeBeam(s);
                #elif defined(_SHAPE_ARROW)
                shape = ShapeArrow(s);
                #elif defined(_SHAPE_CRACK)
                shape = ShapeCrack(s);
                #elif defined(_SHAPE_TORNADO)
                shape = ShapeTornado(s);
                #elif defined(_SHAPE_BUBBLE)
                shape = ShapeBubble(s);
                #elif defined(_SHAPE_LEAF)
                shape = ShapeLeaf(s);
                #elif defined(_SHAPE_CHEVRON)
                shape = ShapeChevron(s);
                #elif defined(_SHAPE_SNOWFLAKE)
                shape = ShapeSnowflake(s);
                #elif defined(_SHAPE_CREST)
                shape = ShapeCrest(s);
                #elif defined(_SHAPE_RETICLE)
                shape = ShapeReticle(s);
                #elif defined(_SHAPE_MOTE)
                shape = ShapeMote(s);
                #else
                shape = ShapeGlow(s);
                #endif

                float bright = IN.params.w > 0 ? IN.params.w : 1;
                half4 color = half4(shape.rgb * (_Intensity * bright), shape.a * IN.color.a * _Color.a);

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
