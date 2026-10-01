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

// An ice lance flying along +x: a long shaft swelling into a faceted head, a bright ridge and
// rims, and a cold glow around the head.
half4 ShapeIceSpear(ShapeIn s)
{
    float x = s.p.x;
    float y = s.p.y;
    const float tip = 0.86;
    float w;
    if (x > 0.55)
        w = 0.13 * saturate((tip - x) / (tip - 0.55));
    else if (x > 0.4)
        w = lerp(0.055, 0.13, (x - 0.4) / 0.15);
    else
        w = lerp(0.02, 0.05, saturate((x + 0.9) / 1.3)) * step(-0.9, x);
    float aa = max(s.aa, 0.004);
    float inside = 1 - smoothstep(w - aa, w + aa, abs(y));
    float facet = y > 0 ? 0.85 : 0.4;
    float shaft = step(-0.9, x) * step(x, tip);
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

// A spike of clear ice standing on its root (at 3% of the height): a lit facet and a shadowed
// one, bright rims, faint streaks; it crumbles in chunks from the tip down.
half4 ShapeIceSpike(ShapeIn s)
{
    float b = 0.5 + 0.35 * Hash1(1.0, s.seed);
    float tx = (Hash1(2.0, s.seed) - 0.5) * 0.5;
    float sx = (Hash1(3.0, s.seed) - 0.5) * b * 0.8;
    float t = saturate((s.p.y + 0.94) / 1.9);
    float left = lerp(-b, tx, t);
    float right = lerp(b, tx, t);
    float dEdge = min(s.p.x - left, right - s.p.x);
    float aa = max(s.aa, 0.01);
    float inside = smoothstep(-aa, aa, dEdge) * step(-0.94, s.p.y) * step(s.p.y, 0.96);
    float split = lerp(sx, tx, t);
    float lit = smoothstep(split + 0.02, split - 0.02, s.p.x);
    half3 face = lerp(half3(0.42, 0.62, 0.88), half3(0.85, 0.95, 1.0), lit);
    // Clearer toward the root, frosted toward the tip.
    face *= 0.8 + 0.3 * Noise(float3(s.p.x * 9.0 + s.seed * 7.0, s.p.y * 1.4, s.seed * 3.0)) + 0.15 * t;
    float rim = Gauss(max(dEdge, 0), 0.04) * inside;
    float splitLine = Gauss(s.p.x - split, 0.012) * inside * 0.7;
    // Faint cracks inside, catching the light.
    float crack = Gauss(frac((s.p.x * 1.3 + s.p.y * 0.9 + s.seed * 5.0) * 2.0) - 0.5, 0.03)
        * inside * 0.35 * step(0.4, Hash1(floor((s.p.x * 1.3 + s.p.y * 0.9) * 2.0), s.seed));
    float2 cell = VoronoiCell(float2(s.p.x * 2.5, s.p.y * 3.5) + s.seed * 10.0);
    float keep = Keep(Hash(float3(cell, s.seed * 4.0)) * 0.65 + (1 - t) * 0.35, s.rem, 0.04);
    half3 col = s.tint * (face + rim * 1.1 + splitLine + crack);
    return half4(col, inside * (0.5 + 0.45 * rim + 0.2 * splitLine) * keep);
}

// A broken chip (of ice, or of dark debris): a skewed diamond with a lit half and a bright rim.
half4 ShapeChip(ShapeIn s)
{
    float2 q = Rotate(s.p, s.seed * Tau);
    q.x += q.y * (Hash1(5.0, s.seed) - 0.5) * 0.8;
    float ax = 0.45 + 0.4 * Hash1(6.0, s.seed);
    float ay = 0.75 + 0.2 * Hash1(7.0, s.seed);
    float d = abs(q.x) / ax + abs(q.y) / ay;
    float aa = max(s.aa * 2.0, 0.03);
    float inside = 1 - smoothstep(1 - aa, 1 + aa, d);
    float lit = q.x + q.y > 0 ? 1.0 : 0.55;
    float rim = Gauss(d - 0.92, 0.08) * inside;
    float keep = Keep(1 - d * 0.7, s.rem, 0.08);
    return half4(s.tint * (lit + rim * 0.6), inside * keep);
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
