// カードの挿絵の手前に重ねるキラキラ（doc/art/direction.md のカードのキラキラ）。
// 挿絵と同じテクスチャを受け取り、挿絵の不透明な所にだけ、光の帯・瞬く小さな粒・模様・縁のにじみを加算で足す。
// 4つの層は、カードを手の中でゆっくり傾ける1つの動きでまとめて動かし、照りが通った所の粒が光を拾って光る。
// 光り方（模様と色）は63通りあり、スキル・装備・ボーナスの種類ごとに1つを使う。
// 模様は7種類（星屑・コスモ・氷の結晶・光沢・波紋・オーロラ・格子）、色は9種類で、光り方の番号を7で割った余りが模様、商が色。
// 値は頂点色で受け取る：r は段階÷5、g はカードごとの乱数の種、b は（光り方の番号＋0.5）÷63、a は全体の濃さ（CanvasGroupの透け）。
// 線形の色空間ではCanvasが頂点色を線形へ変えて渡すため、CardFoil.cs が先に逆へ変えて渡す。ここでは値をそのまま読む。
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

            // 斜めの光の帯：照りの中心 pos からの距離 d（どちらもカードの斜めの座標）で、柔らかい帯にする。
            float Band(float d, float pos, float width)
            {
                float x = (d - pos) / width;
                return exp(-x * x);
            }

            // 照りの近さ（0〜1）。照りの進む先は狭く（来る直前に光り始める）、通った後ろは長く尾を引く（余韻）。
            // dir は照りの進む向き（1 か -1）。
            float Glint(float d, float pos, float dir, float ahead, float behind)
            {
                float x = (d - pos) * dir;
                float w = x > 0 ? ahead : behind;
                return exp(-(x / w) * (x / w));
            }

            // 瞬く小さな粒：挿絵のドット（テクセル）で測った cell ごとに1粒まで。粒は1ドットに満たない大きさで、
            // 粒ごとに速さと位相をずらして、明るくなる・暗くなるをくり返す。暗いときも dim の明るさは残し、
            // 止めたコマでも粒が見えるようにする。
            // 照りが通ると（glint）、粒は箔のかけらのように光を拾って白く光る。粒ごとに反応の鋭さと強さを変え、
            // 一斉には光らせない。ふだんは見えず、照りが通るときだけ光る粒も混ぜる（chance の6割ぶん）。
            // 粒はマスの中ほどにだけ置くため、隣のマスを調べなくてよい。flow は流す量（ドット）。
            float3 Twinkle(float2 px, float t, float seed, float cell, float chance, float size,
                int palette, float2 flow, float dim, float glint, float flash)
            {
                float2 grid = (px - flow) / cell;
                float2 id = floor(grid);
                float2 h = Hash2(id + seed * 31.7);
                if (h.x > chance * 1.6)
                    return 0;
                float hidden = step(chance, h.x);
                float2 at = 0.3 + 0.4 * Hash2(id + 7.1 + seed);
                float2 d = (frac(grid) - at) * cell;
                float s = size * (0.6 + 0.8 * h.y);
                float core = exp(-dot(d, d) / (s * s));
                float phase = Hash(id + 3.3) * 6.2831;
                float speed = 1.3 + 2.2 * Hash(id + 9.9);
                float w = 0.5 + 0.5 * sin(t * speed + phase);
                float twinkle = (dim + (1.0 - dim) * w * w * w) * (1.0 - hidden);
                float catchLight = flash * pow(glint, 1.0 + 3.0 * Hash(id + 5.5)) * (0.5 + Hash(id + 8.1));
                float3 tint = Tint(palette, Hash(id + 1.7));
                return core * (tint * twinkle + lerp(tint, 1.0, 0.5) * catchLight);
            }

            // 0 星屑：粒より細かい点が一面に散って瞬く（スターライトのホロ）。
            float3 Stardust(float2 px, float t, float seed, int palette, float glint)
            {
                return Twinkle(px, t * 1.4, seed + 0.37, 1.7, 0.42, 0.2, palette, 0, 0.08, glint, 1.5) * 0.9;
            }

            // 1 コスモ：大小の輪と丸が浮かび、ゆっくり明滅する。色は場所と傾きでずれる虹。
            float3 Cosmos(float2 px, float2 uv, float t, float tilt, float seed, int palette)
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
                float hue = uv.x * 0.8 + uv.y * 0.6 + t * 0.03 + tilt * 0.3;
                return lerp(Tint(palette, frac(hue)), Rainbow(hue), 0.5) * sum;
            }

            // 2 氷の結晶：割れた氷のような面が、傾きに合わせて1枚ずつ光を拾って光り、細い割れ目が瞬く（クラックアイス）。
            float3 Crystal(float2 px, float2 uv, float t, float tilt, float seed, int palette)
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
                float face = pow(0.5 + 0.5 * cos(tilt * 4.0 + h * 6.2831 + uv.x * 1.5 - uv.y * 1.0), 5.0);
                float crack = exp(-pow((runnerUp - nearest) / 0.05, 2.0)) * (0.4 + 0.6 * (0.5 + 0.5 * sin(t * 1.7 + h * 9.0)));
                return Tint(palette, h) * (face * 0.6 + crack * 0.7);
            }

            // 3 光沢：模様のない、広く柔らかな光が傾きに合わせて往復する。細い横の筋で金属の照りを出す（シーン）。
            float3 Sheen(float2 px, float2 uv, float t, float tilt, int palette)
            {
                float g = uv.x * 0.75 + uv.y * 0.45;
                float pos = 0.6 + 0.75 * tilt;
                float broad = exp(-pow((g - pos) / 0.22, 2.0));
                float lines = 0.75 + 0.25 * sin(px.y * 3.14159);
                return lerp(Tint(palette, frac(g + tilt * 0.2)), Rainbow(g * 0.8 + tilt * 0.25), 0.35) * broad * lines;
            }

            // 4 波紋：上の縁の一点から、細い虹の輪が外へ広がり続ける。
            float3 Ripple(float2 uv, float aspect, float t, float tilt, float seed, int palette)
            {
                float2 center = float2(0.15 + 0.7 * seed, 1.1);
                float d = length((uv - center) * float2(aspect, 1.0));
                float wave = pow(0.5 + 0.5 * sin(d * 46.0 - t * 2.4), 12.0);
                float fade = exp(-d * 0.9);
                return lerp(Tint(palette, frac(d * 1.5)), Rainbow(d * 1.8 - t * 0.04 + tilt * 0.25), 0.45) * wave * fade;
            }

            // 5 オーロラ：揺れる縦の光の幕が、上ほど明るく、色を移しながら流れる。
            float3 Aurora(float2 uv, float t, float tilt, int palette)
            {
                float x = uv.x + 0.06 * sin(uv.y * 7.0 + t * 0.8) + 0.035 * sin(uv.y * 13.0 - t * 1.1);
                float curtain = pow(0.5 + 0.5 * sin(x * 19.0 + t * 0.45), 6.0);
                float top = smoothstep(0.05, 0.9, uv.y);
                return Tint(palette, 0.5 + 0.5 * sin(x * 5.0 + t * 0.15 + tilt * 1.2)) * curtain * top;
            }

            // 6 格子：斜めの細い格子に沿って光が走り、交点がきらめく。
            float3 Lattice(float2 px, float t, float tilt, int palette)
            {
                float2 q = float2(px.x + px.y, px.x - px.y) / 6.0;
                float2 f = frac(q);
                float2 d = min(f, 1.0 - f);
                float lineD = min(d.x, d.y);
                float lineGlow = exp(-pow(lineD / 0.045, 2.0));
                float run = pow(0.5 + 0.5 * sin((q.x + q.y) * 0.7 - t * 2.2), 8.0)
                    + pow(0.5 + 0.5 * sin((q.x - q.y) * 0.55 + t * 1.6), 8.0);
                float joint = exp(-dot(d, d) / 0.006) * (0.5 + 0.5 * sin(t * 2.0 + Hash(floor(q)) * 6.2831));
                return Tint(palette, frac((q.x + q.y) * 0.05 + tilt * 0.2)) * (lineGlow * run * 0.6 + joint * 0.8);
            }

            float4 frag(v2f IN) : SV_Target
            {
                float4 art = tex2D(_MainTex, IN.texcoord);
                float level = round(IN.color.r * 5.0);
                float seed = IN.color.g;
                // 整数の割り算は遅いことがあるため、小数で求めてから整数にする。
                float styleIndex = min(floor(IN.color.b * 63.0), 62.0);
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

                // 1つの傾き：手の中でカードをゆっくり傾け、戻すくり返し（-1〜1）。端ではゆっくり、真ん中では速く動き、
                // period 秒に1回、照りがカードを斜めに横切る（行きと帰りで向きが変わる）。
                // 光の帯・粒が光を拾う瞬間・模様の色のずれ・縁の光は、すべてこの傾きから決める。
                float period = top ? 2.8 : 3.8;
                float phase = t * 3.14159265 / period;
                float tilt = sin(phase);
                float dir = cos(phase) >= 0 ? 1.0 : -1.0;
                float diagonal = uv.x * 0.8 + (1.0 - uv.y) * 0.6;
                float shine = 0.7 + 1.15 * tilt;
                // 照りの近さ：粒用は狭く鋭く、模様と縁用は広く柔らかく。
                float glint = Glint(diagonal, shine, dir, top ? 0.15 : 0.13, top ? 0.42 : 0.38);
                float broad = Glint(diagonal, shine, dir, 0.35, 0.6);

                // 光の帯（段階2から）。Sは細い2本目が後を追う。帯の色は光り方の色に寄せる。
                // 照りの明るさは粒が受け持つため、帯は灰色のもやにならないよう控えめにする。
                float band = Band(diagonal, shine, top ? 0.07 : 0.06);
                if (top)
                    band += 0.6 * Band(diagonal, shine - dir * 0.12, 0.033);
                float3 bandColor = lerp(float3(1.0, 0.97, 0.9), PaletteB[palette], 0.5);
                light += bandColor * band * (top ? 0.4 : 0.3) * (0.55 + 0.45 * lum);

                // 瞬く小さな粒（段階3から）。Sは粒が多く、ゆっくり上へ流れる2層目が重なる。
                // Aも粒を多めにし、暗いときも少し光らせて、止めたコマでもキラキラして見えるようにする。
                // 照りが通ると、粒が光を拾って白く光る。
                if (level >= 3)
                {
                    light += Twinkle(px, t, seed, 3.0, top ? 0.5 : 0.38, top ? 0.36 : 0.36, palette, 0,
                        top ? 0.15 : 0.3, glint, top ? 1.8 : 2.0) * (top ? 1.0 : 0.95);
                    if (top)
                    {
                        float2 flow = float2(sin(t * 0.5 + seed * 6.0) * 1.2, t * 1.6);
                        light += Twinkle(px, t * 0.8, seed + 0.53, 4.0, 0.35, 0.42, palette, flow, 0.08,
                            glint, 1.4) * 0.75;
                    }
                    // 光の芯：まばらな粒を、いつも明るめに光らせる（1ドットに満たない大きさのまま）。
                    light += Twinkle(px, t * 0.6, seed + 0.91, top ? 6.0 : 7.0, top ? 0.32 : 0.26, 0.5,
                        palette, 0, 0.55, glint, 1.5) * (top ? 0.9 : 0.65);
                }

                // 光り方ごとの模様（段階3から）。挿絵全体に掛かる層なので薄くし、Sでも挿絵の色を残す。
                // 照りの近くほど強く出し、離れた所は沈める。
                if (level >= 3)
                {
                    float3 motif;
                    if (pattern == 0)
                        motif = Stardust(px, t, seed, palette, glint) * 1.2;
                    else if (pattern == 1)
                        motif = Cosmos(px, uv, t, tilt, seed, palette) * 0.48;
                    else if (pattern == 2)
                        motif = Crystal(px, uv, t, tilt, seed, palette) * 0.38;
                    else if (pattern == 3)
                        motif = Sheen(px, uv, t, tilt, palette) * 0.42;
                    else if (pattern == 4)
                        motif = Ripple(uv, aspect, t, tilt, seed, palette) * 0.5;
                    else if (pattern == 5)
                        motif = Aurora(uv, t, tilt, palette) * 0.4;
                    else
                        motif = Lattice(px, t, tilt, palette) * 0.5;
                    light += motif * (top ? 1.0 : 0.65) * (0.45 + 0.55 * lum) * (0.55 + 0.9 * broad);
                }

                // ホロの虹（段階3から）：挿絵全体に、斜めの細い筋に沿った虹がうっすら掛かり、照りの近くで濃くなる。
                // 色は場所と傾きでずれる。挿絵が白くかすまないよう薄くし、Sのほうを少し濃くする。
                if (level >= 3)
                {
                    float grain = 0.6 + 0.4 * sin((px.x * 0.9 - px.y * 0.5) * 2.1);
                    float3 holo = Rainbow(diagonal * 1.6 - tilt * 0.5 + 0.08 * sin(px.y * 0.7));
                    light += holo * grain * (0.3 + 0.7 * broad) * (top ? 0.12 : 0.07) * (0.4 + 0.6 * lum);
                }

                // 挿絵の縁のにじみ（Sだけ）：上と左右の縁を、ゆっくり脈打つ光り方の色で。
                // Aは枠の金の線を目立たせるため出さない（2026-10-23、ユーザーの判断）。
                if (top)
                {
                    float edge = min(min(uv.x, 1.0 - uv.x), 1.0 - uv.y);
                    float rim = exp(-edge * 28.0) * (0.55 + 0.45 * sin(t * 2.2));
                    light += lerp(float3(1.0, 0.78, 0.32), PaletteA[palette], 0.6) * rim * 0.4;
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
