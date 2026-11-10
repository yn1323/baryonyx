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

        // The light of ホーリーライト: a warm gold, so it glows gold rather than washing the stage
        // white, and burns white only at its brightest.
        private static readonly Color HolyGold = new(1f, 0.78f, 0.36f);

        // How long a shaft of ホーリーライト takes to reach down to the enemy (s, real time).
        private const float HolyDraw = 0.05f;

        /// <summary>The colour of a card's light (its cut-in, its glow), by the card's id.</summary>
        public static Color ColorOf(string id) =>
            id switch
            {
                var unique when IsUnique(unique) => UniqueColor(unique),
                var stage when IsAwakened(stage) => AwakenedColor(stage),
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
            // A unique skill's last step always cuts in: the biggest moments get the biggest stage.
            if (cutIn || (IsUnique(id) && TierOf(id) == 2))
                yield return PlayCutIn(casterArt, skill, ColorOf(id));
            yield return IsUnique(id) ? Unique(id, caster, hits, Once)
            : IsAwakened(id) ? Awakened(id, caster, hits, Once, cutIn)
            : CardEffect(id, caster, hits, Once, cutIn);
            for (int i = 0; i < landed.Length; i++)
                Once(i);
            DimTo(0f, 0.4f);
        }

        /// <summary>The effect of the card skill <paramref name="id"/> itself (not a unique skill's or an awakened stage's).</summary>
        private IEnumerator CardEffect(
            string id,
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land,
            bool cutIn
        )
        {
            var areas = new List<RectTransform>();
            foreach (var hit in hits)
                areas.Add(hit.Area);
            return id switch
            {
                "Slash" => Slash(caster, areas, land),
                "Fire" => Fire(caster, areas, land),
                "Ice" => Ice(caster, areas, land, cutIn),
                "Thunder" => Thunder(caster, areas, land, cutIn),
                "Heal" => Heal(caster, areas, Blessed(hits, land, ColorOf("Heal"), Boon.Mend)),
                "Guard" => Guard(caster, areas, Blessed(hits, land, ColorOf("Guard"), Boon.Ward)),
                "Whirlwind" => Whirlwind(caster, hits, land),
                "Iai" => Iai(caster, hits, land),
                "BladeDance" => BladeDance(caster, hits, land),
                "Hone" => Hone(caster, hits, Blessed(hits, land, ColorOf("Hone"), Boon.Surge)),
                "ShieldBash" => ShieldBash(caster, hits, land),
                "ArmorBreak" => ArmorBreak(caster, hits, land),
                "EarthSplitter" => EarthSplitter(caster, hits, land),
                "GiantImpact" => GiantImpact(caster, hits, land),
                "StrideStrike" => StrideStrike(caster, hits, land),
                "GuardianOath" => GuardianOath(
                    caster,
                    hits,
                    Blessed(hits, land, ColorOf("GuardianOath"), Boon.Ward)
                ),
                "WarCry" => WarCry(
                    caster,
                    hits,
                    Blessed(hits, land, ColorOf("WarCry"), Boon.Surge)
                ),
                "Embers" => Embers(caster, hits, land),
                "FlamePillar" => FlamePillar(caster, hits, land),
                "FireStorm" => FireStorm(caster, hits, land),
                "BlastSigil" => BlastSigil(caster, hits, land),
                "Meteor" => Meteor(caster, hits, land),
                "FlameEnchant" => FlameEnchant(
                    caster,
                    hits,
                    Blessed(hits, land, ColorOf("FlameEnchant"), Boon.Surge)
                ),
                "Icicles" => Icicles(caster, hits, land),
                "Blizzard" => Blizzard(caster, hits, land),
                "IceMirror" => IceMirror(
                    caster,
                    hits,
                    Blessed(hits, land, ColorOf("IceMirror"), Boon.Ward)
                ),
                "AbsoluteZero" => AbsoluteZero(caster, hits, land),
                "QuickCast" => QuickCast(
                    caster,
                    hits,
                    Blessed(hits, land, ColorOf("QuickCast"), Boon.Surge)
                ),
                "VitalThrust" => VitalThrust(caster, hits, land),
                "PoisonNeedle" => PoisonNeedle(caster, hits, land),
                "ShadowStitch" => ShadowStitch(caster, hits, land),
                "InsightArrow" => InsightArrow(caster, hits, land),
                "ArrowRain" => ArrowRain(caster, hits, land),
                "ShadowSnipe" => ShadowSnipe(caster, hits, land),
                "LightningBolt" => LightningBolt(caster, hits, land),
                "ChainLightning" => ChainLightning(caster, hits, land),
                "Thundercloud" => Thundercloud(caster, hits, land),
                "ThunderSpear" => ThunderSpear(caster, hits, land),
                "Gale" => Gale(caster, hits, Blessed(hits, land, ColorOf("Gale"), Boon.Surge)),
                "Scout" => Scout(caster, hits, Blessed(hits, land, ColorOf("Scout"), Boon.Surge)),
                "StepPrayer" => StepPrayer(
                    caster,
                    hits,
                    Blessed(hits, land, ColorOf("StepPrayer"), Boon.Mend)
                ),
                "Regen" => Regen(caster, hits, Blessed(hits, land, ColorOf("Regen"), Boon.Mend)),
                "Resurrection" => Resurrection(
                    caster,
                    hits,
                    Blessed(hits, land, ColorOf("Resurrection"), Boon.Mend)
                ),
                "HolyHammer" => HolyHammer(caster, hits, land),
                "Protect" => Protect(
                    caster,
                    hits,
                    Blessed(hits, land, ColorOf("Protect"), Boon.Ward)
                ),
                "Purify" => Purify(caster, hits, Blessed(hits, land, ColorOf("Purify"), Boon.Mend)),
                "ManaPrayer" => ManaPrayer(
                    caster,
                    hits,
                    Blessed(hits, land, ColorOf("ManaPrayer"), Boon.Surge)
                ),
                "Revelation" => Revelation(
                    caster,
                    hits,
                    Blessed(hits, land, ColorOf("Revelation"), Boon.Surge)
                ),
                "HolyLight" => HolyLight(
                    caster,
                    hits,
                    Blessed(hits, land, ColorOf("HolyLight"), Boon.Mend)
                ),
                "DivineShield" => DivineShield(
                    caster,
                    hits,
                    Blessed(hits, land, ColorOf("DivineShield"), Boon.Ward)
                ),
                _ => Slash(caster, areas, land),
            };
        }

        // --- Aria ------------------------------------------------------------------------------

        /// <summary>
        /// 旋風斬, sweeping (doc/catalog/skills/skill-whirlwind.md#演出の時間設計): the blade is
        /// swung once all the way round the enemies, one great flat arc whose far half passes
        /// behind them and whose near half before them; out of it a silver whirlwind rises at
        /// the near end of the enemies and runs through them to the far end; each is cut by two
        /// arcs as it passes, sparks fly, and red drops fall as they bleed; the blade comes back
        /// round the other way as the wind dies. Cut leaves spin away in the wind behind it.
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
            Shockwave(Feet(FrontLayer, caster), 260f, 0.4f);
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
            // The swing round all of them: the far half of the turn behind them, then the near
            // half before them, lying flat as the floor does.
            var hub = new Vector2((start + end) * 0.5f, floorY + 120f);
            float turnSize = Mathf.Max(1.7f, (end - start) / 360f);
            Arc(backLight, hub + new Vector2(0f, 36f), 0f, false, color, turnSize, 0.42f);
            yield return Wait(0.07f);
            Arc(frontLight, hub + new Vector2(0f, -56f), 180f, false, color, turnSize, 0.42f);
            Shake(0.14f, Vector2.right);
            var tornado = Spawn(
                backLight,
                BattleVfxShape.Tornado,
                new Vector2(start, floorY),
                new Vector2(380f, 560f),
                color,
                new Vector2(0.5f, 0.05f)
            );
            // A whirlwind shows by the dust it lifts: puffs of it are caught up and spun along its
            // path, and the wind itself is only a faint swirl.
            float nextPuff = 0f;
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
                    Show(s, color, Mathf.Min(1f, t / run), Eaten(t / (run + 0.35f), 0.6f), 0.85f);
                    if (t < nextPuff || t > run)
                        return;
                    nextPuff = t + 0.06f;
                    var dust = new Color(KickedDust.r, KickedDust.g, KickedDust.b, 0.55f);
                    Spray(
                        backSmoke,
                        s.Holder.anchoredPosition + Vector2.up * 40f,
                        new Burst
                        {
                            Shape = BattleVfxShape.Smoke,
                            Bright = 0.45f,
                            From = dust,
                            To = dust,
                            Speed = new Vector2(60f, 220f),
                            Direction = 90f,
                            Spread = 160f,
                            Life = new Vector2(0.5f, 0.8f),
                            Size = new Vector2(120f, 200f),
                            Aspect = 0.6f,
                            Grow = 0.5f,
                            Shrink = 1.5f,
                            Drag = 2f,
                            Spin = 300f,
                            Hold = 0.3f,
                            Area = 60f,
                        },
                        1
                    );
                    // Grit caught up and spun round in it.
                    Spray(
                        frontLight,
                        s.Holder.anchoredPosition + Vector2.up * 20f,
                        new Burst
                        {
                            Shape = BattleVfxShape.Chip,
                            From = new Color(0.45f, 0.37f, 0.3f),
                            To = new Color(0.32f, 0.27f, 0.22f, 0f),
                            Speed = new Vector2(200f, 520f),
                            Direction = 75f,
                            Spread = 60f,
                            Life = new Vector2(0.4f, 0.7f),
                            Size = new Vector2(4f, 9f),
                            Gravity = 900f,
                            Drag = 1f,
                            Spin = 900f,
                            Area = 50f,
                        },
                        2
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
                Sparks(at, BladeSpark, 10, 0.8f, Vector2.right);
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
            // The blade comes back round the other way as the wind dies.
            Arc(frontLight, hub + new Vector2(0f, -56f), 180f, true, color, turnSize * 0.9f, 0.42f);
            Arc(backLight, hub + new Vector2(0f, 36f), 0f, true, color, turnSize * 0.9f, 0.42f);
            Shockwave(new Vector2(hub.x, floorY), 520f, 0.6f);
            Leaves(
                frontLight,
                new Vector2(end - 100f, floorY + 200f),
                new Color(0.75f, 0.85f, 0.7f),
                8,
                0f,
                260f
            );
            yield return Wait(0.34f);
        }

        /// <summary>
        /// 居合一閃, stillness then a single instant, as a draw too fast to see looks
        /// (doc/catalog/skills/skill-iai.md#演出の時間設計): the stage goes dark and still and a
        /// glint runs at the mouth of the scabbard; then the blade is already through: only the
        /// arc it drew by the user and one line across the whole screen show (<see cref="Cut"/>),
        /// and nothing else moves for a beat. With the click of the guard the cut opens: the line
        /// parts into a dark slit with lips of light, arcs burst open across the enemy one after
        /// another, a flat cross flares, the time stops long, and the line breaks into shards
        /// all along its length.
        /// </summary>
        private IEnumerator Iai(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("Iai");
            var from = Center(FrontLayer, caster);
            var hilt = from + new Vector2(50f, 20f);
            DimTo(0.82f, 0.25f);
            Twinkle(hilt, color, 130f, 0.4f, 0.12f, 1f);
            yield return Wait(0.5f);
            // How long the line hangs in the dark before the cut opens.
            const float beat = 0.34f;
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var cutFrom = new Vector2(-1100f, at.y + 30f);
                var cutTo = new Vector2(1100f, at.y - 30f);
                // The draw: the arc the blade drew before the user, and the line it passed along.
                Arc(frontLight, from + new Vector2(70f, 6f), -90f, false, color, 1.25f);
                Star(hilt, Color.white, 200f, 0.12f);
                Cut(cutFrom, cutTo, color, 1f, 0.045f, beat + 0.02f, 1.35f);
                Shake(0.16f, Vector2.right);
                // The wind of the blade kicks the dust back off the floor behind the user.
                DustKick(Feet(FrontLayer, caster), Vector2.left, 150f);
                Illuminate(
                    hilt,
                    Color.Lerp(color, Color.white, 0.5f),
                    0.6f,
                    300f,
                    0.03f,
                    0.2f,
                    spill: false
                );
                yield return Wait(beat - 0.11f);
                // The wound opens: arcs across the enemy, fanned about the line, each a little
                // bigger than the last.
                for (int n = 0; n < 5; n++)
                {
                    float tilt = -90f + (n - 2) * 26f + UnityEngine.Random.Range(-8f, 8f);
                    var spot = at + new Vector2((n - 2) * 34f, UnityEngine.Random.Range(-26f, 26f));
                    // Behind the enemy and before it in turn, so it is seen among them.
                    Arc(
                        n % 2 == 0 ? backLight : frontLight,
                        spot,
                        tilt,
                        n % 2 == 1,
                        color,
                        0.8f + 0.12f * n
                    );
                    yield return Wait(0.022f);
                }
                // The click of the guard on the scabbard.
                Twinkle(hilt + new Vector2(-14f, -8f), Color.white, 80f, 0.22f, 0f, -1f);
                var weight = land(index);
                var cross = Spawn(
                    frontLight,
                    BattleVfxShape.SlashCross,
                    at,
                    new Vector2(680f, 240f),
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
                Impact(area, at, color, weight, 1f, Vector2.right, flashScreen: true);
                Sparks(at, BladeSpark, 26, 1.1f, Vector2.right);
                HitStop(0.18f);
                Punch(at, 0.035f);
                SlowMotion(0.4f, 0.22f);
                var shards = new Burst
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
                };
                Spray(frontLight, at, shards, 18);
                // The line itself breaks up all along its length, later the further from the cut.
                shards.Size = new Vector2(7f, 14f);
                shards.Speed = new Vector2(40f, 180f);
                shards.Area = 70f;
                var slope = (cutTo - cutFrom) / (cutTo.x - cutFrom.x);
                for (int n = -5; n <= 5; n++)
                {
                    if (n == 0)
                        continue;
                    float reach = Mathf.Abs(n) * 0.045f;
                    shards.Delay = new Vector2(0.06f + reach, 0.26f + reach);
                    Spray(frontLight, at + slope * (n * 170f), shards, 4);
                }
            }
            yield return Wait(0.5f);
        }

        /// <summary>
        /// 千刃乱舞, a storm of blades (doc/catalog/skills/skill-blade-dance.md#演出の時間設計):
        /// blades of light open one after another into a fan behind the user, then fly from it,
        /// faster and faster, each straight through the enemies along a thin line that is left
        /// in the air (<see cref="Cut"/>) and cutting an arc where it passes; the blows of the
        /// card are among them, and more that only cut the air. The last lands as a burst of
        /// lines through one point, a great cross and a ring, and shards of the blades fall.
        /// </summary>
        private IEnumerator BladeDance(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("BladeDance");
            var wave = Wave(hits, 0);
            var heart = Center(FrontLayer, caster) + new Vector2(-10f, 40f);
            DimTo(0.62f, 0.15f);
            // The fan: more blades than blows, so the air is full of them.
            int blades = Mathf.Clamp(wave.Count + 6, 9, 16);
            var fan = new Shot[blades];
            var roots = new Vector2[blades];
            for (int n = 0; n < blades; n++)
            {
                // From low behind the user, over the head, to low before.
                float polar = Mathf.Lerp(205f, -25f, n / (blades - 1f)) * Mathf.Deg2Rad;
                var outward = new Vector2(Mathf.Cos(polar), Mathf.Sin(polar));
                roots[n] = heart + new Vector2(outward.x * 230f, outward.y * 190f);
                var blade = Spawn(
                    frontLight,
                    BattleVfxShape.Shard,
                    roots[n],
                    new Vector2(38f, 180f),
                    Color.clear
                );
                blade.Image.rectTransform.localEulerAngles = new Vector3(
                    0f,
                    0f,
                    polar * Mathf.Rad2Deg - 90f
                );
                // Opened from the middle of the fan outward.
                float delay = Mathf.Abs(n - (blades - 1) * 0.5f) * 0.035f;
                var home = roots[n];
                float phase = n * 1.7f;
                var tint = Color.Lerp(Color.white, color, 0.45f);
                Animate(
                    blade,
                    4f,
                    (s, k) =>
                    {
                        float t = k * 4f - delay;
                        if (t < 0f)
                        {
                            Show(s, Color.clear, 0f, 0f);
                            return;
                        }
                        // Springs out from the user, then hangs, trembling to be loosed.
                        float spring = BackOut(Mathf.Clamp01(t / 0.14f));
                        s.Holder.anchoredPosition =
                            Vector2.LerpUnclamped(heart, home, spring)
                            + outward * (4f * Mathf.Sin(phase + t * 18f));
                        s.Holder.localScale = new Vector3(1f, 0.4f + 0.6f * spring, 1f);
                        Show(s, tint, k, 0f, CoreBright);
                    }
                );
                fan[n] = blade;
            }
            Glow(backLight, heart, color, 360f, 0.6f, 0.4f);
            yield return Wait(0.45f);
            if (wave.Count == 0)
            {
                foreach (var blade in fan)
                    End(blade);
                yield break;
            }

            // The blows are spread among the blades, the last blade the last blow.
            int blow = 0;
            for (int n = 0; n < blades; n++)
            {
                bool final = n == blades - 1;
                bool lands =
                    final || (blow < wave.Count - 1 && (n + 1) * wave.Count / blades > blow);
                int index = lands ? wave[blow] : wave[UnityEngine.Random.Range(0, wave.Count)];
                if (lands)
                    blow++;
                // From the two ends of the fan toward its middle.
                int pick = n % 2 == 0 ? n / 2 : blades - 1 - n / 2;
                var at =
                    Center(FrontLayer, hits[index].Area)
                    + UnityEngine.Random.insideUnitCircle * (lands ? 24f : 70f);
                // Gone from the fan in a twinkle, and at once on the enemy from a new side each
                // time (the golden angle keeps two in a row far apart).
                float side = (n * 137.5f + UnityEngine.Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
                var heading = -new Vector2(Mathf.Cos(side), Mathf.Sin(side) * 0.75f).normalized;
                var start = at - heading * UnityEngine.Random.Range(430f, 520f);
                var beyond = at + heading * UnityEngine.Random.Range(300f, 380f);
                End(fan[pick]);
                Star(roots[pick], color, 90f, 0.12f);
                StartCoroutine(
                    Shoot(
                        BattleVfxShape.Shard,
                        start,
                        beyond,
                        new Vector2(34f, 170f),
                        new Vector2(0.5f, 0.9f),
                        0.085f,
                        0f,
                        Color.white,
                        -90f,
                        null
                    )
                );
                Cut(start, beyond, color, lands ? 0.55f : 0.4f, 0.07f, -1f, 0.4f);
                yield return Wait(0.05f);
                float across = Angle(heading) - 90f + UnityEngine.Random.Range(-28f, 28f);
                Arc(frontLight, at, across, n % 2 == 1, color, lands ? 0.8f : 0.55f);
                Sparks(at, BladeSpark, lands ? 9 : 4, 0.7f, heading);
                if (lands && !final)
                {
                    var weight = land(index);
                    Star(at, color, 180f, 0.16f);
                    HitStop(Weighted(0.03f, weight));
                    Shake(Weighted(0.16f, weight), heading);
                    Illuminate(at, color, 0.5f, 260f, 0.02f, 0.18f, spill: false);
                }
                else if (final)
                {
                    // The last: every way at once, lines through one point.
                    for (int ray = 0; ray < 6; ray++)
                    {
                        float turn =
                            (ray * 30f + UnityEngine.Random.Range(-9f, 9f)) * Mathf.Deg2Rad;
                        var along = new Vector2(Mathf.Cos(turn), Mathf.Sin(turn) * 0.8f);
                        float reach = UnityEngine.Random.Range(380f, 560f);
                        Cut(at - along * reach, at + along * reach, color, 0.6f, 0.04f, -1f, 0.5f);
                    }
                    yield return Wait(0.05f);
                    var weight = land(index);
                    Cross(at, color, BattleHitWeight.Weak);
                    // No star of rays over it: the lines and the cross are the burst, and the
                    // enemy stays seen among them.
                    var feet = Feet(FrontLayer, hits[index].Area);
                    HitStop(Weighted(0.1f, weight));
                    Shake(Weighted(0.55f, weight), Vector2.right);
                    ScreenFlash(Color.Lerp(Color.white, color, 0.4f), 0.22f);
                    Pool(feet, color, Weighted(320f, weight), 0.06f, 0.4f);
                    Illuminate(
                        at,
                        Color.Lerp(color, Color.white, 0.35f),
                        0.8f,
                        Weighted(380f, weight),
                        0.05f,
                        0.3f,
                        spill: false
                    );
                    Shockwave(feet, 560f, 1f);
                    Sparks(at, BladeSpark, 22, 1.1f, Vector2.up);
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
                            Speed = new Vector2(120f, 520f),
                            Direction = 90f,
                            Spread = 180f,
                            Life = new Vector2(0.5f, 1f),
                            Size = new Vector2(9f, 20f),
                            Aspect = 2.6f,
                            Gravity = 700f,
                            Drag = 1.4f,
                            Spin = 360f,
                            Area = 90f,
                            Delay = new Vector2(0.03f, 0.18f),
                        },
                        26
                    );
                }
                // Faster and faster.
                yield return Wait(Mathf.Lerp(0.06f, 0.015f, n / (blades - 1f)));
            }
            yield return Wait(0.45f);
        }

        /// <summary>Ends a shot at once (a blade loosed from where it hung).</summary>
        private static void End(Shot shot)
        {
            if (shot == null || !shot.Live)
                return;
            shot.Live = false;
            shot.Holder.gameObject.SetActive(false);
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
        /// シールドバッシュ, a shove (doc/catalog/skills/skill-shield-bash.md#演出の時間設計): a
        /// blue crest flashes up before the user, a glint on its rim, and is driven straight into
        /// the enemy, leaving its afterimages along the way and two thin lines of its speed; where
        /// it strikes its face flashes white for an instant, sparks and scraps fly and dust is
        /// driven on along the floor. No ring in the air: metal meets the enemy. Then small
        /// crests pop over every ally as the party takes the block.
        /// </summary>
        private IEnumerator ShieldBash(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("ShieldBash");
            var from = Center(FrontLayer, caster) + new Vector2(70f, 0f);
            var size = new Vector2(176f, 208f);
            DimTo(0.42f, 0.12f);
            Bloom(frontLight, BattleVfxShape.Crest, from, size, color, 0.24f, 0.9f, ErodeBright);
            Twinkle(from + new Vector2(42f, 72f), Color.white, 90f, 0.2f, 0.04f, 1f);
            yield return Wait(0.16f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var stop = at - new Vector2(46f, 0f);
                const float charge = 0.11f;
                // The charge: the shield's afterimages along its way, fainter the further back,
                // and the lines of its speed above and below.
                var ghost = new Color(color.r, color.g, color.b, 0.5f);
                for (int n = 1; n <= 3; n++)
                    Bloom(
                        frontLight,
                        BattleVfxShape.Crest,
                        Vector2.Lerp(from, stop, n / 4f),
                        size * (0.82f + 0.05f * n),
                        ghost,
                        0.2f,
                        0.3f,
                        1f,
                        delay: charge * n / 4f
                    );
                Cut(
                    from + Vector2.up * 66f,
                    stop + Vector2.up * 66f,
                    color,
                    0.3f,
                    0.09f,
                    -1f,
                    0.26f
                );
                Cut(
                    from + Vector2.down * 60f,
                    stop + Vector2.down * 60f,
                    color,
                    0.3f,
                    0.09f,
                    -1f,
                    0.26f
                );
                yield return Shoot(
                    BattleVfxShape.Crest,
                    from,
                    stop,
                    size,
                    new Vector2(0.5f, 0.5f),
                    charge,
                    0f,
                    color,
                    0f,
                    null,
                    turn: false
                );
                var weight = land(index);
                // The slam: the shield's face flashes white where it struck. Metal meets the
                // enemy: no ring in the air, but scraps, sparks and dust.
                Bloom(
                    frontLight,
                    BattleVfxShape.Crest,
                    stop,
                    size * 1.3f,
                    Color.white,
                    0.18f,
                    0.25f,
                    CoreBright
                );
                Sparks(stop, BladeSpark, 20, 1f, Vector2.right);
                Grit(Feet(FrontLayer, area), 0.9f, Vector2.right);
                DustKick(Feet(FrontLayer, area), Vector2.right, 220f);
                Impact(area, at, color, weight, 0.85f, Vector2.right, flashScreen: false);
                Punch(at, 0.02f);
                Debris(at, new Color(0.55f, 0.6f, 0.7f), 14, Vector2.right);
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
        /// 鎧砕き, a crunch (doc/catalog/skills/skill-armor-break.md#演出の時間設計): a heavy blow
        /// comes down on the enemy, one thick line from above (<see cref="Cut"/>) and the arc of
        /// the swing; cracks split across its body, and its armour bursts off it: big grey plates
        /// thrown slow and turning lazily, behind it and before it, small scraps fast, sparks
        /// down and dust out along the floor; then a purple arrow sinks over it (its defence
        /// falling).
        /// </summary>
        private IEnumerator ArmorBreak(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("ArmorBreak");
            var steel = new Color(0.62f, 0.64f, 0.7f);
            DimTo(0.45f, 0.12f);
            Star(Center(FrontLayer, caster) + new Vector2(40f, 60f), Color.white, 140f, 0.16f);
            yield return Wait(0.14f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var feet = Feet(FrontLayer, area);
                Cut(
                    at + new Vector2(-70f, 430f),
                    at + new Vector2(24f, -70f),
                    color,
                    1.1f,
                    0.05f,
                    -1f,
                    0.5f
                );
                Arc(frontLight, at + new Vector2(-10f, 50f), 60f, false, color, 1.1f);
                yield return Wait(0.06f);
                var weight = land(index);
                Bloom(
                    frontLight,
                    BattleVfxShape.Crack,
                    at,
                    new Vector2(340f, 340f),
                    color,
                    0.6f,
                    0.45f,
                    CoreBright
                );
                Impact(area, at, color, weight, 0.9f, Vector2.down, flashScreen: false);
                Punch(at, 0.025f);
                // The armour bursts off: big plates slow and heavy, behind the enemy and before it.
                var plates = new Burst
                {
                    Shape = BattleVfxShape.Chip,
                    From = steel,
                    To = new Color(steel.r * 0.6f, steel.g * 0.6f, steel.b * 0.6f, 1f),
                    Speed = new Vector2(220f, 640f),
                    Direction = 90f,
                    Spread = 170f,
                    Life = new Vector2(0.6f, 1f),
                    Size = new Vector2(18f, 38f),
                    Gravity = 1500f,
                    Drag = 0.5f,
                    Spin = 420f,
                    Area = 40f,
                };
                Spray(backLight, at, plates, 5);
                Spray(frontLight, at, plates, 8);
                Debris(at, steel, 16, Vector2.up);
                Sparks(at, BladeSpark, 20, 1f, Vector2.down);
                Shockwave(feet, 380f, 0.9f);
                Grit(feet, 0.8f, Vector2.up);
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
            yield return Wait(0.5f);
        }

        /// <summary>
        /// 大地割り, a rumble, as stone breaks (doc/catalog/skills/skill-earth-splitter.md#演出の時間設計):
        /// the ground cracks round the user's feet (<see cref="Tremor"/>), the blow lands on the
        /// floor and dust is driven out low; then the crack runs to the enemies in snaps (ぴき),
        /// hairline slits that show by their shadow, and the floor heaves along it: slabs of rock
        /// thrust up out of it one after another, each bigger than the last
        /// (<see cref="Upheave"/>). Under each enemy the ground bursts: a great slab behind it and
        /// smaller ones beside it, big rocks thrown slow and heavy and small chips fast, dust
        /// boiling up behind that stays after the rocks fall, the stage shaking hard. Then the
        /// slabs crumble from their tips down. The ground never glows.
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
            yield return Tremor(caster, 0.4f);
            Shockwave(feet, 500f, 1f);
            Grit(feet, 1f, Vector2.up);
            Shake(0.5f, Vector2.down);
            HitStop(0.05f);
            var wave = Wave(hits, 0);
            var goal = MidFeet(hits, 0);
            // The crack runs along the floor in snaps, each a new stretch a step further on, and
            // a slab is thrust up out of each, on one side of it and the other in turn.
            const int steps = 6;
            for (int n = 1; n <= steps; n++)
            {
                var step =
                    Vector2.Lerp(feet, goal, n / (steps + 1f))
                    + new Vector2(0f, UnityEngine.Random.Range(-8f, 8f));
                float side = n % 2 == 0 ? 1f : -1f;
                Rupture(step, 380f, 0.03f, 0f, 1.4f);
                Upheave(
                    step + new Vector2(UnityEngine.Random.Range(-12f, 12f), side * 16f),
                    new Vector2(96f + 20f * n, 84f + 26f * n),
                    -side * UnityEngine.Random.Range(5f, 13f) - 6f,
                    0.5f + 0.04f * n,
                    side < 0f
                );
                Hop(step, 110f, 3, 0.8f);
                Shake(0.09f, Vector2.up);
                yield return Wait(0.045f);
            }
            foreach (int index in wave)
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var under = Feet(FrontLayer, area);
                var weight = land(index);
                float big = Weighted(1f, weight);
                // A great slab behind the enemy and a lesser beside it; two small ones before it,
                // off to its sides, so it is still seen.
                Upheave(
                    under + new Vector2(-40f, 30f),
                    new Vector2(300f, 380f) * big,
                    6f,
                    0.75f,
                    false
                );
                Upheave(
                    under + new Vector2(110f, 22f),
                    new Vector2(220f, 260f) * big,
                    -10f,
                    0.68f,
                    false
                );
                Upheave(under + new Vector2(-190f, -18f), new Vector2(130f, 104f), 14f, 0.6f, true);
                Upheave(under + new Vector2(196f, -24f), new Vector2(112f, 84f), -12f, 0.55f, true);
                Rocks(under, 1.15f);
                Rupture(under, 520f, 0.03f, 0f, 1.6f);
                Shockwave(under, Weighted(360f, weight), 1f);
                HitStop(Weighted(0.08f, weight));
                Shake(Weighted(0.55f, weight), Vector2.up);
                if (weight != BattleHitWeight.Normal)
                    ScreenFlash(Color.Lerp(Color.white, color, 0.4f), 0.2f);
                // Light off the struck body only; the ground does not glow.
                Illuminate(
                    at,
                    Color.Lerp(color, Color.white, 0.4f),
                    0.5f,
                    260f,
                    0.04f,
                    0.25f,
                    spill: false
                );
                yield return Wait(0.07f);
            }
            Shake(0.35f, Vector2.up);
            yield return Wait(0.8f);
        }

        /// <summary>
        /// ギガントインパクト, crushing weight (doc/catalog/skills/skill-giant-impact.md#演出の時間設計):
        /// the sky darkens and a great golden light gathers high over the enemy while its light
        /// grows on the floor under it; then a shaft of light slams down on it like a giant's fist,
        /// a white-hot core in it for an instant; the floor breaks in snaps round the feet, rocks
        /// burst up (big and slow, small and fast), slabs of the floor are thrust up round the
        /// feet, leaning outward, the big ones behind the enemy and small ones before it
        /// (<see cref="Upheave"/>), dust is driven out both ways and boils up behind, and the
        /// time stops and slows. The cracks show by their shadow, not as light.
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
                // The light overhead falls on the floor, growing as it comes.
                var spot = Spawn(
                    backLight,
                    BattleVfxShape.Glow,
                    feet,
                    new Vector2(420f, 420f),
                    color
                );
                spot.Holder.localScale = new Vector3(1f, FloorTilt, 1f);
                Animate(
                    spot,
                    0.62f,
                    (s, k) =>
                    {
                        float grow = 0.4f + 0.6f * EaseIn(k);
                        s.Holder.localScale = new Vector3(grow, grow * FloorTilt, 1f);
                        Show(s, new Color(color.r, color.g, color.b, 0.6f * EaseIn(k)), k, 0f);
                    }
                );
                Shake(0.1f, Vector2.zero);
                yield return Wait(0.5f);
                LightColumn(
                    feet,
                    new Vector2(460f, (above.y - feet.y) / 0.9f + 80f),
                    color,
                    0.75f,
                    down: true
                );
                yield return Wait(0.08f);
                var weight = land(index);
                Rupture(feet, 640f, 0.03f, 0f, 1.6f);
                Shockwave(feet, 860f, 1f);
                Impact(
                    area,
                    at,
                    color,
                    Weighted2(weight),
                    1f,
                    Vector2.down,
                    flashScreen: true,
                    rays: false
                );
                HitStop(0.16f);
                SlowMotion(0.4f, 0.25f);
                Punch(at, 0.045f);
                Rocks(feet, 1.2f);
                Upheave(feet + new Vector2(-190f, 26f), new Vector2(210f, 250f), 22f, 0.65f, false);
                Upheave(feet + new Vector2(200f, 20f), new Vector2(190f, 215f), -20f, 0.6f, false);
                Upheave(feet + new Vector2(-250f, -22f), new Vector2(124f, 100f), 26f, 0.5f, true);
                Upheave(feet + new Vector2(262f, -28f), new Vector2(112f, 88f), -24f, 0.45f, true);
                foreach (float side in new[] { 0f, 180f })
                    Spray(
                        backSmoke,
                        feet + Vector2.up * 10f,
                        new Burst
                        {
                            Shape = BattleVfxShape.Smoke,
                            Bright = 0.55f,
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
                            Level = true,
                            Hold = 0.25f,
                            Area = 30f,
                        },
                        5
                    );
            }
            yield return Wait(0.5f);
        }

        /// <summary>
        /// 健脚の一撃, brisk (doc/catalog/skills/skill-stride-strike.md#演出の時間設計): golden
        /// footprints light up on the floor step by step from the user to the enemy, each kicking
        /// a little dust back; then the dash is only its afterimage, a line of gold at the height
        /// of the body (<see cref="Cut"/>), and the kick lands as two arcs crossing up through the
        /// enemy, behind it and before it, golden sparks and dust driven on along the floor; motes
        /// of the day's walk rise from the footprints after. No ring in the air.
        /// </summary>
        private IEnumerator StrideStrike(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("StrideStrike");
            var from = Feet(FrontLayer, caster);
            DimTo(0.4f, 0.15f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var to = Feet(FrontLayer, area) - new Vector2(80f, 0f);
                var prints = Footsteps(from, to, color, 6, 0.06f);
                // Each step kicks a little dust back off the floor.
                for (int p = 0; p < prints.Count; p++)
                {
                    var print = prints[p];
                    StartCoroutine(After(p * 0.06f, () => DustKick(print, Vector2.left, 90f)));
                }
                yield return Wait(0.36f);
                var start = from + Vector2.up * 60f;
                Cut(start, at + (at - start).normalized * 120f, color, 0.7f, 0.07f, -1f, 0.42f);
                StartCoroutine(
                    Shoot(
                        BattleVfxShape.Streak,
                        start,
                        at,
                        new Vector2(60f, 220f),
                        new Vector2(0.5f, 0.85f),
                        0.1f,
                        30f,
                        color,
                        -90f,
                        null
                    )
                );
                yield return Wait(0.1f);
                var weight = land(index);
                // The kick: two arcs crossing, up through the enemy.
                Arc(backLight, at, 55f, false, color, 1.05f);
                Arc(frontLight, at + new Vector2(10f, -6f), -125f, true, color, 0.9f);
                Sparks(at, color, 16, 1f, new Vector2(1f, 0.6f));
                Shockwave(Feet(FrontLayer, area), 340f, 0.8f);
                Impact(
                    area,
                    at,
                    color,
                    weight,
                    0.8f,
                    Vector2.right,
                    flashScreen: false,
                    rays: false
                );
                Punch(at, 0.02f);
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
            yield return Aura(caster, color, 0.4f);
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
                // The cry's pressure drives dust out along the floor, wider each time.
                Shockwave(Feet(FrontLayer, caster), 360f + n * 140f, 0.5f);
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
                for (int n = 0; n < 14; n++)
                {
                    var end = at + UnityEngine.Random.insideUnitCircle * 60f;
                    StartCoroutine(
                        Shoot(
                            BattleVfxShape.Flame,
                            from + UnityEngine.Random.insideUnitCircle * 30f,
                            end,
                            new Vector2(58f, 58f),
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
                Licks(area, 10, 0.9f);
                EmberBurst(at, 12, 0.6f);
                // The little fire catches on the floor under it, flickering.
                GroundFire(Feet(FrontLayer, area), 130f, 0.4f);
            }
            yield return Wait(0.4f);
        }

        /// <summary>
        /// フレイムピラー, an eruption, as a jet of fire out of the ground behaves
        /// (doc/catalog/skills/skill-flame-pillar.md#演出の時間設計): a red circle opens under the
        /// enemy and spins faster while the ground under it heats, glowing, spitting sparks and
        /// wisps of smoke; then fire shoots up out of it with a white-hot core at its foot, the
        /// enemy standing in the fire (its body behind, tongues in front), lit from below with a
        /// flickering light; soot boils off its top and keeps rising, embers drift, and a scorch
        /// is left. A single-target spell of cost 2: no flash of the screen but on a weakness.
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
            const float omen = 0.4f;
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
                        Show(s, color, Mathf.Min(1f, k * 1.5f), Eaten(k, 0.6f), ErodeBright * 1.2f);
                    }
                );
                // The ground heats before it bursts: a dull glow that brightens, sparks and wisps.
                HotFloor(feet, 260f, omen, 0.15f, FireColor(0.4f));
                Spit(feet, 260f, omen);
                Illuminate(
                    feet,
                    FireColor(0.3f),
                    0.45f,
                    300f,
                    omen,
                    0.2f,
                    spill: false,
                    flicker: 0.3f
                );
                yield return Wait(omen);
                var weight = land(index);
                FireColumn(feet, 340f, 960f, 1.1f, color);
                // The eruption throws embers high and sets the floor round its foot burning.
                EmberBurst(feet + Vector2.up * 80f, 26, 1.2f);
                GroundFire(feet, 240f, 0.5f);
                Punch(at, 0.03f);
                // No white flash over the body: the fire itself lights it, so the target stays seen.
                HitStop(Weighted(0.08f, weight));
                Shake(Weighted(0.45f, weight), Vector2.up);
                if (weight != BattleHitWeight.Normal)
                    ScreenFlash(Color.Lerp(Color.white, color, 0.4f), 0.26f);
            }
            yield return Wait(0.6f);
        }

        /// <summary>
        /// ファイアストーム, a raging blaze, as a fire whirl behaves
        /// (doc/catalog/skills/skill-fire-storm.md#演出の時間設計): after the burning circle, two
        /// whirls of fire rise amid the enemies and sweep across them past each other; each is a
        /// column of fire (drawn as density, behind the enemies) wrapped in a faint swirl of hot
        /// air that shows it turning, soot torn off its top and rising, embers spun up out of it.
        /// Each enemy is caught as they pass, flames clinging to it in front; the floor glows hot
        /// along their path and is left scorched, smoke rising after.
        /// </summary>
        private IEnumerator FireStorm(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("FireStorm");
            yield return FireCharge(caster, 0.4f);
            var wave = Wave(hits, 0);
            var mid = MidFeet(hits, 0);
            float left = Inside(mid, 600f).x - 200f;
            float right = Inside(mid, 600f).x + 200f;
            const float sweep = 1.1f;
            foreach (float side in new[] { -1f, 1f })
            {
                float start = side < 0f ? left : right;
                float end = side < 0f ? right : left;
                float seed = UnityEngine.Random.value;
                float lag = side < 0f ? 0f : 0.08f;
                var body = Spawn(
                    backLight,
                    BattleVfxShape.Pillar,
                    new Vector2(start, mid.y),
                    new Vector2(side < 0f ? 620f : 540f, side < 0f ? 900f : 800f),
                    Color.clear,
                    new Vector2(0.5f, 0.1f)
                );
                body.Seed = seed;
                body.Image.material = RichMaterialOf(BattleVfxShape.Pillar, PillarFire);
                float nextSoot = 0f;
                Animate(
                    body,
                    sweep + lag,
                    (s, k) =>
                    {
                        float t = k * (sweep + lag) - lag;
                        if (t < 0f)
                        {
                            Show(s, Color.clear, 0f, 0f);
                            return;
                        }
                        float u = t / sweep;
                        var at = new Vector2(Mathf.Lerp(start, end, EaseOut(u)), mid.y);
                        s.Holder.anchoredPosition = at;
                        float rise = EaseOut(Mathf.Clamp01(t / 0.15f));
                        // It writhes as it turns, and thins as it burns out.
                        float girth =
                            (0.85f + 0.15f * Mathf.Sin(t * 9f + seed * 6f))
                            * Mathf.Lerp(1f, 0.55f, EaseIn(Mathf.Clamp01((u - 0.55f) / 0.45f)));
                        s.Holder.localScale = new Vector3(girth, 0.05f + 0.95f * rise, 1f);
                        var tint = Color.Lerp(
                            Color.Lerp(color, Color.white, 0.2f),
                            FireColor(0.35f),
                            u
                        );
                        Show(s, tint, t * 1.4f, Eaten(u, 0.5f), ErodeBright * 1.3f);
                        if (t < nextSoot || u > 0.8f)
                            return;
                        nextSoot = t + 0.08f;
                        Spray(
                            backSmoke,
                            at + Vector2.up * 420f,
                            new Burst
                            {
                                Shape = BattleVfxShape.Smoke,
                                Bright = 0.4f,
                                From = new Color(0.4f, 0.25f, 0.15f, Soot.a),
                                To = Soot,
                                Speed = new Vector2(60f, 180f),
                                Direction = 90f,
                                Spread = 60f,
                                Life = new Vector2(1f, 1.6f),
                                Size = new Vector2(150f, 240f),
                                Grow = 0.45f,
                                Shrink = 1.8f,
                                Gravity = -80f,
                                Drag = 1.2f,
                                Spin = 60f,
                                Hold = 0.35f,
                                Area = 60f,
                            },
                            1
                        );
                        Spray(
                            frontLight,
                            at + Vector2.up * 120f,
                            new Burst
                            {
                                Shape = BattleVfxShape.Spark,
                                Bright = CoreBright,
                                From = new Color(1f, 0.92f, 0.6f),
                                To = new Color(1f, 0.3f, 0.06f, 0f),
                                Speed = new Vector2(250f, 700f),
                                Direction = 90f + side * 30f,
                                Spread = 80f,
                                Life = new Vector2(0.5f, 1.2f),
                                Size = new Vector2(5f, 10f),
                                Gravity = -150f,
                                Drag = 2f,
                                Area = 80f,
                            },
                            3
                        );
                    }
                );
                // The swirl of hot air round it, faint, so it shows turning.
                var swirl = Spawn(
                    backLight,
                    BattleVfxShape.Tornado,
                    new Vector2(start, mid.y),
                    new Vector2(side < 0f ? 440f : 380f, side < 0f ? 700f : 600f),
                    Color.clear,
                    new Vector2(0.5f, 0.05f)
                );
                Animate(
                    swirl,
                    sweep + lag,
                    (s, k) =>
                    {
                        float t = k * (sweep + lag) - lag;
                        float u = Mathf.Clamp01(t / sweep);
                        s.Holder.anchoredPosition = new Vector2(
                            Mathf.Lerp(start, end, EaseOut(u)),
                            mid.y
                        );
                        s.Holder.localScale = new Vector3(
                            side < 0f ? 1f : -1f,
                            EaseOut(Mathf.Clamp01(t / 0.2f)),
                            1f
                        );
                        var tint = new Color(
                            1f,
                            0.72f,
                            0.42f,
                            0.4f * Envelope(t, 0.05f, 0.15f, 0.6f, sweep)
                        );
                        Show(s, tint, u, Eaten(u, 0.6f), 1f);
                    }
                );
            }
            Illuminate(mid + Vector2.up * 150f, color, 1f, 700f, 0.7f, 0.5f, flicker: 0.45f);
            HotFloor(mid, 760f, 0.9f, 0.6f, color);
            FloorMark(
                BattleVfxShape.Scorch,
                mid,
                new Vector2(620f, 620f),
                new Color(0.06f, 0.045f, 0.04f),
                0.75f,
                2.2f,
                flat: true
            );
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
                Licks(area, 8, 0.8f);
                EmberBurst(Center(FrontLayer, area), 14, 0.9f);
                GroundFire(Feet(FrontLayer, area), 180f, 0.5f);
            }
            yield return Wait(0.6f);
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
            yield return Inhale(caster, color, 0.35f);
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
                    new Vector2(350f, 350f),
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
                Spray(frontLight, at, Embered(color), 10);
                EmberBurst(at, 12, 0.6f);
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
            // Its light falls on the floor ahead of it, growing and warming as it nears.
            var spot = Spawn(backLight, BattleVfxShape.Glow, mid, new Vector2(900f, 900f), color);
            Animate(
                spot,
                0.95f,
                (s, k) =>
                {
                    float grow = 0.3f + 0.7f * EaseIn(k);
                    s.Holder.localScale = new Vector3(grow, grow * FloorTilt, 1f);
                    var tint = Color.Lerp(FireColor(0.4f), Color.Lerp(color, Color.white, 0.3f), k);
                    Show(s, new Color(tint.r, tint.g, tint.b, 0.7f * EaseIn(k)), k, 0f);
                }
            );
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
                },
                material: RichMaterialOf(BattleVfxShape.Fireball)
            );
            ScreenFlash(new Color(1f, 0.85f, 0.6f), MaxFlash);
            HitStop(0.18f);
            SlowMotion(0.4f, 0.3f);
            Punch(mid, 0.05f);
            Shake(1f, Vector2.down);
            Shockwave(mid, 1100f, 1f);
            Rupture(mid, 900f, 0.03f, 0f, 2f);
            Rocks(mid, 1.4f);
            HotFloor(mid, 900f, 0.6f, 0.9f, color);
            // The rim of the crater: slabs of the floor thrust up round where it fell, leaning
            // outward, the big ones behind the enemies and small ones before them; the floor
            // burns out from it, and embers are thrown high.
            Upheave(mid + new Vector2(-360f, 60f), new Vector2(190f, 200f), 26f, 0.8f, false);
            Upheave(mid + new Vector2(-60f, 110f), new Vector2(170f, 160f), 6f, 0.75f, false);
            Upheave(mid + new Vector2(330f, 70f), new Vector2(200f, 190f), -24f, 0.8f, false);
            Upheave(mid + new Vector2(-440f, -70f), new Vector2(140f, 104f), 28f, 0.6f, true);
            Upheave(mid + new Vector2(450f, -80f), new Vector2(130f, 96f), -26f, 0.55f, true);
            GroundFire(mid + Vector2.down * 40f, 520f, 0.8f);
            EmberBurst(mid + Vector2.up * 60f, 40, 1.4f);
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
            yield return Inhale(caster, color, 0.3f);
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
            // The icicles freeze out of the air above: glints of ice crystals gather there.
            Spray(
                frontLight,
                new Vector2(mid.x, top),
                new Burst
                {
                    Shape = BattleVfxShape.Sparkle,
                    Bright = CoreBright,
                    From = new Color(0.85f, 0.95f, 1f, 0f),
                    To = Color.white,
                    Speed = new Vector2(10f, 40f),
                    Direction = -90f,
                    Spread = 360f,
                    Life = new Vector2(0.25f, 0.45f),
                    Size = new Vector2(8f, 16f),
                    Spin = 120f,
                    Area = 300f,
                },
                24
            );
            yield return Wait(0.25f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at =
                    Center(FrontLayer, area) + new Vector2(UnityEngine.Random.Range(-30f, 30f), 0f);
                var start = new Vector2(at.x + UnityEngine.Random.Range(-20f, 20f), top);
                // Two lesser icicles fall beside it, a moment apart, and every one leaves the
                // hairline of its fall.
                for (int side = -1; side <= 1; side += 2)
                {
                    var beside = at + new Vector2(side * UnityEngine.Random.Range(80f, 120f), 0f);
                    var above = new Vector2(beside.x + UnityEngine.Random.Range(-16f, 16f), top);
                    var ground = Feet(FrontLayer, area) + new Vector2(beside.x - at.x, 30f);
                    Cut(above, ground, color, 0.35f, 0.15f, -1f, 0.36f);
                    StartCoroutine(
                        Shoot(
                            BattleVfxShape.IceSpike,
                            above,
                            ground + Vector2.up * 70f,
                            new Vector2(46f, 110f),
                            new Vector2(0.5f, 0.03f),
                            side < 0 ? 0.15f : 0.18f,
                            0f,
                            color,
                            180f,
                            null,
                            turn: false,
                            material: RichMaterialOf(BattleVfxShape.IceSpike)
                        )
                    );
                }
                Cut(start, at, color, 0.5f, 0.13f, -1f, 0.4f);
                // An icicle points down: a spike of ice turned over, falling fast.
                yield return Shoot(
                    BattleVfxShape.IceSpike,
                    start,
                    at + Vector2.up * 110f,
                    new Vector2(70f, 170f),
                    new Vector2(0.5f, 0.03f),
                    0.13f,
                    0f,
                    color,
                    // Turned over, so its point leads as it falls.
                    180f,
                    null,
                    turn: false,
                    material: RichMaterialOf(BattleVfxShape.IceSpike)
                );
                var weight = land(index);
                Spray(
                    frontLight,
                    at,
                    new Burst
                    {
                        Shape = BattleVfxShape.Chip,
                        Material = RichMaterialOf(BattleVfxShape.Chip),
                        From = color,
                        To = color,
                        Speed = new Vector2(150f, 450f),
                        Direction = 90f,
                        Spread = 160f,
                        Life = new Vector2(0.4f, 0.7f),
                        Size = new Vector2(10f, 24f),
                        Gravity = 1400f,
                        Drag = 1f,
                        Spin = 360f,
                        Hold = 0.6f,
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
                // Ice breaks with a glint, not a star of light; cold air spills off it.
                HotFlash(at, IceLight, 150f);
                Twinkle(
                    at + new Vector2(10f, 20f),
                    Color.Lerp(IceLight, Color.white, 0.5f),
                    140f,
                    0.22f,
                    0f,
                    1f
                );
                Pool(Feet(FrontLayer, area), IceLight, 240f, 0.05f, 0.35f);
                ColdMist(Feet(FrontLayer, area), 220f, 1f, 0.5f);
                DiamondDust(at, 14, 110f, 0.9f);
                Punch(at, 0.015f);
                HitStop(Weighted(0.03f, weight));
                Shake(Weighted(0.2f, weight), Vector2.down);
                Illuminate(at, color, 0.6f, 260f, 0.03f, 0.25f, spill: false);
                yield return Wait(0.06f);
            }
            yield return Wait(0.35f);
        }

        /// <summary>
        /// ブリザード, a howling cold, as a real snowstorm moves: a strong wind blows steadily from
        /// the left. Snow is blown along the floor in long thin streams that snake and fray (the
        /// pattern flows through bands that stay, as snow streams past, see
        /// <see cref="WindStream"/>), thin veils of it sweep higher up, and snowflakes rush past at
        /// every depth, small and dim far off, big and blurred close by
        /// (<see cref="SnowFlakes"/>); nothing hangs in the air. The floor under every enemy
        /// freezes over and ice grows on them, leaning into the wind as rime does, in a blue light;
        /// then it cracks and breaks, and its pieces and dust are blown away downwind
        /// (<see cref="Shatter"/>). The screen does not flash.
        /// </summary>
        private IEnumerator Blizzard(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var snow = new Color(0.78f, 0.9f, 1f);
            var wind = Vector2.right;
            DimTo(IceDim, 0.15f);
            yield return ColdFront(caster, 0.45f);
            var mid = MidFeet(hits, 0);
            float storm = IceBreakAt + 1.3f;
            WindStream(backSmoke, mid.y + 30f, 240f, snow, storm, 0.9f);
            WindStream(frontSmoke, mid.y - 90f, 200f, snow, storm, 0.45f);
            WindStream(frontSmoke, mid.y + 200f, 380f, snow, storm, 0.18f);
            SnowFlakes(
                new Vector2(FrontLayer.rect.xMin - 120f, mid.y + 120f),
                320f,
                storm - 0.6f,
                snow
            );
            Illuminate(mid + Vector2.up * 150f, IceLight, 0.7f, 800f, 0.8f, 0.6f);
            yield return Wait(0.3f);
            // The wind itself: hairlines of driven snow across the enemies, one after another.
            for (int n = 0; n < 9; n++)
            {
                var gust =
                    mid
                    + new Vector2(
                        UnityEngine.Random.Range(-760f, -420f),
                        UnityEngine.Random.Range(20f, 330f)
                    );
                float run = UnityEngine.Random.Range(900f, 1300f);
                StartCoroutine(
                    After(
                        n * 0.05f,
                        () => Cut(gust, gust + wind * run, snow, 0.3f, 0.1f, -1f, 0.3f)
                    )
                );
            }
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var feet = Feet(FrontLayer, area);
                var (low, high) = Bounds(FrontLayer, area);
                float width = Mathf.Max(160f, high.x - low.x);
                var weight = land(index);
                HitStop(Weighted(0.05f, weight));
                Shake(Weighted(0.3f, weight), wind);
                FrostSheet(feet, width * 1.6f, IceBreakAt + 0.9f);
                // Rime grows into the wind.
                SpikeRing(feet, width, 0.75f, IceBreakAt, lean: 22f);
                Pool(feet, IceLight, 340f, IceBreakAt, 0.4f);
                Illuminate(at, IceLight, 0.7f, 420f, IceBreakAt, 0.5f);
                StartCoroutine(Shatter(at, feet, width, IceBreakAt, 0.45f, wind));
                StartCoroutine(After(IceBreakAt, () => DiamondDust(at, 18, width * 0.9f, 1.2f)));
                yield return Wait(0.08f);
            }
            // Waits for the ice to break, so it breaks while the stage is still dark.
            yield return Wait(IceBreakAt + 0.1f);
        }

        /// <summary>
        /// Snow blown along by a strong wind in a band across the whole stage at
        /// <paramref name="y"/>, <paramref name="height"/> tall (the shader's stream of wind): long,
        /// thin streams that snake and fray, gusting, with the snow running through the band while
        /// the band itself stays. It rises and dies away with the wind over
        /// <paramref name="seconds"/>; <paramref name="strength"/> is how thick it is.
        /// </summary>
        private void WindStream(
            RectTransform layer,
            float y,
            float height,
            Color color,
            float seconds,
            float strength
        )
        {
            if (layer == null)
                return;
            var shot = Spawn(
                layer,
                BattleVfxShape.Tornado,
                new Vector2(FrontLayer.rect.center.x, y),
                new Vector2(FrontLayer.rect.width * 1.25f, height),
                Color.clear
            );
            shot.Image.material = RichMaterialOf(BattleVfxShape.Tornado);
            Animate(
                shot,
                seconds,
                (s, k) =>
                {
                    float rise = Mathf.Min(1f, k * 6f);
                    float fall = 1f - EaseIn(Mathf.Clamp01((k - 0.7f) / 0.3f));
                    var tint = new Color(color.r, color.g, color.b, strength * rise * fall);
                    Show(s, tint, k, 0f, 1.2f);
                }
            );
        }

        /// <summary>
        /// Snowflakes rushing past on the wind from <paramref name="from"/> (at the upwind edge),
        /// spread <paramref name="spread"/> px up and down, for <paramref name="seconds"/>: at
        /// every depth, as a real storm looks. Far ones are small, dim and slower, behind the
        /// actors; middle ones streak past; a few near ones are big and blurred as they rush by
        /// the eye. Each streaks along its flight in proportion to its speed.
        /// </summary>
        private void SnowFlakes(Vector2 from, float spread, float seconds, Color color)
        {
            var gone = new Color(color.r, color.g, color.b, 0f);
            Spray(
                backLight,
                from,
                new Burst
                {
                    Shape = BattleVfxShape.Spark,
                    From = new Color(color.r, color.g, color.b, 0.7f),
                    To = gone,
                    Speed = new Vector2(900f, 1300f),
                    Direction = -6f,
                    Spread = 14f,
                    Life = new Vector2(1.4f, 1.9f),
                    Size = new Vector2(2f, 4f),
                    Stretch = 0.012f,
                    Gravity = 80f,
                    Drag = 0.05f,
                    Area = spread,
                    Delay = new Vector2(0f, seconds),
                },
                70
            );
            Spray(
                frontLight,
                from,
                new Burst
                {
                    Shape = BattleVfxShape.Spark,
                    Bright = 1.4f,
                    From = Color.white,
                    To = gone,
                    Speed = new Vector2(1500f, 2100f),
                    Direction = -10f,
                    Spread = 20f,
                    Life = new Vector2(0.9f, 1.3f),
                    Size = new Vector2(3f, 6f),
                    Stretch = 0.012f,
                    Gravity = 140f,
                    Drag = 0.05f,
                    Area = spread,
                    Delay = new Vector2(0f, seconds),
                },
                70
            );
            Spray(
                frontLight,
                from,
                new Burst
                {
                    Shape = BattleVfxShape.Spark,
                    Bright = 1.2f,
                    From = new Color(1f, 1f, 1f, 0.55f),
                    To = gone,
                    Speed = new Vector2(2600f, 3400f),
                    Direction = -12f,
                    Spread = 22f,
                    Life = new Vector2(0.5f, 0.8f),
                    Size = new Vector2(6f, 11f),
                    Stretch = 0.018f,
                    Gravity = 60f,
                    Drag = 0.02f,
                    Area = spread * 1.2f,
                    Delay = new Vector2(0f, seconds),
                },
                14
            );
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
            yield return ColdFront(caster, 0.35f);
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
                            BattleVfxShape.Chip,
                            start,
                            pane + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 40f,
                            new Vector2(30f, 30f),
                            new Vector2(0.5f, 0.5f),
                            0.22f + 0.02f * (n % 3),
                            0f,
                            color,
                            -90f,
                            null,
                            material: RichMaterialOf(BattleVfxShape.Chip)
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
                Illuminate(at, IceLight, 0.6f, 260f, 0.4f, 0.4f, spill: false);
                ColdMist(Feet(FrontLayer, area), 240f, 1f, 0.4f);
                // The pane catches the light, and its dust hangs round it.
                Twinkle(pane + new Vector2(-30f, 40f), Color.white, 110f, 0.3f, 0.1f, 1f);
                DiamondDust(pane, 14, 120f, 1f);
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
                        // A pale crystal the stage shows through, not a white sign over it; it
                        // flashes once as the prison closes.
                        float flash = k > 0.38f && k < 0.45f ? 1.6f : 1f;
                        var tint = Color.Lerp(Color.white, color, 0.45f);
                        tint.a = 0.55f;
                        Show(s, tint, Mathf.Min(1f, k * 1.4f), Eaten(k, 0.6f), ErodeBright * flash);
                    }
                );
                Gather(at, color, 20, 300f, 0.6f);
                yield return Wait(0.62f);
                // The prison: tall spikes of clear ice all round, closing in at once; they all
                // break together a little later (パリーン).
                const float breakAt = 0.6f;
                for (int n = 0; n < 16; n++)
                {
                    bool front = n >= 11;
                    float x =
                        Mathf.Lerp(-220f, 220f, (front ? n - 11 : n) / (front ? 4f : 10f))
                        + UnityEngine.Random.Range(-10f, 10f);
                    float height =
                        (front ? 210f : 400f) * (0.75f + 0.5f * (1f - Mathf.Abs(x) / 220f));
                    ClearSpike(
                        front ? frontLight : backLight,
                        feet + new Vector2(x, front ? -14f : 8f),
                        height,
                        -x * 0.1f,
                        front ? 0.04f : 0f,
                        breakAt
                    );
                }
                var weight = land(index);
                // Not a white flash over the screen (it lifts the deep night to grey): its own cold light.
                Impact(
                    area,
                    at,
                    IceLight,
                    weight,
                    1f,
                    Vector2.right,
                    flashScreen: false,
                    rays: false
                );
                HitStop(0.18f);
                Punch(at, 0.035f);
                Pool(feet, IceLight, 520f, breakAt, 0.5f);
                Illuminate(at, IceLight, 1f, 600f, breakAt, 0.6f);
                // The air chilled to nothing sinks and floods out along the floor.
                ColdMist(feet, 520f, 1.8f, 1f, 0.1f);
                // The floor freezes far out round the prison, and when it breaks its dust hangs
                // in the air and twinkles.
                FrostSheet(feet, 900f, breakAt + 1f);
                StartCoroutine(Shatter(at, feet, 440f, breakAt));
                StartCoroutine(After(breakAt, () => DiamondDust(at, 44, 320f, 1.6f)));
                yield return Wait(breakAt + 0.1f);
            }
            yield return Wait(0.4f);
        }

        /// <summary>
        /// 詠唱短縮, quickening (doc/catalog/skills/skill-quick-cast.md): a circle of runes on the
        /// floor under the user turns faster and faster, lighting it from below; rings close in
        /// on it, each sooner than the last, and motes are drawn in; then the gathered tempo is let
        /// go in a short white flash, a pool of light at its feet and a blue arrow sinking (costs
        /// falling), while the circle wears away.
        /// </summary>
        private IEnumerator QuickCast(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("QuickCast");
            var feet = Feet(FrontLayer, caster);
            var chest = Center(FrontLayer, caster) + new Vector2(0f, 30f);
            DimTo(0.36f, 0.15f);
            if (backLight != null)
            {
                var circle = Spawn(
                    backLight,
                    BattleVfxShape.MagicCircle,
                    feet,
                    new Vector2(320f, 320f),
                    color
                );
                Animate(
                    circle,
                    0.95f,
                    (s, k) =>
                    {
                        // Spinning up: the angle grows with the square of the time.
                        s.Image.rectTransform.localEulerAngles = new Vector3(
                            0f,
                            0f,
                            -k * k * 1500f
                        );
                        float grow = EaseOut(Mathf.Min(1f, k * 4f));
                        s.Holder.localScale = new Vector3(grow, grow * FloorTilt, 1f);
                        Show(s, color, Mathf.Min(1f, k * 2f), Eaten(k, 0.6f), CoreBright);
                    }
                );
            }
            Illuminate(feet + Vector2.down * 80f, color, 0.7f, 280f, 0.45f, 0.3f, spill: false);
            Gather(chest, color, 14, 210f, 0.42f);
            // Rings closing in, each sooner and quicker than the last (a quickening beat).
            float[] starts = { 0f, 0.15f, 0.26f };
            float[] lives = { 0.42f, 0.3f, 0.2f };
            for (int n = 0; n < 3; n++)
            {
                var ring = Spawn(
                    frontLight,
                    BattleVfxShape.Ring,
                    chest,
                    new Vector2(420f, 420f),
                    color
                );
                float delay = starts[n];
                float life = lives[n];
                Animate(
                    ring,
                    delay + life,
                    (s, t) =>
                    {
                        float time = t * (delay + life) - delay;
                        if (time < 0f)
                        {
                            Show(s, Color.clear, 0f, 0f);
                            return;
                        }
                        float k = time / life;
                        // Closing in rather than spreading out.
                        float size = 1f - 0.85f * EaseIn(k);
                        s.Holder.localScale = new Vector3(size, size * 0.55f, 1f);
                        Show(s, color, 0.3f, 0.2f + 0.6f * k);
                    }
                );
            }
            yield return Wait(0.46f);
            Glow(frontLight, chest, Color.white, 150f, 0.12f, 0.9f);
            FlashPool(feet, color, 380f, 0.7f);
            Twinkle(chest + new Vector2(0f, 80f), Color.white, 150f, 0.3f, 0f, -1f);
            Bloom(
                frontLight,
                BattleVfxShape.Chevron,
                chest + new Vector2(0f, 20f),
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
        /// 急所突き, a pinpoint (doc/catalog/skills/skill-vital-thrust.md#演出の時間設計): a red
        /// sight closes in on the enemy while a hairline of the aim runs to it; then the thrust is
        /// already through it, one line from the user out past its back (<see cref="Cut"/>), and
        /// the shock leaves its back in a narrow fan of lines, shards and drops of blood; a
        /// twinkle marks the point, and dust is driven along the floor the way of the thrust. The
        /// enemy is lit red. A blow on a weakness sends gold motes back to the user (the energy
        /// won).
        /// </summary>
        private IEnumerator VitalThrust(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("VitalThrust");
            var from = Center(FrontLayer, caster) + new Vector2(50f, 10f);
            DimTo(0.5f, 0.12f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var dir = (at - from).normalized;
                Bloom(
                    frontLight,
                    BattleVfxShape.Reticle,
                    at,
                    new Vector2(260f, 260f),
                    color,
                    0.5f,
                    0.6f,
                    CoreBright,
                    spin: 60f
                );
                Cut(from, at, color, 0.3f, 0.22f, -1f, 0.3f);
                yield return Wait(0.28f);
                StartCoroutine(
                    Shoot(
                        BattleVfxShape.Streak,
                        from,
                        at + dir * 260f,
                        new Vector2(30f, 240f),
                        new Vector2(0.5f, 0.85f),
                        0.07f,
                        0f,
                        Color.white,
                        -90f,
                        null
                    )
                );
                Cut(from, at + dir * 560f, color, 0.85f, 0.03f, -1f, 0.5f);
                yield return Wait(0.05f);
                var weight = land(index);
                Twinkle(at, Color.white, 150f, 0.22f, 0f, 1f);
                // The shock leaves its back in a narrow fan.
                for (int n = -1; n <= 1; n++)
                {
                    var fan = (Vector2)(
                        Quaternion.Euler(0f, 0f, n * 13f + UnityEngine.Random.Range(-4f, 4f)) * dir
                    );
                    Cut(
                        at + fan * 30f,
                        at + fan * UnityEngine.Random.Range(260f, 380f),
                        color,
                        0.45f,
                        0.03f,
                        -1f,
                        0.32f
                    );
                }
                Spray(
                    frontLight,
                    at + dir * 30f,
                    new Burst
                    {
                        Shape = BattleVfxShape.Shard,
                        Bright = CoreBright,
                        From = Color.white,
                        To = new Color(color.r, color.g, color.b, 0f),
                        Speed = new Vector2(700f, 1500f),
                        Direction = Angle(dir),
                        Spread = 30f,
                        Life = new Vector2(0.15f, 0.34f),
                        Size = new Vector2(8f, 18f),
                        Aspect = 3f,
                        Drag = 3f,
                        Area = 10f,
                    },
                    16
                );
                Sparks(at + dir * 20f, BladeSpark, 10, 1.1f, dir);
                Drops(at + dir * 40f, color, 10);
                DustKick(Feet(FrontLayer, area), dir.x >= 0f ? Vector2.right : Vector2.left, 140f);
                Impact(area, at, color, weight, 0.7f, dir, flashScreen: false, rays: false);
                Punch(at, 0.02f);
                Illuminate(at, color, 0.8f, 260f, 0.06f, 0.35f, spill: false);
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
        /// 毒針, a slow sting (doc/catalog/skills/skill-poison-needle.md): three green needles fly
        /// in quick turn, each leaving a hairline of its flight (<see cref="Cut"/>); a sickly
        /// twinkle where they sink in, drops of venom springing out, and the poison works out of
        /// the enemy as a heavy gas:
        /// it rolls down off it, sinks to the floor and creeps out round its feet in four layers
        /// (a dark body, billows, tatters, a dim green light in it), bubbles rising in it and
        /// bursting; the enemy is lit a flickering green from below.
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
            var trail = new Burst
            {
                Shape = BattleVfxShape.Spark,
                Bright = ErodeBright,
                From = new Color(color.r, color.g, color.b, 0.7f),
                To = new Color(0.4f, 0.2f, 0.5f, 0f),
                Speed = new Vector2(10f, 40f),
                Spread = 40f,
                Life = new Vector2(0.15f, 0.3f),
                Size = new Vector2(4f, 7f),
                Area = 3f,
            };
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var feet = Feet(FrontLayer, area);
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
                            trail
                        )
                    );
                    Cut(
                        from + new Vector2(0f, (n - 1) * 30f),
                        at + new Vector2(0f, (n - 1) * 24f),
                        color,
                        0.3f,
                        0.14f,
                        -1f,
                        0.3f
                    );
                    yield return Wait(0.06f);
                }
                yield return Wait(0.12f);
                var weight = land(index);
                Twinkle(at, color, 130f, 0.22f, 0f, 1f);
                Sparks(at, color, 8, 0.6f, Vector2.right);
                Drops(at, color, 10);
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
                Gas(
                    at,
                    feet,
                    PoisonGas,
                    new Color(color.r, color.g, color.b, 0.45f),
                    400f,
                    1.5f,
                    0.45f
                );
                Bubbles(feet + Vector2.up * 70f, color, Violet, 10);
                Illuminate(
                    feet + Vector2.down * 40f,
                    color,
                    0.55f,
                    260f,
                    0.3f,
                    0.7f,
                    spill: false,
                    flicker: 0.5f
                );
            }
            yield return Wait(0.7f);
        }

        // The gas of poison: a murky olive, sickly where its light shows through.
        private static readonly Color PoisonGas = new(0.3f, 0.46f, 0.14f);

        // The gas of a shadow: a deep violet, near black.
        private static readonly Color ShadowGas = new(0.16f, 0.09f, 0.24f);

        /// <summary>
        /// 影縫い, pinning (doc/catalog/skills/skill-shadow-stitch.md): a dark blade flies down
        /// into the enemy's shadow at its feet, leaving the line of its fall, with a cold violet
        /// flash and sparks; it stays planted there, and stitches run across the shadow one after
        /// another, short lines laid on the floor; a crack opens there, the
        /// shadow pools and creeps out along the floor as a heavy dark gas lit violet at its edge,
        /// and thin threads of shadow rise round the enemy, before and behind it, holding it.
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
                Glow(
                    frontLight,
                    feet + Vector2.up * 10f,
                    Color.Lerp(color, Color.white, 0.6f),
                    140f,
                    0.12f,
                    0.9f
                );
                var pin = feet + new Vector2(-20f, 10f);
                var fall = (pin - from).normalized;
                Cut(pin - fall * 520f, pin, color, 0.55f, 0.03f, -1f, 0.4f);
                Sparks(pin, color, 10, 0.7f, Vector2.up);
                Punch(at, 0.015f);
                // The blade stays planted in the shadow, and is eaten away as the hold sets.
                var blade = Spawn(
                    frontLight,
                    BattleVfxShape.Shard,
                    pin - fall * 62f,
                    new Vector2(30f, 150f),
                    color
                );
                blade.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, Angle(fall) - 90f);
                Animate(
                    blade,
                    1f,
                    (s, k) =>
                        Show(
                            s,
                            Color.Lerp(Color.white, color, Mathf.Min(1f, k * 4f)),
                            k,
                            Eaten(k, 0.6f),
                            ErodeBright
                        )
                );
                // The stitches: short lines across the shadow, one after another.
                for (int n = 0; n < 4; n++)
                {
                    var stitch = feet + new Vector2(-130f + n * 80f, n % 2 == 0 ? -6f : 8f);
                    StartCoroutine(
                        After(
                            0.06f + n * 0.05f,
                            () =>
                                Cut(
                                    stitch + new Vector2(-34f, -16f),
                                    stitch + new Vector2(34f, 16f),
                                    color,
                                    0.4f,
                                    0.03f,
                                    -1f,
                                    0.8f
                                )
                        )
                    );
                }
                var crack = Spawn(
                    backSmoke,
                    BattleVfxShape.Crack,
                    feet,
                    new Vector2(360f, 360f),
                    color
                );
                crack.Image.material = RichMaterialOf(BattleVfxShape.Crack);
                crack.Holder.localScale = new Vector3(1f, FloorTilt, 1f);
                Animate(
                    crack,
                    1.1f,
                    (s, k) => Show(s, color, Mathf.Min(1f, k * 2f), Eaten(k, 0.6f), ErodeBright)
                );
                Gas(
                    feet + Vector2.up * 30f,
                    feet,
                    ShadowGas,
                    new Color(color.r, color.g, color.b, 0.22f),
                    380f,
                    1.3f,
                    0.9f
                );
                // The rim of the pooled shadow catches a cold violet light.
                Pool(feet, color, 300f, 0.2f, 0.7f);
                Illuminate(feet + Vector2.down * 60f, color, 0.6f, 240f, 0.3f, 0.5f, spill: false);
                // Threads of shadow rising round the enemy and holding it.
                RisingLines(feet, Color.Lerp(color, ShadowGas, 0.3f), 16, 140f, 0.35f);
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
        /// 看破の矢, seeing through (doc/catalog/skills/skill-insight-arrow.md): a teal arrow flies
        /// to the enemy, leaving a hairline of its flight (<see cref="Cut"/>); a magic eye opens
        /// over it, and a line of light scans down its body, a thin white core in a teal haze,
        /// lighting it as it passes; where the scan has passed, points on the body twinkle one
        /// after another (what was seen).
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
                Cut(from, at, color, 0.45f, 0.16f, -1f, 0.4f);
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
                    ArrowTrail(color)
                );
                var weight = land(index);
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
                    new Vector2(240f, 240f),
                    color,
                    0.9f,
                    0.65f,
                    CoreBright,
                    spin: 120f
                );
                Twinkle(new Vector2(at.x, high.y + 40f), Color.white, 140f, 0.4f, 0.1f, 1f);
                // What the scan saw: points on the body twinkle after it has passed, top down.
                for (int n = 0; n < 3; n++)
                    Twinkle(
                        new Vector2(
                            Mathf.Lerp(low.x, high.x, UnityEngine.Random.Range(0.25f, 0.75f)),
                            Mathf.Lerp(high.y, low.y, 0.25f + 0.25f * n)
                        ),
                        color,
                        90f,
                        0.28f,
                        0.22f + 0.1f * n,
                        n % 2 == 0 ? 1f : -1f
                    );
                float wide = (high.x - low.x) + 80f;
                float top = high.y;
                float bottom = low.y;
                // The scan: a haze band behind, a thin white core in front, sweeping down.
                for (int layer = 0; layer < 2; layer++)
                {
                    bool core = layer == 1;
                    var scan = Spawn(
                        core ? frontLight : backLight,
                        BattleVfxShape.Beam,
                        new Vector2(at.x - wide * 0.5f, top),
                        new Vector2(wide, core ? 14f : 70f),
                        core ? Color.white : color,
                        new Vector2(0f, 0.5f)
                    );
                    scan.Image.material = RichMaterialOf(
                        BattleVfxShape.Beam,
                        core ? ShaftCore : ShaftHaze
                    );
                    float life = core ? 0.5f : 0.7f;
                    Animate(
                        scan,
                        life,
                        (s, k) =>
                        {
                            float t = k * life;
                            s.Holder.anchoredPosition = new Vector2(
                                at.x - wide * 0.5f,
                                Mathf.Lerp(top, bottom, EaseOut(Mathf.Min(1f, t / 0.45f)))
                            );
                            var tint = core ? Color.white : color;
                            tint.a = core ? 1f : 0.7f;
                            Show(
                                s,
                                tint,
                                0.25f + t * 0.4f,
                                Eaten(k, core ? 0.6f : 0.5f),
                                core ? 2f : 1f
                            );
                        }
                    );
                }
                Illuminate(
                    (Vector2)at + Vector2.up * 60f,
                    color,
                    0.6f,
                    260f,
                    0.4f,
                    0.3f,
                    spill: false
                );
            }
            yield return Wait(0.5f);
        }

        /// <summary>The trail an arrow of light sheds: small motes of its colour, cooling as they drift back.</summary>
        private static Burst ArrowTrail(Color color) =>
            new()
            {
                Shape = BattleVfxShape.Spark,
                Bright = ErodeBright,
                From = Color.Lerp(Color.white, color, 0.4f),
                To = new Color(color.r, color.g, color.b, 0f),
                Speed = new Vector2(10f, 50f),
                Spread = 30f,
                Life = new Vector2(0.12f, 0.25f),
                Size = new Vector2(4f, 8f),
                Area = 3f,
            };

        /// <summary>
        /// アローレイン, a downpour (doc/catalog/skills/skill-arrow-rain.md): arrows are loosed high
        /// off the top, then fall in two volleys slanting across all the enemies, nine to each,
        /// each leaving the hairline of its fall and landing on its own beat with a spark, a
        /// little grit and a flicker on the floor; the arrows stay standing in the floor a
        /// moment where they fell; dust runs out along the floor at each enemy.
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
                        ArrowTrail(color)
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
                    for (int n = 0; n < 9; n++)
                    {
                        var end =
                            feet
                            + new Vector2(
                                UnityEngine.Random.Range(-120f, 120f),
                                UnityEngine.Random.Range(-10f, 170f)
                            );
                        var start = end + new Vector2(-260f, top - end.y);
                        StartCoroutine(
                            FallingArrow(start, end, UnityEngine.Random.Range(0.16f, 0.27f), color)
                        );
                    }
                }
                yield return Wait(0.24f);
                foreach (int index in list)
                {
                    var area = hits[index].Area;
                    var weight = land(index);
                    var feet = Feet(FrontLayer, area);
                    DustKick(feet, Vector2.left, 200f);
                    DustKick(feet, Vector2.right, 200f);
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

        /// <summary>One arrow of a downpour falling to <paramref name="end"/> and striking there: a spark, a bit of grit and a flicker of light on the floor.</summary>
        private IEnumerator FallingArrow(Vector2 start, Vector2 end, float seconds, Color color)
        {
            Cut(start, end, color, 0.3f, seconds, -1f, seconds + 0.22f);
            yield return Shoot(
                BattleVfxShape.Arrow,
                start,
                end,
                new Vector2(140f, 40f),
                new Vector2(0.82f, 0.5f),
                seconds,
                0f,
                color,
                0f,
                ArrowTrail(color)
            );
            Glow(frontLight, end, Color.Lerp(color, Color.white, 0.5f), 70f, 0.1f, 0.9f);
            Sparks(end, color, 3, 0.4f, Vector2.up);
            FlashPool(end + Vector2.down * 10f, color, 160f, 0.5f);
            // It stays standing in the floor where it fell, and is eaten away from its tail.
            var stuck = Spawn(
                frontLight,
                BattleVfxShape.Arrow,
                end,
                new Vector2(120f, 34f),
                color,
                new Vector2(0.82f, 0.5f)
            );
            stuck.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, Angle(end - start));
            var dim = new Color(color.r, color.g, color.b, 0.75f);
            Animate(stuck, 0.55f, (s, k) => Show(s, dim, 1f, Eaten(k, 0.35f)));
        }

        /// <summary>
        /// シャドウスナイプ, silence then a hole (doc/catalog/skills/skill-shadow-snipe.md): it goes
        /// almost black, a gold sight locks on slowly with a faint aiming line, then one arrow
        /// crosses in a blink, a flash at the bow and dust kicked back behind the user, and a
        /// shaft of light bursts through and past the enemy in four layers (a violet haze and
        /// body behind it, a white core for an instant and a violet trace in front); along the
        /// whole shot the air is left cut, a dark slit that opens and closes
        /// (<see cref="Cut"/>); the shock leaves the enemy's back in a fan of lines, shards
        /// blowing out with it, and the field is lit along the line.
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
                Star(from, Color.white, 190f, 0.1f);
                DustKick(
                    Feet(FrontLayer, caster),
                    dir.x >= 0f ? Vector2.left : Vector2.right,
                    170f
                );
                Cut(from, at + dir * 1100f, color, 1f, 0.03f, 0.05f, 1.1f);
                Twinkle(at, Color.white, 170f, 0.24f, 0f, 1f);
                for (int n = -2; n <= 2; n++)
                {
                    if (n == 0)
                        continue;
                    var fan = (Vector2)(
                        Quaternion.Euler(0f, 0f, n * 9f + UnityEngine.Random.Range(-3f, 3f)) * dir
                    );
                    Cut(
                        at + fan * 40f,
                        at + fan * UnityEngine.Random.Range(380f, 620f),
                        color,
                        0.5f,
                        0.03f,
                        -1f,
                        0.4f
                    );
                }
                Shaft(at - dir * 140f, at + dir * 1100f, color, 80f, 0.03f, 0.32f);
                Illuminate(at + dir * 300f, color, 1f, 700f, 0.05f, 0.4f);
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
                    Shockwave(Feet(FrontLayer, area), 520f, 0.7f);
            }
            yield return Wait(0.45f);
        }

        /// <summary>
        /// ライトニングボルト, a running flash, as a real discharge looks: a faint leader feels its
        /// way sideways from the user to the enemy, the channel it opened burns white (the core)
        /// in a hazy yellow glow and lights again twice down the same path, the field lit with
        /// each stroke; a yellow trace is left in the eye and breaks up; crackles run round the
        /// enemy (numbed).
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
            const float leader = 0.08f;
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                Channel(from, at, BoltLight(color), BoltTraceOf(color), leader, BoltStrokes, 420f);
                StartCoroutine(
                    ChannelLights(from, at, Feet(FrontLayer, area), color, leader, BoltStrokes, 1f)
                );
                yield return Wait(leader);
                var weight = land(index);
                Glow(frontLight, at, Color.white, 170f, 0.14f, 0.9f);
                Sparks(at, color, 20, 0.9f, (at - from).normalized);
                GroundArcs(Feet(FrontLayer, area), color, 5, 200f);
                Punch(at, 0.02f);
                HitStop(Weighted(0.05f, weight));
                Shake(Weighted(0.28f, weight), (at - from).normalized);
                StartCoroutine(Crackle(area, color));
                StartCoroutine(Crackle(area, Violet));
            }
            yield return Wait(0.5f);
        }

        /// <summary>
        /// チェインライトニング, jumping (doc/catalog/skills/skill-chain-lightning.md): a bolt
        /// leaps from the user to the first enemy and on from each enemy hit to the next, each
        /// jump a real discharge in four layers on one path (a faint leader, the white core at
        /// the stroke and once more on the same path, a hazy glow, a trace left in the eye), so
        /// the path it took stays in the eye a moment as a chain. Each landing lights the field
        /// and the floor, flares, throws sparks and leaves a wisp of scorched smoke.
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
            yield return StaticCharge(caster, 0.35f);
            const float leader = 0.06f;
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var feet = Feet(FrontLayer, area);
                var dir = (at - from).normalized;
                Channel(from, at, BoltLight(color), BoltTraceOf(color), leader, JumpStrokes, 300f);
                StartCoroutine(ChannelLights(from, at, feet, color, leader, JumpStrokes, 0.85f));
                yield return Wait(leader);
                var weight = land(index);
                Glow(frontLight, at, Color.white, 150f, 0.12f, 0.9f);
                Spray(
                    backSmoke,
                    at + Vector2.down * 20f,
                    new Burst
                    {
                        Shape = BattleVfxShape.Smoke,
                        Bright = 0.4f,
                        From = new Color(0.3f, 0.28f, 0.32f, 0.5f),
                        To = new Color(0.3f, 0.28f, 0.32f, 0.5f),
                        Speed = new Vector2(20f, 60f),
                        Direction = 90f,
                        Spread = 40f,
                        Life = new Vector2(0.7f, 1f),
                        Size = new Vector2(60f, 100f),
                        Grow = 0.4f,
                        Shrink = 1.6f,
                        Drag = 0.8f,
                        Hold = 0.35f,
                        Area = 20f,
                        Delay = new Vector2(0.08f, 0.2f),
                    },
                    2
                );
                Sparks(at, color, 16, 0.8f, dir);
                GroundArcs(feet, color, 3, 150f);
                StartCoroutine(Crackle(area, color));
                HitStop(Weighted(0.04f, weight));
                Shake(Weighted(0.22f, weight), dir);
                from = at;
                yield return Wait(0.16f);
            }
            yield return Wait(0.35f);
        }

        /// <summary>The glow of the air round a bolt of <paramref name="color"/>: a paler, whiter tone of it.</summary>
        private static Color BoltLight(Color color) => Color.Lerp(color, Color.white, 0.35f);

        /// <summary>The trace a bolt of <paramref name="color"/> leaves in the eye: a deeper tone of it.</summary>
        private static Color BoltTraceOf(Color color) =>
            new(color.r, color.g * 0.8f, color.b * 0.6f);

        /// <summary>
        /// 雷雲, called: the user gathers violet light for the storm. The cloud itself comes only
        /// when it strikes, at the start of each party turn (<see cref="CloudStrike"/>), so it rolls
        /// in once and strikes instead of gathering now, drawing off and coming back.
        /// </summary>
        private IEnumerator Thundercloud(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            yield return StaticCharge(caster, 0.4f);
            foreach (int index in Wave(hits, 0))
                land(index);
            yield return Wait(0.3f);
        }

        /// <summary>
        /// 雷神の槍, judgement (doc/catalog/skills/skill-thunder-spear.md): the sky goes to night,
        /// motes of light are drawn up high over the enemy into a spear of lightning that
        /// crackles in place, lighting the field from above; then it drops: the bolt runs from
        /// the spear down to the enemy in four layers on one path and lights again twice, the
        /// screen flashes once, the floor is lit wide and scorched, dust runs out and the battle
        /// stops; the lightning runs on low along the floor to every other enemy, striking each.
        /// </summary>
        private IEnumerator ThunderSpear(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("ThunderSpear");
            var halo = BoltLight(color);
            var trace = BoltTraceOf(color);
            DimTo(NightDim, 0.2f);
            yield return StaticCharge(caster, 0.45f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var feet = Feet(FrontLayer, area);
                var high = new Vector2(at.x, FrontLayer.rect.yMax - 170f);
                // The spear forming: motes drawn up into it, and the spear itself crackling in
                // place, the same short channel lit again and again, brighter each time.
                Gather(high, color, 18, 260f, 0.45f);
                Glow(backLight, high, color, 380f, 0.65f, 0.6f);
                Channel(
                    high + Vector2.up * 150f,
                    high + Vector2.down * 150f,
                    halo,
                    trace,
                    0.05f,
                    SpearCharge,
                    120f
                );
                Illuminate(high, color, 0.7f, 600f, 0.45f, 0.2f, spill: false, flicker: 0.6f);
                yield return Wait(0.55f);
                // The drop: one bolt from the spear to the enemy.
                const float leader = 0.04f;
                var top = high + Vector2.up * 150f;
                Channel(top, feet + Vector2.up * 6f, halo, trace, leader, BoltStrokes, 380f);
                StartCoroutine(
                    ChannelLights(
                        top,
                        feet + Vector2.up * 6f,
                        feet,
                        color,
                        leader,
                        BoltStrokes,
                        1.5f
                    )
                );
                yield return Wait(leader);
                var weight = land(index);
                ScreenFlash(new Color(1f, 0.95f, 0.75f), MaxFlash);
                // The dark lifts with the flash and comes back.
                DimTo(0.2f, 0f);
                StartCoroutine(After(0.1f, () => DimTo(NightDim, 0.4f)));
                Glow(frontLight, feet + Vector2.up * 10f, Color.white, 240f, 0.16f, 0.95f);
                Shockwave(feet, 700f, 1f);
                GroundArcs(feet, color, 8, 320f);
                FloorMark(
                    BattleVfxShape.Scorch,
                    feet,
                    new Vector2(320f, 100f),
                    new Color(0.1f, 0.08f, 0.1f),
                    0.8f,
                    1.6f,
                    flat: false
                );
                Sparks(feet + Vector2.up * 10f, color, 18, 1f, Vector2.up);
                HitStop(Weighted(0.14f, weight));
                Shake(Weighted(0.45f, weight), Vector2.down);
                Punch(at, 0.04f);
                StartCoroutine(Crackle(area, Violet));
                StartCoroutine(Rumble());
                yield return Wait(0.2f);
                var from = feet + Vector2.up * 10f;
                foreach (int other in Wave(hits, 1))
                {
                    var otherArea = hits[other].Area;
                    var otherFeet = Feet(FrontLayer, otherArea);
                    var to = otherFeet + Vector2.up * 10f;
                    // Low along the floor: it hardly swings up off it.
                    Channel(from, to, halo, trace, 0.05f, JumpStrokes, 90f);
                    StartCoroutine(
                        ChannelLights(from, to, otherFeet, color, 0.05f, JumpStrokes, 0.7f)
                    );
                    yield return Wait(0.05f);
                    var otherWeight = land(other);
                    GroundArcs(otherFeet, color, 3, 140f);
                    Glow(frontLight, to, Color.white, 130f, 0.12f, 0.9f);
                    HitStop(Weighted(0.03f, otherWeight));
                    StartCoroutine(Crackle(otherArea, color));
                    yield return Wait(0.06f);
                }
            }
            yield return Wait(0.45f);
        }

        // The spear of lightning crackling in place before it drops: the same short channel lit
        // again and again, brighter as it charges.
        private static readonly (float[] times, float[] peaks) SpearCharge = (
            new[] { 0f, 0.12f, 0.22f, 0.3f, 0.36f, 0.41f },
            new[] { 0.3f, 0.35f, 0.45f, 0.55f, 0.7f, 0.85f }
        );

        /// <summary>
        /// 疾風迅雷, light on the feet (doc/catalog/skills/skill-gale.md): a gust sweeps through the
        /// party from the left as wind looks, not as light: long thin streams of driven air,
        /// one behind and one in front of the party, fibrous and gusting as they run past, fast
        /// white streaks and leaves riding them; then each ally twitches with yellow sparks, thin
        /// lines rise off it and an arrow points up.
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
            var air = Color.Lerp(Color.white, color, 0.3f);
            // The streams of air, behind and in front of the party, running left to right.
            for (int layer = 0; layer < 2; layer++)
            {
                var host = layer == 0 ? backLight : frontLight;
                if (host == null)
                    continue;
                var stream = Spawn(
                    host,
                    BattleVfxShape.Tornado,
                    mid + new Vector2(0f, layer == 0 ? 60f : -60f),
                    new Vector2(1500f, layer == 0 ? 360f : 260f),
                    air
                );
                stream.Image.material = RichMaterialOf(BattleVfxShape.Tornado);
                float life = layer == 0 ? 0.8f : 0.65f;
                float strength = layer == 0 ? 0.8f : 0.6f;
                Animate(
                    stream,
                    life,
                    (s, k) =>
                    {
                        float gust =
                            Mathf.Min(1f, k * 6f)
                            * (1f - EaseIn(Mathf.Clamp01((k - 0.45f) / 0.55f)));
                        Show(s, new Color(air.r, air.g, air.b, strength * gust), k * 1.2f, 0f);
                    }
                );
            }
            // Fast streaks racing across, row by row.
            for (int n = 0; n < 4; n++)
            {
                float y = mid.y - 180f + n * 110f;
                Spray(
                    n % 2 == 0 ? frontLight : backLight,
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
                        Size = new Vector2(5f, 9f),
                        Stretch = 0.08f,
                        Area = 50f,
                        Delay = new Vector2(0f, 0.15f),
                    },
                    7
                );
                yield return Wait(0.05f);
            }
            Leaves(
                frontLight,
                new Vector2(mid.x - 500f, mid.y),
                new Color(0.58f, 0.74f, 0.34f),
                12,
                4f,
                1100f,
                40f
            );
            yield return Wait(0.2f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                StartCoroutine(Crackle(area, color));
                RisingLines(Feet(FrontLayer, area), color, 10, 120f, 0.25f);
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
        /// 偵察, a look round (doc/catalog/skills/skill-scout.md): a teal sight sweeps across the
        /// enemy side over a patch of light on the floor that follows it, lighting each enemy as
        /// it passes, and two twinkles fly back up from it (the cards drawn).
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
            if (backLight != null)
            {
                // The patch of light the sight casts on the floor, following it.
                float floor = MidFeet(hits, -1).y;
                var patch = Spawn(
                    backLight,
                    BattleVfxShape.Glow,
                    new Vector2(300f, floor),
                    new Vector2(300f, 300f),
                    color
                );
                Animate(
                    patch,
                    0.7f,
                    (s, k) =>
                    {
                        s.Holder.anchoredPosition = new Vector2(
                            Mathf.Lerp(300f, 820f, EaseOut(k)),
                            floor
                        );
                        s.Holder.localScale = new Vector3(1f, FloorTilt, 1f);
                        Show(
                            s,
                            new Color(color.r, color.g, color.b, 0.5f * Mathf.Sin(k * Mathf.PI)),
                            k,
                            0f
                        );
                    }
                );
            }
            for (int n = 0; n < 3; n++)
            {
                float x = Mathf.Lerp(380f, 780f, n / 2f);
                StartCoroutine(
                    After(
                        0.12f + n * 0.12f,
                        () =>
                            Illuminate(
                                new Vector2(x, 60f),
                                color,
                                0.5f,
                                220f,
                                0.05f,
                                0.3f,
                                spill: false
                            )
                    )
                );
            }
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
                        ArrowTrail(color)
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
        /// 万歩の祈り, every step counting (doc/catalog/skills/skill-step-prayer.md): golden
        /// footprints light up along the floor through the party, then healing rises off each ally
        /// from the floor up: a pool of light at its feet first, thin lines of light rising
        /// round it, before and behind, a soft glow settling on its body last, and a few leaves
        /// drifting up.
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
                var at = Center(FrontLayer, area);
                Pool(feet, color, 300f, 0.5f, 0.5f);
                RisingLines(feet, Color.Lerp(color, Gold, 0.25f), 16, 140f, 0.5f);
                SoftHalo(at, Color.Lerp(color, Gold, 0.2f), 300f, 0.15f, 1f);
                Leaves(frontLight, feet + Vector2.up * 30f, color, 4, 90f, 120f);
                Illuminate(at, Color.Lerp(color, Gold, 0.3f), 0.6f, 260f, 0.4f, 0.5f, spill: false);
                land(index);
                yield return Wait(0.05f);
            }
            yield return Wait(0.5f);
        }

        /// <summary>
        /// A soft glow settling on a body behind it: it comes up slowly after
        /// <paramref name="delay"/>, breathes once and fades over about <paramref name="seconds"/>
        /// (a heal's warmth, never a flash).
        /// </summary>
        private void SoftHalo(Vector2 at, Color color, float size, float delay, float seconds)
        {
            if (backLight == null)
                return;
            var shot = Spawn(backLight, BattleVfxShape.Glow, at, new Vector2(size, size), color);
            float total = delay + seconds;
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
                    // A slow rise, one gentle breath, then a long fall (F3 with a smooth pulse).
                    float envelope =
                        Mathf.SmoothStep(0f, 1f, k / 0.35f)
                        * (1f - Mathf.SmoothStep(0f, 1f, (k - 0.55f) / 0.45f));
                    float breath = 0.85f + 0.15f * Mathf.Sin(k * Mathf.PI * 2f);
                    s.Holder.localScale = Vector3.one * (0.85f + 0.2f * k);
                    Show(s, new Color(color.r, color.g, color.b, 0.55f * envelope * breath), k, 0f);
                }
            );
        }

        /// <summary>
        /// リジェネ, budding (doc/catalog/skills/skill-regen.md): a ring of green spreads on the
        /// floor under the ally, then leaves and motes bud round its feet and spiral up round it,
        /// passing behind and in front of it, opening out as they rise; a gentle warmth settles on
        /// it. (Each turn after, <see cref="StatusTick"/> shows only a quiet pulse of it.)
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
                Pool(feet, color, 260f, 0.6f, 0.6f);
                Sprout(feet, color, 14, 1.1f);
                SoftHalo(Center(FrontLayer, area), color, 300f, 0.25f, 1.1f);
                land(index);
            }
            yield return Wait(0.9f);
        }

        /// <summary>
        /// Leaves (and, every third, a mote) budding round <paramref name="feet"/> one after
        /// another and spiralling up round the body over about <paramref name="seconds"/>,
        /// opening out as they rise; each passes behind the body on the far side of its turn and
        /// in front of it on the near side.
        /// </summary>
        private void Sprout(Vector2 feet, Color color, int count, float seconds)
        {
            if (backLight == null || frontLight == null)
                return;
            for (int n = 0; n < count; n++)
            {
                bool leaf = n % 3 != 2;
                var shot = Spawn(
                    frontLight,
                    leaf ? BattleVfxShape.Leaf : BattleVfxShape.Sparkle,
                    feet,
                    leaf ? new Vector2(20f, 36f) : new Vector2(22f, 22f),
                    color
                );
                float phase = n * (360f / count) + UnityEngine.Random.Range(-15f, 15f);
                float delay = n * seconds * 0.35f / count;
                float life = seconds * UnityEngine.Random.Range(0.75f, 1f);
                float rise = UnityEngine.Random.Range(220f, 320f);
                float total = delay + life;
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
                        float k = time / life;
                        float angle = (phase + k * 320f) * Mathf.Deg2Rad;
                        float radius = 90f * (0.35f + 0.65f * EaseOut(k));
                        float depth = Mathf.Sin(angle);
                        var layer = depth > 0f ? backLight : frontLight;
                        if (s.Holder.parent != layer)
                            s.Holder.SetParent(layer, false);
                        // A smooth climb, slow at first and last.
                        float up = rise * Mathf.SmoothStep(0f, 1f, k);
                        s.Holder.anchoredPosition =
                            feet
                            + new Vector2(
                                Mathf.Cos(angle) * radius,
                                20f + up + depth * radius * 0.3f
                            );
                        s.Image.rectTransform.localEulerAngles = new Vector3(
                            0f,
                            0f,
                            angle * Mathf.Rad2Deg
                        );
                        var tint = new Color(color.r, color.g, color.b, Mathf.Min(1f, k * 6f));
                        Show(s, tint, k, Eaten(k, 0.6f), depth > 0f ? 1f : ErodeBright);
                    }
                );
            }
        }

        /// <summary>
        /// リザレクション, a call from above (doc/catalog/skills/skill-resurrection.md): the stage
        /// dims, a white-gold circle opens on the floor under the ally and a shaft of light comes
        /// slowly down onto it as real light does, seen in the dust (a wide haze and its body
        /// behind the ally, a white core and a gold trace in front), specks drifting down in it;
        /// motes are drawn into the ally, then it flares white, a ring runs out on the floor, thin
        /// lines of light rise off it and twinkles burst out.
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
                Shaft(new Vector2(at.x, top), feet, Color.Lerp(color, Gold, 0.3f), 220f, 0.4f, 1f);
                Pool(feet, color, 380f, 0.9f, 0.6f);
                DustInLight(new Vector2(at.x, top - 80f), Gold, -1f);
                Illuminate(
                    new Vector2(at.x, top - 200f),
                    color,
                    0.6f,
                    500f,
                    0.6f,
                    0.4f,
                    spill: false
                );
                yield return Wait(0.2f);
                Gather(at, Gold, 18, 230f, 0.4f);
                yield return Wait(0.4f);
                land(index);
                Glow(frontLight, at, Color.white, 220f, 0.16f, 0.95f);
                SoftHalo(at, Color.Lerp(color, Gold, 0.4f), 380f, 0.05f, 1.1f);
                Illuminate(at, Color.white, 1f, 360f, 0.4f, 0.6f);
                Ring(backLight, feet, color, 520f, 0.6f, floor: true);
                RisingLines(feet, Gold, 18, 160f, 0.4f);
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
                        Size = new Vector2(14f, 30f),
                        Gravity = -120f,
                        Drag = 2f,
                        Spin = 200f,
                        Area = 40f,
                    },
                    12
                );
                ScreenFlash(Color.white, 0.18f);
            }
            yield return Wait(0.6f);
        }

        /// <summary>
        /// 聖なる鉄槌, judgement (doc/catalog/skills/skill-holy-hammer.md): a golden circle opens
        /// in the air over the enemy and a heavy shaft of gold slams down from it in four layers
        /// (a gold haze and body behind the enemy, a white core for an instant and a gold trace
        /// in front); the floor cracks, chips fly, a wide pool of gold lies on the floor, pale
        /// shards of a broken blessing fly off the enemy and specks of light rise in what is left.
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
                Illuminate(sky, color, 0.5f, 400f, 0.3f, 0.2f, spill: false);
                yield return Wait(0.35f);
                Twinkle(sky, Color.white, 200f, 0.24f, 0f, 1f);
                Shaft(sky, feet + Vector2.down * 10f, color, 400f, 0.06f, 0.35f);
                // The edge of the blow: a hard line down the middle of the shaft.
                Cut(sky, feet + Vector2.down * 10f, color, 0.9f, 0.05f, -1f, 0.45f);
                yield return Wait(0.06f);
                var weight = land(index);
                Shockwave(feet, 520f, 0.9f);
                Punch(at, 0.03f);
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
                Pool(feet, color, 520f, 0.1f, 0.5f);
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
                DustInLight(feet + Vector2.up * 60f, color, 1f);
            }
            yield return Wait(0.45f);
        }

        /// <summary>
        /// プロテクト, gently wrapped (doc/catalog/skills/skill-protect.md): small crests circle
        /// each ally, passing behind and in front of it, and fold into a soft barrier round it: a
        /// membrane behind and a faint rim in front, so the ally stands inside, a ring spreading
        /// on the floor as it closes.
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
                            var layer = Mathf.Sin(angle) > 0f ? backLight : frontLight;
                            if (layer != null && s.Holder.parent != layer)
                                s.Holder.SetParent(layer, false);
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
                Barrier(
                    at,
                    new Vector2(260f, 300f),
                    Color.Lerp(color, Gold, 0.3f),
                    1.1f,
                    0.45f,
                    0.7f
                );
                StartCoroutine(
                    After(
                        0.45f,
                        () =>
                            Ring(backLight, Feet(FrontLayer, area), color, 300f, 0.5f, floor: true)
                    )
                );
                land(index);
                yield return Wait(0.04f);
            }
            yield return Wait(0.8f);
        }

        /// <summary>
        /// A barrier round a body at <paramref name="at"/>, springing up after
        /// <paramref name="delay"/>: the membrane behind the body and a faint rim of it in front
        /// (<paramref name="front"/> as strong), so the body stands inside it, lasting
        /// <paramref name="seconds"/> and worn away.
        /// </summary>
        private void Barrier(
            Vector2 at,
            Vector2 size,
            Color color,
            float seconds,
            float delay,
            float hold,
            float front = 0.35f
        )
        {
            // Drawn as a film of light (the bubble's rich look): a Fresnel rim, a thin film with
            // a fine hexagon pattern, a ripple running in from the rim, worn away cell by cell.
            var film = RichMaterialOf(BattleVfxShape.Bubble);
            var back = Bloom(
                backLight,
                BattleVfxShape.Bubble,
                at,
                size,
                color,
                seconds,
                hold,
                ErodeBright,
                delay: delay
            );
            var rim = Bloom(
                frontLight,
                BattleVfxShape.Bubble,
                at,
                size,
                new Color(color.r, color.g, color.b, front),
                seconds,
                hold,
                1f,
                delay: delay
            );
            if (back != null)
                back.Image.material = film;
            if (rim != null)
                rim.Image.material = film;
        }

        /// <summary>
        /// 浄化の光, clearing (doc/catalog/skills/skill-purify.md): a soft, wide light comes down
        /// over the party, a light rain of twinkles falling through it; dark wisps are driven up
        /// off each ally and dissolve, and a clear ring and a cool warmth are left.
        /// </summary>
        private IEnumerator Purify(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("Purify");
            var clear = new Color(0.6f, 0.95f, 1f);
            DimTo(0.3f, 0.2f);
            var mid = MidFeet(hits, 0);
            Shaft(
                new Vector2(mid.x, FrontLayer.rect.yMax + 40f),
                mid + Vector2.down * 140f,
                clear,
                560f,
                0.35f,
                0.9f,
                core: 0f
            );
            Spray(
                frontLight,
                new Vector2(mid.x, FrontLayer.rect.yMax),
                new Burst
                {
                    Shape = BattleVfxShape.Sparkle,
                    From = Color.white,
                    To = new Color(clear.r, clear.g, clear.b, 0f),
                    Speed = new Vector2(150f, 300f),
                    Direction = -90f,
                    Spread = 20f,
                    Life = new Vector2(0.9f, 1.3f),
                    Size = new Vector2(12f, 26f),
                    Spin = 120f,
                    Area = 360f,
                    Delay = new Vector2(0f, 0.5f),
                },
                26
            );
            yield return Wait(0.35f);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                // The taint driven up out of the ally, dissolving as it rises.
                Spray(
                    backSmoke,
                    at,
                    new Burst
                    {
                        Shape = BattleVfxShape.Smoke,
                        Bright = 0.35f,
                        From = new Color(0.24f, 0.13f, 0.3f, 0.5f),
                        To = new Color(0.24f, 0.13f, 0.3f, 0.5f),
                        Speed = new Vector2(60f, 160f),
                        Direction = 90f,
                        Spread = 50f,
                        Life = new Vector2(0.6f, 0.9f),
                        Size = new Vector2(60f, 110f),
                        Grow = 0.5f,
                        Shrink = 1.5f,
                        Gravity = -120f,
                        Drag = 1f,
                        Spin = 30f,
                        Hold = 0.2f,
                        Area = 50f,
                    },
                    4
                );
                Ring(backLight, Feet(FrontLayer, area), clear, 280f, 0.6f, floor: true);
                SoftHalo(at, Color.Lerp(clear, Color.white, 0.2f), 280f, 0.1f, 0.8f);
                land(index);
                yield return Wait(0.04f);
            }
            yield return Wait(0.6f);
        }

        /// <summary>
        /// マナの祈り, filling up (doc/catalog/skills/skill-mana-prayer.md): blue motes are drawn
        /// into the user from all round, its body breathes with a blue glow that swells as it
        /// fills, thin blue lines rise round it, and two orbs of light float up and away
        /// trailing motes (the energy).
        /// </summary>
        private IEnumerator ManaPrayer(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf("ManaPrayer");
            var at = Center(FrontLayer, caster);
            var feet = Feet(FrontLayer, caster);
            Gather(at, color, 16, 300f, 0.5f);
            yield return Charge(caster, color, 0.3f, big: false);
            SoftHalo(at, color, 320f, 0f, 0.9f);
            RisingLines(feet, color, 14, 140f, 0.35f);
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
                        ArrowTrail(color)
                    )
                );
            Illuminate(at, color, 0.6f, 260f, 0.3f, 0.4f, spill: false);
            foreach (int index in Wave(hits, 0))
                land(index);
            yield return Wait(0.6f);
        }

        /// <summary>
        /// 天啓, a flash of insight (doc/catalog/skills/skill-revelation.md): a thin shaft of gold
        /// comes down on the user as real light does (a haze and its body behind it, a white
        /// core for a moment and a gold trace in front), specks of dust drifting in it; a great
        /// twinkle flares over its head and a gold ring spreads at its feet.
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
            var center = Center(FrontLayer, caster);
            Shaft(new Vector2(center.x, FrontLayer.rect.yMax + 40f), feet, color, 120f, 0.3f, 0.7f);
            DustInLight(new Vector2(center.x, head.y + 60f), color, -1f);
            yield return Wait(0.35f);
            Twinkle(
                new Vector2((feet.x + head.x) * 0.5f, head.y + 30f),
                Color.white,
                260f,
                0.55f,
                0f,
                1f
            );
            Pool(feet, color, 300f, 0.2f, 0.5f);
            Ring(backLight, feet, color, 360f, 0.5f, floor: true);
            Illuminate(feet + Vector2.up * 100f, color, 0.7f, 300f, 0.2f, 0.4f);
            foreach (int index in Wave(hits, 0))
                land(index);
            yield return Wait(0.5f);
        }

        /// <summary>
        /// ホーリーライト, a flood of light, as strong light from above looks in real air: a small,
        /// blinding source high in the middle with a glare round it; shafts of light from it to
        /// every enemy, seen because the dust in the air scatters them, each in four layers (a
        /// haze and the shaft behind the enemy, a white core at the instant it lands and a gold
        /// trace in front), specks of that dust glinting in them; where each lands, a pool of
        /// light on the floor, the enemy lit gold and a glare; then light falls on every ally, a
        /// warm pool at their feet and a gold-green light on them, with specks of dust drifting
        /// down through it as they heal. No cloud: light makes none. Its light is gold and burns
        /// white only at its brightest; the screen does not flash.
        /// </summary>
        private IEnumerator HolyLight(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            var gold = HolyGold;
            var white = Color.Lerp(gold, Color.white, 0.6f);
            DimTo(0.6f, 0.2f);
            yield return Inhale(caster, gold, 0.35f);
            var source = new Vector2(0f, FrontLayer.rect.yMax - 220f);
            Glow(backLight, source, gold, 260f, 1.2f, 0.9f);
            Twinkle(source, white, 280f, 0.6f, 0.08f, 1f);
            Illuminate(source, gold, 0.6f, 800f, 0.5f, 0.6f);
            yield return Wait(0.3f);
            // Every shaft is drawn down first, and each blow lands as its shaft reaches the enemy.
            foreach (int index in Wave(hits, 0))
                Shaft(source, Center(FrontLayer, hits[index].Area), gold, 150f, HolyDraw, 0.4f);
            yield return WaitReal(HolyDraw);
            foreach (int index in Wave(hits, 0))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var feet = Feet(FrontLayer, area);
                var weight = land(index);
                Twinkle(at, white, Weighted(220f, weight), 0.3f, 0f, 1f);
                HitStop(Weighted(0.06f, weight));
                Shake(Weighted(0.3f, weight), Vector2.down);
                Pool(feet, gold, 340f, 0.2f, 0.6f);
                Illuminate(at, gold, 0.9f, 380f, 0.1f, 0.5f, spill: false);
                DustInLight(at, gold, 1f);
            }
            yield return Wait(0.3f);
            var heal = Color.Lerp(gold, ColorOf(BattleSkillVfxKind.Heal), 0.5f);
            foreach (int index in Wave(hits, 1))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                Pool(Feet(FrontLayer, area), heal, 260f, 0.4f, 0.6f);
                Illuminate(at, heal, 0.7f, 300f, 0.4f, 0.5f, spill: false);
                DustInLight(at + Vector2.up * 120f, gold, -1f);
                land(index);
            }
            yield return Wait(0.5f);
        }

        /// <summary>
        /// Specks of dust caught glinting in a light at <paramref name="at"/>: tiny points that
        /// drift up (<paramref name="rise"/> 1) or settle down (-1) through it and fade.
        /// </summary>
        private void DustInLight(Vector2 at, Color color, float rise)
        {
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Spark,
                    Bright = CoreBright,
                    From = Color.white,
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(20f, 90f),
                    Direction = rise > 0f ? 90f : -90f,
                    Spread = 70f,
                    Life = new Vector2(0.8f, 1.4f),
                    Size = new Vector2(3f, 6f),
                    Drag = 1.5f,
                    Area = 80f,
                    Delay = new Vector2(0f, 0.3f),
                },
                16
            );
        }

        /// <summary>
        /// ディバインシールド, absolute (doc/catalog/skills/skill-divine-shield.md): a thin shaft of
        /// gold comes down onto the ally first, then a golden barrier springs up round it (the
        /// membrane behind, its rim in front), a great crest flashes before it, thin gold lines
        /// rise inside, specks drift down and a gold ring spreads on the floor; the barrier holds
        /// a while.
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
                Shaft(
                    new Vector2(at.x, FrontLayer.rect.yMax + 40f),
                    feet,
                    color,
                    130f,
                    0.18f,
                    0.5f
                );
                StartCoroutine(
                    After(
                        0.18f,
                        () =>
                        {
                            Barrier(at, new Vector2(320f, 360f), color, 1.6f, 0f, 0.8f, 0.4f);
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
                            RisingLines(feet, Gold, 14, 200f, 0.6f);
                            Ring(backLight, feet, color, 420f, 0.6f, floor: true);
                            Pool(feet, color, 420f, 0.5f, 0.7f);
                            Illuminate(at, color, 0.8f, 320f, 0.4f, 0.6f);
                            Shake(0.1f, Vector2.zero);
                        }
                    )
                );
                DustInLight(at + Vector2.up * 220f, Gold, -1f);
                land(index);
            }
            yield return Wait(0.9f);
        }

        // --- Statuses that act later -----------------------------------------------------------

        /// <summary>
        /// A status working on a character at the start of a turn, each its own and none the card
        /// that put it there played again (doc/art/direction.md, effects that act later): a burn
        /// licks at it, smoke curling up behind and its light flickering on it; poison wells up in
        /// bubbles and drips off it heavily, a sickly light pulsing in it; a wound spurts dark
        /// drops that fall, with a small red glint; regen breathes a quiet warmth into it, a pool
        /// on the floor and a few motes rising like dew. <paramref name="land"/> is called as it
        /// takes effect.
        /// </summary>
        public IEnumerator StatusTick(CardStatus status, RectTransform target, Action land)
        {
            if (FrontLayer == null || target == null)
            {
                land?.Invoke();
                yield break;
            }
            var at = Center(FrontLayer, target);
            var feet = Feet(FrontLayer, target);
            switch (status)
            {
                case CardStatus.Burn:
                    Licks(target, 6, 0.7f);
                    Spray(
                        backSmoke,
                        at + Vector2.up * 40f,
                        new Burst
                        {
                            Shape = BattleVfxShape.Smoke,
                            Bright = 0.35f,
                            From = new Color(0.25f, 0.2f, 0.18f, 0.5f),
                            To = new Color(0.25f, 0.2f, 0.18f, 0.5f),
                            Speed = new Vector2(30f, 70f),
                            Direction = 90f,
                            Spread = 30f,
                            Life = new Vector2(0.7f, 1f),
                            Size = new Vector2(60f, 100f),
                            Grow = 0.4f,
                            Shrink = 1.6f,
                            Gravity = -60f,
                            Drag = 0.8f,
                            Spin = 20f,
                            Hold = 0.3f,
                            Area = 30f,
                            Delay = new Vector2(0.1f, 0.3f),
                        },
                        3
                    );
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
                    Bubbles(at, Venom, Violet, 6);
                    Spray(
                        frontLight,
                        at + Vector2.down * 20f,
                        new Burst
                        {
                            Shape = BattleVfxShape.Spark,
                            Bright = ErodeBright,
                            From = new Color(Venom.r, Venom.g, Venom.b, 0.9f),
                            To = new Color(0.4f, 0.2f, 0.5f, 0f),
                            Speed = new Vector2(10f, 40f),
                            Direction = -90f,
                            Spread = 40f,
                            Life = new Vector2(0.5f, 0.8f),
                            Size = new Vector2(6f, 10f),
                            Aspect = 1.6f,
                            Gravity = 500f,
                            Drag = 3f,
                            Area = 50f,
                            Delay = new Vector2(0f, 0.3f),
                        },
                        7
                    );
                    Illuminate(at, Venom, 0.5f, 220f, 0.15f, 0.4f, spill: false, flicker: 0.6f);
                    break;
                case CardStatus.Bleed:
                    Spray(
                        frontSmoke,
                        at,
                        new Burst
                        {
                            Shape = BattleVfxShape.Chip,
                            From = new Color(0.5f, 0.04f, 0.05f),
                            To = new Color(0.3f, 0.02f, 0.03f, 0f),
                            Speed = new Vector2(120f, 320f),
                            Direction = 90f,
                            Spread = 120f,
                            Life = new Vector2(0.4f, 0.7f),
                            Size = new Vector2(5f, 10f),
                            Gravity = 1300f,
                            Drag = 0.5f,
                            Spin = 300f,
                            Area = 24f,
                        },
                        10
                    );
                    Drops(at, Blood, 4);
                    Star(at, Blood, 110f, 0.12f);
                    break;
                case CardStatus.Regen:
                    var heal = ColorOf(BattleSkillVfxKind.Heal);
                    Pool(feet, heal, 240f, 0.3f, 0.5f);
                    SoftHalo(at, heal, 260f, 0f, 0.8f);
                    Spray(
                        frontLight,
                        feet + Vector2.up * 60f,
                        new Burst
                        {
                            Shape = BattleVfxShape.Spark,
                            Bright = CoreBright,
                            From = Color.Lerp(Color.white, heal, 0.4f),
                            To = new Color(heal.r, heal.g, heal.b, 0f),
                            Speed = new Vector2(30f, 80f),
                            Direction = 90f,
                            Spread = 30f,
                            Life = new Vector2(0.6f, 1f),
                            Size = new Vector2(5f, 9f),
                            Gravity = -60f,
                            Drag = 1f,
                            Area = 60f,
                            Delay = new Vector2(0f, 0.3f),
                        },
                        8
                    );
                    break;
            }
            yield return Wait(0.12f);
            land?.Invoke();
            Shake(status == CardStatus.Regen ? 0f : 0.1f, Vector2.zero);
            yield return Wait(0.3f);
        }

        /// <summary>
        /// The blast sigil on <paramref name="marked"/> blowing up: motes are sucked into it as it
        /// swells and spins up, its light flickering harder on everyone near; it burns white for an
        /// instant, then every enemy is caught in an explosion. <paramref name="land"/> is called
        /// with each enemy's index.
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
                        s.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, -k * k * 700f);
                        Show(s, Color.Lerp(color, Color.white, k), 1f, Eaten(k, 0.7f), CoreBright);
                    }
                );
                Gather(at, color, 14, 260f, 0.36f);
                Illuminate(at, color, 0.8f, 400f, 0.3f, 0.2f, flicker: 0.5f);
                yield return Wait(0.36f);
                Glow(frontLight, at, Color.white, 260f, 0.12f, 1f);
                yield return Wait(0.02f);
            }
            ScreenFlash(new Color(1f, 0.8f, 0.5f), 0.25f);
            for (int i = 0; i < enemies.Count; i++)
            {
                var weight = land(i);
                Explosion(enemies[i], Center(FrontLayer, enemies[i]), weight);
                GroundFire(Feet(FrontLayer, enemies[i]), 240f, 0.5f);
                EmberBurst(Center(FrontLayer, enemies[i]), 18, 1f);
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
        /// The thundercloud striking <paramref name="target"/>, as real lightning strikes
        /// (doc/catalog/skills/skill-thundercloud.md, its timing): a cloud gathers over it and
        /// flickers; a faint stepped leader feels its way down from the base; the channel that
        /// reaches the target burns white with a flash of the screen, then lights again twice on
        /// the same path, the cloud lighting with it; dust runs out along the floor, sparks fly, a
        /// scorch smokes, the thunder cracks and rolls, and the channel cools and breaks up.
        /// </summary>
        public IEnumerator CloudStrike(RectTransform target, Func<BattleHitWeight> land)
        {
            if (FrontLayer == null || target == null)
            {
                land?.Invoke();
                yield break;
            }
            DimTo(SpellDim, 0.25f);
            var feet = Feet(FrontLayer, target);
            var at = Center(FrontLayer, target);
            // High in the sky over it, so the bolt comes down the whole height of the stage.
            float baseY = FrontLayer.rect.yMax - 230f;
            var storm = StormBank(
                new Vector2(at.x + 40f, baseY),
                720f,
                4,
                3,
                420f,
                0.3f,
                0.03f,
                1.6f,
                0.62f
            );
            yield return Wait(0.2f);
            StartCoroutine(
                StormFlash(
                    storm,
                    new Vector2(at.x + 60f, baseY + 90f),
                    380f,
                    CloudBlinks,
                    0.5f,
                    0.8f
                )
            );
            yield return Wait(0.15f);
            StormBolt(new Vector2(at.x, baseY + 40f), feet + Vector2.up * 6f);
            yield return Wait(StormLeader);
            var weight = land?.Invoke() ?? BattleHitWeight.Normal;
            var color = ColorOf("Thundercloud");
            GroundArcs(feet, color, 6, 240f);
            Punch(at, 0.02f);
            ScreenFlash(StormFlashLight, MaxFlash);
            // The world lights up for a moment: the dark of the spell lifts with the flash (one
            // flash with it, not one per stroke) and comes back.
            DimTo(0f, 0f);
            StartCoroutine(After(0.08f, () => DimTo(SpellDim, 0.4f)));
            HitStop(Weighted(0.06f, weight));
            Shake(Weighted(0.32f, weight), Vector2.down);
            // The strokes light the cloud they come from, and the target and the floor from above.
            StartCoroutine(
                StormFlash(storm, new Vector2(at.x, baseY + 60f), 520f, StrokeBlinks, 1.6f, 0f)
            );
            StartCoroutine(StrokeLights(at + Vector2.up * 160f, feet));
            // A small hot flare where it meets the target; the floor's light is the pool above.
            Glow(frontLight, feet + Vector2.up * 10f, Color.white, 190f, 0.16f, 0.9f);
            DustKick(feet, Vector2.left, 320f);
            DustKick(feet, Vector2.right, 320f);
            Sparks(
                feet + Vector2.up * 10f,
                color,
                Mathf.RoundToInt(Weighted(14f, weight)),
                0.9f,
                Vector2.up
            );
            FloorMark(
                BattleVfxShape.Scorch,
                feet,
                new Vector2(300f, 90f),
                new Color(0.1f, 0.08f, 0.1f),
                0.75f,
                1.6f,
                flat: false
            );
            Spray(
                backSmoke,
                feet,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    Bright = 0.4f,
                    From = new Color(0.32f, 0.3f, 0.33f, 0.55f),
                    To = new Color(0.32f, 0.3f, 0.33f, 0.55f),
                    Speed = new Vector2(25f, 60f),
                    Direction = 90f,
                    Spread = 25f,
                    Life = new Vector2(0.7f, 1f),
                    Size = new Vector2(70f, 110f),
                    Grow = 0.4f,
                    Shrink = 1.6f,
                    Drag = 0.6f,
                    Hold = 0.35f,
                    Area = 30f,
                    Delay = new Vector2(0.15f, 0.4f),
                },
                3
            );
            StartCoroutine(Crackle(target, Violet));
            StartCoroutine(Rumble());
            yield return Wait(0.55f);
            DimTo(0f, 0.5f);
            yield return Wait(0.5f);
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
            // A crack is drawn as a real one shows, by its shadow (the rich look), not as light.
            if (shape == BattleVfxShape.Crack)
                shot.Image.material = RichMaterialOf(BattleVfxShape.Crack);
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
        /// A <paramref name="material"/> draws it in place of its shape's own.
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
            bool turn = true,
            Material material = null
        )
        {
            var shot = Spawn(frontLight, shape, from, size, color, head);
            if (material != null)
                shot.Image.material = material;
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
        /// <paramref name="from"/> fast and worn away after <paramref name="hold"/> of its life, as a
        /// shaft of light looks in real air: a smooth column with specks of dust glinting in it.
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
            shot.Image.material = RichMaterialOf(BattleVfxShape.Beam);
            shot.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, Angle(to - from));
            Animate(
                shot,
                seconds,
                (s, k) => Show(s, color, Mathf.Min(1f, k * 1.2f), Eaten(k, hold), CoreBright)
            );
        }

        // The dark slit a blade leaves across the air (the shader's beam part 5).
        private const float BeamSlit = 5f;

        /// <summary>
        /// The cut of a blade too fast to see, from <paramref name="from"/> to
        /// <paramref name="to"/>, as layers on one seed that go at different times
        /// (vfx-authoring, a line of light): the haze of lit air round it, wide and soft, behind
        /// the characters; a dark hairline under the light, so the cut reads on any ground; the
        /// white-hot core, gone at once; and the afterimage, thin strands left in the eye that
        /// break up last. It is drawn in over <paramref name="draw"/> s and stays
        /// <paramref name="stays"/> s. When <paramref name="openAt"/> is not negative the cut
        /// opens that long after it was made: the dark line parts into a thin slit with lips of
        /// light and closes again, and the core and the haze flare a second time.
        /// <paramref name="scale"/> thins or thickens every layer.
        /// </summary>
        private void Cut(
            Vector2 from,
            Vector2 to,
            Color color,
            float scale,
            float draw,
            float openAt,
            float stays
        )
        {
            float length = Vector2.Distance(from, to);
            float angle = Angle(to - from);
            float seed = UnityEngine.Random.value;
            bool opens = openAt >= 0f;
            Shot Layer(RectTransform layer, float look, float wide)
            {
                if (layer == null)
                    return null;
                var shot = Spawn(
                    layer,
                    BattleVfxShape.Beam,
                    from,
                    new Vector2(length, wide * scale),
                    Color.clear,
                    new Vector2(0f, 0.5f)
                );
                shot.Seed = seed;
                shot.Image.material = RichMaterialOf(BattleVfxShape.Beam, look);
                shot.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
                return shot;
            }
            // The beam's shape is drawn in over the first quarter of its progress.
            float Drawn(float t) => t / (4f * Mathf.Max(draw, 0.001f));
            // The second flare, as the cut opens.
            float Flare(float t, float rise, float hold, float fall) =>
                opens ? Envelope(t, openAt, rise, openAt + hold, openAt + fall) : 0f;

            float hazeLife = opens ? openAt + 0.5f : 0.3f;
            var haze = Layer(backLight, ShaftHaze, 150f);
            if (haze != null)
                Animate(
                    haze,
                    hazeLife,
                    (s, k) =>
                    {
                        float t = k * hazeLife;
                        float a =
                            0.5f * Envelope(t, 0f, 0.02f, 0.06f, 0.28f)
                            + 0.75f * Flare(t, 0.02f, 0.08f, 0.5f);
                        Show(
                            s,
                            new Color(color.r, color.g, color.b, Mathf.Min(1f, a)),
                            Drawn(t),
                            0f
                        );
                    }
                );
            var slit = Layer(frontSmoke, BeamSlit, 18f);
            if (slit != null)
                Animate(
                    slit,
                    stays,
                    (s, k) =>
                    {
                        float t = k * stays;
                        var tint = new Color(color.r, color.g, color.b, Mathf.Clamp01(t / draw));
                        Show(s, tint, Flare(t, 0.025f, 0.09f, 0.42f), Eaten(k, 0.6f));
                    }
                );
            var trace = Layer(frontLight, ShaftTrace, 64f);
            if (trace != null)
            {
                // It starts to break up once the cut has opened.
                float whole = opens ? Mathf.Min(0.8f, (openAt + 0.15f) / stays) : 0.3f;
                Animate(
                    trace,
                    stays,
                    (s, k) =>
                        Show(
                            s,
                            new Color(color.r, color.g, color.b, 0.85f),
                            Drawn(k * stays),
                            Eaten(k, whole),
                            1.3f
                        )
                );
            }
            float coreLife = opens ? openAt + 0.24f : 0.16f;
            var core = Layer(frontLight, ShaftCore, 200f);
            if (core != null)
                Animate(
                    core,
                    coreLife,
                    (s, k) =>
                    {
                        float t = k * coreLife;
                        float a =
                            Envelope(t, 0f, 0.008f, 0.05f, 0.16f) + Flare(t, 0.008f, 0.07f, 0.24f);
                        Show(
                            s,
                            new Color(1f, 1f, 1f, Mathf.Min(1f, a)),
                            Drawn(t),
                            0f,
                            CoreBright * 1.4f
                        );
                    }
                );
        }

        /// <summary>What a card gives an ally, by which its landing is marked (<see cref="Blessing"/>).</summary>
        private enum Boon
        {
            /// <summary>Healing: motes of light rising slowly round the body.</summary>
            Mend,

            /// <summary>A guard: a ring snapping out on the floor and light drawn in to the body.</summary>
            Ward,

            /// <summary>Strength, speed or energy: thin lines of light shooting up round the body.</summary>
            Surge,
        }

        /// <summary>
        /// The mark of a card landing on an ally, over the card's own effect, so that what was
        /// given is seen on the one given it: for <see cref="Boon.Mend"/> motes of light rise
        /// slowly round the body, thin lines among them, and a soft light pools at the feet;
        /// for <see cref="Boon.Ward"/> a ring snaps out on the floor and light is drawn in to
        /// the body; for <see cref="Boon.Surge"/> thin lines of light shoot up round the body
        /// with sparks (the rising aura of doc/features/skill-windups.md). Each ends with a
        /// twinkle over the head. Light only, in <paramref name="color"/>, about half a second.
        /// </summary>
        private void Blessing(RectTransform target, Color color, Boon boon)
        {
            if (target == null || frontLight == null)
                return;
            var (low, high) = Bounds(FrontLayer, target);
            var feet = Feet(FrontLayer, target);
            var at = Center(FrontLayer, target);
            float width = Mathf.Max(120f, high.x - low.x);
            var head = new Vector2(at.x, high.y + 30f);
            switch (boon)
            {
                case Boon.Mend:
                    Spray(
                        frontLight,
                        feet + Vector2.up * 20f,
                        new Burst
                        {
                            Shape = BattleVfxShape.Mote,
                            Bright = CoreBright,
                            From = new Color(color.r, color.g, color.b, 0.9f),
                            To = new Color(1f, 1f, 1f, 0f),
                            Speed = new Vector2(60f, 180f),
                            Direction = 90f,
                            Spread = 24f,
                            Life = new Vector2(0.7f, 1.2f),
                            Size = new Vector2(14f, 28f),
                            Gravity = -40f,
                            Drag = 0.8f,
                            Area = width * 0.5f,
                            Delay = new Vector2(0f, 0.35f),
                        },
                        22
                    );
                    RisingLines(feet, color, 14, width, 0.5f);
                    Pool(feet, color, width * 2.4f, 0.15f, 0.6f);
                    break;
                case Boon.Ward:
                    if (backLight != null)
                    {
                        var ring = Spawn(
                            backLight,
                            BattleVfxShape.Ring,
                            feet,
                            Vector2.one * width * 2.6f,
                            color
                        );
                        Animate(
                            ring,
                            0.38f,
                            (s, k) =>
                            {
                                s.Holder.localScale = RingScale(k, true);
                                Show(s, color, k, k, CoreBright);
                            }
                        );
                    }
                    Gather(at, color, 16, 240f, 0.28f);
                    Pool(feet, color, width * 2.2f, 0.1f, 0.5f);
                    break;
                default:
                    RisingLines(feet, color, 26, width * 1.2f, 0.55f);
                    Spray(
                        frontLight,
                        feet + Vector2.up * 30f,
                        new Burst
                        {
                            Shape = BattleVfxShape.Streak,
                            Bright = CoreBright,
                            From = Color.white,
                            To = new Color(color.r, color.g, color.b, 0f),
                            Speed = new Vector2(500f, 1100f),
                            Direction = 90f,
                            Spread = 18f,
                            Life = new Vector2(0.2f, 0.4f),
                            Size = new Vector2(8f, 13f),
                            Stretch = 0.06f,
                            Drag = 2f,
                            Area = width * 0.45f,
                            Delay = new Vector2(0f, 0.2f),
                        },
                        18
                    );
                    Pool(feet, color, width * 2f, 0.08f, 0.4f);
                    break;
            }
            Twinkle(head, Color.Lerp(color, Color.white, 0.5f), 130f, 0.3f, 0.18f, 1f);
            // What is given lights the one given it.
            Illuminate(at, color, 0.6f, width * 2f, 0.1f, 0.4f, spill: false);
        }

        /// <summary>
        /// The landing of a card's hits, marking each that lands on an ally with a
        /// <see cref="Blessing"/> of <paramref name="boon"/> as it lands.
        /// </summary>
        private Func<int, BattleHitWeight> Blessed(
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land,
            Color color,
            Boon boon
        ) =>
            index =>
            {
                if (index >= 0 && index < hits.Count && hits[index].Ally)
                    Blessing(hits[index].Area, color, boon);
                return land(index);
            };

        /// <summary>
        /// The ground round a lightning strike at <paramref name="feet"/>, as the current
        /// leaves it: <paramref name="count"/> short bolts running out along the floor every
        /// way, to about <paramref name="reach"/> px, a spark where each ends, and the floor
        /// left scorched under the strike.
        /// </summary>
        private void GroundArcs(Vector2 feet, Color color, int count, float reach)
        {
            for (int n = 0; n < count; n++)
            {
                float a = (n + UnityEngine.Random.Range(0.2f, 0.8f)) / count * Mathf.PI * 2f;
                var end =
                    feet
                    + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * FloorTilt)
                        * reach
                        * UnityEngine.Random.Range(0.55f, 1f);
                Zap(feet + Vector2.up * 6f, end, color, UnityEngine.Random.Range(0.16f, 0.26f));
                Sparks(end, color, 2, 0.4f, Vector2.up);
            }
            FloorMark(
                BattleVfxShape.Scorch,
                feet,
                Vector2.one * reach * 0.8f,
                new Color(0.06f, 0.045f, 0.04f),
                0.8f,
                1.3f,
                flat: true
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

        // --- Layered building blocks (vfx-authoring: four layers, each its own cover and time) --

        // The strokes of a bolt jumping between two points: one discharge, then the same path lit
        // again a tenth of a second later, weaker.
        private static readonly (float[] times, float[] peaks) JumpStrokes = (
            new[] { 0f, 0.09f },
            new[] { 1f, 0.55f }
        );

        // The strokes of a bolt let fly at one enemy: the discharge and two restrikes.
        private static readonly (float[] times, float[] peaks) BoltStrokes = (
            new[] { 0f, 0.1f, 0.2f },
            new[] { 1f, 0.65f, 0.45f }
        );

        /// <summary>
        /// Lightning from <paramref name="from"/> to <paramref name="to"/> (its foot) as four
        /// layers on one channel (one seed), the layers of <see cref="StormBolt"/> laid along any
        /// line: the <paramref name="halo"/> of lit air, faint while the leader feels its way over
        /// <paramref name="leader"/> s and swelling with each stroke; the afterimage, a thin
        /// <paramref name="trace"/> left in the eye that breaks up last; the channel and its
        /// branches; and the white-hot core, seen only at each of the <paramref name="strokes"/>,
        /// which light the same path again. <paramref name="wander"/> caps how far it swings
        /// sideways (px). The lights it throws come from <see cref="ChannelLights"/>.
        /// </summary>
        private void Channel(
            Vector2 from,
            Vector2 to,
            Color halo,
            Color trace,
            float leader,
            (float[] times, float[] peaks) strokes,
            float wander = 360f
        )
        {
            if (frontLight == null)
                return;
            // The bolt's foot is at 9% of its board's height and its top 4% from the top.
            float height = Mathf.Max(140f, Vector2.Distance(from, to) / 0.87f);
            var size = new Vector2(Mathf.Min(height * 0.62f, wander), height);
            float seed = UnityEngine.Random.value;
            float angle = Angle(from - to) - 90f;
            float last = strokes.times[^1];
            float lead = Mathf.Max(leader, 0.001f);
            Shot Layer(float look, Color color)
            {
                var shot = Spawn(
                    frontLight,
                    BattleVfxShape.Lightning,
                    to,
                    size,
                    color,
                    new Vector2(0.5f, 0.09f)
                );
                shot.Seed = seed;
                shot.Image.material = RichMaterialOf(BattleVfxShape.Lightning, look);
                shot.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
                return shot;
            }
            float Stage(float t) =>
                t < leader
                    ? 0.5f * t / lead
                    : 0.5f + 0.5f * Mathf.Clamp01((t - leader) / (0.45f + last));
            float Strokes(float t) => t < leader ? 0f : BlinkLight(t - leader, strokes);

            float haloLife = leader + last + 0.4f;
            Animate(
                Layer(BoltHalo, halo),
                haloLife,
                (s, k) =>
                {
                    float t = k * haloLife;
                    float light = t < leader ? 0.35f : 0.2f + Strokes(t);
                    Show(
                        s,
                        new Color(halo.r, halo.g, halo.b, Mathf.Clamp01(light)),
                        Stage(t),
                        Eaten(k, 0.6f),
                        1f + Strokes(t)
                    );
                }
            );
            float traceLife = leader + last + 0.7f;
            Animate(
                Layer(BoltTrace, trace),
                traceLife,
                (s, k) =>
                {
                    float t = k * traceLife;
                    // Left in the eye after the last stroke, broken up as it fades.
                    float since = Mathf.Max(0f, t - leader - last);
                    float alpha = t < leader ? 0f : 0.75f * Mathf.Exp(-since / 0.3f);
                    Show(
                        s,
                        new Color(trace.r, trace.g, trace.b, alpha),
                        Stage(t),
                        Eaten(since / (traceLife - leader - last), 0.25f)
                    );
                }
            );
            float channelLife = leader + last + 0.3f;
            Animate(
                Layer(BoltChannel, halo),
                channelLife,
                (s, k) =>
                {
                    float t = k * channelLife;
                    float alpha = t < leader ? 0.8f : Mathf.Clamp01(0.12f + Strokes(t));
                    Show(
                        s,
                        new Color(halo.r, halo.g, halo.b, alpha),
                        Stage(t),
                        Eaten(k, 0.55f),
                        1f + 1.5f * Strokes(t)
                    );
                }
            );
            float coreLife = leader + last + 0.2f;
            Animate(
                Layer(BoltCore, Color.white),
                coreLife,
                (s, k) =>
                {
                    float t = k * coreLife;
                    float light = Strokes(t);
                    Show(
                        s,
                        new Color(1f, 1f, 1f, Mathf.Clamp01(light * 1.5f)),
                        Stage(t),
                        0f,
                        1f + 6f * light
                    );
                }
            );
        }

        /// <summary>
        /// The light of a <see cref="Channel"/>, in real time: at each stroke the characters along
        /// it are lit from the whole path, the one it meets at <paramref name="to"/> whiter and
        /// nearer, and the floor at <paramref name="feet"/> in a pool of light, each as bright as
        /// the stroke times <paramref name="power"/>. Only the first stroke lights the background.
        /// </summary>
        private IEnumerator ChannelLights(
            Vector2 from,
            Vector2 to,
            Vector2 feet,
            Color color,
            float leader,
            (float[] times, float[] peaks) strokes,
            float power
        )
        {
            var middle = Vector2.Lerp(from, to, 0.5f);
            float reach = Vector2.Distance(from, to) * 0.6f + 300f;
            int next = 0;
            for (
                float t = 0f;
                next < strokes.times.Length;
                t += Mathf.Min(Time.unscaledDeltaTime, 0.1f)
            )
            {
                while (next < strokes.times.Length && t >= leader + strokes.times[next])
                {
                    float peak = strokes.peaks[next] * power;
                    Illuminate(middle, color, 1.3f * peak, reach, 0.02f, 0.16f, spill: false);
                    // The first stroke brings the background a little out of the dark, too.
                    if (next == 0)
                        Illuminate(middle, color, 0.4f * peak, reach * 0.6f, 0.02f, 0.2f);
                    Illuminate(to, Color.white, 1.1f * peak, 300f, 0.02f, 0.12f, spill: false);
                    FlashPool(feet, color, 520f * Mathf.Sqrt(power), 0.6f * peak);
                    next++;
                }
                yield return null;
            }
        }

        /// <summary>A pool of light laid on the floor at <paramref name="feet"/> by a flash, <paramref name="peak"/> bright, gone in a fifth of a second.</summary>
        private void FlashPool(Vector2 feet, Color color, float size, float peak)
        {
            if (backLight == null)
                return;
            var pool = Spawn(backLight, BattleVfxShape.Glow, feet, new Vector2(size, size), color);
            pool.Holder.localScale = new Vector3(1f, FloorTilt, 1f);
            Animate(
                pool,
                0.24f,
                (s, k) =>
                {
                    float t = k * 0.24f;
                    float alpha = peak * (t < 0.03f ? 1f : 1f - EaseIn((t - 0.03f) / 0.21f));
                    Show(s, new Color(color.r, color.g, color.b, alpha), k, 0f);
                }
            );
        }

        // The shader's _Rich for each layer of a shaft of light (drawn on a beam).
        private const float ShaftBody = 1f;
        private const float ShaftHaze = 2f;
        private const float ShaftCore = 3f;
        private const float ShaftTrace = 4f;

        /// <summary>
        /// A shaft of light from <paramref name="from"/> to <paramref name="to"/>,
        /// <paramref name="width"/> px wide, drawn in over <paramref name="draw"/> s and peaking
        /// as it arrives, as four layers on one seed, each its own width and time (vfx-authoring,
        /// a line of light): behind the characters the haze (twice as wide, faint, the longest)
        /// and the body (the shaft seen in the dust); in front of them the white core (a third as
        /// wide, only at the peak) and the afterimage (a thin trace of the colour left once the
        /// core is gone, breaking up last). So it passes through what it strikes, and its colour
        /// lives in the haze and the trace while only its core burns white.
        /// <paramref name="seconds"/> is how long the body lasts; a soft light with no hot core
        /// passes a <paramref name="core"/> of 0.
        /// </summary>
        private void Shaft(
            Vector2 from,
            Vector2 to,
            Color color,
            float width,
            float draw,
            float seconds,
            float core = 1f
        )
        {
            float length = Vector2.Distance(from, to);
            float angle = Angle(to - from);
            float seed = UnityEngine.Random.value;
            float lead = Mathf.Max(draw, 0.001f);
            Shot Layer(RectTransform layer, float look, float wide, Color tint)
            {
                if (layer == null)
                    return null;
                var shot = Spawn(
                    layer,
                    BattleVfxShape.Beam,
                    from,
                    new Vector2(length, wide),
                    tint,
                    new Vector2(0f, 0.5f)
                );
                shot.Seed = seed;
                shot.Image.material = RichMaterialOf(BattleVfxShape.Beam, look);
                shot.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
                return shot;
            }
            // The beam's shape is drawn in over the first quarter of its progress.
            float Drawn(float t) => t < draw ? 0.25f * t / lead : 0.25f + 0.4f * (t - draw);
            void Run(
                Shot shot,
                float life,
                Func<float, Color> tint,
                Func<float, float> eaten,
                float bright
            )
            {
                if (shot == null)
                    return;
                Animate(
                    shot,
                    life,
                    (s, k) =>
                    {
                        float t = k * life;
                        Show(s, tint(t), Drawn(t), eaten(t), bright);
                    }
                );
            }
            float hazeLife = draw + seconds * 1.4f;
            Run(
                Layer(backLight, ShaftHaze, width * 2.2f, color),
                hazeLife,
                t => new Color(
                    color.r,
                    color.g,
                    color.b,
                    0.55f * Mathf.Min(1f, t / (draw + 0.06f))
                ),
                t => Eaten(t / hazeLife, 0.45f),
                1f
            );
            // A soft light (no core) has no hot shaft either: only its haze and trace.
            float bodyLife = draw + seconds;
            Run(
                core > 0f ? Layer(backLight, ShaftBody, width, color) : null,
                bodyLife,
                t => color,
                t => Eaten(t / bodyLife, 0.4f),
                CoreBright
            );
            float coreLife = draw + 0.14f;
            Run(
                core > 0f ? Layer(frontLight, ShaftCore, width * 0.34f, Color.white) : null,
                coreLife,
                t => new Color(
                    1f,
                    1f,
                    1f,
                    core * (t < draw ? 0.6f : Mathf.Exp(-(t - draw) / 0.05f))
                ),
                t => 0f,
                2.4f
            );
            float traceLife = draw + seconds * 1.6f;
            Run(
                Layer(frontLight, ShaftTrace, width * 0.3f, color),
                traceLife,
                t => new Color(
                    color.r,
                    color.g,
                    color.b,
                    t < draw ? 0f : 0.8f * Mathf.Exp(-(t - draw) / (seconds * 0.6f))
                ),
                t => Eaten(Mathf.Max(0f, t - draw) / (traceLife - draw), 0.2f),
                1.4f
            );
        }

        /// <summary>
        /// A heavy gas (poison, shadow) let out at <paramref name="at"/> that sinks to the floor at
        /// <paramref name="feet"/> and creeps out along it, <paramref name="width"/> px across,
        /// for about <paramref name="seconds"/>, in four layers from the back: the dark body, broad
        /// sheets lying on the floor and creeping slowly out, covering about half; billows rolling
        /// down off the target, heavier than air, sinking as they swell; thin tatters running fast
        /// along the floor, mostly holes; and the <paramref name="light"/> in the gas, dim spots
        /// glowing here and there (none when its alpha is 0). The gas is drawn darker by
        /// <paramref name="dark"/> (0 to 1).
        /// </summary>
        private void Gas(
            Vector2 at,
            Vector2 feet,
            Color body,
            Color light,
            float width,
            float seconds,
            float dark
        )
        {
            for (int side = 0; side < 2; side++)
                Spray(
                    backSmoke,
                    feet + Vector2.up * 6f,
                    new Burst
                    {
                        Shape = BattleVfxShape.Smoke,
                        Bright = Mathf.Lerp(0.55f, 0.22f, dark),
                        From = new Color(body.r, body.g, body.b, 0.9f),
                        To = new Color(body.r, body.g, body.b, 0.9f),
                        Speed = new Vector2(30f, 110f),
                        Direction = side == 0 ? 0f : 180f,
                        Spread = 30f,
                        Life = new Vector2(seconds * 0.8f, seconds),
                        Size = new Vector2(0.8f, 1.1f) * width,
                        Aspect = 0.32f,
                        Grow = 0.4f,
                        Shrink = 1.5f,
                        Drag = 1.6f,
                        Level = true,
                        Hold = 0.45f,
                        Area = 30f,
                    },
                    3
                );
            Spray(
                frontSmoke,
                Vector2.Lerp(at, feet, 0.5f),
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    Bright = Mathf.Lerp(0.7f, 0.35f, dark),
                    From = new Color(body.r, body.g, body.b, 0.7f),
                    To = new Color(body.r, body.g, body.b, 0.7f),
                    Speed = new Vector2(40f, 120f),
                    Direction = -90f,
                    Spread = 200f,
                    Life = new Vector2(seconds * 0.5f, seconds * 0.8f),
                    Size = new Vector2(0.4f, 0.6f) * width,
                    Aspect = 0.7f,
                    Grow = 0.5f,
                    Shrink = 1.4f,
                    Gravity = 70f,
                    Drag = 3f,
                    Spin = 20f,
                    Hold = 0.4f,
                    Area = 40f,
                    Delay = new Vector2(0f, 0.25f),
                },
                6
            );
            for (int side = 0; side < 2; side++)
                Spray(
                    frontSmoke,
                    feet,
                    new Burst
                    {
                        Shape = BattleVfxShape.Smoke,
                        Bright = Mathf.Lerp(0.85f, 0.45f, dark),
                        From = new Color(body.r, body.g, body.b, 0.5f),
                        To = new Color(body.r, body.g, body.b, 0.5f),
                        Speed = new Vector2(160f, 340f),
                        Direction = side == 0 ? 0f : 180f,
                        Spread = 16f,
                        Life = new Vector2(0.5f, 0.8f),
                        Size = new Vector2(0.35f, 0.5f) * width,
                        Aspect = 0.22f,
                        Shrink = 1.6f,
                        Drag = 3f,
                        Level = true,
                        Hold = 0.2f,
                        Area = 24f,
                        Delay = new Vector2(0.05f, 0.35f),
                    },
                    3
                );
            if (light.a <= 0f)
                return;
            Spray(
                backLight,
                feet + Vector2.up * 24f,
                new Burst
                {
                    Shape = BattleVfxShape.Glow,
                    From = new Color(light.r, light.g, light.b, light.a),
                    To = new Color(light.r, light.g, light.b, 0f),
                    Speed = new Vector2(10f, 50f),
                    Direction = 0f,
                    Spread = 360f,
                    Life = new Vector2(0.35f, 0.8f),
                    Size = new Vector2(0.2f, 0.4f) * width,
                    Aspect = 0.55f,
                    Area = width * 0.3f,
                    Delay = new Vector2(0f, seconds * 0.5f),
                },
                7
            );
        }

        /// <summary>
        /// Thin lines of light, 4 to 6 px, born round <paramref name="feet"/> one after another
        /// over <paramref name="seconds"/>, rising faster as they go and each gone within a third
        /// of a second (an aura, a heal rising): half behind the target and half in front, so they
        /// rise round it.
        /// </summary>
        private void RisingLines(Vector2 feet, Color color, int count, float width, float seconds)
        {
            for (int side = 0; side < 2; side++)
                Spray(
                    side == 0 ? backLight : frontLight,
                    feet + Vector2.up * 60f,
                    new Burst
                    {
                        Shape = BattleVfxShape.Streak,
                        Bright = CoreBright,
                        From = Color.Lerp(Color.white, color, 0.35f),
                        To = new Color(color.r, color.g, color.b, 0f),
                        Speed = new Vector2(60f, 180f),
                        Direction = 90f,
                        Spread = 4f,
                        Life = new Vector2(0.22f, 0.36f),
                        Size = new Vector2(4f, 6f),
                        Stretch = 0.22f,
                        Gravity = -1500f,
                        Area = width * 0.5f,
                        Delay = new Vector2(0f, seconds),
                    },
                    (count + 1 - side) / 2
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

        // --- The thundercloud ------------------------------------------------------------------

        // A thundercloud's own colour, a dark slate; its billows are drawn darker below it and
        // paler where the sky lights their tops.
        private static readonly Color StormCloud = new(0.2f, 0.21f, 0.29f);

        // The light of lightning: white with a little violet.
        private static readonly Color StormFlashLight = new(0.82f, 0.8f, 1f);

        // A lightning channel's own colour round its white core: a pale violet.
        private static readonly Color BoltHaloColor = new(0.8f, 0.72f, 1f);

        // The scud under a thundercloud: a paler grey than the cloud, catching more light.
        private static readonly Color StormScudColor = new(0.42f, 0.42f, 0.5f);

        // How long the stepped leader takes to come down to the target (s).
        private const float StormLeader = 0.12f;

        // The shader's _Rich for each layer of a thundercloud (drawn on smoke), from the back.
        private const float StormMass = 3f;
        private const float StormBillows = 2f;
        private const float StormScud = 4f;
        private const float StormGlow = 5f;

        // The shader's _Rich for each layer of a lightning flash (drawn on lightning).
        private const float BoltCore = 1f;
        private const float BoltChannel = 2f;
        private const float BoltHalo = 3f;
        private const float BoltTrace = 4f;

        // The strokes down one channel (after the leader, s) and how bright each is: the return
        // stroke, then the strokes that light the same path again. Real ones come 20 to 50 ms
        // apart; these are drawn out to be seen.
        private static readonly float[] StrokeTimes = { 0f, 0.1f, 0.2f };
        private static readonly float[] StrokePeaks = { 1f, 0.7f, 0.5f };

        // The blinks of lightning flickering inside a cloud (s, and how bright each is).
        private static readonly float[] CloudBlinkTimes = { 0f, 0.05f, 0.12f };
        private static readonly float[] CloudBlinkPeaks = { 1f, 0.45f, 0.8f };
        private static readonly (float[] times, float[] peaks) CloudBlinks = (
            CloudBlinkTimes,
            CloudBlinkPeaks
        );
        private static readonly (float[] times, float[] peaks) StrokeBlinks = (
            StrokeTimes,
            StrokePeaks
        );

        /// <summary>A bank of thundercloud billows, and the lightning lighting them from within.</summary>
        private sealed class Storm
        {
            public float Left;
            public float Right;
            public float Base;

            /// <summary>The flashes inside it now: where, how far their light reaches, how bright.</summary>
            public readonly List<(Vector2 at, float radius, float level)> Flashes = new();

            /// <summary>How much lightning lights the billow at <paramref name="at"/> now.</summary>
            public float LightAt(Vector2 at)
            {
                float light = 0f;
                foreach (var flash in Flashes)
                {
                    float near = 1f - Mathf.Clamp01(Vector2.Distance(at, flash.at) / flash.radius);
                    light += flash.level * near * near;
                }
                return light;
            }
        }

        /// <summary>
        /// A bank of thundercloud <paramref name="width"/> wide over <paramref name="center"/> (the
        /// height of its flat base), in layers from the back, each its own shape, cover and speed:
        /// a few broad, dark masses; the billows, a lower row and behind and above it a row of
        /// towering ones, lit on their tops; and torn scud under the base, blown along faster. (The
        /// fourth layer, the light of the lightning inside, comes with each flash:
        /// <see cref="StormFlash"/>.) They roll in from <paramref name="roll"/> px to the right,
        /// the leading ones first, slowing and swelling over <paramref name="rollTime"/>, then
        /// churn and drift on, and are worn away after <paramref name="hold"/> of
        /// <paramref name="seconds"/>.
        /// </summary>
        private Storm StormBank(
            Vector2 center,
            float width,
            int lower,
            int upper,
            float roll,
            float rollTime,
            float stagger,
            float seconds,
            float hold
        )
        {
            var storm = new Storm
            {
                Left = center.x - width * 0.5f,
                Right = center.x + width * 0.5f,
                Base = center.y,
            };
            float Across(int i, int count) =>
                Mathf.Lerp(
                    storm.Left + width * 0.1f,
                    storm.Right - width * 0.1f,
                    count == 1 ? 0.5f : i / (count - 1f)
                )
                + UnityEngine.Random.Range(-0.05f, 0.05f) * width;

            // The dark masses behind, broad boards whose base is at 32% of the height.
            var massSize = new Vector2(width * 0.6f, width * 0.3f);
            for (int i = 0; i < 3; i++)
            {
                var size = massSize * UnityEngine.Random.Range(0.9f, 1.15f);
                var rest = new Vector2(Across(i, 3), center.y + size.y * 0.16f);
                StormLayer(
                    storm,
                    StormMass,
                    rest,
                    size,
                    roll,
                    rollTime,
                    stagger * i,
                    seconds,
                    hold,
                    UnityEngine.Random.Range(8f, 18f),
                    StormCloud,
                    1f
                );
            }
            // The billows on square boards, so the round bulges stay round; the towering row
            // first, its base just above the lower row's so the lower billows hide it. On the
            // board the base is at 29% of the height.
            float lowWidth = width / lower * 1.9f;
            float highWidth = width / upper * 1.7f;
            for (int row = 1; row >= 0; row--)
            {
                int count = row == 1 ? upper : lower;
                for (int i = 0; i < count; i++)
                {
                    float side =
                        (row == 1 ? highWidth : lowWidth) * UnityEngine.Random.Range(0.85f, 1.15f);
                    var size = new Vector2(side, side);
                    float y = center.y + (row == 1 ? lowWidth * 0.06f : 0f) + size.y * 0.21f;
                    var rest = new Vector2(
                        Across(i, count),
                        y + UnityEngine.Random.Range(-14f, 14f)
                    );
                    StormLayer(
                        storm,
                        StormBillows,
                        rest,
                        size,
                        roll,
                        rollTime,
                        stagger * (i + row * 0.5f),
                        seconds,
                        hold,
                        UnityEngine.Random.Range(14f, 30f),
                        StormCloud,
                        1.5f
                    );
                }
            }
            // The scud in front, under the base: thin shreds blown along faster.
            for (int i = 0; i < 7; i++)
            {
                var size =
                    new Vector2(width * 0.22f, width * 0.08f)
                    * UnityEngine.Random.Range(0.8f, 1.3f);
                var rest = new Vector2(
                    UnityEngine.Random.Range(storm.Left, storm.Right),
                    center.y - UnityEngine.Random.Range(5f, 70f)
                );
                StormLayer(
                    storm,
                    StormScud,
                    rest,
                    size,
                    roll * 1.3f,
                    rollTime,
                    stagger * i,
                    seconds,
                    hold * 0.9f,
                    UnityEngine.Random.Range(60f, 120f),
                    StormScudColor,
                    2f
                );
            }
            return storm;
        }

        /// <summary>
        /// One board of a layer of <paramref name="storm"/>, drawn in its <paramref name="look"/>:
        /// it rolls in from <paramref name="roll"/> px to the right of <paramref name="rest"/>
        /// after <paramref name="delay"/>, drifts left at <paramref name="drift"/> px/s, churns, is
        /// lit by the lightning inside (by <paramref name="flashGain"/>) and is worn away after
        /// <paramref name="hold"/> of <paramref name="seconds"/>.
        /// </summary>
        private void StormLayer(
            Storm storm,
            float look,
            Vector2 rest,
            Vector2 size,
            float roll,
            float rollTime,
            float delay,
            float seconds,
            float hold,
            float drift,
            Color color,
            float flashGain
        )
        {
            var from = rest + new Vector2(roll, UnityEngine.Random.Range(-20f, 30f));
            var board = Spawn(backSmoke, BattleVfxShape.Smoke, from, size, Color.clear);
            board.Image.material = RichMaterialOf(BattleVfxShape.Smoke, look);
            Animate(
                board,
                seconds,
                (s, k) =>
                {
                    float t = k * seconds;
                    float arrive = EaseOut(Mathf.Clamp01((t - delay) / rollTime));
                    var at = Vector2.LerpUnclamped(from, rest, arrive) + Vector2.left * (drift * t);
                    s.Holder.anchoredPosition = at;
                    float grow = Mathf.Lerp(0.55f, 1f, arrive);
                    s.Holder.localScale = new Vector3(grow, grow, 1f);
                    float shown = t < delay ? 0f : Mathf.Clamp01((t - delay) / 0.15f);
                    var tint = new Color(color.r, color.g, color.b, shown);
                    // Its gas churns as it goes; the lightning inside lights it.
                    Show(s, tint, t * 0.8f, Eaten(k, hold), 1f + flashGain * storm.LightAt(at));
                }
            );
        }

        /// <summary>
        /// Lightning flashing inside <paramref name="storm"/> at <paramref name="spot"/>, the
        /// storm's fourth layer: a glow seen through the gas, and the layers within
        /// <paramref name="radius"/> lighting up from within at each blink and dying away fast;
        /// the characters below are lit from above (by <paramref name="light"/> of each blink,
        /// none when 0). In real time, so it goes on through a hit stop.
        /// </summary>
        private IEnumerator StormFlash(
            Storm storm,
            Vector2 spot,
            float radius,
            (float[] times, float[] peaks) blinks,
            float strength,
            float light
        )
        {
            int slot = storm.Flashes.Count;
            storm.Flashes.Add((spot, radius, 0f));
            int next = 0;
            float end = blinks.times[^1] + 0.2f;
            // The light itself, seen through the gas, over the cloud's layers.
            var glow = Spawn(
                backLight,
                BattleVfxShape.Smoke,
                spot,
                Vector2.one * radius * 1.1f,
                StormFlashLight
            );
            glow.Image.material = RichMaterialOf(BattleVfxShape.Smoke, StormGlow);
            Animate(
                glow,
                end,
                (s, k) =>
                    Show(
                        s,
                        StormFlashLight,
                        k,
                        0f,
                        0.001f + 0.8f * strength * BlinkLight(k * end, blinks)
                    )
            );
            for (float t = 0f; t < end; t += Mathf.Min(Time.unscaledDeltaTime, 0.1f))
            {
                while (next < blinks.times.Length && t >= blinks.times[next])
                {
                    if (light > 0f)
                        Illuminate(
                            spot + Vector2.down * 80f,
                            StormFlashLight,
                            light * blinks.peaks[next],
                            radius + 160f,
                            0.02f,
                            0.12f,
                            spill: false
                        );
                    next++;
                }
                storm.Flashes[slot] = (spot, radius, strength * BlinkLight(t, blinks));
                yield return null;
            }
            storm.Flashes[slot] = (spot, radius, 0f);
        }

        /// <summary>How bright a run of <paramref name="blinks"/> is <paramref name="t"/> s after it began: each flares and dies away in tens of milliseconds.</summary>
        private static float BlinkLight(float t, (float[] times, float[] peaks) blinks)
        {
            float light = 0f;
            for (int i = 0; i < blinks.times.Length; i++)
                if (t >= blinks.times[i])
                    light += blinks.peaks[i] * Mathf.Exp(-(t - blinks.times[i]) / 0.035f);
            return light;
        }

        /// <summary>
        /// Lightning from a cloud's base at <paramref name="top"/> down to <paramref name="feet"/>,
        /// as four layers on one channel (one seed), each with its own cover and timing: the halo
        /// of lit air, faint for the leader and pulsing with the strokes; the afterimage, a thin
        /// violet trace left once it has struck that fades and breaks up slowest; the channel and
        /// its branches, the faint stepped leader for <see cref="StormLeader"/> and then lit with
        /// each stroke (<see cref="StrokeTimes"/>); and the core, a white-hot line seen only at the
        /// strokes.
        /// </summary>
        private void StormBolt(Vector2 top, Vector2 feet)
        {
            // The bolt's foot is at 9% of its board's height and its top 4% from the top.
            float height = Mathf.Max(300f, (top.y - feet.y) / 0.87f);
            float seed = UnityEngine.Random.value;
            Shot Layer(float look, Color color)
            {
                var shot = Spawn(
                    frontLight,
                    BattleVfxShape.Lightning,
                    feet,
                    new Vector2(height * 0.62f, height),
                    color,
                    new Vector2(0.5f, 0.09f)
                );
                shot.Seed = seed;
                shot.Image.material = RichMaterialOf(BattleVfxShape.Lightning, look);
                shot.Image.rectTransform.localEulerAngles = new Vector3(
                    0f,
                    0f,
                    Angle(top - feet) - 90f
                );
                return shot;
            }
            // The stage the layers draw (0 to 0.5 the leader coming down, then struck and ageing),
            // and the light of the strokes, at t s.
            const float struckFor = 0.5f;
            float Stage(float t) =>
                t < StormLeader
                    ? 0.5f * t / StormLeader
                    : 0.5f + 0.5f * Mathf.Clamp01((t - StormLeader) / struckFor);
            float Strokes(float t) =>
                t < StormLeader ? 0f : BlinkLight(t - StormLeader, StrokeBlinks);

            const float haloLife = StormLeader + 0.45f;
            Animate(
                Layer(BoltHalo, BoltHaloColor),
                haloLife,
                (s, k) =>
                {
                    float t = k * haloLife;
                    // Violet while the leader comes down, the paler lit air at the strokes.
                    float light = t < StormLeader ? 0.5f : 0.25f + Strokes(t);
                    var color = t < StormLeader ? Violet : BoltHaloColor;
                    Show(
                        s,
                        new Color(color.r, color.g, color.b, Mathf.Clamp01(light)),
                        Stage(t),
                        Eaten(k, 0.6f),
                        1f + Strokes(t)
                    );
                }
            );
            const float traceLife = StormLeader + 0.75f;
            Animate(
                Layer(BoltTrace, Violet),
                traceLife,
                (s, k) =>
                {
                    float t = k * traceLife;
                    float since = Mathf.Max(0f, t - StormLeader);
                    float alpha = t < StormLeader ? 0f : 0.7f * Mathf.Exp(-since / 0.35f);
                    Show(
                        s,
                        new Color(Violet.r, Violet.g, Violet.b, alpha),
                        Stage(t),
                        Eaten(since / (traceLife - StormLeader), 0.3f)
                    );
                }
            );
            const float channelLife = StormLeader + 0.4f;
            Animate(
                Layer(BoltChannel, BoltHaloColor),
                channelLife,
                (s, k) =>
                {
                    float t = k * channelLife;
                    float alpha = t < StormLeader ? 0.8f : Mathf.Clamp01(0.15f + Strokes(t));
                    var tint = new Color(BoltHaloColor.r, BoltHaloColor.g, BoltHaloColor.b, alpha);
                    Show(s, tint, Stage(t), Eaten(k, 0.55f), 1f + 1.5f * Strokes(t));
                }
            );
            const float coreLife = StormLeader + 0.3f;
            Animate(
                Layer(BoltCore, Color.white),
                coreLife,
                (s, k) =>
                {
                    float t = k * coreLife;
                    float light = Strokes(t);
                    Show(
                        s,
                        new Color(1f, 1f, 1f, Mathf.Clamp01(light * 1.5f)),
                        Stage(t),
                        0f,
                        1f + 6f * light
                    );
                }
            );
        }

        /// <summary>
        /// Each stroke lights the field, in real time: the characters from the channel
        /// (<paramref name="above"/> the target) far across the field and from where it meets the
        /// ground at <paramref name="feet"/>, hotter and nearer, and the floor round the feet in a
        /// wide pool of light, each as bright as its stroke.
        /// </summary>
        private IEnumerator StrokeLights(Vector2 above, Vector2 feet)
        {
            float last = 0f;
            for (int i = 0; i < StrokeTimes.Length; i++)
            {
                for (
                    float t = last;
                    t < StrokeTimes[i];
                    t += Mathf.Min(Time.unscaledDeltaTime, 0.1f)
                )
                    yield return null;
                last = StrokeTimes[i];
                float peak = StrokePeaks[i];
                Illuminate(above, StormFlashLight, 2.2f * peak, 1100f, 0.03f, 0.18f, spill: false);
                Illuminate(feet, Color.white, 1.4f * peak, 520f, 0.02f, 0.14f, spill: false);
                StrokeFloor(feet, peak);
            }
        }

        /// <summary>The floor lit round <paramref name="feet"/> by one stroke of <paramref name="peak"/> brightness: a wide pool laid on the floor, gone in a fifth of a second.</summary>
        private void StrokeFloor(Vector2 feet, float peak)
        {
            if (backLight == null)
                return;
            var pool = Spawn(
                backLight,
                BattleVfxShape.Glow,
                feet,
                new Vector2(1000f, 1000f),
                StormFlashLight
            );
            pool.Holder.localScale = new Vector3(1f, FloorTilt, 1f);
            const float hold = 0.03f;
            const float fade = 0.2f;
            Animate(
                pool,
                hold + fade,
                (s, k) =>
                {
                    float t = k * (hold + fade);
                    float alpha = 0.8f * peak * (t < hold ? 1f : 1f - EaseIn((t - hold) / fade));
                    Show(
                        s,
                        new Color(StormFlashLight.r, StormFlashLight.g, StormFlashLight.b, alpha),
                        k,
                        0f
                    );
                }
            );
        }

        /// <summary>Thunder rolling on after a strike: small knocks, each weaker, for about half a second.</summary>
        private IEnumerator Rumble()
        {
            yield return Wait(0.08f);
            foreach (float knock in new[] { 0.1f, 0.08f, 0.06f, 0.05f })
            {
                Shake(knock, Vector2.zero);
                yield return Wait(0.12f);
            }
        }
    }
}
