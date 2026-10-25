using System;
using System.Collections;
using System.Collections.Generic;
using Baryonyx.SkillEvolution;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The unique skills' effects (doc/features/skill-evolution.md#段階ごとのエフェクト): each of
    /// the twelve trees has an effect of its own, built from what it shows really is (an orb of
    /// compressed magic, meteors, an arrow calling down the lightning, a volley, a barrier, one
    /// cut, a breeze, a rising, an eye's scan, quick shots, a war cry, shafts of light from the
    /// sky), each of its parts split into layers apart in their cover and time; each branch turns
    /// it its own way (an element, a target, a motion), and each step of evolution plays it
    /// bigger. The first stage is the tree's effect alone. A first step adds a charged wind-up
    /// and, at every blow, the step's layers (the air lit behind the target, a pool of light, a
    /// white star, sparks, dust driven out and motes rising late). A last step always cuts in,
    /// gathers light into the user in slow motion, and lands with a white flash, a hit stop, a
    /// slow-down, a punch-in and thin rays, then a second, weaker and wider burst a moment later
    /// and motes of light drifting up as it ebbs: the bigger the step, the more it pays off.
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
            "ArcaneEye",
            "QuickShot",
            "Rally",
            "Judgment",
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
            // 魔眼・射と滅光は全体を見抜く波のあとに1体を撃ち、聖浄は強化を消す波のあとに打つ。
            int first =
                (tree == "StarFall" && stage == 2)
                || (tree == "ArcaneEye" && stage is 1 or 2)
                || (tree == "Judgment" && stage == 4)
                    ? 1
                    : 0;
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
                ("QuickShot", 1 or 6) => 3,
                ("QuickShot", 2) => 6,
                ("QuickShot", _) => 2,
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
                "ArcaneEye" => branch == 2 ? Gold : Arcane,
                "QuickShot" => branch == 2 ? Steel : Teal,
                "Rally" => branch == 2 ? ColorOf(BattleSkillVfxKind.Guard) : Gold,
                "Judgment" => branch == 2 ? Leafy : Dawn,
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
            yield return WindUp(caster, color, tier, UniqueSkillTrees.FindStage(id)?.Card);
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
                "Revive" => ReviveVfx(stage, caster, hits, empowered, color, scale),
                "ArcaneEye" => ArcaneEyeVfx(stage, caster, hits, empowered, color, scale),
                "QuickShot" => QuickShotVfx(stage, caster, hits, empowered, color, scale),
                "Rally" => RallyVfx(stage, caster, hits, empowered, color, scale),
                _ => JudgmentVfx(stage, caster, hits, empowered, color, scale),
            };
            if (tier == 2)
                Ebb(caster, hits, color);
            yield return Wait(0.25f + 0.15f * tier);
        }

        /// <summary>
        /// 魔眼, an eye that sees through (doc/features/skill-evolution.md#ツリーごとの演出): the user's
        /// eye glints, a thin line of light sweeps across the enemies as a scan does, and as it
        /// passes each a sight closes on it and its weak point glints. 射・滅光 then shoot a thin
        /// beam from the eye into one enemy, which bursts (滅光 wider, laying the guard down);
        /// 識・叡智 draw what was seen back to the user as streaks of light (叡智 with a spark of
        /// energy); 律・星律 turn motes of fire, ice and thunder round the user on a golden circle.
        /// </summary>
        private IEnumerator ArcaneEyeVfx(
            int stage,
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land,
            Color color,
            float scale
        )
        {
            int branch = BranchOf(stage);
            int tier = TierOfStage(stage);
            var eye = Center(FrontLayer, caster) + new Vector2(24f, 70f);
            DimTo(SpellDim, 0.15f);
            // 予兆：使い手の目に光が一点集まって瞬く。
            Gather(eye, color, 8, 160f, 0.18f);
            yield return Wait(0.16f);
            Twinkle(eye, Color.Lerp(color, Color.white, 0.5f), 180f, 0.3f, 0f, 1f);
            Illuminate(eye, color, 0.5f, 220f, 0.05f, 0.25f, spill: false);

            // 走査：細い光の線が敵の列を横切り、通った敵に照準が閉じて、弱点が光る。
            var enemies = DistinctEnemies(hits);
            if (enemies.Count > 0)
            {
                float left = float.MaxValue;
                float right = float.MinValue;
                foreach (int i in enemies)
                {
                    var c = Center(FrontLayer, hits[i].Area);
                    left = Mathf.Min(left, c.x - 160f);
                    right = Mathf.Max(right, c.x + 160f);
                }
                float floorY = Feet(FrontLayer, hits[enemies[0]].Area).y;
                const float sweep = 0.32f;
                Scan(new Vector2(left, floorY), new Vector2(right, floorY), color, sweep);
                foreach (int i in enemies)
                {
                    var c = Center(FrontLayer, hits[i].Area);
                    float when = sweep * Mathf.InverseLerp(left, right, c.x);
                    StartCoroutine(After(when, () => Sight(c, color)));
                }
                yield return Wait(sweep + 0.1f);
            }

            var landed = new HashSet<int>();
            if (branch == 0)
            {
                // 射・滅光：目から細い光線が1体へ走り、内側から弾ける。
                foreach (int i in Wave(hits, 1))
                {
                    var area = hits[i].Area;
                    var at = Center(FrontLayer, area);
                    Shaft(eye, at, color, (26f + 10f * tier) * scale, 0.05f, 0.35f);
                    yield return Wait(0.05f);
                    var weight = land(i);
                    landed.Add(i);
                    HotFlash(at, Color.white, 260f * scale);
                    Rays(at, color, 14 + 6 * tier, 0.9f);
                    Radiance(backLight, at, color, 600f * scale, 0.5f, 0.03f, 0.1f, 0.45f);
                    Impact(
                        area,
                        at,
                        color,
                        weight,
                        0.8f * scale,
                        (at - eye).normalized,
                        flashScreen: false
                    );
                    if (stage == 2)
                        Bloom(
                            frontLight,
                            BattleVfxShape.Chevron,
                            at + new Vector2(0f, 130f),
                            new Vector2(110f, 150f),
                            Violet,
                            0.7f,
                            0.6f,
                            ErodeBright,
                            angle: 180f,
                            delay: 0.12f
                        );
                }
            }
            // 弱点を見抜く（とほかの働き）を、残りの当たりで届ける。
            for (int i = 0; i < hits.Count; i++)
                if (!landed.Contains(i))
                    land(i);
            if (branch == 1 && enemies.Count > 0)
            {
                // 識・叡智：見抜いたことが光の筋になって使い手へ戻る（カードを引く）。
                foreach (int i in enemies)
                {
                    var c = Center(FrontLayer, hits[i].Area);
                    for (int n = 0; n < 4 + 2 * tier; n++)
                        DrawIn(
                            frontLight,
                            c + UnityEngine.Random.insideUnitCircle * 50f,
                            eye,
                            color,
                            0.3f,
                            0.02f * n,
                            UnityEngine.Random.Range(4f, 7f)
                        );
                }
                yield return Wait(0.3f);
                Radiance(backLight, eye, color, 320f, 0.5f, 0.03f, 0.1f, 0.4f);
                Twinkle(eye + new Vector2(20f, 30f), Color.white, 160f, 0.3f, 0f, -1f);
                if (stage == 4)
                {
                    Sparks(eye, Gold, 16, 0.8f, Vector2.up);
                    Twinkle(eye + new Vector2(-30f, 50f), Gold, 220f, 0.4f, 0.08f, 1f);
                }
            }
            if (branch == 2)
            {
                // 律・星律：炎・氷・雷の色の光が、使い手の足元の金の円の上を回って体へ入る。
                var feet = Feet(FrontLayer, caster);
                Circle(feet, color, 0.9f);
                var center = Center(FrontLayer, caster);
                var colors = new[]
                {
                    ColorOf(BattleSkillVfxKind.Fire),
                    ColorOf(BattleSkillVfxKind.Ice),
                    ColorOf(BattleSkillVfxKind.Thunder),
                };
                for (int n = 0; n < 3; n++)
                {
                    float a = n * 120f * Mathf.Deg2Rad;
                    var from = center + new Vector2(Mathf.Cos(a) * 180f, Mathf.Sin(a) * 90f);
                    for (int m = 0; m < 3 + tier; m++)
                        DrawIn(
                            frontLight,
                            from + UnityEngine.Random.insideUnitCircle * 30f,
                            center,
                            colors[n],
                            0.35f,
                            0.05f * m,
                            7f
                        );
                }
                yield return Wait(0.35f);
                Radiance(backLight, center, color, 360f, 0.5f, 0.03f, 0.1f, 0.5f);
                Pool(feet, color, 300f, 0.1f, 0.4f);
            }
        }

        /// <summary>
        /// A thin upright line of light sweeping from <paramref name="from"/> to
        /// <paramref name="to"/> (on the floor) over <paramref name="seconds"/>: a white core in a
        /// wider glow of its colour, and the floor lit where it passes.
        /// </summary>
        private void Scan(Vector2 from, Vector2 to, Color color, float seconds)
        {
            if (frontLight == null)
                return;
            const float tall = 520f;
            Shot Board(float width, Color tint)
            {
                var shot = Spawn(
                    frontLight,
                    BattleVfxShape.Beam,
                    from,
                    new Vector2(tall, width),
                    Color.clear,
                    new Vector2(0f, 0.5f)
                );
                shot.Image.material = RichMaterialOf(BattleVfxShape.Beam);
                shot.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
                return shot;
            }
            var glow = Board(40f, color);
            var core = Board(6f, Color.white);
            var pool =
                backLight != null
                    ? Spawn(
                        backLight,
                        BattleVfxShape.Glow,
                        from,
                        new Vector2(220f, 220f),
                        Color.clear
                    )
                    : null;
            if (pool != null)
                pool.Holder.localScale = new Vector3(1f, FloorTilt, 1f);
            void Sweep(Shot shot, Color tint, float peak, float bright)
            {
                Animate(
                    shot,
                    seconds + 0.08f,
                    (s, k) =>
                    {
                        float t = k * (seconds + 0.08f);
                        float along = Mathf.Clamp01(t / seconds);
                        s.Holder.anchoredPosition = Vector2.Lerp(from, to, EaseInOut(along));
                        float alpha =
                            peak
                            * Mathf.Min(1f, t / 0.05f)
                            * (t > seconds ? 1f - (t - seconds) / 0.08f : 1f);
                        Show(s, new Color(tint.r, tint.g, tint.b, alpha), 1f, 0f, bright);
                    }
                );
            }
            Sweep(glow, color, 0.6f, ErodeBright);
            Sweep(core, Color.white, 0.9f, CoreBright);
            if (pool != null)
                Sweep(pool, color, 0.6f, 1f);
        }

        // The first hit on each enemy, in order.
        private static List<int> DistinctEnemies(IReadOnlyList<BattleSkillHit> hits)
        {
            var seen = new HashSet<RectTransform>();
            var list = new List<int>();
            for (int i = 0; i < hits.Count; i++)
                if (!hits[i].Ally && hits[i].Area != null && seen.Add(hits[i].Area))
                    list.Add(i);
            return list;
        }

        private static float EaseInOut(float k) => k * k * (3f - 2f * k);

        // A sight closing on what the scan has found, and its weak point glinting.
        private void Sight(Vector2 at, Color color)
        {
            Bloom(
                frontLight,
                BattleVfxShape.Reticle,
                at,
                new Vector2(240f, 240f),
                color,
                0.5f,
                0.6f,
                ErodeBright
            );
            Twinkle(
                at + new Vector2(18f, 26f),
                Color.Lerp(color, Color.white, 0.6f),
                130f,
                0.3f,
                0.2f,
                1f
            );
            Illuminate(at, color, 0.45f, 240f, 0.05f, 0.25f, spill: false);
        }

        /// <summary>
        /// 速射, quick shots from the bow (doc/features/skill-evolution.md#ツリーごとの演出): the string is
        /// loosed again and again, each arrow flat and fast with a thin white streak and a fainter
        /// one behind it, the bow kicking a little dust at the user's feet; each lands with a
        /// small spray of sparks and grit. 連・乱れ撃ち loose more (乱れ撃ち fanning over random
        /// enemies); 継・流転 draw a streak of light back to the user after the last (a card
        /// drawn); 穿・貫通 punch through, the streak running on out of the far side with chips of
        /// armour, and the guard laid down.
        /// </summary>
        private IEnumerator QuickShotVfx(
            int stage,
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land,
            Color color,
            float scale
        )
        {
            int branch = BranchOf(stage);
            int tier = TierOfStage(stage);
            var bow = Center(FrontLayer, caster) + new Vector2(50f, 24f);
            var feet = Feet(FrontLayer, caster);
            var landed = new HashSet<int>();
            Twinkle(bow, Color.Lerp(color, Color.white, 0.5f), 120f, 0.2f, 0f, 1f);
            yield return Wait(0.08f);
            foreach (int wave in MainWaves("QuickShot", stage))
            foreach (int index in Wave(hits, wave))
            {
                if (hits[index].Ally)
                    continue;
                var area = hits[index].Area;
                var at = Center(FrontLayer, area) + UnityEngine.Random.insideUnitCircle * 30f;
                // 弦の反動：手元の小さな光と、足元の塵。
                Spray(
                    frontLight,
                    bow,
                    new Burst
                    {
                        Shape = BattleVfxShape.Streak,
                        Bright = CoreBright,
                        From = Color.white,
                        To = new Color(color.r, color.g, color.b, 0f),
                        Speed = new Vector2(300f, 600f),
                        Direction = 180f,
                        Spread = 50f,
                        Life = new Vector2(0.05f, 0.1f),
                        Size = new Vector2(3f, 5f),
                        Stretch = 0.05f,
                        Drag = 6f,
                    },
                    3
                );
                DustKick(feet, Vector2.left, 90f);
                yield return Shoot(
                    BattleVfxShape.Arrow,
                    bow,
                    at,
                    new Vector2(180f, 46f) * scale,
                    new Vector2(0.9f, 0.5f),
                    0.07f,
                    branch == 0 && stage == 2 ? UnityEngine.Random.Range(-60f, 60f) : 8f,
                    color,
                    0f,
                    new Burst
                    {
                        Shape = BattleVfxShape.Streak,
                        Bright = ErodeBright,
                        From = new Color(color.r, color.g, color.b, 0.5f),
                        To = new Color(color.r, color.g, color.b, 0f),
                        Speed = new Vector2(10f, 40f),
                        Spread = 10f,
                        Life = new Vector2(0.08f, 0.14f),
                        Size = new Vector2(5f, 8f),
                        Area = 4f,
                    }
                );
                var weight = land(index);
                landed.Add(index);
                Sparks(at, color, 8 + 2 * tier, 0.8f, (at - bow).normalized);
                Star(at, Color.white, 130f + 30f * tier, 0.12f);
                Illuminate(at, color, 0.4f, 240f, 0.03f, 0.18f, spill: false);
                HitStop(Weighted(0.03f, weight));
                Shake(Weighted(0.12f, weight), (at - bow).normalized);
                if (weight != BattleHitWeight.Normal)
                    Cross(at, color, weight);
                if (branch == 2)
                {
                    // 穿：矢が突き抜け、筋が向こう側へ走り、鎧のかけらが飛ぶ。
                    var through = at + (at - bow).normalized * 320f;
                    Line(
                        frontLight,
                        at,
                        through,
                        Color.Lerp(color, Color.white, 0.4f),
                        14f,
                        0.16f,
                        0.3f
                    );
                    Debris(at, new Color(0.6f, 0.64f, 0.72f), 6 + 4 * tier, (at - bow).normalized);
                }
                yield return Wait(stage == 2 ? 0.03f : 0.06f);
            }
            for (int i = 0; i < hits.Count; i++)
                if (!landed.Contains(i))
                    land(i);
            if (branch == 1)
            {
                // 継・流転：最後の矢のあと、光の筋が弧を描いて使い手へ戻る（カードを引く）。
                foreach (int index in Wave(hits, 0))
                {
                    var c = Center(FrontLayer, hits[index].Area);
                    for (int n = 0; n < 5; n++)
                        DrawIn(
                            frontLight,
                            c + UnityEngine.Random.insideUnitCircle * 40f,
                            bow,
                            color,
                            0.28f,
                            0.02f * n,
                            5f
                        );
                    break;
                }
                yield return Wait(0.28f);
                Twinkle(bow + new Vector2(0f, 40f), color, 160f, 0.3f, 0f, -1f);
                Radiance(backLight, bow, color, 260f, 0.4f, 0.03f, 0.1f, 0.35f);
            }
            if (branch == 2)
                foreach (int index in Wave(hits, 0))
                {
                    if (hits[index].Ally)
                        continue;
                    Bloom(
                        frontLight,
                        BattleVfxShape.Chevron,
                        Center(FrontLayer, hits[index].Area) + new Vector2(0f, 130f),
                        new Vector2(110f, 150f),
                        Violet,
                        0.7f,
                        0.6f,
                        ErodeBright,
                        angle: 180f
                    );
                    break;
                }
        }

        /// <summary>
        /// 鼓舞, a war cry (doc/features/skill-evolution.md#ツリーごとの演出): the user's shout goes out
        /// as rings of pressure, thin and fading as they spread, the dust at its feet blown out,
        /// and each ally is roused in turn as it reaches them: thin lines of light rising off the
        /// body, a chevron rising and a crest of light for the block. 突撃・総攻撃 carry the shout
        /// into the enemies as a rush of wind and dust that strikes each (総攻撃 harder);
        /// 号令・凱歌 send motes back to the user (a card drawn; 凱歌 a burst of light over the
        /// party); 堅陣・鉄壁 raise a dome over the party (鉄壁 stamped into the ground).
        /// </summary>
        private IEnumerator RallyVfx(
            int stage,
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land,
            Color color,
            float scale
        )
        {
            int branch = BranchOf(stage);
            int tier = TierOfStage(stage);
            var center = Center(FrontLayer, caster);
            var feet = Feet(FrontLayer, caster);
            // 叫び：圧の輪が次々に広がり、だんだん薄く細くなる。足元の塵が吹き飛ぶ。
            for (int n = 0; n < 3; n++)
                StartCoroutine(
                    After(
                        0.07f * n,
                        () =>
                            Ring(
                                frontLight,
                                center,
                                Color.Lerp(color, Color.white, 0.3f),
                                640f + 160f * tier,
                                0.45f,
                                floor: false
                            )
                    )
                );
            DustKick(feet, Vector2.right, 220f);
            DustKick(feet, Vector2.left, 220f);
            Shake(0.25f, Vector2.zero);
            Radiance(backLight, center, color, 420f, 0.45f, 0.03f, 0.15f, 0.4f);
            Illuminate(center, color, 0.6f, 320f, 0.05f, 0.3f, spill: false);
            yield return Wait(0.14f);

            var landed = new HashSet<int>();
            if (branch == 0)
            {
                // 突撃・総攻撃：叫びが風と塵の突進になって敵の列を打つ。
                foreach (int index in Wave(hits, 0))
                {
                    if (hits[index].Ally)
                        continue;
                    var area = hits[index].Area;
                    var at = Center(FrontLayer, area);
                    Spray(
                        frontLight,
                        Vector2.Lerp(center, at, 0.4f),
                        new Burst
                        {
                            Shape = BattleVfxShape.Streak,
                            Bright = CoreBright,
                            From = Color.white,
                            To = new Color(color.r, color.g, color.b, 0f),
                            Speed = new Vector2(1800f, 2600f),
                            Direction = Angle(at - center),
                            Spread = 6f,
                            Life = new Vector2(0.08f, 0.16f),
                            Size = new Vector2(5f, 9f),
                            Stretch = 0.06f,
                            Area = 80f,
                        },
                        8 + 4 * tier
                    );
                }
                yield return Wait(0.08f);
                foreach (int index in Wave(hits, 0))
                {
                    if (hits[index].Ally)
                        continue;
                    var area = hits[index].Area;
                    var at = Center(FrontLayer, area);
                    var weight = land(index);
                    landed.Add(index);
                    Impact(
                        area,
                        at,
                        color,
                        weight,
                        (0.6f + 0.2f * tier) * scale,
                        Vector2.right,
                        flashScreen: false
                    );
                    DustKick(Feet(FrontLayer, area), Vector2.right, 200f);
                }
                yield return Wait(0.08f);
            }
            // 味方：輪が届いた順に、体から細い光の線が昇り、矢印と盾の紋章が立つ。
            var roused = new HashSet<RectTransform>();
            for (int i = 0; i < hits.Count; i++)
            {
                if (landed.Contains(i))
                    continue;
                if (!hits[i].Ally)
                {
                    land(i);
                    continue;
                }
                var area = hits[i].Area;
                var at = Center(FrontLayer, area);
                if (!roused.Add(area))
                {
                    land(i);
                    continue;
                }
                float when = Mathf.Abs(at.x - center.x) / 2400f;
                if (when > 0.01f)
                    yield return Wait(when);
                land(i);
                Uplift(area, color, 0, false);
                Bloom(
                    frontLight,
                    BattleVfxShape.Chevron,
                    at + new Vector2(0f, 60f),
                    new Vector2(100f, 140f),
                    color,
                    0.6f,
                    0.6f,
                    ErodeBright
                );
                if (branch != 0)
                    Bloom(
                        frontLight,
                        BattleVfxShape.Crest,
                        at + new Vector2(0f, 170f),
                        new Vector2(90f, 115f),
                        branch == 2 ? ColorOf(BattleSkillVfxKind.Guard) : color,
                        0.6f,
                        0.4f,
                        ErodeBright,
                        delay: 0.1f
                    );
            }
            if (branch == 1)
            {
                foreach (var hit in hits)
                {
                    if (!hit.Ally || hit.Area == caster)
                        continue;
                    var c = Center(FrontLayer, hit.Area);
                    for (int n = 0; n < 3; n++)
                        DrawIn(
                            frontLight,
                            c + UnityEngine.Random.insideUnitCircle * 40f,
                            center,
                            color,
                            0.3f,
                            0.03f * n,
                            5f
                        );
                }
                yield return Wait(0.3f);
                Twinkle(center + new Vector2(20f, 60f), color, 180f, 0.35f, 0f, 1f);
                if (stage == 4)
                    Spray(
                        frontLight,
                        MidFeet(hits, 0) + Vector2.up * 260f,
                        new Burst
                        {
                            Shape = BattleVfxShape.Sparkle,
                            Bright = CoreBright,
                            From = Color.white,
                            To = new Color(color.r, color.g, color.b, 0f),
                            Speed = new Vector2(100f, 360f),
                            Direction = 90f,
                            Spread = 160f,
                            Life = new Vector2(0.6f, 1.1f),
                            Size = new Vector2(14f, 26f),
                            Gravity = 200f,
                            Drag = 2f,
                            Spin = 200f,
                            Area = 200f,
                        },
                        24
                    );
            }
            if (branch == 2)
            {
                var allies = new List<RectTransform>();
                foreach (var hit in hits)
                    if (hit.Ally && hit.Area != null && !allies.Contains(hit.Area))
                        allies.Add(hit.Area);
                if (allies.Count > 0)
                {
                    float left = float.MaxValue;
                    float right = float.MinValue;
                    float floorY = 0f;
                    foreach (var area in allies)
                    {
                        var f = Feet(FrontLayer, area);
                        left = Mathf.Min(left, f.x);
                        right = Mathf.Max(right, f.x);
                        floorY = f.y;
                    }
                    var basePoint = new Vector2((left + right) / 2f, floorY);
                    float width = Mathf.Max(260f, right - left + 300f);
                    if (stage == 6)
                        StartCoroutine(Tremor(caster, 0.3f));
                    Dome(backLight, basePoint, width, 0.9f, breaks: stage == 6);
                    Dome(frontLight, basePoint, width, 0.3f, breaks: false);
                    Pool(basePoint, ColorOf(BattleSkillVfxKind.Guard), width, 0.2f, 0.5f);
                }
            }
            yield return Wait(0.2f);
        }

        /// <summary>
        /// 裁きの光, light from the heavens (doc/features/skill-evolution.md#ツリーごとの演出): shafts
        /// of light come down through the air onto the enemies, the dust in them glinting, each
        /// landing with a white flash and thin rays, and then the light spills softly over the
        /// party, rising off them. 断罪・天罰 bring one wide shaft down on one enemy with a long
        /// white line at its heart (天罰 harder); 浄・聖浄 lift dark wisps off the allies that
        /// turn to light as they rise (聖浄 also breaking a shell of light off the enemies);
        /// 陽光・天照 warm everything under a sun-like glow above and leave motes drifting longer.
        /// </summary>
        private IEnumerator JudgmentVfx(
            int stage,
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land,
            Color color,
            float scale
        )
        {
            int branch = BranchOf(stage);
            int tier = TierOfStage(stage);
            DimTo(SpellDim, 0.15f);
            float top = FrontLayer.rect.yMax;
            if (branch == 2)
            {
                // 陽光・天照：上に太陽のような暖かい光がふくらむ。
                var sun = new Vector2(MidFeet(hits, 0).x, top - 80f);
                Radiance(backLight, sun, color, 1400f, 0.45f, 0.25f, 0.6f, 0.8f);
                yield return Wait(0.15f);
            }
            var landed = new HashSet<int>();
            foreach (int i in DistinctEnemies(hits))
            {
                var area = hits[i].Area;
                var feet = Feet(FrontLayer, area);
                var at = Center(FrontLayer, area);
                bool single = branch == 0;
                // 光の柱：塵で見える柱が上から降り、芯に白い線が通る。
                LightColumn(
                    feet,
                    new Vector2(single ? 560f : 320f, 1400f),
                    color,
                    0.75f,
                    down: true
                );
                if (single)
                    Shaft(new Vector2(at.x, top + 40f), feet, color, 24f + 10f * tier, 0.1f, 0.45f);
                DustInLight(at, color, 1f);
            }
            yield return Wait(0.1f);
            var flashed = new HashSet<RectTransform>();
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].Ally || hits[i].Area == null)
                    continue;
                var area = hits[i].Area;
                var at = Center(FrontLayer, area);
                var weight = land(i);
                landed.Add(i);
                // 同じ敵への2つ目の波（聖浄の強化を消す波と打つ波）は、光を重ねない。
                if (!flashed.Add(area))
                    continue;
                HotFlash(at, Color.white, (branch == 0 ? 300f : 220f) * scale);
                Rays(at, color, branch == 0 ? 16 + 6 * tier : 10, 0.9f);
                Impact(
                    area,
                    at,
                    color,
                    weight,
                    (branch == 0 ? 0.9f : 0.6f) * scale,
                    Vector2.down,
                    flashScreen: branch == 0 && tier == 2,
                    rays: false
                );
                if (stage == 4)
                    // 聖浄：敵をおおう強化の殻が光の破片になって割れる。
                    Spray(
                        frontLight,
                        at,
                        new Burst
                        {
                            Shape = BattleVfxShape.Shard,
                            Bright = CoreBright,
                            From = Color.Lerp(color, Color.white, 0.5f),
                            To = new Color(color.r, color.g, color.b, 0f),
                            Speed = new Vector2(300f, 800f),
                            Spread = 360f,
                            Life = new Vector2(0.3f, 0.6f),
                            Size = new Vector2(14f, 28f),
                            Spin = 600f,
                            Gravity = 900f,
                            Drag = 2f,
                            Area = 50f,
                        },
                        14
                    );
            }
            yield return Wait(0.12f);
            // 味方：光がやわらかく降りかかり、体から昇る（回復）。
            for (int i = 0; i < hits.Count; i++)
            {
                if (landed.Contains(i))
                    continue;
                var area = hits[i].Area;
                land(i);
                if (!hits[i].Ally || area == null)
                    continue;
                var feet = Feet(FrontLayer, area);
                var at = Center(FrontLayer, area);
                LightColumn(
                    feet,
                    new Vector2(240f, 1000f),
                    Color.Lerp(color, Color.white, 0.3f),
                    0.8f,
                    down: true
                );
                Uplift(area, branch == 2 ? Leafy : color, 0, false);
                if (branch == 1)
                {
                    // 浄：体から暗いもやが剥がれて昇り、光に変わって消える。
                    Spray(
                        frontSmoke,
                        at,
                        new Burst
                        {
                            Shape = BattleVfxShape.Smoke,
                            Bright = 0.3f,
                            From = new Color(0.25f, 0.18f, 0.3f, 0.5f),
                            To = new Color(0.25f, 0.18f, 0.3f, 0.5f),
                            Speed = new Vector2(60f, 140f),
                            Direction = 90f,
                            Spread = 50f,
                            Life = new Vector2(0.5f, 0.8f),
                            Size = new Vector2(50f, 90f),
                            Grow = 0.4f,
                            Shrink = 1.2f,
                            Gravity = -80f,
                            Hold = 0.25f,
                            Area = 50f,
                        },
                        5
                    );
                    Motes(at + Vector2.up * 90f, Color.white, 8, 0.3f, 0.5f);
                }
                if (branch == 2)
                    Rise(feet, Leafy, 8 + 6 * tier);
            }
        }

        // --- The steps' layers --------------------------------------------------------------

        /// <summary>
        /// The wind-up a step earns: none for the first stage beyond its own effect, for a first
        /// step one chosen by the stage's <paramref name="card"/> (<see cref="FirstStepWindUp"/>),
        /// and for a last step the night falling, a charge in three steps in the stage's colour
        /// (<see cref="Tiered"/>), and a twinkle and a hot flash as it is let go
        /// (doc/features/skill-windups.md#技への割り当て仮設定).
        /// </summary>
        private IEnumerator WindUp(
            RectTransform caster,
            Color color,
            int tier,
            CardSkill card = null
        )
        {
            if (tier == 0)
                yield break;
            if (tier == 1)
            {
                yield return FirstStepWindUp(caster, color, card);
                yield break;
            }
            DimTo(NightDim, 0.25f);
            var center = Center(FrontLayer, caster);
            Shake(0.06f, Vector2.zero);
            yield return Tiered(caster, 0.22f, color);
            Twinkle(center + new Vector2(30f, 40f), color, 260f, 0.4f, 0f, 1f);
            HotFlash(center, color, 220f);
        }

        /// <summary>
        /// The landing of each blow with the step's layers on top of the tree's own, split as a
        /// real burst of energy is (doc/features/skill-evolution.md#段階の層): behind the target the
        /// air it lights (the longest), a pool of light on the floor, a white star (the shortest),
        /// sparks thrown out and falling, dust driven out along the floor, and motes rising late.
        /// A last step also hits once like a finisher (flash, hit stop, slow-down, punch-in, thin
        /// rays of light shot out, a wider glow) and bursts again, weaker and wider, a moment later.
        /// Allies (healed, guarded) get light rising from the floor instead of a blow.
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
            // 何度も当たる技は、1回ずつの光を控えめにする（重なって標的を白く隠さない）。
            float share = 1f / Mathf.Sqrt(Mathf.Max(1, main.Count));
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
                bool finale = tier == 2 && hit.Wave == lastWave && !climaxed;
                if (hit.Ally)
                {
                    Uplift(hit.Area, color, tier, finale);
                    climaxed |= finale;
                    return weight;
                }
                var at = Center(FrontLayer, hit.Area);
                var feet = Feet(FrontLayer, hit.Area);
                // にじみ：標的の奥の光った空気。最初に来て最後まで残る。
                Radiance(
                    backLight,
                    at,
                    color,
                    360f + 160f * tier,
                    (0.45f + 0.1f * tier) * share,
                    0.03f,
                    0.05f,
                    0.35f + 0.15f * tier
                );
                Pool(
                    feet,
                    color,
                    (320f + 140f * tier) * Mathf.Sqrt(share),
                    0.05f,
                    0.3f + 0.1f * tier
                );
                // 芯：白い星。いちばん短い。
                Star(at, Color.white, 170f + 70f * tier, 0.14f);
                // 残像：段階の色の火花が外へ飛んで落ちる。
                Sparks(at, color, 10 + 10 * tier, 0.9f + 0.25f * tier, Vector2.up);
                Shockwave(feet, 380f + 100f * tier, 0.5f + 0.25f * tier);
                // 余韻：遅れて昇る光の粒。
                Motes(at, color, 4 + 6 * tier, 0.08f, 0.3f);
                Illuminate(
                    at,
                    color,
                    0.55f + 0.2f * tier,
                    360f + 120f * tier,
                    0.05f,
                    0.3f,
                    spill: false
                );
                if (finale)
                {
                    climaxed = true;
                    ScreenFlash(Color.Lerp(color, Color.white, 0.9f), MaxFlash);
                    HitStop(0.14f);
                    SlowMotion(0.35f, 0.3f);
                    Punch(at, 0.05f);
                    Shake(0.9f, Vector2.down);
                    Shockwave(feet, 960f, 1f);
                    Rays(at, color, 22, 1f);
                    Radiance(backLight, at, color, 800f, 0.4f, 0.02f, 0.1f, 0.7f);
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

        /// <summary>
        /// A step's layers on an ally it heals or guards: no blow, a light that comes up gently
        /// from the floor (doc/features/skill-evolution.md#段階の層): a pool on the floor, a soft
        /// glow round the body, thin lines of light rising past it and gone above, a twinkle, and
        /// motes drifting up last. A last step's finale also lifts the whole stage once.
        /// </summary>
        private void Uplift(RectTransform area, Color color, int tier, bool finale)
        {
            if (area == null)
                return;
            var at = Center(FrontLayer, area);
            var feet = Feet(FrontLayer, area);
            var (min, max) = Bounds(FrontLayer, area);
            float width = Mathf.Max(80f, max.x - min.x);
            float height = Mathf.Max(120f, max.y - min.y);
            Pool(feet, color, 300f + 120f * tier, 0.12f, 0.5f);
            Radiance(backLight, at, color, 300f + 100f * tier, 0.28f, 0.12f, 0.1f, 0.5f);
            var hot = Color.Lerp(color, Color.white, 0.55f);
            int lines = 8 + 8 * tier;
            for (int i = 0; i < lines; i++)
            {
                bool front = i % 4 == 0;
                RiseLine(
                    front ? frontLight : backLight,
                    OnBody(min, width, height, 0.4f),
                    new Color(hot.r, hot.g, hot.b, front ? 0.55f : 0.9f),
                    color,
                    new Vector2(3f, 5f),
                    new Vector2(380f, 700f),
                    new Vector2(0.25f, 0.4f),
                    0.25f * UnityEngine.Random.value,
                    CoreBright
                );
            }
            Twinkle(at + new Vector2(-30f, 50f), color, 140f + 60f * tier, 0.45f, 0.08f, 1f);
            Rise(feet, color, 8 + 6 * tier);
            if (!finale)
                return;
            ScreenFlash(Color.Lerp(color, Color.white, 0.85f), MaxFlash * 0.6f);
            Illuminate(at, color, 0.8f, 520f, 0.25f, 0.6f);
            Radiance(backLight, at, color, 900f, 0.4f, 0.1f, 0.15f, 0.7f);
        }

        /// <summary>
        /// A second burst a beat after a last step's climax, as a secondary explosion is: weaker
        /// and wider than the first, in the stage's colour rather than white: a wide glow behind, long streaks, a pool of light and a jolt.
        /// </summary>
        private IEnumerator Aftershock(RectTransform target, Color color)
        {
            yield return Wait(0.16f);
            if (target == null)
                yield break;
            var at = Center(FrontLayer, target);
            var feet = Feet(FrontLayer, target);
            HotFlash(at, color, 300f);
            Radiance(backLight, at, color, 700f, 0.35f, 0.04f, 0.05f, 0.5f);
            Sparks(at, color, 30, 1.4f, Vector2.up);
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
                    Size = new Vector2(8f, 14f),
                    Stretch = 0.06f,
                    Area = 30f,
                },
                16
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

        /// <summary>
        /// Motes of light rising slowly from the floor at <paramref name="feet"/>: most small and
        /// quick to go out, a few larger ones drifting longer, so they never rise as one sheet.
        /// </summary>
        private void Rise(Vector2 feet, Color color, int count)
        {
            var mote = new Burst
            {
                Shape = BattleVfxShape.Mote,
                Bright = CoreBright,
                From = Color.white,
                To = new Color(color.r, color.g, color.b, 0f),
                Speed = new Vector2(60f, 160f),
                Direction = 90f,
                Spread = 40f,
                Life = new Vector2(0.5f, 0.9f),
                Size = new Vector2(10f, 18f),
                Shrink = 0.3f,
                Gravity = -120f,
                Drag = 1.2f,
                Area = 90f,
                Delay = new Vector2(0f, 0.25f),
            };
            Spray(frontLight, feet + Vector2.up * 30f, mote, count - count / 3);
            mote.Shape = BattleVfxShape.Sparkle;
            mote.Speed = new Vector2(30f, 80f);
            mote.Life = new Vector2(0.9f, 1.5f);
            mote.Size = new Vector2(18f, 30f);
            mote.Gravity = -60f;
            mote.Delay = new Vector2(0.1f, 0.4f);
            Spray(frontLight, feet + Vector2.up * 50f, mote, count / 3);
        }

        // --- Layers the trees share ------------------------------------------------------------

        /// <summary>
        /// A soft light that comes up over <paramref name="attack"/> s, holds, and falls away
        /// slowly over <paramref name="fade"/> s (F3), swelling a little as it goes: the air or
        /// the floor lit round a burst. Laid on the floor when <paramref name="floor"/>.
        /// </summary>
        private void Radiance(
            RectTransform layer,
            Vector2 at,
            Color color,
            float size,
            float peak,
            float attack,
            float hold,
            float fade,
            bool floor = false,
            float delay = 0f
        )
        {
            if (layer == null)
                return;
            var shot = Spawn(layer, BattleVfxShape.Glow, at, new Vector2(size, size), Color.clear);
            float life = attack + hold + fade;
            float total = delay + life;
            Animate(
                shot,
                total,
                (s, k) =>
                {
                    float t = k * total - delay;
                    if (t < 0f)
                    {
                        Show(s, Color.clear, 0f, 0f);
                        return;
                    }
                    float up = attack > 0f ? Mathf.SmoothStep(0f, 1f, t / attack) : 1f;
                    float down =
                        t < attack + hold ? 1f : Sq(1f - Mathf.Clamp01((t - attack - hold) / fade));
                    float grow = 0.75f + 0.3f * EaseOut(Mathf.Clamp01(t / life));
                    s.Holder.localScale = new Vector3(grow, grow * (floor ? FloorTilt : 1f), 1f);
                    Show(s, new Color(color.r, color.g, color.b, peak * up * down), k, 0f);
                }
            );
        }

        /// <summary>
        /// Thin rays of light shot straight out from <paramref name="at"/> all round, fast and
        /// gone at once: the light of a release seen as lines, not a disc.
        /// </summary>
        private void Rays(Vector2 at, Color color, int count, float force) =>
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Streak,
                    Bright = CoreBright,
                    From = Color.white,
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(1600f, 2600f) * force,
                    Spread = 360f,
                    Life = new Vector2(0.1f, 0.2f),
                    Size = new Vector2(4f, 7f),
                    Shrink = 0.5f,
                    Stretch = 0.09f,
                    Drag = 3f,
                    Area = 24f,
                },
                count
            );

        /// <summary>
        /// Motes of light lifting off <paramref name="at"/> after <paramref name="from"/> to
        /// <paramref name="to"/> s, slow and few: what is left of a burst once its light is spent.
        /// </summary>
        private void Motes(Vector2 at, Color color, int count, float from, float to) =>
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Spark,
                    Bright = CoreBright,
                    From = Color.Lerp(color, Color.white, 0.6f),
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(30f, 120f),
                    Direction = 90f,
                    Spread = 140f,
                    Life = new Vector2(0.5f, 0.9f),
                    Size = new Vector2(7f, 12f),
                    Shrink = 0.4f,
                    Gravity = -90f,
                    Drag = 1.5f,
                    Area = 70f,
                    Delay = new Vector2(from, to),
                },
                count
            );

        // --- The trees -------------------------------------------------------------------------

        /// <summary>
        /// マナバースト, a sphere of compressed magic (doc/features/skill-evolution.md#マナバーストの演出):
        /// an orb of four layers is flung over the enemies (<see cref="ManaOrb"/>), is squeezed for
        /// an instant as light is drawn into it, and bursts (<see cref="ManaRelease"/>): its spines
        /// shoot out and are eaten away, a small white core and thin rays, the air lit
        /// violet behind and motes of spent magic sinking slowly. 烈・極 burst wider and whiter, 極
        /// venting a column of light upward; 散・嵐 split it into small orbs that rain on random
        /// enemies (嵐 from a swirl round the user); 環・還流 draw the spent magic back to the user
        /// in a golden stream.
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
            var held = new List<Shot>();
            DimTo(SpellDim, 0.15f);
            if (branch == 1)
            {
                if (stage == 4)
                {
                    // 嵐：使い手のまわりに魔力が渦を巻いてから、散って降る。
                    Spiral(Feet(FrontLayer, caster), color, 18, 0.35f);
                    yield return Wait(0.2f);
                }
                foreach (int wave in MainWaves("ManaBurst", stage))
                foreach (int index in Wave(hits, wave))
                {
                    var at = Center(FrontLayer, hits[index].Area);
                    var start =
                        stage == 4 ? from + UnityEngine.Random.insideUnitCircle * 160f : from;
                    held.Clear();
                    yield return ManaOrb(
                        start,
                        at,
                        70f * scale,
                        stage == 4 ? 0.14f : 0.18f,
                        stage == 4 ? UnityEngine.Random.Range(-160f, 160f) : 70f,
                        color,
                        held
                    );
                    land(index);
                    ManaRelease(at, color, 0.45f * scale, held, Feet(FrontLayer, hits[index].Area));
                    Shockwave(Feet(FrontLayer, hits[index].Area), 300f, 0.4f);
                }
                yield break;
            }
            var mid = MidFeet(hits, 0) + Vector2.up * 140f;
            yield return ManaOrb(from, mid, 130f * scale, 0.3f, 90f, color, held);
            // 圧縮：一瞬縮み、まわりの光が吸い込まれる。
            Converge(frontLight, mid, color, 620f * scale, 0.12f, 0f, floor: false);
            Gather(mid, Color.Lerp(color, Color.white, 0.4f), 10 + 6 * branch, 300f * scale, 0.12f);
            yield return Squeeze(held, 0.12f);
            ManaRelease(mid, color, scale, held, MidFeet(hits, 0));
            if (stage == 2)
                LightColumn(MidFeet(hits, 0), new Vector2(420f, 1300f), color, 0.7f, down: false);
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
                // 使った魔力が金の流れになって使い手へ戻り、輪になって収まる。
                for (int i = 0; i < 16; i++)
                    DrawIn(
                        frontLight,
                        mid + UnityEngine.Random.insideUnitCircle * 120f,
                        home + UnityEngine.Random.insideUnitCircle * 30f,
                        Gold,
                        0.32f,
                        0.012f * i,
                        UnityEngine.Random.Range(5f, 9f)
                    );
                Converge(frontLight, home, Gold, 360f, 0.3f, 0.15f, floor: false);
                yield return Wait(0.42f);
                Radiance(backLight, home, Gold, 360f, 0.5f, 0.04f, 0.1f, 0.5f);
                Pool(Feet(FrontLayer, caster), Gold, 300f, 0.1f, 0.5f);
                Twinkle(home + new Vector2(20f, 40f), Gold, 200f, 0.45f, 0f, -1f);
                Illuminate(home, Gold, 0.7f, 300f, 0.1f, 0.4f, spill: false);
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
        /// A sphere of compressed magic flown on an arc, as four layers moving as one but each
        /// drawn its own way: the air it lights, wide and faint (にじみ); spines of light turning
        /// (本体); a small white-hot core (芯); and motes and crackles it sheds, which linger as
        /// its afterimage (残像). It swells out of the hand and throbs as it flies; on arrival its
        /// glow, shell and core are left in <paramref name="held"/> for the burst.
        /// </summary>
        private IEnumerator ManaOrb(
            Vector2 from,
            Vector2 to,
            float size,
            float seconds,
            float lift,
            Color color,
            List<Shot> held
        )
        {
            var glow = Spawn(
                frontLight,
                BattleVfxShape.Glow,
                from,
                Vector2.one * size * 2.6f,
                Color.clear
            );
            var shell = Spawn(
                frontLight,
                BattleVfxShape.Star,
                from,
                Vector2.one * size,
                Color.clear
            );
            var core = Spawn(
                frontLight,
                BattleVfxShape.Glow,
                from,
                Vector2.one * size * 0.55f,
                Color.clear
            );
            held.Add(glow);
            held.Add(shell);
            held.Add(core);
            float shed = 0f;
            var last = from;
            var hot = Color.Lerp(color, Color.white, 0.55f);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                float ease = k * k * (1.4f - 0.4f * k);
                var position =
                    Vector2.Lerp(from, to, ease) + Vector2.up * (Mathf.Sin(k * Mathf.PI) * lift);
                float swell = 0.5f + 0.5f * EaseOut(Mathf.Min(1f, k * 2.5f));
                float throb = 1f + 0.07f * Mathf.Sin(t * 41f);
                foreach (var shot in held)
                {
                    shot.Holder.anchoredPosition = position;
                    shot.Holder.localScale = Vector3.one * (swell * throb);
                }
                shell.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, -t * 220f);
                Show(glow, new Color(color.r, color.g, color.b, 0.4f), 0f, 0f);
                // 圧縮された光の棘が回りながら震える（輪郭を持つ泡にしない）。
                Show(
                    shell,
                    Color.Lerp(color, Color.white, 0.25f),
                    0.2f,
                    0.35f + 0.15f * Mathf.Sin(t * 53f),
                    CoreBright
                );
                Show(core, hot, 0f, 0f, CoreBright * 1.3f);
                shed += Time.deltaTime;
                while (shed > 0.02f)
                {
                    shed -= 0.02f;
                    var away = Angle(last - position);
                    Spray(
                        frontLight,
                        position,
                        new Burst
                        {
                            Shape = BattleVfxShape.Spark,
                            Bright = CoreBright,
                            From = hot,
                            To = new Color(color.r, color.g, color.b, 0f),
                            Speed = new Vector2(20f, 90f),
                            Direction = away,
                            Spread = 90f,
                            Life = new Vector2(0.25f, 0.55f),
                            Size = new Vector2(6f, 12f),
                            Shrink = 0.3f,
                            Drag = 2.5f,
                            Area = size * 0.3f,
                        },
                        2
                    );
                    if (UnityEngine.Random.value < 0.35f)
                        // 殻の上を走る小さな放電。
                        Spray(
                            frontLight,
                            position,
                            new Burst
                            {
                                Shape = BattleVfxShape.Streak,
                                Bright = CoreBright,
                                From = Color.white,
                                To = new Color(color.r, color.g, color.b, 0f),
                                Speed = new Vector2(200f, 500f),
                                Spread = 360f,
                                Life = new Vector2(0.04f, 0.09f),
                                Size = new Vector2(3f, 5f),
                                Stretch = 0.05f,
                                Drag = 6f,
                                Area = size * 0.4f,
                            },
                            1
                        );
                }
                last = position;
                yield return null;
            }
        }

        // The orb squeezed for an instant before it bursts: the shell draws in and the core brightens.
        private static IEnumerator Squeeze(List<Shot> held, float seconds)
        {
            if (held.Count < 3)
                yield break;
            var scale = held[0].Holder.localScale.x;
            var core = held[2].Image.color;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                float squeeze = Mathf.Lerp(scale, scale * 0.6f, EaseIn(k));
                foreach (var shot in held)
                    shot.Holder.localScale = Vector3.one * squeeze;
                Show(held[2], Color.Lerp(core, Color.white, k), 0f, 0f, CoreBright * (1.3f + k));
                yield return null;
            }
        }

        /// <summary>
        /// The orb in <paramref name="held"/> bursts at <paramref name="at"/>, its parts each let go
        /// at its own pace (F3): the small white core flashes and is gone first; thin rays shoot
        /// out; the orb's spines of light shoot out and are eaten back to the middle; the air
        /// behind is lit violet longest; motes of spent magic sink slowly and go out; the floor
        /// below is lit.
        /// </summary>
        private void ManaRelease(
            Vector2 at,
            Color color,
            float scale,
            List<Shot> held,
            Vector2 floor
        )
        {
            if (held.Count >= 3)
            {
                var glow = held[0];
                var shell = held[1];
                var core = held[2];
                float from = shell.Holder.localScale.x;
                Animate(
                    shell,
                    0.32f,
                    (s, k) =>
                    {
                        // 棘が一気に伸びてから、細くなって中心へ削れる。
                        s.Holder.localScale = Vector3.one * (from * (1.4f + 1.4f * EaseOut(k)));
                        Show(
                            s,
                            Color.Lerp(Color.white, color, k),
                            k,
                            Mathf.Clamp01(k * 1.2f),
                            CoreBright
                        );
                    }
                );
                Animate(
                    core,
                    0.12f,
                    (s, k) =>
                    {
                        s.Holder.localScale = Vector3.one * (from * (1f + 2.5f * EaseOut(k)));
                        Show(s, new Color(1f, 1f, 1f, 1f - k), 0f, 0f, CoreBright * 2f);
                    }
                );
                Animate(
                    glow,
                    0.25f,
                    (s, k) =>
                    {
                        s.Holder.localScale = Vector3.one * (from * (1f + 0.8f * EaseOut(k)));
                        Show(s, new Color(color.r, color.g, color.b, 0.4f * (1f - k)), 0f, 0f);
                    }
                );
                held.Clear();
            }
            HotFlash(at, Color.white, 220f * scale);
            Star(at, Color.Lerp(color, Color.white, 0.6f), 520f * scale, 0.22f);
            Rays(at, color, Mathf.RoundToInt(16 * scale), 0.8f + 0.3f * scale);
            Radiance(backLight, at, color, 760f * scale, 0.45f, 0.03f, 0.08f, 0.7f);
            Pool(floor, color, 520f * scale, 0.08f, 0.6f);
            Illuminate(at, color, 0.8f, 560f * scale, 0.06f, 0.45f, spill: false);
            // 使い切った魔力のかけらが、ゆっくり沈みながら消える。
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Sparkle,
                    Bright = CoreBright,
                    From = Color.Lerp(color, Color.white, 0.5f),
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(80f, 340f) * scale,
                    Spread = 360f,
                    Life = new Vector2(0.6f, 1.1f),
                    Size = new Vector2(12f, 22f),
                    Shrink = 0.3f,
                    Gravity = 160f,
                    Drag = 3f,
                    Spin = 120f,
                    Area = 40f * scale,
                    Delay = new Vector2(0.04f, 0.16f),
                },
                Mathf.RoundToInt(14 * scale)
            );
        }

        /// <summary>
        /// 星降り, a shower of falling stars, as meteors look (doc/features/skill-evolution.md#ツリーごとの演出):
        /// each star comes down from the sky with a white-hot head in a glow of its own colour,
        /// leaves a lit train in the air that lingers and breaks up after it has struck, and
        /// lands one after another with a flash, a pool of light and dust thrown out along the
        /// floor (<see cref="Shooting"/>). 灼・焔獄 fall as fireballs and set the ground alight;
        /// 氷晶・凍星 fall as clear ice that freezes the ground where it lands, then breaks;
        /// 墜星・天墜 all converge on one enemy from one point of the sky, 天墜 ending in a pillar of
        /// light that comes down on it and the dust it raises.
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
                    Licks(hits[index].Area, 3, 0.8f);
                }
            var waves = new List<int>(MainWaves("StarFall", stage));
            for (int n = 0; n < waves.Count; n++)
            {
                int wave = waves[n];
                foreach (int index in Wave(hits, wave))
                {
                    var area = hits[index].Area;
                    var at = Center(FrontLayer, area);
                    var feet = Feet(FrontLayer, area);
                    var sky = new Vector2(at.x + UnityEngine.Random.Range(140f, 360f), top);
                    // 墜星・天墜：空の1点から、角度を少しずつ変えて同じ敵へ集まる。
                    if (branch == 2)
                        sky = new Vector2(at.x + 260f + 70f * (n - waves.Count / 2f), top);
                    yield return Shooting(
                        shape,
                        sky,
                        at,
                        360f * scale,
                        0.16f,
                        color,
                        branch == 1 ? RichMaterialOf(BattleVfxShape.IceSpear) : null,
                        branch == 1 ? BattleVfxShape.Snowflake : BattleVfxShape.Spark
                    );
                    var weight = land(index);
                    switch (branch)
                    {
                        case 0:
                            Explosion(area, at, weight);
                            break;
                        case 1:
                            ClearIce(area, at, weight);
                            break;
                        default:
                            // 星の芯が当たって光り、細い光の筋と塵が外へ飛ぶ。
                            HotFlash(at, Color.white, 150f * scale);
                            Star(at, Color.white, 220f * scale, 0.18f);
                            Rays(at, color, 10, 0.8f);
                            Shockwave(feet, 460f * scale, 0.8f);
                            Sparks(at, color, 14, 1f, Vector2.up);
                            break;
                    }
                    // 続けて落ちる星の光が重なって標的を白く隠さないよう、1つずつは控えめにする。
                    Radiance(backLight, at, color, 420f * scale, 0.25f, 0.03f, 0.05f, 0.35f);
                    Pool(feet, color, 340f * scale, 0.05f, 0.3f);
                    Illuminate(at, color, 0.7f, 420f * scale, 0.04f, 0.3f, spill: false);
                    Shake(0.25f * scale, Vector2.down);
                    yield return Wait(0.05f);
                }
            }
            if (stage == 6 && hits.Count > 0)
            {
                var feet = Feet(FrontLayer, hits[0].Area);
                var at = Center(FrontLayer, hits[0].Area);
                // 天墜：集めた星の光が柱になって降り、足元で塵を巻き上げる。
                LightColumn(feet, new Vector2(620f, 1500f), color, 0.9f, down: true);
                yield return Wait(0.1f);
                Shockwave(feet, 900f, 1f);
                Rays(at, color, 18, 1f);
                Radiance(backLight, at, color, 1200f, 0.5f, 0.04f, 0.2f, 0.7f);
                Illuminate(at, color, 1f, 900f, 0.2f, 0.5f);
                Puffs(
                    backSmoke,
                    feet + Vector2.up * 60f,
                    new Color(0.42f, 0.4f, 0.44f, 0.5f),
                    6,
                    260f,
                    1.6f,
                    220f
                );
            }
        }

        /// <summary>
        /// A star falling from <paramref name="sky"/> onto <paramref name="at"/>, as a meteor is
        /// seen: its head (<paramref name="shape"/>, white-hot at the front), the glow of the air
        /// it lights round the head, sparks (<paramref name="spark"/>) shed behind, and a lit
        /// train from where it came down, which stays a moment after it strikes and breaks up as
        /// it fades (残像).
        /// </summary>
        private IEnumerator Shooting(
            BattleVfxShape shape,
            Vector2 sky,
            Vector2 at,
            float size,
            float seconds,
            Color color,
            Material material,
            BattleVfxShape spark
        )
        {
            var heading = at - sky;
            float turn = Angle(heading);
            var train = Spawn(
                frontLight,
                BattleVfxShape.Streak,
                sky,
                new Vector2(26f, 1f),
                Color.clear,
                new Vector2(0.5f, 0.97f)
            );
            train.Holder.localEulerAngles = new Vector3(0f, 0f, turn - 90f);
            var glow = Spawn(
                frontLight,
                BattleVfxShape.Glow,
                sky,
                Vector2.one * size * 0.9f,
                Color.clear
            );
            var head = Spawn(
                frontLight,
                shape,
                sky,
                new Vector2(size, size),
                color,
                new Vector2(0.85f, 0.5f)
            );
            if (shape == BattleVfxShape.IceSpear)
                head.Image.rectTransform.pivot = new Vector2(0.93f, 0.5f);
            if (material != null)
                head.Image.material = material;
            head.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, turn);
            float shed = 0f;
            float length = heading.magnitude;
            var hot = Color.Lerp(color, Color.white, 0.5f);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                float ease = k * (0.7f + 0.3f * k);
                var position = Vector2.Lerp(sky, at, ease);
                head.Holder.anchoredPosition = position;
                glow.Holder.anchoredPosition = position;
                // 列車の頭は星の頭に付いてくる。
                train.Holder.anchoredPosition = position;
                train.Image.rectTransform.sizeDelta = new Vector2(
                    26f,
                    Mathf.Max(1f, length * ease * 0.97f)
                );
                Show(head, color, k, 0f, CoreBright);
                Show(glow, new Color(color.r, color.g, color.b, 0.45f), 0f, 0f);
                Show(train, new Color(hot.r, hot.g, hot.b, 0.8f), 0f, 0f, ErodeBright);
                shed += Time.deltaTime;
                while (shed > 0.025f)
                {
                    shed -= 0.025f;
                    Spray(
                        frontLight,
                        position,
                        new Burst
                        {
                            Shape = spark,
                            Bright = CoreBright,
                            From = Color.white,
                            To = new Color(color.r, color.g, color.b, 0f),
                            Speed = new Vector2(30f, 120f),
                            Direction = turn + 180f,
                            Spread = 120f,
                            Life = new Vector2(0.25f, 0.5f),
                            Size = new Vector2(10f, 20f),
                            Shrink = 0.3f,
                            Gravity = 60f,
                            Drag = 2f,
                            Area = 18f,
                        },
                        1
                    );
                }
                yield return null;
            }
            head.Live = false;
            head.Holder.gameObject.SetActive(false);
            glow.Live = false;
            glow.Holder.gameObject.SetActive(false);
            // 残像：光った空気の列が、落ちたあとしばらく残り、細くなって途切れながら消える。
            var trainColor = new Color(color.r, color.g, color.b, 0.7f);
            Animate(
                train,
                0.45f,
                (s, k) =>
                {
                    float thin = 1f - 0.6f * k;
                    s.Image.rectTransform.localScale = new Vector3(thin, 1f, 1f);
                    var tint = Color.Lerp(
                        new Color(hot.r, hot.g, hot.b, 0.8f),
                        trainColor,
                        Mathf.Min(1f, k * 3f)
                    );
                    tint.a *= Sq(1f - k);
                    Show(s, tint, k, k, ErodeBright);
                }
            );
        }

        /// <summary>
        /// 雷撃の矢, an arrow that calls down the lightning (doc/features/skill-evolution.md#ツリーごとの演出):
        /// as the arrow leaves the bow, a faint leader feels its way down from the sky toward where
        /// it will stick, and the moment it does, the stroke comes down the same path and lights it
        /// twice more (<see cref="Channel"/>), as a real flash does; crackles run round the enemy.
        /// 痺・縛雷 scorch the ground and tie the target in crackling light (縛雷 nails it down with
        /// a second flash and a circle of runes); 連・迅雷 jump from the enemy struck on to the next,
        /// sideways, each jump one discharge lit again once; 閃・閃光 fly as a thin flash at the
        /// speed of light, with no bolt from the sky, and burst gold on a weakness.
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
            var prev = from;
            bool first = true;
            const float flight = 0.12f;
            foreach (int wave in MainWaves("ThunderArrow", stage))
            foreach (int index in Wave(hits, wave))
            {
                var area = hits[index].Area;
                var at = Center(FrontLayer, area);
                var feet = Feet(FrontLayer, area);
                if (branch == 1 && !first)
                {
                    // 連：打たれた敵から次の敵へ、横に跳ぶ放電。
                    const float jump = 0.05f;
                    Channel(
                        prev,
                        at,
                        BoltLight(color),
                        BoltTraceOf(color),
                        jump,
                        JumpStrokes,
                        300f
                    );
                    StartCoroutine(ChannelLights(prev, at, feet, color, jump, JumpStrokes, 0.8f));
                    yield return Wait(jump);
                }
                else if (branch == 2)
                {
                    // 閃：光の速さの細い一閃。芯の白い線と、色のにじみの線を重ねる。
                    Shaft(from, at, color, 26f * scale, 0.04f, 0.3f);
                    Illuminate(
                        Vector2.Lerp(from, at, 0.5f),
                        color,
                        0.6f,
                        Vector2.Distance(from, at) * 0.6f,
                        0.03f,
                        0.2f,
                        spill: false
                    );
                    yield return Wait(0.04f);
                }
                else
                {
                    var top = new Vector2(at.x + 40f, FrontLayer.rect.yMax + 60f);
                    Channel(
                        top,
                        feet,
                        BoltLight(color),
                        BoltTraceOf(color),
                        flight,
                        BoltStrokes,
                        360f
                    );
                    StartCoroutine(
                        ChannelLights(
                            top,
                            at,
                            feet,
                            color,
                            flight,
                            BoltStrokes,
                            0.9f + 0.1f * TierOfStage(stage)
                        )
                    );
                    yield return Shoot(
                        BattleVfxShape.Arrow,
                        from,
                        at,
                        new Vector2(220f, 60f) * scale,
                        new Vector2(0.9f, 0.5f),
                        flight,
                        0f,
                        color,
                        0f,
                        new Burst
                        {
                            Shape = BattleVfxShape.Streak,
                            Bright = CoreBright,
                            From = Color.white,
                            To = new Color(color.r, color.g, color.b, 0f),
                            Speed = new Vector2(150f, 400f),
                            Spread = 360f,
                            Life = new Vector2(0.04f, 0.1f),
                            Size = new Vector2(3f, 5f),
                            Stretch = 0.05f,
                            Drag = 6f,
                            Area = 14f,
                        }
                    );
                }
                first = false;
                var weight = land(index);
                HotFlash(at, Color.white, 180f * scale);
                Impact(
                    area,
                    at,
                    color,
                    weight,
                    0.7f * scale,
                    (at - prev).normalized,
                    flashScreen: false
                );
                StartCoroutine(Crackle(area, color));
                if (branch == 0)
                {
                    FloorMark(
                        BattleVfxShape.Scorch,
                        feet,
                        new Vector2(240f, 78f),
                        new Color(0.1f, 0.08f, 0.1f),
                        0.8f,
                        1.4f,
                        flat: false
                    );
                    StartCoroutine(Crackle(area, Violet));
                    if (stage == 2)
                    {
                        yield return Wait(0.12f);
                        var top = new Vector2(at.x - 30f, FrontLayer.rect.yMax + 60f);
                        Channel(
                            top,
                            feet,
                            BoltLight(color),
                            BoltTraceOf(color),
                            0.04f,
                            JumpStrokes,
                            300f
                        );
                        StartCoroutine(
                            ChannelLights(top, at, feet, color, 0.04f, JumpStrokes, 0.8f)
                        );
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
                if (branch == 2)
                {
                    Rays(at, color, 10, 0.7f);
                    if (weight != BattleHitWeight.Normal)
                    {
                        Twinkle(at + new Vector2(20f, 40f), Gold, 260f, 0.4f, 0f, 1f);
                        Sparks(at, Gold, 24, 1.3f, Vector2.up);
                        Radiance(backLight, at, Gold, 520f, 0.5f, 0.03f, 0.06f, 0.4f);
                    }
                }
                prev = at;
                yield return Wait(branch == 1 ? 0.04f : 0.08f);
            }
            // 光った通り道が目に残って消えるまで待つ。
            yield return Wait(0.2f);
        }

        // How far a unique skill's stage has evolved, by its number: 0 the first, 1 a first step, 2 a last.
        private static int TierOfStage(int stage) =>
            stage == 0 ? 0
            : stage % 2 == 1 ? 1
            : 2;

        /// <summary>
        /// 矢の雨, a volley (doc/features/skill-evolution.md#ツリーごとの演出): the volley is loosed high
        /// from the user and rains slanting onto every enemy, wave after wave, each arrow a thin
        /// white streak with a fainter coloured one left in the eye behind it; the arrows that miss
        /// stick in the floor round the feet one after another with a spurt of dust. 毒・蠱毒 drip
        /// venom and leave a green haze low on the floor; 穿・穿天 punch through armour with heavier
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
            if (branch != 2)
            {
                // 予兆：使い手から矢の一群が高く放たれ、画面の上へ消える。
                var bow = Center(FrontLayer, caster) + new Vector2(40f, 40f);
                Spray(
                    frontLight,
                    bow,
                    new Burst
                    {
                        Shape = BattleVfxShape.Streak,
                        Bright = CoreBright,
                        From = Color.white,
                        To = new Color(color.r, color.g, color.b, 0f),
                        Speed = new Vector2(1800f, 2400f),
                        Direction = 72f,
                        Spread = 10f,
                        Life = new Vector2(0.2f, 0.3f),
                        Size = new Vector2(6f, 10f),
                        Stretch = 0.06f,
                        Area = 30f,
                        Delay = new Vector2(0f, 0.1f),
                    },
                    Mathf.RoundToInt(10 * scale)
                );
                Illuminate(bow, color, 0.4f, 220f, 0.05f, 0.2f, spill: false);
                yield return Wait(0.16f);
            }
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
                    // 残像：矢の後ろに目に残る、色の付いた太く淡い筋。矢より遅れて消える。
                    Spray(
                        backLight,
                        start,
                        new Burst
                        {
                            Shape = BattleVfxShape.Streak,
                            Bright = ErodeBright,
                            From = new Color(color.r, color.g, color.b, 0.35f),
                            To = new Color(color.r, color.g, color.b, 0f),
                            Speed = new Vector2(1500f, 2000f),
                            Direction = direction,
                            Spread = 4f,
                            Life = new Vector2(0.22f, 0.3f),
                            Size = new Vector2(16f, 24f) * scale,
                            Stretch = 0.1f,
                            Drag = 1f,
                            Area = 120f,
                        },
                        Mathf.RoundToInt(4 * scale)
                    );
                }
                yield return Wait(0.18f);
                foreach (int index in indices)
                {
                    var area = hits[index].Area;
                    var at = Center(FrontLayer, area);
                    var feet = Feet(FrontLayer, area);
                    var weight = land(index);
                    // 外れた矢が床に刺さり、小さな土ぼこりが順に上がる。
                    Stuck(feet, direction, 3 + TierOfStage(stage));
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
                // A gust as a real one shows: dust and leaves caught up and spun round.
                Spray(
                    backSmoke,
                    mid + Vector2.up * 30f,
                    new Burst
                    {
                        Shape = BattleVfxShape.Smoke,
                        Bright = 0.5f,
                        From = new Color(KickedDust.r, KickedDust.g, KickedDust.b, 0.6f),
                        To = new Color(KickedDust.r, KickedDust.g, KickedDust.b, 0.6f),
                        Speed = new Vector2(80f, 260f),
                        Direction = 90f,
                        Spread = 120f,
                        Life = new Vector2(0.6f, 1f),
                        Size = new Vector2(140f, 240f) * scale,
                        Aspect = 0.6f,
                        Grow = 0.5f,
                        Shrink = 1.5f,
                        Drag = 1.5f,
                        Spin = 200f,
                        Hold = 0.3f,
                        Area = 120f * scale,
                    },
                    5
                );
                Leaves(frontLight, mid, new Color(0.58f, 0.74f, 0.34f), 10, 90f, 420f, -20f);
                // 疾風 hastens the party: a gust of leaves round each ally.
                if (stage == 6)
                    foreach (var hit in hits)
                        if (hit.Ally)
                            Spray(frontLight, Feet(FrontLayer, hit.Area), Gust(color), 8);
            }
        }

        /// <summary>
        /// Arrows that missed sticking in the floor round <paramref name="feet"/>, one after
        /// another: at each a small spurt of dust thrown back against the flight and a chip of
        /// earth, the near ones before the target.
        /// </summary>
        private void Stuck(Vector2 feet, float direction, int count)
        {
            var dust = new Color(KickedDust.r, KickedDust.g, KickedDust.b, 0.5f);
            for (int i = 0; i < count; i++)
            {
                float side = UnityEngine.Random.Range(-1f, 1f);
                float depth = UnityEngine.Random.Range(-1f, 1f);
                var at = feet + new Vector2(side * 150f, depth * 150f * FloorTilt);
                Spray(
                    depth < 0f ? frontSmoke : backSmoke,
                    at,
                    new Burst
                    {
                        Shape = BattleVfxShape.Smoke,
                        Bright = 0.45f,
                        From = dust,
                        To = dust,
                        Speed = new Vector2(80f, 200f),
                        Direction = direction + 180f + UnityEngine.Random.Range(-20f, 20f),
                        Spread = 40f,
                        Life = new Vector2(0.35f, 0.55f),
                        Size = new Vector2(40f, 70f),
                        Aspect = 0.6f,
                        Grow = 0.3f,
                        Shrink = 1.4f,
                        Drag = 4f,
                        Hold = 0.3f,
                        Delay = new Vector2(0.02f * i, 0.02f * i + 0.04f),
                    },
                    1
                );
                Spray(
                    depth < 0f ? frontSmoke : backSmoke,
                    at,
                    new Burst
                    {
                        Shape = BattleVfxShape.Chip,
                        From = new Color(0.42f, 0.34f, 0.28f),
                        To = new Color(0.3f, 0.24f, 0.2f, 0.6f),
                        Speed = new Vector2(150f, 300f),
                        Direction = 90f,
                        Spread = 60f,
                        Life = new Vector2(0.2f, 0.35f),
                        Size = new Vector2(5f, 9f),
                        Gravity = 1600f,
                        Spin = 360f,
                        Delay = new Vector2(0.02f * i, 0.02f * i + 0.04f),
                    },
                    2
                );
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
                        Twinkle(at, Color.Lerp(color, Color.white, 0.5f), 200f, 0.2f, 0f, 1f);
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
                // 予兆：紋章へ光が寄せられてから、膜が床から立ち上がる。
                Converge(
                    frontLight,
                    basePoint + Vector2.up * 220f,
                    color,
                    420f,
                    0.12f,
                    0f,
                    floor: false
                );
                yield return Wait(0.1f);
                Dome(backLight, basePoint, width, branch == 0 ? 1f : 0.8f, breaks: branch != 0);
                Dome(frontLight, basePoint, width, 0.3f, breaks: false);
                // 膜の内側の光った空気と、床の光だまり。膜が立った風で床の塵が外へ押される。
                Radiance(
                    backLight,
                    basePoint + Vector2.up * 160f,
                    color,
                    width * 1.1f,
                    0.35f,
                    0.08f,
                    0.3f,
                    0.5f
                );
                Pool(basePoint, color, width * 1.2f, 0.25f, 0.5f);
                DustKick(basePoint + Vector2.left * width * 0.45f, Vector2.left, 160f);
                DustKick(basePoint + Vector2.right * width * 0.45f, Vector2.right, 160f);
                foreach (int index in indices)
                {
                    land(index);
                    var feet = Feet(FrontLayer, hits[index].Area);
                    Circle(feet, color, 0.8f);
                    if (branch == 2)
                    {
                        // 陣：守られた一人ずつの足元から、細い光の線が膜の中を昇る。
                        var (min, max) = Bounds(FrontLayer, hits[index].Area);
                        for (int i = 0; i < 6 + 4 * TierOfStage(stage); i++)
                            RiseLine(
                                backLight,
                                OnBody(
                                    min,
                                    Mathf.Max(80f, max.x - min.x),
                                    Mathf.Max(120f, max.y - min.y),
                                    0.3f
                                ),
                                Color.Lerp(color, Color.white, 0.5f),
                                color,
                                new Vector2(3f, 5f),
                                new Vector2(300f, 600f),
                                new Vector2(0.25f, 0.4f),
                                0.3f * UnityEngine.Random.value,
                                CoreBright
                            );
                    }
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
            // 予兆：抜く前の刃に光が一点走る。
            var hilt = Center(FrontLayer, caster) + new Vector2(34f, 24f);
            Twinkle(hilt, Color.Lerp(color, Color.white, 0.6f), 150f, 0.22f, 0f, 1f);
            Illuminate(hilt, color, 0.4f, 200f, 0.05f, 0.2f, spill: false);
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
                    // 振った刃の風で、足元の塵が払われる。
                    DustKick(Feet(FrontLayer, area), Vector2.right, 220f);
                    yield return Wait(branch == 2 ? 0.02f : 0.1f);
                    var weight = land(index);
                    Cross(at, color, weight);
                    StartCoroutine(Split(at, -20f, color, 0.08f));
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
        /// The cut opening a beat after the blade has passed: a thin white line along it flashes
        /// and is gone, with its colour lingering a moment longer round it.
        /// </summary>
        private IEnumerator Split(Vector2 at, float angle, Color color, float delay)
        {
            yield return Wait(delay);
            var along = (Vector2)(Quaternion.Euler(0f, 0f, angle) * Vector2.right) * 240f;
            Shaft(at - along, at + along, color, 16f, 0.03f, 0.25f);
            Rays(at, color, 8, 0.6f);
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
            // 予兆：味方の高さを、葉と光を運ぶ風の帯が左から吹き抜ける。
            var band = MidFeet(hits, 0) + Vector2.up * 120f;
            WindStream(
                backLight,
                band.y,
                260f,
                Color.Lerp(color, Color.white, 0.4f),
                0.9f,
                0.5f + 0.15f * TierOfStage(stage)
            );
            Leaves(
                frontLight,
                new Vector2(FrontLayer.rect.xMin + 40f, band.y),
                color,
                8 + 4 * TierOfStage(stage),
                0f,
                700f,
                -10f
            );
            yield return Wait(0.18f);
            foreach (int wave in MainWaves("HealingWind", stage))
            {
                var indices = Wave(hits, wave);
                foreach (int index in indices)
                {
                    var area = hits[index].Area;
                    var feet = Feet(FrontLayer, area);
                    var at = Center(FrontLayer, area);
                    land(index);
                    // 風が体を巻いて昇り、体の奥に柔らかい光が満ちる。
                    Spiral(feet, color, 6 + 2 * TierOfStage(stage), 0.8f);
                    Radiance(backLight, at, color, 300f * scale, 0.22f, 0.15f, 0.15f, 0.5f);
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
                    Pool(feet, color, 220f * scale, 0.2f, 0.5f);
                    Illuminate(at, color, 0.5f, 320f, 0.2f, 0.5f, spill: false);
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
                Glow(frontLight, home, Gold, 240f, 0.45f, 0.6f);
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
        /// 蘇生, a rising (doc/features/skill-evolution.md#ツリーごとの演出): the scattered light gathers
        /// into the fallen ally, a shaft of light comes down on them and thin lines of light and
        /// white motes lift them.
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
                    // 予兆：散った光が倒れた体へ寄り集まる（形を取り戻す）。
                    Gather(at, Color.Lerp(color, Color.white, 0.5f), 14 + 6 * branch, 280f, 0.3f);
                    Radiance(backLight, at, color, 300f * scale, 0.35f, 0.2f, 0.2f, 0.5f);
                    LightColumn(
                        feet,
                        new Vector2(320f * scale, 1100f),
                        Color.Lerp(color, Color.white, 0.3f),
                        branch == 0 ? 0.5f : 0.75f,
                        down: true
                    );
                    yield return Wait(branch == 0 ? 0.12f : 0.22f);
                    Pillar(feet);
                }
                land(index);
                // 段階の層（Empower）が同じ光を重ねるため、最初の段階だけここで出す。
                if (stage == 0)
                    Uplift(area, color, 0, false);
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
