// 戦闘のエフェクトの形を、画像を使わずに計算で描く関数（BattleVfxShape.shaderから読み込む）。
// 形は四角い板の中の位置から、距離・向き・ノイズで明るさと透明度を決める。
// 加算の形は (光の色, 1) を、通常合成の形は (色, 覆う割合) を返す。
// 1枚ずつの値は ShapeIn で受け取る。
// - seed：乱数の種（0〜1）。同じ形でも1枚ずつ模様を変える。
// - k：進み具合（0〜1）。形の動き（広がる、流れる、描き込まれる）に使う。
// - rem：残り（1で欠けていない、0で消えた）。削れ・途切れ・崩れに使う。
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

// --- Light shapes (additive) -----------------------------------------------------------------

// A soft round light, a little hotter in the middle (glows, pools of light, the hot flash).
half4 ShapeGlow(ShapeIn s)
{
    float light = exp(-s.r * s.r * 4.0) * (1 - smoothstep(0.7, 1.0, s.r));
    light += exp(-s.r * s.r * 30.0) * 0.35;
    return half4(s.tint * light, 1);
}

// A ring of shock: a hot line at a sharp front, a soft halo and a faint trail inside, streaks
// along it and sparks around it. It thins as it spreads and breaks into arcs from halfway.
half4 ShapeRing(ShapeIn s)
{
    float life = s.rem;
    float t = 1 - life;
    float front = 0.74 + (Noise(float3(s.dir * 1.7, 3.1 + t * 1.5)) - 0.5) * 0.05;
    float d = s.r - front;

    float thin = lerp(1, 0.45, t);
    float outer = 1 - smoothstep(0, max(0.016, s.aa), d - 0.016);
    float haloWidth = (d < 0 ? 0.04 : 0.025) * thin;
    float halo = Gauss(d, haloWidth) * 0.45;
    float trail = exp(min(d, 0) / (0.1 * thin)) * 0.15;
    float streak = Noise(float3(s.dir * 3, s.r * 38 + t * 3));
    float band = outer * (halo + trail) * (0.3 + 1.26 * smoothstep(0.35, 0.85, streak));

    float coreWidth = max(0.01 * thin, s.aa * 1.2);
    float hot = 0.45 + 0.8 * Noise(float3(s.dir * 3.3, 5.9 + t * 2));
    float core = Gauss(d, coreWidth) * 1.4 * hot;

    float arcs = Noise(float3(s.dir * 2, 7.7 + s.r * 4)) * 0.7
        + Noise(float3(s.dir * 4.5, 1.3 + s.r * 8)) * 0.3;
    float keep = smoothstep(
        saturate((t - 0.3) / 0.7) * 0.75 - 0.12,
        saturate((t - 0.3) / 0.7) * 0.75 + 0.12,
        arcs);

    float around = atan2(s.p.y, s.p.x) / Tau + 0.5;
    float slice = floor(around * 64);
    float h1 = Hash(float3(slice, 1.3, 0.7));
    float h2 = Hash(float3(slice, 7.1, 2.9));
    float h3 = Hash(float3(slice, 4.2, 5.3));
    float sparkR = front + (h3 - 0.65) * 0.2 + t * (0.04 + 0.12 * h2);
    float da = (around - (slice + 0.35 + 0.3 * h2) / 64) * Tau * s.r;
    float dr = s.r - sparkR;
    float sparkSize = max(0.005 + 0.005 * h3, s.aa);
    float spark = step(0.36, h1) * exp(-(da * da + dr * dr) / (sparkSize * sparkSize))
        * (0.6 + 0.4 * sin(t * 24 + h1 * 20)) * saturate(1.2 - t * 1.2);

    half3 light = s.tint * band * keep
        + lerp(s.tint, 1, 0.5) * core * keep
        + lerp(s.tint, 1, 0.6) * spark * 2.5;
    return half4(light * saturate(life * 1.25), 1);
}

// The flash of a blow: eight rays of different lengths around a hot centre. The rays shorten
// and thin as it is eaten away.
half4 ShapeStar(ShapeIn s)
{
    float rot = s.seed * Tau;
    float light = 0;
    [unroll]
    for (int i = 0; i < 8; i++)
    {
        float main = (i % 2 == 0) ? 1.0 : 0.6;
        float a = rot + i * 0.7853982 + (Hash1(i, s.seed) - 0.5) * 0.3;
        float2 u = float2(cos(a), sin(a));
        float along = dot(s.p, u);
        float perp = abs(s.p.x * u.y - s.p.y * u.x);
        float len = (main > 0.9 ? 0.95 : 0.6) * (0.75 + 0.25 * Hash1(i + 8, s.seed))
            * (0.35 + 0.65 * s.rem);
        float t = saturate(along / len);
        float width = max((0.05 * (1 - t) + 0.004) * (0.4 + 0.6 * s.rem), s.aa);
        light += step(0, along) * (1 - t) * Gauss(perp, width) * main;
    }
    light += Gauss(s.r, 0.1) * 1.2 + Gauss(s.r, 0.35) * 0.3 * s.rem;
    light *= 1 - smoothstep(0.9, 1.0, s.r);
    half3 col = s.tint * light + (1 - s.tint) * saturate(light - 0.7) * 0.6;
    return half4(col, 1);
}

// A sweeping sword arc: a crescent with a hot outer edge and a blurred trail inside, drawn in
// from one tip to the other as the blade passes, then eaten away from its thin tips.
half4 ShapeSlashArc(ShapeIn s)
{
    float2 q = s.p - float2(0, -0.35);
    float rr = length(q);
    float ang = atan2(q.y, q.x);
    const float a0 = 0.3;
    const float a1 = 2.84;
    float t = saturate((ang - a0) / (a1 - a0));
    float inArc = step(a0, ang) * step(ang, a1);
    float shape = pow(saturate(sin(Pi * t)), 0.8);
    float thick = 0.2 * shape;

    float d = rr - 0.88;
    float outer = 1 - smoothstep(0, max(0.012, s.aa), d);
    float body = outer * exp(min(d, 0) / max(thick, 1e-3)) * inArc * step(0.001, thick);
    float streak = Noise(float3(q / max(rr, 1e-3) * 3.0, rr * 24.0 + s.seed * 10.0));
    body *= 0.65 + 0.6 * smoothstep(0.3, 0.8, streak);
    float core = Gauss(d + 0.01, max(0.006 + 0.03 * shape, s.aa)) * inArc
        * smoothstep(0, 0.25, shape);
    float halo = Gauss(d, 0.05) * shape * inArc * 0.3;

    float head = s.k * 3.5;
    float drawn = 1 - smoothstep(head - 0.15, head + 0.05, t);
    float keep = Keep(shape * 0.8 + streak * 0.2, s.rem, 0.08);
    float light = (body * 1.1 + core * 2.0 + halo) * drawn * keep;
    half3 col = s.tint * light + saturate(core * drawn * keep) * 0.8;
    return half4(col * BoardFade(s.p), 1);
}

// The finishing cross cut: two thin blades of light crossing, shooting out to full length,
// over a hot centre; eaten away from their ends.
half4 ShapeSlashCross(ShapeIn s)
{
    float light = 0;
    float hot = 0;
    float n = Noise(float3(s.p * 6.0, s.seed * 9.0));
    [unroll]
    for (int i = 0; i < 2; i++)
    {
        float a = (i == 0 ? 0.785398 : 2.356194) + (s.seed - 0.5) * 0.3 * (i == 0 ? 1 : -1);
        float2 u = float2(cos(a), sin(a));
        float along = dot(s.p, u);
        float perp = s.p.x * u.y - s.p.y * u.x;
        float len = 0.95 * (0.55 + 0.45 * saturate(s.k * 4.0));
        float t = saturate(abs(along) / len);
        float w = 0.09 * (1 - t * t);
        float keep = Keep((1 - t) * 0.85 + n * 0.15, s.rem, 0.1);
        light += Gauss(perp, max(w * 0.55, s.aa)) * (1 - t * t * t * t) * keep;
        hot += Gauss(perp, max(w * 0.15, s.aa)) * (1 - t * t) * keep;
    }
    light += Gauss(s.r, 0.18) * 0.8 * s.rem;
    half3 col = s.tint * (light * 0.8) + hot * 1.3;
    return half4(col * BoardFade(s.p), 1);
}

// A fireball flying along +x: a white-hot head and a turbulent tail streaming back, cooling
// from white to yellow, the tint and red along it.
half4 ShapeFireball(ShapeIn s)
{
    // A teardrop: a round head at x 0.55 tapering back to a point at the tail.
    const float headX = 0.55;
    float back = headX - s.p.x;
    float along = saturate(back / 1.45);
    float flow = Fbm(float3(s.p.x * 3.0 + s.k * 9.0, s.p.y * 4.0, s.seed * 7.0));
    float y = s.p.y + (flow - 0.5) * 0.18 * along;
    float d = UnevenCapsuleDistance(float2(y, back), 0.27, 0.02, 1.4);
    d += (flow - 0.5) * (0.06 + 0.14 * along);
    float body = smoothstep(0.03, -0.1, d) * (0.6 + 0.6 * flow);
    float heat = saturate(smoothstep(0.0, -0.28, d) * (1 - along * 0.8) + 0.15);
    float head = Gauss(length(s.p - float2(headX, 0)), 0.17);
    half3 col = HeatColor(saturate(heat + head * 0.5), s.tint) * (body + head * 0.8);
    col += s.tint * Gauss(max(d, 0), 0.12) * 0.35 * (1 - along);
    return half4(col * BoardFade(s.p), 1);
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

// An ice lance flying along +x: a long shaft swelling into a faceted head, a bright ridge and
// rims, and a cold glow around the head.
half4 ShapeIceSpear(ShapeIn s)
{
    float x = s.p.x;
    float y = s.p.y;
    float w = IceSpearWidth(x);
    float aa = max(s.aa, 0.004);
    float inside = 1 - smoothstep(w - aa, w + aa, abs(y));
    float facet = y > 0 ? 0.85 : 0.4;
    float shaft = step(-0.9, x) * step(x, IceSpearTip);
    float ridge = Gauss(y, max(0.008, aa)) * shaft;
    float rim = Gauss(abs(y) - w, max(0.01, aa)) * shaft * step(0.001, w);
    float glint = smoothstep(0.6, 0.9, Noise(float3(x * 14.0, y * 30.0, s.seed * 5.0)));
    float light = inside * (facet * 0.6 + 0.3 * glint) + ridge * 1.2 + rim * 0.9;
    light += Gauss(length(s.p - float2(0.68, 0)), 0.3) * 0.25;
    light += Gauss(y, 0.08) * smoothstep(0.4, -0.9, x) * 0.25 * step(-1.0, x);
    half3 col = lerp(s.tint, 1, saturate(ridge + rim * 0.6 + facet * inside * 0.3)) * light;
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

// A bolt of lightning from the top of the board down to its foot (at 9% of the height), with two
// thinner branches; a new seed strikes a new shape. It breaks up into segments as it goes.
half4 ShapeLightning(ShapeIn s)
{
    // Square units: the board is 0.62 times as wide as it is tall.
    float2 q = float2(s.p.x * 0.62, s.p.y);
    const float foot = -0.82;
    const float segment = 0.114;
    float f = max(0, (q.y - foot) / segment);
    float x0 = BoltX(f, s.seed);
    float x1 = BoltX(f + 0.05, s.seed);
    float slope = (x1 - x0) / (0.05 * segment);
    float d = abs(q.x - x0) / sqrt(1 + slope * slope);
    d = q.y < foot ? length(q - float2(0, foot)) : d;
    float aa = max(s.aa * 0.62, 0.002);
    float core = Gauss(d, max(0.01, aa));
    float light = core * 1.6 + Gauss(d, 0.05) * 0.5 + Gauss(d, 0.15) * 0.15;
    float keep = Keep(Hash1(floor(f), s.seed + 0.37) * 0.7 + 0.3, s.rem, 0.1);
    light *= keep;

    [unroll]
    for (int b = 0; b < 2; b++)
    {
        float start = 5 + floor(Hash1(b + 20, s.seed) * 7);
        float side = Hash1(b + 30, s.seed) < 0.5 ? -1 : 1;
        float y0 = foot + start * segment;
        float len = 0.25 + 0.25 * Hash1(b + 40, s.seed);
        float down = y0 - q.y;
        float xb = BoltX(start, s.seed) + side * down * 0.55
            + (BoltX(down / (segment * 0.5) + 50 + b * 9, s.seed) * 0.5);
        float db = abs(q.x - xb) * 0.85;
        float on = step(0, down) * (1 - smoothstep(len * 0.6, len, down));
        float branchCore = Gauss(db, max(0.006, aa)) * on;
        core += branchCore * 0.6;
        light += (branchCore * 0.9 + Gauss(db, 0.035) * 0.3 * on) * keep;
    }
    light += Gauss(length(q - float2(0, foot)), 0.1) * 0.8 * s.rem;
    half3 col = s.tint * light + saturate(core * keep) * 0.8;
    return half4(col * BoardFade(s.p), 1);
}

// A pillar of light rising from the feet (at 10% of the height): a soft column of rising
// streaks, a bright middle, motes drifting up inside and a glow on the floor at its foot.
half4 ShapePillar(ShapeIn s)
{
    float x = s.p.x;
    float y = s.p.y;
    float h = saturate((y + 0.8) / 1.75);
    float w = lerp(0.42, 0.32, h);
    float rays = Noise(float3(x * 7.0, y * 1.2 - s.k * 3.0, s.seed * 5.0));
    float column = Gauss(x, w * 0.7) * (0.55 + 0.9 * smoothstep(0.3, 0.85, rays))
        + Gauss(x, w * 1.4) * 0.25;
    float core = Gauss(x, 0.06) * 0.8;
    float vertical = smoothstep(-0.85, -0.7, y) * (1 - smoothstep(0.55, 0.98, h + (rays - 0.5) * 0.2));

    float2 cell = float2(x * 6.0, y * 4.0 - s.k * 6.0);
    float2 ci = floor(cell);
    float2 cf = frac(cell) - 0.5;
    float2 off = float2(Hash(float3(ci, 3.1)), Hash(float3(ci, 5.7))) - 0.5;
    float mote = step(0.7, Hash(float3(ci, s.seed * 13.0))) * Gauss(length(cf - off * 0.5), 0.08)
        * Gauss(x, 0.3) * vertical;

    float base = Gauss(length(float2(x, (y + 0.8) * 3.5)), 0.35) * 0.6;
    float keep = Keep((1 - h) * 0.6 + rays * 0.4, s.rem, 0.1);
    float light = ((column + core) * vertical + mote * 1.5) * keep + base * s.rem;
    half3 col = s.tint * light + saturate(core * vertical * keep) * 0.5 + mote * keep * 0.6;
    return half4(col * BoardFade(s.p), 1);
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

// A dome of light over the party, its base on the floor (at 16% of the height): a bright rim,
// a face brighter toward its edges, hexagons, a shimmer sweeping across and a ring on the
// floor. It breaks by dropping its hexagons.
half4 ShapeDome(ShapeIn s)
{
    float2 q = float2(s.p.x / 0.92, (s.p.y + 0.68) / 1.6);
    float e = length(q);
    float above = smoothstep(-0.02, 0.02, q.y);
    float rim = Gauss(e - 1, max(0.015, s.aa * 2)) * above;
    float face = (1 - smoothstep(0.97, 1.0, e)) * above;
    float fresnel = pow(saturate(e), 4) * 0.5 + 0.08;

    float4 hex = HexCell(s.p * 7.0 + float2(0, s.seed * 3.0));
    float2 g = abs(hex.zw);
    float edge = 0.5 - max(dot(g, float2(0.5, 0.8660254)), g.x);
    float hexLine = 1 - smoothstep(0.02, 0.07, edge);
    float cell = Hash(float3(hex.xy, s.seed * 7.0));
    float shimmer = Gauss(q.x * 1.2 + q.y * 0.8 - (s.k * 3.0 - 1.0), 0.18);
    float light = face * (fresnel + hexLine * 0.35 + shimmer * 0.4) + rim * 1.2;

    float2 b = float2(s.p.x / 0.92, (s.p.y + 0.68) / 0.13);
    float baseRing = Gauss(length(b) - 1, 0.25) * 0.6 * (1 - above * 0.5);
    float keep = Keep(cell * 0.85 + 0.1, s.rem, 0.05);
    light = light * keep + baseRing * s.rem;
    half3 col = s.tint * light + rim * 0.4 * keep;
    return half4(col * BoardFade(s.p), 1);
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
// hexagram with small circles at its points and an inner ring. It is drawn in around the circle
// at first and eaten away along it at the end.
half4 ShapeMagicCircle(ShapeIn s)
{
    float r = s.r;
    float2 p = s.p;
    float w = max(0.011, s.aa * 1.2);
    float around = atan2(p.y, p.x) / Tau + 0.5;
    float lines = Gauss(r - 0.93, w) + Gauss(r - 0.86, w * 0.8) + Gauss(r - 0.6, w)
        + Gauss(r - 0.18, w * 0.8);

    float tickDist = abs(frac(around * 48 + 0.5) - 0.5) / 48 * Tau * r;
    lines += Gauss(tickDist, w) * step(0.865, r) * step(r, 0.925);

    float cu = around * 24;
    float ci = floor(cu);
    float2 g = float2((frac(cu) - 0.5) * 2.0, (r - 0.73) / 0.07);
    float glyphHash = Hash1(ci, 4.2);
    float gw = max(0.13, s.aa / 0.09);
    float glyph = step(0.3, glyphHash) * Gauss(g.x, gw) * step(abs(g.y), 0.8);
    glyph += step(0.55, frac(glyphHash * 7.3)) * Gauss(g.y - (frac(glyphHash * 3.1) - 0.5), gw)
        * step(abs(g.x), 0.6);
    glyph += step(0.6, frac(glyphHash * 5.7)) * Gauss(g.x - g.y * 0.6, gw) * step(length(g), 0.85);
    lines += glyph * step(abs(g.y), 1.0) * step(abs(g.x), 0.75);

    lines += Gauss(abs(TriangleDistance(p, 0.52)), w) + Gauss(abs(TriangleDistance(-p, 0.52)), w);
    [unroll]
    for (int i = 0; i < 6; i++)
    {
        float a = i * 1.0471976 + 1.5707963;
        float2 c = float2(cos(a), sin(a)) * 0.6;
        lines += Gauss(length(p - c) - 0.05, w * 0.8);
    }

    float glow = Gauss(r - 0.6, 0.18) * 0.12 + Gauss(r - 0.9, 0.08) * 0.15;
    float drawn = 1 - smoothstep(s.k * 4.0 - 0.05, s.k * 4.0, around);
    float keep = Keep(Noise(float3(s.dir * 2.3, r * 4.0)) * 0.7 + 0.3 * (1 - r), s.rem, 0.1);
    float light = (min(lines, 1.5) + glow) * drawn * keep;
    half3 col = s.tint * light + saturate(lines - 0.8) * 0.3 * drawn * keep;
    return half4(col * BoardFade(s.p), 1);
}

// A streak of a spark flying along +y: a narrow tail and a hot head.
half4 ShapeStreak(ShapeIn s)
{
    float along = saturate(s.p.y * 0.5 + 0.5);
    float w = lerp(0.15, 0.55, along);
    float body = Gauss(s.p.x, w) * smoothstep(0.0, 0.35, along) * (1 - smoothstep(0.85, 1.0, along));
    float core = Gauss(s.p.x, w * 0.35) * along * (1 - smoothstep(0.9, 1.0, along));
    return half4(s.tint * body + core * 0.9, 1);
}

// A round spark: a soft dot with a hot middle.
half4 ShapeSpark(ShapeIn s)
{
    float light = Gauss(s.r, 0.45) * (1 - smoothstep(0.8, 1.0, s.r));
    float core = Gauss(s.r, 0.15);
    return half4(s.tint * (light + core) + core * 0.6, 1);
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

// A twinkle: four long rays, four short ones between and a soft middle.
half4 ShapeSparkle(ShapeIn s)
{
    float2 p = s.p;
    float rays = Gauss(p.x, 0.06) * pow(saturate(1 - abs(p.y)), 2)
        + Gauss(p.y, 0.06) * pow(saturate(1 - abs(p.x)), 2);
    float2 q = Rotate(p, 0.785398);
    rays += 0.35 * (Gauss(q.x, 0.05) * pow(saturate(1 - abs(q.y) * 1.6), 2)
        + Gauss(q.y, 0.05) * pow(saturate(1 - abs(q.x) * 1.6), 2));
    float core = Gauss(s.r, 0.16);
    return half4(s.tint * (rays + core * 0.8) + core * 0.5, 1);
}

// A thin shard of light: a long diamond with a lit facet and bright rims; it shrinks as it goes.
half4 ShapeShard(ShapeIn s)
{
    float d = abs(s.p.x) / 0.32 + abs(s.p.y) / 0.95;
    float aa = max(s.aa * 3.0, 0.04);
    float body = 1 - smoothstep(1 - aa, 1 + aa, d);
    float facet = s.p.x > 0 ? 0.7 : 0.4;
    float rim = Gauss(d - 0.92, 0.08) * body;
    float keep = Keep(1 - d * 0.8, s.rem, 0.08);
    float light = (body * facet + rim * 0.8 + Gauss(s.p.x, 0.04) * body * 0.5) * keep;
    return half4(s.tint * light + rim * keep * 0.3, 1);
}

// A torn lump of flame: a round bottom licked up into tongues, the pattern rising through it,
// hotter inside. It is eaten away from its thin licks.
half4 ShapeFlame(ShapeIn s)
{
    // Licks stretched upward: the noise is finer across than along, and rises through it.
    float n = Fbm(float3(s.p.x * 2.4 + s.seed * 17.0, s.p.y * 1.3 - s.k * 2.5, s.seed * 5.0 + s.k));
    float2 q = float2(s.p.x, s.p.y * 0.8 + 0.1);
    // Kept well inside the board, so its square edge never cuts it.
    float field = 0.95 - length(q) * 1.55 + (n - 0.5) * 1.0 + max(q.y, 0) * (n - 0.45) * 0.9;
    float shape = smoothstep(0.03, 0.25, field);
    float heat = smoothstep(0.15, 0.85, field);
    float keep = Keep(shape * 0.5 + n * 0.5, s.rem, 0.08);
    half3 col = HeatColor(heat * 0.85 + 0.1, s.tint) * shape * (0.55 + 0.6 * n) * keep;
    return half4(col * BoardFade(s.p), 1);
}

// A tongue of flame rising from its root at the bottom, swaying and flickering, burning away
// from the tip down.
half4 ShapeFlameTongue(ShapeIn s)
{
    float y01 = s.p.y * 0.5 + 0.5;
    float sway = (Noise(float3(y01 * 2.5 - s.k * 3.0, s.seed * 9.0, 0.5)) - 0.5) * 0.55 * y01;
    float x = s.p.x - sway;
    float w = 0.46 * pow(saturate(1 - y01), 0.9) * smoothstep(0.0, 0.2, y01);
    float n = Fbm(float3(x * 5.0, y01 * 4.0 - s.k * 6.0, s.seed * 3.0));
    float across = abs(x) + (n - 0.5) * 0.3 * (0.4 + y01);
    float shape = 1 - smoothstep(w * 0.4, w + 0.03, across);
    float heat = saturate((1 - across / max(w, 1e-3)) * (1.1 - y01));
    float keep = Keep((1 - y01) * 0.6 + n * 0.4, s.rem, 0.08);
    half3 col = HeatColor(heat * 0.9 + 0.1, s.tint) * shape * (0.6 + 0.6 * n) * keep;
    return half4(col * BoardFade(s.p), 1);
}

// --- Solid shapes (blended normally) ---------------------------------------------------------

// A billow of smoke, mist or dust: a ragged soft blob, lit from above, eaten into holes.
half4 ShapeSmoke(ShapeIn s)
{
    float n = Fbm(float3(s.p * 1.7 + s.seed * 23.0, s.k * 0.6 + s.seed * 3.0));
    float n2 = Noise(float3(s.p * 4.3 + s.seed * 7.0, s.k));
    float edge = s.r + (n - 0.5) * 0.7;
    float density = (1 - smoothstep(0.35, 0.85, edge)) * (0.65 + 0.35 * n2);
    float keep = Keep(density * 0.6 + n * 0.4, s.rem, 0.1);
    float shade = 0.8 + 0.15 * s.p.y - 0.12 * s.p.x + 0.15 * (n2 - 0.5);
    return half4(s.tint * shade, density * keep);
}

// A burnt patch on the floor: a ragged dark blot with specks around its edge.
half4 ShapeScorch(ShapeIn s)
{
    float n = Fbm(float3(s.p * 2.2 + s.seed * 31.0, 1.7));
    float edge = s.r + (n - 0.5) * 0.6;
    float density = (1 - smoothstep(0.35, 0.9, edge)) * (0.7 + 0.5 * n);
    float speck = step(0.78, Noise(float3(s.p * 18.0 + s.seed * 5.0, 2.3)))
        * (1 - smoothstep(0.6, 1.0, edge)) * 0.5;
    density = saturate(density + speck);
    float keep = Keep(density * 0.6 + n * 0.4, s.rem, 0.1);
    return half4(s.tint * (0.8 + 0.4 * n), density * keep);
}

// Frost on the floor seen low from the side: a pale patch of fine crystals radiating out from the
// middle, with glints that catch the light.
half4 ShapeFrost(ShapeIn s)
{
    float n = Fbm(float3(s.p * float2(3.0, 1.2) + s.seed * 13.0, 0.5));
    float cover = 1 - smoothstep(0.4, 0.9, s.r + (n - 0.5) * 0.45);
    float ang = atan2(s.p.y * 3.0, s.p.x);
    float rays = Noise(float3(cos(ang) * 9.0, sin(ang) * 9.0, s.r * 3.0 + s.seed * 4.0));
    float crystal = smoothstep(0.55, 0.8, rays) * cover;
    float glint = step(0.9, Noise(float3(s.p * float2(30.0, 10.0) + s.seed * 11.0, 4.4))) * cover;
    float density = cover * 0.45 + crystal * 0.4 + glint * 0.6;
    float keep = Keep(cover * 0.5 + n * 0.5, s.rem, 0.1);
    return half4(s.tint * (0.9 + crystal * 0.3) + glint * 1.2, saturate(density) * keep);
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
// one, bright rims, faint streaks; it crumbles in chunks from the tip down.
half4 ShapeIceSpike(ShapeIn s)
{
    SpikeShape g = SpikeAt(s);
    half3 face = lerp(half3(0.42, 0.62, 0.88), half3(0.85, 0.95, 1.0), g.lit);
    // Clearer toward the root, frosted toward the tip.
    face *= 0.8 + 0.3 * Noise(float3(s.p.x * 9.0 + s.seed * 7.0, s.p.y * 1.4, s.seed * 3.0)) + 0.15 * g.t;
    float rim = Gauss(max(g.dEdge, 0), 0.04) * g.inside;
    float splitLine = Gauss(s.p.x - g.split, 0.012) * g.inside * 0.7;
    // Faint cracks inside, catching the light.
    float crack = Gauss(frac((s.p.x * 1.3 + s.p.y * 0.9 + s.seed * 5.0) * 2.0) - 0.5, 0.03)
        * g.inside * 0.35 * step(0.4, Hash1(floor((s.p.x * 1.3 + s.p.y * 0.9) * 2.0), s.seed));
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

// A broken chip (of ice, or of dark debris): a skewed diamond with a lit half and a bright rim.
half4 ShapeChip(ShapeIn s)
{
    float2 q;
    float d = ChipDistance(s, q);
    float aa = max(s.aa * 2.0, 0.03);
    float inside = 1 - smoothstep(1 - aa, 1 + aa, d);
    float lit = q.x + q.y > 0 ? 1.0 : 0.55;
    float rim = Gauss(d - 0.92, 0.08) * inside;
    float keep = Keep(1 - d * 0.7, s.rem, 0.08);
    return half4(s.tint * (lit + rim * 0.6), inside * keep);
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

// A beam along +x across the board: a hot core and a soft sheath that ripple as the energy flows
// down it, rings of light racing along, drawn in from the left end and eaten away from it.
half4 ShapeBeam(ShapeIn s)
{
    float x01 = s.p.x * 0.5 + 0.5;
    float flow = Noise(float3(s.p.x * 6.0 - s.k * 14.0, s.seed * 7.0, 0.3));
    float w = 0.16 * (0.75 + 0.5 * flow) * smoothstep(0.0, 0.08, x01) * (1 - smoothstep(0.92, 1.0, x01));
    float y = abs(s.p.y);
    float sheath = Gauss(y, max(w, 1e-3)) * (0.5 + 0.5 * flow);
    float core = Gauss(y, max(w * 0.22, s.aa));
    float rings = Gauss(frac(s.p.x * 4.0 - s.k * 6.0) - 0.5, 0.06) * Gauss(y, w * 1.3 + 1e-3) * 0.4;
    float drawn = 1 - smoothstep(s.k * 4.0 - 0.1, s.k * 4.0, x01);
    float keep = Keep(x01 * 0.7 + flow * 0.3, s.rem, 0.08);
    float light = (sheath * 0.9 + core * 1.6 + rings) * drawn * keep;
    half3 col = s.tint * light + saturate(core * drawn * keep) * 0.7;
    return half4(col * BoardFade(float2(s.p.x * 0.98, s.p.y)), 1);
}

// An arrow (or a needle) of light flying along +x: a sharp head at 82% of the width, a thin shaft
// back to swept vanes at the tail, and a glow streaming behind it.
half4 ShapeArrow(ShapeIn s)
{
    float x = s.p.x;
    float y = s.p.y;
    float aa = max(s.aa, 0.004);
    float inHead = step(0.5, x) * step(x, 0.82);
    float headW = 0.12 * saturate((0.82 - x) / 0.32);
    float head = (1 - smoothstep(headW - aa, headW + aa, abs(y))) * inHead;
    float shaft = Gauss(y, max(0.013, aa)) * step(-0.62, x) * step(x, 0.55);
    float fx = x + 0.62;
    float vane = (0.2 - fx) * 0.5;
    float fletch = step(0, fx) * step(fx, 0.2) * (1 - smoothstep(vane - aa, vane + aa, abs(y)));
    float trail = Gauss(y, 0.04 + 0.08 * saturate(-x)) * smoothstep(0.5, -1.0, x) * step(-1.0, x) * 0.5;
    float ridge = Gauss(y - 0.012, max(0.006, aa)) * inHead;
    float light = head * 0.9 + shaft + fletch * 0.55 + trail + ridge;
    light += Gauss(length(s.p - float2(0.7, 0)), 0.16) * 0.4;
    float keep = Keep(saturate(x * 0.5 + 0.5) * 0.7 + 0.3, s.rem, 0.1);
    half3 col = lerp(s.tint, 1, saturate(head * 0.5 + shaft * 0.6 + ridge)) * light * keep;
    return half4(col * BoardFade(s.p), 1);
}

// Cracks spreading out from the middle (laid onto the floor by the caller, or upright on a
// body): jagged lines glowing hot inside, longer as it goes on, and closing from their tips.
half4 ShapeCrack(ShapeIn s)
{
    float light = 0;
    [unroll]
    for (int i = 0; i < 9; i++)
    {
        float a0 = (i + Hash1(i, s.seed) * 0.6) / 9.0 * Tau;
        float len = (0.45 + 0.5 * Hash1(i + 10, s.seed)) * saturate(s.k * 3.0 + 0.2);
        // Jagged: the line's heading wobbles with the distance from the middle.
        float a = a0 + (Noise(float3(s.r * 7.0, i * 3.1, s.seed * 11.0)) - 0.5) * 0.5;
        float2 u = float2(cos(a), sin(a));
        float along = dot(s.p, u);
        float perp = abs(s.p.x * u.y - s.p.y * u.x);
        float t = saturate(along / max(len, 1e-3));
        float on = step(0, along) * (1 - smoothstep(0.85, 1.0, t));
        float w = max(0.018 * (1 - t) + 0.004, s.aa);
        light += on * (Gauss(perp, w) * 1.4 + Gauss(perp, w * 4.0) * 0.3) * (1 - t * 0.6);
    }
    light += Gauss(s.r, 0.12) * 0.8;
    float keep = Keep(1 - s.r * 0.9, s.rem, 0.1);
    half3 col = HeatColor(saturate(light * 0.6), s.tint) * light * keep;
    return half4(col * BoardFade(s.p), 1);
}

// A whirlwind seen from the side, its foot at the bottom: a funnel widening upward, made of bands
// that swirl round it (brighter on the near side), swaying as it goes.
half4 ShapeTornado(ShapeIn s)
{
    float h = saturate((s.p.y + 0.9) / 1.8);
    float sway = (Noise(float3(h * 2.0, s.seed * 5.0, s.k * 2.0)) - 0.5) * 0.25 * h;
    float x = s.p.x - sway;
    float w = lerp(0.12, 0.8, pow(h, 0.8));
    float u = x / max(w, 1e-3);
    float inside = 1 - smoothstep(0.85, 1.05, abs(u));
    float phase = asin(clamp(u, -1, 1));
    float bands = Noise(float3(phase * 2.0 + h * 9.0 - s.k * 18.0, h * 6.0, s.seed * 3.0));
    float streak = smoothstep(0.5, 0.85, bands);
    float near = 0.55 + 0.45 * cos(phase);
    float edge = Gauss(abs(u) - 0.92, 0.12) * 0.5 * inside;
    float vertical = smoothstep(-0.95, -0.8, s.p.y) * (1 - smoothstep(0.75, 0.98, s.p.y));
    float light = (inside * (streak * near * 1.2 + 0.15) + edge) * vertical;
    float keep = Keep(bands * 0.6 + (1 - h) * 0.4, s.rem, 0.1);
    half3 col = s.tint * light * keep + saturate(streak * inside * near * vertical - 0.6) * keep * 0.6;
    return half4(col * BoardFade(s.p), 1);
}

// A bubble (a barrier, or a bubble of poison): a thin bright rim, a faint body brighter toward
// its edge, a highlight at the top left and a swirl of colour over it; it bursts into arcs.
half4 ShapeBubble(ShapeIn s)
{
    float swirl = Noise(float3(s.dir * 2.0 + s.seed * 7.0, s.k * 2.0 + s.r * 3.0));
    float rim = Gauss(s.r - 0.82, max(0.035, s.aa * 2.0));
    float body = (1 - smoothstep(0.7, 0.86, s.r)) * (0.12 + 0.25 * pow(saturate(s.r / 0.82), 3.0));
    float spot = Gauss(length(s.p - float2(-0.35, 0.38)), 0.1) * 1.2
        + Gauss(length(s.p - float2(0.3, -0.42)), 0.06) * 0.3;
    float keep = Keep(swirl * 0.8 + 0.2, s.rem, 0.06);
    float light = (rim * (0.8 + 0.5 * swirl) + body * (0.6 + 0.8 * swirl)) * keep + spot * s.rem;
    half3 col = s.tint * light + spot * s.rem * 0.6;
    return half4(col * BoardFade(s.p), 1);
}

// A leaf (or, white and long, a feather of light): a pointed blade along y with a midrib and
// veins, a lit half and a bright rim, curling a little by its seed; eaten from its edges.
half4 ShapeLeaf(ShapeIn s)
{
    float2 p = s.p;
    float y01 = p.y * 0.5 + 0.5;
    float w = 0.42 * pow(saturate(sin(Pi * saturate(y01 * 1.05))), 0.8);
    float bend = (y01 - 0.5) * (y01 - 0.5) * 0.8 * (Hash1(1.0, s.seed) - 0.5);
    float x = p.x - bend;
    float aa = max(s.aa * 1.5, 0.02);
    float inside = (1 - smoothstep(w - aa, w + aa, abs(x))) * step(-0.95, p.y) * step(p.y, 0.95);
    float rib = Gauss(x, max(0.02, s.aa)) * inside;
    float veins = Gauss(frac(y01 * 6.0 - abs(x) * 3.0) - 0.5, 0.08) * inside * 0.3;
    float lit = x < 0 ? 1.0 : 0.6;
    float rim = Gauss(abs(x) - w, 0.03) * inside;
    float keep = Keep(1 - abs(x) / max(w, 1e-3) * 0.7, s.rem, 0.1);
    float light = (inside * lit * 0.6 + rib * 0.7 + veins + rim * 0.5) * keep;
    return half4(s.tint * light + rib * keep * 0.3, 1);
}

// Arrows pointing up (a power rising; turned over, a power falling): two chevrons sliding up the
// board as it goes on, each fading in at the bottom and out at the top.
half4 ShapeChevron(ShapeIn s)
{
    float light = 0;
    [unroll]
    for (int i = 0; i < 2; i++)
    {
        float y0 = frac(s.k * 1.2 + i * 0.5) * 1.4 - 0.7;
        float2 q = float2(abs(s.p.x), s.p.y - y0);
        float d = abs(q.y + q.x * 0.75) * 0.8;
        float on = 1 - smoothstep(0.5, 0.58, q.x);
        float fade = sin(saturate((y0 + 0.7) / 1.4) * Pi);
        light += on * (Gauss(d, max(0.05, s.aa)) * 1.2 + Gauss(d, 0.14) * 0.3) * fade
            * (1 - q.x * 1.2);
    }
    light *= s.rem;
    return half4(s.tint * light + saturate(light - 1.0) * 0.4, 1);
}

// A snowflake crystal: six arms with side branches and a hexagonal heart. It grows out from the
// middle and breaks off from the arms' tips.
half4 ShapeSnowflake(ShapeIn s)
{
    float a = atan2(s.p.y, s.p.x) + s.seed * Tau;
    const float sector = Tau / 6.0;
    float local = (frac(a / sector + 0.5) - 0.5) * sector;
    float2 q = float2(cos(local), abs(sin(local))) * s.r;
    float grow = saturate(s.k * 3.0 + 0.25);
    float len = 0.88 * grow;
    float w = max(0.022, s.aa);
    float arm = Gauss(q.y, w) * (1 - smoothstep(len - 0.02, len, q.x));
    float branches = 0;
    [unroll]
    for (int i = 1; i <= 3; i++)
    {
        float bx = len * (0.28 + 0.2 * i);
        float bl = 0.22 * (1.2 - 0.3 * i) * grow;
        float2 b = q - float2(bx, 0);
        float along = b.x * 0.5 + b.y * 0.8660254;
        float perp = abs(b.x * 0.8660254 - b.y * 0.5);
        branches += Gauss(perp, w * 0.8) * step(0, along) * (1 - smoothstep(bl - 0.02, bl, along));
    }
    float heart = Gauss(abs(s.r - 0.16 * grow), w) + Gauss(s.r, 0.08) * 0.8;
    float lines = arm + branches + heart;
    float keep = Keep(1 - s.r, s.rem, 0.08);
    float light = (lines * 1.2 + Gauss(s.r, 0.5) * 0.15) * keep;
    half3 col = s.tint * light + saturate(lines - 0.7) * 0.5 * keep;
    return half4(col * BoardFade(s.p), 1);
}

// A crest shaped like a shield, standing upright: a bright rim, a faint face brighter toward its
// edges, a cross on it, and a band of light sweeping over it.
half4 ShapeCrest(ShapeIn s)
{
    float2 p = s.p;
    float t = saturate((0.75 - p.y) / 1.6);
    float w = 0.7 * sqrt(saturate(1 - t * t));
    float edge = max(abs(p.x) - w, p.y - 0.75);
    edge = p.y < -0.85 ? max(edge, -0.85 - p.y) : edge;
    float aa = max(s.aa * 1.5, 0.015);
    float face = 1 - smoothstep(-aa, aa, edge);
    float rim = Gauss(edge, max(0.03, s.aa * 2.0));
    float crossLine = (Gauss(p.x, 0.035) * step(-0.6, p.y) * step(p.y, 0.55)
        + Gauss(p.y - 0.2, 0.035) * step(abs(p.x), 0.38)) * face;
    float sweep = Gauss(p.x * 0.8 + p.y * 0.6 - (s.k * 3.0 - 1.2), 0.15) * face;
    float fres = pow(saturate((edge + 0.4) / 0.4), 2.0) * face * 0.4;
    float keep = Keep(Noise(float3(p * 5.0, s.seed * 9.0)) * 0.6 + 0.4 * (1 - abs(p.y)), s.rem, 0.08);
    float light = (rim * 1.3 + face * 0.12 + fres + crossLine * 0.9 + sweep * 0.5) * keep;
    half3 col = s.tint * light + rim * keep * 0.4;
    return half4(col * BoardFade(p), 1);
}

// A sight locking on: two rings broken at the four ticks pointing in, closing in on the middle
// as it goes on, with a hot dot at the centre.
half4 ShapeReticle(ShapeIn s)
{
    float close = lerp(1.0, 0.72, saturate(s.k * 2.5));
    float w = max(0.014, s.aa * 1.2);
    float around = atan2(s.p.y, s.p.x);
    float gaps = step(0.12, abs(frac(around / (Pi * 0.5) + 0.5) - 0.5));
    float outer = Gauss(s.r - 0.8 * close, w) * gaps;
    float inner = Gauss(s.r - 0.42 * close, w * 0.8);
    float2 a = abs(s.p);
    float far = max(a.x, a.y);
    float ticks = Gauss(min(a.x, a.y), w) * step(0.5 * close, far) * step(far, 0.98 * close);
    float centre = Gauss(s.r, 0.05) * 1.5;
    float light = (outer * 1.1 + inner * 0.7 + ticks + centre) * s.rem;
    return half4(s.tint * light + saturate(light - 1.0) * 0.4, 1);
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
