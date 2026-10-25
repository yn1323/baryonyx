using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// Layered pieces of the physical, fire and ice card skills (doc/catalog/skills/), each split
    /// into layers that differ in cover, width and timing, so that no layer appears, peaks and
    /// goes at the same moment as another (.agents/skills/vfx-authoring/SKILL.md): a column of
    /// fire, its sparks spat before it, cold air that sinks and crawls along the floor, the glow
    /// of a hot floor, grit and dust thrown by a blow.
    /// </summary>
    public sealed partial class BattleSkillVfx
    {
        // The looks of the sword arc's layers (the shader's SlashArc _Rich): 1 the body (its
        // strands and edge), 2 the haze of light round it, 3 the white-hot leading edge alone,
        // 4 the afterimage of thin strands that breaks up as it is eaten.
        private const float ArcBody = 1f;
        private const float ArcHaze = 2f;
        private const float ArcCore = 3f;
        private const float ArcTrace = 4f;

        // How long a sword arc takes to be drawn and worn (its body's life, s): every layer
        // reads its progress on this clock, so all of them share one stroke.
        private const float ArcDraw = 0.26f;

        // The looks of the shader's Pillar _Rich: 1 a shaft of light seen by its dust, 2 fire
        // erupting from the floor.
        private const float PillarLight = 1f;
        private const float PillarFire = 2f;

        // Soot over a fire, lit brown from below at first and greying as it rises.
        private static readonly Color Soot = new(0.2f, 0.15f, 0.13f, 0.75f);

        // A cold air's frost, heavy and pale.
        private static readonly Color ColdAir = new(0.74f, 0.88f, 1f, 0.45f);

        /// <summary>
        /// How strongly a layer shows <paramref name="t"/> s after it was made: nothing before
        /// <paramref name="start"/>, up over <paramref name="attack"/>, held until
        /// <paramref name="hold"/> and gone by <paramref name="end"/> (F3).
        /// </summary>
        public static float Envelope(float t, float start, float attack, float hold, float end)
        {
            if (t < start || t >= end)
                return 0f;
            float up = attack <= 0f ? 1f : Mathf.Clamp01((t - start) / attack);
            float down = t <= hold ? 1f : 1f - Mathf.Clamp01((t - hold) / Mathf.Max(0.001f, end - hold));
            return up * up * (3f - 2f * up) * down;
        }

        /// <summary>
        /// Fire erupting from the floor at <paramref name="feet"/>, <paramref name="width"/> wide
        /// and <paramref name="height"/> tall, burning <paramref name="seconds"/>, as a real jet of
        /// fire behaves: it shoots up in a tenth of a second with a white-hot core at its foot that
        /// is gone in a quarter of a second; its body (fire drawn as density, behind the target)
        /// rolls upward faster at the top, swells, then narrows and is eaten from the top down;
        /// tongues lick up in front of the target, shorter-lived, so the target stands in the fire;
        /// soot boils off its top a moment later and keeps rising after the fire is gone; embers
        /// are flung up and drift; the floor glows hot and is left scorched. It lights the target
        /// and the floor with a flickering light.
        /// </summary>
        private void FireColumn(Vector2 feet, float width, float height, float seconds, Color color)
        {
            float seed = UnityEngine.Random.value;
            // Soot first, so it is drawn behind every light (and behind the fire on its layer).
            Spray(
                backSmoke,
                feet + Vector2.up * (height * 0.6f),
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    Bright = 0.4f,
                    From = new Color(0.42f, 0.26f, 0.15f, Soot.a),
                    To = Soot,
                    Speed = new Vector2(80f, 220f),
                    Direction = 90f,
                    Spread = 50f,
                    Life = new Vector2(1.2f, 2f),
                    Size = new Vector2(0.55f, 0.9f) * width,
                    Grow = 0.45f,
                    Shrink = 1.9f,
                    Gravity = -90f,
                    Drag = 1.2f,
                    Spin = 30f,
                    Hold = 0.35f,
                    Delay = new Vector2(0.12f, seconds * 0.55f),
                    Area = width * 0.25f,
                },
                9
            );
            // The body: fire as density, behind the target.
            Shot Column(RectTransform layer, Vector2 size)
            {
                var shot = Spawn(layer, BattleVfxShape.Pillar, feet, size, color, new Vector2(0.5f, 0.1f));
                shot.Seed = seed;
                shot.Image.material = RichMaterialOf(BattleVfxShape.Pillar, PillarFire);
                return shot;
            }
            // The flame fills about a third of its board, so the board is wider than the fire.
            var body = Column(backLight, new Vector2(width * 1.8f, height));
            Animate(
                body,
                seconds,
                (s, k) =>
                {
                    float t = k * seconds;
                    float rise = EaseOut(Mathf.Clamp01(t / 0.1f));
                    // Swells as it roars, then narrows as it burns out.
                    float girth = Mathf.Lerp(0.6f, 1.05f, EaseOut(Mathf.Clamp01(t / 0.25f)))
                        * Mathf.Lerp(1f, 0.55f, EaseIn(Mathf.Clamp01((k - 0.45f) / 0.55f)));
                    s.Holder.localScale = new Vector3(girth, 0.05f + 0.95f * rise, 1f);
                    // Its own colour, cooling toward red as it dies.
                    var tint = Color.Lerp(color, FireColor(0.35f), k);
                    Show(s, tint, t * 1.2f, Eaten(k, 0.4f), ErodeBright * 1.6f);
                }
            );
            // The core: a narrow white-hot jet at the foot, there only while it bursts.
            var core = Column(backLight, new Vector2(width * 0.5f, height * 0.3f));
            const float coreLife = 0.26f;
            Animate(
                core,
                coreLife,
                (s, k) =>
                {
                    float t = k * coreLife;
                    float rise = EaseOut(Mathf.Clamp01(t / 0.08f));
                    s.Holder.localScale = new Vector3(1f - 0.4f * k, 0.05f + 0.95f * rise, 1f);
                    var tint = new Color(1f, 0.95f, 0.8f, Envelope(t, 0f, 0.03f, 0.1f, coreLife));
                    Show(s, tint, t * 1.2f, Eaten(k, 0.3f), CoreBright);
                }
            );
            // Tongues licking up in front of the target, shorter-lived than the body.
            Spray(
                frontLight,
                feet + Vector2.up * 10f,
                new Burst
                {
                    Shape = BattleVfxShape.FlameTongue,
                    Bright = ErodeBright,
                    Fire = true,
                    Speed = new Vector2(380f, 760f),
                    Direction = 90f,
                    Spread = 16f,
                    Life = new Vector2(0.25f, 0.5f),
                    Size = new Vector2(0.3f, 0.5f) * width,
                    Grow = 0.5f,
                    Shrink = 1.2f,
                    Gravity = -200f,
                    Drag = 1.6f,
                    Spin = 12f,
                    Hold = 0.15f,
                    Delay = new Vector2(0.02f, seconds * 0.45f),
                    Area = width * 0.22f,
                },
                14
            );
            // Lumps of flame torn off the top, rolling up behind.
            Spray(
                backLight,
                feet + Vector2.up * (height * 0.55f),
                new Burst
                {
                    Shape = BattleVfxShape.Flame,
                    Material = RichMaterialOf(BattleVfxShape.Flame),
                    Bright = ErodeBright,
                    Fire = true,
                    Speed = new Vector2(150f, 380f),
                    Direction = 90f,
                    Spread = 40f,
                    Life = new Vector2(0.35f, 0.6f),
                    Size = new Vector2(0.3f, 0.5f) * width,
                    Grow = 0.5f,
                    Shrink = 1.3f,
                    Gravity = -260f,
                    Drag = 2f,
                    Spin = 60f,
                    Hold = 0.15f,
                    Delay = new Vector2(0.1f, seconds * 0.6f),
                    Area = width * 0.2f,
                },
                6
            );
            // Embers flung up, slowed by the air and drifting as they cool.
            Spray(
                frontLight,
                feet + Vector2.up * (height * 0.3f),
                new Burst
                {
                    Shape = BattleVfxShape.Spark,
                    Bright = CoreBright,
                    From = new Color(1f, 0.92f, 0.6f),
                    To = new Color(1f, 0.3f, 0.06f, 0f),
                    Speed = new Vector2(250f, 900f),
                    Direction = 90f,
                    Spread = 70f,
                    Life = new Vector2(0.6f, 1.6f),
                    Size = new Vector2(5f, 11f),
                    Gravity = -120f,
                    Drag = 2.4f,
                    Delay = new Vector2(0.03f, seconds * 0.7f),
                    Area = width * 0.3f,
                },
                30
            );
            // The floor: a hot glow that flickers with the fire, and a scorch left after it.
            HotFloor(feet, width * 1.5f, seconds * 0.7f, seconds * 0.5f, color);
            FloorMark(
                BattleVfxShape.Scorch,
                feet,
                Vector2.one * width,
                new Color(0.06f, 0.045f, 0.04f),
                0.8f,
                seconds + 0.9f,
                flat: true
            );
            Illuminate(feet + Vector2.up * 60f, color, 1f, width * 1.5f, seconds * 0.6f, seconds * 0.4f, flicker: 0.45f);
        }

        /// <summary>
        /// A floor glowing hot round <paramref name="feet"/>, <paramref name="size"/> wide, as
        /// embers and burning ground do: a pool of fire light lying on the floor that flickers,
        /// held for <paramref name="hold"/> and cooling to a dull red over <paramref name="fade"/>.
        /// </summary>
        private void HotFloor(Vector2 feet, float size, float hold, float fade, Color color)
        {
            if (backLight == null)
                return;
            var pool = Spawn(backLight, BattleVfxShape.Glow, feet, new Vector2(size, size), color);
            pool.Holder.localScale = new Vector3(1f, FloorTilt, 1f);
            float seed = UnityEngine.Random.value * 40f;
            Animate(
                pool,
                hold + fade,
                (s, k) =>
                {
                    float t = k * (hold + fade);
                    float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(seed, t * 12f);
                    float alpha = 0.8f * flicker * Envelope(t, 0f, 0.05f, hold, hold + fade);
                    var tint = Color.Lerp(color, FireColor(0.45f), Mathf.Clamp01((t - hold) / fade));
                    Show(s, new Color(tint.r, tint.g, tint.b, alpha), k, 0f);
                }
            );
        }

        /// <summary>
        /// The ground spitting before fire bursts out of it at <paramref name="feet"/>: a few
        /// sparks hop out and fall back, and thin wisps of hot smoke rise, over
        /// <paramref name="seconds"/>.
        /// </summary>
        private void Spit(Vector2 feet, float width, float seconds)
        {
            Spray(
                frontLight,
                feet + Vector2.up * 6f,
                new Burst
                {
                    Shape = BattleVfxShape.Spark,
                    Bright = CoreBright,
                    From = new Color(1f, 0.85f, 0.45f),
                    To = new Color(0.9f, 0.25f, 0.05f, 0f),
                    Speed = new Vector2(120f, 380f),
                    Direction = 90f,
                    Spread = 70f,
                    Life = new Vector2(0.25f, 0.5f),
                    Size = new Vector2(4f, 8f),
                    Gravity = 900f,
                    Drag = 1f,
                    Delay = new Vector2(0f, seconds),
                    Area = width * 0.3f,
                },
                12
            );
            Spray(
                backSmoke,
                feet + Vector2.up * 20f,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    Bright = 0.8f,
                    From = new Color(0.36f, 0.26f, 0.2f, 0.35f),
                    To = new Color(0.3f, 0.28f, 0.27f, 0.35f),
                    Speed = new Vector2(40f, 110f),
                    Direction = 90f,
                    Spread = 30f,
                    Life = new Vector2(0.5f, 0.9f),
                    Size = new Vector2(0.18f, 0.3f) * width,
                    Aspect = 1.6f,
                    Grow = 0.4f,
                    Shrink = 1.6f,
                    Gravity = -60f,
                    Drag = 1f,
                    Hold = 0.3f,
                    Delay = new Vector2(0f, seconds * 0.8f),
                    Area = width * 0.25f,
                },
                4
            );
        }

        /// <summary>
        /// Cold air at <paramref name="feet"/>, as air chilled by ice behaves: heavier than the air
        /// round it, it sinks and spills out both ways along the floor, slowing, in low sheets of
        /// pale gas behind the feet and over them, gone over about <paramref name="seconds"/>.
        /// <paramref name="density"/> (0 to 1) is how thick.
        /// </summary>
        private void ColdMist(Vector2 feet, float width, float seconds, float density, float delay = 0f)
        {
            var color = new Color(ColdAir.r, ColdAir.g, ColdAir.b, ColdAir.a * density);
            for (int front = 0; front < 2; front++)
                foreach (float side in new[] { 0f, 180f })
                    Spray(
                        front == 0 ? backSmoke : frontSmoke,
                        feet + Vector2.up * (front == 0 ? 14f : -8f),
                        new Burst
                        {
                            Shape = BattleVfxShape.Smoke,
                            Bright = 1.1f,
                            From = color,
                            To = color,
                            Speed = new Vector2(60f, 220f),
                            Direction = side,
                            Spread = 24f,
                            Life = new Vector2(seconds * 0.6f, seconds),
                            Size = new Vector2(0.35f, 0.6f) * width * (front == 0 ? 1f : 0.75f),
                            Aspect = 0.26f,
                            Grow = 0.5f,
                            Shrink = 1.9f,
                            Gravity = 40f,
                            Drag = 1.6f,
                            Level = true,
                            Hold = 0.35f,
                            Delay = new Vector2(delay, delay + seconds * 0.25f),
                            Area = width * 0.12f,
                        },
                        front == 0 ? 2 : 1
                    );
        }

        /// <summary>
        /// The floor cracking round <paramref name="at"/>, <paramref name="size"/> across, as stone
        /// breaks under a blow (the crack's rupture look): hairline slits that snap outward a step
        /// at a time every <paramref name="gap"/> s after <paramref name="delay"/>, each new stretch
        /// catching the light for a moment, shown by their shadow; they stay
        /// <paramref name="stays"/> s and close up from the outside.
        /// </summary>
        private void Rupture(Vector2 at, float size, float gap, float delay, float stays)
        {
            if (backSmoke == null)
                return;
            var crack = Spawn(backSmoke, BattleVfxShape.Crack, at, new Vector2(size, size), Earthen);
            crack.Image.material = RichMaterialOf(BattleVfxShape.Crack, RuptureLook);
            crack.Holder.localScale = new Vector3(1f, FloorTilt, 1f);
            float total = delay + stays;
            Animate(
                crack,
                total,
                (s, k) =>
                {
                    float t = k * total - delay;
                    if (t < 0f)
                    {
                        Show(s, Color.clear, 0f, 0f);
                        return;
                    }
                    // Six snaps; the fraction is how long since the last, for its glint.
                    int made = Mathf.Min(6, 1 + Mathf.FloorToInt(t / gap));
                    float since = t - (made - 1) * gap;
                    float snaps = made + Mathf.Min(0.99f, since / RuptureGlint);
                    Show(s, Earthen, snaps, Eaten(t / stays, 0.6f));
                }
            );
        }

        /// <summary>
        /// A shaft of light (the shader's lit dust) standing on <paramref name="feet"/>,
        /// <paramref name="size"/> big, slammed <paramref name="down"/> from above or rising, in
        /// two layers on one seed: the wide shaft, and a narrow white-hot core there only for the
        /// first quarter of a second.
        /// </summary>
        private void LightColumn(Vector2 feet, Vector2 size, Color color, float seconds, bool down)
        {
            float seed = UnityEngine.Random.value;
            void Shaft(RectTransform layer, Vector2 shape, float life, Color tint, float bright, float hold)
            {
                if (layer == null)
                    return;
                var shot = Spawn(
                    layer,
                    BattleVfxShape.Pillar,
                    down ? feet + Vector2.up * (shape.y * 0.9f) : feet,
                    shape,
                    Color.clear,
                    down ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.1f)
                );
                shot.Seed = seed;
                shot.Image.material = RichMaterialOf(BattleVfxShape.Pillar, PillarLight);
                Animate(
                    shot,
                    life,
                    (s, k) =>
                    {
                        float t = k * life;
                        float grow = EaseOut(Mathf.Clamp01(t / 0.08f));
                        s.Holder.localScale = new Vector3(
                            Mathf.Lerp(1f, 0.6f, EaseIn(k)),
                            0.05f + 0.95f * grow,
                            1f
                        );
                        var c = new Color(tint.r, tint.g, tint.b, Envelope(t, 0f, 0.04f, life * hold, life));
                        Show(s, c, t, Eaten(k, hold), bright);
                    }
                );
            }
            Shaft(backLight, size, seconds, color, ErodeBright * 1.3f, 0.45f);
            Shaft(frontLight, new Vector2(size.x * 0.3f, size.y), 0.25f, Color.Lerp(color, Color.white, 0.7f), CoreBright * 1.5f, 0.3f);
        }

        /// <summary>
        /// The ground bursting up under <paramref name="feet"/>, as broken rock flies: a few big
        /// lumps thrown slow and heavy, turning lazily, behind the target and before it; many
        /// small chips thrown fast and high, spinning; and dust boiling up behind that stays
        /// after the rocks have fallen.
        /// </summary>
        private void Rocks(Vector2 feet, float power)
        {
            var rock = new Color(0.4f, 0.32f, 0.25f);
            var dark = new Color(0.28f, 0.23f, 0.19f);
            for (int front = 0; front < 2; front++)
                Spray(
                    front == 0 ? backLight : frontLight,
                    feet + Vector2.up * (front == 0 ? 24f : 6f),
                    new Burst
                    {
                        Shape = BattleVfxShape.Chip,
                        From = rock,
                        To = dark,
                        Speed = new Vector2(260f, 560f) * power,
                        Direction = 90f,
                        Spread = 70f,
                        Life = new Vector2(0.7f, 1.05f),
                        Size = new Vector2(22f, 42f),
                        Gravity = 1500f,
                        Drag = 0.3f,
                        Spin = 240f,
                        Area = 40f,
                    },
                    front == 0 ? 4 : 2
                );
            Spray(
                frontLight,
                feet + Vector2.up * 10f,
                new Burst
                {
                    Shape = BattleVfxShape.Chip,
                    From = rock,
                    To = new Color(dark.r, dark.g, dark.b, 0f),
                    Speed = new Vector2(500f, 1150f) * power,
                    Direction = 90f,
                    Spread = 100f,
                    Life = new Vector2(0.4f, 0.8f),
                    Size = new Vector2(5f, 12f),
                    Gravity = 2000f,
                    Drag = 0.6f,
                    Spin = 900f,
                    Area = 40f,
                },
                Mathf.RoundToInt(16 * power)
            );
            Spray(
                backSmoke,
                feet + Vector2.up * 30f,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    Bright = 0.55f,
                    From = new Color(0.55f, 0.46f, 0.37f, 0.7f),
                    To = new Color(0.45f, 0.41f, 0.37f, 0.7f),
                    Speed = new Vector2(80f, 260f) * power,
                    Direction = 90f,
                    Spread = 70f,
                    Life = new Vector2(0.9f, 1.4f),
                    Size = new Vector2(120f, 210f),
                    Grow = 0.4f,
                    Shrink = 1.8f,
                    Gravity = -40f,
                    Drag = 1.5f,
                    Spin = 20f,
                    Hold = 0.3f,
                    Delay = new Vector2(0.04f, 0.2f),
                    Area = 50f,
                },
                5
            );
        }

        /// <summary>
        /// Hard ground breaking under a blow at <paramref name="feet"/>: grit and chips flung up
        /// and falling back fast, and a little dust that rolls low and settles slower.
        /// </summary>
        private void Grit(Vector2 feet, float power, Vector2 direction)
        {
            Spray(
                frontLight,
                feet + Vector2.up * 12f,
                new Burst
                {
                    Shape = BattleVfxShape.Chip,
                    From = new Color(0.45f, 0.37f, 0.3f),
                    To = new Color(0.32f, 0.27f, 0.22f),
                    Speed = new Vector2(240f, 760f) * power,
                    Direction = Angle(direction),
                    Spread = 70f,
                    Life = new Vector2(0.45f, 0.85f),
                    Size = new Vector2(6f, 18f),
                    Gravity = 1800f,
                    Drag = 0.5f,
                    Spin = 720f,
                    Area = 30f,
                },
                Mathf.RoundToInt(10 * power)
            );
            DustKick(feet, Vector2.right, 200f * power);
            DustKick(feet, Vector2.left, 200f * power);
        }
    }
}
