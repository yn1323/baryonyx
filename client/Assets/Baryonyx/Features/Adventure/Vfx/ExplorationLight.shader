// 探索画面の演出の光（宝箱の光、出現の閃き）。uGUIの板の上に、画像を使わず計算で形を描き、加算で足す。
// _Shape：0 中心から外へ消える丸い光、1 回る光芒（長短8本）、2 四方へ伸びるきらめき。色と強さは頂点色（RawImageの色）で渡す。
Shader "Baryonyx/Adventure/Exploration Light"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Shape ("Shape (0 glow, 1 rays, 2 sparkle)", Float) = 0
        _Bright ("Brightness", Range(0, 4)) = 1.4

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
        // Add the light to the colour and keep the destination alpha unchanged.
        Blend SrcAlpha One, Zero One
        ColorMask [_ColorMask]

        Pass
        {
            Name "Light"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

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
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float4 local    : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float _Shape;
            float _Bright;
            float4 _ClipRect;
            int _UIVertexColorAlwaysGammaSpace;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.uv = v.texcoord;
                OUT.local = v.vertex;
                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                    v.color.rgb = UIGammaToLinear(v.color.rgb);
                OUT.color = v.color;
                return OUT;
            }

            // A soft round light: a hot core and a long falloff to nothing at the edge.
            half Glow(float2 p)
            {
                float r = length(p);
                half halo = saturate(1.0 - r);
                halo *= halo;
                half core = exp(-r * r * 40.0);
                return halo * 0.6 + core;
            }

            // Eight rays, long and short by turns, thin at their tips and gone at the edge.
            half Rays(float2 p)
            {
                float r = length(p);
                float a = atan2(p.y, p.x);
                float beam = pow(saturate(cos(a * 4.0)), 24.0);
                float shortBeam = pow(saturate(cos(a * 4.0 + 3.14159265)), 24.0);
                half reach = saturate(1.0 - r) * beam + saturate(1.0 - r * 1.7) * shortBeam;
                return reach * saturate(r * 6.0) + exp(-r * r * 60.0) * 0.6;
            }

            // A twinkle of four rays crossing at a bright point.
            half Sparkle(float2 p)
            {
                float2 q = abs(p);
                half cross = saturate(1.0 - q.x * 14.0) * saturate(1.0 - q.y)
                    + saturate(1.0 - q.y * 14.0) * saturate(1.0 - q.x);
                return cross * cross + exp(-dot(p, p) * 80.0);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 p = IN.uv * 2.0 - 1.0;
                half shape = _Shape < 0.5 ? Glow(p) : _Shape < 1.5 ? Rays(p) : Sparkle(p);
                half4 color = half4(IN.color.rgb * _Bright, IN.color.a * saturate(shape));

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.local.xy, _ClipRect);
                #endif
                return color;
            }
        ENDCG
        }
    }
}
