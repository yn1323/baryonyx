using System.Collections;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The wind-ups the skills open with, played on their own in the skill lab
    /// (doc/features/skill-windups.md).
    /// </summary>
    public enum BattleWindUp
    {
        /// <summary>A spell's charge, with the flat magic circle.</summary>
        Spell,

        /// <summary>A big spell's charge, lit from below more strongly.</summary>
        BigSpell,

        /// <summary>A unique skill's first step of no element (the plain charge the lab plays).</summary>
        UniqueFirstStep,

        /// <summary>A unique skill's last step: the night falling and a charge in three steps.</summary>
        UniqueLastStep,

        /// <summary>A fire spell's charge, with the layered circle burned into the floor.</summary>
        FireSpell,

        /// <summary>The air and light round the user drawn in to it, then held for an instant.</summary>
        Inhale,

        /// <summary>A rising aura: light rising off the user like heat, and wind pushed out at the feet.</summary>
        Aura,

        /// <summary>A blade catching the light as the user plants a foot.</summary>
        BladeGlint,

        /// <summary>Cold air pouring off the user and flowing out along the floor, frost spreading.</summary>
        ColdFront,

        /// <summary>The user charged with static: small discharges crawling over it, dust lifted.</summary>
        StaticCharge,

        /// <summary>The ground rumbling: cracks spreading from the feet, pebbles hopping.</summary>
        Tremor,

        /// <summary>A charge in three steps, its colour and light stepping up at each.</summary>
        Tiered,
    }

    public sealed partial class BattleSkillVfx
    {
        /// <summary>
        /// Plays a wind-up on its own from <paramref name="caster"/>, and waits while what it
        /// leaves wears away. Those of no element are drawn in fire's colour, so they can be told
        /// apart by their shapes alone; the elements' own are drawn in theirs.
        /// </summary>
        public IEnumerator PlayWindUp(BattleWindUp kind, RectTransform caster)
        {
            var color = ColorOf(BattleSkillVfxKind.Fire);
            switch (kind)
            {
                case BattleWindUp.BigSpell:
                    yield return Charge(caster, color, 0.3f, big: true);
                    break;
                case BattleWindUp.UniqueFirstStep:
                    yield return WindUp(caster, color, 1);
                    break;
                case BattleWindUp.UniqueLastStep:
                    yield return WindUp(caster, color, 2);
                    break;
                case BattleWindUp.FireSpell:
                    yield return FireCharge(caster, FireChargeSeconds);
                    break;
                case BattleWindUp.Inhale:
                    yield return Inhale(caster, color, 0.5f);
                    break;
                case BattleWindUp.Aura:
                    yield return Aura(caster, Gold, 0.6f);
                    break;
                case BattleWindUp.BladeGlint:
                    yield return BladeGlint(caster, Steel, 0.35f);
                    break;
                case BattleWindUp.ColdFront:
                    yield return ColdFront(caster, 0.5f);
                    break;
                case BattleWindUp.StaticCharge:
                    yield return StaticCharge(caster, 0.5f);
                    break;
                case BattleWindUp.Tremor:
                    yield return Tremor(caster, 0.55f);
                    break;
                case BattleWindUp.Tiered:
                    yield return Tiered(caster, 0.3f, color);
                    break;
                default:
                    yield return Charge(caster, color, 0.3f, big: false);
                    break;
            }
            DimTo(0f, 0.4f);
            yield return Wait(HotCircleAfter);
        }

        // How long a fire spell is charged (doc/catalog/skills/skill-fire.md#演出の時間設計).
        private const float FireChargeSeconds = 0.4f;

        /// <summary>
        /// The wind-up of a fire spell: the stage darkens, a magic circle burned into the floor
        /// heats under the user (<see cref="HotCircle"/>), a glow swells on the user, lit from
        /// below by the circle, and a twinkle marks the moment it is let go.
        /// </summary>
        private IEnumerator FireCharge(RectTransform caster, float seconds)
        {
            var color = ColorOf(BattleSkillVfxKind.Fire);
            if (dimTarget < SpellDim)
                DimTo(SpellDim, 0.15f);
            var center = Center(FrontLayer, caster);
            var feet = Feet(FrontLayer, caster);
            HotCircle(feet, seconds);
            Glow(frontLight, center, color, 280f, seconds + 0.2f, 0.6f);
            Illuminate(
                feet + Vector2.down * 90f,
                color,
                0.6f,
                280f,
                seconds + 0.1f,
                0.4f,
                spill: false
            );
            yield return Wait(seconds);
            Star(center, Color.white, 180f, 0.18f);
        }

        // The layered circle's parts, as the looks of the magic circle's material.
        private const float CircleOuterLook = 1f;
        private const float CircleGlyphsLook = 2f;
        private const float CircleInnerLook = 3f;
        private const float CircleVeilLook = 4f;

        // The layered circle's size, and how long it takes to cool and wear away once let go.
        private const float HotCircleSize = 340f;
        private const float HotCircleCool = 0.35f;
        private const float HotCircleAfter = 0.7f;

        /// <summary>
        /// A magic circle burned into the floor at <paramref name="feet"/>, heating while a spell
        /// is charged for <paramref name="charge"/> seconds and cooling once it is let go. Its
        /// outer band, band of glyphs and figure inside are drawn in one after another on boards
        /// of their own, each turning its own way; the lines glow as heated metal does, from dark
        /// red through orange to yellow with a white-hot core, pulsing twice as the heat builds.
        /// A veil of heat rises from its rim, embers float up off it and the floor under it
        /// scorches. As it is let go the figure flares white and the circle draws in a little;
        /// then the veil lifts away, the lines cool back to red and break up, and the scorch and
        /// a thin smoke are left (doc/catalog/skills/skill-fire.md#演出の時間設計).
        /// </summary>
        private void HotCircle(Vector2 feet, float charge)
        {
            if (backLight == null)
                return;
            float life = charge + HotCircleAfter;
            float erode = (charge + 0.26f) / life;

            float Heat(float t)
            {
                if (t >= charge)
                    return Mathf.Clamp01(1f - (t - charge) / HotCircleCool);
                float k = t / charge;
                if (k < 0.5f)
                    return 0.2f * k / 0.5f;
                float u = (k - 0.5f) / 0.5f;
                // Two pulses as it heats, like a fire fanned.
                float pulse = 0.5f - 0.5f * Mathf.Cos(u * Mathf.PI * 4f);
                return 0.2f + 0.8f * EaseIn(u) + 0.12f * pulse;
            }
            // The white flare and the small draw-in as it is let go.
            float Flare(float t) => Mathf.Exp(-Sq((t - charge) / 0.05f));
            float Shrink(float t) => 1f - 0.07f * Mathf.Exp(-Sq((t - charge - 0.03f) / 0.04f));

            Part(CircleOuterLook, 30f, 0f, 0.08f, inner: false);
            Part(CircleGlyphsLook, -70f, 0.06f, 0.2f, inner: false);
            Part(CircleInnerLook, 45f, 0.1f, 0.2f, inner: true);

            // One part of the circle: turning at turn°/s, drawn in from drawFrom to drawTo s.
            void Part(float look, float turn, float drawFrom, float drawTo, bool inner)
            {
                var shot = Spawn(
                    backLight,
                    BattleVfxShape.MagicCircle,
                    feet,
                    new Vector2(HotCircleSize, HotCircleSize),
                    Color.black
                );
                shot.Image.material = RichMaterialOf(BattleVfxShape.MagicCircle, look);
                shot.Image.rectTransform.localEulerAngles = new Vector3(
                    0f,
                    0f,
                    UnityEngine.Random.Range(0f, 360f)
                );
                float start = shot.Image.rectTransform.localEulerAngles.z;
                Animate(
                    shot,
                    life,
                    (s, k) =>
                    {
                        float t = k * life;
                        s.Image.rectTransform.localEulerAngles = new Vector3(
                            0f,
                            0f,
                            start - turn * t
                        );
                        float grow = (0.9f + 0.1f * EaseOut(Mathf.Min(1f, t / 0.1f))) * Shrink(t);
                        s.Holder.localScale = new Vector3(grow, grow * FloorTilt, 1f);
                        float heat = Heat(t);
                        float flare = inner ? Flare(t) : Flare(t) * 0.35f;
                        var tint = Color.Lerp(HeatColor(heat), Color.white, flare * 0.6f);
                        float drawn = Mathf.Clamp01((t - drawFrom) / (drawTo - drawFrom));
                        Show(
                            s,
                            tint,
                            drawn,
                            Eaten(k, erode),
                            1f + 0.5f * Mathf.Clamp01(heat + flare)
                        );
                    }
                );
            }

            // The veil of heat on the rim, standing on its front curve: behind the user, and
            // faintly in front, as the near side of it passes before the user's feet.
            var rim = new Vector2(HotCircleSize * 0.94f, 300f);
            var foot = feet + Vector2.down * (HotCircleSize * 0.47f * FloorTilt + rim.y * 0.04f);
            Veil(backLight, 0.6f);
            Veil(frontLight, 0.22f);

            void Veil(RectTransform layer, float strength)
            {
                if (layer == null)
                    return;
                var veil = Spawn(
                    layer,
                    BattleVfxShape.MagicCircle,
                    foot,
                    rim,
                    Color.black,
                    new Vector2(0.5f, 0f)
                );
                veil.Image.material = RichMaterialOf(BattleVfxShape.MagicCircle, CircleVeilLook);
                float rises = charge * 0.45f;
                float gone = charge + 0.26f;
                Animate(
                    veil,
                    gone,
                    (s, k) =>
                    {
                        float t = k * gone;
                        float up = EaseOut(Mathf.Clamp01((t - rises) / (charge - rises)));
                        float lift = t < charge ? 0f : EaseIn((t - charge) / (gone - charge));
                        s.Holder.localScale = new Vector3(1f, up * (1f + 0.3f * lift), 1f);
                        s.Holder.anchoredPosition = foot + Vector2.up * (120f * lift);
                        var tint = HeatColor(Heat(t));
                        tint.a = strength;
                        Show(s, tint, t, lift, 1f + 0.5f * Heat(t));
                    }
                );
            }

            // A glow on the floor in the lines' heat colour, and the scorch under it.
            var pool = Spawn(
                backLight,
                BattleVfxShape.Glow,
                feet,
                Vector2.one * HotCircleSize * 1.15f,
                Color.clear
            );
            pool.Holder.localScale = new Vector3(1f, FloorTilt, 1f);
            Animate(
                pool,
                life,
                (s, k) =>
                {
                    float heat = Heat(k * life);
                    var tint = HeatColor(heat);
                    tint.a = 0.15f + 0.35f * heat;
                    Show(s, tint, k, 0f);
                }
            );
            if (backSmoke != null)
            {
                var scorch = Spawn(
                    backSmoke,
                    BattleVfxShape.Scorch,
                    feet,
                    Vector2.one * HotCircleSize * 1.05f,
                    Color.clear
                );
                scorch.Holder.localScale = new Vector3(1f, FloorTilt, 1f);
                float stays = life + 0.6f;
                Animate(
                    scorch,
                    stays,
                    (s, k) =>
                    {
                        float t = k * stays;
                        float into = Mathf.SmoothStep(
                            0f,
                            1f,
                            (t - charge * 0.2f) / (charge * 0.8f)
                        );
                        var dark = new Color(0.07f, 0.045f, 0.035f, 0.42f * into);
                        Show(s, dark, k, Eaten(k, (charge + 0.6f) / stays));
                    }
                );
            }

            // Embers float up off the rim while it heats and just after, cooling as they rise;
            // those off the near side pass in front of the user.
            for (int i = 0; i < 22; i++)
            {
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float radius = HotCircleSize * 0.5f * UnityEngine.Random.Range(0.55f, 0.95f);
                var at =
                    feet + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * FloorTilt) * radius;
                Spray(
                    Mathf.Sin(angle) < 0f ? frontLight : backLight,
                    at,
                    new Burst
                    {
                        Shape = BattleVfxShape.Spark,
                        Bright = CoreBright,
                        From = new Color(1f, 0.62f, 0.22f, 1f),
                        To = new Color(0.7f, 0.12f, 0.03f, 0f),
                        Speed = new Vector2(30f, 90f),
                        Direction = 90f,
                        Spread = 40f,
                        Life = new Vector2(0.5f, 0.9f),
                        Size = new Vector2(9f, 15f),
                        Shrink = 0.5f,
                        Gravity = -160f,
                        Drag = 1.5f,
                        Delay = new Vector2(charge * 0.3f, charge + 0.1f),
                    },
                    1
                );
            }

            // A thin smoke from the scorch once the lines have cooled.
            Spray(
                backSmoke,
                feet + Vector2.up * 6f,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    From = new Color(0.36f, 0.3f, 0.27f, 0.18f),
                    To = new Color(0.25f, 0.24f, 0.24f, 0.18f),
                    Speed = new Vector2(40f, 90f),
                    Direction = 90f,
                    Spread = 40f,
                    Life = new Vector2(0.9f, 1.4f),
                    Size = new Vector2(120f, 200f),
                    Aspect = 0.5f,
                    Grow = 0.5f,
                    Shrink = 1.6f,
                    Gravity = -40f,
                    Drag = 1.2f,
                    Spin = 20f,
                    Hold = 0.3f,
                    Delay = new Vector2(charge + 0.2f, charge + 0.5f),
                    Area = HotCircleSize * 0.3f,
                },
                4
            );
        }

        /// <summary>The colour of a line heated to <paramref name="heat"/> (0 dark red, 1 yellow; its white-hot core is the shader's).</summary>
        private static Color HeatColor(float heat) => FireColor(Mathf.Lerp(0.62f, 0.12f, heat));

        private static float Sq(float x) => x * x;

        // --- Wind-ups other than a magic circle (doc/features/skill-windups.md) -----------------

        /// <summary>
        /// The wind-up a unique skill's first step of a branch opens with, by the stage's element,
        /// and for one of no element by its user and kind: the fighters (Aria, Luka) rouse
        /// themselves with an aura, the casters (Toma, Mina) charge a magic circle for a heal or
        /// a guard and draw the air in otherwise (doc/features/skill-windups.md#技への割り当て仮設定).
        /// </summary>
        private IEnumerator FirstStepWindUp(RectTransform caster, Color color, CardSkill card)
        {
            if (card == null)
            {
                yield return Charge(caster, color, 0.32f, big: false);
                yield break;
            }
            bool fighter = card.User is CardUser.Aria or CardUser.Luka;
            yield return card.Element switch
            {
                CardElement.Fire => FireCharge(caster, 0.32f),
                CardElement.Ice => ColdFront(caster, 0.32f),
                CardElement.Thunder => StaticCharge(caster, 0.32f),
                CardElement.Blunt => Tremor(caster, 0.35f),
                CardElement.Slash or CardElement.Pierce => BladeGlint(caster, color, 0.3f),
                _ when fighter => Aura(caster, color, 0.35f),
                _ when card.Kind is CardKind.Heal or CardKind.Guard => Charge(
                    caster,
                    color,
                    0.32f,
                    big: false
                ),
                _ => Inhale(caster, color, 0.32f),
            };
        }

        // How fast what is drawn in speeds up as it nears (1/s): the air rushing into a low.
        private const float DrawInPull = 4f;

        /// <summary>
        /// The air and light round the user drawn in to it: streaks of light rush in from all
        /// round, slow at first and faster as they near, dust is pulled along the floor to the
        /// feet, rings close in on the user, and a core of light swells where they meet. All of it
        /// arrives as the charge ends, holds for an instant and is let go with a hot flash.
        /// </summary>
        private IEnumerator Inhale(RectTransform caster, Color color, float seconds)
        {
            if (dimTarget < SpellDim)
                DimTo(SpellDim, 0.15f);
            var center = Center(FrontLayer, caster);
            var feet = Feet(FrontLayer, caster);
            var heart = center + new Vector2(20f, 10f);

            // Streaks of light from all round, each arriving by the end of the charge.
            for (int i = 0; i < 30; i++)
            {
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                var from =
                    heart
                    + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                        * UnityEngine.Random.Range(240f, 380f);
                float travel = UnityEngine.Random.Range(0.52f, 0.68f) * seconds;
                float delay = UnityEngine.Random.Range(0f, seconds - travel);
                DrawIn(
                    from.y < heart.y ? frontLight : backLight,
                    from,
                    heart,
                    color,
                    travel,
                    delay,
                    UnityEngine.Random.Range(6f, 10f)
                );
            }

            // Dust pulled along the floor to the feet, behind them and over them.
            var dust = RichMaterialOf(BattleVfxShape.Smoke);
            for (int i = 0; i < 8; i++)
            {
                float angle = (i + UnityEngine.Random.value * 0.6f) / 8f * Mathf.PI * 2f;
                var from =
                    feet
                    + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * FloorTilt)
                        * UnityEngine.Random.Range(260f, 340f);
                float travel = UnityEngine.Random.Range(0.68f, 0.84f) * seconds;
                float speed = DrawInSpeed(Vector2.Distance(from, feet), travel);
                Spray(
                    Mathf.Sin(angle) < 0f ? frontSmoke : backSmoke,
                    from,
                    new Burst
                    {
                        Shape = BattleVfxShape.Smoke,
                        Material = dust,
                        Bright = 0.45f,
                        From = new Color(KickedDust.r, KickedDust.g, KickedDust.b, 0.4f),
                        To = new Color(KickedDust.r, KickedDust.g, KickedDust.b, 0.4f),
                        Speed = new Vector2(speed, speed),
                        Direction = Angle(feet - from),
                        Life = new Vector2(travel, travel),
                        Size = new Vector2(120f, 170f),
                        Aspect = 0.35f,
                        Grow = 1f,
                        Shrink = 0.35f,
                        Drag = -DrawInPull,
                        Level = true,
                        Hold = 0.7f,
                        Delay = new Vector2(0f, seconds - travel),
                    },
                    1
                );
            }

            // Rings closing in, one on the floor and one round the body, and the core swelling.
            Converge(backLight, feet, color, 520f, seconds * 0.9f, 0f, floor: true);
            Converge(frontLight, heart, color, 360f, seconds * 0.7f, seconds * 0.3f, floor: false);
            var core = Spawn(frontLight, BattleVfxShape.Glow, heart, Vector2.one * 240f, color);
            Animate(
                core,
                seconds + 0.12f,
                (s, k) =>
                {
                    float t = k * (seconds + 0.12f);
                    float grow = t < seconds ? 0.25f + 0.75f * EaseIn(t / seconds) : 1f;
                    s.Holder.localScale = new Vector3(grow, grow, 1f);
                    float alpha = t < seconds ? 0.9f * EaseIn(t / seconds) : 0.9f;
                    Show(s, new Color(color.r, color.g, color.b, alpha), k, 0f, ErodeBright);
                }
            );
            Illuminate(heart, color, 0.6f, 300f, seconds, 0.3f, spill: false);
            yield return Wait(seconds);
            // Held for an instant, all of it gathered, then let go.
            Shake(0.05f, Vector2.zero);
            yield return Wait(0.08f);
            HotFlash(heart, color, 200f);
            Twinkle(heart, color, 220f, 0.3f, 0f, 1f);
        }

        /// <summary>
        /// A ring drawn in to <paramref name="at"/> after <paramref name="delay"/>: it closes from
        /// <paramref name="size"/> to a point over <paramref name="seconds"/>, slowly at first and
        /// fast at the end, brightening as what it draws in thickens; laid on the floor when
        /// <paramref name="floor"/>.
        /// </summary>
        private void Converge(
            RectTransform layer,
            Vector2 at,
            Color color,
            float size,
            float seconds,
            float delay,
            bool floor
        )
        {
            if (layer == null)
                return;
            var shot = Spawn(layer, BattleVfxShape.Ring, at, new Vector2(size, size), Color.clear);
            float total = seconds + delay;
            Animate(
                shot,
                total,
                (s, t) =>
                {
                    float time = t * total - delay;
                    if (time < 0f)
                    {
                        Show(s, Color.clear, 0f, 0f);
                        return;
                    }
                    float k = time / seconds;
                    float grow = Mathf.Lerp(1f, 0.08f, EaseIn(k));
                    s.Holder.localScale = new Vector3(grow, grow * (floor ? FloorTilt : 1f), 1f);
                    // Faint while wide, so it never pops in, and brightest as it closes.
                    var tint = new Color(
                        color.r,
                        color.g,
                        color.b,
                        0.9f * Mathf.SmoothStep(0f, 1f, k)
                    );
                    float eaten = k > 0.85f ? (k - 0.85f) / 0.15f : 0.1f;
                    Show(s, tint, k, eaten, ErodeBright);
                }
            );
        }

        /// <summary>
        /// A streak of light drawn in from <paramref name="from"/> to <paramref name="to"/> after
        /// <paramref name="delay"/>, arriving <paramref name="travel"/> seconds on: slow at first
        /// and faster as it nears, stretched along its flight and brightening.
        /// </summary>
        private void DrawIn(
            RectTransform layer,
            Vector2 from,
            Vector2 to,
            Color color,
            float travel,
            float delay,
            float width
        )
        {
            float speed = DrawInSpeed(Vector2.Distance(from, to), travel);
            Spray(
                layer,
                from,
                new Burst
                {
                    Shape = BattleVfxShape.Streak,
                    Bright = CoreBright,
                    From = new Color(color.r, color.g, color.b, 0.2f),
                    To = Color.Lerp(color, Color.white, 0.6f),
                    Speed = new Vector2(speed, speed),
                    Direction = Angle(to - from),
                    Life = new Vector2(travel, travel),
                    Size = new Vector2(width, width),
                    Shrink = 0.8f,
                    Stretch = 0.025f,
                    Drag = -DrawInPull,
                    Delay = new Vector2(delay, delay),
                },
                1
            );
        }

        // The starting speed that, sped up by DrawInPull, covers distance in travel seconds.
        private static float DrawInSpeed(float distance, float travel) =>
            distance * DrawInPull / (Mathf.Exp(DrawInPull * travel) - 1f);

        /// <summary>
        /// A rising aura: the user's body heats the air round it, and the rising air carries
        /// sparks and dust up so fast that they show as thin lines. Thin lines of light are born
        /// one after another round the body's outline, rush up faster and faster and thin out to
        /// nothing above it, more of them as it builds, with fainter, wider ones rising slower
        /// behind; motes are carried up, wind is pushed out along the floor at the feet as it
        /// bursts on, the body is lit more strongly in pulses, and it flares as it is let go.
        /// Nothing of it stays in place (doc/features/skill-windups.md#立ち上るオーラ).
        /// </summary>
        private IEnumerator Aura(RectTransform caster, Color color, float seconds)
        {
            var center = Center(FrontLayer, caster);
            var feet = Feet(FrontLayer, caster);
            var (min, max) = Bounds(FrontLayer, caster);
            float width = Mathf.Max(80f, max.x - min.x);
            float height = Mathf.Max(120f, max.y - min.y);

            // It bursts on: a small ring along the floor and the wind of it pushing the dust out.
            Ring(backLight, feet, color, 300f, 0.4f, floor: true);
            DustKick(feet, Vector2.right, 100f);
            DustKick(feet, Vector2.left, 100f);
            Shake(0.03f, Vector2.zero);

            // Thin lines, born more often as it builds; one in four passes before the body.
            var hot = Color.Lerp(color, Color.white, 0.5f);
            for (int i = 0; i < 56; i++)
            {
                bool front = i % 4 == 0;
                RiseLine(
                    front ? frontLight : backLight,
                    OnBody(min, width, height, 0.7f),
                    new Color(hot.r, hot.g, hot.b, front ? 0.6f : 1f),
                    color,
                    new Vector2(3f, 5f),
                    new Vector2(500f, 900f),
                    new Vector2(0.2f, 0.35f),
                    seconds * Mathf.Sqrt(UnityEngine.Random.value),
                    CoreBright
                );
            }
            // Fainter, wider ones rising slower behind.
            for (int i = 0; i < 18; i++)
                RiseLine(
                    backLight,
                    OnBody(min, width, height, 0.5f),
                    new Color(color.r, color.g, color.b, 0.3f),
                    color,
                    new Vector2(8f, 12f),
                    new Vector2(250f, 450f),
                    new Vector2(0.35f, 0.5f),
                    seconds * UnityEngine.Random.value,
                    ErodeBright
                );
            Spray(
                frontLight,
                feet + Vector2.up * height * 0.3f,
                new Burst
                {
                    Shape = BattleVfxShape.Mote,
                    Bright = CoreBright,
                    From = hot,
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(40f, 100f),
                    Direction = 90f,
                    Spread = 30f,
                    Life = new Vector2(0.5f, 0.8f),
                    Size = new Vector2(7f, 11f),
                    Gravity = -120f,
                    Area = width * 0.6f,
                    Delay = new Vector2(0f, seconds),
                },
                14
            );

            // The body lit in pulses, stronger each time.
            for (int pulse = 0; pulse < 3; pulse++)
            {
                Illuminate(center, color, 0.35f + 0.15f * pulse, 260f, 0.08f, 0.14f, spill: false);
                yield return Wait(seconds / 3f);
            }
            Illuminate(center, color, 0.9f, 320f, 0.05f, 0.3f, spill: false);
            Twinkle(new Vector2(center.x, max.y), color, 200f, 0.3f, 0f, 1f);
        }

        // A point in or just round a body (its lower `up` part), for something rising off it.
        private static Vector2 OnBody(Vector2 min, float width, float height, float up) =>
            new(
                min.x + width * UnityEngine.Random.Range(-0.1f, 1.1f),
                min.y + height * UnityEngine.Random.Range(0f, up)
            );

        /// <summary>
        /// A thin line of light born at <paramref name="at"/> after <paramref name="delay"/>,
        /// rushing up faster and faster (stretched with its speed, its head bright) and thinning
        /// out to nothing.
        /// </summary>
        private void RiseLine(
            RectTransform layer,
            Vector2 at,
            Color from,
            Color color,
            Vector2 width,
            Vector2 speed,
            Vector2 life,
            float delay,
            float bright
        ) =>
            Spray(
                layer,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Streak,
                    Bright = bright,
                    From = from,
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = speed,
                    Direction = 90f,
                    Spread = 6f,
                    Life = life,
                    Size = width,
                    Shrink = 0.6f,
                    Stretch = 0.08f,
                    Gravity = -900f,
                    Delay = new Vector2(delay, delay),
                },
                1
            );

        /// <summary>
        /// A blade catching the light as the user plants a foot: dust and grit kicked back along
        /// the floor with a small jolt, then a highlight slides along the edge from the hilt to
        /// the tip, where the light catches in a glint. No darkening: it is quick and physical.
        /// </summary>
        private IEnumerator BladeGlint(RectTransform caster, Color color, float seconds)
        {
            var center = Center(FrontLayer, caster);
            var feet = Feet(FrontLayer, caster);
            // The party faces right; the blade is held up and forward of the body.
            var along = new Vector2(0.6f, 0.8f);
            var hand = center + new Vector2(34f, 8f);
            var hilt = hand - along * 30f;
            var tip = hand + along * 56f;

            DustKick(feet, Vector2.left, 110f);
            Spray(
                frontSmoke,
                feet + Vector2.left * 10f,
                new Burst
                {
                    Shape = BattleVfxShape.Chip,
                    From = new Color(0.42f, 0.34f, 0.28f),
                    To = new Color(0.3f, 0.25f, 0.2f, 0f),
                    Speed = new Vector2(220f, 420f),
                    Direction = 150f,
                    Spread = 40f,
                    Life = new Vector2(0.3f, 0.5f),
                    Size = new Vector2(6f, 10f),
                    Gravity = 1600f,
                    Drag = 0.8f,
                    Spin = 540f,
                    Area = 16f,
                },
                6
            );
            Shake(0.04f, Vector2.down);

            // The highlight sliding along the edge.
            var edge = Spawn(frontLight, BattleVfxShape.Streak, hilt, new Vector2(12f, 70f), color);
            edge.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, Angle(along) - 90f);
            const float slideDelay = 0.06f;
            const float slide = 0.14f;
            Animate(
                edge,
                slideDelay + slide,
                (s, k) =>
                {
                    float t = k * (slideDelay + slide) - slideDelay;
                    if (t < 0f)
                    {
                        Show(s, Color.clear, 0f, 0f);
                        return;
                    }
                    float u = t / slide;
                    s.Holder.anchoredPosition = Vector2.Lerp(
                        hilt,
                        tip,
                        Mathf.SmoothStep(0f, 1f, u)
                    );
                    var tint = Color.Lerp(color, Color.white, 0.6f);
                    tint.a = Mathf.Sin(u * Mathf.PI);
                    Show(s, tint, u, 0f, CoreBright);
                }
            );
            Twinkle(tip, color, 160f, 0.28f, slideDelay + slide - 0.02f, 1f);
            Glow(frontLight, tip, color, 120f, 0.3f, 0.7f);
            Illuminate(tip, color, 0.35f, 160f, 0.1f, 0.2f, spill: false);
            yield return Wait(seconds);
        }

        /// <summary>
        /// The cold of an ice spell coming on, as cold air behaves: it is heavier than the air
        /// round it, so it pours down off the user and flows out low along the floor; frost
        /// spreads from the feet, fine ice glints in the air (diamond dust), and a pale light
        /// rises from the frost.
        /// </summary>
        private IEnumerator ColdFront(RectTransform caster, float seconds)
        {
            var color = IceLight;
            if (dimTarget < SpellDim)
                DimTo(SpellDim, 0.15f);
            var center = Center(FrontLayer, caster);
            var feet = Feet(FrontLayer, caster);
            var gas = RichMaterialOf(BattleVfxShape.Smoke);
            var pale = new Color(0.78f, 0.9f, 1f, 0.42f);
            var paler = new Color(0.62f, 0.76f, 0.9f, 0.42f);

            // Flowing out low along the floor, both ways, behind the feet and over them.
            foreach (float side in new[] { 0f, 180f })
                for (int front = 0; front < 2; front++)
                    Spray(
                        front == 0 ? backSmoke : frontSmoke,
                        feet + Vector2.up * (front == 0 ? 6f : -8f),
                        new Burst
                        {
                            Shape = BattleVfxShape.Smoke,
                            Material = gas,
                            Bright = 0.8f,
                            From = pale,
                            To = paler,
                            Speed = new Vector2(40f, 150f),
                            Direction = side,
                            Spread = 36f,
                            Life = new Vector2(0.9f, 1.4f),
                            Size = new Vector2(150f, 250f) * (front == 0 ? 1f : 0.7f),
                            Aspect = 0.32f,
                            Grow = 0.4f,
                            Shrink = 1.8f,
                            Gravity = 15f,
                            Drag = 1.5f,
                            Level = true,
                            Hold = 0.45f,
                            Delay = new Vector2(0f, seconds * 0.6f),
                            Area = 24f,
                        },
                        front == 0 ? 4 : 2
                    );
            // Pouring down off the body to the floor.
            Spray(
                backSmoke,
                center + Vector2.down * 20f,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    Material = gas,
                    Bright = 0.8f,
                    From = new Color(pale.r, pale.g, pale.b, 0.2f),
                    To = new Color(paler.r, paler.g, paler.b, 0.2f),
                    Speed = new Vector2(20f, 60f),
                    Direction = 270f,
                    Spread = 40f,
                    Life = new Vector2(0.6f, 0.9f),
                    Size = new Vector2(80f, 120f),
                    Aspect = 0.6f,
                    Grow = 0.5f,
                    Shrink = 1.5f,
                    Gravity = 80f,
                    Drag = 1f,
                    Hold = 0.4f,
                    Delay = new Vector2(0f, seconds * 0.5f),
                    Area = 30f,
                },
                4
            );

            // Hoarfrost spreading from the feet: fine crystals, not plates of ice.
            if (backSmoke != null)
            {
                var frost = Spawn(
                    backSmoke,
                    BattleVfxShape.Frost,
                    feet,
                    new Vector2(300f, 110f),
                    Color.clear
                );
                float stays = seconds + 1f;
                Animate(
                    frost,
                    stays,
                    (s, k) =>
                    {
                        float t = k * stays;
                        float grow = 0.25f + 0.75f * EaseOut(Mathf.Clamp01(t / (seconds * 0.9f)));
                        s.Holder.localScale = new Vector3(grow, grow, 1f);
                        var tint = new Color(0.85f, 0.95f, 1f, 0.45f);
                        Show(s, tint, k, Eaten(k, (seconds + 0.4f) / stays), 1.2f);
                    }
                );
            }

            // Diamond dust glinting round the body.
            var (min, max) = Bounds(FrontLayer, caster);
            for (int i = 0; i < 14; i++)
            {
                var at = new Vector2(
                    Mathf.Lerp(min.x - 60f, max.x + 60f, UnityEngine.Random.value),
                    Mathf.Lerp(min.y, max.y + 60f, UnityEngine.Random.value)
                );
                Twinkle(
                    at,
                    color,
                    UnityEngine.Random.Range(26f, 44f),
                    UnityEngine.Random.Range(0.18f, 0.26f),
                    UnityEngine.Random.Range(0f, seconds),
                    UnityEngine.Random.value < 0.5f ? 1f : -1f
                );
            }
            Pool(feet, color, 280f, seconds, 0.4f);
            Illuminate(feet + Vector2.down * 60f, color, 0.45f, 240f, seconds, 0.3f, spill: false);
            yield return Wait(seconds);
            Twinkle(center + new Vector2(20f, 30f), color, 200f, 0.3f, 0f, 1f);
        }

        /// <summary>
        /// The user charged with static before a thunder spell, as a strong charge shows: small
        /// discharges crack between points on the body in short flickers, a corona glows at its
        /// highest point and flickers, and light dust is lifted off the floor round the feet.
        /// It is let go in a hot flash and a spray of sparks.
        /// </summary>
        private IEnumerator StaticCharge(RectTransform caster, float seconds)
        {
            var color = Violet;
            if (dimTarget < SpellDim)
                DimTo(SpellDim, 0.15f);
            var center = Center(FrontLayer, caster);
            var feet = Feet(FrontLayer, caster);
            var (min, max) = Bounds(FrontLayer, caster);
            var crown = new Vector2(center.x + 10f, max.y - 6f);

            // The corona at the highest point, flickering for the whole charge.
            Illuminate(crown, color, 0.5f, 200f, seconds, 0.15f, spill: false, flicker: 0.8f);
            Glow(
                frontLight,
                crown,
                Color.Lerp(color, Color.white, 0.4f),
                50f,
                seconds + 0.1f,
                0.5f
            );
            for (int i = 0; i < 4; i++)
                Twinkle(
                    crown + UnityEngine.Random.insideUnitCircle * 10f,
                    Color.Lerp(color, Color.white, 0.5f),
                    UnityEngine.Random.Range(30f, 46f),
                    0.1f,
                    seconds * i / 4f,
                    i % 2 == 0 ? 1f : -1f
                );

            // Dust lifted round the feet.
            Spray(
                backLight,
                feet + Vector2.up * 10f,
                new Burst
                {
                    Shape = BattleVfxShape.Mote,
                    From = new Color(0.75f, 0.7f, 0.85f, 0.6f),
                    To = new Color(0.75f, 0.7f, 0.85f, 0f),
                    Speed = new Vector2(10f, 40f),
                    Direction = 90f,
                    Spread = 40f,
                    Life = new Vector2(0.5f, 0.9f),
                    Size = new Vector2(5f, 8f),
                    Gravity = -60f,
                    Area = 90f,
                    Delay = new Vector2(0f, seconds * 0.6f),
                },
                12
            );

            // Discharges cracking between points on the body, quicker as the charge builds.
            float t = 0f;
            while (t < seconds)
            {
                var from = new Vector2(
                    Mathf.Lerp(min.x, max.x, UnityEngine.Random.value),
                    Mathf.Lerp(min.y + 20f, max.y, UnityEngine.Random.value)
                );
                var to =
                    from
                    + UnityEngine.Random.insideUnitCircle.normalized
                        * UnityEngine.Random.Range(40f, 80f);
                Zap(from, to, Color.Lerp(color, Color.white, 0.3f), 0.08f);
                Sparks(to, color, 2, 0.25f, to - from);
                float gap = Mathf.Lerp(0.12f, 0.04f, t / seconds);
                yield return Wait(gap);
                t += gap;
            }
            HotFlash(center, Color.Lerp(color, Color.white, 0.5f), 200f);
            Sparks(center, color, 10, 0.6f, Vector2.up);
        }

        // The crack's material look that splits the ground open round a point (ShapeRupture).
        private const float RuptureLook = 2f;

        // How wide the ground cracks round the feet, and when it snaps further (s, as parts of
        // the charge): six snaps at uneven gaps, the last the stamp that lets it go. ShapeRupture
        // counts the same six.
        private const float RuptureSize = 520f;
        private static readonly float[] RuptureSnapAt = { 0f, 0.16f, 0.36f, 0.55f, 0.76f, 1f };

        // How long the stretch a snap has just broken catches the light (s).
        private const float RuptureGlint = 0.12f;

        /// <summary>
        /// The ground cracking before an earth skill, as stone and dry earth break round the point
        /// they are stressed at: the floor round the user's feet cracks into pieces, small near
        /// the feet and larger out, in sudden snaps (ぴき) that each reach at once a step further
        /// out, with a tiny jolt and a chip flicked off at each; it is let go with a stamp, a
        /// bigger shake, the cracks reaching their furthest, the pieces near the feet tipping and
        /// a little dust out of them. The ground does not glow: the cracks show as slits by their
        /// shadow and lit far walls (ShapeRupture, doc/features/skill-windups.md#土の前触れ地鳴り).
        /// </summary>
        private IEnumerator Tremor(RectTransform caster, float seconds)
        {
            var feet = Feet(FrontLayer, caster);
            // How many snaps have been made by t (each is at once, with nothing between), and as
            // its fraction how long since the last, for the glint of the stretch it broke.
            float SnapsBy(float t)
            {
                int made = 0;
                while (made < RuptureSnapAt.Length && t >= RuptureSnapAt[made] * seconds)
                    made++;
                if (made == 0)
                    return 0f;
                float since = t - RuptureSnapAt[made - 1] * seconds;
                return made + Mathf.Min(0.99f, since / RuptureGlint);
            }
            if (backSmoke != null)
            {
                var crack = Spawn(
                    backSmoke,
                    BattleVfxShape.Crack,
                    feet,
                    new Vector2(RuptureSize, RuptureSize),
                    Earthen
                );
                crack.Image.material = RichMaterialOf(BattleVfxShape.Crack, RuptureLook);
                // Laid flat but not turned: its far wall is lit up the board, away from the camera.
                crack.Holder.localScale = new Vector3(1f, FloorTilt, 1f);
                float stays = seconds + 1.1f;
                Animate(
                    crack,
                    stays,
                    (s, k) =>
                        Show(s, Earthen, SnapsBy(k * stays), Eaten(k, (seconds + 0.5f) / stays))
                );
            }
            float last = 0f;
            for (int snap = 0; snap < RuptureSnapAt.Length; snap++)
            {
                float at = RuptureSnapAt[snap] * seconds;
                if (at > last)
                    yield return Wait(at - last);
                last = at;
                bool stamp = snap == RuptureSnapAt.Length - 1;
                float reach = RuptureSize * 0.5f * Mathf.Lerp(0.3f, 0.9f, RuptureSnapAt[snap]);
                Shake(stamp ? 0.12f : 0.025f, Vector2.down);
                Hop(feet, reach, stamp ? 8 : 2, stamp ? 1.2f : 0.7f);
                if (stamp)
                {
                    CrackDust(feet, reach, 4, 0.35f);
                    DustKick(feet, Vector2.right, 110f);
                    DustKick(feet, Vector2.left, 110f);
                }
            }
        }

        // The colour of bare earth showing in a crack.
        private static readonly Color Earthen = new(0.5f, 0.4f, 0.3f);

        // Low puffs of dust out of the floor out to reach round the feet, the near ones before the user.
        private void CrackDust(Vector2 feet, float reach, int count, float density)
        {
            var dust = RichMaterialOf(BattleVfxShape.Smoke);
            var color = new Color(KickedDust.r, KickedDust.g, KickedDust.b, density);
            for (int i = 0; i < count; i++)
            {
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                var at =
                    feet
                    + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * FloorTilt)
                        * reach
                        * UnityEngine.Random.Range(0.5f, 1f);
                Spray(
                    Mathf.Sin(angle) < 0f ? frontSmoke : backSmoke,
                    at,
                    new Burst
                    {
                        Shape = BattleVfxShape.Smoke,
                        Material = dust,
                        Bright = 0.45f,
                        From = color,
                        To = color,
                        Speed = new Vector2(30f, 80f),
                        Direction = 90f,
                        Spread = 40f,
                        Life = new Vector2(0.6f, 1f),
                        Size = new Vector2(70f, 120f),
                        Aspect = 0.6f,
                        Grow = 0.4f,
                        Shrink = 1.5f,
                        Gravity = -30f,
                        Drag = 2f,
                        Hold = 0.3f,
                    },
                    1
                );
            }
        }

        // Pebbles hopping off the floor out to reach round the feet, those on the near side
        // before the user.
        private void Hop(Vector2 feet, float reach, int count, float force)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                var at =
                    feet
                    + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * FloorTilt)
                        * reach
                        * UnityEngine.Random.Range(0.2f, 1f);
                Spray(
                    Mathf.Sin(angle) < 0f ? frontSmoke : backSmoke,
                    at,
                    new Burst
                    {
                        Shape = BattleVfxShape.Chip,
                        From = new Color(0.4f, 0.33f, 0.27f),
                        To = new Color(0.3f, 0.25f, 0.2f, 0.6f),
                        Speed = new Vector2(180f, 320f) * force,
                        Direction = 90f,
                        Spread = 30f,
                        Life = new Vector2(0.22f, 0.36f) * force,
                        Size = new Vector2(6f, 12f),
                        Gravity = 1800f,
                        Spin = 360f,
                    },
                    1
                );
            }
        }

        /// <summary>
        /// A charge in steps of <paramref name="step"/> seconds, each stepping its colour up as
        /// heated metal does: dimmer than <paramref name="tint"/>, then the tint, then near white.
        /// At each step a ring closes in on the user, motes are drawn in and the body is lit more
        /// strongly, then the step snaps with a twinkle, sparks, a ring thrown out and a small
        /// jolt, bigger each time.
        /// </summary>
        private IEnumerator Tiered(RectTransform caster, float step, Color tint)
        {
            if (dimTarget < SpellDim)
                DimTo(SpellDim, 0.15f);
            var center = Center(FrontLayer, caster);
            var feet = Feet(FrontLayer, caster);
            var colors = new[]
            {
                new Color(tint.r * 0.7f, tint.g * 0.55f, tint.b * 0.55f),
                tint,
                Color.Lerp(tint, Color.white, 0.65f),
            };
            for (int level = 0; level < colors.Length; level++)
            {
                var color = colors[level];
                float grow = 1f + 0.35f * level;
                Converge(frontLight, center, color, 320f * grow, step * 0.8f, 0f, floor: false);
                Gather(center, color, 8 + 6 * level, 200f * grow, step * 0.8f);
                Pool(feet, color, 220f * grow, step * 0.8f, 0.2f);
                Illuminate(
                    center,
                    color,
                    0.3f + 0.25f * level,
                    220f + 60f * level,
                    step * 0.8f,
                    0.2f,
                    spill: false
                );
                yield return Wait(step * 0.85f);
                Twinkle(
                    center + new Vector2(16f, 24f),
                    color,
                    (140f + 60f * level),
                    0.26f,
                    0f,
                    level % 2 == 0 ? 1f : -1f
                );
                Sparks(center, color, 6 + 4 * level, 0.4f + 0.15f * level, Vector2.up);
                Ring(frontLight, center, color, 200f + 90f * level, 0.25f, floor: false);
                Shake(0.03f + 0.03f * level, Vector2.zero);
                yield return Wait(step * 0.15f);
            }
        }
    }
}
