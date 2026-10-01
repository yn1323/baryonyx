// 戦闘のスキル演出に使うuGUI用シェーダー。UI/Defaultと同じ頂点色・RectMask2D・Maskの扱いに、次を足す。
// - 削れて消える（アルファエロージョン）：_Erodeが1のとき、頂点色のアルファを「残り」として使い、
//   絵の薄い部分から先に透明にする。縁はノイズで不規則にし、削れる縁を少し明るくする。
// - 明るさの倍率（_Intensity）：1を超える明るさにして、HDRのBloomで芯だけをにじませる。
// - 合成（_SrcBlend・_DstBlend）：加算（SrcAlpha One）と、煙などの通常合成（SrcAlpha OneMinusSrcAlpha）。
Shader "Baryonyx/Combat/Battle Vfx"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1
        [Toggle] _Erode ("Erode By Vertex Alpha", Float) = 0
        _Softness ("Erode Softness", Range(0.01, 1)) = 0.12
        _EdgeGlow ("Erode Edge Glow", Float) = 0.6
        _NoiseTex ("Erode Noise", 2D) = "gray" {}
        _NoiseScale ("Noise Scale", Float) = 2
        _NoiseAmount ("Noise Amount", Range(0, 1)) = 0.3
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
        // Keep the destination alpha unchanged, as the additive UI material does.
        Blend [_SrcBlend] [_DstBlend], Zero One
        ColorMask [_ColorMask]

        Pass
        {
            Name "BattleVfx"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

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
                half4 color     : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 mask     : TEXCOORD1;
                float2 noiseUv  : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _NoiseTex;
            half4 _Color;
            half _Intensity;
            half _Erode;
            half _Softness;
            half _EdgeGlow;
            half _NoiseScale;
            half _NoiseAmount;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
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
                OUT.texcoord = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
                // The noise follows the picture, so its edge keeps its shape as the picture moves.
                OUT.noiseUv = v.texcoord.xy * _NoiseScale;
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
                half4 tex = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                half shape = tex.a;
                half3 rgb = tex.rgb * IN.color.rgb * _Color.rgb * _Intensity;

                // Without erosion, the vertex alpha fades the whole picture.
                half alpha = shape * IN.color.a;
                if (_Erode > 0.5)
                {
                    // The vertex alpha is what is left: 1 whole, 0 gone. The threshold rises as it
                    // falls, so the faint parts go first and the shape thins as it fades.
                    half t = 1 - IN.color.a;
                    half n = tex2D(_NoiseTex, IN.noiseUv).r - 0.5;
                    half edge = shape - t + n * _NoiseAmount * saturate(t * 4);
                    // Fully kept at the start; pure erosion once the threshold has moved a little.
                    half keep = saturate(edge / _Softness + 1 - saturate(t * 5));
                    alpha = shape * keep;
                    // A thin bright rim where it is being eaten away, like a burning edge.
                    half rim = saturate(1 - abs(edge) / (_Softness * 1.5)) * saturate(t * 5);
                    rgb *= 1 + rim * _EdgeGlow;
                }

                half4 color = half4(rgb, alpha * _Color.a);

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
