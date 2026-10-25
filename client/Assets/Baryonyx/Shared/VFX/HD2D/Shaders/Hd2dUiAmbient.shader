// 描いた2D背景の上に足す光（揺らぐ光の芯とハロー、床の光だまり、光芒、火の粉、塵、きらめき、もや）を、
// 画像を使わずに計算で描くuGUI用の加算のシェーダー。形はマテリアルのキーワード（_SHAPE_〜）で選ぶ。
// 色と濃さは頂点色（Imageの色）で受け取り、明るさの倍率（1を超えるとBloomでにじむ）はマテリアルで決める。
Shader "Baryonyx/HD2D/UI Ambient"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [KeywordEnum(Glow, Pool, Beam, Mote, Star, Ember, Puff)] _Shape ("形", Float) = 0
        _Gain ("明るさの倍率", Float) = 1
        _CoreWhite ("芯の白さ", Range(0, 1)) = 0.5
        _Core ("芯（x：広がり、y：明るさ）", Vector) = (0.12, 1.5, 0, 0)
        _Halo ("ハロー（x：広がり、y：明るさ、z：空気のゆらぎ）", Vector) = (0.4, 0.6, 0.2, 0)
        _Beam ("光芒（x：根元の半幅、y：先の半幅、z：先への減り方、w：筋の強さ）", Vector) = (0.42, 0.9, 1.3, 0.5)
        _Motes ("光の中の塵（x：升目px、y：出る割合、z：大きさpx、w：流れる速さpx/秒）", Vector) = (28, 0.3, 1.2, 10)
        _MoteColor ("塵の色", Color) = (0.9, 0.95, 1, 1)
        _MoteSway ("塵の横ゆれ（px）", Float) = 4
        _Seed ("模様の開始位置", Vector) = (0, 0, 0, 0)

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
        // Add the light to the colour and keep the destination alpha unchanged.
        Blend One One, Zero One
        ColorMask [_ColorMask]

        Pass
        {
            Name "Ambient"
        CGPROGRAM
            #pragma vertex AmbientVert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma shader_feature_local _SHAPE_GLOW _SHAPE_POOL _SHAPE_BEAM _SHAPE_MOTE _SHAPE_STAR _SHAPE_EMBER _SHAPE_PUFF

            #include "Hd2dAmbient.cginc"

            float _Gain;
            half _CoreWhite;
            float4 _Core;
            float4 _Halo;
            float4 _Beam;
            float4 _Motes;
            half4 _MoteColor;
            float _MoteSway;
            float4 _Seed;

            float Gauss(float d, float sigma)
            {
                return exp(-(d * d) / (2 * sigma * sigma));
            }

            // Zero before the edge of the board, so its square edge never shows. The fade starts
            // early and is gentle, so the soft halos never end in a visible rim.
            float BoardFade(float r)
            {
                return 1 - smoothstep(0.4, 1.0, r);
            }

            // The light, separated into a white-hot core and a coloured halo (F2), so the core
            // stays sharp while the halo spreads. Returns the colour to add.
            half3 Glow(float2 p, half3 color, float t)
            {
                float r = length(p);
                float core = Gauss(r, _Core.x) * _Core.y;
                // The air in the halo moves a little, so the light never sits as a flat disc.
                float shimmer = AmbientFbm2(p * 1.8 + _Seed.xy + float2(t * 0.17, -t * 0.13));
                float halo = Gauss(r, _Halo.x) * _Halo.y * lerp(1 - _Halo.z, 1 + _Halo.z, shimmer);
                return (color * halo + lerp(color, 1, _CoreWhite) * core) * BoardFade(r);
            }

            // Light fallen on an uneven floor: a wide ellipse (the board is wide) with a brighter
            // middle, its rim broken by the floor.
            half3 Pool(float2 uv, float2 p, half3 color, float t)
            {
                float2 size = AmbientBoardPixels(uv);
                float2 floorP = p * float2(size.x / max(size.y, 1), 1);
                float n = AmbientFbm3(floorP * 1.4 + _Seed.xy + float2(t * 0.02, 0));
                float r = length(p);
                float broken = r * lerp(0.8, 1.25, n);
                float light = Gauss(broken, _Halo.x) * _Halo.y + Gauss(r, _Core.x) * _Core.y;
                return color * light * BoardFade(r);
            }

            // A shaft of light through dusty air: a soft band that widens and fades from its
            // source (uv.x = 0) toward the floor, streaked where it passed through gaps, with a
            // slowly drifting dust volume and specks of dust that sparkle while inside it.
            half3 Beam(float2 uv, half3 color, float t)
            {
                float along = uv.x;
                float across = uv.y * 2 - 1;
                float halfWidth = lerp(_Beam.x, _Beam.y, along);
                float k = across / halfWidth;
                float band = exp(-k * k * 2.2) * (1 - smoothstep(0.9, 1.0, abs(across)));
                float fadeIn = saturate(along * 5 + 0.2);
                float intensity = saturate(pow(1 - along, _Beam.z) * fadeIn / 0.8);

                // Streaks run along the shaft and sway slowly.
                float streaks = AmbientFbm3(float2(k * 2.6 + _Seed.x, along * 0.8 - t * 0.03));
                band *= lerp(1 - _Beam.w, 1 + _Beam.w * 0.6, streaks);
                // Dust in the air thickens and thins as it drifts down the shaft.
                float dust = AmbientFbm2(float2(k * 1.5 + _Seed.y, along * 5) + float2(t * 0.05, -t * 0.12));
                float lit = band * intensity;
                half3 light = color * lit * lerp(0.75, 1.2, dust);

                // Specks: one chance per cell of the board in pixels, drifting toward the floor.
                float2 size = AmbientBoardPixels(uv);
                float2 pixel = uv * size;
                float cell = max(_Motes.x, 4);
                pixel.x -= t * _Motes.w;
                float2 cellP = pixel / cell;
                float2 id = floor(cellP);
                float h = AmbientHash(id + _Seed.zw);
                // Within a quarter of the cell, so a speck and its glow never cross into the next cell.
                float2 centre = 0.5 + (float2(AmbientHash(id + 7.1), AmbientHash(id + 3.3)) - 0.5) * 0.3;
                // Each speck sways across the shaft on its own phase.
                centre.y += sin(t * 0.7 + h * 40) * min(_MoteSway / cell, 0.1);
                float2 offset = (frac(cellP) - centre) * cell;
                float d = length(offset);
                float present = step(1 - _Motes.y, h);
                float twinkle = 0.5 + 0.5 * sin(t * (1.2 + h * 2.3) + h * 61);
                twinkle *= twinkle;
                float speck = Gauss(d, _Motes.z) * 1.8 + Gauss(d, _Motes.z * 3) * 0.25;
                // Only dust inside the light shows; the root keeps it visible lower down.
                float mote = speck * present * lerp(0.35, 1, twinkle) * sqrt(saturate(lit * 1.6));
                return light + _MoteColor.rgb * mote;
            }

            // Dust: a small soft point with a brighter middle.
            half3 Mote(float2 p, half3 color)
            {
                float r = length(p);
                float light = Gauss(r, _Core.x) * _Core.y + Gauss(r, _Halo.x) * _Halo.y;
                return lerp(color, 1, _CoreWhite * Gauss(r, _Core.x)) * light * BoardFade(r);
            }

            // A four-pointed twinkle: a white core, long thin rays across, short faint diagonal
            // rays and a soft halo. The rays keep at least about a pixel of width.
            half3 Star(float2 p, half3 color)
            {
                float r = length(p);
                float2 a = abs(p);
                float w = max(fwidth(p.x) * 0.8, 0.03);
                float across = exp(-a.y / w) * pow(saturate(1 - a.x), 1.8) * 1.6;
                float down = exp(-a.x / w) * pow(saturate(1 - a.y), 1.8) * 1.6;
                float2 diagonal = abs(float2(p.x + p.y, p.x - p.y)) * 0.7071;
                float slant = (exp(-diagonal.y / w) * pow(saturate(1 - diagonal.x * 1.9), 3)
                    + exp(-diagonal.x / w) * pow(saturate(1 - diagonal.y * 1.9), 3)) * 0.35;
                float core = Gauss(r, _Core.x) * _Core.y;
                float halo = Gauss(r, _Halo.x) * _Halo.y;
                return (color * (across + down + slant + halo) + lerp(color, 1, _CoreWhite) * core)
                    * (1 - smoothstep(0.85, 1.0, max(a.x, a.y)));
            }

            // An ember: a round glowing head at the right end of the board (its centre is half the
            // board's height in from the end) and a thin tail behind it that narrows and cools.
            // The board is turned to the direction of travel and lengthened with the speed.
            half3 Ember(float2 uv, half3 color)
            {
                float2 size = AmbientBoardPixels(uv);
                float2 pos = (uv - float2(1, 0.5)) * size + float2(size.y * 0.5, 0);
                float headRadius = size.y * _Core.x;
                float d = length(pos);
                float core = Gauss(d, headRadius) * _Core.y;
                float halo = Gauss(d, headRadius * 2.2) * _Halo.y;
                float tailLength = max(size.x - size.y, 1e-3);
                float s = saturate(1 + pos.x / tailLength);
                float width = max(headRadius * 0.7 * s, 0.45);
                float behind = smoothstep(headRadius * 0.5, -headRadius * 0.5, pos.x);
                float tail = Gauss(pos.y, width) * pow(s, 1.2) * behind * _Halo.z;
                // Nothing reaches the board's edges, so its rectangle never shows.
                float halfHeight = size.y * 0.5;
                float room = min(halfHeight - abs(pos.y),
                    min(halfHeight - pos.x, pos.x + tailLength + halfHeight));
                float fade = smoothstep(0, halfHeight * 0.45, room);
                return (color * (halo + tail) + lerp(color, 1, _CoreWhite) * core) * fade;
            }

            // A puff of soft light whose rim drifts like haze.
            half3 Puff(float2 p, half3 color, float t)
            {
                float r = length(p);
                float n = AmbientFbm3(p * 1.7 + _Seed.xy + float2(t * 0.21, -t * 0.16));
                float broken = r * lerp(0.75, 1.3, n);
                float light = Gauss(broken, _Halo.x) * _Halo.y + Gauss(r, _Core.x) * _Core.y;
                return color * light * BoardFade(r);
            }

            half4 frag(AmbientV2F IN) : SV_Target
            {
                float2 uv = IN.uv;
                float2 p = uv * 2 - 1;
                float t = _Hd2dAmbientTime;
                half3 color = IN.color.rgb;
                half3 light;
                #if defined(_SHAPE_POOL)
                light = Pool(uv, p, color, t);
                #elif defined(_SHAPE_BEAM)
                light = Beam(uv, color, t);
                #elif defined(_SHAPE_MOTE)
                light = Mote(p, color);
                #elif defined(_SHAPE_STAR)
                light = Star(p, color);
                #elif defined(_SHAPE_EMBER)
                light = Ember(uv, color);
                #elif defined(_SHAPE_PUFF)
                light = Puff(p, color, t);
                #else
                light = Glow(p, color, t);
                #endif
                half strength = IN.color.a * _Gain * AmbientClip(IN);
                half3 rgb = max(light * strength + AmbientDither(IN.screen) * step(1e-4, strength), 0);
                return half4(rgb, 0);
            }
        ENDCG
        }
    }
}
