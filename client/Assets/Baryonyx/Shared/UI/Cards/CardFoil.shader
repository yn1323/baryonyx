// カードの挿絵の手前に重ねるキラキラ（doc/art/direction.md のカードのキラキラ）。
// 挿絵と同じテクスチャを受け取り、挿絵の不透明な所にだけ、光の帯・瞬く小さな粒・模様・縁のにじみを加算で足す。
// 光り方（模様と色）は63通りあり、スキル・装備・ボーナスの種類ごとに1つを使う。
// 模様は7種類（星屑・コスモ・氷の結晶・光沢・波紋・オーロラ・格子）、色は9種類で、光り方の番号を7で割った余りが模様、商が色。
// 値は頂点色で受け取る：r は段階÷5、g はカードごとの乱数の種、b は光り方の番号÷255、a は全体の濃さ（CanvasGroupの透け）。
// 値として読むため、UI/Default と違って頂点色をガンマ補正しない。
Shader "Baryonyx/UI/CardFoil"
{
    Properties
    {
        [PerRendererData] _MainTex ("Card Art", 2D) = "white" {}

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
        // 光を下の色へ足し、描き先のアルファは変えない。
        Blend One One, Zero One
        ColorMask [_ColorMask]

        Pass
        {
            Name "CardFoil"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 mask     : TEXCOORD1;
                float2 local    : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            // 撮影用に時刻を足す（ふだんは0）。Editorが背面で時間が止まっていても、止めたコマを撮れる。
            float _CardFoilClock;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.vertex = vPosition;
                float2 pixelSize = vPosition.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
                OUT.local = v.texcoord.xy;
                OUT.mask = float4(
                    v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));
                OUT.color = v.color;
                return OUT;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 Hash2(float2 p)
            {
                float n = Hash(p);
                return float2(n, Hash(p + n * 17.0));
            }

            float3 Rainbow(float h)
            {
                return saturate(abs(frac(h + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
            }

            // 色の組：粒と模様は A と B の間の色で光る。7番は虹色。
            static const float3 PaletteA[9] =
            {
                float3(1.00, 0.82, 0.42), // 金
                float3(0.80, 0.90, 1.00), // 銀
                float3(1.00, 0.55, 0.75), // 薔薇
                float3(0.45, 0.95, 1.00), // 水
                float3(0.50, 1.00, 0.62), // 翠
                float3(0.72, 0.55, 1.00), // 菫
                float3(1.00, 0.58, 0.28), // 焔
                float3(1.00, 1.00, 1.00), // 虹
                float3(0.58, 0.80, 1.00), // 薄氷と桜
            };

            static const float3 PaletteB[9] =
            {
                float3(1.00, 0.97, 0.85),
                float3(1.00, 1.00, 1.00),
                float3(1.00, 0.88, 0.96),
                float3(0.85, 1.00, 1.00),
                float3(0.90, 1.00, 0.80),
                float3(0.95, 0.85, 1.00),
                float3(1.00, 0.92, 0.65),
                float3(1.00, 1.00, 1.00),
                float3(1.00, 0.75, 0.90),
            };

            float3 Tint(int palette, float h)
            {
                float3 c = lerp(PaletteA[palette], PaletteB[palette], h);
                return palette == 7 ? lerp(Rainbow(h), float3(1, 1, 1), 0.3) : c;
            }

            // 斜めに横切る光の帯：周期 period 秒に1回、中心 offset だけずらして通る。
            float Sweep(float2 uv, float t, float period, float offset, float width)
            {
                float d = uv.x * 0.8 + (1.0 - uv.y) * 0.6;
                float pos = frac(t / period + offset) * 2.6 - 0.8;
                float x = (d - pos) / width;
                return exp(-x * x);
            }

            // 瞬く小さな粒：挿絵のドット（テクセル）で測った cell ごとに1粒まで。粒は1ドットに満たない大きさで、
            // 粒ごとに速さと位相をずらして、明るくなる・暗くなるをくり返す。暗いときも dim の明るさは残し、
            // 止めたコマでも粒が見えるようにする。
            // 粒はマスの中ほどにだけ置くため、隣のマスを調べなくてよい。flow は流す量（ドット）。
            float3 Twinkle(float2 px, float t, float seed, float cell, float chance, float size,
                int palette, float2 flow, float dim)
            {
                float2 grid = (px - flow) / cell;
                float2 id = floor(grid);
                float2 h = Hash2(id + seed * 31.7);
                if (h.x > chance)
                    return 0;
                float2 at = 0.3 + 0.4 * Hash2(id + 7.1 + seed);
                float2 d = (frac(grid) - at) * cell;
                float s = size * (0.6 + 0.8 * h.y);
                float core = exp(-dot(d, d) / (s * s));
                float phase = Hash(id + 3.3) * 6.2831;
                float speed = 1.3 + 2.2 * Hash(id + 9.9);
                float w = 0.5 + 0.5 * sin(t * speed + phase);
                float twinkle = dim + (1.0 - dim) * w * w * w;
                return Tint(palette, Hash(id + 1.7)) * core * twinkle;
            }

            // 0 星屑：粒より細かい点が一面に散って瞬く（スターライトのホロ）。
            float3 Stardust(float2 px, float t, float seed, int palette)
            {
                return Twinkle(px, t * 1.4, seed + 0.37, 1.7, 0.42, 0.2, palette, 0, 0.08) * 0.9;
            }

            // 1 コスモ：大小の輪と丸が浮かび、ゆっくり明滅する。色は場所と時間でずれる虹。
            float3 Cosmos(float2 px, float2 uv, float t, float seed, int palette)
            {
                const float cell = 9.0;
                float2 grid = px / cell;
                float2 cellId = floor(grid);
                float sum = 0;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 id = cellId + float2(x, y);
                    if (Hash(id + seed * 5.1) > 0.5)
                        continue;
                    float2 at = id + 0.2 + 0.6 * Hash2(id + 2.7);
                    float r = (0.2 + 0.45 * Hash(id + 4.4)) * cell;
                    float d = length((grid - at) * cell);
                    float ring = exp(-pow((d - r) / 0.45, 2.0));
                    float fill = exp(-d * d / (r * r)) * 0.25;
                    sum += (ring + fill) * (0.35 + 0.65 * pow(0.5 + 0.5 * sin(t * 0.9 + Hash(id) * 6.2831), 2.0));
                }
                float hue = uv.x * 0.8 + uv.y * 0.6 + t * 0.05;
                return lerp(Tint(palette, frac(hue)), Rainbow(hue), 0.5) * sum;
            }

            // 2 氷の結晶：割れた氷のような面が、1枚ずつ光を拾って光り、細い割れ目が瞬く（クラックアイス）。
            float3 Crystal(float2 px, float2 uv, float t, float seed, int palette)
            {
                float2 grid = px / 7.0;
                float2 cellId = floor(grid);
                float nearest = 9, runnerUp = 9;
                float2 nearId = 0;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 id = cellId + float2(x, y);
                    float2 at = id + Hash2(id + seed * 3.9);
                    float d = length(grid - at);
                    if (d < nearest)
                    {
                        runnerUp = nearest;
                        nearest = d;
                        nearId = id;
                    }
                    else if (d < runnerUp)
                        runnerUp = d;
                }
                float h = Hash(nearId + 6.6);
                float face = pow(0.5 + 0.5 * sin(t * 0.8 + h * 6.2831 + uv.x * 2.5 - uv.y * 1.5), 5.0);
                float crack = exp(-pow((runnerUp - nearest) / 0.05, 2.0)) * (0.4 + 0.6 * (0.5 + 0.5 * sin(t * 1.7 + h * 9.0)));
                return Tint(palette, h) * (face * 0.6 + crack * 0.7);
            }

            // 3 光沢：模様のない、広く柔らかな光がゆっくり往復する。細い横の筋で金属の照りを出す（シーン）。
            float3 Sheen(float2 px, float2 uv, float t, int palette)
            {
                float g = uv.x * 0.75 + uv.y * 0.45;
                float pos = 0.6 + 0.75 * sin(t * 0.45);
                float broad = exp(-pow((g - pos) / 0.22, 2.0));
                float lines = 0.75 + 0.25 * sin(px.y * 3.14159);
                return lerp(Tint(palette, frac(g + t * 0.05)), Rainbow(g * 0.8 + t * 0.04), 0.35) * broad * lines;
            }

            // 4 波紋：上の縁の一点から、細い虹の輪が外へ広がり続ける。
            float3 Ripple(float2 uv, float aspect, float t, float seed, int palette)
            {
                float2 center = float2(0.15 + 0.7 * seed, 1.1);
                float d = length((uv - center) * float2(aspect, 1.0));
                float wave = pow(0.5 + 0.5 * sin(d * 46.0 - t * 2.4), 12.0);
                float fade = exp(-d * 0.9);
                return lerp(Tint(palette, frac(d * 1.5)), Rainbow(d * 1.8 - t * 0.08), 0.45) * wave * fade;
            }

            // 5 オーロラ：揺れる縦の光の幕が、上ほど明るく、色を移しながら流れる。
            float3 Aurora(float2 uv, float t, int palette)
            {
                float x = uv.x + 0.06 * sin(uv.y * 7.0 + t * 0.8) + 0.035 * sin(uv.y * 13.0 - t * 1.1);
                float curtain = pow(0.5 + 0.5 * sin(x * 19.0 + t * 0.45), 6.0);
                float top = smoothstep(0.05, 0.9, uv.y);
                return Tint(palette, 0.5 + 0.5 * sin(x * 5.0 + t * 0.3)) * curtain * top;
            }

            // 6 格子：斜めの細い格子に沿って光が走り、交点がきらめく。
            float3 Lattice(float2 px, float t, int palette)
            {
                float2 q = float2(px.x + px.y, px.x - px.y) / 6.0;
                float2 f = frac(q);
                float2 d = min(f, 1.0 - f);
                float lineD = min(d.x, d.y);
                float lineGlow = exp(-pow(lineD / 0.045, 2.0));
                float run = pow(0.5 + 0.5 * sin((q.x + q.y) * 0.7 - t * 2.2), 8.0)
                    + pow(0.5 + 0.5 * sin((q.x - q.y) * 0.55 + t * 1.6), 8.0);
                float joint = exp(-dot(d, d) / 0.006) * (0.5 + 0.5 * sin(t * 2.0 + Hash(floor(q)) * 6.2831));
                return Tint(palette, frac((q.x + q.y) * 0.05)) * (lineGlow * run * 0.6 + joint * 0.8);
            }

            float4 frag(v2f IN) : SV_Target
            {
                float4 art = tex2D(_MainTex, IN.texcoord);
                float level = round(IN.color.r * 5.0);
                float seed = IN.color.g;
                // 整数の割り算は遅いことがあるため、小数で求めてから整数にする。
                float styleIndex = fmod(round(IN.color.b * 255.0), 63.0);
                float paletteIndex = floor(styleIndex / 7.0 + 0.001);
                int pattern = (int)(styleIndex - paletteIndex * 7.0 + 0.5);
                int palette = (int)(paletteIndex + 0.5);
                float strength = IN.color.a;
                // 段階1には何も足さない。挿絵の透けた所にも足さない。
                float cover = step(1.5, level) * step(0.5, art.a) * strength;
                if (cover <= 0)
                    return 0;

                float2 uv = IN.local;
                // 挿絵のドットで測った位置。粒の大きさを、挿絵の大きさによらずドットにそろえる。
                float2 px = IN.texcoord * _MainTex_TexelSize.zw;
                float aspect = _MainTex_TexelSize.z > 0 ? _MainTex_TexelSize.z / _MainTex_TexelSize.w : 1.1;
                float t = _Time.y + _CardFoilClock + seed * 37.0;
                float lum = dot(art.rgb, float3(0.299, 0.587, 0.114));
                bool top = level >= 5;
                float3 light = 0;

                // 光の帯（段階2から）。Sは細い2本目が後を追う。帯の色は光り方の色に寄せる。
                float period = top ? 2.8 : 3.8;
                float band = Sweep(uv, t, period, 0.0, top ? 0.07 : 0.06);
                if (top)
                    band += 0.6 * Sweep(uv, t, period, -0.07, 0.033);
                float3 bandColor = lerp(float3(1.0, 0.97, 0.9), PaletteB[palette], 0.5);
                light += bandColor * band * (top ? 0.5 : 0.4) * (0.55 + 0.45 * lum);

                // 瞬く小さな粒（段階3から）。Sは粒が多く、ゆっくり上へ流れる2層目が重なる。
                // Aも粒を多めにし、暗いときも少し光らせて、止めたコマでもキラキラして見えるようにする。
                if (level >= 3)
                {
                    light += Twinkle(px, t, seed, 3.0, top ? 0.5 : 0.38, top ? 0.36 : 0.36, palette, 0,
                        top ? 0.15 : 0.3) * (top ? 1.0 : 0.95);
                    if (top)
                    {
                        float2 flow = float2(sin(t * 0.5 + seed * 6.0) * 1.2, t * 1.6);
                        light += Twinkle(px, t * 0.8, seed + 0.53, 4.0, 0.35, 0.42, palette, flow, 0.08) * 0.75;
                    }
                    // 光の芯：まばらな粒を、いつも明るめに光らせる（1ドットに満たない大きさのまま）。
                    light += Twinkle(px, t * 0.6, seed + 0.91, top ? 6.0 : 7.0, top ? 0.32 : 0.26, 0.5,
                        palette, 0, 0.55) * (top ? 0.9 : 0.65);
                }

                // 光り方ごとの模様（段階3から）。挿絵全体に掛かる層なので薄くし、Sでも挿絵の色を残す。
                if (level >= 3)
                {
                    float3 motif;
                    if (pattern == 0)
                        motif = Stardust(px, t, seed, palette) * 1.2;
                    else if (pattern == 1)
                        motif = Cosmos(px, uv, t, seed, palette) * 0.48;
                    else if (pattern == 2)
                        motif = Crystal(px, uv, t, seed, palette) * 0.38;
                    else if (pattern == 3)
                        motif = Sheen(px, uv, t, palette) * 0.42;
                    else if (pattern == 4)
                        motif = Ripple(uv, aspect, t, seed, palette) * 0.5;
                    else if (pattern == 5)
                        motif = Aurora(uv, t, palette) * 0.4;
                    else
                        motif = Lattice(px, t, palette) * 0.5;
                    light += motif * (top ? 1.0 : 0.65) * (0.45 + 0.55 * lum);
                }

                // 挿絵の縁のにじみ（段階3から）：上と左右の縁を、ゆっくり脈打つ光り方の色で。Aは細く薄く。
                if (level >= 3)
                {
                    float edge = min(min(uv.x, 1.0 - uv.x), 1.0 - uv.y);
                    float rim = exp(-edge * (top ? 28.0 : 40.0)) * (0.55 + 0.45 * sin(t * 2.2));
                    light += lerp(float3(1.0, 0.78, 0.32), PaletteA[palette], 0.6) * rim * (top ? 0.4 : 0.2);
                }

                float4 color = float4(light * cover, 0);
                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                color.rgb *= m.x * m.y;
                #endif
                return color;
            }
        ENDCG
        }
    }
}
