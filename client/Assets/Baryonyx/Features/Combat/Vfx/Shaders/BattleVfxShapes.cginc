// 戦闘のエフェクトの形を、画像を使わずに計算で描く関数（BattleVfxShape.shaderから読み込む）。
// 形は四角い板の中の位置から、距離・向き・ノイズで明るさと透明度を決める。
// 加算の形は (光の色, 1) を、通常合成の形は (色, 覆う割合) を返す。
// 1枚ずつの値は ShapeIn で受け取る。
// - seed：乱数の種（0〜1）。同じ形でも1枚ずつ模様を変える。
// - k：進み具合（0〜1）。形の動き（広がる、流れる、描き込まれる）に使う。
// - rem：残り（1で欠けていない、0で消えた）。削れ・途切れ・崩れに使う。
// - bright：明るさの倍率（1が通常）。ふつうは描いたあとに全体へ掛けるが、雷雲は中の稲光の強さとして使う。
// 描き方の共通の約束（vfx-authoring の formulas.md・procedural.md）：
// - 濃淡は回転付きの多段ノイズ（Fbm4・Fbm5）で作り、読む座標を別のノイズでゆがめる（Warp）。
//   乱数の種は SeedOffset で小さなずらしに直して足し、座標に掛けない。
// - 光は白い芯と色のにじみを別の幅のガウスにし（F2）、芯だけを1より明るくする。
// - 細い線は Line で、fwidth から求めた1画素より細くしない（細くなる分は暗くする）。
// - 消えるときは一様に薄めず、ノイズに沿って削る（Keep、F5）。
// - 板の端より内側で0にする（シェーダー側でも端の帯を削る）。
#ifndef BARYONYX_BATTLE_VFX_SHAPES
#define BARYONYX_BATTLE_VFX_SHAPES

struct ShapeIn
{
    float2 uv;
    float2 p;
    float r;
    float2 dir;
    float aa;
    float seed;
    float k;
    float rem;
    float bright;
    half3 tint;
};

static const float Pi = 3.14159265;
static const float Tau = 6.2831853;

// --- Hashes, noise and helpers ---------------------------------------------------------------

float Hash(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float Hash1(float i, float seed)
{
    return Hash(float3(i, seed * 97.0, 17.17));
}

// Smooth value noise (0 to 1).
float Noise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3 - 2 * f);
    return lerp(
        lerp(
            lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
            lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x),
            f.y),
        lerp(
            lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
            lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x),
            f.y),
        f.z);
}

// Three octaves of noise, each twice as fine and half as strong (fBM).
float Fbm(float3 p)
{
    return Noise(p) * 0.57 + Noise(p * 2.03 + 11.1) * 0.29 + Noise(p * 4.01 + 23.7) * 0.14;
}

// A soft line or glow around a distance of 0, w wide.
float Gauss(float d, float w)
{
    return exp(-(d * d) / (w * w));
}

float2 Rotate(float2 p, float a)
{
    float c = cos(a);
    float s = sin(a);
    return float2(c * p.x - s * p.y, s * p.x + c * p.y);
}

// Four octaves of noise (0 to 1), each turned and twice as fine (fBM), so no square steps or
// grid show; z moves the pattern in time.
float Fbm4(float2 p, float z)
{
    const float2x2 turn = float2x2(0.8, -0.6, 0.6, 0.8);
    float sum = 0;
    float amp = 0.5;
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        sum += amp * Noise(float3(p, z + i * 7.3));
        p = mul(turn, p) * 2.03;
        amp *= 0.5;
    }
    return sum / 0.9375;
}

// Five octaves of noise (0 to 1), each turned and twice as fine, for detail down to a pixel.
float Fbm5(float2 p, float z)
{
    const float2x2 turn = float2x2(0.8, -0.6, 0.6, 0.8);
    float sum = 0;
    float amp = 0.5;
    [unroll]
    for (int i = 0; i < 5; i++)
    {
        sum += amp * Noise(float3(p, z + i * 7.3));
        p = mul(turn, p) * 2.02;
        amp *= 0.5;
    }
    return sum / 0.96875;
}

// A shape's own place in the noise (0 to 8 each way), so a seed moves the pattern without
// scaling its coordinates (large coordinates lose precision in the finer octaves).
float2 SeedOffset(float seed)
{
    return float2(Hash1(3.7, seed), Hash1(9.1, seed)) * 8.0;
}

// p moved by two different noises (domain warping), up to amount each way, so a pattern streams
// and curls instead of lying as round blobs; z moves it in time.
float2 Warp(float2 p, float z, float amount)
{
    float2 q = float2(Fbm4(p, z), Fbm4(p + float2(5.2, 1.3), z + 3.1)) - 0.5;
    return p + q * amount;
}

// A thin line of light at a distance d of 0, w wide, never thinner than px (a pixel's distance,
// from fwidth): where it would be thinner it dims instead, so a small shape's lines never break up.
float Line(float d, float w, float px)
{
    float ww = max(w, px);
    return Gauss(d, ww) * max(w / ww, 0.4);
}

// A glowing stroke at a distance d of 0, w wide: x its body and soft halo (in the tint), y its
// thin white-hot core, at least a pixel wide.
float2 GlowStroke(float d, float w, float px)
{
    return float2(Gauss(d, w) + Gauss(d, w * 4.0) * 0.12, Line(d, w * 0.3, px));
}

// The colour of soot: the dark left where a flame cools.
static const half3 Soot = half3(0.05, 0.04, 0.035);

// Thinned out before the edge of the board, so its square edge never shows.
float BoardFade(float2 p)
{
    return 1 - smoothstep(0.88, 1.0, max(abs(p.x), abs(p.y)));
}

// Eaten away from the low parts of value as rem falls: whole at 1, gone at 0.
float Keep(float value, float rem, float soft)
{
    float cut = 1 - rem;
    return smoothstep(cut - soft, cut + soft, value);
}

// A signed distance to a capsule whose ends have different radii (Inigo Quilez): r1 at the
// origin, r2 at (0, h).
float UnevenCapsuleDistance(float2 p, float r1, float r2, float h)
{
    p.x = abs(p.x);
    float b = (r1 - r2) / h;
    float a = sqrt(1 - b * b);
    float k = dot(p, float2(-b, a));
    if (k < 0)
        return length(p) - r1;
    if (k > a * h)
        return length(p - float2(0, h)) - r2;
    return dot(p, float2(a, b)) - r1;
}

// The cell of an irregular pattern of cells (Voronoi) a point falls in, so things break into
// angular chunks instead of squares.
float2 VoronoiCell(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float best = 8;
    float2 cell = i;
    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float2 g = float2(x, y);
            float2 o = float2(Hash(float3(i + g, 1.7)), Hash(float3(i + g, 8.3)));
            float2 r = g + o - f;
            float d = dot(r, r);
            if (d < best)
            {
                best = d;
                cell = i + g;
            }
        }
    }
    return cell;
}

// The cell of VoronoiCell, and how far the point is from the nearest border between cells (0 on
// a border; Inigo Quilez), so cracks can run along the borders the pieces later break apart at.
float2 VoronoiCellBorder(float2 p, out float border)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float best = 8;
    float2 cell = i;
    float2 toBest = 0;
    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float2 g = float2(x, y);
            float2 o = float2(Hash(float3(i + g, 1.7)), Hash(float3(i + g, 8.3)));
            float2 r = g + o - f;
            float d = dot(r, r);
            if (d < best)
            {
                best = d;
                cell = i + g;
                toBest = r;
            }
        }
    }
    border = 8;
    [unroll]
    for (int v = -1; v <= 1; v++)
    {
        [unroll]
        for (int u = -1; u <= 1; u++)
        {
            float2 g = float2(u, v);
            float2 o = float2(Hash(float3(i + g, 1.7)), Hash(float3(i + g, 8.3)));
            float2 r = g + o - f;
            float2 between = r - toBest;
            if (dot(between, between) > 1e-5)
                border = min(border, dot(0.5 * (toBest + r), normalize(between)));
        }
    }
    return cell;
}

// The colour of a flame at heat h: dark, the tint, yellow, then white-hot.
half3 HeatColor(float h, half3 tint)
{
    half3 c = lerp(tint * half3(0.55, 0.22, 0.12), tint, saturate(h * 2.5));
    c = lerp(c, half3(1.0, 0.86, 0.5), saturate(h * 2.5 - 1.0));
    return lerp(c, half3(1.0, 0.98, 0.92), saturate(h * 3.0 - 2.0));
}

// The colour of burning gas at heat h (0 to 1), as a real flame's: dark red at its coolest, then
// red, orange, yellow, and white only at its hottest. The tint sets the flame's own hue where it is
// saturated (a coloured flame keeps it); a pale tint (white or yellow, as a fresh flame's is) still
// cools through orange and red at its edges, and a dark tint (a dying flame) darkens all of it.
half3 FlameColor(float h, half3 tint)
{
    float top = max(tint.r, max(tint.g, tint.b));
    float low = min(tint.r, min(tint.g, tint.b));
    float sat = (top - low) / max(top, 1e-3);
    half3 warm = half3(1.0, 0.42, 0.12) * top;
    half3 mid = lerp(warm, tint, saturate(sat * 1.6 - 0.3));
    half3 c = lerp(mid * half3(0.45, 0.2, 0.12), mid, saturate(h * 2.2));
    c = lerp(c, half3(1.0, 0.8, 0.38) * top, saturate(h * 2.4 - 1.0));
    return lerp(c, half3(1.0, 0.97, 0.88) * top, saturate(h * 3.2 - 2.2));
}

// --- Light shapes (additive) -----------------------------------------------------------------

// A soft round light (glows, pools of light, the hot flash of a blow): a small hot core and a wide
// halo as two Gaussians of their own (F2), the core whiter, the halo a little uneven so a pool of
// light never reads as a drawn disc.
half4 ShapeGlow(ShapeIn s)
{
    float2 o = SeedOffset(s.seed);
    float uneven = Fbm4(s.dir * 1.3 + s.r * 1.6 + o, o.x + s.k * 0.6);
    float rr = s.r * (1 + (uneven - 0.5) * 0.2);
    float halo = exp(-rr * rr * 4.2) * 0.92 + exp(-rr * rr * 1.6) * 0.08;
    float core = exp(-s.r * s.r / (2 * 0.075 * 0.075));
    float edge = 1 - smoothstep(0.72, 0.97, s.r);
    half3 col = s.tint * halo + lerp(s.tint, 1, 0.65) * core * 0.45;
    return half4(col * edge, 1);
}

// A ring of shock (F9): a hot white line at a sharp front, uneven round the ring; a soft halo,
// wider inside where the air has been passed, and a faint trail; streaks along it and sparks
// around it. It thins as it spreads and breaks into arcs from halfway, eaten by a noise that mixes
// the direction and the radius so it never cuts like slices of a pie, its broken ends glowing.
half4 ShapeRing(ShapeIn s)
{
    float life = s.rem;
    float t = 1 - life;
    float2 o = SeedOffset(s.seed);
    float px = max(s.aa, 1e-4);
    float front = 0.74 + (Fbm4(s.dir * 1.6 + o, 3.1 + t * 1.5) - 0.5) * 0.06;
    float d = s.r - front;

    float thin = lerp(1, 0.45, t);
    float outer = 1 - smoothstep(0, max(0.016, px), d - 0.016);
    float haloWidth = (d < 0 ? 0.045 : 0.025) * thin;
    float halo = Gauss(d, haloWidth) * 0.42;
    float trail = exp(min(d, 0) / (0.1 * thin)) * 0.13;
    float streak = Noise(float3(s.dir * 3, s.r * 38 + t * 3)) * 0.7
        + Noise(float3(s.dir * 7 + 2.1, s.r * 90 + t * 5)) * 0.3;
    float band = outer * (halo + trail) * (0.3 + 1.26 * smoothstep(0.35, 0.85, streak));

    // The front's brightness is uneven round it, so it never reads as a drawn outline.
    float hot = 0.3 + 1.0 * Fbm4(s.dir * 2.2 + o.yx, 5.9 + t * 2);
    float core = Line(d, 0.009 * thin, px * 0.9) * 1.5 * hot;

    float arcs = Fbm4(s.dir * 1.8 + float2(s.r * 3.0, 0) + o, 7.7);
    float cut = saturate((t - 0.3) / 0.7) * 0.75;
    float keep = smoothstep(cut - 0.12, cut + 0.12, arcs);
    float boundary = keep * (1 - keep) * 4 * step(0.001, cut);

    float around = atan2(s.p.y, s.p.x) / Tau + 0.5;
    float slice = floor(around * 64);
    float h1 = Hash(float3(slice, 1.3, 0.7));
    float h2 = Hash(float3(slice, 7.1, 2.9));
    float h3 = Hash(float3(slice, 4.2, 5.3));
    float sparkR = front + (h3 - 0.65) * 0.2 + t * (0.04 + 0.12 * h2);
    float da = (around - (slice + 0.35 + 0.3 * h2) / 64) * Tau * s.r;
    float dr = s.r - sparkR;
    float sparkSize = max(0.005 + 0.005 * h3, px);
    float spark = step(0.36, h1) * exp(-(da * da + dr * dr) / (sparkSize * sparkSize))
        * (0.6 + 0.4 * sin(t * 24 + h1 * 20)) * saturate(1.2 - t * 1.2);

    half3 light = s.tint * band * keep
        + lerp(s.tint, 1, 0.75) * core * keep
        + lerp(s.tint, 1, 0.5) * Gauss(d, 0.03) * boundary * 0.6
        + lerp(s.tint, 1, 0.6) * spark * 2.5;
    return half4(light * saturate(life * 1.25), 1);
}

// The flash of a blow: eight rays of different lengths around a hot centre, each a thin white
// core inside a wider halo of the tint (F2). The rays shorten and thin as it is eaten away.
half4 ShapeStar(ShapeIn s)
{
    float rot = s.seed * Tau;
    float px = max(s.aa, 1e-4);
    float core = 0;
    float halo = 0;
    [unroll]
    for (int i = 0; i < 8; i++)
    {
        float main = (i % 2 == 0) ? 1.0 : 0.6;
        float a = rot + i * 0.7853982 + (Hash1(i, s.seed) - 0.5) * 0.3;
        float2 u = float2(cos(a), sin(a));
        float along = dot(s.p, u);
        float perp = abs(s.p.x * u.y - s.p.y * u.x);
        float len = (main > 0.9 ? 0.92 : 0.58) * (0.75 + 0.25 * Hash1(i + 8, s.seed))
            * (0.35 + 0.65 * s.rem);
        float t = saturate(along / len);
        float w = (0.05 * (1 - t) + 0.004) * (0.4 + 0.6 * s.rem);
        float on = step(0, along) * (1 - t);
        halo += on * Gauss(perp, w * 1.8) * main * 0.55;
        core += on * Line(perp, w * 0.4, px) * main;
    }
    core += Gauss(s.r, 0.06);
    halo += Gauss(s.r, 0.16) * 0.7 + Gauss(s.r, 0.35) * 0.25 * s.rem;
    float edge = 1 - smoothstep(0.85, 0.98, s.r);
    half3 col = s.tint * halo + lerp(s.tint, 1, 0.8) * core * 1.2;
    return half4(col * edge, 1);
}

// A fireball flying along +x, as burning gas streaming off a hot head: a white-hot heart at the
// head and a halo around it (F2); a body of flame whose pattern streams back faster toward the
// tail and curls (two warps, F4), cooling from white to yellow, the tint and red along it and torn
// into tongues at the tail; it is eaten from the tail (F5). x is its light, w the soot its cooled
// tail leaves (for the rich look; 0 to about 0.35).
float4 FireballLook(ShapeIn s)
{
    const float headX = 0.55;
    float2 o = SeedOffset(s.seed);
    float back = headX - s.p.x;
    float along = saturate(back / 1.45);
    float2 fp = float2(s.p.x * 2.6 + s.k * (6.0 + 6.0 * along), s.p.y * 3.6) + o;
    float2 w = Warp(fp, o.x + s.k * 1.2, 0.7 + 0.6 * along);
    float big = Fbm4(w, o.y);
    float fine = Fbm4(w * 2.6 + 9.7, o.y + 4.0);
    float flow = big * 0.7 + fine * 0.3;
    float y = s.p.y + (big - 0.5) * 0.2 * along;
    float d = UnevenCapsuleDistance(float2(y, back), 0.27, 0.02, 1.4);
    d += (flow - 0.5) * (0.05 + 0.18 * along);
    float tear = smoothstep(along * 0.6 - 0.12, along * 0.6 + 0.12, fine);
    float body = smoothstep(0.03, -0.1, d) * (0.55 + 0.65 * flow) * lerp(1, tear, along);
    float heat = saturate(smoothstep(0.0, -0.3, d) * (1 - along * 0.85) + 0.12);
    float headD = length((s.p - float2(headX - 0.04, 0)) * float2(1.0, 1.15));
    float core = Gauss(headD, 0.1);
    float glow = Gauss(headD, 0.32) * 0.35 + Gauss(max(d, 0), 0.1) * 0.3 * (1 - along);
    float keep = Keep((1 - along) * 0.6 + flow * 0.4, s.rem, 0.1);
    half3 col = FlameColor(saturate(heat + core * 0.6), s.tint) * body
        + lerp(s.tint, 1, 0.85) * core * 0.9
        + FlameColor(0.5, s.tint) * glow;
    float soot = body * smoothstep(0.35, 0.9, along) * (1 - heat) * 0.35;
    float fade = BoardFade(s.p);
    return float4(col * keep * fade, soot * keep * fade);
}

half4 ShapeFireball(ShapeIn s)
{
    return half4(FireballLook(s).rgb, 1);
}

static const float IceSpearTip = 0.86;

// The half width of an ice lance at x along it: a long thin shaft from -0.9 swelling into a
// faceted head that comes to its tip.
float IceSpearWidth(float x)
{
    if (x > 0.55)
        return 0.13 * saturate((IceSpearTip - x) / (IceSpearTip - 0.55));
    if (x > 0.4)
        return lerp(0.055, 0.13, (x - 0.4) / 0.15);
    return lerp(0.02, 0.05, saturate((x + 0.9) / 1.3)) * step(-0.9, x);
}

// An ice lance flying along +x: a long shaft swelling into a faceted head, a thin bright ridge
// and rims at least a pixel wide, glints in the frost of its facets, and a cold glow around the
// head and down the shaft that is a halo of its own.
half4 ShapeIceSpear(ShapeIn s)
{
    float x = s.p.x;
    float y = s.p.y;
    float w = IceSpearWidth(x);
    float aa = max(s.aa, 0.004);
    float inside = 1 - smoothstep(w - aa, w + aa, abs(y));
    float2 o = SeedOffset(s.seed);
    float frost = Fbm4(float2(x * 9.0, y * 22.0) + o, o.x);
    float facet = (y > 0 ? 0.8 : 0.38) * (0.8 + 0.4 * frost);
    float shaft = step(-0.9, x) * step(x, IceSpearTip);
    float ridge = Line(y, 0.005, aa) * shaft;
    float rim = Line(abs(y) - w, 0.006, aa) * shaft * step(0.001, w);
    float glint = smoothstep(0.62, 0.8, frost) * inside;
    float light = inside * (facet * 0.55 + 0.35 * glint);
    float halo = Gauss(length(s.p - float2(0.68, 0)), 0.3) * 0.25
        + Gauss(y, 0.08) * smoothstep(0.4, -0.9, x) * 0.25 * step(-1.0, x);
    half3 col = s.tint * (light + halo) + lerp(s.tint, 1, 0.8) * (ridge * 1.3 + rim * 1.0);
    return half4(col * BoardFade(s.p), 1);
}

// The x of a lightning path at height y (in half-heights from the foot), jagged from one
// segment to the next, pinned to the foot.
float BoltX(float f, float seed)
{
    float i = floor(f);
    float t = frac(f);
    float a0 = (Hash1(i, seed) - 0.5) * 0.24 * smoothstep(0, 3, i)
        + (Noise(float3(i * 0.25, seed * 13.0, 1.7)) - 0.5) * 0.25 * smoothstep(0, 4, i);
    float i1 = i + 1;
    float a1 = (Hash1(i1, seed) - 0.5) * 0.24 * smoothstep(0, 3, i1)
        + (Noise(float3(i1 * 0.25, seed * 13.0, 1.7)) - 0.5) * 0.25 * smoothstep(0, 4, i1);
    return lerp(a0, a1, t);
}

// Where a real lightning channel runs at height y01 (0 at its foot, 1 at the top of the board),
// in the square units of the lightning board: a slow wander, sharp turns every ninth of the height
// and a fine tortuous jitter, all drawn in to the foot so it strikes its target.
float RichBoltX(float y01, float seed)
{
    float f = y01 * 9.0;
    float i = floor(f);
    float zig = lerp(Hash1(i, seed), Hash1(i + 1, seed), frac(f)) - 0.5;
    float wander = Noise(float3(y01 * 1.7, seed * 13.0, 3.1)) - 0.5;
    float fine = Noise(float3(y01 * 46.0, seed * 7.0, 5.3)) - 0.5;
    return (zig * 0.3 + wander * 0.5 + fine * 0.03) * smoothstep(0.0, 0.3, y01);
}

// How far q is from the channel of RichBoltX, measured across its slope.
float RichBoltDistance(float2 q, float y01, float span, float seed)
{
    float x0 = RichBoltX(y01, seed);
    float x1 = RichBoltX(y01 + 0.004, seed);
    float slope = (x1 - x0) / (0.004 * span);
    return abs(q.x - x0) / sqrt(1 + slope * slope);
}

// How far q is from the channel of RichBoltX near y01, measured to a polyline through the channel
// sampled above and below: the slope-based distance is wrong away from the channel's sharp turns,
// where it jumps from row to row and smeared the halo into horizontal streaks. Close to the
// channel the slope-based one is kept, as it follows the fine jitter between the samples.
float BoltDistance(float2 q, float y01, float span, float seed)
{
    const float foot = -0.82;
    float best = 8;
    float2 prev = float2(RichBoltX(y01 - 0.12, seed), foot + (y01 - 0.12) * span);
    [unroll]
    for (int j = -5; j <= 6; j++)
    {
        float yy = y01 + j * 0.02;
        float2 cur = float2(RichBoltX(yy, seed), foot + yy * span);
        float2 pa = q - prev;
        float2 ba = cur - prev;
        float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-6));
        best = min(best, length(pa - ba * h));
        prev = cur;
    }
    float near = RichBoltDistance(q, y01, span, seed);
    return lerp(min(near, best + 0.004), best, smoothstep(0.015, 0.05, best));
}

// One layer of a cloud-to-ground lightning flash as a real one looks, from the cloud at the top of
// the board down to its foot (at 9% of the height; the board 0.62 times as wide as it is tall). A
// flash is drawn as four boards with the same seed, so they share one channel, each its own layer
// with its own timing and cover from the caller: part 1, the core, a thin white-hot line on the
// main channel only; part 2, the channel, a narrower bright body in the tint with the branches
// forked off it (and, before the stroke, the stepped leader); part 3, the halo, a wide soft glow of
// the air round the channel and its branches; part 4, the afterimage, a thin trace of the channel
// that is left in the eye and breaks up as it is eaten. The progress k tells the stage: up to 0.5
// the stepped leader comes down in six steps (only the channel and the halo draw it, faint, its
// tips brightest); past 0.5 it has struck, and the branches dim within a fifth of the rest, since
// the strokes that follow light the same main channel again. Light only, for the premultiplied
// blend (alpha 0).
half4 ShapeRichLightning(ShapeIn s, float part)
{
    float2 q = float2(s.p.x * 0.62, s.p.y);
    const float foot = -0.82;
    const float top = 0.92;
    const float span = top - foot;
    float y01 = (q.y - foot) / span;
    float aa = max(s.aa * 0.62, 0.0015);
    float leading = step(s.k, 0.5);
    // The leader's front, stepping down: everything above it has formed.
    float front = 1 - floor(saturate(s.k / 0.5) * 6.0 + 1.0) / 6.0;
    float age = saturate((s.k - 0.5) / 0.5);
    float formed = leading > 0.5 ? smoothstep(front - 0.01, front + 0.01, y01) : 1;

    // The main channel, uneven along it and brighter low down.
    float d = BoltDistance(q, y01, span, s.seed);
    d = y01 < 0 ? length(q - float2(0, foot)) : d;
    float along = 0.7 + 0.3 * Noise(float3(y01 * 7.0, s.seed * 3.0, 1.1)) + 0.25 * (1 - y01);

    // The branches, forked off the channel, reaching down and out and fading to their tips: the
    // distance to the nearest and how strongly it is lit there.
    float branchCore = 0;
    float branchGlow = 0;
    [unroll]
    for (int b = 0; b < 7; b++)
    {
        float start = lerp(0.3, 0.95, Hash1(b + 20, s.seed));
        float side = Hash1(b + 30, s.seed) < 0.5 ? -1 : 1;
        float len = lerp(0.12, 0.4, Hash1(b + 40, s.seed));
        float down = start - y01;
        float xb = RichBoltX(start, s.seed) + side * down * span * lerp(0.35, 0.8, Hash1(b + 50, s.seed))
            + (Noise(float3(down * 30.0, b * 3.7, s.seed * 11.0)) - 0.5) * 0.05 * saturate(down * 8.0);
        float db = abs(q.x - xb) * 0.8;
        float on = step(0, down) * (1 - smoothstep(len * 0.5, len, down)) * (1 - down / len);
        branchCore += Gauss(db, max(aa, 0.005)) * on;
        branchGlow += Gauss(db, 0.05) * on;
    }
    float branchesLit = lerp(1 - smoothstep(0.0, 0.2, age), 0.5, leading);
    // The leader's tips glow brightest as they feel their way down.
    float tip = leading * Gauss(y01 - front, 0.02) * Gauss(d, 0.03);

    half3 col;
    if (part < 1.5)
    {
        // The core: white-hot, only once it has struck.
        float core = Gauss(d, max(aa, 0.012 * (1 - 0.5 * age))) * along * (1 - leading);
        col = half3(1, 1, 1) * core * 2.0;
    }
    else if (part < 2.5)
    {
        // The channel and its branches, white toward the middle and the tint at the sides.
        float body = Gauss(d, max(aa, 0.022)) * along * lerp(1.0, 0.35, leading);
        float branches = branchCore * branchesLit;
        col = lerp(s.tint, half3(1, 1, 1), lerp(0.75, 0.3, leading)) * (body + branches * 0.9 + tip * 0.8);
    }
    else if (part < 3.5)
    {
        // The halo: the air lit round the channel and, more faintly, round its branches.
        float halo = (Gauss(d, 0.07) * 0.5 + Gauss(d, 0.2) * 0.18) * along;
        col = s.tint * (halo * lerp(1.0, 0.3, leading) + branchGlow * branchesLit * 0.25);
    }
    else
    {
        // The afterimage: a thin trace of the main channel, broken as it is eaten.
        float trace = Gauss(d, max(aa, 0.009)) * 0.8 + Gauss(d, 0.03) * 0.25;
        col = s.tint * trace * along * (1 - leading);
    }
    float keep = Keep(Hash1(floor(y01 * 14.0), s.seed + 0.37) * 0.6 + Noise(float3(q * 9.0, s.seed)) * 0.4, s.rem, 0.08);
    return half4(col * formed * keep * BoardFade(s.p), 0);
}

// A bolt of lightning from the top of the board down to its foot (at 9% of the height; the board
// 0.62 times as wide as it is tall), on one board: the channel of ShapeRichLightning as it looks
// once it has struck, a thin white-hot core, a narrower body of the tint, and a halo of lit air,
// each its own width (F2), with four branches forking off it and a glow where it strikes; a new
// seed strikes a new shape. It breaks up into segments as it is eaten.
half4 ShapeLightning(ShapeIn s)
{
    float2 q = float2(s.p.x * 0.62, s.p.y);
    const float foot = -0.82;
    const float top = 0.92;
    const float span = top - foot;
    float y01 = (q.y - foot) / span;
    float aa = max(s.aa * 0.62, 0.0015);
    float d = BoltDistance(q, y01, span, s.seed);
    d = y01 < 0 ? length(q - float2(0, foot)) : d;
    float along = 0.7 + 0.3 * Noise(float3(y01 * 7.0, s.seed * 3.0, 1.1)) + 0.25 * (1 - y01);
    float branchCore = 0;
    float branchGlow = 0;
    [unroll]
    for (int b = 0; b < 4; b++)
    {
        float start = lerp(0.3, 0.9, Hash1(b + 20, s.seed));
        float side = Hash1(b + 30, s.seed) < 0.5 ? -1 : 1;
        float len = lerp(0.12, 0.35, Hash1(b + 40, s.seed));
        float down = start - y01;
        float xb = RichBoltX(start, s.seed) + side * down * span * lerp(0.35, 0.8, Hash1(b + 50, s.seed))
            + (Noise(float3(down * 30.0, b * 3.7, s.seed * 11.0)) - 0.5) * 0.05 * saturate(down * 8.0);
        float db = abs(q.x - xb) * 0.8;
        float on = step(0, down) * (1 - smoothstep(len * 0.5, len, down)) * (1 - down / len);
        branchCore += Line(db, 0.004, aa) * on;
        branchGlow += Gauss(db, 0.045) * on;
    }
    float core = Line(d, 0.006, aa) * along;
    float body = Gauss(d, max(aa, 0.02)) * along;
    float halo = (Gauss(d, 0.07) * 0.45 + Gauss(d, 0.2) * 0.15) * along;
    float strike = Gauss(length(q - float2(0, foot)), 0.1) * 0.7 * s.rem;
    float keep = Keep(Hash1(floor(y01 * 14.0), s.seed + 0.37) * 0.6 + Noise(float3(q * 9.0, s.seed)) * 0.4, s.rem, 0.08);
    half3 col = half3(1, 1, 1) * (core * 1.6 + branchCore * 0.7)
        + lerp(s.tint, 1, 0.5) * (body * 0.8 + branchCore * 0.4)
        + s.tint * (halo + branchGlow * 0.22);
    col = col * keep + lerp(s.tint, 1, 0.5) * strike;
    return half4(col * BoardFade(s.p), 1);
}

// A pillar of light rising from the feet (at 10% of the height), as light shows in dusty air:
// a soft column whose streaks rise (a turned fBM, faster up high), fine strands in it, a thin
// white core, motes drifting up and a glow on the floor at its foot. It is eaten from the top.
// With dust (the rich look) the floor glow is left to the caller.
half3 PillarLight(ShapeIn s, float withBase)
{
    float x = s.p.x;
    float y = s.p.y;
    float h = saturate((y + 0.8) / 1.75);
    float2 o = SeedOffset(s.seed);
    float px = max(fwidth(x), 1e-4);
    float w = lerp(0.42, 0.3, h);
    float rays = Fbm4(float2(x * 5.0, y * 0.9 - s.k * (2.0 + 1.5 * h)) + o, o.x);
    float fine = Noise(float3(x * 22.0 + o.x, y * 1.5 - s.k * 4.0, o.y));
    float column = Gauss(x, w * 0.7) * (0.5 + 0.9 * smoothstep(0.35, 0.8, rays))
        + Gauss(x, w * 1.5) * 0.2;
    float strands = smoothstep(0.62, 0.9, fine) * Gauss(x, w * 0.8) * 0.45;
    float core = Line(x, 0.025, px) * (0.6 + 0.4 * rays);
    float vertical = smoothstep(-0.85, -0.7, y) * (1 - smoothstep(0.55, 0.95, h + (rays - 0.5) * 0.25));

    float2 cell = float2(x * 6.0, y * 4.0 - s.k * (5.0 + 3.0 * h)) + o;
    float2 ci = floor(cell);
    float2 cf = frac(cell) - 0.5;
    float2 off = float2(Hash(float3(ci, 3.1)), Hash(float3(ci, 5.7))) - 0.5;
    float mote = step(0.7, Hash(float3(ci, 13.0))) * Gauss(length(cf - off * 0.5), max(0.07, px * 3.0))
        * Gauss(x, 0.3) * vertical;

    float base = Gauss(length(float2(x, (y + 0.8) * 3.5)), 0.35) * 0.6 * withBase;
    float keep = Keep((1 - h) * 0.6 + rays * 0.4, s.rem, 0.1);
    half3 col = s.tint * ((column + strands) * vertical * keep + base * s.rem)
        + lerp(s.tint, 1, 0.85) * (core * vertical * keep + mote * keep * 1.4);
    return col * BoardFade(s.p);
}

half4 ShapePillar(ShapeIn s)
{
    return half4(PillarLight(s, 1), 1);
}

// A shaft of light standing up from the feet (at 10% of the height), seen as it is in dusty
// air: a faint haze, a few thin strands that waver and brighten in patches rising up them, a thin
// core and motes of dust glinting as they drift up. No floor glow. Light only (alpha 0).
half4 ShapeDustPillar(ShapeIn s)
{
    float x = s.p.x;
    float y = s.p.y;
    float h = saturate((y + 0.8) / 1.75);
    float2 o = SeedOffset(s.seed);
    float px = max(fwidth(x), 1e-4);
    float w = lerp(0.36, 0.28, h);
    float haze = Gauss(x, w * 0.8) * 0.3 + Gauss(x, w * 1.6) * 0.1;
    float strands = 0;
    [unroll]
    for (int i = 0; i < 6; i++)
    {
        float off = (Hash1(i, s.seed) - 0.5) * w * 1.4;
        float wav = (Noise(float3(y * 2.0 - s.k * 1.5, i * 3.7, o.x)) - 0.5) * 0.04;
        float lit = smoothstep(0.35, 0.75, Fbm4(float2(y * 2.5 - s.k * (3.0 + i * 0.4), i * 5.1) + o, o.y));
        strands += Line(x - off - wav, 0.008 + 0.006 * Hash1(i + 9, s.seed), px) * lit
            * (0.35 + 0.5 * Hash1(i + 20, s.seed));
    }
    float core = Line(x, 0.012, px) * 0.6;
    float vertical = smoothstep(-0.85, -0.68, y) * (1 - smoothstep(0.5, 0.95, h));
    float2 cell = float2(x * 7.0, y * 5.0 - s.k * 4.0) + o;
    float2 ci = floor(cell);
    float2 cf = frac(cell) - 0.5;
    float2 off2 = float2(Hash(float3(ci, 3.1)), Hash(float3(ci, 5.7))) - 0.5;
    float twinkle = 0.4 + 0.6 * sin(s.k * 18.0 + Hash(float3(ci, 4.4)) * Tau);
    float mote = step(0.72, Hash(float3(ci, 13.0))) * Gauss(length(cf - off2 * 0.5), max(0.06, px * 3.0))
        * Gauss(x, w) * twinkle;
    float keep = Keep((1 - h) * 0.6 + Fbm4(s.p * 3.0 + o, 2.0) * 0.4, s.rem, 0.1);
    half3 col = s.tint * (haze + strands * 0.9) + lerp(s.tint, 1, 0.85) * (core + saturate(mote) * 1.6);
    return half4(col * vertical * keep * BoardFade(s.p), 0);
}

// A pillar of fire bursting up from the feet (at 10% of the height): a column of flame whose
// density rises, faster up high, curls (two warps) and tears at its ragged sides and top into
// tongues that stream up; white-hot and yellow low in the middle, the tint, red and dark toward
// its edges and top; a glow round it and a white-hot spot at its foot; and a thin veil of soot
// over its top (alpha up to about 0.35). It is eaten from the top. Premultiplied.
half4 ShapeFirePillar(ShapeIn s)
{
    float x = s.p.x;
    float y = s.p.y;
    float y01 = saturate((y + 0.8) / 1.75);
    float2 o = SeedOffset(s.seed);
    float rise = s.k * 3.0 * (0.6 + 0.9 * y01);
    float2 fp = float2(x * 3.2, y * 1.2 - rise) + o;
    float2 w = Warp(fp, o.x + s.k, 0.7 + 0.5 * y01);
    float big = Fbm4(w, o.y);
    float fine = Fbm4(w * 2.5 + 7.3, o.y + 3.0);
    float width = lerp(0.3, 0.46, y01) * (0.7 + 0.6 * big);
    float field = 1 - abs(x) / width + (big - 0.5) * 1.2 + (fine - 0.5) * 0.55 - y01 * 0.75;
    float vertical = smoothstep(-0.86, -0.74, y) * (1 - smoothstep(0.8, 0.96, y01));
    float shape = smoothstep(0.0, 0.3, field) * vertical;
    // White-hot only deep in its foot; yellow, the tint and red up and out, so it reads as fire,
    // not as a shaft of light.
    float heat = smoothstep(0.2, 1.4, field + (1 - y01) * 0.2 + (fine - 0.5) * 0.3);
    float keep = Keep((1 - y01) * 0.5 + big * 0.5, s.rem, 0.1);
    half3 flame = FlameColor(heat * 0.85 + 0.05, s.tint) * shape * (0.4 + 0.55 * big + 0.25 * fine);
    float halo = smoothstep(-0.7, 0.0, field) * (1 - shape) * 0.16 * vertical * (1 - y01 * 0.5);
    float foot = Gauss(length(float2(x, (y + 0.8) * 3.5)), 0.3) * 0.6;
    half3 light = flame + FlameColor(0.45, s.tint) * halo + FlameColor(0.9, s.tint) * foot * s.rem;
    float soot = shape * (1 - heat) * smoothstep(0.45, 0.9, y01) * 0.35;
    float fade = BoardFade(s.p);
    half a = soot * keep * fade;
    return half4((Soot * a + light * keep) * fade, a);
}

// The centre and offset of the hexagon a point falls in (xy: the cell, zw: the point in it).
float4 HexCell(float2 p)
{
    const float2 r = float2(1, 1.7320508);
    const float2 h = r * 0.5;
    float2 a = p - r * floor(p / r) - h;
    float2 b = (p - h) - r * floor((p - h) / r) - h;
    float2 gv = dot(a, a) < dot(b, b) ? a : b;
    return float4(p - gv, gv);
}

// A dome of light over the party, its base on the floor (at 16% of the height): a rim with a thin
// white core and a halo of its own, a face brighter toward its edges (Fresnel), hexagons drawn as
// hairlines at least a pixel wide, a shimmer sweeping across and a ring on the floor. It breaks by
// dropping its hexagons.
half4 ShapeDome(ShapeIn s)
{
    float2 q = float2(s.p.x / 0.92, (s.p.y + 0.68) / 1.6);
    float e = length(q);
    float pe = max(fwidth(e), 1e-4);
    float above = smoothstep(-0.02, 0.02, q.y);
    float rimCore = Line(e - 1, 0.004, pe) * above;
    float rimHalo = Gauss(e - 1, 0.03) * above;
    float face = (1 - smoothstep(0.97, 1.0, e)) * above;
    float fresnel = pow(saturate(e), 4) * 0.5 + 0.08;

    float4 hex = HexCell(s.p * 7.0 + float2(0, s.seed * 3.0));
    float2 g = abs(hex.zw);
    float edge = 0.5 - max(dot(g, float2(0.5, 0.8660254)), g.x);
    float hexLine = Line(edge, 0.015, max(fwidth(edge), 1e-4));
    float cell = Hash(float3(hex.xy, s.seed * 7.0));
    float shimmer = Gauss(q.x * 1.2 + q.y * 0.8 - (s.k * 3.0 - 1.0), 0.18);
    float light = face * (fresnel + hexLine * 0.4 * (0.6 + 0.8 * fresnel) + shimmer * 0.4) + rimHalo * 0.7;

    float2 b = float2(s.p.x / 0.92, (s.p.y + 0.68) / 0.13);
    float baseRing = Gauss(length(b) - 1, 0.25) * 0.6 * (1 - above * 0.5);
    float keep = Keep(cell * 0.85 + 0.1, s.rem, 0.05);
    half3 col = s.tint * (light * keep + baseRing * s.rem) + lerp(s.tint, 1, 0.8) * rimCore * 1.3 * keep;
    return half4(col * BoardFade(s.p), 1);
}

// A barrier's film (the rich dome and bubble): e is how far out a point is (1 on the rim), inside
// whether it is on the film, hexP where it falls among the hexagons, ripple the distance from the
// ripple's front. A thin film that veils what is behind it a little, more toward its rim where the
// eye passes through more of it (Fresnel); a rim with a white core and a halo; fine hexagons that
// show most toward the rim, each flickering on its own; a ripple running over it once with k; a
// soft highlight. It breaks hexagon by hexagon. Premultiplied.
half4 BarrierFilm(ShapeIn s, float e, float inside, float2 hexP, float ripple, float2 spot)
{
    float pe = max(fwidth(e), 1e-4);
    float fresnel = pow(saturate(e), 3.0);
    float rimCore = Line(e - 1, 0.004, pe);
    float rimHalo = Gauss(e - 1, 0.035) + Gauss(e - 1, 0.1) * 0.25;
    float4 hex = HexCell(hexP + float2(0, s.seed * 3.0));
    float2 g = abs(hex.zw);
    float edge = 0.5 - max(dot(g, float2(0.5, 0.8660254)), g.x);
    float hexLine = Line(edge, 0.012, max(fwidth(edge), 1e-4));
    float cell = Hash(float3(hex.xy, s.seed * 7.0));
    float flicker = 0.6 + 0.4 * sin(s.k * 7.0 + cell * Tau);
    float wave = Gauss(ripple, 0.05) * (1 - smoothstep(0.75, 1.0, s.k));
    float keep = Keep(cell * 0.85 + 0.1, s.rem, 0.05);
    half3 body = s.tint * s.tint * 0.4;
    half a = inside * (0.05 + 0.18 * fresnel + 0.12 * wave) * keep;
    half3 light = s.tint * (inside * (fresnel * 0.35 + hexLine * (0.15 + 0.6 * fresnel) * flicker + wave * 0.7)
            + rimHalo * 0.55)
        + lerp(s.tint, 1, 0.8) * (rimCore * 1.4 + inside * hexLine * wave * 0.8)
        + lerp(s.tint, 1, 0.6) * Gauss(length(spot), 0.12) * inside * 0.5;
    return half4(body * a + light * keep, a);
}

// A dome of light (a barrier) over the party, its base on the floor (at 16% of the height), as
// a film of light (BarrierFilm): its ripple runs from its top down to its base as k goes from 0
// to 1. Premultiplied.
half4 ShapeRichDome(ShapeIn s)
{
    float2 q = float2(s.p.x / 0.92, (s.p.y + 0.68) / 1.6);
    float e = length(q);
    float above = smoothstep(-0.02, 0.02, q.y);
    float inside = (1 - smoothstep(0.98, 1.0, e)) * above;
    float ripple = length(q - float2(0, 1)) - s.k * 1.5;
    half4 c = BarrierFilm(s, e, inside, s.p * 9.0, ripple, q - float2(-0.45, 0.7));
    c *= above;
    float2 b = float2(s.p.x / 0.92, (s.p.y + 0.68) / 0.13);
    c.rgb += s.tint * Gauss(length(b) - 1, 0.25) * 0.5 * (1 - above * 0.5) * s.rem;
    float fade = BoardFade(s.p);
    return c * fade;
}

// A signed distance to an equilateral triangle (Inigo Quilez), r half of a side.
float TriangleDistance(float2 p, float r)
{
    const float k = 1.7320508;
    p.x = abs(p.x) - r;
    p.y = p.y + r / k;
    if (p.x + k * p.y > 0)
        p = float2(p.x - k * p.y, -k * p.x - p.y) / 2;
    p.x -= clamp(p.x, -2 * r, 0);
    return -length(p) * sign(p.y);
}

// A magic circle seen from above: double outer rings with ticks, a band of glyph strokes, a
// hexagram with small circles at its points and an inner ring, each line a thin white core in a
// glow of the tint, glowing unevenly round the circle. It is drawn in around the circle at first
// and eaten away along it at the end.
half4 ShapeMagicCircle(ShapeIn s)
{
    float r = s.r;
    float2 p = s.p;
    float px = max(s.aa, 1e-4);
    float w = max(0.011, px * 1.2);
    float around = atan2(p.y, p.x) / Tau + 0.5;
    float2 lines = GlowStroke(r - 0.93, w, px) + GlowStroke(r - 0.86, w * 0.8, px)
        + GlowStroke(r - 0.6, w, px) + GlowStroke(r - 0.18, w * 0.8, px);

    float tickDist = abs(frac(around * 48 + 0.5) - 0.5) / 48 * Tau * r;
    lines += GlowStroke(tickDist, w, px) * step(0.865, r) * step(r, 0.925);

    float cu = around * 24;
    float ci = floor(cu);
    float2 g = float2((frac(cu) - 0.5) * 2.0, (r - 0.73) / 0.07);
    float glyphHash = Hash1(ci, 4.2);
    float gw = max(0.13, px / 0.09);
    float glyph = step(0.3, glyphHash) * Gauss(g.x, gw) * step(abs(g.y), 0.8);
    glyph += step(0.55, frac(glyphHash * 7.3)) * Gauss(g.y - (frac(glyphHash * 3.1) - 0.5), gw)
        * step(abs(g.x), 0.6);
    glyph += step(0.6, frac(glyphHash * 5.7)) * Gauss(g.x - g.y * 0.6, gw) * step(length(g), 0.85);
    glyph *= step(abs(g.y), 1.0) * step(abs(g.x), 0.75);
    lines += float2(glyph, glyph * glyph * 0.6);

    lines += GlowStroke(abs(TriangleDistance(p, 0.52)), w, px) + GlowStroke(abs(TriangleDistance(-p, 0.52)), w, px);
    [unroll]
    for (int i = 0; i < 6; i++)
    {
        float a = i * 1.0471976 + 1.5707963;
        float2 c = float2(cos(a), sin(a)) * 0.6;
        lines += GlowStroke(length(p - c) - 0.05, w * 0.8, px);
    }

    float2 o = SeedOffset(s.seed);
    float uneven = 0.7 + 0.6 * Fbm4(s.dir * 2.0 + o, o.x + s.k * 0.5);
    float glow = Gauss(r - 0.6, 0.18) * 0.12 + Gauss(r - 0.9, 0.08) * 0.15;
    float drawn = 1 - smoothstep(s.k * 4.0 - 0.05, s.k * 4.0, around);
    float keep = Keep(Fbm4(s.dir * 2.3 + float2(r * 4.0, 0) + o, 1.7) * 0.7 + 0.3 * (1 - r), s.rem, 0.1);
    half3 col = s.tint * (min(lines.x, 1.5) * uneven + glow) + lerp(s.tint, 1, 0.8) * min(lines.y, 1.2) * 0.8 * uneven;
    return half4(col * drawn * keep * BoardFade(s.p), 1);
}

// --- A layered magic circle, burned into the floor ----------------------------------------
// Its parts are drawn on boards of their own, so each turns its own way and keeps its own time
// (the outer band, the band of glyphs, the figure inside, and the veil of heat rising from it).
// The lines glow as hot metal does: dark red when cool, then orange and yellow, with a thin
// white-hot core that only shows, and widens, as they heat. The heat (0 to 1) is taken from the
// brightness above 1 (twice it, from 1 to 1.5), as a hotter line is a brighter one. The parts add their light (alpha 0)
// on the premultiplied material, which thins it with the board.

float CircleHeat(ShapeIn s)
{
    return saturate((s.bright - 1) * 2);
}

// One hot line at a distance of 0, w wide: x its body and halo, y its white-hot core.
float2 HotStroke(float d, float w, float heat)
{
    return float2(
        Gauss(d, w) + Gauss(d, w * 4.0) * 0.15,
        Gauss(d, w * lerp(0.2, 0.55, heat)) * smoothstep(0.2, 1.0, heat));
}

// The colour of hot lines (from HotStroke), glowing unevenly as heated metal does.
half3 HotShade(float2 lines, float heat, half3 tint, float uneven)
{
    half3 hot = lerp(half3(1, 0.72, 0.32), half3(1, 0.97, 0.88), heat);
    return (tint * min(lines.x, 1.6) + hot * min(lines.y, 1.2)) * uneven;
}

// How far round a point is (0 to 1), and whether the part drawn in round to k covers it.
float CircleDrawn(float around, float k)
{
    return 1 - smoothstep(k - 0.02, k, around);
}

// The outer band: two rings, ticks between and small rings just outside. It is drawn in around
// the circle by k, hotter where it is being drawn, and breaks up along its length as it is
// eaten away.
half4 ShapeCircleOuter(ShapeIn s)
{
    float heat = CircleHeat(s);
    float r = s.r;
    float w = max(0.012, s.aa * 1.2);
    float around = atan2(s.p.y, s.p.x) / Tau + 0.5;
    float2 lines = HotStroke(r - 0.94, w * 1.3, heat) + HotStroke(r - 0.86, w * 0.8, heat);
    float tickDist = abs(frac(around * 48 + 0.5) - 0.5) / 48 * Tau * r;
    lines += HotStroke(tickDist, w * 0.9, heat) * step(0.865, r) * step(r, 0.935);
    float a = (round(around * 16) / 16 - 0.5) * Tau;
    lines += HotStroke(length(s.p - float2(cos(a), sin(a)) * 0.975) - 0.014, w * 0.7, heat);

    float lead = Gauss(around - s.k, 0.03) * (1 - step(0.999, s.k));
    float local = saturate(heat + lead);
    lines.y += lines.x * lead * 0.8;
    float uneven = 0.7 + 0.6 * Noise(float3(s.dir * 3.0, s.seed * 7.0));
    float keep = Keep(Noise(float3(s.dir * 4.0, s.seed * 3.0 + 1.3)), s.rem, 0.08);
    half3 col = HotShade(lines, local, s.tint, uneven) * CircleDrawn(around, s.k) * keep;
    return half4(col * BoardFade(s.p), 0);
}

// The band of glyphs between two thin rings: the glyphs light one by one as k goes round, each
// flaring as it lights.
half4 ShapeCircleGlyphs(ShapeIn s)
{
    float heat = CircleHeat(s);
    float r = s.r;
    float w = max(0.01, s.aa * 1.2);
    float around = atan2(s.p.y, s.p.x) / Tau + 0.5;
    float drawn = CircleDrawn(around, s.k);
    float2 lines = (HotStroke(r - 0.8, w * 0.7, heat) + HotStroke(r - 0.665, w * 0.7, heat)) * drawn;

    float cu = around * 20;
    float ci = floor(cu);
    float2 g = float2((frac(cu) - 0.5) * 2.0, (r - 0.732) / 0.05);
    float glyphHash = Hash1(ci, 4.2);
    float gw = max(0.13, s.aa / 0.05);
    float glyph = step(0.3, glyphHash) * Gauss(g.x, gw) * step(abs(g.y), 0.8);
    glyph += step(0.55, frac(glyphHash * 7.3)) * Gauss(g.y - (frac(glyphHash * 3.1) - 0.5), gw)
        * step(abs(g.x), 0.6);
    glyph += step(0.6, frac(glyphHash * 5.7)) * Gauss(g.x - g.y * 0.6, gw) * step(length(g), 0.85);
    glyph *= step(abs(g.y), 1.0) * step(abs(g.x), 0.75);
    float order = (ci + 0.5) / 20;
    float lit = step(order, s.k);
    float flare = 1 + 1.5 * saturate(1 - (s.k - order) * 12);
    lines += float2(glyph * flare, glyph * glyph * glyph * smoothstep(0.2, 1.0, heat)) * lit;

    float uneven = 0.75 + 0.5 * Noise(float3(s.dir * 5.0, s.seed * 5.0));
    float keep = Keep(Noise(float3(s.dir * 6.0, s.seed * 3.0 + 4.1)), s.rem, 0.08);
    half3 col = HotShade(lines, heat, s.tint, uneven) * keep;
    return half4(col * BoardFade(s.p), 0);
}

// The figure inside: a hexagram with small rings at its points, two rings and a small turned
// square in the middle, drawn out from the middle by k.
half4 ShapeCircleInner(ShapeIn s)
{
    float heat = CircleHeat(s);
    float2 p = s.p;
    float r = s.r;
    float w = max(0.011, s.aa * 1.2);
    float2 lines = HotStroke(r - 0.6, w, heat) + HotStroke(r - 0.2, w * 0.8, heat);
    lines += HotStroke(abs(TriangleDistance(p, 0.52)), w, heat)
        + HotStroke(abs(TriangleDistance(-p, 0.52)), w, heat);
    [unroll]
    for (int i = 0; i < 6; i++)
    {
        float a = i * 1.0471976 + 1.5707963;
        lines += HotStroke(length(p - float2(cos(a), sin(a)) * 0.6) - 0.05, w * 0.8, heat);
    }
    float2 q = abs(Rotate(p, 0.785398));
    lines += HotStroke(max(q.x, q.y) - 0.09, w * 0.8, heat);

    float drawn = 1 - smoothstep(s.k * 0.7 - 0.05, s.k * 0.7, r);
    float uneven = 0.75 + 0.5 * Noise(float3(p * 3.0, s.seed * 9.0));
    float keep = Keep(Noise(float3(p * 4.0, s.seed * 3.0 + 7.7)), s.rem, 0.08);
    half3 col = HotShade(lines, heat, s.tint, uneven) * drawn * keep;
    return half4(col * BoardFade(s.p), 0);
}

// The veil of heat rising from the circle's rim, on a standing board as wide as the rim with its
// foot at the bottom: it stands on the rim's front curve (the circle lies flat, so that curve
// sags to the bottom edge in the middle and reaches 0.18 of the board up at the sides), is
// brightest at its two sides, where the eye looks through more of it, and rises in wavering
// streaks that thin out as they go up. k moves the streaks up; it is eaten away from its foot.
half4 ShapeCircleVeil(ShapeIn s)
{
    float heat = CircleHeat(s);
    float y = saturate((s.uv.y - 0.04) / 0.96);
    float x = s.p.x + (Noise(float3(y * 3.0 - s.k * 2.0, s.seed * 4.0, 0.7)) - 0.5) * 0.14;
    float side = saturate(1 - x * x);
    float rise = y - 0.18 * (1 - sqrt(side));
    float streak = Noise(float3(x * 9.0, y * 1.6 - s.k * 3.0, s.seed * 5.0));
    streak = 0.3 + 1.1 * smoothstep(0.35, 0.85, streak);
    // Brightest a little inside its sides, so they do not stand as straight lines.
    float limb = 0.35 + 0.65 * (1 - side) * (1 - side);
    float body = smoothstep(0.0, 0.05, rise) * exp(-rise * 2.4)
        * (1 - smoothstep(0.5, 0.95, rise + (streak - 0.6) * 0.25));
    float edge = 1 - smoothstep(0.7, 1.0, abs(x) + (streak - 0.6) * 0.1);
    // Nothing below the rim it stands on.
    float stand = smoothstep(-0.015, 0.005, rise) * edge;
    float foot = Gauss(rise - 0.02, 0.04) * (0.4 + 0.6 * heat) * stand;
    float keep = Keep(y * 0.6 + streak * 0.3, s.rem, 0.1);
    float light = (body * limb * streak * stand + foot * 0.6) * keep;
    half3 col = s.tint * light + foot * heat * keep * half3(1, 0.9, 0.7) * 0.5;
    return half4(col, 0);
}

// A streak of a spark flying along +y (F7): a hot head near its top and a tail that tapers and
// fades back from it, the head and the middle of the tail a white-hot core inside a narrow glow
// of the tint, never thinner than a pixel across.
half4 ShapeStreak(ShapeIn s)
{
    float px = max(fwidth(s.p.x), 1e-4);
    float along = saturate(s.p.y * 0.5 + 0.5);
    float w = 0.34 * pow(along, 1.6) + 0.04;
    float ends = smoothstep(0.0, 0.25, along) * (1 - smoothstep(0.9, 0.99, along));
    float body = Line(s.p.x, w, px) * ends * pow(along, 1.2);
    float core = Line(s.p.x, w * 0.3, px * 0.8) * smoothstep(0.35, 0.85, along) * (1 - smoothstep(0.9, 0.99, along));
    float head = Gauss(s.p.x, max(w * 0.5, px)) * Gauss(along - 0.84, 0.07);
    half3 col = s.tint * body * 0.9 + lerp(s.tint, 1, 0.8) * (core * 0.9 + head * 0.8);
    return half4(col, 1);
}

// A round spark: a white-hot point at least a pixel wide (F2's core), a glow of the tint around
// it (its halo), flickering a little as it flies.
half4 ShapeSpark(ShapeIn s)
{
    float px = max(s.aa, 1e-4);
    float flicker = 0.85 + 0.15 * sin(s.k * 40.0 + s.seed * Tau);
    float core = Line(s.r, 0.13, px * 0.8);
    float halo = Gauss(s.r, 0.42) * 0.6 + Gauss(s.r, 0.22) * 0.3;
    float edge = 1 - smoothstep(0.75, 0.97, s.r);
    half3 col = s.tint * halo + lerp(s.tint, 1, 0.75) * core * 1.25 * flicker;
    return half4(col * edge, 1);
}

// A mote of a crumbling body: a square dot a third of the board across, upright like the pixel
// art it came off, lit through and in a soft glow of its own colour.
half4 ShapeMote(ShapeIn s)
{
    float box = max(abs(s.p.x), abs(s.p.y));
    float aa = max(s.aa, 0.02);
    float mote = 1 - smoothstep(0.34 - aa, 0.34 + aa, box);
    float glow = Gauss(max(box - 0.34, 0), 0.24) * (1 - mote) * BoardFade(s.p);
    return half4(s.tint * (mote + glow * 0.4), 1);
}

// A twinkle: four long rays and four short ones between, each a thin white core (at least a pixel
// wide) in a softer glow of the tint, round a hot middle; the rays lengthen and shorten as it
// twinkles.
half4 ShapeSparkle(ShapeIn s)
{
    float2 p = s.p;
    float px = max(s.aa, 1e-4);
    float twinkle = 0.8 + 0.2 * sin(s.k * 14.0 + s.seed * Tau);
    float len = 0.92 * twinkle;
    float2 fx = saturate(1 - abs(p.yx) / len);
    float core = Line(p.x, 0.022, px) * pow(fx.x, 2.2) + Line(p.y, 0.022, px) * pow(fx.y, 2.2);
    float halo = Gauss(p.x, 0.08) * pow(fx.x, 1.5) + Gauss(p.y, 0.08) * pow(fx.y, 1.5);
    float2 q = Rotate(p, 0.785398);
    float2 fq = saturate(1 - abs(q.yx) * 1.7 / len);
    core += 0.4 * (Line(q.x, 0.018, px) * pow(fq.x, 2.0) + Line(q.y, 0.018, px) * pow(fq.y, 2.0));
    halo += 0.3 * (Gauss(q.x, 0.06) * fq.x * fq.x + Gauss(q.y, 0.06) * fq.y * fq.y);
    core += Gauss(s.r, 0.07);
    halo += Gauss(s.r, 0.22) * 0.5;
    half3 col = s.tint * halo * 0.45 + lerp(s.tint, 1, 0.85) * core * 1.1;
    return half4(col, 1);
}

// A thin shard of light: a long diamond with a lit facet and a darker one, hairline rims and a
// spine as white cores at least a pixel wide, and a soft glow of the tint round it; it shrinks as
// it goes.
half4 ShapeShard(ShapeIn s)
{
    float d = abs(s.p.x) / 0.32 + abs(s.p.y) / 0.95;
    float pd = max(fwidth(d), 1e-4);
    float body = 1 - smoothstep(1 - pd, 1 + pd, d);
    float facet = (s.p.x > 0 ? 0.65 : 0.35) + 0.15 * s.p.y;
    float rim = Line(d - 0.96, 0.02, pd) * body;
    float spine = Line(s.p.x, 0.01, max(fwidth(s.p.x), 1e-4)) * body * 0.7;
    float halo = Gauss(max(d - 1, 0), 0.18) * 0.3 * (1 - body);
    float keep = Keep(1 - d * 0.8, s.rem, 0.08);
    half3 col = s.tint * (body * facet * 0.7 + halo) + lerp(s.tint, 1, 0.8) * (rim * 1.1 + spine);
    return half4(col * keep, 1);
}

// A torn lump of flame, as burning gas: its density rises, faster up high (so it stretches into
// licks), and curls (two warps, F4); a round bottom licked up into tongues that split and are eaten
// from their thin tips (F5); coloured by its heat, white-hot and yellow deep inside, the tint, red
// and dark toward its edges, with a soft glow round it. x is its light, w the soot its cooled tips
// leave (for the rich look; 0 to about 0.35).
float4 FlameLook(ShapeIn s)
{
    float2 p = s.p;
    float y01 = p.y * 0.5 + 0.5;
    float2 o = SeedOffset(s.seed);
    float rise = s.k * 2.5 * (0.8 + 0.5 * y01);
    float2 fp = float2(p.x * 2.4, p.y * 1.3 - rise) + o;
    float2 w = Warp(fp, o.x + s.k * 0.8, 0.3 + 0.35 * y01);
    float big = Fbm4(w, o.y);
    float fine = Fbm4(w * 2.3 + 13.1, o.y + 2.0);
    float n = big * 0.65 + fine * 0.35;
    float2 q = float2(p.x * (1.0 + 0.5 * y01), p.y * 0.8 + 0.1);
    float field = 0.95 - length(q) * 1.55 + (n - 0.5) * 1.1 + max(q.y, 0) * (n - 0.45) * 1.3
        + (fine - 0.5) * 0.2;
    float env = (1 - smoothstep(0.75, 0.93, p.y)) * (1 - smoothstep(0.72, 0.92, abs(p.x)))
        * smoothstep(-0.95, -0.78, p.y);
    float shape = smoothstep(0.03, 0.14, field) * env;
    float heat = smoothstep(0.08, 0.95, field + (fine - 0.5) * 0.2);
    float keep = Keep(saturate(field * 1.2) * 0.5 + n * 0.5, s.rem, 0.1);
    float boundary = keep * (1 - keep) * 4 * step(0.001, 1 - s.rem);
    half3 col = FlameColor(heat * 0.95 + 0.05, s.tint) * shape * (0.7 + 0.6 * n);
    col += FlameColor(0.45, s.tint) * boundary * shape * 0.3;
    float halo = smoothstep(-0.22, 0.05, field) * (1 - shape) * env * 0.1;
    col = col * keep + FlameColor(0.4, s.tint) * halo * keep;
    float soot = shape * (1 - heat) * smoothstep(0.2, 0.8, y01) * 0.35 * keep;
    return float4(col * BoardFade(p), soot * BoardFade(p));
}

half4 ShapeFlame(ShapeIn s)
{
    return half4(FlameLook(s).rgb, 1);
}

// A tongue of flame rising from its root at the bottom, as burning gas: it sways, its pattern
// rises faster toward its tip and curls (two warps), its tip splits into licks and burns away
// from the top down (F5); white-hot at its root's middle, cooling through yellow, the tint and red
// to dark at its edges and tip, with a soft glow round it. x is its light, w the soot of its
// cooled tip (for the rich look).
float4 FlameTongueLook(ShapeIn s)
{
    float y01 = s.p.y * 0.5 + 0.5;
    float2 o = SeedOffset(s.seed);
    float sway = (Fbm4(float2(y01 * 2.0 - s.k * 2.5, 0.5) + o, o.x) - 0.5) * 0.6 * y01;
    float x = s.p.x - sway;
    float w = 0.46 * pow(saturate(1 - y01), 0.9) * smoothstep(0.0, 0.2, y01);
    float rise = s.k * 4.0 * (0.5 + 1.0 * y01);
    float2 wp = Warp(float2(x * 4.0, y01 * 3.0 - rise) + o, o.y + s.k, 0.5 + 0.6 * y01);
    float big = Fbm4(wp, o.x + 1.0);
    float fine = Fbm4(wp * 2.6 + 5.1, o.x + 3.0);
    float n = big * 0.7 + fine * 0.3;
    float across = abs(x) + (n - 0.5) * 0.32 * (0.4 + y01);
    float shape = (1 - smoothstep(w * 0.4, w + 0.03, across)) * (1 - smoothstep(0.85, 0.97, y01));
    float heat = saturate((1 - across / max(w, 1e-3)) * (1.1 - y01) + (fine - 0.5) * 0.1);
    float keep = Keep((1 - y01) * 0.6 + n * 0.4, s.rem, 0.08);
    float boundary = keep * (1 - keep) * 4 * step(0.001, 1 - s.rem);
    half3 col = FlameColor(heat * 0.95 + 0.05, s.tint) * shape * (0.6 + 0.6 * n);
    col += FlameColor(0.45, s.tint) * boundary * shape * 0.3;
    float halo = Gauss(across, max(w, 0.04) * 0.9) * (1 - shape) * 0.07 * smoothstep(0.0, 0.15, y01)
        * (1 - smoothstep(0.75, 0.95, y01));
    col = col * keep + FlameColor(0.4, s.tint) * halo * keep;
    float soot = shape * (1 - heat) * smoothstep(0.4, 0.9, y01) * 0.35 * keep;
    float fade = BoardFade(s.p);
    return float4(col * fade, soot * fade);
}

half4 ShapeFlameTongue(ShapeIn s)
{
    return half4(FlameTongueLook(s).rgb, 1);
}

// A flame's look (FlameLook and the like) drawn premultiplied: its light added and its soot
// veiling what is behind it a little.
half4 FlameRich(float4 look)
{
    return half4(Soot * look.w + look.rgb, look.w);
}

// --- Solid shapes (blended normally) ---------------------------------------------------------

// A burnt patch on the floor (laid flat by the caller): charred ground darkest in the middle with
// a ragged edge that curls (a warped fBM), soot spread thinner round it, a fine grain of ash, the
// char split by fine cracks that glow with embers for a moment at first, and specks round its edge.
half4 ShapeScorch(ShapeIn s)
{
    float2 o = SeedOffset(s.seed);
    float2 p = s.p;
    float big = Fbm5(Warp(p * 2.0 + o, o.x, 0.9), o.y);
    float fine = Fbm4(p * float2(9.0, 6.0) + o * 1.7, o.x + 2.0);
    float env = 1 - smoothstep(0.62, 0.9, s.r);
    float edge = s.r + (0.5 - big) * 0.6;
    float body = (1 - smoothstep(0.28, 0.72, edge)) * env;
    float dens = body * (0.55 + 0.45 * smoothstep(0.62, 0.2, edge)) * (0.75 + 0.5 * fine);
    float fringe = (1 - smoothstep(0.45, 0.85, edge)) * env * 0.35 * fine * (1 - body);
    float border;
    float2 cell = VoronoiCellBorder(p * float2(8.0, 5.5) + o, border);
    float bw = max(fwidth(border), 1e-4);
    float crack = (1 - smoothstep(bw * 0.5, bw * 1.3, border)) * smoothstep(0.45, 0.2, edge) * step(0.6, Hash(float3(cell, 4.1)));
    float speck = step(0.8, Noise(float3(p * 20.0 + o, 2.3))) * (1 - smoothstep(0.5, 0.92, edge)) * env * 0.5;
    float keep = Keep(dens * 0.6 + big * 0.4, s.rem, 0.1);
    half a = saturate(dens + fringe + speck * 0.6) * keep;
    // Embers in the cracks, in a glowing hue of the tint, gone within the first third.
    half3 hue = s.tint / max(max(s.tint.r, max(s.tint.g, s.tint.b)), 1e-3);
    float ember = crack * (1 - smoothstep(0.0, 0.3, s.k)) * 0.7;
    half3 col = s.tint * (0.75 + 0.45 * fine) * (1 - crack * 0.4)
        + hue * half3(1.0, 0.55, 0.3) * ember / max(a, 0.05) * keep;
    return half4(col, a);
}

// Frost on the floor seen low from the side (laid flat): a pale patch with a ragged edge, made of
// fine crystal plates with hairline borders, feathers of frost radiating out from the middle at
// a finer scale, a grain of rime, and glints that twinkle on their own as the light catches them.
half4 ShapeFrost(ShapeIn s)
{
    float2 o = SeedOffset(s.seed);
    float2 p = s.p;
    float big = Fbm4(Warp(p * float2(2.4, 1.2) + o, o.x, 0.6), o.y);
    float env = 1 - smoothstep(0.62, 0.92, s.r);
    float cover = (1 - smoothstep(0.35, 0.85, s.r + (big - 0.5) * 0.5)) * env;
    float border;
    float2 cell = VoronoiCellBorder(p * float2(7.0, 4.0) + o, border);
    float bw = max(fwidth(border), 1e-4);
    float plates = 1 - smoothstep(bw * 0.6, bw * 1.8, border);
    float2 squash = float2(s.p.x, s.p.y * 3.0);
    float2 dir = squash / max(length(squash), 1e-3);
    float feather = smoothstep(0.6, 0.85, Noise(float3(dir * 9.0 + o, s.r * 3.0 + o.x)))
        * (0.5 + 0.5 * Noise(float3(dir * 30.0, s.r * 12.0 + o.y)));
    float grain = Fbm4(p * float2(16.0, 9.0) + o * 2.0, o.y + 1.0);
    float glint = smoothstep(0.84, 0.95, Noise(float3(p * float2(30.0, 12.0) + o, 4.4)))
        * (0.5 + 0.5 * sin(s.k * 12.0 + Hash(float3(cell, 2.2)) * Tau));
    float density = cover * (0.28 + 0.2 * grain + feather * 0.5 + plates * 0.22 + glint * 0.5);
    float keep = Keep(cover * 0.5 + big * 0.5, s.rem, 0.1);
    half3 col = s.tint * (0.85 + 0.3 * grain + feather * 0.45) + plates * 0.25 + glint * 1.4;
    return half4(col, saturate(density) * keep);
}

// The outline of a spike of ice standing on its root (at 3% of the height), with its own width,
// tip and facet split from its seed.
struct SpikeShape
{
    // Up the spike: 0 at the root, 1 at the tip.
    float t;
    // In from its nearer side (negative outside).
    float dEdge;
    float halfWidth;
    // 1 inside, 0 outside, smoothed over a pixel.
    float inside;
    // Where its lit facet meets its shadowed one, at this height and at its root.
    float split;
    float splitRoot;
    float tipX;
    // 1 on the lit facet, 0 on the shadowed one.
    float lit;
};

SpikeShape SpikeAt(ShapeIn s)
{
    float b = 0.5 + 0.35 * Hash1(1.0, s.seed);
    SpikeShape g;
    g.tipX = (Hash1(2.0, s.seed) - 0.5) * 0.5;
    g.splitRoot = (Hash1(3.0, s.seed) - 0.5) * b * 0.8;
    g.t = saturate((s.p.y + 0.94) / 1.9);
    float left = lerp(-b, g.tipX, g.t);
    float right = lerp(b, g.tipX, g.t);
    g.dEdge = min(s.p.x - left, right - s.p.x);
    g.halfWidth = (right - left) * 0.5;
    float aa = max(s.aa, 0.01);
    g.inside = smoothstep(-aa, aa, g.dEdge) * step(-0.94, s.p.y) * step(s.p.y, 0.96);
    g.split = lerp(g.splitRoot, g.tipX, g.t);
    g.lit = smoothstep(g.split + 0.02, g.split - 0.02, s.p.x);
    return g;
}

// Where a point of a spike falls among the cells it breaks into.
float2 SpikeCells(ShapeIn s)
{
    return float2(s.p.x * 2.5, s.p.y * 3.5) + s.seed * 10.0;
}

// How much of a spike is kept as rem falls: it crumbles in the chunks of its cells, from the tip
// down.
float SpikeChunkKeep(float2 cell, ShapeIn s, SpikeShape g, float rem)
{
    return Keep(Hash(float3(cell, s.seed * 4.0)) * 0.65 + (1 - g.t) * 0.35, rem, 0.04);
}

float SpikeKeep(ShapeIn s, SpikeShape g)
{
    return SpikeChunkKeep(VoronoiCell(SpikeCells(s)), s, g, s.rem);
}

// A spike of clear ice standing on its root (at 3% of the height): a lit facet and a shadowed
// one with frost toward the tip (a turned fBM), hairline rims and a facet line at least a pixel
// wide, faint cracks; it crumbles in chunks from the tip down.
half4 ShapeIceSpike(ShapeIn s)
{
    SpikeShape g = SpikeAt(s);
    float2 o = SeedOffset(s.seed);
    float aa = max(s.aa, 1e-4);
    half3 face = lerp(half3(0.42, 0.62, 0.88), half3(0.85, 0.95, 1.0), g.lit);
    float frost = Fbm4(float2(s.p.x * 6.0, s.p.y * 2.5) + o, o.x);
    face *= 0.78 + 0.3 * frost + 0.15 * g.t;
    float rim = Line(max(g.dEdge, 0), 0.02, aa) * g.inside;
    float splitLine = Line(s.p.x - g.split, 0.008, aa) * g.inside * 0.7;
    float border;
    VoronoiCellBorder(SpikeCells(s), border);
    float crack = (1 - smoothstep(0, max(fwidth(border), 1e-4) * 1.5, border)) * g.inside * 0.3;
    float keep = SpikeKeep(s, g);
    half3 col = s.tint * (face + rim * 1.1 + splitLine + crack);
    return half4(col, g.inside * (0.5 + 0.45 * rim + 0.2 * splitLine) * keep);
}

// The outline of a broken chip, a skewed diamond turned by its seed: how far out a point is (1 on
// its edge), and q, the point in the chip's own frame.
float ChipDistance(ShapeIn s, out float2 q)
{
    q = Rotate(s.p, s.seed * Tau);
    q.x += q.y * (Hash1(5.0, s.seed) - 0.5) * 0.8;
    float ax = 0.45 + 0.4 * Hash1(6.0, s.seed);
    float ay = 0.75 + 0.2 * Hash1(7.0, s.seed);
    return abs(q.x) / ax + abs(q.y) / ay;
}

// A broken chip (of ice, or of dark debris): a skewed diamond with a lit half and a dark one, a
// hairline rim at least a pixel wide and a faint grain.
half4 ShapeChip(ShapeIn s)
{
    float2 q;
    float d = ChipDistance(s, q);
    float pd = max(fwidth(d), 1e-4);
    float inside = 1 - smoothstep(1 - pd, 1 + pd, d);
    float lit = q.x + q.y > 0 ? 1.0 : 0.5;
    float rim = Line(d - 0.93, 0.03, pd) * inside;
    float grain = Noise(float3(q * 5.0 + SeedOffset(s.seed), 1.3));
    float keep = Keep(1 - d * 0.7, s.rem, 0.08);
    return half4(s.tint * (lit * (0.85 + 0.3 * grain) + rim * 0.6), inside * keep);
}

// --- Shapes of the rich look (premultiplied) -------------------------------------------------
// Drawn premultiplied (One, OneMinusSrcAlpha): the alpha is how much a shape covers (darkens or
// veils) what is behind it and the colour is what it shows, so a deep, half-clear body or a pale
// veil and hard edges of light come from one shape. The tint is the colour of its light; clear
// ice takes the tint squared for its body, a deeper blue of the same hue.

// A spike of clear ice: a deep body, thin in the middle and thicker toward its sides and its
// frosted tip so the floor shows through; an inner edge seen faintly through it; hard bright rims
// and a facet line, brighter on the lit side and toward the tip; a band of light that climbs it
// once as it catches the light; and a glint that twinkles on it. It breaks as rem falls: to 0.9
// its cracks light up, racing up it along the borders of the chunks it is cut into, and below
// 0.9 it breaks into those chunks, each flashing as it goes.
half4 ShapeClearIceSpike(ShapeIn s)
{
    SpikeShape g = SpikeAt(s);
    float aa = max(s.aa, 0.006);
    // 0 at its sides, 1 down its middle.
    float across = saturate(g.dEdge / max(g.halfWidth, 1e-3));
    float n = Noise(float3(s.p.x * 6.0 + s.seed * 7.0, s.p.y * 1.5, s.seed * 3.0));
    float side = 1 - across;
    float cover = g.inside * saturate(0.4 + 0.45 * side * side + 0.2 * g.t + 0.12 * (n - 0.5));
    half3 body = s.tint * s.tint * lerp(0.12, 0.38, g.lit);

    float inner = Gauss(across - 0.45, 0.06) * g.inside;
    float rim = (1 - smoothstep(aa, aa * 3.0, g.dEdge)) * g.inside
        * lerp(0.5, 1.0, g.lit) * (0.55 + 0.45 * g.t);
    float facet = Gauss(s.p.x - g.split, aa * 1.2) * g.inside * (0.3 + 0.7 * g.t);
    float climb = -0.3 + s.k * 1.8;
    float band = Gauss(g.t - climb + s.p.x * 0.35, 0.07) * g.inside * (0.4 + 0.6 * g.lit);
    // Faint fractures run along the borders of its chunks from the start; as rem falls to 0.9
    // they light up, racing from its root to its tip.
    float border;
    float2 cell = VoronoiCellBorder(SpikeCells(s), border);
    float bw = max(fwidth(border), 1e-4);
    float cracking = saturate((1 - s.rem) / 0.1);
    float reached = smoothstep(cracking * 1.3, cracking * 1.3 - 0.15, g.t);
    float crack = (1 - smoothstep(bw * 0.6, bw * 2.0, border)) * g.inside
        * (0.12 + 1.6 * reached * cracking);
    // Below 0.9 it breaks into those chunks, each flashing as it goes.
    float breaking = 1 - smoothstep(0.88, 0.9, s.rem);

    // The glint sits on the facet line high up; the board is half as wide as it is high.
    float gt = lerp(0.4, 0.75, Hash1(4.0, s.seed));
    float2 gp = float2(lerp(g.splitRoot, g.tipX, gt), gt * 1.9 - 0.94);
    float2 dg = (s.p - gp) * float2(0.5, 1.0);
    float twinkle = pow(saturate(sin(s.k * 9.0 + s.seed * Tau)), 8);
    float glint = (Gauss(dg.x, 0.006) * Gauss(dg.y, 0.07) + Gauss(dg.y, 0.006) * Gauss(dg.x, 0.07)
        + Gauss(length(dg), 0.012)) * twinkle;

    float keep = SpikeChunkKeep(cell, s, g, saturate(s.rem / 0.9));
    half3 light = lerp(s.tint, 1, 0.5) * (rim * 2.2 + facet * 1.1 + band * 0.9)
        + lerp(s.tint, 1, 0.75) * (crack * 1.4 + cover * breaking * 1.5)
        + s.tint * inner * 0.3
        + lerp(s.tint, 1, 0.7) * glint * 2.5;
    half a = saturate(cover + rim * 0.5) * keep;
    return half4(body * a + light * keep, a);
}

// An ice lance of clear ice flying along +x: a deep body the background shows through, thicker
// toward its rims; hard bright rims and ridge; a white-hot tip; and a cold glow around the head
// and down the shaft that is only added light.
half4 ShapeClearIceSpear(ShapeIn s)
{
    float x = s.p.x;
    float y = s.p.y;
    float w = IceSpearWidth(x);
    float aa = max(s.aa, 0.004);
    float shaft = step(-0.9, x) * step(x, IceSpearTip);
    float inside = (1 - smoothstep(w - aa, w + aa, abs(y))) * shaft;
    // 0 down its middle, 1 at its rims.
    float side = saturate(abs(y) / max(w, 1e-3));
    float lit = y > 0 ? 1.0 : 0.0;
    float cover = inside * (0.35 + 0.5 * side * side);
    half3 body = s.tint * s.tint * lerp(0.15, 0.45, lit);

    float rim = (1 - smoothstep(aa, aa * 3.0, w - abs(y))) * inside * lerp(0.6, 1.0, lit);
    float head = smoothstep(0.4, 0.6, x);
    float ridge = Gauss(y, max(aa, 0.006)) * inside * (0.8 + 0.8 * head);
    float hot = Gauss(length((s.p - float2(IceSpearTip - 0.06, 0)) * float2(1.0, 2.5)), 0.08);
    float glow = Gauss(length(s.p - float2(0.68, 0)), 0.3) * 0.3
        + Gauss(y, 0.07) * smoothstep(0.4, -0.9, x) * 0.25 * step(-1.0, x);

    half3 light = lerp(s.tint, 1, 0.5) * (rim * 2.0 + ridge)
        + lerp(s.tint, 1, 0.8) * hot * 2.0
        + s.tint * glow;
    half a = saturate(cover + rim * 0.5);
    float fade = BoardFade(s.p);
    return half4((body * a + light) * fade, a * fade);
}

// A chip of clear ice tumbling as it falls: a deep, half-clear body, a hard bright rim on its lit
// side, and a glint that flashes now and then as it turns to catch the light.
half4 ShapeClearChip(ShapeIn s)
{
    float2 q;
    float d = ChipDistance(s, q);
    float aa = max(s.aa * 2.0, 0.03);
    float inside = 1 - smoothstep(1 - aa, 1 + aa, d);
    float lit = q.x + q.y > 0 ? 1.0 : 0.4;
    float rim = smoothstep(1 - aa * 4.0, 1 - aa, d) * inside;
    float glint = pow(saturate(sin(s.k * 26.0 + s.seed * Tau)), 6);
    float keep = Keep(1 - d * 0.7, s.rem, 0.08);
    half3 body = s.tint * s.tint * lerp(0.15, 0.45, lit);
    half a = inside * saturate(0.45 + 0.4 * rim) * keep;
    half3 light = lerp(s.tint, 1, 0.6) * (rim * 1.8 * lit + inside * glint * 2.2);
    return half4(body * a + light * keep, a);
}

// --- Gas and cracks of the rich look (premultiplied) ------------------------------------------
// For gas that really forms (smoke, dust, steam, blowing snow), drawn as real gas looks in light:
// detail at every scale, a colour that changes with brightness, edges that are thicker where the
// eye passes through more of it, and dark lanes. Costly per pixel.

// A patch of gas: its pattern warped twice so it streams and curls (Inigo Quilez's domain
// warping), with its broad density, a fine grain and thin bright filaments.
struct Gas
{
    float density;
    float fine;
    float filament;
};

Gas GasAt(float2 p, float z)
{
    float2 q = float2(Fbm(float3(p, z)), Fbm(float3(p + float2(5.2, 1.3), z))) - 0.5;
    float2 w = p + q * 1.6;
    Gas g;
    g.density = Fbm5(w, z * 1.3);
    g.fine = Fbm5(w * 3.1 + 17.0, z * 2.0);
    // Strands where a noise crosses its middle.
    float strand = 1 - abs(Fbm(float3(w * 2.3 + 9.1, z)) * 2 - 1);
    g.filament = strand * strand * strand * strand;
    return g;
}

// The colour of gas at brightness e, in the hue of its tint: the tint cubed and dark where it is
// faint (deep navy for blue, deep amber for gold), the tint squared, the tint, and white where it
// is brightest.
half3 GasRamp(float e, half3 tint)
{
    float t = saturate(e) * 3;
    half3 c = lerp(tint * tint * tint * 0.35, tint * tint * 0.9, saturate(t));
    c = lerp(c, tint, saturate(t - 1));
    return lerp(c, half3(1, 1, 1), saturate(t - 2));
}

// Smoke, dust or steam as real ones look in light: a billow of gas with detail at every scale,
// ragged and curling at its edge and melting away below, thicker toward its rim where the eye
// passes through more of it, lit from above and shadowed below, with dark lanes in it. It dims as
// it ages (cooling or settling), curls as it goes and wears away into wisps. It is matter, so its
// colour leans to a pale of its tint; a low brightness makes it dark, as soot or dust. Laid flat
// (a low, wide board), it is a sheet creeping along the floor.
half4 ShapeRichSmoke(ShapeIn s)
{
    Gas g = GasAt(s.p * 1.5 + s.seed * 5.0, s.seed * 13.0 + s.k * 0.7);
    float edge = s.r + (0.5 - g.density) * 0.85;
    float soft = lerp(0.06, 0.22, saturate(-s.p.y * 1.4));
    float body = 1 - smoothstep(0.7 - soft, 0.74, edge);
    float rim = smoothstep(0.4, 0.72, edge) * body;
    float lit = saturate(0.4 + s.p.y * 0.9);
    float density = body * (0.45 + 0.8 * g.density) * (0.7 + 0.5 * g.fine);
    float e = density * (0.3 + 0.7 * lit) * (0.65 + 0.7 * rim) * (0.8 + 0.4 * g.filament)
        * lerp(1.1, 0.6, s.k);
    float dust = smoothstep(0.58, 0.82, g.fine) * body * 0.6;
    e *= 1 - dust * 0.6;
    float keep = Keep(g.density * 0.7 + body * 0.3, s.rem, 0.06);
    half a = saturate(density * 0.5 + dust * 0.25) * keep;
    half3 col = lerp(GasRamp(e, s.tint), lerp(s.tint, 1, 0.6), 0.35) * e;
    float fade = BoardFade(s.p);
    return half4(col * keep * fade, a * fade);
}

// Smoke, mist or dust drawn normally: the same gas as ShapeRichSmoke, its premultiplied colour
// turned back into a colour for the normal blend.
half4 ShapeSmoke(ShapeIn s)
{
    half4 c = ShapeRichSmoke(s);
    return half4(c.rgb / max(c.a, 1e-3), c.a);
}

// A billow of a thundercloud (cumulonimbus) as real ones look from below, on a square board: a
// flat, dark base at 29% of the height with ragged scud hanging under it, and a cauliflower top of
// round bulges grown on bulges, each lit from above and shadowed under it, so the creases between
// them are dark, with a pale lining where the sky light catches the top edge. Its gas churns with
// the progress (k). It is gas: the sky shows through its thin parts and soft, wispy edge, and it
// covers more where it is deep and dense, never wholly. The brightness (s.bright, 1
// at rest) is the lightning inside it: above 1 the billow lights up from within around a spot of
// its own, most where the gas is fine, and its edge glows. For the premultiplied blend; the caller
// must not scale it by the brightness again.
half4 ShapeStormCloud(ShapeIn s)
{
    float2 p = s.p;
    Gas g = GasAt(p * 1.6 + s.seed * 5.0, s.seed * 13.0 + s.k * 0.6);

    // The flat base, a little ragged, under a low dark body with rounded ends.
    float baseY = -0.42 + (g.density - 0.5) * 0.08;
    float2 slab = abs(p - float2(0, baseY + 0.2)) - float2(0.42, 0.0);
    float dist = length(max(slab, 0)) + min(max(slab.x, slab.y), 0) - 0.2;
    dist = max(dist, baseY - p.y);
    float lit = 0.12 + 0.25 * saturate((p.y - baseY) * 2.0);
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        float x = lerp(-0.5, 0.5, i / 3.0) + (Hash1(i, s.seed) - 0.5) * 0.16;
        float r = lerp(0.22, 0.34, Hash1(i + 10, s.seed));
        float y = -0.2 + r + 0.25 * (1 - abs(x)) * Hash1(i + 20, s.seed);
        [unroll]
        for (int j = 0; j < 3; j++)
        {
            // The bulge, and two smaller ones grown on its upper side.
            float a = j == 0 ? 0 : lerp(0.35, 1.25, Hash1(i * 3 + j + 60, s.seed)) * (j == 1 ? 1 : -1);
            float rr = j == 0 ? r : r * lerp(0.45, 0.62, Hash1(i * 3 + j + 70, s.seed));
            float2 c = float2(x, y) + (j == 0 ? 0 : float2(sin(a), cos(a)) * (r - rr * 0.35));
            float di = length(p - c) - rr;
            // A smooth union, so the bulges grow out of each other.
            float h = saturate(0.5 + 0.5 * (dist - di) / 0.08);
            dist = lerp(dist, di, h) - 0.08 * h * (1 - h);
            // Each bulge is lit on its top and dark under it; the ones in front hide the creases.
            float inside = 1 - smoothstep(rr * 0.7, rr * 1.02, length(p - c));
            float up = saturate(0.45 + 0.75 * (p.y - c.y) / rr);
            lit = max(lit, inside * (0.15 + 0.85 * up * up));
        }
    }
    float edge = dist + (0.5 - g.density) * 0.16 + (0.5 - g.fine) * 0.06;
    float under = saturate((baseY + 0.06 - p.y) * 6.0);
    float body = 1 - smoothstep(-0.14 - 0.1 * under, 0.03, edge);
    // How deep inside the silhouette, where the eye passes through the most gas.
    float depth = saturate(-edge * 3.0);
    float lining = smoothstep(-0.07, 0.0, edge) * body * saturate(p.y * 1.5 + 0.2);

    float shade = lit * (0.75 + 0.5 * g.density) * (0.8 + 0.4 * g.fine);
    half3 dark = s.tint * s.tint * 0.35;
    half3 col = lerp(dark, s.tint, saturate(shade * 1.6));
    col = lerp(col, lerp(s.tint, 1, 0.5), saturate(shade * 1.6 - 1) * 0.8 + lining * 0.35);
    // Dark lanes where the gas is thin (washed out where lightning lights it from within).
    col *= 1 - 0.35 * smoothstep(0.5, 0.75, 1 - g.fine) * saturate(1.5 - s.bright * 0.5);

    // The lightning inside, around a spot of its own in the middle, seen through the gas.
    float flash = max(0, s.bright - 1);
    float2 spot = float2((Hash1(7, s.seed) - 0.5) * 0.6, -0.1 + Hash1(8, s.seed) * 0.3);
    float hot = exp(-dot(p - spot, p - spot) * 3.0);
    float through = saturate(0.45 + 1.0 * g.fine * g.density + 0.6 * g.filament - 0.25 * (1 - lit));
    float glowing = (flash * body * (0.12 + hot * hot) * through + flash * lining * 0.35) * 0.6;
    half3 inner = half3(0.76, 0.72, 1.0);

    float keep = Keep(g.density * 0.6 + lit * 0.4, s.rem, 0.08);
    half a = body * saturate(0.3 + 0.6 * depth + 0.7 * (g.density - 0.35)) * (0.75 + 0.4 * g.filament) * keep;
    a = min(a, 0.86);
    float fade = BoardFade(p);
    return half4((col * a + inner * glowing * keep) * fade, a * fade);
}

// The dark mass of a thundercloud, the layer behind its billows: a broad, soft body over a flat
// base, darkest low down, its density patchy and its outline torn by the gas so it is no smooth
// round; the sky shows through it everywhere, a little. Lightning inside (s.bright above 1) only
// tinges it, as the billows in front catch most of the light. For the premultiplied blend.
half4 ShapeStormMass(ShapeIn s)
{
    float2 p = s.p;
    Gas g = GasAt(p * float2(1.1, 1.8) + s.seed * 9.0, s.seed * 7.0 + s.k * 0.4);
    float baseY = -0.35 + (g.density - 0.5) * 0.1;
    // A wide dome over the base, its outline broken by the gas.
    float dome = length(float2(p.x * 0.95, max(0, p.y - baseY) * 1.5)) - 0.78;
    float edge = max(dome, baseY - p.y) + (0.5 - g.density) * 0.4 + (0.5 - g.fine) * 0.12;
    float body = 1 - smoothstep(-0.25, 0.05, edge);
    float low = saturate(1 - (p.y - baseY) * 1.6);
    half3 col = lerp(s.tint * 0.7, s.tint * s.tint * 0.3, low) * (0.8 + 0.4 * g.fine);
    float flash = max(0, s.bright - 1);
    half a = body * saturate(0.35 + 0.6 * g.density + 0.2 * low) * 0.85 * Keep(g.density, s.rem, 0.1);
    float fade = BoardFade(p);
    half3 light = half3(0.76, 0.72, 1.0) * flash * 0.12 * body * g.density;
    return half4((col * a + light) * fade, a * fade);
}

// Scud: torn, ragged shreds of cloud hanging under a thundercloud's base and blown along faster
// than it, the front layer of the cloud. Thin and broken, mostly holes, pale grey where they catch
// the light. For the premultiplied blend.
half4 ShapeStormScud(ShapeIn s)
{
    float2 p = s.p;
    Gas g = GasAt(p * float2(1.3, 2.6) + s.seed * 11.0, s.seed * 5.0 + s.k * 0.9);
    float shred = smoothstep(0.42, 0.62, g.density + g.filament * 0.25) * (1 - smoothstep(0.35, 0.95, length(p * float2(1.0, 1.7))));
    float lit = saturate(0.5 + p.y * 0.8);
    half3 col = lerp(s.tint * 0.6, lerp(s.tint, 1, 0.35), lit * g.fine);
    float flash = max(0, s.bright - 1);
    half a = shred * (0.45 + 0.4 * g.fine) * 0.5 * Keep(g.density, s.rem, 0.1);
    float fade = BoardFade(p);
    return half4((col * a + half3(0.76, 0.72, 1.0) * flash * 0.2 * shred) * fade, a * fade);
}

// The light of lightning inside a thundercloud, seen through its gas: a glow in the tint that is
// brightest at its middle and broken up by the gas into bright and dark patches and filaments, so
// it lights the cloud from within rather than lying on it as a round blot; s.bright is how bright
// it is now (it is not scaled again). Light only, for the premultiplied blend (alpha 0).
half4 ShapeStormGlow(ShapeIn s)
{
    float2 p = s.p;
    Gas g = GasAt(p * 1.4 + s.seed * 3.0, s.seed * 17.0 + s.k * 0.5);
    float spread = exp(-dot(p, p) * 3.2) * (1 - smoothstep(0.75, 1.0, s.r));
    float through = saturate(0.2 + 1.3 * g.fine * g.density + 0.9 * g.filament);
    float light = spread * through * s.bright;
    half3 col = lerp(s.tint, half3(1, 1, 1), saturate(light * 0.4)) * light;
    return half4(col * BoardFade(p), 0);
}

// A crack in stone, earth or armour, as a real one looks: dark, jagged lines branching out from
// the middle that split open as it runs (k), wider and darker near the middle, their lips catching
// the light in the tint, with grit scattered along them; it is worn away from its tips. It shows
// by its shadow, not as lines of light.
half4 ShapeFissure(ShapeIn s)
{
    float dark = 0;
    float lip = 0;
    [unroll]
    for (int i = 0; i < 9; i++)
    {
        float a0 = (i + Hash1(i, s.seed) * 0.6) / 9.0 * Tau;
        float len = (0.45 + 0.5 * Hash1(i + 10, s.seed)) * saturate(s.k * 3.0 + 0.2);
        float a = a0 + (Noise(float3(s.r * 7.0, i * 3.1, s.seed * 11.0)) - 0.5) * 0.5;
        float2 u = float2(cos(a), sin(a));
        float along = dot(s.p, u);
        float perp = abs(s.p.x * u.y - s.p.y * u.x);
        float t = saturate(along / max(len, 1e-3));
        float on = step(0, along) * (1 - smoothstep(0.85, 1.0, t));
        float w = max(0.022 * (1 - t) + 0.004, s.aa);
        dark += on * Gauss(perp, w) * (1 - t * 0.5);
        lip += on * Gauss(perp - w * 1.6, max(w * 0.45, s.aa)) * (1 - t);
    }
    dark = saturate(dark + Gauss(s.r, 0.08) * 0.6);
    float2 grid = s.p * 30.0 + s.seed * 7.0;
    float2 ci = floor(grid);
    float grit = Gauss(length(grid - ci - 0.5), 0.12) * step(0.8, Hash(float3(ci, 2.7)))
        * saturate(dark * 3.0 + lip);
    float keep = Keep(1 - s.r * 0.9, s.rem, 0.1);
    float fade = BoardFade(s.p);
    half a = saturate(dark * 0.85 + grit * 0.5) * keep * fade;
    half3 col = lerp(s.tint, 1, 0.4) * saturate(lip) * 0.5 * keep * fade + s.tint * 0.05 * a;
    return half4(col, a);
}

// --- The ground cracked round a point (ShapeRupture) -------------------------------------------
// The floor is broken into pieces round the point it is stamped on, as stone and dry earth break:
// the cracks are the borders of Voronoi cells laid out in log-polar coordinates, so they run out
// from the point and are joined by arcs, the pieces small near it and larger further out. The
// map is conformal (the same scale both ways, K / r), so a distance worked out in it is a distance
// on the floor times K / r.

// How many pieces go round the point, and how many times the ground snaps further.
static const float RuptureAround = 9.0;
static const int RuptureSnaps = 6;

// A cell's seed point in the log-polar grid, wrapped round the point (0.1 to 0.9 of the cell).
float2 RuptureSeed(float2 cell, float seed)
{
    cell.y = fmod(fmod(cell.y, RuptureAround) + RuptureAround, RuptureAround);
    return 0.1 + 0.8 * float2(Hash(float3(cell, seed)), Hash(float3(cell, seed + 1.7)));
}

// The nearest crack to a point q of the log-polar grid: x the distance to it (in the grid), y how
// far from the middle it is (the radius of its midpoint), z a number of its own (0 to 1, plus 2
// when it runs round the middle rather than out from it), w a number of the piece the point is on.
float4 RuptureEdge(float2 q, float seed)
{
    float2 ip = floor(q);
    float2 fp = frac(q);
    float2 mg = 0;
    float2 mr = 0;
    float md = 8;
    [unroll]
    for (int j = -1; j <= 1; j++)
    [unroll]
    for (int i = -1; i <= 1; i++)
    {
        float2 g = float2(i, j);
        float2 r = g + RuptureSeed(ip + g, seed) - fp;
        float d = dot(r, r);
        if (d < md)
        {
            md = d;
            mr = r;
            mg = g;
        }
    }
    float piece = Hash(float3(fmod(fmod(ip.y + mg.y, RuptureAround) + RuptureAround, RuptureAround), ip.x + mg.x, seed + 5.3));
    md = 8;
    float2 mid = 0;
    float own = 0;
    [unroll]
    for (int y = -2; y <= 2; y++)
    [unroll]
    for (int x = -2; x <= 2; x++)
    {
        float2 g = mg + float2(x, y);
        float2 r = g + RuptureSeed(ip + g, seed) - fp;
        float2 between = r - mr;
        if (dot(between, between) > 1e-5)
        {
            float e = dot(0.5 * (mr + r), normalize(between));
            if (e < md)
            {
                md = e;
                mid = 0.5 * (mr + r);
                // An arc when the pieces it parts lie one outside the other.
                float arc = step(0.7, abs(normalize(between).x));
                own = Hash(float3(ip + g + ip + mg, seed + 9.1)) + arc * 2;
            }
        }
    }
    float midR = exp((q.x + mid.x) / (RuptureAround / Tau));
    return float4(md, midR, own, piece);
}

// How far out the ground has cracked after n snaps (0 to 1 of the board's radius).
float RuptureReachAfter(float n)
{
    return n <= 0 ? 0 : 0.1 + 0.8 * pow(saturate(n / RuptureSnaps), 1.2);
}

// The darkness of the cracks at board point p, and of the one just broken: x every crack the
// ground has broken so far, y those the last snap broke; z a number of the piece p is on.
float3 RuptureCracks(float2 p, float reach, float before, float aa, float seed)
{
    const float K = RuptureAround / Tau;
    float r = max(length(p), 1e-3);
    float2 q = float2(log(r) * K, atan2(p.y, p.x) * K);
    float4 e = RuptureEdge(q, seed);
    float d = e.x * r / K;
    float own = frac(e.z);
    bool arc = e.z >= 2;
    // Hairline, measured in pixels (aa) so it stays one to two pixels however the floor lies:
    // wider near the middle and by a number of its own, fading out to its tips. Arcs are
    // thinner, and half of them never break, so the pieces do not ring the middle like a web.
    float w = aa * 1.3 * lerp(2.0, 1.0, saturate(r * 2.5)) * (0.75 + 0.5 * own) * (arc ? 0.8 : 1.0);
    float taper = 1 - smoothstep(0.6, 0.95, e.y);
    float slit = (1 - smoothstep(w * 0.5, w * 1.2, d)) * taper * step(0.035, r)
        * (arc ? step(0.5, own) : 1.0);
    float shown = step(e.y, reach);
    float fresh = shown * step(before, e.y);
    return float3(slit * shown, slit * fresh, e.w);
}

// The ground cracked round the point it is stamped on, seen from above (laid onto the floor by the
// caller, not turned, so up the board is away from the camera): hairline cracks dark as slits,
// their far side (up the board) lit where the far wall of the slit faces the camera and catches
// the light from above, the stamped point crushed dark; it cracks outward in sudden snaps (ぴき),
// each reaching at once a step further out, the cracks a snap has just broken catching the light
// for a moment; at the stamp the pieces near the point tip a little, some lighter, some darker. k
// counts the snaps so far (0 to RuptureSnaps), its fraction the time since the last (0 just after
// it). It closes up from the outside as it is eaten away. It shows by its shadow, not as light.
half4 ShapeRupture(ShapeIn s)
{
    float snaps = floor(s.k);
    float since = frac(s.k);
    // Worn away from the outside in: what it reaches shrinks back to the middle.
    float reach = RuptureReachAfter(snaps) * saturate(s.rem * 1.2);
    float before = RuptureReachAfter(snaps - 1);
    float3 here = RuptureCracks(s.p, reach, before, s.aa, s.seed);
    // The far wall: a crack just below a point (down the board, nearer the camera) lights it.
    float3 below = RuptureCracks(s.p - float2(0, s.aa * 1.6), reach, before, s.aa, s.seed);
    float wall = saturate(below.x - here.x);
    float crushed = Gauss(length(s.p), 0.04) * step(0.5, snaps);
    float stamped = step(RuptureSnaps - 0.5, snaps);
    float tipped = stamped * (here.z - 0.5) * 0.24 * (1 - smoothstep(0.15, 0.45, length(s.p)))
        * step(length(s.p), reach);
    float fade = BoardFade(s.p);
    half a = saturate(here.x * 0.95 + crushed * 0.8 + max(-tipped, 0)) * fade;
    half3 col = s.tint * (wall * 0.55 + max(tipped, 0)) * fade
        + s.tint * 1.2 * here.y * (1 - since) * (1 - since) * 0.5 * fade;
    return half4(col, a);
}

// --- Blades and beams of the rich look (premultiplied, all light) ------------------------------

// A sword arc, as a real blade looks when it swings fast: a sheaf of thin strands along the
// crescent (its afterimages), the outermost white-hot along its leading edge where the metal
// catches the light and the inner ones fainter in the tint; behind them a short motion blur that
// fades inward, striated along the arc and gone soon after the blade has passed; and a glint at
// the tip as it draws the arc. No cloud: a blade leaves none. It is drawn in from one tip to the
// other, then eaten away.
half4 ShapeRichSlashArc(ShapeIn s)
{
    float2 q = s.p - float2(0, -0.35);
    float rr = length(q);
    float ang = atan2(q.y, q.x);
    const float a0 = 0.3;
    const float a1 = 2.84;
    float t = saturate((ang - a0) / (a1 - a0));
    float inArc = step(a0, ang) * step(ang, a1);
    float shape = pow(saturate(sin(Pi * t)), 0.8);
    // 0 on its leading (outer) edge, below 0 inside it.
    float d = rr - 0.88;
    float aa = max(s.aa, 0.004);

    float strands = 0;
    float hot = 0;
    [unroll]
    for (int i = 0; i < 5; i++)
    {
        float h1 = Hash1(i, s.seed);
        float start = Hash1(i + 5, s.seed) * 0.25 * min(i, 1);
        float end = 1 - Hash1(i + 10, s.seed) * 0.25 * min(i, 1);
        float span = saturate((t - start) / max(end - start, 0.05));
        float taper = pow(saturate(sin(Pi * span)), 0.7) * step(start, t) * step(t, end);
        float offset = -(i * 0.032 + h1 * 0.018) * shape;
        float w = max(aa, (0.004 + 0.007 * (1 - i * 0.2)) * taper);
        float stroke = Gauss(d - offset, w) * taper;
        hot += i == 0 ? stroke : 0;
        strands += i == 0 ? 0 : stroke * (0.35 + 0.45 * h1);
    }

    // Fine streaks across the arc, long along it, as a blur is.
    float streak = Noise(float3(d * 90.0, t * 1.5 + s.seed * 5.0, s.seed * 3.0));
    float smear = exp(min(d, 0) / max(0.12 * shape, 0.01)) * (1 - smoothstep(0, 0.02, d)) * shape
        * (0.35 + 0.65 * smoothstep(0.3, 0.8, streak)) * saturate(1.2 - s.k * 1.5);

    float headAt = s.k * 3.5;
    float drawn = 1 - smoothstep(headAt - 0.15, headAt + 0.05, t);
    float glint = Gauss(t - min(headAt, 1.0), 0.04) * step(headAt, 1.15) * hot;
    float keep = Keep(streak * 0.5 + shape * 0.5, s.rem, 0.08) * drawn * inArc;
    half3 col = lerp(s.tint, 1, 0.9) * hot * 2.2
        + s.tint * strands * 1.3
        + lerp(s.tint, 1, smear * smear * 0.5) * smear * 0.9
        + glint * 2.0;
    float fade = BoardFade(s.p);
    return half4(col * keep * fade, 0);
}

// The finishing cross cut: two blades of light crossing, each a white-hot edge with a few
// fainter strands beside it of different lengths and a short motion blur striated along it,
// shooting out to full length over a hot centre, then eaten away from their ends.
half4 ShapeRichSlashCross(ShapeIn s)
{
    float aa = max(s.aa, 0.004);
    half3 col = 0;
    float grain = Noise(float3(s.p * 6.0, s.seed * 9.0));
    [unroll]
    for (int b = 0; b < 2; b++)
    {
        float a = (b == 0 ? 0.785398 : 2.356194) + (s.seed - 0.5) * 0.3 * (b == 0 ? 1 : -1);
        float2 u = float2(cos(a), sin(a));
        float along = dot(s.p, u);
        float perp = s.p.x * u.y - s.p.y * u.x;
        float len = 0.95 * (0.55 + 0.45 * saturate(s.k * 4.0));
        float t = saturate(abs(along) / len);
        float taper = 1 - t * t;
        float keep = Keep((1 - t) * 0.85 + grain * 0.15, s.rem, 0.1);
        float hot = Gauss(perp, max(aa, 0.009 * taper)) * taper;
        float strands = 0;
        [unroll]
        for (int i = 1; i < 4; i++)
        {
            float off = (Hash1(i + b * 4, s.seed) - 0.5) * 0.09 * taper;
            float reach = 0.55 + 0.45 * Hash1(i + 20 + b * 4, s.seed);
            strands += Gauss(perp - off, max(aa, 0.004 * taper)) * taper * step(t, reach)
                * (0.4 + 0.3 * Hash1(i + 9 + b * 4, s.seed));
        }
        float streak = Noise(float3(perp * 90.0, along * 1.5 + s.seed * 5.0, b * 3.1));
        float smear = Gauss(perp, 0.05 * taper + 0.006) * taper
            * (0.3 + 0.7 * smoothstep(0.3, 0.8, streak)) * saturate(1.2 - s.k * 1.5);
        col += (lerp(s.tint, 1, 0.9) * hot * 2.4 + s.tint * strands * 1.3 + s.tint * smear * 0.9) * keep;
    }
    col += lerp(s.tint, 1, 0.7) * Gauss(s.r, 0.12) * 1.2 * s.rem;
    float fade = BoardFade(s.p);
    return half4(col * fade, 0);
}

// One layer of a sword arc, the crescent of ShapeRichSlashArc for the same seed, drawn in from one
// tip (its head at k * 3.5 of the arc) and eaten the same way, so the layers drawn on boards of
// their own line up. Light only. part 2 is the halo: a wide, soft light of the tint hugging the
// arc, mostly inside its leading edge; part 3 the core: the white-hot leading edge alone, at least
// a pixel wide, and the glint at its tip; part 4 the afterimage: the fainter inner strands,
// broken along their length as rem falls.
half3 SlashArcLayer(ShapeIn s, float part)
{
    float2 q = s.p - float2(0, -0.35);
    float rr = length(q);
    float ang = atan2(q.y, q.x);
    const float a0 = 0.3;
    const float a1 = 2.84;
    float t = saturate((ang - a0) / (a1 - a0));
    float inArc = step(a0, ang) * step(ang, a1);
    float shape = pow(saturate(sin(Pi * t)), 0.8);
    float d = rr - 0.88;
    float px = max(s.aa, 1e-4);
    float streak = Noise(float3(d * 90.0, t * 1.5 + s.seed * 5.0, s.seed * 3.0));
    float headAt = s.k * 3.5;
    float drawn = 1 - smoothstep(headAt - 0.15, headAt + 0.05, t);
    half3 col = 0;
    if (part < 2.5)
    {
        float halo = Gauss(d + 0.03 * shape, 0.03 + 0.05 * shape) * 0.8
            + Gauss(d + 0.06 * shape, 0.02 + 0.12 * shape) * 0.3;
        col = s.tint * halo * shape * (0.75 + 0.4 * smoothstep(0.2, 0.8, streak))
            * Keep(streak * 0.5 + shape * 0.5, s.rem, 0.08);
    }
    else if (part < 3.5)
    {
        float taper = pow(saturate(sin(Pi * t)), 0.7);
        float offset = -Hash1(0, s.seed) * 0.018 * shape;
        float hot = Line(d - offset, 0.011 * taper, px) * taper;
        float glint = Gauss(t - min(headAt, 1.0), 0.04) * step(headAt, 1.15) * hot;
        col = (lerp(s.tint, 1, 0.9) * hot * 2.2 + glint * 2.0) * Keep(streak * 0.5 + shape * 0.5, s.rem, 0.08);
    }
    else
    {
        float strands = 0;
        [unroll]
        for (int i = 1; i < 5; i++)
        {
            float h1 = Hash1(i, s.seed);
            float start = Hash1(i + 5, s.seed) * 0.25;
            float end = 1 - Hash1(i + 10, s.seed) * 0.25;
            float span = saturate((t - start) / max(end - start, 0.05));
            float taper = pow(saturate(sin(Pi * span)), 0.7) * step(start, t) * step(t, end);
            float offset = -(i * 0.032 + h1 * 0.018) * shape;
            float stroke = Line(d - offset, (0.004 + 0.007 * (1 - i * 0.2)) * taper, px) * taper;
            float broken = Keep(Noise(float3(t * 16.0, i * 3.1, s.seed * 7.0)), s.rem, 0.06);
            strands += stroke * (0.35 + 0.45 * h1) * broken;
        }
        col = s.tint * strands * 1.3;
    }
    return col * drawn * inArc * BoardFade(s.p);
}

// A sweeping sword arc on one board: the rich arc's white edge, strands and short blur, with its
// halo (SlashArcLayer part 2) under it, for the additive material.
half4 ShapeSlashArc(ShapeIn s)
{
    return half4(ShapeRichSlashArc(s).rgb + SlashArcLayer(s, 2) * 0.5, 1);
}

// The finishing cross cut on one board: the rich cross cut, for the additive material.
half4 ShapeSlashCross(ShapeIn s)
{
    return half4(ShapeRichSlashCross(s).rgb, 1);
}

// A shaft of light along +x across the board, seen as real light is in the air: a smooth column,
// brightest down its middle, that shows because the dust in the air scatters it, with specks of
// that dust glinting in it as they drift; a thin white-hot core and a few fainter strands. Drawn
// in from the left end and worn away.
half4 ShapeRichBeam(ShapeIn s)
{
    float x01 = s.p.x * 0.5 + 0.5;
    float y = s.p.y;
    float aa = max(s.aa, 0.004);
    float ends = smoothstep(0.0, 0.08, x01) * (1 - smoothstep(0.9, 1.0, x01));
    float w = 0.45 * ends + 1e-3;
    float haze = exp(-(y * y) / (w * w)) * ends * (0.75 + 0.25 * (1 - x01));
    // Dust in the shaft: one speck in some cells of a grid that drifts along it, twinkling.
    float2 grid = float2(s.p.x * 18.0 - s.k * 3.0, y * 6.0) + s.seed * 7.0;
    float2 ci = floor(grid);
    float2 speck = ci + float2(Hash(float3(ci, 3.1)), Hash(float3(ci, 6.7)));
    float mote = Gauss(length((grid - speck) * float2(1.0, 0.5)), 0.05) * step(0.75, Hash(float3(ci, 9.9)))
        * (0.4 + 0.6 * sin(s.k * 20.0 + Hash(float3(ci, 4.4)) * Tau)) * haze;
    float core = Gauss(y, max(aa, 0.03 * ends)) * ends;
    float strands = 0;
    [unroll]
    for (int i = 0; i < 3; i++)
    {
        float off = (Hash1(i, s.seed) - 0.5) * 0.5;
        float flicker = 0.5 + 0.5 * sin(s.k * 30.0 + i * 2.1 + s.seed * Tau);
        strands += Gauss(y - off, max(aa, 0.012)) * ends * flicker;
    }
    float drawn = 1 - smoothstep(s.k * 4.0 - 0.1, s.k * 4.0, x01);
    float keep = Keep(x01 * 0.5 + Noise(float3(s.p * 4.0, s.seed * 5.0)) * 0.5, s.rem, 0.08) * drawn;
    half3 col = lerp(s.tint, 1, 0.9) * core * 1.8 + s.tint * strands * 0.6
        + lerp(s.tint * s.tint, s.tint, haze) * haze * 0.8 + lerp(s.tint, 1, 0.8) * saturate(mote) * 2.0;
    float fade = BoardFade(float2(s.p.x * 0.98, s.p.y));
    return half4(col * keep * fade, 0);
}

// One layer of a shaft of light along +x, the shaft of ShapeRichBeam for the same seed, drawn in
// from its left end (k * 4) and worn away the same way. Light only. part 2 the halo: a wide, soft
// light of the tint round it, brightening in swells that flow along it, with no core; part 3 the
// core: a thin white-hot line alone, flickering a little; part 4 the afterimage: thin strands of
// the tint beside it, broken along their length as rem falls.
half3 BeamLayer(ShapeIn s, float part)
{
    float x01 = s.p.x * 0.5 + 0.5;
    float y = s.p.y;
    float px = max(fwidth(y), 1e-4);
    float2 o = SeedOffset(s.seed);
    float ends = smoothstep(0.0, 0.08, x01) * (1 - smoothstep(0.9, 1.0, x01));
    float drawn = 1 - smoothstep(s.k * 4.0 - 0.1, s.k * 4.0, x01);
    float keep = Keep(x01 * 0.5 + Noise(float3(s.p * 4.0, s.seed * 5.0)) * 0.5, s.rem, 0.08);
    half3 col = 0;
    if (part < 2.5)
    {
        float swell = 0.7 + 0.6 * Fbm4(float2(s.p.x * 3.0 - s.k * 8.0, 0.5) + o, o.x);
        float halo = Gauss(y, 0.16 * ends + 1e-3) * 0.6 + Gauss(y, 0.42 * ends + 1e-3) * 0.2;
        col = s.tint * halo * ends * swell * keep;
    }
    else if (part < 3.5)
    {
        float flicker = 0.85 + 0.15 * sin(s.k * 40.0 + s.seed * Tau);
        col = lerp(s.tint, 1, 0.92) * Line(y, 0.02 * ends, px) * ends * 2.2 * flicker * keep;
    }
    else
    {
        float strands = 0;
        [unroll]
        for (int i = 0; i < 3; i++)
        {
            float off = (Hash1(i, s.seed) - 0.5) * 0.5;
            float broken = Keep(Noise(float3(s.p.x * 10.0, i * 3.7, s.seed * 9.0)), s.rem, 0.06);
            strands += Line(y - off, 0.01, px) * ends * broken * (0.5 + 0.3 * Hash1(i + 7, s.seed));
        }
        col = s.tint * strands * 1.2;
    }
    return col * drawn * BoardFade(float2(s.p.x * 0.98, s.p.y));
}

// Ice freezing over the floor, seen low from the side (the board is flattened to lie on it, so
// the plates are flattened as the floor is): plates of clear ice that freeze outward from the
// middle one by one over the first fifth of its life, each appearing whole when the freezing
// front reaches its centre, so the edge is jagged and the growth snaps; hard bright cracks between
// the plates; thin feathers of frost shooting out past the edge; each plate catching the light a
// little differently, and a glint sweeping across. A thin, clear cover with hard edges of light.
// It breaks up plate by plate as it is eaten.
half4 ShapeIceSheet(ShapeIn s)
{
    float border;
    float2 cell = VoronoiCellBorder(s.p * 4.5 + s.seed * 10.0, border);
    float2 centre = cell + float2(Hash(float3(cell, 1.7)), Hash(float3(cell, 8.3)));
    float plateR = length((centre - s.seed * 10.0) / 4.5);
    float front = saturate(s.k * 5.0) * 1.1;
    float frozen = step(plateR + Hash(float3(cell, 5.5)) * 0.12, front) * step(plateR, 0.92);

    float bw = max(fwidth(border), 1e-4);
    float crack = (1 - smoothstep(bw * 0.6, bw * 1.8, border)) * frozen;
    // Feathers of frost: sparse thin streaks out along the radius, just past the edge.
    float edge = min(front, 0.92);
    float ang = atan2(s.p.y, s.p.x);
    float rays = smoothstep(0.78, 0.92, Noise(float3(cos(ang) * 16.0, sin(ang) * 16.0, s.r * 2.0 + s.seed * 4.0)));
    float feather = rays * step(edge - 0.08, s.r) * (1 - smoothstep(edge - 0.02, edge + 0.14, s.r))
        * step(0.05, front);
    float shade = Hash(float3(cell, 7.7));
    float sweep = -1.4 + saturate((s.k - 0.15) / 0.45) * 2.8;
    float glint = Gauss(s.p.x + s.p.y * 0.6 - sweep, 0.12) * frozen * (0.3 + 0.7 * step(0.7, shade));

    float keep = Keep(Hash(float3(cell, 3.3)) * 0.7 + (1 - plateR) * 0.3, s.rem, 0.02);
    half3 body = s.tint * s.tint * (0.25 + 0.2 * shade);
    half a = frozen * (0.22 + 0.1 * shade) * keep;
    half3 light = lerp(s.tint, 1, 0.7) * (crack * 1.6 + glint * 1.2)
        + lerp(s.tint, 1, 0.5) * feather * 0.9 + s.tint * frozen * shade * 0.12;
    return half4(body * a + light * keep, a + crack * 0.25 * keep);
}

// Snow blown along by a strong wind, seen from the side, the wind running toward +x across a
// band of the stage: a few long, thin streams low in the band that snake and fray, fibrous along
// the wind and fine across it, gusting brighter and fainter as they go. The pattern runs through
// the band with the wind (k), the fine fibres faster than the streams, while the band stays: snow
// flows past rather than a picture sliding. Pale snow with a little cover.
half4 ShapeWindStream(ShapeIn s)
{
    float u = s.p.x * 2.0 - s.k * 4.8;
    float y = s.p.y;
    float gust = smoothstep(0.25, 0.75, Noise(float3(u * 0.35, s.seed * 7.0, 0.5)));
    float density = 0;
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        float lane = -0.65 + i * 0.28 + (Hash1(i, s.seed) - 0.5) * 0.15;
        float snake = (Noise(float3(u * 0.8 + i * 13.1, s.seed * 3.0, 1.7)) - 0.5) * 0.35;
        float width = (0.03 + 0.09 * Noise(float3(u * 0.6 + i * 5.3, s.seed * 5.0, 2.9))) * (1 - i * 0.15);
        density += Gauss(y - lane - snake, width) * (1 - i * 0.18);
    }
    // Short fibres that break up into patches, not lines drawn across the whole stage.
    float fibre = Fbm5(float2(s.p.x * 7.0 - s.k * 18.0, y * 28.0), s.seed * 11.0);
    float patches = smoothstep(0.35, 0.65, Noise(float3(s.p.x * 2.5 - s.k * 6.0, y * 3.0, s.seed * 13.0)));
    density *= (0.15 + 1.0 * smoothstep(0.4, 0.8, fibre)) * (0.35 + 0.65 * gust) * patches;
    float ends = smoothstep(-1.0, -0.75, s.p.x) * (1 - smoothstep(0.75, 1.0, s.p.x));
    float rows = smoothstep(-1.0, -0.85, y) * (1 - smoothstep(0.6, 0.95, y));
    density *= ends * rows;
    half a = saturate(density * 0.4);
    return half4(lerp(s.tint, 1, 0.55) * density * 0.75, a);
}

// --- Shapes of the card skills (additive) ----------------------------------------------------

// A beam along +x across the board (F2, F4): a thin white-hot core, a sheath of the tint streaked
// along its length that ripples as the energy flows down it, a wide faint halo of its own, and
// swells of light racing along; drawn in from the left end and eaten away from it.
half4 ShapeBeam(ShapeIn s)
{
    float x01 = s.p.x * 0.5 + 0.5;
    float2 o = SeedOffset(s.seed);
    float px = max(fwidth(s.p.y), 1e-4);
    float flow = Fbm4(float2(s.p.x * 3.0 - s.k * 10.0, 0.3) + o, o.x);
    float ends = smoothstep(0.0, 0.08, x01) * (1 - smoothstep(0.92, 1.0, x01));
    float w = 0.16 * (0.75 + 0.5 * flow) * ends;
    float y = abs(s.p.y);
    float striation = 0.7 + 0.6 * smoothstep(0.3, 0.8, Noise(float3(s.p.x * 4.0 - s.k * 14.0, s.p.y * 30.0, o.y)));
    float sheath = Gauss(y, max(w, 1e-3)) * (0.45 + 0.55 * flow) * striation;
    float core = Line(y, w * 0.18, px);
    float halo = Gauss(y, w * 3.0 + 1e-3) * 0.18;
    float rings = Gauss(frac(s.p.x * 4.0 - s.k * 6.0) - 0.5, 0.06) * Gauss(y, w * 1.3 + 1e-3) * 0.35;
    float drawn = 1 - smoothstep(s.k * 4.0 - 0.1, s.k * 4.0, x01);
    float keep = Keep(x01 * 0.7 + flow * 0.3, s.rem, 0.08);
    half3 col = s.tint * (sheath * 0.9 + halo + rings) + lerp(s.tint, 1, 0.85) * core * 1.6;
    return half4(col * drawn * keep * BoardFade(float2(s.p.x * 0.98, s.p.y)), 1);
}

// An arrow (or a needle) of light flying along +x: a sharp head at 82% of the width with a lit
// facet and hairline rims, a shaft whose white core is at least a pixel wide in a glow of the
// tint, swept vanes at the tail, a hot tip, and a trail streaming behind that tapers and fades
// back from it (F7).
half4 ShapeArrow(ShapeIn s)
{
    float x = s.p.x;
    float y = s.p.y;
    float px = max(fwidth(y), 1e-4);
    float aa = max(s.aa, 0.004);
    float inHead = step(0.5, x) * step(x, 0.82);
    float headW = 0.12 * saturate((0.82 - x) / 0.32);
    float head = (1 - smoothstep(headW - aa, headW + aa, abs(y))) * inHead;
    float facet = y > 0 ? 0.95 : 0.55;
    float rim = Line(abs(y) - headW, 0.005, px) * inHead;
    float onShaft = step(-0.62, x) * step(x, 0.55);
    float shaft = Line(y, 0.006, px) * onShaft;
    float shaftGlow = Gauss(y, 0.03) * onShaft * 0.4;
    float fx = x + 0.62;
    float vane = (0.2 - fx) * 0.5;
    float fletch = step(0, fx) * step(fx, 0.2) * (1 - smoothstep(vane - aa, vane + aa, abs(y)));
    float back = saturate((0.55 - x) / 1.55);
    float streak = 0.7 + 0.6 * smoothstep(0.3, 0.8, Noise(float3(x * 6.0, y * 40.0, s.seed * 5.0)));
    float trail = Gauss(y, 0.02 + 0.09 * back) * pow(1 - back, 1.4) * step(-1.0, x) * step(x, 0.55) * 0.55 * streak;
    float tip = Gauss(length(s.p - float2(0.8, 0)), 0.06);
    float tipGlow = Gauss(length(s.p - float2(0.7, 0)), 0.2) * 0.35;
    float keep = Keep(saturate(x * 0.5 + 0.5) * 0.7 + 0.3, s.rem, 0.1);
    half3 col = s.tint * (head * facet * 0.8 + shaftGlow + fletch * 0.5 + trail + tipGlow)
        + lerp(s.tint, 1, 0.85) * (rim * 1.1 + shaft * 1.1 + tip * 0.9);
    return half4(col * keep * BoardFade(s.p), 1);
}

// Cracks spreading out from the middle (laid onto the floor by the caller, or upright on a body):
// jagged lines (their heading wobbles at two scales) with a white-hot core at least a pixel wide
// and a glow of the tint round them, longer as it goes on, and closing from their tips.
half4 ShapeCrack(ShapeIn s)
{
    float px = max(s.aa, 1e-4);
    float core = 0;
    float halo = 0;
    [unroll]
    for (int i = 0; i < 9; i++)
    {
        float a0 = (i + Hash1(i, s.seed) * 0.6) / 9.0 * Tau;
        float len = (0.45 + 0.5 * Hash1(i + 10, s.seed)) * saturate(s.k * 3.0 + 0.2);
        float a = a0 + (Noise(float3(s.r * 7.0, i * 3.1, s.seed * 11.0)) - 0.5) * 0.5
            + (Noise(float3(s.r * 26.0, i * 5.3, s.seed * 7.0)) - 0.5) * 0.18;
        float2 u = float2(cos(a), sin(a));
        float along = dot(s.p, u);
        float perp = abs(s.p.x * u.y - s.p.y * u.x);
        float t = saturate(along / max(len, 1e-3));
        float on = step(0, along) * (1 - smoothstep(0.85, 1.0, t));
        float w = 0.018 * (1 - t) + 0.004;
        core += on * Line(perp, w * 0.45, px) * (1 - t * 0.6);
        halo += on * Gauss(perp, w * 3.5) * 0.4 * (1 - t * 0.6);
    }
    core += Gauss(s.r, 0.05);
    halo += Gauss(s.r, 0.14) * 0.6;
    float keep = Keep(1 - s.r * 0.9, s.rem, 0.1);
    half3 col = HeatColor(saturate(0.3 + core * 0.7), s.tint) * (core * 1.4 + halo) * keep;
    return half4(col * BoardFade(s.p), 1);
}

// --- A card cracking like glass (ShapeGlassCrack) ----------------------------------------------
// How many times the glass snaps further, and how far out it has cracked after n snaps (0 to 1 of
// the board's radius): nothing before the first, 0.15 after it and 0.95 after the fourth.
static const float GlassSnaps = 4.0;

float GlassReach(float n)
{
    return n <= 0 ? 0 : 0.15 + 0.8 * pow(saturate(n / GlassSnaps), 1.1);
}

// A card cracking like glass round a point, on an upright board: a web of hairline cracks (the
// borders of Voronoi cells, small near the point and larger further out, so they run out from it
// and are joined across) with a white-hot core of one to two and a half pixels and a fine glow of
// the tint, the faces between them not lit. It cracks outward in sudden snaps (ぴき): k counts the
// snaps so far (0 to 4, its whole part), each reaching at once a step further out, and its
// fraction is the time since the last, the cracks that snap just broke flashing white for a
// moment. It is eaten from the outside in as rem falls. Light only (alpha 0).
half4 ShapeGlassCrack(ShapeIn s)
{
    float snaps = min(floor(max(s.k, 0)), GlassSnaps);
    float since = s.k >= GlassSnaps + 1 ? 1 : frac(max(s.k, 0));
    float reach = GlassReach(snaps) * s.rem;
    float before = GlassReach(snaps - 1);
    float2 o = SeedOffset(s.seed);
    float r = max(s.r, 1e-3);
    float2 m = s.dir * pow(r, 0.7) * 2.6 + o;
    float border;
    float2 cell = VoronoiCellBorder(m, border);
    float bw = max(fwidth(border), 1e-4);
    float jag = (Noise(float3(cell, o.x)) - 0.5) * 0.08;
    float rr = r + jag;
    float shown = 1 - smoothstep(reach - 0.03, reach, rr);
    float fresh = shown * smoothstep(before - 0.02, before + 0.02, rr) * (1 - since) * (1 - since);
    // Not every border breaks, so the cracks run out and branch instead of closing into a net.
    float breaks = step(0.3, Hash(float3(cell, o.y + 2.7)));
    float core = (1 - smoothstep(bw * 0.7, bw * 1.8, border)) * shown * breaks;
    float halo = exp(-(border / bw) * (border / bw) / 4.0) * shown * breaks * 0.12;
    float impact = Gauss(s.r, 0.03) * step(0.5, snaps) + Gauss(s.r, 0.08) * 0.3 * step(0.5, snaps);
    half3 col = half3(1, 1, 1) * core * (1.4 + 2.2 * fresh)
        + s.tint * (halo + core * 0.3)
        + lerp(s.tint, 1, 0.6) * impact * s.rem;
    return half4(col * BoardFade(s.p), 0);
}

// A whirlwind seen from the side, its foot at the bottom: a funnel widening upward, made of bands
// that swirl round it (a turned fBM read round the funnel), brighter on the near side, with fine
// thin streaks along the bands whose middles are white, swaying as it goes.
half4 ShapeTornado(ShapeIn s)
{
    float h = saturate((s.p.y + 0.9) / 1.8);
    float2 o = SeedOffset(s.seed);
    float sway = (Fbm4(float2(h * 2.0, s.k * 2.0) + o, o.x) - 0.5) * 0.28 * h;
    float x = s.p.x - sway;
    float w = lerp(0.12, 0.8, pow(h, 0.8));
    float u = x / max(w, 1e-3);
    float inside = 1 - smoothstep(0.85, 1.05, abs(u));
    float phase = asin(clamp(u, -1, 1));
    float bands = Fbm4(float2(phase * 1.6 + h * 8.0 - s.k * 16.0, h * 5.0) + o, o.y);
    float streak = smoothstep(0.48, 0.8, bands);
    float fine = Noise(float3(phase * 5.0 + h * 26.0 - s.k * 34.0, h * 18.0, o.x));
    float thin = smoothstep(0.7, 0.9, fine) * streak;
    float near = 0.55 + 0.45 * cos(phase);
    float edge = Gauss(abs(u) - 0.92, 0.12) * 0.5 * inside;
    float vertical = smoothstep(-0.95, -0.8, s.p.y) * (1 - smoothstep(0.75, 0.98, s.p.y));
    float light = (inside * (streak * near * 1.1 + 0.12) + edge) * vertical;
    float keep = Keep(bands * 0.6 + (1 - h) * 0.4, s.rem, 0.1);
    half3 col = s.tint * light * keep + lerp(s.tint, 1, 0.8) * thin * inside * near * vertical * keep * 0.7;
    return half4(col * BoardFade(s.p), 1);
}

// A bubble (a barrier, or a bubble of poison): a rim with a thin white core and a halo of its own,
// a faint body brighter toward its edge, a swirl of colour over it that curls (warped), a highlight
// at the top left and a small one opposite; it bursts into arcs.
half4 ShapeBubble(ShapeIn s)
{
    float2 o = SeedOffset(s.seed);
    float px = max(s.aa, 1e-4);
    float swirl = Fbm4(Warp(s.p * 1.8 + o, o.x + s.k * 1.5, 0.8), o.y + s.k);
    float rimCore = Line(s.r - 0.82, 0.008, px);
    float rimHalo = Gauss(s.r - 0.82, 0.04);
    float body = (1 - smoothstep(0.7, 0.86, s.r)) * (0.12 + 0.25 * pow(saturate(s.r / 0.82), 3.0));
    float spot = Gauss(length(s.p - float2(-0.35, 0.38)), 0.1) * 1.2
        + Gauss(length(s.p - float2(0.3, -0.42)), 0.06) * 0.3;
    float keep = Keep(Fbm4(s.dir * 2.0 + float2(s.r * 2.0, 0) + o, 3.3) * 0.8 + 0.2, s.rem, 0.06);
    half3 col = s.tint * (rimHalo * (0.6 + 0.5 * swirl) + body * (0.6 + 0.8 * swirl)) * keep
        + lerp(s.tint, 1, 0.8) * rimCore * 1.2 * keep
        + lerp(s.tint, 1, 0.5) * spot * s.rem;
    return half4(col * BoardFade(s.p), 1);
}

// A barrier bubble as a film of light (BarrierFilm): a circle of radius 0.82, its ripple running
// in from its rim to its middle as k goes from 0 to 1. Premultiplied.
half4 ShapeBarrierBubble(ShapeIn s)
{
    float e = s.r / 0.82;
    float inside = 1 - smoothstep(0.98, 1.0, e);
    half4 c = BarrierFilm(s, e, inside, s.p * 8.0, e - (1 - s.k * 1.1), s.p - float2(-0.35, 0.38));
    return c * BoardFade(s.p);
}

// A leaf (or, white and long, a feather of light): a pointed blade along y with a midrib and
// veins, a lit half and a hairline rim at least a pixel wide, a faint grain, curling a little by
// its seed; eaten from its edges.
half4 ShapeLeaf(ShapeIn s)
{
    float2 p = s.p;
    float y01 = p.y * 0.5 + 0.5;
    float w = 0.42 * pow(saturate(sin(Pi * saturate(y01 * 1.05))), 0.8);
    float bend = (y01 - 0.5) * (y01 - 0.5) * 0.8 * (Hash1(1.0, s.seed) - 0.5);
    float x = p.x - bend;
    float px = max(fwidth(x), 1e-4);
    float aa = max(px * 1.5, 0.02);
    float inside = (1 - smoothstep(w - aa, w + aa, abs(x))) * step(-0.95, p.y) * step(p.y, 0.95);
    float rib = Line(x, 0.012, px) * inside;
    float veins = Gauss(frac(y01 * 6.0 - abs(x) * 3.0) - 0.5, 0.08) * inside * 0.3;
    float lit = x < 0 ? 1.0 : 0.6;
    float rim = Line(abs(x) - w, 0.012, px) * inside;
    float grain = Noise(float3(p * 7.0 + SeedOffset(s.seed), 2.1));
    float keep = Keep(1 - abs(x) / max(w, 1e-3) * 0.7, s.rem, 0.1);
    float light = (inside * lit * (0.5 + 0.2 * grain) + rib * 0.6 + veins + rim * 0.5) * keep;
    return half4(s.tint * light + (rib * 0.4 + rim * 0.2) * keep, 1);
}

// Arrows pointing up (a power rising; turned over, a power falling): two chevrons sliding up the
// board as it goes on, each a white core at least a pixel wide in a glow of the tint, fading in at
// the bottom and out at the top.
half4 ShapeChevron(ShapeIn s)
{
    float px = max(s.aa, 1e-4);
    float core = 0;
    float halo = 0;
    [unroll]
    for (int i = 0; i < 2; i++)
    {
        float y0 = frac(s.k * 1.2 + i * 0.5) * 1.4 - 0.7;
        float2 q = float2(abs(s.p.x), s.p.y - y0);
        float d = abs(q.y + q.x * 0.75) * 0.8;
        float on = (1 - smoothstep(0.5, 0.58, q.x)) * (1 - q.x * 1.2);
        float fade = sin(saturate((y0 + 0.7) / 1.4) * Pi);
        core += on * Line(d, 0.025, px) * fade;
        halo += on * (Gauss(d, 0.07) * 0.9 + Gauss(d, 0.16) * 0.3) * fade;
    }
    half3 col = (s.tint * halo + lerp(s.tint, 1, 0.75) * core * 1.1) * s.rem;
    return half4(col * BoardFade(s.p), 1);
}

// A snowflake crystal: six arms with side branches and a hexagonal heart, each a thin white core
// at least a pixel wide in a glow of the tint. It grows out from the middle and breaks off from the
// arms' tips.
half4 ShapeSnowflake(ShapeIn s)
{
    float a = atan2(s.p.y, s.p.x) + s.seed * Tau;
    const float sector = Tau / 6.0;
    float local = (frac(a / sector + 0.5) - 0.5) * sector;
    float2 q = float2(cos(local), abs(sin(local))) * s.r;
    float px = max(s.aa, 1e-4);
    float grow = saturate(s.k * 3.0 + 0.25);
    float len = 0.88 * grow;
    float armOn = 1 - smoothstep(len - 0.02, len, q.x);
    float core = Line(q.y, 0.01, px) * armOn;
    float halo = Gauss(q.y, 0.04) * armOn;
    [unroll]
    for (int i = 1; i <= 3; i++)
    {
        float bx = len * (0.28 + 0.2 * i);
        float bl = 0.22 * (1.2 - 0.3 * i) * grow;
        float2 b = q - float2(bx, 0);
        float along = b.x * 0.5 + b.y * 0.8660254;
        float perp = abs(b.x * 0.8660254 - b.y * 0.5);
        float on = step(0, along) * (1 - smoothstep(bl - 0.02, bl, along));
        core += Line(perp, 0.008, px) * on;
        halo += Gauss(perp, 0.032) * on * 0.8;
    }
    core += Line(abs(s.r - 0.16 * grow), 0.01, px) + Gauss(s.r, 0.05);
    halo += Gauss(abs(s.r - 0.16 * grow), 0.04) + Gauss(s.r, 0.5) * 0.12;
    float keep = Keep(1 - s.r, s.rem, 0.08);
    half3 col = (s.tint * halo + lerp(s.tint, 1, 0.8) * core * 1.1) * keep;
    return half4(col * BoardFade(s.p), 1);
}

// A crest shaped like a shield, standing upright: a rim with a thin white core and a halo of its
// own, a faint face brighter toward its edges, a cross on it, and a band of light sweeping over it.
half4 ShapeCrest(ShapeIn s)
{
    float2 p = s.p;
    float t = saturate((0.75 - p.y) / 1.6);
    float w = 0.7 * sqrt(saturate(1 - t * t));
    float edge = max(abs(p.x) - w, p.y - 0.75);
    edge = p.y < -0.85 ? max(edge, -0.85 - p.y) : edge;
    float px = max(fwidth(edge), 1e-4);
    float aa = max(px * 1.5, 0.015);
    float face = 1 - smoothstep(-aa, aa, edge);
    float rimCore = Line(edge, 0.008, px);
    float rimHalo = Gauss(edge, 0.04);
    float crossD = min(abs(p.x) + step(0.55, p.y) + step(p.y, -0.6), abs(p.y - 0.2) + step(0.38, abs(p.x)));
    float crossLine = (Line(crossD, 0.012, px) * 0.8 + Gauss(crossD, 0.04) * 0.4) * face;
    float sweep = Gauss(p.x * 0.8 + p.y * 0.6 - (s.k * 3.0 - 1.2), 0.15) * face;
    float fres = pow(saturate((edge + 0.4) / 0.4), 2.0) * face * 0.4;
    float keep = Keep(Fbm4(p * 3.0 + SeedOffset(s.seed), 1.1) * 0.6 + 0.4 * (1 - abs(p.y)), s.rem, 0.08);
    half3 col = (s.tint * (rimHalo * 0.9 + face * 0.12 + fres + crossLine * 0.7 + sweep * 0.5)
        + lerp(s.tint, 1, 0.8) * (rimCore * 1.2 + crossLine * 0.3)) * keep;
    return half4(col * BoardFade(p), 1);
}

// A sight locking on: two rings broken at the four ticks pointing in, closing in on the middle as
// it goes on, each line a white core at least a pixel wide in a glow of the tint, with a hot dot at
// the centre.
half4 ShapeReticle(ShapeIn s)
{
    float close = lerp(1.0, 0.72, saturate(s.k * 2.5));
    float px = max(s.aa, 1e-4);
    float around = atan2(s.p.y, s.p.x);
    float gaps = step(0.12, abs(frac(around / (Pi * 0.5) + 0.5) - 0.5));
    float2 a = abs(s.p);
    float far = max(a.x, a.y);
    float onTicks = step(0.5 * close, far) * step(far, 0.98 * close);
    float2 outer = GlowStroke(s.r - 0.8 * close, 0.014, px) * gaps;
    float2 inner = GlowStroke(s.r - 0.42 * close, 0.011, px);
    float2 ticks = GlowStroke(min(a.x, a.y), 0.014, px) * onTicks;
    float2 lines = outer * 1.1 + inner * 0.7 + ticks;
    float centre = Gauss(s.r, 0.04) * 1.5;
    half3 col = (s.tint * lines.x + lerp(s.tint, 1, 0.75) * (lines.y + centre)) * s.rem;
    return half4(col * BoardFade(s.p), 1);
}

// --- Screen -----------------------------------------------------------------------------------

// The cut-in's streaks: rows of dashes along x, read straight from the UV so the image's
// uvRect scrolls them and repeats them.
half4 ShapeCutInStreaks(ShapeIn s)
{
    const float rows = 22.0;
    float row = floor(s.uv.y * rows);
    float fy = frac(s.uv.y * rows) - 0.5;
    float h = Hash1(row, 9.1);
    float stripe = Gauss(fy, lerp(0.06, 0.22, Hash1(row, 2.3)));
    float dash = Noise(float3(s.uv.x * lerp(1.5, 4.0, h) + h * 40.0, row, 0.5));
    float on = smoothstep(0.45, 0.7, dash) * step(0.35, h);
    float light = stripe * on * lerp(0.4, 1.0, Hash1(row, 7.7));
    return half4(s.tint * light + stripe * on * 0.3, 1);
}

#endif
