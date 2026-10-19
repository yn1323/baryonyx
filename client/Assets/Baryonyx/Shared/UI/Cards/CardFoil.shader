// カードの挿絵の手前に重ねるキラキラ（doc/art/direction.md のカードのキラキラ）。
// 挿絵と同じテクスチャを受け取り、挿絵の不透明な所にだけ、光の帯・ラメ・ホロの虹色・縁の金のにじみを加算で足す。
// 値は頂点色で受け取る：r はレア度÷5、g はカードごとの乱数の種、a は全体の濃さ（CanvasGroupの透け）。
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

            // 斜めに横切る光の帯：周期 period 秒に1回、中心 offset だけずらして通る。
            float Sweep(float2 uv, float t, float period, float offset, float width)
            {
                float d = uv.x * 0.8 + (1.0 - uv.y) * 0.6;
                float pos = frac(t / period + offset) * 2.6 - 0.8;
                float x = (d - pos) / width;
                return exp(-x * x);
            }

            // ラメ：格子のマスごとに1粒。近くの9マスだけを調べる。
            // cells は横のマス数、chance は粒のあるマスの割合、star は大きい粒に十字の光条を足す強さ。
            float3 Glitter(float2 uv, float aspect, float t, float seed, float cells, float chance, float star)
            {
                float2 grid = uv * float2(cells, cells / aspect);
                float2 cell = floor(grid);
                float3 sum = 0;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 id = cell + float2(x, y);
                    float2 h = Hash2(id + seed * 31.7);
                    if (h.x > chance)
                        continue;
                    float2 at = id + 0.2 + 0.6 * Hash2(id + 7.1 + seed);
                    float2 d = grid - at;
                    float phase = Hash(id + 3.3) * 6.2831;
                    float speed = 1.4 + 2.2 * Hash(id + 9.9);
                    // 一瞬だけ強く光る。
                    float blink = pow(saturate(sin(t * speed + phase)), 7.0);
                    float big = step(0.72, Hash(id + 5.5));
                    float r = length(d);
                    float dot0 = exp(-r * r * (big > 0 ? 22.0 : 45.0));
                    float rays = star * big * (exp(-abs(d.x) * 14.0) * exp(-abs(d.y) * 2.2)
                        + exp(-abs(d.y) * 14.0) * exp(-abs(d.x) * 2.2));
                    float3 tint = lerp(float3(1, 1, 1), Rainbow(Hash(id + 1.7)), 0.35);
                    sum += tint * blink * (dot0 * 1.6 + rays);
                }
                return sum;
            }

            // 漂う光の粒：格子ごと上へ流し、横に揺らす。層ごとに大きさと速さを変え、奥行きを出す。
            // 粒は近くの9マスだけを調べる。star は十字の光条の強さ。
            float3 Drift(float2 uv, float aspect, float t, float seed, int layers, float chance, float star)
            {
                float3 sum = 0;
                [unroll] for (int l = 0; l < 3; l++)
                {
                    if (l >= layers)
                        break;
                    float cells = 5.0 + l * 3.0;
                    float speed = 0.07 + l * 0.05;
                    float2 flow = float2(sin(t * 0.6 + l * 2.1) * 0.04, -t * speed);
                    float2 grid = (uv + flow) * float2(cells, cells / aspect);
                    float2 cell = floor(grid);
                    [unroll] for (int y = -1; y <= 1; y++)
                    [unroll] for (int x = -1; x <= 1; x++)
                    {
                        float2 id = cell + float2(x, y);
                        if (Hash(id + seed * 13.1 + l * 7.7) > chance)
                            continue;
                        float2 at = id + 0.15 + 0.7 * Hash2(id + l * 3.3 + seed);
                        // 粒ごとに小さく円を描いて揺れる。
                        float wob = Hash(id + 2.2) * 6.2831;
                        at += 0.18 * float2(sin(t * 1.3 + wob), cos(t * 1.1 + wob));
                        float2 d = grid - at;
                        float r2 = dot(d, d);
                        // ふわっと明るくなって消えるのを、粒ごとにずらしてくり返す。
                        float pulse = pow(saturate(sin(t * (0.9 + Hash(id + 4.4)) + wob)), 3.0);
                        float size = 1.0 - l * 0.25;
                        float glow = exp(-r2 * 30.0 / size) + 0.35 * exp(-r2 * 6.0 / size);
                        float rays = star * (exp(-abs(d.x) * 16.0) * exp(-abs(d.y) * 2.6)
                            + exp(-abs(d.y) * 16.0) * exp(-abs(d.x) * 2.6)) * size;
                        float3 tint = lerp(float3(1, 0.97, 0.88), Rainbow(Hash(id + 8.8)), 0.25);
                        sum += tint * pulse * (glow + rays) * (0.7 + 0.3 * size);
                    }
                }
                return sum;
            }

            float4 frag(v2f IN) : SV_Target
            {
                float4 art = tex2D(_MainTex, IN.texcoord);
                float rarity = round(IN.color.r * 5.0);
                float seed = IN.color.g;
                float strength = IN.color.a;
                // ☆1 には何も足さない。挿絵の透けた所にも足さない。
                float cover = step(1.5, rarity) * step(0.5, art.a) * strength;
                if (cover <= 0)
                    return 0;

                float2 uv = IN.local;
                float aspect = _MainTex_TexelSize.z > 0 ? _MainTex_TexelSize.z / _MainTex_TexelSize.w : 1.1;
                float t = _Time.y + _CardFoilClock + seed * 37.0;
                float lum = dot(art.rgb, float3(0.299, 0.587, 0.114));
                float3 light = 0;

                // 光の帯（☆2から）。☆5は細い2本目が後を追う。
                float sweepPower = rarity >= 5 ? 0.85 : rarity >= 4 ? 0.65 : rarity >= 3 ? 0.5 : 0.32;
                float period = rarity >= 5 ? 2.8 : 3.8;
                float band = Sweep(uv, t, period, 0.0, rarity >= 5 ? 0.075 : 0.06);
                if (rarity >= 5)
                    band += 0.6 * Sweep(uv, t, period, -0.07, 0.035);
                light += float3(1.0, 0.97, 0.9) * band * sweepPower * (0.55 + 0.45 * lum);

                // ラメ（☆3から）。☆5は粒が多く、大きい粒に十字の光条。
                if (rarity >= 3)
                {
                    float cells = rarity >= 5 ? 14.0 : rarity >= 4 ? 11.0 : 9.0;
                    float chance = rarity >= 5 ? 0.7 : rarity >= 4 ? 0.55 : 0.4;
                    float star = rarity >= 5 ? 0.9 : 0.0;
                    light += Glitter(uv, aspect, t, seed, cells, chance, star) * (rarity >= 5 ? 1.0 : 0.75);
                }

                // 漂う光の粒（☆3から）。☆が上がるほど層と粒が増え、☆5は十字の光条つき。
                if (rarity >= 3)
                {
                    int layers = rarity >= 5 ? 3 : rarity >= 4 ? 2 : 1;
                    float chance = rarity >= 5 ? 0.6 : rarity >= 4 ? 0.45 : 0.3;
                    light += Drift(uv, aspect, t, seed, layers, chance, rarity >= 5 ? 0.8 : 0.0)
                        * (rarity >= 5 ? 0.9 : 0.7);
                }

                // ホロの虹色（☆4から）：位置と時間でずれる虹を、細かい縞で変調し、明るい所ほど強く。
                if (rarity >= 4)
                {
                    float hue = uv.x * 0.9 + (1.0 - uv.y) * 0.55 + t * 0.06;
                    float stripes = 0.75 + 0.25 * sin((uv.x * 1.3 + uv.y) * 38.0 + t * 1.5);
                    float holo = (rarity >= 5 ? 0.3 : 0.2) * stripes * (0.35 + 0.65 * lum);
                    light += Rainbow(hue) * holo;
                }

                // 挿絵の縁の金のにじみ（☆5）：上と左右の縁を、ゆっくり脈打つ金で。
                if (rarity >= 5)
                {
                    float edge = min(min(uv.x, 1.0 - uv.x), 1.0 - uv.y);
                    float rim = exp(-edge * 28.0) * (0.55 + 0.45 * sin(t * 2.2));
                    light += float3(1.0, 0.78, 0.32) * rim * 0.55;
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
