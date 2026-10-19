using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The unique skills' effects (doc/features/skill-evolution.md): each tree has an effect of
    /// its own, each branch turns it its own way (an element, a target, a shape), and each step
    /// of evolution plays it bigger. The first stage is the tree's effect alone. A first step
    /// adds a charged wind-up and, at every blow, a shock ring, a star, sparks and a light on
    /// those near. A last step always cuts in, gathers light into the user in slow motion, and
    /// lands with a white flash, a hit stop, a slow-down and a punch-in, then a second burst a
    /// moment later and motes of light drifting up as it ebbs: the bigger the step, the more it
    /// pays off.
    /// </summary>
    public sealed partial class BattleSkillVfx
    {
        private static readonly string[] UniqueTrees =
        {
            "ManaBurst",
            "StarFall",
            "ThunderArrow",
            "ArrowStorm",
            "ShieldStance",
            "Issen",
            "HealingWind",
            "Revive",
        };

        private static readonly Color Arcane = new(0.74f, 0.46f, 1f);
        private static readonly Color Starlight = new(0.7f, 0.86f, 1f);
        private static readonly Color Bolt8 = new(1f, 0.88f, 0.32f);
        private static readonly Color Steel = new(0.72f, 0.84f, 1f);
        private static readonly Color Crimson = new(1f, 0.3f, 0.36f);
        private static readonly Color Leafy = new(0.5f, 1f, 0.5f);
        private static readonly Color Blossom = new(1f, 0.66f, 0.82f);
        private static readonly Color Dawn = new(1f, 0.9f, 0.62f);

        /// <summary>True when <paramref name="id"/> is a unique skill's stage ("ManaBurst", "StarFall.4").</summary>
        public static bool IsUnique(string id) =>
            !string.IsNullOrEmpty(id) && Array.IndexOf(UniqueTrees, TreeOf(id)) >= 0;

        private static string TreeOf(string id)
        {
            int dot = id.IndexOf('.');
            return dot < 0 ? id : id.Substring(0, dot);
        }

        /// <summary>The stage of a unique skill's id: 0 for the first, 1 to 6 for the branches' steps.</summary>
        public static int StageOf(string id)
        {
            int dot = id.IndexOf('.');
            return dot >= 0 && int.TryParse(id.Substring(dot + 1), out int stage) ? stage : 0;
        }

        /// <summary>How far the stage has evolved: 0 for the first, 1 for a branch's first step, 2 for its last.</summary>
        public static int TierOf(string id)
        {
            int stage = StageOf(id);
            return stage == 0 ? 0
                : stage % 2 == 1 ? 1
                : 2;
        }

        private static int BranchOf(int stage) => stage == 0 ? -1 : (stage - 1) / 2;

        /// <summary>
        /// The waves a stage's main blows (or its heal or guard) land in, in order: a card's other
        /// actions (a status, a draw) land in waves of their own after the effect, so the effect
        /// throws one arrow or star per blow, not per action.
        /// </summary>
        private static IEnumerable<int> MainWaves(string tree, int stage)
        {
            // 同じ相手への効果は1つの波にまとまる（BattleCardPlan）。焔獄だけは、先に付けるやけどが最初の波になる。
            int first = tree == "StarFall" && stage == 2 ? 1 : 0;
            int count = (tree, stage) switch
            {
                ("ManaBurst", 3) => 4,
                ("ManaBurst", 4) => 6,
                ("StarFall", 2 or 4) => 6,
                ("StarFall", _) => 5,
                ("ThunderArrow", 3) => 3,
                ("ThunderArrow", 4) => 5,
                ("ArrowStorm", 4) => 4,
                ("ArrowStorm", _) => 3,
                _ => 1,
            };
            for (int i = 0; i < count; i++)
                yield return first + i;
        }

        /// <summary>The colour of a unique skill's light, by its tree and branch.</summary>
        public static Color UniqueColor(string id)
        {
            int branch = BranchOf(StageOf(id));
            return TreeOf(id) switch
            {
                "ManaBurst" => branch == 2 ? Gold : Arcane,
                "StarFall" => branch switch
                {
                    0 => ColorOf(BattleSkillVfxKind.Fire),
                    1 => ColorOf(BattleSkillVfxKind.Ice),
                    _ => Starlight,
                },
                "ThunderArrow" => Bolt8,
                "ArrowStorm" => branch switch
                {
                    0 => Venom,
                    1 => Steel,
                    _ => Teal,
                },
                "ShieldStance" => branch == 1 ? Steel : ColorOf(BattleSkillVfxKind.Guard),
                "Issen" => branch == 0 ? Crimson : new Color(0.78f, 0.9f, 1f),
                "HealingWind" => branch switch
                {
                    0 => Blossom,
                    2 => Gold,
                    _ => Leafy,
                },
                _ => branch == 1 ? ColorOf(BattleSkillVfxKind.Fire) : Dawn,
            };
        }

        /// <summary>
        /// Plays a unique skill's stage: the wind-up its step earns, the tree's effect, and at its
        /// blows the step's extra layers.
        /// </summary>
        private IEnumerator Unique(
            string id,
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land
        )
        {
            int stage = StageOf(id);
            int tier = TierOf(id);
            var color = UniqueColor(id);
            float scale = 1f + 0.22f * tier;
            yield return WindUp(caster, color, tier);
            var main = new List<int>(MainWaves(TreeOf(id), stage));
            var empowered = Empower(hits, land, color, tier, main);
            yield return TreeOf(id) switch
            {
                "ManaBurst" => ManaBurstVfx(stage, caster, hits, empowered, color, scale),
                "StarFall" => StarFallVfx(stage, caster, hits, empowered, color, scale),
                "ThunderArrow" => ThunderArrowVfx(stage, caster, hits, empowered, color, scale),
                "ArrowStorm" => ArrowStormVfx(stage, caster, hits, empowered, color, scale),
                "ShieldStance" => ShieldStanceVfx(stage, caster, hits, empowered, color, scale),
                "Issen" => IssenVfx(stage, caster, hits, empowered, color, scale),
                "HealingWind" => HealingWindVfx(stage, caster, hits, empowered, color, scale),
                _ => ReviveVfx(stage, caster, hits, empowered, color, scale),
            };
            if (tier == 2)
                Ebb(caster, hits, color);
            yield return Wait(0.25f + 0.15f * tier);
        }

        // --- The steps' layers --------------------------------------------------------------

        /// <summary>
        /// The wind-up a step earns: none for the first stage beyond its own effect, a charge for a
        /// first step, and for a last step the night falling, light gathering into the user in
        /// slow motion, a magic circle turning under the user and a twinkle as it is let go.
        /// </summary>
        private IEnumerator WindUp(RectTransform caster, Color color, int tier)
        {
            if (tier == 0)
                yield break;
            if (tier == 1)
            {
                yield return Charge(caster, color, 0.32f, big: false);
                yield break;
            }
            DimTo(NightDim, 0.25f);
            var center = Center(FrontLayer, caster);
            var feet = Feet(FrontLayer, caster);
            Bloom(
                backLight,
                BattleVfxShape.MagicCircle,
                feet,
                new Vector2(420f, 420f),
                color,
                1.1f,
                0.6f,
                ErodeBright,
                floor: true,
                spin: 90f
            );
            Glow(frontLight, center, color, 520f, 0.9f, 0.7f);
            Gather(center, color, 36, 380f, 0.55f);
            SlowMotion(0.55f, 0.45f);
            Shake(0.06f, Vector2.zero);
            yield return Charge(caster, color, 0.5f, big: true);
            Twinkle(center + new Vector2(30f, 40f), color, 260f, 0.4f, 0f, 1f);
            Ring(frontLight, center, Color.white, 360f, 0.25f, floor: false);
        }

        /// <summary>
        /// The landing of each blow with the step's layers on top of the tree's own: a first step
        /// adds a shock ring, a star, sparks and a light; a last step also hits once like a
        /// finisher (flash, hit stop, slow-down, punch-in, two rings) and bursts again a moment
        /// later. Allies (healed, guarded) get a softer rise of light instead.
        /// </summary>
        private Func<int, BattleHitWeight> Empower(
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land,
            Color color,
            int tier,
            IReadOnlyList<int> main
        )
        {
            bool climaxed = false;
            int lastWave = main.Count > 0 ? main[main.Count - 1] : 0;
            return index =>
            {
                var weight = land(index);
                if (tier == 0 || index < 0 || index >= hits.Count)
                    return weight;
                var hit = hits[index];
                // 状態だけを付ける波（やけど・まひの付与、手札を引くなど）には重ねない。
                bool blow = false;
                foreach (int wave in main)
                    blow |= wave == hit.Wave;
                if (!blow)
                    return weight;
                // 山場は主な当たりの最後の波で一度だけ：最後の一撃がいちばん気持ちいい。
                bool finale = hit.Wave == lastWave;
                var at = Center(FrontLayer, hit.Area);
                var feet = Feet(FrontLayer, hit.Area);
                if (hit.Ally)
                {
                    Ring(backLight, feet, color, 420f + 160f * tier, 0.5f, floor: true);
                    Twinkle(
                        at + new Vector2(-30f, 50f),
                        color,
                        140f + 60f * tier,
                        0.45f,
                        0.05f,
                        1f
                    );
                    Rise(feet, color, 10 + 8 * tier);
                    if (tier == 2 && finale && !climaxed)
                    {
                        climaxed = true;
                        ScreenFlash(Color.Lerp(color, Color.white, 0.85f), MaxFlash * 0.6f);
                        Illuminate(at, color, 0.8f, 520f, 0.25f, 0.6f);
                    }
                    return weight;
                }
                Ring(
                    backLight,
                    feet,
                    Color.Lerp(color, Color.white, 0.4f),
                    560f,
                    0.4f,
                    floor: true
                );
                Star(at, Color.white, 200f + 60f * tier, 0.18f);
                Sparks(at, color, 14 + 10 * tier, 1f + 0.2f * tier, Vector2.up);
                Illuminate(
                    at,
                    color,
                    0.6f + 0.2f * tier,
                    380f + 120f * tier,
                    0.05f,
                    0.35f,
                    spill: false
                );
                if (tier == 2 && finale && !climaxed)
                {
                    climaxed = true;
                    ScreenFlash(Color.Lerp(color, Color.white, 0.9f), MaxFlash);
                    HitStop(0.14f);
                    SlowMotion(0.35f, 0.3f);
                    Punch(at, 0.05f);
                    Shake(0.9f, Vector2.down);
                    Ring(backLight, feet, Color.white, 980f, 0.45f, floor: true);
                    Ring(backLight, feet, color, 1400f, 0.75f, floor: true);
                    Illuminate(
                        at + Vector2.up * 80f,
                        color,
                        0.75f,
                        700f,
                        0.25f,
                        0.6f,
                        flicker: 0.3f
                    );
                    StartCoroutine(Aftershock(hit.Area, color));
                }
                return weight;
            };
        }

        /// <summary>A second burst a beat after a last step's climax: the blow keeps paying off.</summary>
        private IEnumerator Aftershock(RectTransform target, Color color)
        {
            yield return Wait(0.16f);
            if (target == null)
                yield break;
            var at = Center(FrontLayer, target);
            var feet = Feet(FrontLayer, target);
            HotFlash(at, Color.white, 320f);
            Star(at, color, 420f, 0.3f);
            Ring(frontLight, at, Color.Lerp(color, Color.white, 0.5f), 700f, 0.35f, floor: false);
            Sparks(at, color, 40, 1.5f, Vector2.up);
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Streak,
                    Bright = CoreBright,
                    From = Color.white,
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(900f, 1600f),
                    Spread = 360f,
                    Life = new Vector2(0.12f, 0.24f),
                    Size = new Vector2(10f, 18f),
                    Stretch = 0.06f,
                    Area = 30f,
                },
                18
            );
            Pool(feet, color, 420f, 0.15f, 0.5f);
            Shake(0.35f, Vector2.up);
        }

        /// <summary>The ebb of a last step: motes of its light drifting up over everyone it reached.</summary>
        private void Ebb(RectTransform caster, IReadOnlyList<BattleSkillHit> hits, Color color)
        {
            var seen = new HashSet<RectTransform>();
            foreach (var hit in hits)
                if (hit.Area != null && seen.Add(hit.Area))
                    Rise(Feet(FrontLayer, hit.Area), color, 14);
            if (caster != null)
                Rise(Feet(FrontLayer, caster), color, 10);
        }

        /// <summary>Motes of light rising slowly from the floor at <paramref name="feet"/>.</summary>
        private void Rise(Vector2 feet, Color color, int count) =>
            Spray(
                frontLight,
                feet + Vector2.up * 30f,
                new Burst
                {
                    Shape = BattleVfxShape.Mote,
                    Bright = CoreBright,
                    From = Color.white,
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(40f, 140f),
                    Direction = 90f,
                    Spread = 40f,
                    Life = new Vector2(0.7f, 1.3f),
                    Size = new Vector2(12f, 26f),
                    Shrink = 0.3f,
                    Gravity = -120f,
                    Drag = 1.2f,
                    Area = 90f,
                    Delay = new Vector2(0f, 0.25f),
                },
                count
            );

        // --- The trees -------------------------------------------------------------------------

        /// <summary>
        /// マナバースト, a swell of raw magic: a violet orb is flung and bursts over the enemies in
        /// rays. 烈・極 burst wider and whiter; 散・嵐 split it into bolts that rain on random
        /// enemies (嵐 swirls them); 環・還流 send the spent magic back to the user in golden rings.
        /// </summary>
        private IEnumerator ManaBurstVfx(
            int stage,
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land,
            Color color,
            float scale
        )
        {
            int branch = BranchOf(stage);
            var from = Center(FrontLayer, caster) + new Vector2(60f, 40f);
            if (branch == 1)
            {
                foreach (int wave in MainWaves("ManaBurst", stage))
                foreach (int index in Wave(hits, wave))
                {
                    var at = Center(FrontLayer, hits[index].Area);
                    var start =
                        stage == 4 ? from + UnityEngine.Random.insideUnitCircle * 160f : from;
                    yield return Missile(
                        BattleVfxShape.Fireball,
                        start,
                        at,
                        new Vector2(150f, 150f) * scale,
                        new Vector2(0.85f, 0.5f),
                        stage == 4 ? 0.14f : 0.2f,
                        stage == 4 ? UnityEngine.Random.Range(-160f, 160f) : 70f,
                        color
                    );
                    land(index);
                    HotFlash(at, color, 200f * scale);
                    Ring(
                        backLight,
                        Feet(FrontLayer, hits[index].Area),
                        color,
                        380f,
                        0.3f,
                        floor: true
                    );
                }
                yield break;
            }
            var mid = MidFeet(hits, 0) + Vector2.up * 140f;
            yield return Missile(
                BattleVfxShape.Fireball,
                from,
                mid,
                new Vector2(260f, 260f) * scale,
                new Vector2(0.85f, 0.5f),
                0.3f,
                90f,
                color
            );
            Glow(frontLight, mid, color, 900f * scale, 0.7f, 0.9f);
            Bloom(
                frontLight,
                BattleVfxShape.Star,
                mid,
                new Vector2(700f, 700f) * scale,
                Color.white,
                0.45f,
                0.1f,
                CoreBright,
                spin: 40f
            );
            Ring(frontLight, mid, color, 1000f * scale, 0.5f, floor: false);
            if (stage == 2)
                Column(
                    frontLight,
                    MidFeet(hits, 0),
                    Color.Lerp(color, Color.white, 0.5f),
                    new Vector2(380f, 1200f),
                    0.6f,
                    down: false,
                    fast: true
                );
            foreach (int index in Wave(hits, 0))
            {
                var at = Center(FrontLayer, hits[index].Area);
                var weight = land(index);
                Impact(
                    hits[index].Area,
                    at,
                    color,
                    weight,
                    0.8f * scale,
                    Vector2.right,
                    flashScreen: false
                );
                Sparks(at, color, 16, 1f, (at - mid).normalized);
            }
            if (branch == 2)
            {
                yield return Wait(0.18f);
                var home = Center(FrontLayer, caster);
                yield return Missile(
                    BattleVfxShape.Glow,
                    mid,
                    home,
                    new Vector2(220f, 220f),
                    new Vector2(0.5f, 0.5f),
                    0.32f,
                    -120f,
                    Gold
                );
                Ring(frontLight, home, Gold, 380f, 0.4f, floor: false);
                Twinkle(home + new Vector2(20f, 40f), Gold, 200f, 0.45f, 0f, -1f);
                if (stage == 6)
                    Bloom(
                        frontLight,
                        BattleVfxShape.Crest,
                        home + Vector2.up * 120f,
                        new Vector2(140f, 180f),
                        Gold,
                        0.8f,
                        0.4f,
                        ErodeBright
                    );
            }
        }

        /// <summary>
        /// 星降り, a shower of falling stars: stars streak down from the sky onto their targets
        /// one after another and burst. 灼・焔獄 fall as fireballs and set the ground alight;
        /// 氷晶・凍星 fall as ice and freeze where they land; 墜星・天墜 all converge on one
        /// enemy, 天墜 ending in a pillar of light.
        /// </summary>
        private IEnumerator StarFallVfx(
            int stage,
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land,
            Color color,
            float scale
        )
        {
            int branch = BranchOf(stage);
            DimTo(NightDim, 0.2f);
            var shape = branch switch
            {
                0 => BattleVfxShape.Fireball,
                1 => BattleVfxShape.IceSpear,
                _ => BattleVfxShape.Streak,
            };
            float top = FrontLayer.rect.yMax + 120f;
            // 焔獄 sets every enemy alight first, so its stars fall on burning ground.
            if (stage == 2)
                foreach (int index in Wave(hits, 0))
                {
                    land(index);
                    FloorMark(
                        BattleVfxShape.Scorch,
                        Feet(FrontLayer, hits[index].Area),
                        Vector2.one * 300f,
                        new Color(1f, 0.42f, 0.1f),
                        0.7f,
                        1.6f,
                        flat: true
                    );
                    Pool(Feet(FrontLayer, hits[index].Area), color, 360f, 0.3f, 0.6f);
                }
            var waves = new List<int>(MainWaves("StarFall", stage));
            for (int n = 0; n < waves.Count; n++)
            {
                int wave = waves[n];
                foreach (int index in Wave(hits, wave))
                {
                    var area = hits[index].Area;
                    var at = Center(FrontLayer, area);
                    var sky = new Vector2(at.x + UnityEngine.Random.Range(140f, 360f), top);
                    if (branch == 2)
                        sky = new Vector2(at.x + 260f + 70f * (n - waves.Count / 2f), top);
                    yield return Shoot(
                        shape,
                        sky,
                        at,
                        new Vector2(360f, 360f) * scale,
                        new Vector2(0.85f, 0.5f),
                        0.16f,
                        0f,
                        color,
                        0f,
                        new Burst
                        {
                            Shape = branch == 1 ? BattleVfxShape.Snowflake : BattleVfxShape.Spark,
                            Bright = CoreBright,
                            From = Color.white,
                            To = new Color(color.r, color.g, color.b, 0f),
                            Speed = new Vector2(30f, 120f),
                            Spread = 360f,
                            Life = new Vector2(0.25f, 0.5f),
                            Size = new Vector2(10f, 22f),
                            Area = 20f,
                        }
                    );
                    var weight = land(index);
                    switch (branch)
                    {
                        case 0:
                            Explosion(area, at, weight);
                            break;
                        case 1:
                            Frost(area, at, weight);
                            break;
                        default:
                            Star(at, Color.white, 260f * scale, 0.2f);
                            Ring(
                                backLight,
                                Feet(FrontLayer, area),
                                color,
                                520f * scale,
                                0.35f,
                                floor: true
                            );
                            Sparks(at, color, 14, 1f, Vector2.up);
                            break;
                    }
                    Illuminate(at, color, 0.7f, 420f * scale, 0.04f, 0.3f, spill: false);
                    Shake(0.25f * scale, Vector2.down);
                    yield return Wait(0.05f);
                }
            }
            if (stage == 6 && hits.Count > 0)
            {
                var feet = Feet(FrontLayer, hits[0].Area);
                Column(
                    frontLight,
                    feet,
                    Color.Lerp(color, Color.white, 0.6f),
                    new Vector2(520f, 1400f),
                    0.8f,
                    down: true,
                    fast: true
                );
                Ring(backLight, feet, Color.white, 1100f, 0.5f, floor: true);
                Puffs(
                    backSmoke,
                    feet + Vector2.up * 60f,
                    new Color(0.2f, 0.22f, 0.3f, 0.6f),
                    8,
                    260f,
                    1.8f,
                    220f
                );
            }
        }

        /// <summary>
        /// 雷撃の矢, an arrow wrapped in lightning: it flies from the user and a bolt strikes where
        /// it lands. 痺・縛雷 tie the target in crackling rings (縛雷 also nails it down with a
        /// second bolt); 連・迅雷 jump from enemy to enemy; 閃・閃光 fly as a thin flash that bursts
        /// gold when it finds a weakness.
        /// </summary>
        private IEnumerator ThunderArrowVfx(
            int stage,
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land,
            Color color,
            float scale
        )
        {
            int branch = BranchOf(stage);
            var from = Center(FrontLayer, caster) + new Vector2(70f, 30f);
            foreach (int wave in MainWaves("ThunderArrow", stage))
            foreach (int index in Wave(hits, wave))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var feet = Feet(FrontLayer, area);
                yield return Shoot(
                    BattleVfxShape.Arrow,
                    from,
                    at,
                    new Vector2(220f, 60f) * scale,
                    new Vector2(0.9f, 0.5f),
                    branch == 2 ? 0.07f : 0.12f,
                    0f,
                    color,
                    0f,
                    new Burst
                    {
                        Shape = BattleVfxShape.Spark,
                        Bright = CoreBright,
                        From = Color.white,
                        To = new Color(color.r, color.g, color.b, 0f),
                        Speed = new Vector2(60f, 240f),
                        Spread = 360f,
                        Life = new Vector2(0.15f, 0.35f),
                        Size = new Vector2(8f, 16f),
                        Area = 14f,
                    }
                );
                var weight = land(index);
                Bolt(feet);
                Impact(
                    area,
                    at,
                    color,
                    weight,
                    0.7f * scale,
                    (at - from).normalized,
                    flashScreen: false
                );
                StartCoroutine(Crackle(area, color));
                if (branch == 0)
                {
                    Ring(frontLight, at, color, 300f, 0.5f, floor: false);
                    Ring(backLight, feet, color, 420f, 0.6f, floor: true);
                    if (stage == 2)
                    {
                        yield return Wait(0.1f);
                        Bolt(feet);
                        Bloom(
                            backLight,
                            BattleVfxShape.MagicCircle,
                            feet,
                            new Vector2(360f, 360f),
                            color,
                            0.8f,
                            0.3f,
                            ErodeBright,
                            floor: true,
                            spin: -120f
                        );
                    }
                }
                if (branch == 2 && weight != BattleHitWeight.Normal)
                {
                    Twinkle(at + new Vector2(20f, 40f), Gold, 260f, 0.4f, 0f, 1f);
                    Sparks(at, Gold, 24, 1.3f, Vector2.up);
                }
                if (branch == 1)
                    from = at;
                yield return Wait(branch == 1 ? 0.04f : 0.08f);
            }
        }

        /// <summary>
        /// 矢の雨, a storm of arrows: streaks rain slanting onto every enemy, wave after wave.
        /// 毒・蠱毒 drip venom and leave a green haze; 穿・穿天 punch through armour with heavier
        /// heads and fly debris (穿天 from straight above); 速・疾風 fly flat and fast on a swirl of
        /// wind.
        /// </summary>
        private IEnumerator ArrowStormVfx(
            int stage,
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land,
            Color color,
            float scale
        )
        {
            int branch = BranchOf(stage);
            float direction =
                branch == 2 ? 0f
                : stage == 4 ? -90f
                : -60f;
            foreach (int wave in MainWaves("ArrowStorm", stage))
            {
                var indices = Wave(hits, wave);
                foreach (int index in indices)
                {
                    var area = hits[index].Area;
                    var at = Center(FrontLayer, area);
                    var start =
                        at - (Vector2)(Quaternion.Euler(0f, 0f, direction) * Vector2.right) * 420f;
                    Spray(
                        frontLight,
                        start,
                        new Burst
                        {
                            Shape = BattleVfxShape.Streak,
                            Bright = CoreBright,
                            From = Color.white,
                            To = new Color(color.r, color.g, color.b, 0.2f),
                            Speed = new Vector2(1500f, 2200f),
                            Direction = direction,
                            Spread = 5f,
                            Life = new Vector2(0.18f, 0.24f),
                            Size = new Vector2(8f, 14f) * scale,
                            Stretch = 0.06f,
                            Area = 120f,
                        },
                        Mathf.RoundToInt(8 * scale)
                    );
                }
                yield return Wait(0.18f);
                foreach (int index in indices)
                {
                    var area = hits[index].Area;
                    var at = Center(FrontLayer, area);
                    var feet = Feet(FrontLayer, area);
                    var weight = land(index);
                    Sparks(at, color, 10, 0.8f, Vector2.up);
                    Illuminate(at, color, 0.45f, 280f, 0.03f, 0.2f, spill: false);
                    if (branch == 0)
                        Puffs(
                            frontSmoke,
                            feet + Vector2.up * 40f,
                            new Color(0.45f, 0.9f, 0.3f, 0.45f),
                            3,
                            70f,
                            1.1f,
                            110f
                        );
                    if (branch == 1)
                        Debris(at, new Color(0.6f, 0.66f, 0.75f), 8, Vector2.up);
                    if (weight != BattleHitWeight.Normal)
                        Cross(at, color, weight);
                }
                Shake(0.15f * scale, Vector2.down);
            }
            if (branch == 2)
            {
                var mid = MidFeet(hits, 0);
                Bloom(
                    backLight,
                    BattleVfxShape.Tornado,
                    mid,
                    new Vector2(520f, 640f) * scale,
                    color,
                    0.7f,
                    0.2f,
                    ErodeBright
                );
                // 疾風 hastens the party: a gust of leaves round each ally.
                if (stage == 6)
                    foreach (var hit in hits)
                        if (hit.Ally)
                            Spray(frontLight, Feet(FrontLayer, hit.Area), Gust(color), 8);
            }
        }

        private static Burst Gust(Color color) =>
            new()
            {
                Shape = BattleVfxShape.Leaf,
                From = Color.Lerp(color, Color.white, 0.4f),
                To = new Color(color.r, color.g, color.b, 0f),
                Speed = new Vector2(120f, 320f),
                Direction = 60f,
                Spread = 60f,
                Life = new Vector2(0.5f, 0.9f),
                Size = new Vector2(16f, 30f),
                Gravity = -80f,
                Drag = 1.5f,
                Spin = 240f,
                Area = 60f,
            };

        /// <summary>
        /// 盾の構え, a shield raised: a crest flashes up and a dome of light closes over the
        /// guarded. 堅・不動 thicken it into a heavy wall that holds (不動 shakes the ground);
        /// 返・鏡盾 turn it to a mirror that glints, 鏡盾 first driving the crest into an enemy;
        /// 陣・城壁 raise a dome over the whole party with a crest over each.
        /// </summary>
        private IEnumerator ShieldStanceVfx(
            int stage,
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land,
            Color color,
            float scale
        )
        {
            int branch = BranchOf(stage);
            var home = Center(FrontLayer, caster);
            bool guarded = false;
            for (int wave = 0; wave < Waves(hits); wave++)
            {
                var indices = Wave(hits, wave);
                if (indices.Count == 0 || (guarded && hits[indices[0]].Ally))
                    continue;
                if (!hits[indices[0]].Ally)
                {
                    foreach (int index in indices)
                    {
                        var area = hits[index].Area;
                        var at = Center(FrontLayer, area);
                        yield return Shoot(
                            BattleVfxShape.Crest,
                            home,
                            at,
                            new Vector2(160f, 200f) * scale,
                            new Vector2(0.5f, 0.5f),
                            0.14f,
                            0f,
                            color,
                            0f,
                            null,
                            turn: false
                        );
                        var weight = land(index);
                        Impact(
                            area,
                            at,
                            color,
                            weight,
                            0.9f * scale,
                            Vector2.right,
                            flashScreen: false
                        );
                        Ring(frontLight, at, Color.white, 420f, 0.3f, floor: false);
                    }
                    continue;
                }
                guarded = true;
                var left = float.MaxValue;
                var right = float.MinValue;
                var floorY = 0f;
                foreach (int index in indices)
                {
                    var feet = Feet(FrontLayer, hits[index].Area);
                    left = Mathf.Min(left, feet.x);
                    right = Mathf.Max(right, feet.x);
                    floorY = feet.y;
                }
                float width = Mathf.Max(260f, right - left + 260f) * (branch == 0 ? scale : 1f);
                var basePoint = new Vector2((left + right) / 2f, floorY);
                Bloom(
                    frontLight,
                    BattleVfxShape.Crest,
                    basePoint + Vector2.up * 220f,
                    new Vector2(150f, 190f) * scale,
                    color,
                    0.7f,
                    0.4f,
                    ErodeBright
                );
                Dome(backLight, basePoint, width, branch == 0 ? 1f : 0.8f, breaks: branch != 0);
                Dome(frontLight, basePoint, width, 0.3f, breaks: false);
                foreach (int index in indices)
                {
                    land(index);
                    var feet = Feet(FrontLayer, hits[index].Area);
                    Circle(feet, color, 0.8f);
                    if (branch == 2 && indices.Count > 1)
                        Bloom(
                            frontLight,
                            BattleVfxShape.Crest,
                            feet + Vector2.up * 260f,
                            new Vector2(70f, 90f),
                            color,
                            0.6f,
                            0.3f,
                            ErodeBright
                        );
                }
                if (branch == 1)
                {
                    Twinkle(basePoint + new Vector2(-40f, 260f), Color.white, 300f, 0.5f, 0.1f, 1f);
                    Twinkle(basePoint + new Vector2(60f, 180f), color, 200f, 0.4f, 0.25f, -1f);
                }
                if (stage == 2)
                {
                    Shake(0.5f, Vector2.down);
                    Debris(
                        basePoint + Vector2.up * 10f,
                        new Color(0.5f, 0.5f, 0.55f),
                        14,
                        Vector2.up
                    );
                }
                Illuminate(
                    basePoint + Vector2.up * 120f,
                    color,
                    0.7f,
                    420f + width * 0.4f,
                    0.3f,
                    0.5f
                );
                yield return Wait(0.2f);
            }
        }

        /// <summary>
        /// 一閃, a single flash of the blade: the night falls, one long white stroke cuts the
        /// target and it splits a beat later. 血華・散華 scatter crimson petals that swirl (散華
        /// into a whirl of them); 疾・瞬 strike so fast after-images of the stroke stack up; 薙・
        /// 嵐薙 sweep one long arc across every enemy (嵐薙 blowing armour shards away).
        /// </summary>
        private IEnumerator IssenVfx(
            int stage,
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land,
            Color color,
            float scale
        )
        {
            int branch = BranchOf(stage);
            if (branch != 1)
                DimTo(NightDim, 0.15f);
            yield return Wait(branch == 1 ? 0.04f : 0.16f);
            foreach (int wave in MainWaves("Issen", stage))
            {
                var indices = Wave(hits, wave);
                foreach (int index in indices)
                {
                    var area = hits[index].Area;
                    var at = Center(FrontLayer, area);
                    if (hits[index].Ally)
                    {
                        land(index);
                        continue;
                    }
                    int strokes = branch == 1 ? (stage == 4 ? 4 : 3) : 1;
                    for (int n = 0; n < strokes; n++)
                    {
                        Arc(
                            frontLight,
                            at + new Vector2(0f, 10f * n),
                            -20f + 14f * n,
                            n % 2 == 1,
                            color,
                            1.3f * scale
                        );
                        if (n < strokes - 1)
                            yield return Wait(0.03f);
                    }
                    Spray(
                        frontLight,
                        at,
                        new Burst
                        {
                            Shape = BattleVfxShape.Streak,
                            Bright = CoreBright,
                            From = Color.white,
                            To = new Color(color.r, color.g, color.b, 0f),
                            Speed = new Vector2(1600f, 2400f),
                            Direction = -20f,
                            Spread = 4f,
                            Life = new Vector2(0.1f, 0.16f),
                            Size = new Vector2(8f, 14f),
                            Stretch = 0.08f,
                            Area = 40f,
                        },
                        10
                    );
                    yield return Wait(branch == 2 ? 0.02f : 0.1f);
                    var weight = land(index);
                    Cross(at, color, weight);
                    Impact(
                        area,
                        at,
                        color,
                        weight,
                        0.9f * scale,
                        Vector2.right,
                        flashScreen: branch == 0
                    );
                    if (branch == 0)
                        Spray(
                            frontLight,
                            at,
                            new Burst
                            {
                                Shape = BattleVfxShape.Leaf,
                                From = new Color(1f, 0.55f, 0.62f),
                                To = new Color(0.8f, 0.1f, 0.18f, 0f),
                                Speed = new Vector2(120f, 520f),
                                Spread = 360f,
                                Life = new Vector2(0.7f, 1.3f),
                                Size = new Vector2(18f, 32f) * scale,
                                Gravity = 160f,
                                Drag = 2f,
                                Spin = stage == 2 ? 520f : 260f,
                                Area = 50f,
                            },
                            stage == 2 ? 40 : 22
                        );
                    if (stage == 6)
                        Debris(at, new Color(0.55f, 0.6f, 0.7f), 12, Vector2.right);
                }
            }
        }

        /// <summary>
        /// 癒しの風, a healing breeze: leaves of light swirl round each ally and a green light
        /// rises. 息吹・春風 bloom into petals that keep drifting (the regen); 守・聖域 close a soft
        /// veil over the party (聖域 on a glowing circle); 巡り・恵み turn golden rings that bring
        /// the energy back to the user.
        /// </summary>
        private IEnumerator HealingWindVfx(
            int stage,
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land,
            Color color,
            float scale
        )
        {
            int branch = BranchOf(stage);
            foreach (int wave in MainWaves("HealingWind", stage))
            {
                var indices = Wave(hits, wave);
                foreach (int index in indices)
                {
                    var area = hits[index].Area;
                    var feet = Feet(FrontLayer, area);
                    var at = Center(FrontLayer, area);
                    land(index);
                    Spray(
                        frontLight,
                        feet + Vector2.up * 20f,
                        new Burst
                        {
                            Shape = branch == 0 ? BattleVfxShape.Sparkle : BattleVfxShape.Leaf,
                            From = Color.Lerp(color, Color.white, 0.5f),
                            To = new Color(color.r, color.g, color.b, 0f),
                            Speed = new Vector2(80f, 220f) * scale,
                            Direction = 90f,
                            Spread = 70f,
                            Life = new Vector2(0.6f, 1.1f),
                            Size = new Vector2(14f, 30f),
                            Gravity = -120f,
                            Drag = 1.5f,
                            Spin = 300f,
                            Area = 70f,
                        },
                        Mathf.RoundToInt(14 * scale)
                    );
                    Pool(feet, color, 300f * scale, 0.3f, 0.6f);
                    Illuminate(at, color, 0.6f, 320f, 0.2f, 0.5f);
                }
                if (indices.Count > 0 && branch == 1)
                {
                    var mid = MidFeet(hits, wave);
                    if (stage == 4)
                        Bloom(
                            backLight,
                            BattleVfxShape.MagicCircle,
                            mid,
                            new Vector2(760f, 760f),
                            color,
                            1.2f,
                            0.5f,
                            ErodeBright,
                            floor: true,
                            spin: 60f
                        );
                    Dome(backLight, mid, 900f, 0.6f, breaks: false);
                }
                yield return Wait(0.12f);
            }
            if (branch == 2)
            {
                var home = Center(FrontLayer, caster);
                Ring(frontLight, home, Gold, 340f, 0.45f, floor: false);
                Twinkle(home + new Vector2(30f, 50f), Gold, 220f, 0.45f, 0.05f, 1f);
                if (stage == 6)
                    Bloom(
                        frontLight,
                        BattleVfxShape.Leaf,
                        home + Vector2.up * 140f,
                        new Vector2(120f, 120f),
                        Gold,
                        0.8f,
                        0.4f,
                        ErodeBright,
                        spin: 180f
                    );
            }
        }

        /// <summary>
        /// 蘇生, a rising: a shaft of light comes down on the fallen ally and white motes lift them.
        /// 祈・奇跡 are quick and quiet (奇跡 with a burst of feathers); 加護・不死鳥 wrap them in a
        /// golden shell (不死鳥 in rising wings of flame); 活力・再起 flare red-gold as they stand,
        /// a crest of courage over them.
        /// </summary>
        private IEnumerator ReviveVfx(
            int stage,
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land,
            Color color,
            float scale
        )
        {
            int branch = BranchOf(stage);
            foreach (int wave in MainWaves("Revive", stage))
            foreach (int index in Wave(hits, wave))
            {
                var area = hits[index].Area;
                var feet = Feet(FrontLayer, area);
                var at = Center(FrontLayer, area);
                {
                    Column(
                        frontLight,
                        feet,
                        Color.Lerp(color, Color.white, 0.5f),
                        new Vector2(300f * scale, 1100f),
                        branch == 0 ? 0.45f : 0.7f,
                        down: true,
                        fast: branch == 0
                    );
                    yield return Wait(branch == 0 ? 0.12f : 0.22f);
                    Pillar(feet);
                }
                land(index);
                Rise(feet, color, Mathf.RoundToInt(16 * scale));
                Pool(feet, color, 380f * scale, 0.4f, 0.7f);
                Illuminate(at, color, 0.9f, 460f * scale, 0.3f, 0.6f);
                if (stage == 2)
                    Spray(frontLight, at + Vector2.up * 60f, Feathers(Color.white), 24);
                if (branch == 1)
                {
                    Dome(backLight, feet, 300f, 0.9f, breaks: true);
                    if (stage == 4)
                        Spray(
                            backLight,
                            feet + Vector2.up * 40f,
                            new Burst
                            {
                                Shape = BattleVfxShape.FlameTongue,
                                Bright = ErodeBright,
                                Fire = true,
                                Speed = new Vector2(200f, 520f),
                                Direction = 90f,
                                Spread = 70f,
                                Life = new Vector2(0.5f, 0.9f),
                                Size = new Vector2(80f, 160f),
                                Grow = 0.4f,
                                Shrink = 1.3f,
                                Gravity = -300f,
                                Drag = 2f,
                                Area = 80f,
                            },
                            16
                        );
                }
                if (branch == 2)
                {
                    Bloom(
                        frontLight,
                        BattleVfxShape.Crest,
                        at + Vector2.up * 170f,
                        new Vector2(110f, 140f),
                        Crimson,
                        0.8f,
                        0.4f,
                        ErodeBright
                    );
                    Sparks(at, Crimson, 18, 1.1f, Vector2.up);
                }
                yield return Wait(0.1f);
            }
        }

        private static Burst Feathers(Color color) =>
            new()
            {
                Shape = BattleVfxShape.Sparkle,
                From = color,
                To = new Color(color.r, color.g, color.b, 0f),
                Speed = new Vector2(80f, 260f),
                Spread = 360f,
                Life = new Vector2(0.8f, 1.4f),
                Size = new Vector2(18f, 34f),
                Gravity = 60f,
                Drag = 2f,
                Spin = 200f,
                Area = 60f,
            };
    }
}
