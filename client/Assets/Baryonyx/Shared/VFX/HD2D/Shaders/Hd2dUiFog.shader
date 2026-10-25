// 描いた2D背景の上に流れる霧を、画像を使わずに計算で描くuGUI用のシェーダー（Hd2dFog が霧の範囲ごとに使う）。
// 5段のfBmを2回ゆがめた座標で読み、濃さで色を変え、縁を明るく、折り目に暗い筋を入れる。
// 霧は奥が透けるよう乗算済みアルファで重ね、暗い筋は背景を少し暗くする。
Shader "Baryonyx/HD2D/UI Fog"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _ShadeColor ("薄い所の色（霧の色に掛ける）", Color) = (0.68, 0.74, 0.88, 1)
        _LitColor ("縁の明るさの色", Color) = (0.85, 0.9, 1, 1)
        _Rim ("縁の明るさ", Range(0, 1)) = 0.35
        _Folds ("暗い筋の濃さ", Range(0, 1)) = 0.35
        _Veil ("薄く全体に掛かる量", Range(0, 1)) = 0.25
        _Cover ("濃さのしきい値（下・上）", Vector) = (0.36, 0.78, 0, 0)
        _Warp ("ゆがみの強さ", Range(0, 4)) = 2.2

        // Hd2dFog sets these per bank of fog.
        _Tiles ("模様の数（横・縦）", Vector) = (2, 1, 0, 0)
        _Flow ("流れ（xy：大きな模様、zw：細かい切れ端）", Vector) = (0, 0, 0, 0)
        _Evolve ("形の変わり具合", Float) = 0
        _Seed ("模様の開始位置", Vector) = (0, 0, 0, 0)
        _Edge ("縁のぼかし（横・縦の割合）・縁の崩れ", Vector) = (0.12, 0.3, 0.6, 0)
        _Detail ("細かい切れ端の濃さ", Range(0, 1)) = 0.5
        _Settle ("床に沈む度合い", Range(0, 1)) = 0.5

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
        Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Fog"
        CGPROGRAM
            #pragma vertex AmbientVert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "Hd2dAmbient.cginc"

            half4 _ShadeColor;
            half4 _LitColor;
            half _Rim;
            half _Folds;
            half _Veil;
            float4 _Cover;
            float _Warp;
            float4 _Tiles;
            float4 _Flow;
            float _Evolve;
            float4 _Seed;
            float4 _Edge;
            half _Detail;
            half _Settle;

            // Five octaves: the two large ones ride the wind, the three fine ones (the torn
            // wisps) drift on their own and can be faded with _Detail.
            float Density(float2 p, float2 detailFlow)
            {
                float sum = AmbientNoise(p) * 0.516;
                p = mul(AmbientOctave, p) + float2(3.1, 7.7);
                sum += AmbientNoise(p) * 0.258;
                // detailFlow is in the bank's tiles; turned and scaled like the octaves so far.
                p = mul(AmbientOctave, p) + float2(-5.3, 1.9)
                    + mul(AmbientOctave, mul(AmbientOctave, detailFlow));
                float fine = AmbientNoise(p) * 0.129;
                p = mul(AmbientOctave, p) + float2(2.6, -4.4);
                fine += AmbientNoise(p) * 0.065;
                p = mul(AmbientOctave, p) + float2(-1.2, 6.3);
                fine += AmbientNoise(p) * 0.032;
                // Without the wisps the large shapes still span the same range.
                return sum + lerp(0.113, fine, _Detail);
            }

            half4 frag(AmbientV2F IN) : SV_Target
            {
                float2 uv = IN.uv;
                float2 p = uv * _Tiles.xy + _Seed.xy + _Flow.xy;
                float t = _Evolve;

                // Two rounds of domain warping: the first rolls the mist in slow eddies, the
                // second, read through the first, folds it into the dark creases.
                float2 q = float2(
                    AmbientFbm2(p * 0.7 + float2(0.0, t * 0.11)),
                    AmbientFbm2(p * 0.7 + float2(5.2, 1.3) - t * 0.09));
                float2 r = float2(
                    AmbientFbm2(p + _Warp * q + float2(1.7, 9.2) + t * 0.06),
                    AmbientFbm2(p + _Warp * q + float2(8.3, 2.8) - t * 0.05));
                float d = Density(p + _Warp * (r - 0.5), _Flow.zw);

                // Mist settles on the floor: thicker low in the bank, torn into wisps at the top.
                float body = d * lerp(1, saturate(1.25 - uv.y * 0.8), _Settle);
                float cover = smoothstep(_Cover.x, _Cover.y, body);
                cover = lerp(_Veil * saturate(body * 2), 1, cover);

                // The bank fades out before its rectangle ends, along the shape of the mist.
                float2 edgeDistance = min(uv, 1 - uv) / max(_Edge.xy, 1e-3);
                // The density only pulls the fade in or out, so it is always 0 at the rectangle.
                float edge = saturate(min(edgeDistance.x, edgeDistance.y) * (1 + (d - 0.5) * _Edge.z * 2));
                edge = edge * edge * (3 - 2 * edge);
                cover *= edge;

                // Thin parts take the shade, dense parts the full colour, wisp rims catch light.
                half3 fogColor = IN.color.rgb;
                half3 color = lerp(fogColor * _ShadeColor.rgb, fogColor, saturate(body * 1.4));
                half rim = smoothstep(0.08, 0.3, cover) * (1 - smoothstep(0.35, 0.75, cover));
                color += _LitColor.rgb * fogColor * rim * _Rim;
                // Dark creases where the second warp folds over (a thin band of r.x).
                half fold = 1 - smoothstep(0.0, 0.04, abs(r.x - 0.5));
                fold *= smoothstep(0.25, 0.65, cover);
                color *= 1 - _Folds * fold;

                half alpha = saturate(cover * IN.color.a) * AmbientClip(IN);
                half dither = AmbientDither(IN.screen);
                return half4(color * alpha + dither, saturate(alpha + dither));
            }
        ENDCG
        }
    }
}
