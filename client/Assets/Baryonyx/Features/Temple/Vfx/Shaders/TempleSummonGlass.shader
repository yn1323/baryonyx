// 神殿の召喚の演出（ステンドグラスの窓が砕ける）を、画像の窓の絵と計算で描くuGUI用シェーダー。
// 層は _Mode で選ぶ（TempleSummonShow が層ごとにマテリアルを複製して設定する）。
//   0 裏の光（丸いにじみ）、1 窓（絵を灯す光とひび）、2 破片（窓の絵の多角形）、
//   3 光の筋とほこり、4 ガラスの粉のきらめき、5 窓の抜けた穴。
// 1枚ずつの値（追加のUV1）：x 乱数の種、y 進み具合、z 層ごとの値（下の各層の説明）、w 明るさの倍率（0は1）。
// 頂点色のrgbで光の色を、アルファで全体の濃さを決める。_Tier が2のときは虹色にする。
// ひびと破片は同じ分け方（放射状の線 _Angles と、扇ごとの同心円の弧 _Rings）から作るため、ひびの線で割れる。
Shader "Baryonyx/Temple/Summon Glass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Window Picture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1
        _Mode ("Mode", Float) = 0
        _Tier ("Tier (0 white, 1 gold, 2 rainbow)", Float) = 0
        // 絵の中の窓の範囲（UV）。窓の円が、この範囲に内接する。
        _UvRect ("Window UV Rect", Vector) = (0,0,1,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1

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
        Blend [_SrcBlend] [_DstBlend], Zero One
        ColorMask [_ColorMask]

        Pass
        {
            Name "TempleSummonGlass"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "../../../Combat/Vfx/Shaders/BattleVfxShapes.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 params   : TEXCOORD1;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                half4 color     : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 params   : TEXCOORD2;
            };

            sampler2D _MainTex;
            half4 _Color;
            half _Intensity;
            float _Mode;
            float _Tier;
            float4 _UvRect;
            float4 _ClipRect;

            // 放射状のひびの角度（0〜2π、小さい順）と、扇ごとの同心円の弧の半径（窓の半径を1として、内から3本）。
            // 扇 i は角度 i と i+1 の間。
            #define MaxAngles 16
            float _Angles[MaxAngles];
            float _Rings[MaxAngles * 3];
            float _AngleCount;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.params = v.params;
                OUT.color = v.color * _Color;
                return OUT;
            }

            half3 Hue(float h)
            {
                return saturate(abs(frac(h + float3(0, 2.0 / 3, 1.0 / 3)) * 6 - 3) - 1);
            }

            // 段階の光の色。虹は、窓の周りの角度と進み具合で色相を回し、白に寄せて淡くする。
            half3 TierTint(half3 tint, float2 p, float k)
            {
                if (_Tier < 1.5)
                    return tint;
                float around = atan2(p.y, p.x) / Tau;
                return lerp(half3(1, 1, 1), Hue(around + k * 0.6), 0.4);
            }

            float WrapAngle(float a)
            {
                return a - Tau * floor((a + Pi) / Tau);
            }

            // 窓の中の位置（中心0、半径1）の、ひびの線までの距離（窓の半径を1とした長さ）。
            // radial：放射状の線の伸びた割合、rings：弧の入った割合（内の弧から順に）。
            float CrackDistance(float2 p, float radial, float rings)
            {
                float r = length(p);
                float theta = atan2(p.y, p.x);
                theta = theta < 0 ? theta + Tau : theta;
                int count = (int)_AngleCount;
                float best = 10;
                int sector = count - 1;
                for (int i = 0; i < MaxAngles; i++)
                {
                    if (i >= count)
                        break;
                    float a = _Angles[i];
                    if (theta >= a)
                        sector = i;
                    float d = WrapAngle(theta - a);
                    // 線ごとに伸びる長さを少しずらす。
                    float reach = radial * (0.82 + 0.18 * Hash1(i, 0.37)) * 1.02;
                    if (abs(d) < Pi * 0.5 && r < reach)
                        best = min(best, r * abs(sin(d)));
                }
                float a0 = _Angles[sector];
                float a1 = sector + 1 < count ? _Angles[sector + 1] : _Angles[0] + Tau;
                float span = WrapAngle(a1 - a0);
                span = span <= 0 ? span + Tau : span;
                float rel = theta - a0;
                rel = rel < 0 ? rel + Tau : rel;
                float f = saturate(rel / span);
                for (int m = 0; m < 3; m++)
                {
                    float ring = _Rings[sector * 3 + m];
                    // 内の弧から順に、両側の放射状の線から真ん中へ向かって入る。
                    float reveal = saturate(rings * 3 - m);
                    if (ring < radial && min(f, 1 - f) < reveal * 0.5)
                        best = min(best, abs(r - ring));
                }
                return best;
            }

            half4 Glow(float2 p, half3 tint, float bright)
            {
                float r = length(p);
                float v = Gauss(r, 0.55) * 0.75 + Gauss(r, 0.22) * 0.6;
                v *= 1 - smoothstep(0.85, 1.0, r);
                return half4(tint * v * bright, 1);
            }

            // 窓：絵を裏の光で灯し（z）、ひびを描く。y は0〜1で放射状の線が伸び、1〜2で弧が入る。
            half4 Window(float2 board, float2 tex, half3 tint, float4 params)
            {
                float2 p = board * 2 - 1;
                float r = length(p);
                float inside = 1 - smoothstep(0.985, 1.0, r);
                half3 glass = tex2D(_MainTex, tex).rgb;
                float lit = params.z;
                half3 color = glass * lit * inside;
                float radial = saturate(params.y);
                float rings = saturate(params.y - 1);
                float d = CrackDistance(p, radial, rings);
                float px = max(fwidth(r), 1e-4);
                float core = 1 - smoothstep(0.35 * px, 1.1 * px, d);
                float halo = Gauss(d, 0.008) * 0.45 + Gauss(d, 0.025) * 0.1;
                // 中心の打点は、細かく砕けて白く光る。
                float impact = Gauss(r, 0.05 + 0.04 * radial) * saturate(radial * 3);
                float crack = (core * 1.6 + halo + impact * 1.4) * inside * step(0.001, radial);
                color += TierTint(tint, p, radial) * crack * params.w;
                return half4(color, 1);
            }

            // 破片：窓の絵の多角形。z は破片の縁への近さ（中心0、縁1）、y は削れた割合、w は縁のきらめき。
            half4 Shard(float2 board, float2 tex, half3 tint, float4 params, half alpha)
            {
                float2 p = board * 2 - 1;
                half3 glass = tex2D(_MainTex, tex).rgb;
                float eaten = params.y;
                float n = Fbm(float3(board * 26, params.x * 9));
                float keep = smoothstep(eaten - 0.08, eaten + 0.08, n * 0.85 + (1 - params.z) * 0.15);
                float rim = smoothstep(0.7, 1.0, params.z);
                half3 light = TierTint(tint, p, params.x) * rim * params.w;
                // 裏から光を受けたガラスとして、絵の色を少し明るく灯す。
                half3 color = glass * 1.25 + light;
                half a = alpha * keep;
                return half4(color * a, a);
            }

            // 光の筋とほこり：上（窓）から下（床）へ広がる扇。y 進み具合（ほこりが漂う）。
            half4 Rays(float2 uv, half3 tint, float4 params)
            {
                float v = 1 - uv.y;
                float x = (uv.x - 0.5) / lerp(0.16, 0.5, v);
                float edge = 1 - smoothstep(0.75, 1.0, abs(x));
                float stripes = 0;
                for (int i = 0; i < 7; i++)
                {
                    float c = (Hash1(i, params.x) * 2 - 1) * 0.8;
                    float w = 0.05 + 0.09 * Hash1(i + 11, params.x);
                    stripes += Gauss(x - c, w) * (0.35 + 0.65 * Hash1(i + 23, params.x));
                }
                stripes += Noise(float3(x * 9 + params.x * 30, v * 1.5, params.y * 0.4)) * 0.25;
                float fall = pow(saturate(1 - v), 0.7) * smoothstep(0.0, 0.06, v);
                float beams = stripes * fall * edge;
                // ほこり：格子ごとに1粒。ゆっくり漂い、筋の中でだけ見える。
                float2 grid = float2(uv.x * 46, (uv.y + params.y * 0.06) * 30);
                float2 cell = floor(grid);
                float2 at = float2(Hash(float3(cell, 3.1)), Hash(float3(cell, 7.7)));
                float2 drift = float2(sin(params.y * 3 + at.x * 6.28), cos(params.y * 2 + at.y * 6.28)) * 0.15;
                float dust = Gauss(length(frac(grid) - (0.25 + at * 0.5) - drift), 0.06);
                dust *= step(0.55, Hash(float3(cell, 1.3))) * (0.5 + 0.5 * sin(params.y * 9 + at.x * 30));
                float value = beams * 0.45 + dust * beams * 3.5;
                return half4(TierTint(tint, float2(x, -v), params.x) * value * params.w, 1);
            }

            // ガラスの粉：窓から下へ落ちながら瞬く細かい粒。y は砕けてからの秒数を4で割った値、z は窓の中心の高さ。
            half4 Glitter(float2 uv, half3 tint, float4 params)
            {
                float t = params.y * 4;
                float2 aspect = float2(1.78, 1);
                float columns = 70;
                float column = floor(uv.x * columns);
                float speed = 0.08 + 0.1 * Hash(float3(column, 5.5, 0));
                float y = uv.y + t * speed;
                float2 grid = float2(uv.x * columns, y * 40);
                float2 cell = floor(grid);
                float2 at = float2(Hash(float3(cell, 2.2)), Hash(float3(cell, 9.1)));
                float spark = Gauss(length((frac(grid) - (0.2 + at * 0.6)) * float2(1, 0.6)), 0.07);
                float live = step(0.7, Hash(float3(cell, 4.4)));
                float twinkle = pow(saturate(sin(t * (6 + at.x * 10) + at.y * 30)), 6);
                // 窓の真下に広がる範囲だけ、砕けてから時間とともに広がって薄れる。
                float center = abs(uv.x - 0.5) * 2;
                float reach = 0.25 + t * 0.12;
                float below = smoothstep(params.z + 0.05, params.z - 0.1, uv.y);
                float area = (1 - smoothstep(reach * 0.6, reach, center)) * below;
                float fade = saturate(1.4 - t * 0.35);
                float value = spark * live * (0.25 + twinkle * 2.5) * area * fade;
                return half4(TierTint(tint, uv * 2 - 1, at.x) * value * params.w, 1);
            }

            // 抜けた穴：窓の円の中を裏の光で塗る。y は光の強さ。
            half4 Hole(float2 p, half3 tint, float4 params)
            {
                float r = length(p);
                // 石の窓枠は残り、ガラスのあった内側だけが抜ける。
                float inside = 1 - smoothstep(0.9, 0.93, r);
                half3 night = half3(0.03, 0.035, 0.09);
                float light = params.y * (0.55 + 0.45 * Gauss(r, 0.7));
                half3 color = lerp(night, TierTint(tint, p, params.x) * params.w, saturate(light));
                return half4(color * inside, inside);
            }

            half4 frag(v2f IN) : SV_Target
            {
                half3 tint = IN.color.rgb;
                float4 params = IN.params;
                params.w = params.w > 0 ? params.w : 1;
                float2 board = (IN.texcoord - _UvRect.xy) / _UvRect.zw;
                half4 c;
                if (_Mode < 0.5)
                    c = Glow(IN.texcoord * 2 - 1, TierTint(tint, IN.texcoord * 2 - 1, params.y), params.w);
                else if (_Mode < 1.5)
                    c = Window(board, IN.texcoord, tint, params);
                else if (_Mode < 2.5)
                    c = Shard(board, IN.texcoord, tint, params, IN.color.a);
                else if (_Mode < 3.5)
                    c = Rays(IN.texcoord, tint, params);
                else if (_Mode < 4.5)
                    c = Glitter(IN.texcoord, tint, params);
                else
                    c = Hole(IN.texcoord * 2 - 1, tint, params);
                c.rgb *= _Intensity;
                // 破片と穴は覆う形（乗算済みアルファ）。それ以外は加算で、頂点色のアルファで濃さを決める。
                if (_Mode < 1.5 || (_Mode > 2.5 && _Mode < 4.5))
                    c.rgb *= IN.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                c *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                return c;
            }
        ENDCG
        }
    }
}
