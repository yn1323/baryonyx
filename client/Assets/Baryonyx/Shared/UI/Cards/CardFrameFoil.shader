// カードの枠の品質の光（doc/art/direction.md のカードのキラキラ）。
// 枠の画像（70×98ドット）をそのまま描き、品質A・Sのときだけ、2ドットの金属の部分の色を変える。太さと形は変えない。
// A：外側の1ドットの線を金にし、照りを流す。内側の線は属性の色のまま。
// S：金属の部分を4辺とも虹にし、属性の色を3割残す。照りを流す。
// 値は頂点色で受け取る：r は光り方（1 なし、0.5 金、0 虹。白なら元の枠のまま）、g はカードごとの乱数の種、a は濃さ。
// 線形の色空間ではCanvasが頂点色を線形へ変えて渡す（0.5は約0.21になる）ため、r は区切りの値で見分ける。
Shader "Baryonyx/UI/CardFrameFoil"
{
    Properties
    {
        [PerRendererData] _MainTex ("Card Frame", 2D) = "white" {}

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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "CardFrameFoil"
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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            // 撮影用に時刻を足す（ふだんは0）。カードのキラキラと共通。
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
                OUT.mask = float4(
                    v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));
                OUT.color = v.color;
                return OUT;
            }

            float3 Rainbow(float h)
            {
                return saturate(abs(frac(h + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
            }

            float Luma(float3 c)
            {
                return dot(c, float3(0.299, 0.587, 0.114));
            }

            // 枠に沿った位置（左下から反時計回りに0〜1）。いちばん近い辺で決める。
            float Perimeter(float2 dots, float2 size)
            {
                float left = dots.x, right = size.x - dots.x, bottom = dots.y, top = size.y - dots.y;
                float nearest = min(min(left, right), min(bottom, top));
                float s;
                if (nearest == bottom)
                    s = dots.x;
                else if (nearest == right)
                    s = size.x + dots.y;
                else if (nearest == top)
                    s = size.x + size.y + (size.x - dots.x);
                else
                    s = 2.0 * size.x + size.y + (size.y - dots.y);
                return s / (2.0 * (size.x + size.y));
            }

            float4 frag(v2f IN) : SV_Target
            {
                float4 frame = tex2D(_MainTex, IN.texcoord);
                // 計算はガンマ空間の色で行い（見本と同じ見た目にする）、最後に戻す。
                float3 o = frame.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                o = LinearToGammaSpace(o);
                #endif
                float mode = IN.color.r > 0.75 ? 0.0 : (IN.color.r < 0.1 ? 2.0 : 1.0);
                float seed = IN.color.g;
                float3 c = o;

                float2 size = _MainTex_TexelSize.zw;
                float2 dots = IN.texcoord * size;
                float edge = min(min(dots.x, size.x - dots.x), min(dots.y, size.y - dots.y));
                // 金属の部分：枠の明るい線（外側の1ドットの暗い縁と、内側の暗い地は除く）。
                float metal = step(0.27, Luma(o));
                if (mode > 0.5 && metal > 0.5)
                {
                    float t = _Time.y + _CardFoilClock + seed * 37.0;
                    float2 uv = IN.texcoord;
                    float stripe = pow(0.5 + 0.5 * sin((uv.x + uv.y) * 9.0 - t * 1.4), 6.0);
                    if (mode < 1.5)
                    {
                        // A：外側の1ドットの線を金に。斜めの照りでゆっくり明るさが揺れる。
                        float outerRow = step(1.0, edge) * step(edge, 2.0);
                        float shimmer = 0.75 + 0.35 * pow(0.5 + 0.5 * sin((uv.x - uv.y) * 14.0 - t * 1.2), 4.0);
                        float3 gold = float3(1.0, 0.84, 0.42) * shimmer;
                        c = lerp(o * 1.05, gold, outerRow) + float3(1.0, 0.97, 0.9) * stripe * 0.25;
                    }
                    else
                    {
                        // S：4辺とも虹にして、属性の色を3割残す。虹は枠に沿ってゆっくり流れる。
                        float h = Perimeter(dots, size) * 2.0 - t * 0.12;
                        float3 rainbow = lerp(Rainbow(h), 1.0, 0.15) * (0.35 + 0.9 * Luma(o));
                        c = lerp(rainbow, o, 0.3) * 1.2 + lerp(o * 1.3, 1.0, 0.5) * stripe * 0.35;
                    }
                    c = saturate(c);
                }
                #ifndef UNITY_COLORSPACE_GAMMA
                c = GammaToLinearSpace(c);
                #endif
                float4 color = float4(c, frame.a * IN.color.a);
                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                color.a *= m.x * m.y;
                #endif
                return color;
            }
        ENDCG
        }
    }
}
