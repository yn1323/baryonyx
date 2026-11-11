// VFXの質感デバッグルーム（VfxLookLab）の見本を、画像を使わずに計算で描くuGUI用シェーダー。
// 戦闘の形（BattleVfxShape.shader）は、芯・にじみ・ノイズの値を形ごとに式の中へ書いている。
// ここでは同じ部品（BattleVfxShapes.cginc のノイズ・ガウス・ゆがみ）を使い、その値をマテリアルから渡して、
// 「くっきり」から「もや」までを1つの見本で動かせるようにする。
// - 形（_Form）：0 線（斬撃の弧）、1 玉、2 輪（広がる衝撃）、3 かたまり（煙）。
// - 1枚ずつの値（追加のUV1）：x 乱数の種、y 進み具合、z 欠けた割合、w ノイズを動かす時間（秒）。
// - 層：本体（輪郭のやわらかさで縁の幅が変わる）、芯（白い光）、にじみ（色の光）、もや（ノイズの筋）。
// - もやは、画面に沿って昇る筋のほか、形の線に沿って流し（_HazeFollow）、線の外側か内側へ寄せられる
//   （_HazeSide）。空間に一様には置かず、固まる所と薄い所を作る（_HazeClump・_HazeClumpSize）。
//   もやは、本体のまわりにいつもある幕（_HazeEmit 0）か、本体から湧いて外へ流れるもの（1）にできる。
//   湧くもやは、本体の表面で生まれ、勢い（_EmitSpeed）を失いながら外へ進む。どの点のもやも、いつ本体を
//   離れたか（born）で濃さが決まるため、最初はまだ届いていない所には何もなく、本体が消え始める
//   （_EmitStop）と新しいもやが出なくなって、本体のまわりから先に空く。筋は本体から外へ伸び、渦と
//   流れで曲がり、古いもやほど空気の乱れで崩れ、薄まり、冷める。
//   濃淡のもとは現象ごとに足し合わせる：空気の乱れ（_ClumpTurbulence）、本体から湧いて離れていくかたまり
//   （_ClumpSource）、流される先に溜まる（_ClumpCarry。風下、昇る先、沈む先）、渦に巻かれた腕
//   （_ClumpSwirl）、本体の動きの後ろ（_ClumpWake）、衝撃に押し出された殻（_ClumpBlast）、床に溜まる
//   （_ClumpFloor）。
// - 板を傾けても上下が変わらないよう、画面の上の向き（_Up）と床の高さ（_FloorAt）を板の座標で受け取る。
//   浮き沈み（_HazeRise）と横流れ（_HazeWind）で、もやが流される向きを決める。筋はその向きへ伸びて流れ、
//   もやは流される側へ長く、反対側へ短く広がる。
//   本体ともやは _Additive で通常合成から加算へ寄せ、芯とにじみはいつも光として足す。
// - もやのやわらかさ（_HazeSoft）は本体の輪郭と別に決める。もやは本体の光を受け、本体に近い所と、
//   ふくらみの本体を向いた側が明るく熱い色になる（_HazeShade で裏側を暗い筋にし、_HazeGlow で自分も光る）。
// - 熱の色づき（_Heat）：本体・にじみ・もやの色を、熱い所は白く淡く、冷めた所は深く暗く移り変わらせる。
// - 光条（_Rays）と粒（_Sparks）：形から立つ細い光の筋と、まわりを漂う小さな光の粒。
// - サブVFX：本体から生まれて本体の動きに追従する、形のはっきりした副次の層。
//   残像の帯（_SubAfter。本体と同じ線をなぞる帯が、遅れて、本体の動きの後ろ側へ並ぶ）、
//   風圧の筋（_SubPressure。本体の外側に沿う細く鋭い線。長さも間隔もばらばら）、
//   巻きつく帯（_SubSpiral。本体のまわりを螺旋に巻く細い光の帯。手前は明るく、奥は暗く細い）、
//   地を這う余波（_SubWake。床に沿って走る低い光。_Form 4 の別の板に描く）。広がりは _SubSpread。
//   端の薄れ（_SubTaper）で、線の中ほどから両端へ、また本体から離れた帯・筋ほど薄くする。
//   やわらかさ（_SubSoft）で、帯と筋の輪郭をにじませ、白さと明るさを抑え、ノイズで所々をかすれさせる。
//   どちらも0で、くっきりした元の形になる。
//   ばらつき（_SubVary）で、帯・筋ごとに間隔・長さ・位置・太さ・明るさを変え、本体と平行にそろえず、
//   巻く帯の振れ幅と巻きの間隔も場所ごとに変える。0で、等間隔にそろった形になる。
// - 階調の段（_Steps）：2以上で濃淡を段に分ける（アニメ調の塗り分け）。
// - 消え方：0でノイズに沿って削れ、1で一様に薄れる（_DissolveSoft）。削れる順番は、むら（_EatNoise）、
//   外からか芯からか（_EatOuter）、動きの後ろからか先からか（_EatWake）、風上からか風下からか（_EatCarry）を
//   足し合わせて決め、削れ目のやわらかさ（_EatSoft）は輪郭と別に決める。消えながら、細り（_EatThin）、
//   冷めて白さと明るさを失い（_EatCool）、削れ目が光る（_EatRim）。芯は先に、もやは遅れて消える（_EatLag）。
// 合成は乗算済みアルファ（One OneMinusSrcAlpha）。アルファで奥を覆い、色で光を足す。
Shader "Baryonyx/Combat/Vfx Look Lab"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Form ("Form (0 line, 1 orb, 2 ring, 3 mass)", Float) = 0

        _EdgeSoft ("Edge Softness", Range(0, 1)) = 0.3
        _EdgeNoise ("Edge Noise", Range(0, 1)) = 0.1
        _BodyWidth ("Body Width", Range(0, 1)) = 0.3
        _BodyOpacity ("Body Opacity", Range(0, 1)) = 1
        _CoreWidth ("Core Width", Range(0, 1)) = 0.3
        _CoreWhite ("Core Whiteness", Range(0, 1)) = 0.75
        _CoreBright ("Core Brightness", Range(0, 4)) = 1.8
        _HaloWidth ("Halo Width", Range(0, 1)) = 0.25
        _HaloStrength ("Halo Strength", Range(0, 1.5)) = 0.3
        _HazeAmount ("Haze Amount", Range(0, 1)) = 0.15
        _HazeReach ("Haze Reach", Range(0, 1)) = 0.35
        _HazeFill ("Haze Fill", Range(0, 1)) = 0.3
        _HazeSoft ("Haze Softness", Range(0, 1)) = 0.3
        _HazeGlow ("Haze Glow", Range(0, 1)) = 0
        _HazeShade ("Haze Shading", Range(0, 1)) = 0.3
        _HazeFollow ("Haze Follows The Form", Range(0, 1)) = 0
        _HazeEmit ("Haze Comes Out Of The Form", Range(0, 1)) = 0
        _EmitSpeed ("Haze Outflow Speed", Range(0, 1)) = 0.5
        _EmitStop ("When The Form Stops Giving Off Haze", Range(0, 1)) = 0.45
        _HazeClump ("Haze Clumping", Range(0, 1)) = 0.3
        _HazeClumpSize ("Haze Clump Size", Range(0, 1)) = 0.5
        _ClumpTurbulence ("Clumped By Turbulence", Range(0, 1)) = 1
        _ClumpSource ("Clumped By Its Source", Range(0, 1)) = 0
        _ClumpCarry ("Clumped Where It Is Carried", Range(0, 1)) = 0
        _ClumpSwirl ("Clumped By A Swirl", Range(0, 1)) = 0
        _ClumpWake ("Clumped In The Wake", Range(0, 1)) = 0
        _ClumpBlast ("Clumped By A Blast", Range(0, 1)) = 0
        _ClumpFloor ("Clumped On The Floor", Range(0, 1)) = 0
        _Up ("World Up On The Board", Vector) = (0, 1, 0, 0)
        _FloorAt ("Floor Height On The Board", Float) = -0.235
        _HazeSide ("Haze Side (-1 inner, 1 outer)", Range(-1, 1)) = 0
        _HazeRise ("Haze Rise (-1 sinks, 1 rises)", Range(-1, 1)) = 0.3
        _HazeWind ("Haze Wind (-1 left, 1 right)", Range(-1, 1)) = 0
        _NoiseScale ("Noise Scale", Range(0, 1)) = 0.4
        _NoiseWarp ("Noise Warp", Range(0, 1)) = 0.4
        _NoiseFlow ("Noise Flow", Range(0, 1)) = 0.3
        _Heat ("Heat Colours", Range(0, 1)) = 0.2
        _Rays ("Rays", Range(0, 1)) = 0
        _Sparks ("Sparks", Range(0, 1)) = 0
        _SubAfter ("Sub: Afterimage Ribbons", Range(0, 1)) = 0
        _SubPressure ("Sub: Pressure Lines", Range(0, 1)) = 0
        _SubSpiral ("Sub: Winding Ribbon", Range(0, 1)) = 0
        _SubWake ("Sub: Wake Along The Floor", Range(0, 1)) = 0
        _SubSpread ("Sub: Spread", Range(0, 1)) = 0.5
        _SubTaper ("Sub: Fading Toward The Ends", Range(0, 1)) = 0.6
        _SubSoft ("Sub: Softness", Range(0, 1)) = 0.5
        _SubVary ("Sub: Variety", Range(0, 1)) = 0.6
        _WakeDrag ("Wake Dragged One Way (the wake's own board)", Range(0, 1)) = 0
        _Additive ("Additive", Range(0, 1)) = 1
        _Intensity ("Intensity", Range(0, 4)) = 1.3
        _Steps ("Tone Steps", Range(0, 6)) = 0
        _DissolveSoft ("Dissolve Softness", Range(0, 1)) = 0
        _EatSoft ("Eaten Edge Softness", Range(0, 1)) = 0.3
        _EatNoise ("Eaten Unevenly", Range(0, 1)) = 1
        _EatOuter ("Eaten From Outside (-1 from the core)", Range(-1, 1)) = 0
        _EatWake ("Eaten From Behind (-1 from the front)", Range(-1, 1)) = 0
        _EatCarry ("Eaten From Upwind (-1 from downwind)", Range(-1, 1)) = 0
        _EatThin ("Thins As It Goes", Range(0, 1)) = 0
        _EatCool ("Cools As It Goes", Range(0, 1)) = 0
        _EatRim ("Glows Where It Is Eaten", Range(0, 1)) = 0
        _EatLag ("Haze Lingers", Range(0, 1)) = 0

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
        // Keep the destination alpha unchanged, as the other UI materials do.
        Blend One OneMinusSrcAlpha, Zero One
        ColorMask [_ColorMask]

        Pass
        {
            Name "VfxLookLab"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // fwidth keeps a hard edge a pixel wide at any size of the board.
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "BattleVfxShapes.cginc"

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
                float4 params   : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half4 _Color;
            half _Form;
            half _EdgeSoft;
            half _EdgeNoise;
            half _BodyWidth;
            half _BodyOpacity;
            half _CoreWidth;
            half _CoreWhite;
            half _CoreBright;
            half _HaloWidth;
            half _HaloStrength;
            half _HazeAmount;
            half _HazeReach;
            half _HazeFill;
            half _HazeSoft;
            half _HazeGlow;
            half _HazeShade;
            half _Heat;
            half _Rays;
            half _Sparks;
            half _HazeFollow;
            half _HazeEmit;
            half _EmitSpeed;
            half _EmitStop;
            half _HazeClump;
            half _HazeClumpSize;
            half _ClumpTurbulence;
            half _ClumpSource;
            half _ClumpCarry;
            half _ClumpSwirl;
            half _ClumpWake;
            half _ClumpBlast;
            half _ClumpFloor;
            float4 _Up;
            float _FloorAt;
            half _HazeSide;
            half _HazeRise;
            half _HazeWind;
            half _NoiseScale;
            half _NoiseWarp;
            half _NoiseFlow;
            half _SubAfter;
            half _SubPressure;
            half _SubSpiral;
            half _SubWake;
            half _SubSpread;
            half _SubTaper;
            half _SubSoft;
            half _SubVary;
            half _WakeDrag;
            half _Additive;
            half _Intensity;
            half _Steps;
            half _DissolveSoft;
            half _EatSoft;
            half _EatNoise;
            half _EatOuter;
            half _EatWake;
            half _EatCarry;
            half _EatThin;
            half _EatCool;
            half _EatRim;
            half _EatLag;
            int _UIVertexColorAlwaysGammaSpace;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord.xy;
                OUT.params = v.params;
                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                    v.color.rgb = UIGammaToLinear(v.color.rgb);
                OUT.color = v.color;
                return OUT;
            }

            float EaseOut(float x)
            {
                x = saturate(x);
                return 1 - (1 - x) * (1 - x);
            }

            // v (0 to 1) cut into n flat steps, each step's edge a pixel wide; smooth below 2 steps.
            float Posterize(float v, float n)
            {
                if (n < 1.5)
                    return v;
                float x = v * n;
                float w = clamp(fwidth(x), 1e-3, 0.5);
                return (floor(x) + smoothstep(0.5 - w, 0.5 + w, frac(x))) / n;
            }

            // The tint at heat h (0 to 1): deep and dark spot it is cool, itself in the middle,
            // paler as it heats, and nearForm white at its hottest.
            half3 LookHeat(float h, half3 tint)
            {
                half3 c = lerp(tint * tint * 0.5, tint, saturate(h * 2.5));
                c = lerp(c, lerp(tint, 1, 0.55), saturate(h * 2.5 - 1.0));
                return lerp(c, lerp(tint, 1, 0.92), saturate(h * 3.0 - 2.0));
            }

            // Grains of light, one to a cell of at (most cells empty, up to half filled as
            // amount rises, so they stay scattered and never read as a stipple), each a pixel or
            // two wide and twinkling.
            float LookSparks(float2 at, float grain, float t, float amount)
            {
                float2 cell = floor(at);
                float2 inCell = frac(at) - 0.5;
                float pick = Hash(float3(cell, 3.1));
                float2 spot = float2(Hash(float3(cell, 7.7)), Hash(float3(cell, 12.3))) * 0.6 - 0.3;
                float size = max(grain * 1.6, 0.012);
                float d = length(inCell - spot);
                float twinkle = 0.55 + 0.45 * sin(t * (5.0 + 9.0 * pick) + pick * 40.0);
                return step(1 - amount * 0.5, pick) * exp(-(d * d) / (size * size)) * twinkle;
            }

            // How much is left of a point whose turn is turn, when eaten of the whole has gone:
            // cut off as the eating passes its turn (edge soft), or thinned evenly (even).
            float LookKeep(float turn, float eaten, float edge, float even)
            {
                float cut = lerp(-edge, 1 + edge, eaten);
                return lerp(smoothstep(cut - edge, cut + edge, turn), 1 - eaten, even);
            }

            // The wake along the floor, on a board of its own laid flat at the floor (_Form 4): a
            // low run of light and heat dragged the way the form moved from where it met the
            // floor (_WakeDrag 1, from the board's left end) or spreading both ways from under a
            // form that stands (0, from the board's middle). It is thick and bright at its root,
            // breaks into streaks along the floor and thins toward its end, and grows out with
            // the form's life.
            half4 LookWake(float2 p, float2 o, float k, float eaten, float t, half3 tint, float2 pixel)
            {
                float rootAt = lerp(0.0, -0.82, _WakeDrag);
                float reachOut = lerp(0.6, 1.1, _SubSpread) * lerp(0.9, 1.75, _WakeDrag)
                    * EaseOut(saturate((k - 0.14 * _WakeDrag) / 0.45));
                float from = p.x - rootAt;
                float out01 = lerp(abs(from), from, _WakeDrag) / max(reachOut, 1e-3);
                // Fuller near its root and fainter the further it runs (_SubTaper).
                float body = pow(saturate(1 - out01), lerp(1.0, 2.0, _SubTaper))
                    * smoothstep(-0.12, 0.0, out01 + (1 - _WakeDrag));
                // Low: thickest at the root, thinner as it runs.
                float tall = lerp(0.3, 0.1, saturate(out01));
                float low = exp(-(p.y * p.y) / (tall * tall));
                float along = from * 5.0 - t * 1.4 * lerp(sign(from), 1.0, _WakeDrag);
                float streaks = Fbm4(float2(along, p.y * 6.0) + o, t * 0.3);
                float torn = smoothstep(lerp(0.28, 0.1, _SubSoft), lerp(0.62, 0.9, _SubSoft), streaks);
                float wake = body * low * (0.25 + 1.1 * torn);
                // It goes later than the form, thinning from its far end.
                float left = saturate(1 - eaten * 1.15 - saturate(out01) * eaten * 0.6);
                float fade = (1 - smoothstep(0.86, 1.0, abs(p.x))) * (1 - smoothstep(0.7, 1.0, abs(p.y)));
                float heat = saturate(body * (0.4 + 0.6 * torn)) * (1 - eaten * _EatCool * 0.7);
                half3 light = lerp(tint, LookHeat(0.15 + 0.6 * heat, tint), _Heat) * wake * left * fade
                    * _SubWake * 1.15 * _Intensity * saturate(k / 0.05);
                float dither = frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715)))) - 0.5;
                light = max(0, light + dither / 255.0 * saturate(max(light.r, max(light.g, light.b)) * 64));
                return half4(light, 0);
            }

            half4 frag(v2f IN) : SV_Target
            {
                float2 p = IN.texcoord * 2 - 1;
                float seed = IN.params.x;
                float k = saturate(IN.params.y);
                float eaten = saturate(IN.params.z);
                float t = IN.params.w;
                float px = max(max(fwidth(p.x), fwidth(p.y)), 1e-4);
                float2 o = SeedOffset(seed);
                half3 tint = IN.color.rgb * _Color.rgb;
                float soft = _EdgeSoft;
                float steps = floor(_Steps + 0.5);
                if (_Form > 3.5)
                    return LookWake(p, o, k, eaten, t, tint * IN.color.a * _Color.a, IN.vertex.xy);

                float scale = lerp(1.5, 9.0, _NoiseScale);
                float drift = t * (0.05 + 0.3 * _NoiseFlow);

                // The form: how far a point is from its centre line or point, and how thick the
                // body is there. thick thins a stroke toward its ends. For what follows the form
                // (the haze laid along it, and its waves): across is the distance from the form's
                // line, positive on its outer side; awayDir the way from the form to the point;
                // formAt a place in the noise that runs along the form, stretched along it;
                // wobbleAt a place along or round the form for what is uneven along it; and
                // behind how far in the wake of the form's own motion the point lies (0 ahead of
                // it, 1 well behind it, 0.5 where the form does not move).
                float center;
                float extent;
                float thick = 1;
                // As it goes it may thin, down to a thread, before it is eaten.
                float thin = lerp(1.0, 1.0 - 0.85 * eaten, _EatThin);
                float across;
                float2 awayDir;
                float2 alongDir;
                float2 formToward;
                float2 formAt;
                float2 wobbleAt;
                float behind = 0.5;
                // For the sub effects: alongAt how far along the form (-1 to 1 along a stroke
                // from its start to its tip, or round a closed form, where -1 and 1 meet);
                // drawn whether the stroke has been drawn this far yet; and trailSide the side
                // the form's own motion leaves behind (1 its outer side, -1 its inner).
                float alongAt = 0;
                float drawn = 1;
                float trailSide = 1;
                float closed = 1;
                float r = length(p);
                if (_Form < 0.5)
                {
                    // A sword arc, drawn in from the left over the first sixth of its life.
                    const float2 arcCenter = float2(0, -0.78);
                    const float arcRadius = 0.98;
                    const float span = 0.86;
                    float2 q = p - arcCenter;
                    float head = lerp(-1.0, 1.0, EaseOut(k / 0.16));
                    float turned = atan2(q.x, q.y) / span;
                    float along = clamp(turned, -1.0, head);
                    float2 nearest = arcCenter + arcRadius * float2(sin(along * span), cos(along * span));
                    center = length(p - nearest);
                    thick = pow(saturate(1 - along * along), 0.7);
                    extent = lerp(0.006, 0.11, _BodyWidth) * thick * thin;
                    across = length(q) - arcRadius;
                    awayDir = q / max(length(q), 1e-4) * (across < 0 ? -1.0 : 1.0);
                    formToward = float2(0, across < 0 ? 1.0 : -1.0);
                    float2 fromCentre = q / max(length(q), 1e-4);
                    alongDir = float2(fromCentre.y, -fromCentre.x);
                    float run = clamp(turned, -1.6, 1.6) * span * arcRadius;
                    formAt = float2(run * 0.45 - t * 1.6 * _NoiseFlow, across * 1.5);
                    wobbleAt = float2(run, 0);
                    alongAt = turned;
                    drawn = smoothstep(head + 0.05, head - 0.03, turned) * smoothstep(-1.08, -0.98, turned);
                    closed = 0;
                    // The blade passed the stroke's start first: the air there has had longest
                    // to gather behind it.
                    behind = saturate(0.5 - turned * 0.5);
                }
                else
                {
                    float2 q = p;
                    float rim = 0;
                    float wake = 0;
                    if (_Form < 1.5)
                    {
                        // An orb that swells as it appears.
                        center = r;
                        extent = lerp(0.06, 0.5, _BodyWidth) * lerp(0.55, 1.0, EaseOut(k / 0.2)) * thin;
                        rim = extent;
                    }
                    else if (_Form < 2.5)
                    {
                        // A ring of shock that spreads and thins.
                        rim = lerp(0.16, 0.66, EaseOut(k));
                        wake = 1;
                        center = abs(r - rim);
                        extent = lerp(0.004, 0.09, _BodyWidth) * lerp(1.0, 0.6, k) * thin;
                    }
                    else
                    {
                        // A lumpy mass of smoke that puffs out and rises.
                        q = p - float2(0, k * 0.16 - 0.05);
                        float lump = Fbm4(q * 2.6 + o.yx, t * 0.15);
                        center = length(q * float2(1.0, 1.1));
                        extent = lerp(0.1, 0.5, _BodyWidth) * lerp(0.6, 1.05, EaseOut(k / 0.5))
                            * (0.5 + 1.0 * smoothstep(0.25, 0.75, lump)) * thin;
                        rim = extent;
                        wake = 2;
                    }
                    // Round a closed form the noise is read on a circle, so it has no seam: far
                    // round it is a short way in the noise, and across it a long way.
                    float away = length(q);
                    float2 dir = q / max(away, 1e-4);
                    across = away - rim;
                    awayDir = dir * (across < 0 ? -1.0 : 1.0);
                    formToward = -awayDir;
                    alongDir = float2(-dir.y, dir.x);
                    formAt = Rotate(dir, t * 0.9 * _NoiseFlow) * max(0.05, 0.4 + across * 1.5);
                    wobbleAt = dir;
                    alongAt = atan2(dir.y, dir.x) / Pi;
                    // A spreading ring leaves its trail inside it.
                    trailSide = wake > 0.5 && wake < 1.5 ? -1.0 : 1.0;
                    // A spreading ring leaves its wake inside it, and a rising mass under it.
                    if (wake > 1.5)
                        behind = saturate(0.5 - q.y * 1.5);
                    else if (wake > 0.5)
                        behind = saturate(0.5 - across * 4.0);
                }
                // A stroke's light and mist draw in toward its ends with it, so they close round
                // the ends instead of stopping at a straight cut.
                float tip = saturate(thick * 5);
                float reach = lerp(0.35, 1.0, thick);

                // The way the haze is carried: up or down (_HazeRise, lighter or heavier than the
                // air) and sideways (_HazeWind).
                float2 upDir = normalize(_Up.xy);
                float2 carry = _HazeWind * float2(upDir.y, -upDir.x) + _HazeRise * upDir;
                float carried = min(length(carry), 1.0);
                float2 carryDir = carried > 0.02 ? carry / max(length(carry), 1e-4) : float2(0, 1);
                float2 carrySide = float2(carryDir.y, -carryDir.x);

                // The noise the edge and the haze read, warped: laid over the screen, stretched
                // into strands the way the haze is carried and moving that way, or (_HazeFollow)
                // laid along the form and streaming along it. nearer is the same noise a step
                // toward the form, which tells which side of a billow faces the form's light.
                const float lightStep = 0.3;
                float warp = _NoiseWarp * 1.6;
                float2 screenAt = float2(dot(p, carrySide), dot(p, carryDir) * 0.6) * scale + o
                    - float2(0, t * 1.6 * _NoiseFlow * carried);
                float2 screenToward = -float2(dot(awayDir, carrySide), dot(awayDir, carryDir) * 0.6);
                float2 screenWarped = Warp(screenAt, drift, warp);
                float n1 = Fbm4(screenWarped, 1.7 + drift);
                float nearer = Fbm4(screenWarped + screenToward * lightStep, 1.7 + drift);
                if (_HazeFollow > 0.001)
                {
                    float2 formWarped = Warp(formAt * scale + o, drift, warp);
                    n1 = lerp(n1, Fbm4(formWarped, 1.7 + drift), _HazeFollow);
                    nearer = lerp(nearer, Fbm4(formWarped + formToward * lightStep, 1.7 + drift), _HazeFollow);
                }

                // Signed distance to the body's edge, pushed in and out by the noise.
                float sd = center - extent + (n1 - 0.5) * _EdgeNoise * (0.04 + extent * 0.9) * 2;
                float outside = max(sd, 0);

                // Body: a hard edge a pixel wide, or one that fades from the middle of the body
                // to well outside it. Heated (_Heat), it is hottest in its middle.
                float inner = soft * extent;
                float outer = max(px, soft * soft * 0.45 * reach);
                float density = Posterize(1 - smoothstep(-inner, outer, sd), steps);
                // As it goes it may cool: the heat's colours fall back toward the deep, dark end.
                float warmth = 1 - eaten * _EatCool;
                half3 bodyTint = lerp(tint, LookHeat((0.5 + 0.5 * saturate(-sd / max(extent, 1e-3))) * lerp(0.3, 1.0, warmth), tint), _Heat);

                // Core: the white-hot middle, never thinner than a pixel (it dims instead).
                float coreWidth = max(extent, 0.012 * tip) * 0.7 * _CoreWidth;
                float coreDrawn = max(coreWidth, px * 0.75);
                float coreEdge = lerp(0.04, 1.0, soft);
                float core = (1 - smoothstep(coreDrawn * (1 - coreEdge), coreDrawn * (1 + coreEdge) + px, center))
                    * saturate(coreWidth / (px * 0.75));

                // Halo: the tint's light around the body, cooling as it thins.
                float haloWidth = lerp(0.02, 0.6, _HaloWidth) * reach;
                float haloFall = exp(-(outside * outside) / (haloWidth * haloWidth));
                float halo = Posterize(haloFall, steps) * _HaloStrength * (1 - density);
                half3 haloTint = lerp(tint, LookHeat((0.15 + 0.6 * haloFall) * lerp(0.3, 1.0, warmth), tint), _Heat);

                // Haze: the noise around the body, in strands or (_HazeFill) filled in, as soft as
                // _HazeSoft makes it whatever the body's edge is, its own edge billowing with the
                // noise. It is not spread evenly: what moves the air gathers it thick in places
                // and leaves the air nearly clear in others (clump; _HazeClump, in patches of
                // _HazeClumpSize). It can keep to the form's outer or inner side
                // (_HazeSide). Carried, it reaches further the way it is carried, more as it
                // ages, and less against it.
                float lean = dot(awayDir, carryDir);
                float reachBase = lerp(0.06, 0.75, _HazeReach) * reach;
                float height = dot(p, upDir) - _FloorAt;
                float contrast = lerp(0.5, 0.1, _HazeClump);
                float patches = lerp(3.2, 0.8, _HazeClumpSize);
                float clump = 1;
                if (_HazeClump > 0.001)
                {
                    // What gathers the haze, each cause pushing the density up or down from even.
                    float gathered = 0.5;
                    if (_ClumpTurbulence > 0.001)
                    {
                        // The air's own eddies: a slow, large noise carried with the haze.
                        float2 clumpAt = float2(dot(p, carrySide), dot(p, carryDir) * 0.55) * patches
                            + o.yx * 1.7 - float2(0, t * 0.7 * _NoiseFlow * carried);
                        clumpAt += (float2(Noise(float3(clumpAt * 0.8, 3.3 + drift)), Noise(float3(clumpAt * 0.8 + 5.1, 8.7 + drift))) - 0.5)
                            * warp;
                        float eddies = Fbm(float3(clumpAt, 2.1 + drift * 0.6));
                        if (_HazeFollow > 0.001)
                            eddies = lerp(eddies, Fbm(float3(formAt * patches * 1.4 + o.yx * 1.7, 2.1 + drift * 0.6)), _HazeFollow);
                        gathered += (saturate((eddies - 0.5) * 2.0 + 0.5) - 0.5) * _ClumpTurbulence;
                    }
                    // Carried, it piles up where it is carried to (downwind, or above a hot form,
                    // or under a cold one) and the side it leaves is swept clear.
                    gathered += 0.5 * lean * carried * _ClumpCarry;
                    // Left behind the form's own motion.
                    gathered += (behind - 0.5) * _ClumpWake;
                    // Heavy, it lies thick along the floor and thins above it.
                    gathered += (exp(-max(height, 0) / 0.2) - 0.5) * _ClumpFloor;
                    // A veil that is always there has no history, so its puffs, its swirl and the
                    // shell a blow pushes out are laid on as patterns. Haze that comes out of the
                    // form gets them from when and where it left the form instead (below).
                    float still = 1 - _HazeEmit;
                    if (_ClumpSource * still > 0.001)
                    {
                        float puffs = Fbm(float3(wobbleAt * patches * 1.6 + o,
                            abs(across) * patches * 2.4 - t * (0.3 + 1.2 * _NoiseFlow)));
                        gathered += (saturate((puffs - 0.5) * 2.2 + 0.5) - 0.5) * _ClumpSource * still;
                    }
                    if (_ClumpSwirl * still > 0.001)
                    {
                        float wound = (_Form < 0.5 ? wobbleAt.x * 7.0 : atan2(wobbleAt.y, wobbleAt.x) * 3.0)
                            + abs(across) * 9.0 - t * (0.5 + 2.0 * _NoiseFlow)
                            + (Noise(float3(wobbleAt * 1.7 + o, t * 0.25)) - 0.5) * 2.0;
                        gathered += 0.5 * sin(wound) * _ClumpSwirl * still;
                    }
                    if (_ClumpBlast * still > 0.001)
                    {
                        float front = (0.25 + 0.75 * k) * reachBase;
                        gathered += (saturate(1 - abs(outside - front) / (front * 0.6 + 0.03)) - 0.5) * _ClumpBlast * still;
                    }
                    clump = lerp(1.0, smoothstep(0.5 - contrast, 0.5 + contrast, saturate(gathered)), _HazeClump);
                }
                float hazeWidth = reachBase
                    * lerp(1.0, 0.45 + 1.0 * clump, _HazeClump)
                    * lerp(1.0, 1.0 + 2.2 * (0.5 + k) * max(lean, 0) - 0.7 * max(-lean, 0), carried);
                float hazeEdge = lerp(0.02, 0.35, _HazeSoft);
                float fill = lerp(0.62, 0.25, _HazeFill);
                float wisp = smoothstep(fill - hazeEdge, fill + hazeEdge, n1);
                float veilEdge = lerp(0.06, 1.0, _HazeSoft);
                float veilAt = outside + (0.5 - n1) * hazeWidth * 0.7;
                float veil = 1 - smoothstep(hazeWidth * (1 - veilEdge), hazeWidth * (1 + veilEdge) + px, veilAt);

                // Haze that comes out of the form (_HazeEmit). It is born on the form's surface
                // and moves out, losing its speed (_EmitSpeed) until it stands at its furthest
                // reach, so how far a point is from the form tells how old the haze there is
                // (age, in parts of the form's life) and when it left the form (born). There is
                // none yet where it has not had time to come; and once the form stops giving it
                // off (_EmitStop, as the form starts to go) the air next to the form clears first.
                float fresh = exp(-outside / max(hazeWidth * 0.6, 1e-3));
                float streamed = veil;
                if (_HazeEmit > 0.001)
                {
                    float furthest = max(hazeWidth * 1.8, 1e-3);
                    float slowing = lerp(0.7, 0.16, _EmitSpeed);
                    float age = -slowing * log(max(1 - saturate(outside / furthest), 1e-3));
                    // Old haze has been pulled about by the air: where and when it left the form
                    // is less and less certain, so its streams and puffs break up as they go out.
                    float torn = saturate(age * 2.5) * (0.3 + 0.7 * _ClumpTurbulence);
                    float born = k - age + (n1 - 0.5) * 0.3 * torn;
                    // It leaves along the form's surface where it was given off; a swirl winds it
                    // round the form as it ages (_ClumpSwirl), and the wind bends it downwind.
                    float wound = age * (_ClumpSwirl * 2.6 - dot(carry, alongDir) * 1.6) + (nearer - 0.5) * 1.6 * torn;
                    float2 leftAt = _Form < 0.5 ? float2(wobbleAt.x - wound * 0.35, 0) : Rotate(wobbleAt, wound);
                    // Streams: uneven along the form, steady in time, so they stand off the form
                    // as streaks that show which way the haze has gone.
                    float streams = Noise(float3(leftAt * lerp(9.0, 3.0, _HazeClumpSize) + o, born * 1.3 + 3.0));
                    // Puffs: given off in bursts (_ClumpSource), each a band that leaves the form.
                    float puffs = Noise(float3(leftAt * 1.4 + o.yx, born * lerp(5.0, 13.0, _ClumpSource) + 7.0));
                    float given = saturate(0.5 + (streams - 0.5) * 1.3 + (puffs - 0.5) * 2.0 * _ClumpSource);
                    given = lerp(1.0, smoothstep(0.5 - contrast, 0.5 + contrast, given), _HazeClump);
                    // Given off from the moment the form appears until it starts to go.
                    float giving = smoothstep(0.0, 0.04, born) * (1 - smoothstep(_EmitStop, _EmitStop + 0.1, born));
                    // The first breath: the blow's own burst, a shell that leads the rest out.
                    float burst = exp(-(born * born) / (0.07 * 0.07)) * step(-0.12, born) * _ClumpBlast * 1.4;
                    // It thins as it spreads and ages.
                    streamed = saturate(given * giving + burst) * exp(-age / 0.8);
                    streamed = lerp(veil, streamed, _HazeEmit);
                    fresh = lerp(fresh, exp(-age * 2.4), _HazeEmit);
                }

                float sideEdge = 0.02 + _HazeSoft * 0.1 + px;
                float outerSide = smoothstep(-sideEdge, sideEdge, across);
                float side = lerp(1.0, outerSide, saturate(_HazeSide))
                    * lerp(1.0, 1 - outerSide, saturate(-_HazeSide));
                // What lies on the floor does not sink through it.
                float aboveFloor = lerp(1.0, smoothstep(-0.1, -0.02, height), _HazeClump * _ClumpFloor);
                // Streams keep their shape: the fine strands thin them less than they do a veil.
                float strands = lerp(wisp, lerp(wisp, 1.0, 0.45), _HazeEmit);
                float hazeDensity = Posterize(strands * streamed * lerp(1.0, clump * 1.35, _HazeClump), steps)
                    * side * aboveFloor;

                // The haze is lit by the form: brighter and hotter where it is fresh from it, and
                // on the side of each billow that faces it (_HazeShade darkens the far side into
                // streaks); _HazeGlow lets it give that light off itself.
                float nearForm = fresh;
                float facing = saturate(0.5 + (n1 - nearer) * 5.0);
                float hazeHeat = nearForm * (0.45 + 0.55 * facing);
                half3 hazeTint = lerp(tint * 0.75, LookHeat(hazeHeat * 0.85 * lerp(0.3, 1.0, warmth), tint), _Heat)
                    * lerp(1.0, 0.25 + 0.9 * facing, _HazeShade);
                half3 hazeLight = lerp(tint, LookHeat(0.25 + 0.75 * hazeHeat, tint), _Heat)
                    * hazeDensity * _HazeAmount * nearForm * (0.4 + 0.6 * facing) * _HazeGlow * 2.2 * warmth;

                // Rays: thin shafts of light standing off the form, uneven along it. A stroke
                // throws them from its outer side only (on its inner side they would all meet at
                // the arc's centre).
                float shaft = Noise(float3(wobbleAt * 9.0 + o.yx, t * 0.4));
                float rays = pow(saturate(shaft * 1.5 - 0.25), 4) * exp(-outside / 0.4) * (1 - density) * _Rays
                    * (_Form < 0.5 ? smoothstep(-0.02, 0.06, across) : 1.0);

                // Sparks: small bright grains nearForm the form, drifting the way the haze is carried.
                float sparks = 0;
                if (_Sparks > 0.001)
                {
                    float2 drifted = p - carryDir * t * 0.12 * (0.3 + carried);
                    sparks = (LookSparks(drifted * 5.0 + o, px * 5.0, t, _Sparks)
                        + LookSparks(drifted * 8.0 + o.yx, px * 8.0, t, _Sparks) * 0.7)
                        * exp(-outside / (hazeWidth + 0.12));
                }

                // How it goes. Each point has a turn (0 goes first, 1 last), from what eats the
                // form: an uneven noise (_EatNoise); from the outside in, the far haze first and
                // the middle of the body last, or the other way (_EatOuter); from behind the form's
                // motion or from ahead of it (_EatWake); and from upwind or from downwind
                // (_EatCarry; with no wind, from below or from above).
                float eatNoise = saturate((Fbm4(p * scale * 0.8 + o.yx + 7.1, 2.3) - 0.2) / 0.6);
                float inward = saturate(1 - outside / (reachBase * 1.2 + 0.05)) * 0.7
                    + saturate(-sd / max(extent, 1e-3)) * 0.3;
                float downwind = saturate(0.5 + dot(p, carryDir) * 0.6);
                float turn = saturate(0.5
                    + (eatNoise - 0.5) * _EatNoise
                    + (inward - 0.5) * _EatOuter
                    + (0.5 - behind) * _EatWake
                    + (downwind - 0.5) * _EatCarry);
                // With nothing to order it, it can only thin evenly.
                float even = max(_DissolveSoft,
                    1 - saturate((_EatNoise + abs(_EatOuter) + abs(_EatWake) + abs(_EatCarry)) * 4));
                float eatEdge = lerp(0.03, 0.35, _EatSoft);

                // The layers do not go together (_EatLag): the core, the rays and the sparks go
                // early, and the haze lingers after the body.
                float early = saturate(eaten * (1 + _EatLag));
                float late = saturate(eaten * (1 + _EatLag) - _EatLag);
                float keepBody = LookKeep(turn, eaten, eatEdge, even);
                float keepLight = LookKeep(turn, early, eatEdge, even);
                float keepHaze = LookKeep(turn, late, eatEdge, even);
                // Where it is being eaten, its edge may glow (_EatRim).
                float eatenRim = keepBody * (1 - keepBody) * 4 * step(0.001, eaten) * _EatRim;

                // --- Sub effects: born of the form and following it, each with a shape of its own ---

                // Afterimages: ribbons that trace the form's own line a little later, one behind
                // another on the side its motion leaves, the nearest thin and bright and the
                // furthest wide and faint. They drift off and unravel as they age.
                // Toward the ends (_SubTaper): along a stroke the sub effects are fullest at its
                // middle and thin out to nothing at both its ends (the higher, the sooner they
                // thin), and the ones further from the form are fainter and shorter. A closed
                // form has no ends, so round it only the falloff with the distance from the form
                // applies. Softness (_SubSoft) widens their edges and takes their sharp inner
                // lines away.
                // Softness also frays them: a slow noise thins them out in places, so no ribbon
                // or line runs unbroken at one strength, and it takes their whiteness down.
                float fray = lerp(1.0, smoothstep(0.12, 0.72, Fbm4(p * 4.5 + o.yx + 7.3, t * 0.5)), _SubSoft * 0.75);
                float midst = lerp(max(1 - smoothstep(0.1, 1.0, abs(alongAt)), 1e-4), 1.0, closed);
                float ends = pow(midst, _SubTaper * 2.0);
                float afterimages = 0;
                if (_SubAfter > 0.001)
                {
                    float apart = lerp(0.035, 0.1, _SubSpread);
                    float along01 = lerp(lerp(0.35 + 0.65 * behind, 0.8, _SubTaper), 1.0, closed);
                    [unroll]
                    for (int ribbonAt = 1; ribbonAt <= 3; ribbonAt++)
                    {
                        float later = ribbonAt * 0.07;
                        float aged = saturate((k - later) / 0.3);
                        // Variety (_SubVary): each ribbon has a gap, a length, a place along the
                        // form, a width and a brightness of its own, and does not keep parallel
                        // to the form: it leans away toward one end and sways.
                        float v1 = Hash1(ribbonAt + 31.0, seed);
                        float v2 = Hash1(ribbonAt + 42.0, seed);
                        float v3 = Hash1(ribbonAt + 53.0, seed);
                        float own = alongAt - (v2 - 0.5) * 0.9 * _SubVary;
                        float ownAway = lerp(abs(own), abs(frac(own * 0.5 + 0.5) - 0.5) * 2.0, closed);
                        float ownReach = lerp(0.42, 1.1, v3) * lerp(1.0, 0.85, closed);
                        float ownShown = lerp(1.0, 1 - smoothstep(ownReach * 0.3, ownReach, ownAway), _SubVary);
                        float lean = ((v1 - 0.5) * 0.2 * own * (1 - closed) + sin(own * Pi * (1.0 + round(v3 * 2.0)) + v2 * Tau) * 0.022)
                            * ribbonAt * _SubVary;
                        float off = apart * ribbonAt * (0.55 + 0.8 * aged) * lerp(1.0, 0.4 + 1.3 * v1, _SubVary)
                            + (n1 - 0.5) * 0.035 * ribbonAt * aged + lean;
                        float wide = lerp(0.012, 0.03, _BodyWidth) * (1.0 + 0.55 * ribbonAt) * max(thick, 0.15)
                            * lerp(1.0, 0.6 + 0.9 * v3, _SubVary);
                        float from = across * trailSide - off;
                        float further = ribbonAt - 1.0;
                        float lessFar = lerp(1.0, exp(-0.3 * further), _SubTaper)
                            * pow(midst, 0.4 * further * _SubTaper);
                        afterimages += (Gauss(from, wide * (1.0 + 0.7 * _SubSoft)) * 0.55
                                + Line(from, wide * 0.3, px) * 0.6 * (1.0 - 0.95 * _SubSoft))
                            * (1.0 - 0.27 * ribbonAt) * lessFar * ownShown * lerp(1.0, 0.6 + 0.7 * v2, _SubVary)
                            * smoothstep(later, later + 0.06, k);
                    }
                    afterimages *= along01 * tip * drawn * ends * fray * _SubAfter;
                }

                // Pressure lines: the air the form cuts, as thin sharp lines along its outer side.
                // Each runs only part of the way along the form, the further ones shorter and
                // fainter, and they slide the way the form moves and are soon gone.
                float pressure = 0;
                if (_SubPressure > 0.001)
                {
                    float apart = lerp(0.022, 0.06, _SubSpread);
                    [unroll]
                    for (int lineAt = 0; lineAt < 6; lineAt++)
                    {
                        float h1 = Hash1(lineAt + 1.0, seed);
                        float h2 = Hash1(lineAt + 12.0, seed);
                        float h3 = Hash1(lineAt + 23.0, seed);
                        float mid = lerp(-0.75, 0.75, h2) + (k - 0.15) * 0.7;
                        // Variety (_SubVary): uneven gaps, and each line leans a little off the
                        // form's own curve, with a width and a brightness of its own.
                        float h4 = Hash1(lineAt + 34.0, seed);
                        float h5 = Hash1(lineAt + 45.0, seed);
                        float off = apart * (lineAt + 1.0) * lerp(0.8 + 0.4 * h1, 0.4 + 1.3 * h1, _SubVary)
                            + ((h4 - 0.5) * 0.26 * lerp(alongAt - mid, (frac((alongAt - mid) * 0.5 + 0.5) - 0.5) * 2.0, closed)
                                + sin(alongAt * Pi * (2.0 + round(h5 * 3.0)) + h4 * Tau) * 0.007) * _SubVary;
                        float ownWide = lerp(1.0, 0.55 + 1.1 * h5, _SubVary);
                        float ownLit = lerp(1.0, 0.45 + 0.9 * h4, _SubVary);
                        float reachAlong = lerp(0.7, 0.22, lineAt / 5.0) * (0.6 + 0.8 * h3);
                        float gone = alongAt - mid;
                        gone = lerp(abs(gone), abs(frac(gone * 0.5 + 0.5) - 0.5) * 2.0, closed);
                        // Each line is fullest at its own middle and thins to nothing at its ends.
                        float reachSoft = reachAlong * 1.25;
                        float window = lerp(
                            1 - smoothstep(reachAlong * 0.55, reachAlong, gone),
                            pow(saturate(1 - gone / reachSoft), 1.5),
                            _SubTaper);
                        float lessFar = lerp(1.0, exp(-0.3 * lineAt), _SubTaper);
                        pressure += (Line(across - off, 0.0045 * (1.0 + 3.5 * _SubSoft) * ownWide, px) * (1.0 - 0.7 * _SubSoft)
                                + Gauss(across - off, 0.014 * (1.0 + _SubSoft) * ownWide) * 0.25)
                            * window * (1.0 - 0.12 * lineAt) * lessFar * ownLit;
                    }
                    pressure *= tip * drawn * ends * fray * smoothstep(0.02, 0.09, k) * (1 - smoothstep(0.4, 0.7, k)) * _SubPressure;
                }

                // A winding ribbon: a thin band of light wound round the form in a helix, bright
                // and wide where it passes in front and dim and thin where it passes behind,
                // looser toward the stroke's start and tight at its tip; it turns with time.
                float winding = 0;
                if (_SubSpiral > 0.001)
                {
                    float swing = lerp(0.045, 0.13, _SubSpread) * lerp(lerp(1.7, 0.6, saturate(alongAt * 0.5 + 0.5)), 1.0, closed);
                    float turns = lerp(2.5, 3.0, closed);
                    float wind = alongAt * Pi * turns - t * 3.2;
                    // Variety (_SubVary): the coils crowd and open out along the form, the swing
                    // swells and shrinks, and the second strand is not the first one's mirror.
                    // The waves below close up round a closed form (whole numbers of them).
                    float w1 = Hash1(61.0, seed) * Tau;
                    float w2 = Hash1(72.0, seed) * Tau;
                    [unroll]
                    for (int strand = 0; strand < 2; strand++)
                    {
                        float crowd = (sin(alongAt * Pi * 2.0 + w1 + strand * 2.1) * 0.9
                            + sin(alongAt * Pi * 5.0 + w2 - strand * 1.3) * 0.35) * _SubVary;
                        float swell = 1.0 + (sin(alongAt * Pi + w2 + strand * 2.6) * 0.5
                            + sin(alongAt * Pi * 4.0 + w1 + strand) * 0.2) * _SubVary;
                        float at = wind + strand * (Pi + 0.9 * _SubVary) + crowd;
                        float front = cos(at) * 0.5 + 0.5;
                        float from = across - swing * swell * sin(at);
                        float wide = 0.0055 * (0.6 + 0.9 * front) * (1.0 + 2.6 * _SubSoft);
                        winding += (Line(from, wide, px) * (1.0 - 0.5 * _SubSoft) + Gauss(from, wide * 3.5) * 0.3)
                            * lerp(0.22, 1.0, front) * (strand == 0 ? 1.0 : 0.55);
                    }
                    winding *= lerp(saturate(thick * 3.0), 1.0, closed) * drawn * ends * lerp(1.0, fray, 0.35) * _SubSpiral;
                }

                // Gone before the board's edge, and in a round, so a wide halo or haze never
                // shows the board's square.
                float inBoard = BoardFade(p) * (1 - smoothstep(0.72, 1.0, r));
                float shown = saturate(k / 0.05) * inBoard * IN.color.a * _Color.a;
                float bodyCover = density * _BodyOpacity * keepBody * shown;
                float hazeCover = hazeDensity * _HazeAmount * keepHaze * shown * (1 - bodyCover);
                half3 cover = bodyTint * bodyCover + hazeTint * hazeCover;
                half3 light = lerp(tint, 1, _CoreWhite * warmth) * _CoreBright * warmth * core * tip * keepLight * shown
                    + haloTint * halo * lerp(1.0, warmth, 0.6) * keepBody * shown
                    + hazeLight * keepHaze * shown * (1 - bodyCover)
                    + lerp(tint, 1, 0.5) * rays * 1.5 * keepLight * shown
                    + lerp(tint, 1, 0.6) * sparks * 2.5 * keepLight * shown
                    + lerp(tint, 1, 0.6) * eatenRim * (density + hazeDensity * _HazeAmount * 0.5) * 2.0 * shown
                    + lerp(tint, LookHeat(0.55 * lerp(0.3, 1.0, warmth), tint), _Heat) * afterimages * 2.3 * keepHaze * shown
                    + lerp(tint, 1, 0.5 * (1.0 - 0.7 * _SubSoft)) * pressure * 2.6 * lerp(1.0, 0.75, _SubSoft) * keepLight * shown
                    + lerp(tint, 1, 0.3 * (1.0 - 0.7 * _SubSoft)) * winding * 2.0 * keepBody * shown;

                half4 color = half4(
                    (cover + light) * _Intensity,
                    saturate(bodyCover + hazeCover) * (1 - _Additive));

                // A faint noise of under one step of 8 bits, only spot there is light or cover, so
                // wide, soft glows and veils do not band (interleaved gradient noise).
                float dither = frac(52.9829189 * frac(dot(IN.vertex.xy, float2(0.06711056, 0.00583715)))) - 0.5;
                color.rgb = max(0, color.rgb + dither / 255.0 * saturate(max(color.r, max(color.g, color.b)) * 64));
                color.a = saturate(color.a + dither / 255.0 * saturate(color.a * 64));
                return color;
            }
        ENDCG
        }
    }
}
