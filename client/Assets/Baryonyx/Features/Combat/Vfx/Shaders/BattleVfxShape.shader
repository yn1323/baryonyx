// 戦闘のエフェクトを、画像を使わずに計算で描くuGUI用シェーダー。
// 形はマテリアルのキーワード（_Shape）で選び、式は BattleVfxShapes.cginc に置く。
// UI/Defaultと同じ頂点色・RectMask2D・Maskの扱いに、次を足す。
// - 1枚ずつの値（追加のUV1）：x 乱数の種、y 進み具合、z 欠けた割合（0で欠けていない）、w 明るさの倍率（0は1とみなす）。
//   Canvasの追加のシェーダーチャンネルにTexCoord1がないと0になり、欠けていない・倍率1の形として描く。
// - 頂点色：rgbで形を染め、アルファで全体を薄める。
// - 明るさの倍率（_Intensity）：1を超える明るさにして、HDRのBloomで光だけをにじませる。
// - 合成（_SrcBlend・_DstBlend）：光は加算（SrcAlpha One）、煙や氷は通常合成（SrcAlpha OneMinusSrcAlpha）。
// - 作り込んだ見た目（_Rich）：1にすると、現実の見え方で描く。氷の槍・棘・破片を透ける氷、霜を床に広がる氷の板、
//   煙を陰影のある煙・土ぼこり・湯気、亀裂を影で見える割れ目、竜巻を風で流れる雪、
//   斬撃・十字の斬撃を細い筋と短い残像、光線を塵で見える光の柱として描く。
//   炎の塊・炎の舌・火球を冷えた先端の煤の覆いを持つ炎、光の柱を塵で見える光の柱（2で火柱）、
//   ドームと泡を結界の膜として描く。
//   稲妻は値で層を選ぶ（1 芯、2 通り道と枝・先駆放電、3 にじみ、4 残像）。
//   斬撃の弧と光線は2以上で層だけを描く（2 にじみ、3 芯、4 残像）。亀裂は2で踏まれて割れる床、3で割れるガラス。
//   煙は2以上で雷雲の層になる（2 盛り上がり、3 暗い本体、4 ちぎれ雲、5 中の稲光の光）。
//   雷雲の層は明るさの倍率を全体に掛けず、中の稲光の強さとして使う。
// - どの形も板の端の手前で0にし、光と覆いがある所だけに8ビットの1段に満たないノイズ（ディザ）を足して、
//   広く薄いにじみの帯（バンディング）を散らす。
//   乗算済みアルファ（One OneMinusSrcAlpha）用で、アルファで奥を暗くするか覆い、色で光を足す。
//   マテリアルの複製に、合成と一緒に設定する。
Shader "Baryonyx/Combat/Battle Vfx Shape"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1
        _Rich ("Rich Look (premultiplied)", Float) = 0
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
            half _Rich;
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
                float bright = IN.params.w > 0 ? IN.params.w : 1;
                s.bright = bright;
                s.tint = IN.color.rgb * _Color.rgb;

                half4 shape;
                #if defined(_SHAPE_RING)
                shape = ShapeRing(s);
                #elif defined(_SHAPE_STAR)
                shape = ShapeStar(s);
                #elif defined(_SHAPE_SLASHARC)
                // A sword arc's layers on boards of their own (2 halo, 3 core, 4 afterimage).
                if (_Rich > 1.5)
                    shape = half4(SlashArcLayer(s, _Rich), 0);
                else if (_Rich > 0.5)
                    shape = ShapeRichSlashArc(s);
                else
                    shape = ShapeSlashArc(s);
                #elif defined(_SHAPE_SLASHCROSS)
                if (_Rich > 0.5)
                    shape = ShapeRichSlashCross(s);
                else
                    shape = ShapeSlashCross(s);
                #elif defined(_SHAPE_FIREBALL)
                if (_Rich > 0.5)
                    shape = FlameRich(FireballLook(s));
                else
                    shape = ShapeFireball(s);
                #elif defined(_SHAPE_ICESPEAR)
                if (_Rich > 0.5)
                    shape = ShapeClearIceSpear(s);
                else
                    shape = ShapeIceSpear(s);
                #elif defined(_SHAPE_LIGHTNING)
                if (_Rich > 0.5)
                    shape = ShapeRichLightning(s, _Rich);
                else
                    shape = ShapeLightning(s);
                #elif defined(_SHAPE_PILLAR)
                if (_Rich > 1.5)
                    shape = ShapeFirePillar(s);
                else if (_Rich > 0.5)
                    shape = ShapeDustPillar(s);
                else
                    shape = ShapePillar(s);
                #elif defined(_SHAPE_DOME)
                if (_Rich > 0.5)
                    shape = ShapeRichDome(s);
                else
                    shape = ShapeDome(s);
                #elif defined(_SHAPE_MAGICCIRCLE)
                // The layered circle's parts, each on a board of its own (BattleSkillVfx.HotCircle).
                if (_Rich > 3.5)
                    shape = ShapeCircleVeil(s);
                else if (_Rich > 2.5)
                    shape = ShapeCircleInner(s);
                else if (_Rich > 1.5)
                    shape = ShapeCircleGlyphs(s);
                else if (_Rich > 0.5)
                    shape = ShapeCircleOuter(s);
                else
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
                if (_Rich > 0.5)
                    shape = FlameRich(FlameLook(s));
                else
                    shape = ShapeFlame(s);
                #elif defined(_SHAPE_FLAMETONGUE)
                if (_Rich > 0.5)
                    shape = FlameRich(FlameTongueLook(s));
                else
                    shape = ShapeFlameTongue(s);
                #elif defined(_SHAPE_SMOKE)
                if (_Rich > 1.5)
                {
                    // The thundercloud's layers take the brightness as the lightning inside them.
                    if (_Rich < 2.5)
                        shape = ShapeStormCloud(s);
                    else if (_Rich < 3.5)
                        shape = ShapeStormMass(s);
                    else if (_Rich < 4.5)
                        shape = ShapeStormScud(s);
                    else
                        shape = ShapeStormGlow(s);
                    bright = 1;
                }
                else if (_Rich > 0.5)
                    shape = ShapeRichSmoke(s);
                else
                    shape = ShapeSmoke(s);
                #elif defined(_SHAPE_SCORCH)
                shape = ShapeScorch(s);
                #elif defined(_SHAPE_FROST)
                if (_Rich > 0.5)
                    shape = ShapeIceSheet(s);
                else
                    shape = ShapeFrost(s);
                #elif defined(_SHAPE_ICESPIKE)
                if (_Rich > 0.5)
                    shape = ShapeClearIceSpike(s);
                else
                    shape = ShapeIceSpike(s);
                #elif defined(_SHAPE_CHIP)
                if (_Rich > 0.5)
                    shape = ShapeClearChip(s);
                else
                    shape = ShapeChip(s);
                #elif defined(_SHAPE_CUTINSTREAKS)
                shape = ShapeCutInStreaks(s);
                #elif defined(_SHAPE_BEAM)
                // A shaft's layers on boards of their own (2 halo, 3 core, 4 afterimage).
                if (_Rich > 1.5)
                    shape = half4(BeamLayer(s, _Rich), 0);
                else if (_Rich > 0.5)
                    shape = ShapeRichBeam(s);
                else
                    shape = ShapeBeam(s);
                #elif defined(_SHAPE_ARROW)
                shape = ShapeArrow(s);
                #elif defined(_SHAPE_CRACK)
                if (_Rich > 2.5)
                    shape = ShapeGlassCrack(s);
                else if (_Rich > 1.5)
                    shape = ShapeRupture(s);
                else if (_Rich > 0.5)
                    shape = ShapeFissure(s);
                else
                    shape = ShapeCrack(s);
                #elif defined(_SHAPE_TORNADO)
                if (_Rich > 0.5)
                    shape = ShapeWindStream(s);
                else
                    shape = ShapeTornado(s);
                #elif defined(_SHAPE_BUBBLE)
                if (_Rich > 0.5)
                    shape = ShapeBarrierBubble(s);
                else
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

                // Every shape is gone before the edge of its board, so the square never shows (the
                // cut-in's streaks tile, and the magic circle's veil stands on its bottom edge).
                #if !defined(_SHAPE_CUTINSTREAKS) && !defined(_SHAPE_MAGICCIRCLE)
                float2 side = abs(s.p);
                half edge = 1 - smoothstep(0.93, 1.0, max(side.x, side.y));
                shape.a *= edge;
                shape.rgb *= lerp(1, edge, step(0.5, _Rich));
                #endif

                half fade = IN.color.a * _Color.a;

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                fade *= m.x * m.y;
                #endif

                // A premultiplied shape's light thins with it too, not only its cover.
                half3 rgb = shape.rgb * (_Intensity * bright) * lerp(1, fade, step(0.5, _Rich));
                half4 color = half4(rgb, shape.a * fade);

                // A faint noise of under one step of 8 bits, only where there is light or cover, so
                // wide, soft glows and veils do not band (interleaved gradient noise).
                float dither = frac(52.9829189 * frac(dot(IN.vertex.xy, float2(0.06711056, 0.00583715)))) - 0.5;
                color.rgb = max(0, color.rgb + dither / 255.0 * saturate(max(color.r, max(color.g, color.b)) * 64));
                color.a = saturate(color.a + dither / 255.0 * saturate(color.a * 64));

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
