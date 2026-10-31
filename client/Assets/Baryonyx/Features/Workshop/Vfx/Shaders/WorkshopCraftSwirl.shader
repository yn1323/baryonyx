// 合成の演出（光の渦）の螺旋の光の線を、画像を使わずに計算で描くuGUI用シェーダー（doc/features/synthesis.md 合成の演出）。
// 対数螺旋の腕を何本か描き、腕の先頭（頭）が進み具合とともに中心へ近づき、渦は進むほど速く回る。
// 層はマテリアルの _Layer で選ぶ（0 にじみ：線の周りの光った空気、1 線：細い線と先頭の白い芯）。
// ☆4・☆5の見た目は別のマテリアルで、腕の数（_Arms）と、腕ごとに淡い虹をまとわせる強さ（_Rainbow）を変える。
// UI/Defaultと同じ頂点色・RectMask2D・Maskの扱いに、次を足す。
// - 1枚ずつの値（追加のUV1）：x 乱数の種、y 進み具合（0で外側、1で中心）、z 消えた割合（0で消えていない）、w 明るさの倍率（0は1とみなす）。
// - 頂点色：rgbで腕を染め（1本おきに薄紅へ寄せる）、アルファで全体を薄める。
// 加算（SrcAlpha One）で描く。ハッシュ・ノイズ・線の式は戦闘の形のシェーダーと共通（BattleVfxShapes.cginc）。
Shader "Baryonyx/Workshop/Craft Swirl"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1
        _Layer ("Layer (0 halo, 1 line)", Float) = 1
        _Arms ("Arms", Range(2, 8)) = 6
        _Twist ("Twist", Float) = 1.7
        _Width ("Line Width", Float) = 0.004
        _Rainbow ("Rainbow", Range(0, 1)) = 0

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
        Blend SrcAlpha One, Zero One
        ColorMask [_ColorMask]

        Pass
        {
            Name "WorkshopCraftSwirl"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // fwidth keeps the thin lines at least a pixel wide.
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "Assets/Baryonyx/Features/Combat/Vfx/Shaders/BattleVfxShapes.cginc"

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

            half4 _Color;
            half _Intensity;
            half _Layer;
            half _Arms;
            float _Twist;
            float _Width;
            half _Rainbow;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

            // 1本おきの腕の色（材料の貝殻のような薄紅）。
            static const half3 Rose = half3(1.0, 0.62, 0.7);

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

            // The spiral arms at p: halo (the lit air round the lines) or line (a thin line whose
            // head is a white-hot core). Each arm is a logarithmic spiral turning faster as the
            // swirl goes on (a free vortex spins faster near its centre); its head falls in toward
            // the centre and its tail thins and breaks up behind it.
            half3 Arms(float2 p, float seed, float k, half3 tint, float px, bool halo)
            {
                float r = max(length(p), 1e-3);
                float theta = atan2(p.y, p.x);
                float lnr = log(r);
                // 進むほど速く回る（反時計回り）。
                float spin = Tau * (0.55 * k + 1.6 * k * k);
                float inward = k * k * (3 - 2 * k);
                float slant = rsqrt(1 + _Twist * _Twist);
                half3 light = 0;
                int count = (int)_Arms;
                [loop]
                for (int i = 0; i < 8; i++)
                {
                    if (i >= count)
                        break;
                    float h1 = Hash1(i + 1.0, seed);
                    float h2 = Hash1(i + 11.0, seed);
                    float phase = i * Tau / count + (h1 - 0.5) * 0.7;
                    float d = theta - (phase + spin + _Twist * lnr);
                    d = (frac(d / Tau + 0.5) - 0.5) * Tau;
                    // 角度の差を、螺旋に垂直な距離へ直す（対数螺旋は半径と一定の角度で交わる）。
                    float dist = abs(d) * r * slant;

                    float head = lerp(0.9, 0.05, inward) * (0.82 + 0.36 * h2);
                    float tailLength = 0.5 * (1 - 0.45 * k) * (0.75 + 0.5 * h1);
                    float along = r - head;
                    float tail = along >= 0
                        ? pow(saturate(1 - along / tailLength), 1.6)
                        : exp(-along * along / (0.012 * 0.012));
                    // 尾は先へ行くほど途切れる。
                    float n = Noise(float3(r * 11.0 + i * 4.3, seed * 7.0 + i, k * 3.0));
                    float breaks = smoothstep(0.25, 0.55, n + saturate(1 - along / tailLength) * 0.45);
                    float weight = (0.6 + 0.4 * h2) * tail * breaks;
                    half3 armTint = lerp(tint, Rose, (i % 2) * 0.55 * (1 - _Rainbow));
                    // 虹：腕ごとと半径で色相をずらした淡い色（濃くしない）。芯は白のまま。
                    float hue = frac(i / (float)count + r * 0.35 - k * 0.3 + h1 * 0.1);
                    half3 spectrum = saturate(abs(frac(hue + float3(0, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3) - 1);
                    armTint = lerp(armTint, lerp(1, spectrum, 0.55) * tint, _Rainbow);

                    if (halo)
                    {
                        light += armTint * Gauss(dist, _Width * 7.0) * weight * 0.32;
                    }
                    else
                    {
                        float hot = Gauss(along, 0.035);
                        float body = Line(dist, _Width, px) * weight;
                        float core = Line(dist, _Width * 0.35, px * 0.8) * hot * (0.6 + 0.4 * h2);
                        light += armTint * body * 0.85 + lerp(armTint, 1, 0.85) * core * 1.7;
                    }
                }
                // 中心の特異点の乱れを隠す。
                light *= smoothstep(0.015, 0.06, r);
                return light;
            }

            half4 frag(v2f IN) : SV_Target
            {
                float2 p = IN.texcoord * 2 - 1;
                float seed = IN.params.x;
                float k = saturate(IN.params.y);
                float rem = saturate(1 - IN.params.z);
                float bright = IN.params.w > 0 ? IN.params.w : 1;
                half3 tint = IN.color.rgb * _Color.rgb;
                float px = max(fwidth(length(p)), 1e-4);

                half3 light = Arms(p, seed, k, tint, px, _Layer < 0.5);
                light *= BoardFade(p) * rem * _Intensity * bright;

                half fade = IN.color.a * _Color.a;
                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                fade *= m.x * m.y;
                #endif

                half4 color = half4(light, fade);
                // 薄く広いにじみの帯（バンディング）を、8ビットの1段に満たないノイズで散らす。
                float dither = frac(52.9829189 * frac(dot(IN.vertex.xy, float2(0.06711056, 0.00583715)))) - 0.5;
                color.rgb = max(0, color.rgb + dither / 255.0 * saturate(max(color.r, max(color.g, color.b)) * 64));

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
