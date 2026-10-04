using System;
using System.Collections;
using System.Collections.Generic;
using Baryonyx.Combat;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// One landing of a card's effect: on whom it lands, which side that is, and in which wave
    /// (a card's actions land wave by wave: a second action, or a second round of hits, is a new
    /// wave).
    /// </summary>
    public readonly struct BattleSkillHit
    {
        public readonly RectTransform Area;
        public readonly bool Ally;
        public readonly int Wave;

        public BattleSkillHit(RectTransform area, bool ally, int wave)
        {
            Area = area;
            Ally = ally;
            Wave = wave;
        }
    }

    /// <summary>
    /// The effects of every card skill (doc/catalog/skills/README.md), each its own: a feeling put
    /// into shapes, colours and timing, in three beats (wind-up, climax, fall-off), built from the
    /// shapes worked out by the shader. The first six cards keep their own effects; the others are
    /// below, by user. Statuses that act later (a burn, a sigil, a thundercloud) have their own.
    /// </summary>
    public sealed partial class BattleSkillVfx
    {
        // The colours of the new elements' light: earth for blunt blows, a cold teal for piercing.
        private static readonly Color Earth = new(1f, 0.7f, 0.32f);
        private static readonly Color Teal = new(0.42f, 1f, 0.82f);
        private static readonly Color Gold = new(1f, 0.86f, 0.42f);
        private static readonly Color Violet = new(0.72f, 0.48f, 1f);
        private static readonly Color Venom = new(0.62f, 1f, 0.32f);
        private static readonly Color Blood = new(1f, 0.22f, 0.2f);

        /// <summary>The colour of a card's light (its cut-in, its glow), by the card's id.</summary>
        public static Color ColorOf(string id) =>
            id switch
            {
                "Slash" => ColorOf(BattleSkillVfxKind.Slash),
                "Fire"
                or "Embers"
                or "FlamePillar"
                or "FireStorm"
                or "BlastSigil"
                or "Meteor"
                or "FlameEnchant" => ColorOf(BattleSkillVfxKind.Fire),
                "Ice" or "Icicles" or "Blizzard" or "IceMirror" or "AbsoluteZero" => ColorOf(
                    BattleSkillVfxKind.Ice
                ),
                "Thunder"
                or "LightningBolt"
                or "ChainLightning"
                or "Thundercloud"
                or "ThunderSpear"
                or "Gale" => ColorOf(BattleSkillVfxKind.Thunder),
                "Heal" or "StepPrayer" or "Regen" => ColorOf(BattleSkillVfxKind.Heal),
                "Guard" or "Protect" or "ShieldBash" => ColorOf(BattleSkillVfxKind.Guard),
                "Whirlwind" or "Iai" or "BladeDance" or "Hone" => new Color(0.78f, 0.9f, 1f),
                "ArmorBreak" or "EarthSplitter" => Earth,
                "GiantImpact"
                or "StrideStrike"
                or "HolyHammer"
                or "Revelation"
                or "DivineShield"
                or "GuardianOath"
                or "WarCry" => Gold,
                "VitalThrust" => Blood,
                "PoisonNeedle" => Venom,
                "ShadowStitch" or "ShadowSnipe" => Violet,
                "InsightArrow" or "ArrowRain" or "Scout" => Teal,
                "QuickCast" or "ManaPrayer" => new Color(0.5f, 0.66f, 1f),
                "Resurrection" or "HolyLight" or "Purify" => new Color(1f, 0.96f, 0.8f),
                _ => ColorOf(BattleSkillVfxKind.Slash),
            };

        /// <summary>
        /// Plays the card <paramref name="id"/> from <paramref name="caster"/> on its
        /// <paramref name="hits"/>. <paramref name="land"/> is called once for each hit (its index)
        /// as the effect lands there and tells how hard it hit; any hit the effect does not reach
        /// is landed at its end. Ends once the last blow has landed and its burst has spread.
        /// </summary>
        public IEnumerator PlayCard(
            string id,
            string skill,
            RectTransform caster,
            Texture casterArt,
            bool cutIn,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var landed = new bool[hits.Count];
            BattleHitWeight Once(int index)
            {
                if (index < 0 || index >= landed.Length || landed[index])
                    return BattleHitWeight.Normal;
                landed[index] = true;
                return land(index);
            }
            if (cutIn)
                yield return PlayCutIn(casterArt, skill, ColorOf(id));
            var areas = new List<RectTransform>();
            foreach (var hit in hits)
                areas.Add(hit.Area);
            yield return id switch
            {
                "Slash" => Slash(caster, areas, Once),
                "Fire" => Fire(caster, areas, Once),
                "Ice" => Ice(caster, areas, Once, cutIn),
                "Thunder" => Thunder(caster, areas, Once, cutIn),
                "Heal" => Heal(caster, areas, Once),
                "Guard" => Guard(caster, areas, Once),
                "Whirlwind" => Whirlwind(caster, hits, Once),
                "Iai" => Iai(caster, hits, Once),
                "BladeDance" => BladeDance(caster, hits, Once),
                "Hone" => Hone(caster, hits, Once),
                "ShieldBash" => ShieldBash(caster, hits, Once),
                "ArmorBreak" => ArmorBreak(caster, hits, Once),
                "EarthSplitter" => EarthSplitter(caster, hits, Once),
                "GiantImpact" => GiantImpact(caster, hits, Once),
                "StrideStrike" => StrideStrike(caster, hits, Once),
                "GuardianOath" => GuardianOath(caster, hits, Once),
                "WarCry" => WarCry(caster, hits, Once),
                "Embers" => Embers(caster, hits, Once),
                "FlamePillar" => FlamePillar(caster, hits, Once),
                "FireStorm" => FireStorm(caster, hits, Once),
                "BlastSigil" => BlastSigil(caster, hits, Once),
                "Meteor" => Meteor(caster, hits, Once),
                "FlameEnchant" => FlameEnchant(caster, hits, Once),
                "Icicles" => Icicles(caster, hits, Once),
                "Blizzard" => Blizzard(caster, hits, Once),
                "IceMirror" => IceMirror(caster, hits, Once),
                "AbsoluteZero" => AbsoluteZero(caster, hits, Once),
                "QuickCast" => QuickCast(caster, hits, Once),
                "VitalThrust" => VitalThrust(caster, hits, Once),
                "PoisonNeedle" => PoisonNeedle(caster, hits, Once),
                "ShadowStitch" => ShadowStitch(caster, hits, Once),
                "InsightArrow" => InsightArrow(caster, hits, Once),
                "ArrowRain" => ArrowRain(caster, hits, Once),
                "ShadowSnipe" => ShadowSnipe(caster, hits, Once),
                "LightningBolt" => LightningBolt(caster, hits, Once),
                "ChainLightning" => ChainLightning(caster, hits, Once),
                "Thundercloud" => Thundercloud(caster, hits, Once),
                "ThunderSpear" => ThunderSpear(caster, hits, Once),
                "Gale" => Gale(caster, hits, Once),
                "Scout" => Scout(caster, hits, Once),
                "StepPrayer" => StepPrayer(caster, hits, Once),
                "Regen" => Regen(caster, hits, Once),
                "Resurrection" => Resurrection(caster, hits, Once),
                "HolyHammer" => HolyHammer(caster, hits, Once),
                "Protect" => Protect(caster, hits, Once),
                "Purify" => Purify(caster, hits, Once),
                "ManaPrayer" => ManaPrayer(caster, hits, Once),
                "Revelation" => Revelation(caster, hits, Once),
                "HolyLight" => HolyLight(caster, hits, Once),
                "DivineShield" => DivineShield(caster, hits, Once),
                _ => Slash(caster, areas, Once),
            };
            for (int i = 0; i < landed.Length; i++)
                Once(i);
            DimTo(0f, 0.4f);
        }

        // --- Aria ------------------------------------------------------------------------------

        /// <summary>
        /// 旋風斬, sweeping: a silver whirlwind rises at the near end of the enemies and runs
        /// through them to the far end; each is cut by two arcs as it passes, sparks fly, and red
        /// drops fall as they bleed. Cut leaves spin away in the wind behind it.
        /// </summary>
        private IEnumerator Whirlwind(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("Whirlwind");
            var from = Center(FrontLayer, caster);
            DimTo(0.42f, 0.15f);
            Star(from + new Vector2(30f, 40f), Color.white, 150f, 0.18f);
            Ring(backLight, Feet(FrontLayer, caster), color, 300f, 0.35f, floor: true);
            yield return Wait(0.16f);

            var wave = Wave(hits, 0);
            wave.Sort(
                (a, b) =>
                    Center(FrontLayer, hits[a].Area).x.CompareTo(Center(FrontLayer, hits[b].Area).x)
            );
            var floorY = MidFeet(hits, 0).y;
            float start = wave.Count > 0 ? Center(FrontLayer, hits[wave[0]].Area).x - 160f : 300f;
            float end = wave.Count > 0 ? Center(FrontLayer, hits[wave[^1]].Area).x + 160f : 800f;
            const float run = 0.62f;
            var tornado = Spawn(
                backLight,
                BattleVfxShape.Tornado,
                new Vector2(start, floorY),
                new Vector2(380f, 560f),
                color,
                new Vector2(0.5f, 0.05f)
            );
            Animate(
                tornado,
                run + 0.35f,
                (s, k) =>
                {
                    float t = k * (run + 0.35f);
                    float move = EaseOut(Mathf.Clamp01(t / run));
                    s.Holder.anchoredPosition = new Vector2(Mathf.Lerp(start, end, move), floorY);
                    float grow = EaseOut(Mathf.Min(1f, t / 0.12f));
                    s.Holder.localScale = new Vector3(0.6f + 0.4f * grow, grow, 1f);
                    Show(
                        s,
                        color,
                        Mathf.Min(1f, t / run),
                        Eaten(t / (run + 0.35f), 0.6f),
                        ErodeBright
                    );
                }
            );
            // The wind's leaves and streaks swirl along with it.
            for (int n = 0; n < 4; n++)
                Spray(
                    frontLight,
                    new Vector2(start + n * 60f, floorY + 160f),
                    new Burst
                    {
                        Shape = BattleVfxShape.Streak,
                        Bright = CoreBright,
                        From = Color.white,
                        To = new Color(color.r, color.g, color.b, 0f),
                        Speed = new Vector2(700f, 1200f),
                        Direction = 10f,
                        Spread = 50f,
                        Life = new Vector2(0.2f, 0.4f),
                        Size = new Vector2(8f, 12f),
                        Stretch = 0.05f,
                        Drag = 2f,
                        Delay = new Vector2(n * 0.1f, n * 0.1f + 0.08f),
                        Area = 90f,
                    },
                    6
                );

            float last = 0f;
            foreach (int index in wave)
            {
                var at = Center(FrontLayer, hits[index].Area);
                float when = Mathf.InverseLerp(start, end, at.x);
                // Reached by the whirlwind's ease-out: solve for the time it gets there.
                float reach = run * (1f - Mathf.Pow(1f - Mathf.Clamp01(when), 1f / 3f));
                yield return Wait(Mathf.Max(0f, reach - last));
                last = reach;
                Arc(backLight, at, 30f, false, color, 0.9f);
                Arc(frontLight, at + new Vector2(0f, -10f), -20f, true, color, 0.8f);
                Sparks(at, color, 10, 0.8f, Vector2.right);
                var weight = land(index);
                Impact(
                    hits[index].Area,
                    at,
                    color,
                    weight,
                    0.45f,
                    Vector2.right,
                    flashScreen: false
                );
                Drops(at, Blood, 8);
            }
            Leaves(
                frontLight,
                new Vector2(end - 100f, floorY + 200f),
                new Color(0.75f, 0.85f, 0.7f),
                8,
                0f,
                260f
            );
            yield return Wait(0.28f);
        }

        /// <summary>
        /// 居合一閃, stillness then a single instant: the stage goes dark and still, a glint runs
        /// along the sheathed blade, then one thin line of white light cuts across the whole screen
        /// through the enemy. A beat later the cut opens: a flat cross flares, the time stops
        /// long, and shards of light fall as the line wears away from its ends.
        /// </summary>
        private IEnumerator Iai(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("Iai");
            var from = Center(FrontLayer, caster);
            DimTo(0.7f, 0.2f);
            Twinkle(from + new Vector2(50f, 20f), color, 120f, 0.4f, 0.12f, 1f);
            yield return Wait(0.42f);
            foreach (int index in Wave(hits, 0))
            {
                var at = Center(FrontLayer, hits[index].Area);
                float y = at.y;
                // The cut: a hair-thin line across the whole battlefield, at the enemy's height.
                Line(
                    frontLight,
                    new Vector2(-1100f, y + 30f),
                    new Vector2(1100f, y - 30f),
                    Color.white,
                    26f,
                    0.55f,
                    0.35f
                );
                Line(
                    backLight,
                    new Vector2(-1100f, y + 30f),
                    new Vector2(1100f, y - 30f),
                    color,
                    90f,
                    0.6f,
                    0.3f
                );
                Shake(0.12f, Vector2.right);
                yield return Wait(0.12f);
                var weight = land(index);
                var cross = Spawn(
                    frontLight,
                    BattleVfxShape.SlashCross,
                    at,
                    new Vector2(620f, 220f),
                    Color.white
                );
                cross.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, -3f);
                Animate(
                    cross,
                    0.4f,
                    (s, k) =>
                    {
                        float grow = 0.6f + 0.5f * EaseOut(Mathf.Min(1f, k * 3f));
                        s.Holder.localScale = new Vector3(grow, grow, 1f);
                        Show(s, Color.Lerp(Color.white, color, k), k, Eaten(k, 0.2f), CoreBright);
                    }
                );
                Impact(hits[index].Area, at, color, weight, 1f, Vector2.right, flashScreen: true);
                HitStop(0.16f);
                Punch(at, 0.03f);
                Spray(
                    frontLight,
                    at,
                    new Burst
                    {
                        Shape = BattleVfxShape.Shard,
                        Bright = ErodeBright,
                        From = Color.white,
                        To = new Color(color.r, color.g, color.b, 0f),
                        Speed = new Vector2(80f, 300f),
                        Direction = 90f,
                        Spread = 160f,
                        Life = new Vector2(0.5f, 0.9f),
                        Size = new Vector2(10f, 20f),
                        Aspect = 2.4f,
                        Gravity = 600f,
                        Drag = 1.5f,
                        Spin = 300f,
                        Area = 120f,
                        Delay = new Vector2(0.05f, 0.2f),
                    },
                    18
                );
            }
            yield return Wait(0.4f);
        }

        /// <summary>
        /// 千刃乱舞, a storm of blades: blades of light gather and circle over the user, then fly
        /// one after another from every side to the enemies picked, each cutting an arc; the last
        /// lands with a great cross and a ring.
        /// </summary>
        private IEnumerator BladeDance(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("BladeDance");
            var from = Center(FrontLayer, caster) + new Vector2(0f, 120f);
            DimTo(0.55f, 0.15f);
            for (int n = 0; n < 8; n++)
            {
                float a = n * 45f;
                var shot = Spawn(
                    frontLight,
                    BattleVfxShape.Shard,
                    from,
                    new Vector2(18f, 60f),
                    Color.white
                );
                float phase = a;
                Animate(
                    shot,
                    0.5f,
                    (s, k) =>
                    {
                        float angle = (phase + k * 300f) * Mathf.Deg2Rad;
                        float radius = 110f * EaseOut(Mathf.Min(1f, k * 3f));
                        s.Holder.anchoredPosition =
                            from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.5f) * radius;
                        s.Image.rectTransform.localEulerAngles = new Vector3(
                            0f,
                            0f,
                            angle * Mathf.Rad2Deg
                        );
                        Show(
                            s,
                            Color.Lerp(Color.white, color, 0.4f),
                            k,
                            Eaten(k, 0.85f),
                            CoreBright
                        );
                    }
                );
            }
            Glow(frontLight, from, color, 300f, 0.55f, 0.5f);
            yield return Wait(0.45f);

            var wave = Wave(hits, 0);
            for (int n = 0; n < wave.Count; n++)
            {
                int index = wave[n];
                var at =
                    Center(FrontLayer, hits[index].Area)
                    + UnityEngine.Random.insideUnitCircle * 30f;
                float side = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                var start = at + new Vector2(Mathf.Cos(side), Mathf.Sin(side)) * 520f;
                StartCoroutine(
                    Shoot(
                        BattleVfxShape.Shard,
                        start,
                        at,
                        new Vector2(40f, 120f),
                        new Vector2(0.5f, 0.9f),
                        0.09f,
                        0f,
                        Color.white,
                        -90f,
                        null
                    )
                );
                yield return Wait(0.09f);
                Arc(frontLight, at, UnityEngine.Random.Range(-60f, 60f), n % 2 == 1, color, 0.7f);
                Sparks(at, color, 8, 0.7f, (at - start).normalized);
                var weight = land(index);
                bool final = n == wave.Count - 1;
                if (final)
                {
                    Cross(at, color, BattleHitWeight.Weak);
                    Impact(
                        hits[index].Area,
                        at,
                        color,
                        weight,
                        1f,
                        Vector2.right,
                        flashScreen: true
                    );
                    Ring(
                        backLight,
                        Feet(FrontLayer, hits[index].Area),
                        color,
                        700f,
                        0.5f,
                        floor: true
                    );
                }
                else
                {
                    Star(at, color, 180f, 0.16f);
                    HitStop(Weighted(0.03f, weight));
                    Shake(Weighted(0.16f, weight), (at - start).normalized);
                    Illuminate(at, color, 0.5f, 260f, 0.02f, 0.18f, spill: false);
                }
                yield return Wait(0.04f);
            }
            yield return Wait(0.3f);
        }

        /// <summary>
        /// 研ぎ澄ます, a clean ring of steel: whetstone sparks spray down from the user's blade, a
        /// line of light runs up its edge, a twinkle flashes at the tip, and a silver arrow
        /// pointing down (the cost falling) sinks over the user.
        /// </summary>
        private IEnumerator Hone(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("Hone");
            var at = Center(FrontLayer, caster);
            DimTo(0.3f, 0.15f);
            var edgeFrom = at + new Vector2(-30f, -40f);
            var edgeTo = at + new Vector2(90f, 90f);
            Spray(
                frontLight,
                edgeFrom,
                new Burst
                {
                    Shape = BattleVfxShape.Streak,
                    Bright = CoreBright,
                    From = new Color(1f, 0.9f, 0.6f),
                    To = new Color(1f, 0.5f, 0.15f, 0f),
                    Speed = new Vector2(300f, 700f),
                    Direction = -60f,
                    Spread = 70f,
                    Life = new Vector2(0.15f, 0.35f),
                    Size = new Vector2(6f, 10f),
                    Stretch = 0.05f,
                    Gravity = 900f,
                    Drag = 2f,
                    Delay = new Vector2(0f, 0.3f),
                    Area = 12f,
                },
                22
            );
            yield return Wait(0.2f);
            Line(frontLight, edgeFrom, edgeTo, color, 30f, 0.4f, 0.4f);
            yield return Wait(0.12f);
            Twinkle(edgeTo, Color.white, 150f, 0.45f, 0f, 1f);
            Illuminate(edgeTo, color, 0.6f, 240f, 0.05f, 0.3f, spill: false);
            Bloom(
                frontLight,
                BattleVfxShape.Chevron,
                at + new Vector2(0f, 150f),
                new Vector2(120f, 160f),
                color,
                0.7f,
                0.6f,
                ErodeBright,
                angle: 180f
            );
            foreach (int index in Wave(hits, 0))
                land(index);
            yield return Wait(0.35f);
        }

        /// <summary>
        /// シールドバッシュ, a shove: a blue crest flashes up before the user and is driven straight
        /// into the enemy; it hits with a ring of shock facing out, a burst and scraps flying, and
        /// then small crests pop over every ally as the party takes the block.
        /// </summary>
        private IEnumerator ShieldBash(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("ShieldBash");
            var from = Center(FrontLayer, caster) + new Vector2(70f, 0f);
            DimTo(0.38f, 0.12f);
            Bloom(
                frontLight,
                BattleVfxShape.Crest,
                from,
                new Vector2(150f, 180f),
                color,
                0.22f,
                0.9f,
                ErodeBright
            );
            yield return Wait(0.16f);
            foreach (int index in Wave(hits, 0))
            {
                var at = Center(FrontLayer, hits[index].Area);
                yield return Shoot(
                    BattleVfxShape.Crest,
                    from,
                    at - new Vector2(40f, 0f),
                    new Vector2(170f, 200f),
                    new Vector2(0.5f, 0.5f),
                    0.14f,
                    0f,
                    color,
                    0f,
                    null,
                    turn: false
                );
                var weight = land(index);
                var ring = Spawn(
                    frontLight,
                    BattleVfxShape.Ring,
                    at - new Vector2(30f, 0f),
                    new Vector2(360f, 360f),
                    color
                );
                Animate(
                    ring,
                    0.35f,
                    (s, k) =>
                    {
                        s.Holder.localScale = new Vector3(
                            RingScale(k, false).x * 0.45f,
                            RingScale(k, false).y,
                            1f
                        );
                        Show(s, color, k, k);
                    }
                );
                Impact(
                    hits[index].Area,
                    at,
                    color,
                    weight,
                    0.7f,
                    Vector2.right,
                    flashScreen: false
                );
                Debris(at, new Color(0.55f, 0.6f, 0.7f), 10, Vector2.right);
            }
            yield return Wait(0.2f);
            foreach (int index in Wave(hits, 1))
            {
                var at = Center(FrontLayer, hits[index].Area);
                Bloom(
                    frontLight,
                    BattleVfxShape.Crest,
                    at + new Vector2(0f, 20f),
                    new Vector2(90f, 110f),
                    color,
                    0.6f,
                    0.55f,
                    ErodeBright,
                    delay: 0.05f
                );
                land(index);
            }
            yield return Wait(0.4f);
        }

        /// <summary>
        /// 鎧砕き, a crunch: a heavy blow comes down on the enemy, cracks of light split across its
        /// body, grey scraps of armour burst out and fall, and a purple arrow sinks over it (its
        /// defence falling).
        /// </summary>
        private IEnumerator ArmorBreak(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("ArmorBreak");
            DimTo(0.4f, 0.12f);
            Star(Center(FrontLayer, caster) + new Vector2(40f, 60f), Color.white, 140f, 0.16f);
            yield return Wait(0.14f);
            foreach (int index in Wave(hits, 0))
            {
                var at = Center(FrontLayer, hits[index].Area);
                Spray(
                    frontLight,
                    at + new Vector2(-40f, 160f),
                    new Burst
                    {
                        Shape = BattleVfxShape.Streak,
                        Bright = CoreBright,
                        From = Color.white,
                        To = new Color(color.r, color.g, color.b, 0f),
                        Speed = new Vector2(1600f, 2200f),
                        Direction = -75f,
                        Spread = 8f,
                        Life = new Vector2(0.06f, 0.1f),
                        Size = new Vector2(10f, 16f),
                        Stretch = 0.05f,
                        Area = 40f,
                    },
                    6
                );
                yield return Wait(0.07f);
                var weight = land(index);
                Bloom(
                    frontLight,
                    BattleVfxShape.Crack,
                    at,
                    new Vector2(300f, 300f),
                    color,
                    0.55f,
                    0.45f,
                    CoreBright
                );
                Impact(
                    hits[index].Area,
                    at,
                    color,
                    weight,
                    0.75f,
                    Vector2.down,
                    flashScreen: false
                );
                Debris(at, new Color(0.62f, 0.64f, 0.7f), 16, Vector2.up);
                Bloom(
                    frontLight,
                    BattleVfxShape.Chevron,
                    at + new Vector2(0f, 120f),
                    new Vector2(110f, 150f),
                    Violet,
                    0.75f,
                    0.6f,
                    ErodeBright,
                    angle: 180f,
                    delay: 0.15f
                );
            }
            yield return Wait(0.45f);
        }

        /// <summary>
        /// 大地割り, a rumble: the user's blow strikes the floor with a ring of shock, glowing cracks
        /// run along the ground toward the enemies, and the ground bursts up under each one in
        /// rocks and dust, the stage shaking hard.
        /// </summary>
        private IEnumerator EarthSplitter(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("EarthSplitter");
            var feet = Feet(FrontLayer, caster) + new Vector2(80f, 0f);
            DimTo(0.42f, 0.15f);
            Glow(
                frontLight,
                Center(FrontLayer, caster) + new Vector2(30f, 120f),
                color,
                240f,
                0.3f,
                0.6f
            );
            yield return Wait(0.22f);
            Ring(backLight, feet, color, 500f, 0.45f, floor: true);
            Shake(0.45f, Vector2.down);
            HitStop(0.05f);
            Puffs(backSmoke, feet, new Color(0.45f, 0.38f, 0.3f, 0.7f), 4, 60f, 1f);
            var wave = Wave(hits, 0);
            var goal = MidFeet(hits, 0);
            // Cracks in steps along the floor from the user to the enemies.
            for (int n = 1; n <= 4; n++)
            {
                var step = Vector2.Lerp(feet, goal, n / 4f);
                var crack = Spawn(
                    backSmoke,
                    BattleVfxShape.Crack,
                    step,
                    new Vector2(320f, 320f),
                    color
                );
                crack.Holder.localScale = new Vector3(1f, FloorTilt, 1f);
                Animate(
                    crack,
                    0.9f,
                    (s, k) => Show(s, color, Mathf.Min(1f, k * 2f), Eaten(k, 0.4f), CoreBright)
                );
                yield return Wait(0.05f);
            }
            foreach (int index in wave)
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var under = Feet(FrontLayer, area);
                var weight = land(index);
                Spray(
                    frontLight,
                    under,
                    new Burst
                    {
                        Shape = BattleVfxShape.Chip,
                        From = new Color(0.42f, 0.32f, 0.24f),
                        To = new Color(0.3f, 0.24f, 0.2f, 0f),
                        Speed = new Vector2(400f, 900f),
                        Direction = 90f,
                        Spread = 50f,
                        Life = new Vector2(0.6f, 1f),
                        Size = new Vector2(14f, 30f),
                        Gravity = 1800f,
                        Drag = 0.5f,
                        Spin = 600f,
                        Area = 50f,
                    },
                    14
                );
                Spray(
                    backSmoke,
                    under + Vector2.up * 20f,
                    new Burst
                    {
                        Shape = BattleVfxShape.Smoke,
                        From = new Color(0.5f, 0.42f, 0.34f, 0.8f),
                        To = new Color(0.4f, 0.36f, 0.33f, 0.8f),
                        Speed = new Vector2(80f, 260f),
                        Direction = 90f,
                        Spread = 70f,
                        Life = new Vector2(0.8f, 1.3f),
                        Size = new Vector2(120f, 200f),
                        Grow = 0.4f,
                        Shrink = 1.8f,
                        Gravity = -40f,
                        Drag = 1.5f,
                        Spin = 20f,
                        Hold = 0.3f,
                        Area = 50f,
                    },
                    5
                );
                Impact(area, at, color, weight, 0.7f, Vector2.up, flashScreen: false, rays: false);
                Pool(under, color, 380f, 0.1f, 0.5f);
                yield return Wait(0.06f);
            }
            Shake(0.35f, Vector2.up);
            yield return Wait(0.4f);
        }

        /// <summary>
        /// ギガントインパクト, crushing weight: the sky darkens and a great golden light gathers high
        /// over the enemy, then a column of light slams down on it like a giant's fist; the floor
        /// cracks and rings twice, dust and rocks fly both ways, and the time stops and slows.
        /// </summary>
        private IEnumerator GiantImpact(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("GiantImpact");
            DimTo(NightDim, 0.2f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var feet = Feet(FrontLayer, area);
                var above = new Vector2(at.x, FrontLayer.rect.yMax - 120f);
                Glow(frontLight, above, color, 520f, 0.6f, 0.9f);
                Gather(above, color, 24, 280f, 0.5f);
                Shake(0.1f, Vector2.zero);
                yield return Wait(0.5f);
                Column(
                    frontLight,
                    feet,
                    color,
                    new Vector2(460f, (above.y - feet.y) / 0.9f + 80f),
                    0.75f,
                    down: true,
                    fast: true
                );
                yield return Wait(0.08f);
                var weight = land(index);
                Bloom(
                    backSmoke,
                    BattleVfxShape.Crack,
                    feet,
                    new Vector2(560f, 560f),
                    color,
                    1.2f,
                    0.5f,
                    CoreBright,
                    floor: true
                );
                Ring(backLight, feet, Color.white, 600f, 0.4f, floor: true);
                Ring(backLight, feet, color, 900f, 0.6f, floor: true);
                Impact(area, at, color, Weighted2(weight), 1f, Vector2.down, flashScreen: true);
                HitStop(0.16f);
                SlowMotion(0.4f, 0.25f);
                Punch(at, 0.045f);
                Debris(feet + Vector2.up * 20f, new Color(0.4f, 0.32f, 0.25f), 20, Vector2.up);
                foreach (float side in new[] { 0f, 180f })
                    Spray(
                        backSmoke,
                        feet + Vector2.up * 10f,
                        new Burst
                        {
                            Shape = BattleVfxShape.Smoke,
                            From = new Color(0.55f, 0.48f, 0.4f, 0.75f),
                            To = new Color(0.42f, 0.4f, 0.38f, 0.75f),
                            Speed = new Vector2(300f, 700f),
                            Direction = side,
                            Spread = 18f,
                            Life = new Vector2(0.8f, 1.3f),
                            Size = new Vector2(130f, 200f),
                            Aspect = 0.45f,
                            Grow = 0.4f,
                            Shrink = 1.9f,
                            Drag = 3f,
                            Gravity = -15f,
                            Spin = 10f,
                            Hold = 0.25f,
                            Area = 30f,
                        },
                        5
                    );
            }
            yield return Wait(0.5f);
        }

        /// <summary>
        /// 健脚の一撃, brisk: golden footprints light up on the floor step by step from the user to
        /// the enemy, a golden streak dashes along them, and the kick bursts in gold; motes of the
        /// day's walk rise from the footprints after.
        /// </summary>
        private IEnumerator StrideStrike(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("StrideStrike");
            var from = Feet(FrontLayer, caster);
            DimTo(0.36f, 0.15f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var to = Feet(FrontLayer, area) - new Vector2(80f, 0f);
                var prints = Footsteps(from, to, color, 6, 0.06f);
                yield return Wait(0.36f);
                StartCoroutine(
                    Shoot(
                        BattleVfxShape.Streak,
                        from + Vector2.up * 60f,
                        at,
                        new Vector2(60f, 220f),
                        new Vector2(0.5f, 0.85f),
                        0.12f,
                        30f,
                        color,
                        -90f,
                        null
                    )
                );
                yield return Wait(0.12f);
                var weight = land(index);
                Star(at, color, Weighted(320f, weight), 0.22f);
                var ring = Spawn(
                    frontLight,
                    BattleVfxShape.Ring,
                    at,
                    new Vector2(300f, 300f),
                    color
                );
                Animate(
                    ring,
                    0.3f,
                    (s, k) =>
                    {
                        s.Holder.localScale = new Vector3(
                            RingScale(k, false).x * 0.5f,
                            RingScale(k, false).y,
                            1f
                        );
                        Show(s, color, k, k);
                    }
                );
                Impact(
                    area,
                    at,
                    color,
                    weight,
                    0.7f,
                    Vector2.right,
                    flashScreen: false,
                    rays: false
                );
                foreach (var print in prints)
                    Spray(
                        frontLight,
                        print,
                        new Burst
                        {
                            Shape = BattleVfxShape.Sparkle,
                            From = Color.white,
                            To = new Color(color.r, color.g, color.b, 0f),
                            Speed = new Vector2(40f, 120f),
                            Direction = 90f,
                            Spread = 40f,
                            Life = new Vector2(0.5f, 0.9f),
                            Size = new Vector2(14f, 24f),
                            Gravity = -100f,
                            Spin = 120f,
                            Area = 14f,
                            Delay = new Vector2(0.05f, 0.25f),
                        },
                        2
                    );
            }
            yield return Wait(0.45f);
        }

        /// <summary>
        /// 守護の誓い, steadfast: a great red-gold crest rises behind the user, a ring spreads at its
        /// feet, and a red wave of challenge rolls out to the enemies, lighting each of them red
        /// for a moment.
        /// </summary>
        private IEnumerator GuardianOath(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("GuardianOath");
            var at = Center(FrontLayer, caster);
            var feet = Feet(FrontLayer, caster);
            DimTo(0.42f, 0.18f);
            yield return Charge(caster, color, 0.2f, big: false);
            Bloom(
                backLight,
                BattleVfxShape.Crest,
                at + new Vector2(0f, 30f),
                new Vector2(240f, 300f),
                new Color(1f, 0.55f, 0.35f),
                0.9f,
                0.65f,
                CoreBright
            );
            Ring(backLight, feet, color, 380f, 0.5f, floor: true);
            Pool(feet, color, 340f, 0.3f, 0.5f);
            Shake(0.12f, Vector2.zero);
            foreach (int index in Wave(hits, 0))
                land(index);
            yield return Wait(0.2f);
            // The challenge: a red wave rolling to the right.
            var wave = Spawn(frontLight, BattleVfxShape.Ring, at, new Vector2(400f, 400f), Blood);
            Animate(
                wave,
                0.6f,
                (s, k) =>
                {
                    s.Holder.anchoredPosition = Vector2.Lerp(
                        at,
                        at + new Vector2(1000f, 0f),
                        EaseOut(k)
                    );
                    s.Holder.localScale = new Vector3(0.35f, 0.9f + 0.5f * k, 1f);
                    Show(s, Blood, k, k);
                }
            );
            Illuminate(at + new Vector2(900f, 0f), Blood, 0.6f, 900f, 0.2f, 0.4f, spill: false);
            foreach (int index in Wave(hits, 1))
                land(index);
            yield return Wait(0.5f);
        }

        /// <summary>
        /// 鬨の声, rousing: rings of sound burst from the user one after another, and over each ally
        /// an orange light rises with arrows pointing up and sparks.
        /// </summary>
        private IEnumerator WarCry(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = new Color(1f, 0.62f, 0.28f);
            var at = Center(FrontLayer, caster) + new Vector2(30f, 40f);
            DimTo(0.36f, 0.15f);
            for (int n = 0; n < 3; n++)
            {
                var ring = Spawn(
                    frontLight,
                    BattleVfxShape.Ring,
                    at,
                    new Vector2(700f, 700f),
                    color
                );
                Animate(
                    ring,
                    0.5f,
                    (s, k) =>
                    {
                        s.Holder.localScale = RingScale(k, false);
                        Show(s, color, k, k);
                    }
                );
                Shake(0.12f, Vector2.zero);
                yield return Wait(0.1f);
            }
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var center = Center(FrontLayer, area);
                Column(
                    backLight,
                    Feet(FrontLayer, area),
                    color,
                    new Vector2(220f, 320f),
                    0.8f,
                    down: false,
                    fast: false
                );
                Bloom(
                    frontLight,
                    BattleVfxShape.Chevron,
                    center + new Vector2(0f, 40f),
                    new Vector2(110f, 150f),
                    Gold,
                    0.8f,
                    0.6f,
                    ErodeBright
                );
                Sparks(center, color, 8, 0.6f, Vector2.up);
                Illuminate(center, color, 0.5f, 220f, 0.2f, 0.3f, spill: false);
                land(index);
                yield return Wait(0.05f);
            }
            yield return Wait(0.45f);
        }

        // --- Toma ------------------------------------------------------------------------------

        /// <summary>
        /// 火の粉, a crackle: a flick of the staff throws a swarm of embers that weave through the
        /// air to the enemy, and little flames cling to it and flicker, embers drifting up.
        /// </summary>
        private IEnumerator Embers(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("Embers");
            var from = Center(FrontLayer, caster) + new Vector2(60f, 30f);
            DimTo(0.36f, 0.12f);
            Glow(frontLight, from, color, 160f, 0.3f, 0.6f);
            yield return Wait(0.12f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                for (int n = 0; n < 9; n++)
                {
                    var end = at + UnityEngine.Random.insideUnitCircle * 50f;
                    StartCoroutine(
                        Shoot(
                            BattleVfxShape.Flame,
                            from + UnityEngine.Random.insideUnitCircle * 30f,
                            end,
                            new Vector2(46f, 46f),
                            new Vector2(0.5f, 0.5f),
                            UnityEngine.Random.Range(0.28f, 0.42f),
                            UnityEngine.Random.Range(-90f, 140f),
                            color,
                            0f,
                            Embered(color)
                        )
                    );
                }
                yield return Wait(0.36f);
                var weight = land(index);
                Impact(
                    area,
                    at,
                    color,
                    weight,
                    0.35f,
                    Vector2.right,
                    flashScreen: false,
                    rays: false
                );
                Licks(area, 7, 0.9f);
            }
            yield return Wait(0.4f);
        }

        /// <summary>
        /// フレイムピラー, an eruption: a red circle opens under the enemy and spins faster, then a
        /// column of fire roars up from it, licks of flame and lumps rising inside it, lighting the
        /// enemy from below; smoke and a scorch are left.
        /// </summary>
        private IEnumerator FlamePillar(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("FlamePillar");
            DimTo(SpellDim, 0.15f);
            Glow(frontLight, Center(FrontLayer, caster), color, 220f, 0.4f, 0.5f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var feet = Feet(FrontLayer, area);
                var circle = Spawn(
                    backLight,
                    BattleVfxShape.MagicCircle,
                    feet,
                    new Vector2(360f, 360f),
                    color
                );
                Animate(
                    circle,
                    1.2f,
                    (s, k) =>
                    {
                        s.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, -k * k * 900f);
                        float grow = EaseOut(Mathf.Min(1f, k * 4f));
                        s.Holder.localScale = new Vector3(grow, grow * FloorTilt, 1f);
                        Show(s, color, Mathf.Min(1f, k * 1.5f), Eaten(k, 0.7f), ErodeBright * 1.2f);
                    }
                );
                Illuminate(feet, color, 0.5f, 300f, 0.35f, 0.2f, spill: false);
                yield return Wait(0.35f);
                Column(
                    backLight,
                    feet,
                    color,
                    new Vector2(300f, 760f),
                    0.9f,
                    down: false,
                    fast: true,
                    fire: true
                );
                Spray(
                    backLight,
                    feet + Vector2.up * 20f,
                    new Burst
                    {
                        Shape = BattleVfxShape.FlameTongue,
                        Bright = ErodeBright,
                        Fire = true,
                        Speed = new Vector2(500f, 900f),
                        Direction = 90f,
                        Spread = 18f,
                        Life = new Vector2(0.4f, 0.7f),
                        Size = new Vector2(90f, 170f),
                        Grow = 0.5f,
                        Shrink = 1.2f,
                        Gravity = -200f,
                        Drag = 1.2f,
                        Spin = 10f,
                        Hold = 0.2f,
                        Area = 40f,
                    },
                    12
                );
                Spray(
                    frontLight,
                    feet + Vector2.up * 40f,
                    new Burst
                    {
                        Shape = BattleVfxShape.Flame,
                        Bright = ErodeBright,
                        Fire = true,
                        Speed = new Vector2(500f, 1000f),
                        Direction = 90f,
                        Spread = 14f,
                        Life = new Vector2(0.3f, 0.55f),
                        Size = new Vector2(60f, 110f),
                        Grow = 0.5f,
                        Shrink = 1.1f,
                        Drag = 1.5f,
                        Spin = 40f,
                        Hold = 0.15f,
                        Area = 30f,
                    },
                    6
                );
                var weight = land(index);
                Impact(area, at, color, weight, 0.85f, Vector2.up, flashScreen: true, rays: false);
                Illuminate(feet + Vector2.down * 40f, color, 1f, 420f, 0.4f, 0.4f, flicker: 0.4f);
                FloorMark(
                    BattleVfxShape.Scorch,
                    feet,
                    Vector2.one * 260f,
                    new Color(0.06f, 0.045f, 0.04f),
                    0.8f,
                    1.6f,
                    flat: true
                );
                Puffs(
                    backSmoke,
                    at + Vector2.up * 120f,
                    new Color(0.18f, 0.15f, 0.14f, 0.7f),
                    5,
                    60f,
                    1.6f,
                    160f
                );
            }
            yield return Wait(0.5f);
        }

        /// <summary>
        /// ファイアストーム, a raging blaze: a whirlwind of fire rises amid the enemies and sweeps
        /// across them, flames thrown out of it; each enemy is caught as it passes, and embers and
        /// smoke are left swirling.
        /// </summary>
        private IEnumerator FireStorm(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("FireStorm");
            yield return Charge(caster, color, 0.28f, big: false);
            var wave = Wave(hits, 0);
            var mid = MidFeet(hits, 0);
            float left = mid.x - 260f;
            float right = mid.x + 260f;
            foreach (float offset in new[] { -1f, 1f })
            {
                var storm = Spawn(
                    backLight,
                    BattleVfxShape.Tornado,
                    new Vector2(mid.x, mid.y),
                    new Vector2(460f, 720f),
                    color,
                    new Vector2(0.5f, 0.05f)
                );
                float side = offset;
                Animate(
                    storm,
                    1.1f,
                    (s, k) =>
                    {
                        float x = Mathf.Lerp(
                            side < 0 ? left : right,
                            side < 0 ? right : left,
                            EaseOut(k)
                        );
                        s.Holder.anchoredPosition = new Vector2(x, mid.y);
                        float grow = EaseOut(Mathf.Min(1f, k * 6f));
                        s.Holder.localScale = new Vector3(side < 0 ? 1f : 0.8f, grow, 1f);
                        s.Image.color = Color.Lerp(Color.white, color, 0.3f + 0.5f * k);
                        s.Image.SetShape(s.Seed, k, Eaten(k, 0.55f), CoreBright);
                    }
                );
            }
            Spray(
                backLight,
                mid + Vector2.up * 200f,
                new Burst
                {
                    Shape = BattleVfxShape.Flame,
                    Bright = ErodeBright,
                    Fire = true,
                    Speed = new Vector2(200f, 500f),
                    Spread = 360f,
                    Life = new Vector2(0.5f, 0.9f),
                    Size = new Vector2(90f, 160f),
                    Grow = 0.4f,
                    Shrink = 1.3f,
                    Gravity = -250f,
                    Drag = 2f,
                    Spin = 200f,
                    Hold = 0.2f,
                    Area = 220f,
                    Delay = new Vector2(0f, 0.5f),
                },
                20
            );
            Illuminate(mid + Vector2.up * 150f, color, 1f, 700f, 0.7f, 0.5f, flicker: 0.4f);
            ScreenFlash(color, 0.18f);
            foreach (int index in wave)
            {
                yield return Wait(0.12f);
                var area = hits[index].Area;
                var weight = land(index);
                Impact(
                    area,
                    Center(FrontLayer, area),
                    color,
                    weight,
                    0.6f,
                    Vector2.up,
                    flashScreen: false,
                    rays: false
                );
                Licks(area, 4, 0.7f);
            }
            Puffs(
                backSmoke,
                mid + Vector2.up * 300f,
                new Color(0.16f, 0.13f, 0.13f, 0.7f),
                7,
                200f,
                1.8f,
                200f
            );
            yield return Wait(0.55f);
        }

        /// <summary>
        /// 爆炎の刻印, a timed mark: a spark of fire flies to the enemy and a burning sigil stamps
        /// onto it face on, pulses hot, and shrinks to a small glowing mark that waits (it blows up
        /// at the start of the next party turn: <see cref="Detonate"/>).
        /// </summary>
        private IEnumerator BlastSigil(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("BlastSigil");
            var from = Center(FrontLayer, caster) + new Vector2(60f, 30f);
            yield return Charge(caster, color, 0.22f, big: false);
            foreach (int index in Wave(hits, 0))
            {
                var at = Center(FrontLayer, hits[index].Area);
                yield return Shoot(
                    BattleVfxShape.Spark,
                    from,
                    at,
                    new Vector2(60f, 60f),
                    new Vector2(0.5f, 0.5f),
                    0.2f,
                    60f,
                    color,
                    0f,
                    Embered(color)
                );
                var sigil = Spawn(
                    frontLight,
                    BattleVfxShape.MagicCircle,
                    at,
                    new Vector2(300f, 300f),
                    color
                );
                Animate(
                    sigil,
                    1.1f,
                    (s, k) =>
                    {
                        float stamp =
                            k < 0.15f
                                ? BackOut(k / 0.15f)
                                : 1f - 0.55f * EaseOut((k - 0.15f) / 0.85f);
                        s.Holder.localScale = new Vector3(stamp, stamp, 1f);
                        s.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, -k * 200f);
                        float pulse = 0.75f + 0.25f * Mathf.Sin(k * 40f);
                        Show(
                            s,
                            Color.Lerp(Color.white, color, 0.4f + 0.4f * k) * pulse,
                            Mathf.Min(1f, k * 3f),
                            Eaten(k, 0.8f),
                            CoreBright
                        );
                    }
                );
                HotFlash(at, color, 200f);
                Shake(0.12f, Vector2.right);
                Illuminate(at, color, 0.6f, 280f, 0.4f, 0.4f, spill: false, flicker: 0.3f);
                land(index);
            }
            yield return Wait(0.6f);
        }

        /// <summary>
        /// メテオ, the sky falling: the stage goes dark red and rumbles, a glow swells at the top
        /// corner, and a huge burning rock falls on the enemies trailing fire; it blows up among
        /// them in a great blast, each caught in its own explosion, with rings, debris, a long stop
        /// and a column of smoke after.
        /// </summary>
        private IEnumerator Meteor(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("Meteor");
            DimTo(NightDim, 0.2f);
            var mid = MidFeet(hits, 0) + Vector2.up * 60f;
            var sky = new Vector2(FrontLayer.rect.xMax - 120f, FrontLayer.rect.yMax + 120f);
            Glow(frontLight, sky + new Vector2(-120f, -160f), color, 700f, 0.9f, 0.8f);
            for (int n = 0; n < 6; n++)
            {
                Shake(0.08f, Vector2.zero);
                yield return Wait(0.08f);
            }
            yield return Shoot(
                BattleVfxShape.Fireball,
                sky,
                mid,
                new Vector2(820f, 820f),
                new Vector2(0.85f, 0.5f),
                0.42f,
                0f,
                color,
                0f,
                new Burst
                {
                    Shape = BattleVfxShape.Flame,
                    Bright = ErodeBright,
                    Fire = true,
                    Speed = new Vector2(40f, 200f),
                    Spread = 360f,
                    Life = new Vector2(0.4f, 0.8f),
                    Size = new Vector2(90f, 170f),
                    Grow = 0.6f,
                    Shrink = 1.4f,
                    Gravity = -120f,
                    Drag = 2f,
                    Spin = 90f,
                    Hold = 0.2f,
                    Area = 60f,
                }
            );
            ScreenFlash(new Color(1f, 0.85f, 0.6f), MaxFlash);
            HitStop(0.18f);
            SlowMotion(0.4f, 0.3f);
            Punch(mid, 0.05f);
            Shake(1f, Vector2.down);
            Ring(backLight, mid, Color.white, 900f, 0.45f, floor: true);
            Ring(backLight, mid, color, 1300f, 0.7f, floor: true);
            Illuminate(mid + Vector2.up * 100f, color, 1f, 1200f, 0.4f, 0.8f, flicker: 0.4f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var weight = land(index);
                Explosion(area, Center(FrontLayer, area), weight);
            }
            Puffs(
                backSmoke,
                mid + Vector2.up * 260f,
                new Color(0.17f, 0.14f, 0.14f, 0.8f),
                10,
                260f,
                2.2f,
                260f
            );
            yield return Wait(0.7f);
        }

        /// <summary>
        /// フレイムエンチャント, a flame taking hold: a ring of fire runs round the ally's feet, licks
        /// of flame climb up the ally, and a hot twinkle flares at weapon height as the fire settles
        /// into it.
        /// </summary>
        private IEnumerator FlameEnchant(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("FlameEnchant");
            yield return Charge(caster, color, 0.2f, big: false);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var feet = Feet(FrontLayer, area);
                Ring(backLight, feet, color, 300f, 0.5f, floor: true);
                Spray(
                    backLight,
                    feet,
                    new Burst
                    {
                        Shape = BattleVfxShape.FlameTongue,
                        Bright = ErodeBright,
                        Fire = true,
                        Speed = new Vector2(120f, 260f),
                        Direction = 90f,
                        Spread = 30f,
                        Life = new Vector2(0.5f, 0.8f),
                        Size = new Vector2(50f, 90f),
                        Grow = 0.5f,
                        Shrink = 1.1f,
                        Gravity = -200f,
                        Drag = 1f,
                        Spin = 15f,
                        Hold = 0.2f,
                        Area = 70f,
                        Delay = new Vector2(0f, 0.25f),
                    },
                    12
                );
                yield return Wait(0.25f);
                Twinkle(
                    at + new Vector2(40f, 20f),
                    Color.Lerp(Color.white, color, 0.3f),
                    200f,
                    0.5f,
                    0f,
                    1f
                );
                Illuminate(at, color, 0.8f, 260f, 0.4f, 0.4f, spill: false, flicker: 0.4f);
                land(index);
                Licks(area, 5, 1f);
            }
            yield return Wait(0.45f);
        }

        /// <summary>
        /// つらら落とし, falling points: a cold mist gathers at the top of the screen, and icicles
        /// drop from it one after another onto the enemies picked, each shattering into chips over
        /// a patch of frost.
        /// </summary>
        private IEnumerator Icicles(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("Icicles");
            DimTo(SpellDim, 0.15f);
            float top = FrontLayer.rect.yMax - 40f;
            var mid = MidFeet(hits, 0);
            Spray(
                backSmoke,
                new Vector2(mid.x, top),
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    From = new Color(0.85f, 0.94f, 1f, 0.6f),
                    To = new Color(0.7f, 0.85f, 1f, 0.6f),
                    Speed = new Vector2(20f, 80f),
                    Direction = 0f,
                    Spread = 360f,
                    Life = new Vector2(1.2f, 1.8f),
                    Size = new Vector2(200f, 300f),
                    Aspect = 0.5f,
                    Grow = 0.5f,
                    Shrink = 1.5f,
                    Drag = 1f,
                    Spin = 6f,
                    Hold = 0.35f,
                    Area = 300f,
                },
                6
            );
            yield return Wait(0.25f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at =
                    Center(FrontLayer, area) + new Vector2(UnityEngine.Random.Range(-30f, 30f), 0f);
                var start = new Vector2(at.x + UnityEngine.Random.Range(-20f, 20f), top);
                // An icicle points down: a spike of ice turned over, falling fast.
                yield return Shoot(
                    BattleVfxShape.IceSpike,
                    start,
                    at,
                    new Vector2(70f, 170f),
                    new Vector2(0.5f, 0.03f),
                    0.13f,
                    0f,
                    Color.white,
                    90f,
                    null,
                    turn: false
                );
                var weight = land(index);
                Spray(
                    frontLight,
                    at,
                    new Burst
                    {
                        Shape = BattleVfxShape.Chip,
                        From = Color.white,
                        To = new Color(0.8f, 0.92f, 1f, 0f),
                        Speed = new Vector2(150f, 450f),
                        Direction = 90f,
                        Spread = 160f,
                        Life = new Vector2(0.4f, 0.7f),
                        Size = new Vector2(10f, 24f),
                        Gravity = 1400f,
                        Drag = 1f,
                        Spin = 360f,
                        Area = 20f,
                    },
                    10
                );
                FloorMark(
                    BattleVfxShape.Frost,
                    Feet(FrontLayer, area),
                    new Vector2(240f, 80f),
                    new Color(0.85f, 0.95f, 1f),
                    0.6f,
                    1.2f,
                    flat: false
                );
                Star(at, color, 200f, 0.16f);
                HitStop(Weighted(0.03f, weight));
                Shake(Weighted(0.2f, weight), Vector2.down);
                Illuminate(at, color, 0.6f, 260f, 0.03f, 0.25f, spill: false);
                yield return Wait(0.06f);
            }
            yield return Wait(0.35f);
        }

        /// <summary>
        /// ブリザード, a howling cold: the stage goes dark, then a gale of snow streams across the
        /// enemies from the left with a pale whirl among them; each is frosted over, and a cold
        /// mist rolls low over the floor long after.
        /// </summary>
        private IEnumerator Blizzard(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("Blizzard");
            yield return Charge(caster, color, 0.26f, big: true);
            var mid = MidFeet(hits, 0);
            var whirl = Spawn(
                backLight,
                BattleVfxShape.Tornado,
                mid,
                new Vector2(700f, 700f),
                new Color(0.85f, 0.95f, 1f),
                new Vector2(0.5f, 0.05f)
            );
            Animate(
                whirl,
                1.3f,
                (s, k) =>
                {
                    s.Holder.localScale = new Vector3(1f, EaseOut(Mathf.Min(1f, k * 5f)), 1f);
                    Show(s, new Color(0.85f, 0.95f, 1f, 0.6f), k, Eaten(k, 0.6f), ErodeBright);
                }
            );
            for (int n = 0; n < 5; n++)
            {
                Spray(
                    frontLight,
                    new Vector2(mid.x - 600f, mid.y + 120f + n * 50f),
                    new Burst
                    {
                        Shape = n % 2 == 0 ? BattleVfxShape.Spark : BattleVfxShape.Sparkle,
                        From = Color.white,
                        To = new Color(0.8f, 0.92f, 1f, 0f),
                        Speed = new Vector2(900f, 1500f),
                        Direction = -8f,
                        Spread = 20f,
                        Life = new Vector2(0.6f, 1f),
                        Size = new Vector2(10f, 22f),
                        Gravity = 100f,
                        Drag = 0.5f,
                        Spin = 300f,
                        Area = 120f,
                        Delay = new Vector2(0f, 0.5f),
                    },
                    14
                );
                Spray(
                    backSmoke,
                    new Vector2(mid.x - 500f, mid.y + 100f + n * 40f),
                    new Burst
                    {
                        Shape = BattleVfxShape.Smoke,
                        From = new Color(0.86f, 0.94f, 1f, 0.45f),
                        To = new Color(0.75f, 0.88f, 1f, 0.45f),
                        Speed = new Vector2(700f, 1000f),
                        Direction = -5f,
                        Spread = 10f,
                        Life = new Vector2(0.8f, 1.1f),
                        Size = new Vector2(180f, 260f),
                        Aspect = 0.5f,
                        Grow = 0.6f,
                        Shrink = 1.4f,
                        Drag = 1.2f,
                        Spin = 6f,
                        Hold = 0.3f,
                        Area = 60f,
                        Delay = new Vector2(0f, 0.3f),
                    },
                    3
                );
            }
            Illuminate(mid + Vector2.up * 150f, color, 0.8f, 800f, 0.6f, 0.6f);
            ScreenFlash(new Color(0.85f, 0.95f, 1f), 0.2f);
            yield return Wait(0.3f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var weight = land(index);
                Impact(
                    area,
                    Center(FrontLayer, area),
                    color,
                    weight,
                    0.6f,
                    Vector2.right,
                    flashScreen: false,
                    rays: false
                );
                FloorMark(
                    BattleVfxShape.Frost,
                    Feet(FrontLayer, area),
                    new Vector2(320f, 110f),
                    new Color(0.85f, 0.95f, 1f),
                    0.75f,
                    2f,
                    flat: false
                );
                yield return Wait(0.08f);
            }
            Spray(
                backSmoke,
                mid + Vector2.up * 20f,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    From = new Color(0.86f, 0.95f, 1f, 0.7f),
                    To = new Color(0.7f, 0.85f, 1f, 0.7f),
                    Speed = new Vector2(40f, 160f),
                    Direction = 0f,
                    Spread = 360f,
                    Life = new Vector2(1.4f, 2f),
                    Size = new Vector2(220f, 320f),
                    Aspect = 0.45f,
                    Grow = 0.5f,
                    Shrink = 1.5f,
                    Gravity = 10f,
                    Drag = 1.8f,
                    Spin = 6f,
                    Hold = 0.3f,
                    Area = 200f,
                },
                7
            );
            yield return Wait(0.5f);
        }

        /// <summary>
        /// アイスミラー, a clear reflection: shards of ice fly together in front of the ally into a
        /// pane, a clear bubble closes round the ally, and a band of light sweeps across it as it
        /// catches the light.
        /// </summary>
        private IEnumerator IceMirror(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("IceMirror");
            yield return Charge(caster, color, 0.2f, big: false);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var pane = at + new Vector2(90f, 0f);
                for (int n = 0; n < 10; n++)
                {
                    float a = n * 36f * Mathf.Deg2Rad;
                    var start = pane + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 240f;
                    StartCoroutine(
                        Shoot(
                            BattleVfxShape.Shard,
                            start,
                            pane + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 40f,
                            new Vector2(16f, 40f),
                            new Vector2(0.5f, 0.5f),
                            0.22f,
                            0f,
                            Color.white,
                            -90f,
                            null
                        )
                    );
                }
                yield return Wait(0.24f);
                Bloom(
                    frontLight,
                    BattleVfxShape.Snowflake,
                    pane,
                    new Vector2(200f, 200f),
                    Color.white,
                    0.6f,
                    0.5f,
                    CoreBright,
                    spin: 40f
                );
                var bubble = Bloom(
                    backLight,
                    BattleVfxShape.Bubble,
                    at,
                    new Vector2(300f, 330f),
                    color,
                    1.1f,
                    0.75f,
                    ErodeBright
                );
                Bloom(
                    frontLight,
                    BattleVfxShape.Bubble,
                    at,
                    new Vector2(300f, 330f),
                    new Color(color.r, color.g, color.b, 0.35f),
                    1.1f,
                    0.75f,
                    1f
                );
                Line(
                    frontLight,
                    at + new Vector2(-150f, 160f),
                    at + new Vector2(150f, -160f),
                    Color.white,
                    40f,
                    0.35f,
                    0.3f
                );
                Illuminate(at, color, 0.6f, 260f, 0.4f, 0.4f, spill: false);
                Ring(backLight, Feet(FrontLayer, area), color, 300f, 0.5f, floor: true);
                land(index);
            }
            yield return Wait(0.6f);
        }

        /// <summary>
        /// 絶対零度, time stopping: all goes a deep blue and frost creeps over the floor while a
        /// great snowflake grows slowly behind the enemy; it flashes, a ring of tall ice closes
        /// round the enemy and the time stops, then it all breaks into chips and a cold mist.
        /// </summary>
        private IEnumerator AbsoluteZero(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("AbsoluteZero");
            DimTo(NightDim, 0.25f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var feet = Feet(FrontLayer, area);
                FloorMark(
                    BattleVfxShape.Frost,
                    feet,
                    new Vector2(700f, 220f),
                    new Color(0.85f, 0.95f, 1f),
                    0.8f,
                    2.6f,
                    flat: false
                );
                var flake = Spawn(
                    backLight,
                    BattleVfxShape.Snowflake,
                    at,
                    new Vector2(620f, 620f),
                    Color.white
                );
                Animate(
                    flake,
                    1.6f,
                    (s, k) =>
                    {
                        s.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, k * 30f);
                        float flash = k > 0.38f && k < 0.45f ? 1.6f : 1f;
                        Show(
                            s,
                            Color.Lerp(Color.white, color, 0.3f) * flash,
                            Mathf.Min(1f, k * 1.4f),
                            Eaten(k, 0.6f),
                            CoreBright
                        );
                    }
                );
                Gather(at, color, 20, 300f, 0.6f);
                yield return Wait(0.62f);
                // The prison: tall spikes all round, closing in at once.
                for (int n = 0; n < 12; n++)
                {
                    bool front = n >= 8;
                    float x =
                        Mathf.Lerp(-170f, 170f, (front ? n - 8 : n) / (front ? 3f : 7f))
                        + UnityEngine.Random.Range(-10f, 10f);
                    float height =
                        (front ? 200f : 330f) * (0.75f + 0.5f * (1f - Mathf.Abs(x) / 170f));
                    Spike(
                        front ? frontLight : backLight,
                        feet + new Vector2(x, front ? -14f : 8f),
                        height,
                        -x * 0.12f,
                        front ? 0.04f : 0f,
                        front
                    );
                }
                var weight = land(index);
                Impact(area, at, color, weight, 1f, Vector2.right, flashScreen: true, rays: false);
                HitStop(0.18f);
                Punch(at, 0.035f);
                Illuminate(at, color, 1f, 600f, 0.6f, 0.6f);
                yield return Wait(0.5f);
                Spray(
                    frontLight,
                    at,
                    new Burst
                    {
                        Shape = BattleVfxShape.Chip,
                        From = Color.white,
                        To = new Color(0.8f, 0.92f, 1f, 0f),
                        Speed = new Vector2(200f, 700f),
                        Direction = 90f,
                        Spread = 220f,
                        Life = new Vector2(0.6f, 1f),
                        Size = new Vector2(16f, 40f),
                        Gravity = 1400f,
                        Drag = 1f,
                        Spin = 400f,
                        Area = 120f,
                    },
                    30
                );
                Puffs(
                    backSmoke,
                    feet + Vector2.up * 30f,
                    new Color(0.86f, 0.95f, 1f, 0.7f),
                    6,
                    160f,
                    1.6f,
                    200f
                );
            }
            yield return Wait(0.4f);
        }

        /// <summary>
        /// 詠唱短縮, quickening: a circle of runes turns faster and faster over the user, rings close
        /// in on it, and a blue arrow pointing down (costs falling) sinks.
        /// </summary>
        private IEnumerator QuickCast(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("QuickCast");
            var at = Center(FrontLayer, caster) + new Vector2(0f, 150f);
            DimTo(0.36f, 0.15f);
            var circle = Spawn(
                frontLight,
                BattleVfxShape.MagicCircle,
                at,
                new Vector2(240f, 240f),
                color
            );
            Animate(
                circle,
                0.9f,
                (s, k) =>
                {
                    s.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, -k * k * 1400f);
                    float grow = EaseOut(Mathf.Min(1f, k * 4f));
                    s.Holder.localScale = new Vector3(grow, grow * 0.55f, 1f);
                    Show(s, color, Mathf.Min(1f, k * 2f), Eaten(k, 0.7f), CoreBright);
                }
            );
            for (int n = 0; n < 3; n++)
            {
                var ring = Spawn(
                    frontLight,
                    BattleVfxShape.Ring,
                    at,
                    new Vector2(420f, 420f),
                    color
                );
                float delay = n * 0.12f;
                Animate(
                    ring,
                    0.5f + delay,
                    (s, t) =>
                    {
                        float time = t * (0.5f + delay) - delay;
                        if (time < 0f)
                        {
                            Show(s, Color.clear, 0f, 0f);
                            return;
                        }
                        float k = time / 0.5f;
                        // Closing in rather than spreading out.
                        float size = 1f - 0.85f * EaseOut(k);
                        s.Holder.localScale = new Vector3(size, size * 0.55f, 1f);
                        Show(s, color, 0.3f, 0.3f + 0.6f * k);
                    }
                );
            }
            yield return Wait(0.45f);
            Twinkle(at, Color.white, 160f, 0.35f, 0f, -1f);
            Bloom(
                frontLight,
                BattleVfxShape.Chevron,
                Center(FrontLayer, caster) + new Vector2(0f, 60f),
                new Vector2(110f, 150f),
                color,
                0.7f,
                0.6f,
                ErodeBright,
                angle: 180f
            );
            foreach (int index in Wave(hits, 0))
                land(index);
            yield return Wait(0.4f);
        }

        // --- Luka ------------------------------------------------------------------------------

        /// <summary>
        /// 急所突き, a pinpoint: a red sight closes in on the enemy, a thin glint darts to it, and a
        /// tight hot burst pierces straight through, shards flying out of its back. A blow on a
        /// weakness sends gold motes flying back to the user (the energy won).
        /// </summary>
        private IEnumerator VitalThrust(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("VitalThrust");
            var from = Center(FrontLayer, caster) + new Vector2(50f, 10f);
            DimTo(0.42f, 0.12f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                Bloom(
                    frontLight,
                    BattleVfxShape.Reticle,
                    at,
                    new Vector2(240f, 240f),
                    color,
                    0.55f,
                    0.6f,
                    CoreBright,
                    spin: 60f
                );
                yield return Wait(0.3f);
                yield return Shoot(
                    BattleVfxShape.Streak,
                    from,
                    at,
                    new Vector2(40f, 260f),
                    new Vector2(0.5f, 0.85f),
                    0.08f,
                    0f,
                    Color.white,
                    -90f,
                    null
                );
                var weight = land(index);
                Star(at, Color.white, 240f, 0.18f);
                var dir = (at - from).normalized;
                Spray(
                    frontLight,
                    at,
                    new Burst
                    {
                        Shape = BattleVfxShape.Shard,
                        Bright = CoreBright,
                        From = Color.white,
                        To = new Color(color.r, color.g, color.b, 0f),
                        Speed = new Vector2(700f, 1300f),
                        Direction = Angle(dir),
                        Spread = 24f,
                        Life = new Vector2(0.15f, 0.3f),
                        Size = new Vector2(8f, 16f),
                        Aspect = 3f,
                        Drag = 3f,
                        Area = 10f,
                    },
                    10
                );
                Impact(area, at, color, weight, 0.6f, dir, flashScreen: false);
                if (weight != BattleHitWeight.Normal)
                    for (int n = 0; n < 5; n++)
                        StartCoroutine(
                            Shoot(
                                BattleVfxShape.Sparkle,
                                at,
                                from + UnityEngine.Random.insideUnitCircle * 40f,
                                new Vector2(40f, 40f),
                                new Vector2(0.5f, 0.5f),
                                0.35f + n * 0.04f,
                                UnityEngine.Random.Range(60f, 160f),
                                Gold,
                                0f,
                                null
                            )
                        );
            }
            yield return Wait(0.45f);
        }

        /// <summary>
        /// 毒針, a slow sting: three green needles fly in quick turn, a sickly flash, and bubbles of
        /// poison and purple wisps rise from the enemy for a while.
        /// </summary>
        private IEnumerator PoisonNeedle(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("PoisonNeedle");
            var from = Center(FrontLayer, caster) + new Vector2(50f, 20f);
            DimTo(0.38f, 0.12f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                for (int n = 0; n < 3; n++)
                {
                    StartCoroutine(
                        Shoot(
                            BattleVfxShape.Arrow,
                            from + new Vector2(0f, (n - 1) * 30f),
                            at + new Vector2(0f, (n - 1) * 24f),
                            new Vector2(150f, 40f),
                            new Vector2(0.82f, 0.5f),
                            0.16f,
                            20f,
                            color,
                            0f,
                            null
                        )
                    );
                    yield return Wait(0.06f);
                }
                yield return Wait(0.12f);
                var weight = land(index);
                Star(at, color, 180f, 0.16f);
                Impact(
                    area,
                    at,
                    color,
                    weight,
                    0.35f,
                    Vector2.right,
                    flashScreen: false,
                    rays: false
                );
                Bubbles(at, color, Violet, 10);
                Puffs(backSmoke, at, new Color(0.35f, 0.18f, 0.45f, 0.45f), 4, 50f, 1.3f, 120f);
            }
            yield return Wait(0.6f);
        }

        /// <summary>
        /// 影縫い, pinning: a dark blade flies down into the enemy's shadow at its feet, dark cracks
        /// and a creeping shadow spread from it, and threads of shadow rise round the enemy and hold
        /// it a moment.
        /// </summary>
        private IEnumerator ShadowStitch(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("ShadowStitch");
            var from = Center(FrontLayer, caster) + new Vector2(50f, 60f);
            DimTo(0.55f, 0.15f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var feet = Feet(FrontLayer, area);
                var at = Center(FrontLayer, area);
                yield return Shoot(
                    BattleVfxShape.Arrow,
                    from,
                    feet + new Vector2(-20f, 10f),
                    new Vector2(170f, 50f),
                    new Vector2(0.82f, 0.5f),
                    0.16f,
                    120f,
                    color,
                    0f,
                    null
                );
                var weight = land(index);
                var crack = Spawn(
                    backSmoke,
                    BattleVfxShape.Crack,
                    feet,
                    new Vector2(360f, 360f),
                    color
                );
                crack.Holder.localScale = new Vector3(1f, FloorTilt, 1f);
                Animate(
                    crack,
                    1.1f,
                    (s, k) => Show(s, color, Mathf.Min(1f, k * 2f), Eaten(k, 0.6f), ErodeBright)
                );
                Spray(
                    backSmoke,
                    feet,
                    new Burst
                    {
                        Shape = BattleVfxShape.Smoke,
                        From = new Color(0.12f, 0.06f, 0.18f, 0.75f),
                        To = new Color(0.08f, 0.04f, 0.12f, 0.75f),
                        Speed = new Vector2(60f, 180f),
                        Direction = 0f,
                        Spread = 360f,
                        Life = new Vector2(0.9f, 1.3f),
                        Size = new Vector2(120f, 180f),
                        Aspect = 0.4f,
                        Grow = 0.5f,
                        Shrink = 1.6f,
                        Drag = 2f,
                        Spin = 10f,
                        Hold = 0.3f,
                        Area = 40f,
                    },
                    6
                );
                // Threads of shadow rising round the enemy.
                Spray(
                    frontLight,
                    feet,
                    new Burst
                    {
                        Shape = BattleVfxShape.Streak,
                        Bright = ErodeBright,
                        From = color,
                        To = new Color(color.r * 0.4f, color.g * 0.3f, color.b * 0.6f, 0f),
                        Speed = new Vector2(300f, 500f),
                        Direction = 90f,
                        Spread = 30f,
                        Life = new Vector2(0.4f, 0.7f),
                        Size = new Vector2(6f, 10f),
                        Stretch = 0.25f,
                        Drag = 4f,
                        Area = 80f,
                        Delay = new Vector2(0f, 0.3f),
                    },
                    14
                );
                Impact(
                    area,
                    at,
                    color,
                    weight,
                    0.4f,
                    Vector2.down,
                    flashScreen: false,
                    rays: false
                );
            }
            yield return Wait(0.6f);
        }

        /// <summary>
        /// 看破の矢, seeing through: a teal arrow flies to the enemy and a magic eye opens over it,
        /// a band of light scanning down its body.
        /// </summary>
        private IEnumerator InsightArrow(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("InsightArrow");
            var from = Center(FrontLayer, caster) + new Vector2(50f, 20f);
            DimTo(0.42f, 0.12f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var (low, high) = Bounds(FrontLayer, area);
                yield return Shoot(
                    BattleVfxShape.Arrow,
                    from,
                    at,
                    new Vector2(220f, 60f),
                    new Vector2(0.82f, 0.5f),
                    0.18f,
                    30f,
                    color,
                    0f,
                    null
                );
                var weight = land(index);
                Star(at, color, 220f, 0.18f);
                Impact(
                    area,
                    at,
                    color,
                    weight,
                    0.5f,
                    Vector2.right,
                    flashScreen: false,
                    rays: false
                );
                Bloom(
                    frontLight,
                    BattleVfxShape.MagicCircle,
                    new Vector2(at.x, high.y + 40f),
                    new Vector2(200f, 200f),
                    color,
                    0.9f,
                    0.65f,
                    CoreBright,
                    spin: 120f
                );
                var scan = Spawn(
                    frontLight,
                    BattleVfxShape.Beam,
                    new Vector2(at.x, high.y),
                    new Vector2((high.x - low.x) + 80f, 40f),
                    color
                );
                float top = high.y;
                float bottom = low.y;
                Animate(
                    scan,
                    0.6f,
                    (s, k) =>
                    {
                        s.Holder.anchoredPosition = new Vector2(
                            at.x,
                            Mathf.Lerp(top, bottom, EaseOut(k))
                        );
                        Show(s, color, Mathf.Min(1f, k * 4f), Eaten(k, 0.7f), ErodeBright);
                    }
                );
                Twinkle(new Vector2(at.x, high.y + 40f), Color.white, 140f, 0.4f, 0.1f, 1f);
            }
            yield return Wait(0.5f);
        }

        /// <summary>
        /// アローレイン, a downpour: arrows are loosed high off the top, then fall in two volleys
        /// slanting across all the enemies, kicking up dust where they land.
        /// </summary>
        private IEnumerator ArrowRain(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("ArrowRain");
            var from = Center(FrontLayer, caster) + new Vector2(40f, 60f);
            DimTo(0.42f, 0.15f);
            for (int n = 0; n < 5; n++)
                StartCoroutine(
                    Shoot(
                        BattleVfxShape.Arrow,
                        from,
                        from + new Vector2(120f + n * 30f, 700f),
                        new Vector2(140f, 40f),
                        new Vector2(0.82f, 0.5f),
                        0.22f,
                        0f,
                        color,
                        0f,
                        null
                    )
                );
            yield return Wait(0.3f);
            float top = FrontLayer.rect.yMax + 80f;
            for (int wave = 0; wave < Waves(hits); wave++)
            {
                var list = Wave(hits, wave);
                foreach (int index in list)
                {
                    var feet = Feet(FrontLayer, hits[index].Area);
                    for (int n = 0; n < 6; n++)
                    {
                        var end =
                            feet
                            + new Vector2(
                                UnityEngine.Random.Range(-90f, 90f),
                                UnityEngine.Random.Range(10f, 160f)
                            );
                        var start = end + new Vector2(-260f, top - end.y);
                        StartCoroutine(
                            Shoot(
                                BattleVfxShape.Arrow,
                                start,
                                end,
                                new Vector2(140f, 40f),
                                new Vector2(0.82f, 0.5f),
                                UnityEngine.Random.Range(0.18f, 0.26f),
                                0f,
                                color,
                                0f,
                                null
                            )
                        );
                    }
                }
                yield return Wait(0.24f);
                foreach (int index in list)
                {
                    var area = hits[index].Area;
                    var weight = land(index);
                    var feet = Feet(FrontLayer, area);
                    Puffs(
                        backSmoke,
                        feet + Vector2.up * 10f,
                        new Color(0.45f, 0.4f, 0.35f, 0.6f),
                        3,
                        70f,
                        0.8f,
                        90f
                    );
                    Sparks(Center(FrontLayer, area), color, 6, 0.5f, Vector2.down);
                    HitStop(Weighted(0.02f, weight));
                    Illuminate(
                        Center(FrontLayer, area),
                        color,
                        0.4f,
                        220f,
                        0.02f,
                        0.2f,
                        spill: false
                    );
                }
                Shake(0.25f, Vector2.down);
                yield return Wait(0.12f);
            }
            yield return Wait(0.3f);
        }

        /// <summary>
        /// シャドウスナイプ, silence then a hole: it goes almost black, a gold sight locks on slowly
        /// with a faint aiming line, then one arrow crosses in a blink and a line of light bursts
        /// through and past the enemy, shards blowing out of its back.
        /// </summary>
        private IEnumerator ShadowSnipe(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("ShadowSnipe");
            var from = Center(FrontLayer, caster) + new Vector2(50f, 20f);
            DimTo(0.78f, 0.25f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                Bloom(
                    frontLight,
                    BattleVfxShape.Reticle,
                    at,
                    new Vector2(300f, 300f),
                    Gold,
                    0.95f,
                    0.7f,
                    CoreBright,
                    spin: -90f
                );
                var aim = Spawn(
                    frontLight,
                    BattleVfxShape.Beam,
                    from,
                    new Vector2(Vector2.Distance(from, at), 14f),
                    new Color(color.r, color.g, color.b, 0.4f),
                    new Vector2(0f, 0.5f)
                );
                aim.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, Angle(at - from));
                Animate(
                    aim,
                    0.6f,
                    (s, k) =>
                        Show(
                            s,
                            new Color(
                                color.r,
                                color.g,
                                color.b,
                                0.25f + 0.25f * Mathf.Sin(k * 50f)
                            ),
                            1f,
                            0f
                        )
                );
                yield return Wait(0.6f);
                var dir = (at - from).normalized;
                yield return Shoot(
                    BattleVfxShape.Arrow,
                    from,
                    at,
                    new Vector2(320f, 90f),
                    new Vector2(0.82f, 0.5f),
                    0.06f,
                    0f,
                    Color.white,
                    0f,
                    null
                );
                var weight = land(index);
                Line(frontLight, at - dir * 120f, at + dir * 1100f, Color.white, 60f, 0.45f, 0.25f);
                Line(backLight, at - dir * 120f, at + dir * 1100f, color, 160f, 0.5f, 0.2f);
                Spray(
                    frontLight,
                    at,
                    new Burst
                    {
                        Shape = BattleVfxShape.Shard,
                        Bright = CoreBright,
                        From = Color.white,
                        To = new Color(color.r, color.g, color.b, 0f),
                        Speed = new Vector2(900f, 1800f),
                        Direction = Angle(dir),
                        Spread = 30f,
                        Life = new Vector2(0.2f, 0.4f),
                        Size = new Vector2(10f, 20f),
                        Aspect = 3f,
                        Drag = 3f,
                        Area = 20f,
                    },
                    18
                );
                Impact(area, at, color, weight, 1f, dir, flashScreen: true);
                HitStop(0.14f);
                Punch(at, 0.035f);
                if (weight != BattleHitWeight.Normal)
                    Ring(backLight, Feet(FrontLayer, area), Gold, 800f, 0.5f, floor: true);
            }
            yield return Wait(0.45f);
        }

        /// <summary>
        /// ライトニングボルト, a running flash: a jagged bolt leaps sideways from the user to the
        /// enemy, crackles run round it and it twitches with small sparks (numbed).
        /// </summary>
        private IEnumerator LightningBolt(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("LightningBolt");
            var from = Center(FrontLayer, caster) + new Vector2(60f, 30f);
            DimTo(0.5f, 0.12f);
            Glow(frontLight, from, color, 200f, 0.25f, 0.7f);
            yield return Wait(0.14f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                Zap(from, at, Color.white, 0.32f);
                var weight = land(index);
                Star(at, color, 240f, 0.2f);
                Impact(
                    area,
                    at,
                    color,
                    weight,
                    0.55f,
                    Vector2.right,
                    flashScreen: false,
                    rays: false
                );
                StartCoroutine(Crackle(area, color));
                StartCoroutine(Crackle(area, Violet));
            }
            yield return Wait(0.5f);
        }

        /// <summary>
        /// チェインライトニング, jumping: a bolt leaps from the user to the first enemy, then from
        /// each enemy hit to the next, each landing with its own flash, ring and light.
        /// </summary>
        private IEnumerator ChainLightning(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("ChainLightning");
            var from = Center(FrontLayer, caster) + new Vector2(60f, 30f);
            DimTo(0.55f, 0.12f);
            yield return Charge(caster, color, 0.2f, big: false);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                Zap(from, at, Color.white, 0.24f);
                var weight = land(index);
                Star(at, color, 220f, 0.18f);
                Ring(frontLight, at, color, 260f, 0.3f, floor: false);
                Sparks(at, color, 10, 0.8f, (at - from).normalized);
                HitStop(Weighted(0.04f, weight));
                Shake(Weighted(0.22f, weight), (at - from).normalized);
                Illuminate(at, color, 0.8f, 320f, 0.05f, 0.3f);
                from = at;
                yield return Wait(0.14f);
            }
            yield return Wait(0.3f);
        }

        /// <summary>
        /// 雷雲, gathering: dark clouds roll in over the enemies at the top of the screen, light
        /// flickering inside them with a rumble; they stay (and strike at the start of each party
        /// turn: <see cref="CloudStrike"/>).
        /// </summary>
        private IEnumerator Thundercloud(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("Thundercloud");
            DimTo(NightDim, 0.25f);
            yield return Charge(caster, Violet, 0.24f, big: true);
            float top = FrontLayer.rect.yMax - 80f;
            float x = 560f;
            Clouds(new Vector2(x, top), 18, 2.2f);
            Clouds(new Vector2(x, top - 60f), 8, 2f);
            yield return Wait(0.2f);
            for (int n = 0; n < 5; n++)
            {
                var spot = new Vector2(
                    x + UnityEngine.Random.Range(-300f, 300f),
                    top + UnityEngine.Random.Range(-40f, 20f)
                );
                Glow(backLight, spot, n % 2 == 0 ? Color.white : Violet, 420f, 0.16f, 1f);
                // A short bolt flickering between the clouds.
                Zap(
                    spot + new Vector2(-90f, 30f),
                    spot + new Vector2(80f, -50f),
                    n % 2 == 0 ? Color.white : Violet,
                    0.14f
                );
                Illuminate(spot, color, 0.5f, 600f, 0.02f, 0.18f, spill: false);
                Shake(0.12f, Vector2.zero);
                yield return Wait(0.16f);
            }
            foreach (int index in Wave(hits, 0))
                land(index);
            yield return Wait(0.4f);
        }

        /// <summary>
        /// 雷神の槍, judgement: the sky darkens, a spear of lightning forms high over the enemy and
        /// charges, then drops onto it with a bolt, a great ring and a stop; the lightning runs on
        /// along the floor to every enemy, striking each.
        /// </summary>
        private IEnumerator ThunderSpear(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("ThunderSpear");
            DimTo(NightDim, 0.2f);
            yield return Charge(caster, Violet, 0.22f, big: true);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var feet = Feet(FrontLayer, area);
                var high = new Vector2(at.x, FrontLayer.rect.yMax - 160f);
                Clouds(high + Vector2.up * 60f, 6, 1.4f);
                var spear = Spawn(
                    frontLight,
                    BattleVfxShape.IceSpear,
                    high,
                    new Vector2(420f, 120f),
                    color,
                    new Vector2(0.93f, 0.5f)
                );
                spear.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, -90f);
                Animate(
                    spear,
                    0.5f,
                    (s, k) =>
                    {
                        float pulse = 0.8f + 0.3f * Mathf.Sin(k * 60f);
                        s.Holder.localScale = Vector3.one * (0.6f + 0.4f * EaseOut(k));
                        Show(s, Color.Lerp(Color.white, color, 0.4f) * pulse, k, 0f, CoreBright);
                    }
                );
                Glow(frontLight, high, color, 360f, 0.5f, 0.8f);
                yield return Wait(0.5f);
                yield return Shoot(
                    BattleVfxShape.IceSpear,
                    high,
                    at,
                    new Vector2(420f, 120f),
                    new Vector2(0.93f, 0.5f),
                    0.06f,
                    0f,
                    color,
                    0f,
                    null
                );
                Bolt(feet);
                var weight = land(index);
                ScreenFlash(new Color(1f, 0.95f, 0.75f), MaxFlash);
                Ring(backLight, feet, Color.white, 600f, 0.4f, floor: true);
                Ring(backLight, feet, Violet, 900f, 0.55f, floor: true);
                Impact(area, at, color, Weighted2(weight), 1f, Vector2.down, flashScreen: false);
                HitStop(0.14f);
                Punch(at, 0.04f);
                StartCoroutine(Crackle(area, Violet));
                yield return Wait(0.2f);
                foreach (int other in Wave(hits, 1))
                {
                    var otherArea = hits[other].Area;
                    var otherFeet = Feet(FrontLayer, otherArea);
                    Zap(feet + Vector2.up * 10f, otherFeet + Vector2.up * 10f, color, 0.2f);
                    var otherWeight = land(other);
                    Star(Center(FrontLayer, otherArea), color, 200f, 0.16f);
                    Pool(otherFeet, color, 300f, 0.05f, 0.35f);
                    HitStop(Weighted(0.03f, otherWeight));
                    StartCoroutine(Crackle(otherArea, color));
                    yield return Wait(0.06f);
                }
            }
            yield return Wait(0.4f);
        }

        /// <summary>
        /// 疾風迅雷, light on the feet: a gust sweeps through the party from the left, white streaks
        /// and feathers riding it, and each ally twitches with yellow sparks under an arrow
        /// pointing up.
        /// </summary>
        private IEnumerator Gale(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("Gale");
            DimTo(0.36f, 0.15f);
            var mid = MidFeet(hits, 0) + Vector2.up * 120f;
            // Bands of wind racing across the party from the left, one after another.
            for (int n = 0; n < 4; n++)
            {
                float y = mid.y - 180f + n * 110f;
                Line(
                    frontLight,
                    new Vector2(mid.x - 700f, y),
                    new Vector2(mid.x + 500f, y + 30f),
                    Color.Lerp(Color.white, color, 0.4f),
                    26f,
                    0.4f,
                    0.35f
                );
                Spray(
                    frontLight,
                    new Vector2(mid.x - 600f, y),
                    new Burst
                    {
                        Shape = BattleVfxShape.Streak,
                        Bright = CoreBright,
                        From = Color.white,
                        To = new Color(color.r, color.g, color.b, 0f),
                        Speed = new Vector2(1600f, 2400f),
                        Direction = 2f,
                        Spread = 5f,
                        Life = new Vector2(0.4f, 0.6f),
                        Size = new Vector2(8f, 14f),
                        Stretch = 0.08f,
                        Area = 50f,
                        Delay = new Vector2(0f, 0.15f),
                    },
                    8
                );
                yield return Wait(0.05f);
            }
            Leaves(frontLight, new Vector2(mid.x - 500f, mid.y), Color.white, 12, 4f, 1100f, 40f);
            yield return Wait(0.2f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                StartCoroutine(Crackle(area, color));
                Sparks(at, color, 10, 0.6f, Vector2.up);
                Bloom(
                    frontLight,
                    BattleVfxShape.Chevron,
                    at + new Vector2(0f, 60f),
                    new Vector2(150f, 200f),
                    color,
                    0.8f,
                    0.6f,
                    CoreBright
                );
                Illuminate(at, color, 0.6f, 220f, 0.1f, 0.3f, spill: false);
                land(index);
                yield return Wait(0.05f);
            }
            yield return Wait(0.45f);
        }

        /// <summary>
        /// 偵察, a look round: a teal sight sweeps across the enemy side, and two twinkles fly back
        /// up from it (the cards drawn).
        /// </summary>
        private IEnumerator Scout(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("Scout");
            var at = Center(FrontLayer, caster);
            DimTo(0.32f, 0.15f);
            Ring(backLight, Feet(FrontLayer, caster), color, 280f, 0.4f, floor: true);
            var sight = Spawn(
                frontLight,
                BattleVfxShape.Reticle,
                new Vector2(300f, 60f),
                new Vector2(220f, 220f),
                color
            );
            Animate(
                sight,
                0.7f,
                (s, k) =>
                {
                    s.Holder.anchoredPosition = new Vector2(
                        Mathf.Lerp(300f, 820f, EaseOut(k)),
                        60f + Mathf.Sin(k * 7f) * 60f
                    );
                    s.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, k * 120f);
                    Show(s, color, 0.2f + 0.3f * k, Eaten(k, 0.75f), CoreBright);
                }
            );
            yield return Wait(0.6f);
            for (int n = 0; n < 2; n++)
                StartCoroutine(
                    Shoot(
                        BattleVfxShape.Sparkle,
                        new Vector2(820f, 60f),
                        at + new Vector2(-40f + n * 80f, 160f),
                        new Vector2(60f, 60f),
                        new Vector2(0.5f, 0.5f),
                        0.35f,
                        200f,
                        Color.white,
                        0f,
                        null
                    )
                );
            yield return Wait(0.36f);
            Twinkle(at + new Vector2(0f, 160f), color, 150f, 0.35f, 0f, 1f);
            foreach (int index in Wave(hits, 0))
                land(index);
            yield return Wait(0.3f);
        }

        // --- Mina ------------------------------------------------------------------------------

        /// <summary>
        /// 万歩の祈り, every step counting: golden footprints light up along the floor through the
        /// party, then a green light rises from each ally with leaves and twinkles drifting up.
        /// </summary>
        private IEnumerator StepPrayer(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("StepPrayer");
            DimTo(0.3f, 0.2f);
            yield return Charge(caster, color, 0.2f, big: false);
            var wave = Wave(hits, 0);
            if (wave.Count > 0)
            {
                var a = Feet(FrontLayer, hits[wave[0]].Area);
                var b = Feet(FrontLayer, hits[wave[^1]].Area);
                Footsteps(
                    a + new Vector2(-200f, -20f),
                    b + new Vector2(200f, -20f),
                    Gold,
                    8,
                    0.05f
                );
            }
            yield return Wait(0.4f);
            foreach (int index in wave)
            {
                var area = hits[index].Area;
                var feet = Feet(FrontLayer, area);
                Column(
                    backLight,
                    feet,
                    color,
                    new Vector2(300f, 380f),
                    1f,
                    down: false,
                    fast: false
                );
                Leaves(frontLight, feet + Vector2.up * 30f, color, 5, 90f, 120f);
                Pool(feet, color, 300f, 0.4f, 0.5f);
                Illuminate(
                    Center(FrontLayer, area),
                    Color.Lerp(color, Gold, 0.3f),
                    0.6f,
                    260f,
                    0.4f,
                    0.5f,
                    spill: false
                );
                land(index);
                yield return Wait(0.05f);
            }
            yield return Wait(0.5f);
        }

        /// <summary>
        /// リジェネ, budding: a soft green ring opens under the ally and leaves spiral up round it,
        /// a gentle glow settling in.
        /// </summary>
        private IEnumerator Regen(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("Regen");
            DimTo(0.28f, 0.2f);
            yield return Charge(caster, color, 0.2f, big: false);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var feet = Feet(FrontLayer, area);
                Ring(backLight, feet, color, 260f, 0.7f, floor: true);
                Spiral(feet, color, 10, 1f);
                Glow(backLight, Center(FrontLayer, area), color, 300f, 1f, 0.45f);
                land(index);
            }
            yield return Wait(0.7f);
        }

        /// <summary>
        /// リザレクション, a call from above: the stage dims, a white-gold circle opens under the
        /// ally and a beam of light comes slowly down onto it with feathers drifting; the ally
        /// flares white and a ring and twinkles burst out.
        /// </summary>
        private IEnumerator Resurrection(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("Resurrection");
            DimTo(0.6f, 0.3f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var feet = Feet(FrontLayer, area);
                var at = Center(FrontLayer, area);
                Bloom(
                    backLight,
                    BattleVfxShape.MagicCircle,
                    feet,
                    new Vector2(360f, 360f),
                    color,
                    1.6f,
                    0.75f,
                    ErodeBright * 1.2f,
                    floor: true,
                    spin: -120f
                );
                float top = FrontLayer.rect.yMax + 40f;
                Column(
                    frontLight,
                    feet,
                    color,
                    new Vector2(240f, (top - feet.y) / 0.9f),
                    1.4f,
                    down: true,
                    fast: false
                );
                Leaves(frontLight, new Vector2(at.x, top - 80f), Color.white, 8, -90f, 160f, 60f);
                yield return Wait(0.6f);
                land(index);
                Illuminate(at, Color.white, 1f, 360f, 0.4f, 0.6f);
                Ring(backLight, feet, color, 520f, 0.6f, floor: true);
                Spray(
                    frontLight,
                    at,
                    new Burst
                    {
                        Shape = BattleVfxShape.Sparkle,
                        Bright = CoreBright,
                        From = Color.white,
                        To = new Color(1f, 0.9f, 0.6f, 0f),
                        Speed = new Vector2(100f, 400f),
                        Spread = 360f,
                        Life = new Vector2(0.6f, 1f),
                        Size = new Vector2(16f, 36f),
                        Gravity = -120f,
                        Drag = 2f,
                        Spin = 200f,
                        Area = 40f,
                    },
                    20
                );
                ScreenFlash(Color.white, 0.18f);
            }
            yield return Wait(0.6f);
        }

        /// <summary>
        /// 聖なる鉄槌, judgement: a golden circle opens in the air over the enemy and a heavy column
        /// of gold slams down from it; the floor cracks gold, chips fly, and pale shards of a broken
        /// blessing fly off the enemy.
        /// </summary>
        private IEnumerator HolyHammer(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("HolyHammer");
            DimTo(SpellDim, 0.15f);
            Glow(frontLight, Center(FrontLayer, caster), color, 240f, 0.4f, 0.6f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var feet = Feet(FrontLayer, area);
                var sky = new Vector2(at.x, FrontLayer.rect.yMax - 140f);
                Bloom(
                    frontLight,
                    BattleVfxShape.MagicCircle,
                    sky,
                    new Vector2(320f, 320f),
                    color,
                    0.9f,
                    0.6f,
                    CoreBright,
                    spin: 160f
                );
                yield return Wait(0.35f);
                Column(
                    frontLight,
                    feet,
                    color,
                    new Vector2(300f, (sky.y - feet.y) / 0.9f),
                    0.6f,
                    down: true,
                    fast: true
                );
                yield return Wait(0.06f);
                var weight = land(index);
                Bloom(
                    backSmoke,
                    BattleVfxShape.Crack,
                    feet,
                    new Vector2(420f, 420f),
                    color,
                    0.9f,
                    0.5f,
                    CoreBright,
                    floor: true
                );
                Impact(area, at, color, weight, 0.85f, Vector2.down, flashScreen: true);
                Debris(feet + Vector2.up * 20f, new Color(0.5f, 0.42f, 0.3f), 10, Vector2.up);
                Spray(
                    frontLight,
                    at,
                    new Burst
                    {
                        Shape = BattleVfxShape.Shard,
                        Bright = ErodeBright,
                        From = new Color(0.8f, 0.9f, 1f),
                        To = new Color(0.6f, 0.75f, 1f, 0f),
                        Speed = new Vector2(200f, 500f),
                        Direction = 90f,
                        Spread = 140f,
                        Life = new Vector2(0.5f, 0.8f),
                        Size = new Vector2(10f, 20f),
                        Aspect = 2.2f,
                        Gravity = 500f,
                        Drag = 1f,
                        Spin = 400f,
                        Area = 40f,
                        Delay = new Vector2(0.1f, 0.2f),
                    },
                    12
                );
            }
            yield return Wait(0.45f);
        }

        /// <summary>
        /// プロテクト, gently wrapped: small crests circle each ally and fold into a soft bubble of
        /// pale light round it.
        /// </summary>
        private IEnumerator Protect(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("Protect");
            DimTo(0.32f, 0.2f);
            yield return Charge(caster, color, 0.2f, big: false);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                for (int n = 0; n < 4; n++)
                {
                    var crest = Spawn(
                        frontLight,
                        BattleVfxShape.Crest,
                        at,
                        new Vector2(60f, 74f),
                        color
                    );
                    float phase = n * 90f;
                    Animate(
                        crest,
                        0.7f,
                        (s, k) =>
                        {
                            float angle = (phase + k * 400f) * Mathf.Deg2Rad;
                            float radius = 140f * (1f - 0.7f * EaseIn(k));
                            s.Holder.anchoredPosition =
                                at
                                + new Vector2(
                                    Mathf.Cos(angle) * radius,
                                    Mathf.Sin(angle) * radius * 0.4f
                                );
                            Show(s, color, k, Eaten(k, 0.7f), ErodeBright);
                        }
                    );
                }
                Bloom(
                    backLight,
                    BattleVfxShape.Bubble,
                    at,
                    new Vector2(260f, 300f),
                    Color.Lerp(color, Gold, 0.3f),
                    1.1f,
                    0.7f,
                    ErodeBright,
                    delay: 0.45f
                );
                land(index);
                yield return Wait(0.04f);
            }
            yield return Wait(0.8f);
        }

        /// <summary>
        /// 浄化の光, clearing: a light rain of twinkles falls over the party, dark wisps rise off
        /// each ally and fade, and a clear ring and glow are left.
        /// </summary>
        private IEnumerator Purify(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("Purify");
            DimTo(0.3f, 0.2f);
            var mid = MidFeet(hits, 0);
            Spray(
                frontLight,
                new Vector2(mid.x, FrontLayer.rect.yMax),
                new Burst
                {
                    Shape = BattleVfxShape.Sparkle,
                    From = Color.white,
                    To = new Color(0.6f, 0.95f, 1f, 0f),
                    Speed = new Vector2(150f, 300f),
                    Direction = -90f,
                    Spread = 20f,
                    Life = new Vector2(0.9f, 1.3f),
                    Size = new Vector2(12f, 26f),
                    Spin = 120f,
                    Area = 360f,
                    Delay = new Vector2(0f, 0.5f),
                },
                30
            );
            yield return Wait(0.35f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                Puffs(backSmoke, at, new Color(0.22f, 0.12f, 0.28f, 0.5f), 4, 50f, 1f, 90f);
                Ring(
                    backLight,
                    Feet(FrontLayer, area),
                    new Color(0.6f, 0.95f, 1f),
                    280f,
                    0.6f,
                    floor: true
                );
                Glow(frontLight, at, new Color(0.7f, 0.95f, 1f), 260f, 0.8f, 0.45f);
                land(index);
                yield return Wait(0.04f);
            }
            yield return Wait(0.6f);
        }

        /// <summary>
        /// マナの祈り, filling up: blue motes are drawn into the user, a blue column rises and two
        /// orbs of light float up and away (the energy).
        /// </summary>
        private IEnumerator ManaPrayer(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("ManaPrayer");
            yield return Charge(caster, color, 0.3f, big: false);
            var at = Center(FrontLayer, caster);
            Column(
                backLight,
                Feet(FrontLayer, caster),
                color,
                new Vector2(240f, 360f),
                0.9f,
                down: false,
                fast: false
            );
            for (int n = 0; n < 2; n++)
                StartCoroutine(
                    Shoot(
                        BattleVfxShape.Glow,
                        at,
                        at + new Vector2(-120f + n * 60f, 360f),
                        new Vector2(70f, 70f),
                        new Vector2(0.5f, 0.5f),
                        0.55f,
                        40f,
                        color,
                        0f,
                        null
                    )
                );
            Illuminate(at, color, 0.6f, 260f, 0.3f, 0.4f, spill: false);
            foreach (int index in Wave(hits, 0))
                land(index);
            yield return Wait(0.6f);
        }

        /// <summary>
        /// 天啓, a flash of insight: a thin shaft of gold comes down on the user, a great twinkle
        /// flares over its head and a gold ring spreads at its feet.
        /// </summary>
        private IEnumerator Revelation(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("Revelation");
            DimTo(0.42f, 0.2f);
            var feet = Feet(FrontLayer, caster);
            var head = Bounds(FrontLayer, caster).max;
            Column(
                frontLight,
                feet,
                color,
                new Vector2(140f, (FrontLayer.rect.yMax + 40f - feet.y) / 0.9f),
                0.9f,
                down: true,
                fast: false
            );
            yield return Wait(0.35f);
            Twinkle(
                new Vector2((feet.x + head.x) * 0.5f, head.y + 30f),
                Color.white,
                260f,
                0.55f,
                0f,
                1f
            );
            Ring(backLight, feet, color, 360f, 0.5f, floor: true);
            Illuminate(feet + Vector2.up * 100f, color, 0.7f, 300f, 0.2f, 0.4f);
            foreach (int index in Wave(hits, 0))
                land(index);
            yield return Wait(0.5f);
        }

        /// <summary>
        /// ホーリーライト, a flood of light: a white-gold star blooms high in the middle, rays shoot
        /// from it to every enemy and strike, then a soft light falls on every ally as they heal.
        /// </summary>
        private IEnumerator HolyLight(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("HolyLight");
            yield return Charge(caster, color, 0.26f, big: false);
            var source = new Vector2(0f, FrontLayer.rect.yMax - 200f);
            var star = Spawn(
                frontLight,
                BattleVfxShape.Star,
                source,
                new Vector2(520f, 520f),
                color
            );
            Animate(
                star,
                0.9f,
                (s, k) =>
                {
                    float grow = BackOut(Mathf.Min(1f, k * 4f));
                    s.Holder.localScale = new Vector3(grow, grow, 1f);
                    s.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, k * 40f);
                    Show(s, color, k, Eaten(k, 0.6f), CoreBright);
                }
            );
            Glow(backLight, source, color, 700f, 0.9f, 0.6f);
            Illuminate(source, color, 0.8f, 1200f, 0.4f, 0.5f);
            yield return Wait(0.22f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                Line(frontLight, source, at, Color.white, 50f, 0.35f, 0.3f);
                var weight = land(index);
                Star(at, color, 260f, 0.2f);
                Impact(area, at, color, weight, 0.55f, Vector2.down, flashScreen: false);
            }
            yield return Wait(0.25f);
            foreach (int index in Wave(hits, 1))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                Spray(
                    frontLight,
                    at + Vector2.up * 160f,
                    new Burst
                    {
                        Shape = BattleVfxShape.Sparkle,
                        From = Color.white,
                        To = new Color(1f, 0.92f, 0.6f, 0f),
                        Speed = new Vector2(60f, 160f),
                        Direction = -90f,
                        Spread = 30f,
                        Life = new Vector2(0.6f, 0.9f),
                        Size = new Vector2(14f, 28f),
                        Spin = 120f,
                        Area = 60f,
                    },
                    8
                );
                Glow(backLight, at, ColorOf(BattleSkillVfxKind.Heal), 240f, 0.7f, 0.4f);
                land(index);
            }
            yield return Wait(0.5f);
        }

        /// <summary>
        /// ディバインシールド, absolute: a golden bubble springs up round the ally, a great crest
        /// flashes before it, feathers drift down and a gold ring spreads; the bubble holds a while.
        /// </summary>
        private IEnumerator DivineShield(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("DivineShield");
            DimTo(0.42f, 0.2f);
            yield return Charge(caster, color, 0.22f, big: false);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var feet = Feet(FrontLayer, area);
                Bloom(
                    backLight,
                    BattleVfxShape.Bubble,
                    at,
                    new Vector2(320f, 360f),
                    color,
                    1.6f,
                    0.8f,
                    CoreBright
                );
                Bloom(
                    frontLight,
                    BattleVfxShape.Bubble,
                    at,
                    new Vector2(320f, 360f),
                    new Color(color.r, color.g, color.b, 0.3f),
                    1.6f,
                    0.8f,
                    1f
                );
                Bloom(
                    frontLight,
                    BattleVfxShape.Crest,
                    at + new Vector2(110f, 0f),
                    new Vector2(180f, 220f),
                    Color.white,
                    0.6f,
                    0.5f,
                    CoreBright
                );
                Ring(backLight, feet, color, 420f, 0.6f, floor: true);
                Leaves(frontLight, at + Vector2.up * 220f, Color.white, 6, -90f, 120f, 60f);
                Illuminate(at, color, 0.8f, 320f, 0.4f, 0.6f);
                Shake(0.1f, Vector2.zero);
                land(index);
            }
            yield return Wait(0.7f);
        }

        // --- Statuses that act later -----------------------------------------------------------

        /// <summary>
        /// A status working on a character at the start of a turn: a burn licks at it, poison
        /// bubbles up, a wound bleeds, or regen lets leaves rise. <paramref name="land"/> is
        /// called as it takes effect.
        /// </summary>
        public IEnumerator StatusTick(CardStatus status, RectTransform target, Action land)
        {
            if (FrontLayer == null || target == null)
            {
                land?.Invoke();
                yield break;
            }
            var at = Center(FrontLayer, target);
            switch (status)
            {
                case CardStatus.Burn:
                    Licks(target, 6, 0.7f);
                    Illuminate(
                        at,
                        ColorOf(BattleSkillVfxKind.Fire),
                        0.6f,
                        240f,
                        0.2f,
                        0.3f,
                        spill: false,
                        flicker: 0.4f
                    );
                    break;
                case CardStatus.Poison:
                    Bubbles(at, Venom, Violet, 8);
                    Glow(frontLight, at, Venom, 200f, 0.4f, 0.4f);
                    break;
                case CardStatus.Bleed:
                    Drops(at, Blood, 10);
                    Star(at, Blood, 140f, 0.14f);
                    break;
                case CardStatus.Regen:
                    Spiral(Feet(FrontLayer, target), ColorOf(BattleSkillVfxKind.Heal), 6, 0.8f);
                    Glow(backLight, at, ColorOf(BattleSkillVfxKind.Heal), 240f, 0.6f, 0.4f);
                    break;
            }
            yield return Wait(0.12f);
            land?.Invoke();
            Shake(status == CardStatus.Regen ? 0f : 0.1f, Vector2.zero);
            yield return Wait(0.3f);
        }

        /// <summary>
        /// The blast sigil on <paramref name="marked"/> blowing up: it flares and swells, then every
        /// enemy is caught in an explosion. <paramref name="land"/> is called with each enemy's index.
        /// </summary>
        public IEnumerator Detonate(
            RectTransform marked,
            IReadOnlyList<RectTransform> enemies,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("BlastSigil");
            DimTo(SpellDim, 0.12f);
            if (marked != null && FrontLayer != null)
            {
                var at = Center(FrontLayer, marked);
                var sigil = Spawn(
                    frontLight,
                    BattleVfxShape.MagicCircle,
                    at,
                    new Vector2(160f, 160f),
                    color
                );
                Animate(
                    sigil,
                    0.4f,
                    (s, k) =>
                    {
                        float grow = 1f + 2.5f * EaseIn(k);
                        s.Holder.localScale = new Vector3(grow, grow, 1f);
                        s.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, -k * 500f);
                        Show(s, Color.Lerp(color, Color.white, k), 1f, Eaten(k, 0.7f), CoreBright);
                    }
                );
                Illuminate(at, color, 0.8f, 400f, 0.3f, 0.2f, flicker: 0.5f);
                yield return Wait(0.38f);
            }
            ScreenFlash(new Color(1f, 0.8f, 0.5f), 0.25f);
            for (int i = 0; i < enemies.Count; i++)
            {
                var weight = land(i);
                Explosion(enemies[i], Center(FrontLayer, enemies[i]), weight);
                Impact(
                    enemies[i],
                    Center(FrontLayer, enemies[i]),
                    color,
                    weight,
                    0.7f,
                    Vector2.up,
                    flashScreen: false,
                    rays: false
                );
                yield return Wait(0.05f);
            }
            yield return Wait(0.5f);
            DimTo(0f, 0.4f);
        }

        /// <summary>
        /// The thundercloud striking <paramref name="target"/>: a dark cloud gathers over it and a
        /// bolt comes down with a ring and a light.
        /// </summary>
        public IEnumerator CloudStrike(RectTransform target, Func<BattleHitWeight> land)
        {
            var color = ColorOf("Thundercloud");
            if (FrontLayer == null || target == null)
            {
                land?.Invoke();
                yield break;
            }
            DimTo(SpellDim, 0.12f);
            var feet = Feet(FrontLayer, target);
            var at = Center(FrontLayer, target);
            Clouds(new Vector2(at.x, FrontLayer.rect.yMax - 80f), 5, 1.1f);
            yield return Wait(0.25f);
            Bolt(feet);
            var weight = land?.Invoke() ?? BattleHitWeight.Normal;
            Star(at, color, 260f, 0.2f);
            Ring(backLight, feet, Violet, 460f, 0.4f, floor: true);
            Illuminate(at, color, 0.9f, 420f, 0.1f, 0.4f);
            HitStop(Weighted(0.05f, weight));
            Shake(Weighted(0.3f, weight), Vector2.down);
            StartCoroutine(Crackle(target, Violet));
            yield return Wait(0.45f);
            DimTo(0f, 0.4f);
        }

        // --- Building blocks of the card effects -----------------------------------------------

        private static IEnumerator After(float seconds, Action then)
        {
            yield return Wait(seconds);
            then();
        }

        /// <summary>The indexes of the hits in <paramref name="wave"/>, in order.</summary>
        private static List<int> Wave(IReadOnlyList<BattleSkillHit> hits, int wave)
        {
            var list = new List<int>();
            for (int i = 0; i < hits.Count; i++)
                if (hits[i].Wave == wave)
                    list.Add(i);
            return list;
        }

        private static int Waves(IReadOnlyList<BattleSkillHit> hits)
        {
            int waves = 0;
            foreach (var hit in hits)
                waves = Mathf.Max(waves, hit.Wave + 1);
            return waves;
        }

        /// <summary>The middle of the feet of a wave's targets (the enemies' side when empty).</summary>
        private Vector2 MidFeet(IReadOnlyList<BattleSkillHit> hits, int wave)
        {
            var sum = Vector2.zero;
            int count = 0;
            foreach (int index in Wave(hits, wave))
            {
                sum += Feet(FrontLayer, hits[index].Area);
                count++;
            }
            return count > 0 ? sum / count : new Vector2(560f, -120f);
        }

        /// <summary>A defeat counts as a weakness for the shapes sized by weight, a little bigger still.</summary>
        private static BattleHitWeight Weighted2(BattleHitWeight weight) =>
            weight == BattleHitWeight.Normal ? BattleHitWeight.Weak : BattleHitWeight.Defeat;

        /// <summary>
        /// A shape that springs to its size (overshooting a little) after <paramref name="delay"/>,
        /// holds until <paramref name="hold"/> of its life and is eaten away; turned by
        /// <paramref name="angle"/>, turning by <paramref name="spin"/> degrees over its life, and
        /// laid on the floor when <paramref name="floor"/>.
        /// </summary>
        private Shot Bloom(
            RectTransform layer,
            BattleVfxShape shape,
            Vector2 at,
            Vector2 size,
            Color color,
            float seconds,
            float hold,
            float bright,
            float angle = 0f,
            bool floor = false,
            float spin = 0f,
            float delay = 0f
        )
        {
            if (layer == null)
                return null;
            var shot = Spawn(layer, shape, at, size, Color.clear);
            shot.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
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
                    float grow = BackOut(Mathf.Min(1f, k * 5f));
                    s.Holder.localScale = new Vector3(grow, grow * (floor ? FloorTilt : 1f), 1f);
                    if (spin != 0f)
                        s.Image.rectTransform.localEulerAngles = new Vector3(
                            0f,
                            0f,
                            angle + spin * k
                        );
                    Show(s, color, k, Eaten(k, hold), bright);
                }
            );
            return shot;
        }

        /// <summary>
        /// Flies a shape (its <paramref name="head"/> at the front) from one point to another on an
        /// arc of <paramref name="lift"/> px, turned along its path (plus <paramref name="turnBy"/>
        /// degrees) unless <paramref name="turn"/> is off, shedding <paramref name="trail"/> as it goes.
        /// </summary>
        private IEnumerator Shoot(
            BattleVfxShape shape,
            Vector2 from,
            Vector2 to,
            Vector2 size,
            Vector2 head,
            float seconds,
            float lift,
            Color color,
            float turnBy,
            Burst? trail,
            bool turn = true
        )
        {
            var shot = Spawn(frontLight, shape, from, size, color, head);
            float shed = 0f;
            var last = from;
            if (!turn)
                shot.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, turnBy);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                float ease = k * k * (1.4f - 0.4f * k);
                var position =
                    Vector2.Lerp(from, to, ease) + Vector2.up * (Mathf.Sin(k * Mathf.PI) * lift);
                var heading = position - last;
                if (turn && heading.sqrMagnitude > 0.01f)
                    shot.Image.rectTransform.localEulerAngles = new Vector3(
                        0f,
                        0f,
                        Angle(heading) + turnBy
                    );
                shot.Holder.anchoredPosition = position;
                Show(shot, color, k, 0f, CoreBright);
                if (trail.HasValue)
                {
                    shed += Time.deltaTime;
                    while (shed > 0.03f)
                    {
                        shed -= 0.03f;
                        var spray = trail.Value;
                        spray.Direction = Angle(-heading);
                        Spray(frontLight, position, spray, 1);
                    }
                }
                last = position;
                yield return null;
            }
            shot.Live = false;
            shot.Holder.gameObject.SetActive(false);
        }

        /// <summary>
        /// A beam from one point to another, <paramref name="width"/> px thick, drawn in from
        /// <paramref name="from"/> fast and worn away after <paramref name="hold"/> of its life.
        /// </summary>
        private void Line(
            RectTransform layer,
            Vector2 from,
            Vector2 to,
            Color color,
            float width,
            float seconds,
            float hold
        )
        {
            if (layer == null)
                return;
            var shot = Spawn(
                layer,
                BattleVfxShape.Beam,
                from,
                new Vector2(Vector2.Distance(from, to), width),
                color,
                new Vector2(0f, 0.5f)
            );
            shot.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, Angle(to - from));
            Animate(
                shot,
                seconds,
                (s, k) => Show(s, color, Mathf.Min(1f, k * 1.2f), Eaten(k, hold), CoreBright)
            );
        }

        /// <summary>A bolt of lightning between two points, its foot at <paramref name="to"/>, striking twice.</summary>
        private void Zap(Vector2 from, Vector2 to, Color color, float seconds)
        {
            float distance = Vector2.Distance(from, to);
            float height = distance / 0.88f;
            var shot = Spawn(
                frontLight,
                BattleVfxShape.Lightning,
                to,
                new Vector2(Mathf.Min(height * 0.62f, 300f), height),
                color,
                new Vector2(0.5f, 0.09f)
            );
            shot.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, Angle(from - to) - 90f);
            float seed = shot.Seed;
            Animate(
                shot,
                seconds,
                (s, k) =>
                {
                    s.Seed = Mathf.Repeat(seed + Mathf.Floor(k * 2.5f) * 0.318f, 1f);
                    Show(s, color, k, Eaten(k, 0.55f), CoreBright);
                }
            );
            Illuminate(
                Vector2.Lerp(from, to, 0.5f),
                ColorOf(BattleSkillVfxKind.Thunder),
                0.5f,
                distance * 0.6f + 200f,
                0.03f,
                0.2f,
                spill: false
            );
        }

        /// <summary>
        /// A column of light (or of fire) standing on <paramref name="feet"/>: rising out of the
        /// floor, or coming <paramref name="down"/> from above onto it; <paramref name="fast"/>
        /// ones slam in.
        /// </summary>
        private void Column(
            RectTransform layer,
            Vector2 feet,
            Color color,
            Vector2 size,
            float seconds,
            bool down,
            bool fast,
            bool fire = false
        )
        {
            if (layer == null)
                return;
            // Coming down, it grows from its top edge, so its foot reaches the feet last.
            var shot = Spawn(
                layer,
                BattleVfxShape.Pillar,
                down ? feet + Vector2.up * (size.y * 0.9f) : feet,
                size,
                color,
                down ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.1f)
            );
            float rise = fast ? 0.1f : 0.3f;
            Animate(
                shot,
                seconds,
                (s, k) =>
                {
                    float grow = EaseOut(Mathf.Min(1f, k * seconds / rise));
                    s.Holder.localScale = new Vector3(0.7f + 0.3f * grow, 0.05f + 0.95f * grow, 1f);
                    var tint = fire
                        ? Color.Lerp(Color.white, FireColor(k * 0.6f), 0.4f + 0.6f * k)
                        : color;
                    Show(s, tint, k, Eaten(k, 0.5f), fast ? CoreBright : ErodeBright);
                }
            );
        }

        /// <summary>
        /// Footprints of light pressed into the floor one after another from one point to another,
        /// left and right by turns; returns where they are.
        /// </summary>
        private List<Vector2> Footsteps(Vector2 from, Vector2 to, Color color, int count, float gap)
        {
            var prints = new List<Vector2>();
            for (int n = 0; n < count; n++)
            {
                var at =
                    Vector2.Lerp(from, to, (n + 0.5f) / count)
                    + new Vector2(0f, n % 2 == 0 ? 10f : -10f);
                prints.Add(at);
                StartCoroutine(
                    After(n * gap, () => Ring(backLight, at, color, 120f, 0.3f, floor: true))
                );
                var print = Spawn(
                    backLight,
                    BattleVfxShape.Glow,
                    at,
                    new Vector2(70f, 110f),
                    Color.clear
                );
                print.Holder.localScale = new Vector3(1f, FloorTilt * 1.6f, 1f);
                print.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
                float delay = n * gap;
                Animate(
                    print,
                    1.1f + delay,
                    (s, t) =>
                    {
                        float time = t * (1.1f + delay) - delay;
                        float alpha =
                            time < 0f ? 0f
                            : time < 0.05f ? time / 0.05f
                            : 1f - EaseIn((time - 0.05f) / 1.05f);
                        // A white flash as it is pressed, then the colour.
                        var tint = Color.Lerp(Color.white, color, Mathf.Clamp01(time / 0.15f));
                        Show(
                            s,
                            new Color(tint.r, tint.g, tint.b, alpha),
                            0f,
                            0f,
                            CoreBright * 1.3f
                        );
                    }
                );
            }
            return prints;
        }

        /// <summary>
        /// Motes of light drawn in to <paramref name="center"/> from a ring
        /// <paramref name="radius"/> px round it, arriving over about <paramref name="seconds"/>.
        /// </summary>
        private void Gather(Vector2 center, Color color, int count, float radius, float seconds)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                var start =
                    center
                    + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                        * radius
                        * UnityEngine.Random.Range(0.7f, 1f);
                var particle = Take(
                    frontLight,
                    i % 3 == 0 ? BattleVfxShape.Sparkle : BattleVfxShape.Spark
                );
                float life = seconds * UnityEngine.Random.Range(0.6f, 1f);
                particle.Position = start;
                particle.Velocity = (center - start) / life;
                particle.Life = life;
                float size =
                    i % 3 == 0
                        ? UnityEngine.Random.Range(20f, 32f)
                        : UnityEngine.Random.Range(10f, 16f);
                particle.Size = new Vector2(size, size);
                particle.EndScale = 0.3f;
                particle.From = new Color(color.r, color.g, color.b, 0.2f);
                particle.To = Color.white;
                particle.Spin = 0f;
                particle.Gravity = 0f;
                particle.Drag = 0f;
                particle.Stretch = 0f;
                particle.Apply();
            }
        }

        /// <summary>Scraps (of armour, rock) thrown from a blow and falling, mostly along <paramref name="direction"/>.</summary>
        private void Debris(Vector2 at, Color color, int count, Vector2 direction) =>
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Chip,
                    From = color,
                    To = new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, 0f),
                    Speed = new Vector2(300f, 800f),
                    Direction = Angle(direction),
                    Spread = 130f,
                    Life = new Vector2(0.5f, 0.9f),
                    Size = new Vector2(10f, 24f),
                    Gravity = 1600f,
                    Drag = 0.8f,
                    Spin = 720f,
                    Area = 30f,
                },
                count
            );

        /// <summary>Drops of blood (or of light) that spring from a wound and fall.</summary>
        private void Drops(Vector2 at, Color color, int count) =>
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Spark,
                    Bright = ErodeBright,
                    From = color,
                    To = new Color(color.r * 0.5f, color.g * 0.2f, color.b * 0.2f, 0f),
                    Speed = new Vector2(120f, 320f),
                    Direction = 90f,
                    Spread = 120f,
                    Life = new Vector2(0.4f, 0.7f),
                    Size = new Vector2(8f, 14f),
                    Gravity = 1200f,
                    Drag = 0.5f,
                    Area = 30f,
                },
                count
            );

        /// <summary>Little flames clinging to a character and flickering up, embers rising from them.</summary>
        private void Licks(RectTransform target, int count, float seconds)
        {
            var (low, high) = Bounds(FrontLayer, target);
            var middle = (low + high) * 0.5f;
            Spray(
                frontLight,
                new Vector2(middle.x, low.y + (high.y - low.y) * 0.3f),
                new Burst
                {
                    Shape = BattleVfxShape.FlameTongue,
                    Bright = ErodeBright,
                    Fire = true,
                    Speed = new Vector2(30f, 90f),
                    Direction = 90f,
                    Spread = 30f,
                    Life = new Vector2(seconds * 0.5f, seconds),
                    Size = new Vector2(34f, 64f),
                    Grow = 0.4f,
                    Shrink = 1f,
                    Gravity = -60f,
                    Drag = 1f,
                    Spin = 10f,
                    Hold = 0.3f,
                    Area = (high.x - low.x) * 0.35f,
                    Delay = new Vector2(0f, seconds * 0.4f),
                },
                count
            );
            Spray(frontLight, middle, Embered(ColorOf(BattleSkillVfxKind.Fire)), count);
        }

        /// <summary>Embers rising and cooling: the trail of fire, and the sparks over a burn.</summary>
        private static Burst Embered(Color color) =>
            new()
            {
                Shape = BattleVfxShape.Spark,
                Bright = CoreBright,
                From = new Color(1f, 0.92f, 0.6f),
                To = new Color(color.r, color.g * 0.5f, color.b * 0.3f, 0f),
                Speed = new Vector2(30f, 140f),
                Direction = 90f,
                Spread = 90f,
                Life = new Vector2(0.3f, 0.7f),
                Size = new Vector2(6f, 12f),
                Gravity = -160f,
                Drag = 2f,
                Area = 30f,
            };

        /// <summary>Bubbles of poison rising from a character and bursting, in two colours.</summary>
        private void Bubbles(Vector2 at, Color color, Color other, int count)
        {
            for (int n = 0; n < count; n++)
                Spray(
                    frontLight,
                    at + Vector2.down * 30f,
                    new Burst
                    {
                        Shape = BattleVfxShape.Bubble,
                        Bright = ErodeBright,
                        From = n % 2 == 0 ? color : other,
                        To = n % 2 == 0 ? color : other,
                        Speed = new Vector2(40f, 120f),
                        Direction = 90f,
                        Spread = 50f,
                        Life = new Vector2(0.6f, 1.1f),
                        Size = new Vector2(20f, 46f),
                        Grow = 0.3f,
                        Shrink = 1f,
                        Gravity = -60f,
                        Drag = 1f,
                        Hold = 0.6f,
                        Area = 50f,
                        Delay = new Vector2(0f, 0.4f),
                    },
                    1
                );
        }

        /// <summary>
        /// Leaves (or white feathers) carried from <paramref name="at"/>: thrown toward
        /// <paramref name="direction"/> at up to <paramref name="speed"/>, turning and drifting.
        /// </summary>
        private void Leaves(
            RectTransform layer,
            Vector2 at,
            Color color,
            int count,
            float direction,
            float speed,
            float gravity = -40f
        ) =>
            Spray(
                layer,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Leaf,
                    Bright = ErodeBright,
                    From = color,
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(speed * 0.4f, speed),
                    Direction = direction,
                    Spread = 60f,
                    Life = new Vector2(0.9f, 1.5f),
                    Size = new Vector2(18f, 32f),
                    Aspect = 1.8f,
                    Gravity = gravity,
                    Drag = 1.5f,
                    Spin = 240f,
                    Area = 60f,
                    Delay = new Vector2(0f, 0.3f),
                },
                count
            );

        /// <summary>Leaves and motes spiralling up round a character from its feet.</summary>
        private void Spiral(Vector2 feet, Color color, int count, float seconds)
        {
            for (int n = 0; n < count; n++)
            {
                var leaf = Spawn(
                    frontLight,
                    n % 2 == 0 ? BattleVfxShape.Leaf : BattleVfxShape.Sparkle,
                    feet,
                    n % 2 == 0 ? new Vector2(22f, 40f) : new Vector2(26f, 26f),
                    color
                );
                float phase = n * (360f / count);
                float delay = n * 0.05f;
                Animate(
                    leaf,
                    seconds + delay,
                    (s, t) =>
                    {
                        float time = t * (seconds + delay) - delay;
                        if (time < 0f)
                        {
                            Show(s, Color.clear, 0f, 0f);
                            return;
                        }
                        float k = time / seconds;
                        float angle = (phase + k * 300f) * Mathf.Deg2Rad;
                        s.Holder.anchoredPosition =
                            feet
                            + new Vector2(
                                Mathf.Cos(angle) * 90f,
                                20f + k * 260f + Mathf.Sin(angle) * 20f
                            );
                        s.Image.rectTransform.localEulerAngles = new Vector3(
                            0f,
                            0f,
                            angle * Mathf.Rad2Deg
                        );
                        Show(s, color, k, Eaten(k, 0.6f), ErodeBright);
                    }
                );
            }
        }

        /// <summary>Dark clouds rolling in at <paramref name="at"/> and staying a while (behind the light).</summary>
        private void Clouds(Vector2 at, int count, float seconds) =>
            Spray(
                backSmoke,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    From = new Color(0.16f, 0.15f, 0.22f, 0.9f),
                    To = new Color(0.1f, 0.1f, 0.15f, 0.9f),
                    Speed = new Vector2(20f, 90f),
                    Direction = 0f,
                    Spread = 360f,
                    Life = new Vector2(seconds * 0.8f, seconds),
                    Size = new Vector2(260f, 420f),
                    Aspect = 0.5f,
                    Grow = 0.5f,
                    Shrink = 1.4f,
                    Drag = 1.5f,
                    Spin = 8f,
                    Hold = 0.45f,
                    Area = 260f,
                },
                count
            );
    }
}
