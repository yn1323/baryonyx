using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>What an awakened stage marks each place it reaches with, beside its card skill's effect.</summary>
    public enum AwakenedMark
    {
        /// <summary>A white-hot flash and a cross of light where it lands.</summary>
        Core,

        /// <summary>Petals scattering where it lands and drifting down.</summary>
        Petals,

        /// <summary>A ring of a shield's light at the feet and motes rising.</summary>
        Shield,

        /// <summary>A ring of frost at the feet and shards of ice in the air.</summary>
        Frost,

        /// <summary>Motes of a poison mist rising.</summary>
        Venom,

        /// <summary>Sparks of a small lightning bursting.</summary>
        Bolt,

        /// <summary>Motes of light rising from the feet in a soft light.</summary>
        Holy,

        /// <summary>Streaks of wind blowing across.</summary>
        Gale,

        /// <summary>A wide ring of dust at the feet and the screen shaking.</summary>
        Quake,

        /// <summary>A star twinkling over the user and motes gathering to it.</summary>
        Glint,

        /// <summary>Points of light twinkling over each place in turn, as if linked.</summary>
        Chain,

        /// <summary>Embers flying up.</summary>
        Ember,
    }

    /// <summary>An awakened stage's own look: the colour of its light and its mark.</summary>
    public readonly struct AwakenedLook
    {
        public readonly Color Color;
        public readonly AwakenedMark Mark;

        public AwakenedLook(Color color, AwakenedMark mark)
        {
            Color = color;
            Mark = mark;
        }
    }

    /// <summary>
    /// The awakened stages of the card skills (doc/features/skill-awakening.md#演出): each plays its
    /// card skill's effect in light of its own colour, after a wind-up its awakening earns and with
    /// a first step's layers at each blow, as a unique skill's first step does, and marks each
    /// place it reaches its own way. What it does beyond its card skill shows by its order: a
    /// status laid before the blow lights its targets first, more rounds of blows play the effect
    /// again, and what it adds after the blow (a block, a draw, a status) lands with its mark.
    /// </summary>
    public sealed partial class BattleSkillVfx
    {
        // 色はほかの部分の静的な値を使うため、初めて使うときに作る。
        private static Dictionary<string, AwakenedLook> awakenedLooks;

        /// <summary>True when <paramref name="id"/> is an awakened stage of a card skill ("Fire.1").</summary>
        public static bool IsAwakened(string id) =>
            !string.IsNullOrEmpty(id) && CardSkillAwakenings.IsAwakened(id);

        /// <summary>The look of an awakened stage, or null for another card.</summary>
        public static AwakenedLook? LookOf(string id)
        {
            awakenedLooks ??= Looks();
            return id != null && awakenedLooks.TryGetValue(id, out var look) ? look : null;
        }

        /// <summary>The colour of an awakened stage's light.</summary>
        public static Color AwakenedColor(string id) => LookOf(id)?.Color ?? Gold;

        private static Dictionary<string, AwakenedLook> Looks() =>
            new()
            {
                ["Slash.1"] = new AwakenedLook(Blood, AwakenedMark.Petals),
                ["Slash.2"] = new AwakenedLook(Steel, AwakenedMark.Shield),
                ["Fire.1"] = new AwakenedLook(ColorOf(BattleSkillVfxKind.Fire), AwakenedMark.Core),
                ["Fire.2"] = new AwakenedLook(ColorOf(BattleSkillVfxKind.Fire), AwakenedMark.Ember),
                ["Ice.1"] = new AwakenedLook(ColorOf(BattleSkillVfxKind.Ice), AwakenedMark.Core),
                ["Ice.2"] = new AwakenedLook(Starlight, AwakenedMark.Frost),
                ["Thunder.1"] = new AwakenedLook(Bolt8, AwakenedMark.Bolt),
                ["Thunder.2"] = new AwakenedLook(
                    ColorOf(BattleSkillVfxKind.Thunder),
                    AwakenedMark.Core
                ),
                ["Heal.1"] = new AwakenedLook(Leafy, AwakenedMark.Holy),
                ["Heal.2"] = new AwakenedLook(Blossom, AwakenedMark.Gale),
                ["Guard.1"] = new AwakenedLook(
                    ColorOf(BattleSkillVfxKind.Guard),
                    AwakenedMark.Shield
                ),
                ["Guard.2"] = new AwakenedLook(Steel, AwakenedMark.Glint),
                ["Whirlwind.1"] = new AwakenedLook(Crimson, AwakenedMark.Petals),
                ["Whirlwind.2"] = new AwakenedLook(Steel, AwakenedMark.Gale),
                ["Iai.1"] = new AwakenedLook(Crimson, AwakenedMark.Core),
                ["Iai.2"] = new AwakenedLook(Starlight, AwakenedMark.Gale),
                ["BladeDance.1"] = new AwakenedLook(Blood, AwakenedMark.Petals),
                ["BladeDance.2"] = new AwakenedLook(Gold, AwakenedMark.Glint),
                ["Hone.1"] = new AwakenedLook(Teal, AwakenedMark.Glint),
                ["Hone.2"] = new AwakenedLook(Steel, AwakenedMark.Core),
                ["ShieldBash.1"] = new AwakenedLook(Earth, AwakenedMark.Quake),
                ["ShieldBash.2"] = new AwakenedLook(Gold, AwakenedMark.Shield),
                ["ArmorBreak.1"] = new AwakenedLook(Earth, AwakenedMark.Quake),
                ["ArmorBreak.2"] = new AwakenedLook(Violet, AwakenedMark.Core),
                ["EarthSplitter.1"] = new AwakenedLook(Earth, AwakenedMark.Quake),
                ["EarthSplitter.2"] = new AwakenedLook(Gold, AwakenedMark.Shield),
                ["GiantImpact.1"] = new AwakenedLook(
                    ColorOf(BattleSkillVfxKind.Guard),
                    AwakenedMark.Shield
                ),
                ["GiantImpact.2"] = new AwakenedLook(Gold, AwakenedMark.Quake),
                ["StrideStrike.1"] = new AwakenedLook(Gold, AwakenedMark.Gale),
                ["StrideStrike.2"] = new AwakenedLook(Earth, AwakenedMark.Quake),
                ["GuardianOath.1"] = new AwakenedLook(Steel, AwakenedMark.Shield),
                ["GuardianOath.2"] = new AwakenedLook(Starlight, AwakenedMark.Glint),
                ["WarCry.1"] = new AwakenedLook(Crimson, AwakenedMark.Ember),
                ["WarCry.2"] = new AwakenedLook(Gold, AwakenedMark.Shield),
                ["Embers.1"] = new AwakenedLook(
                    ColorOf(BattleSkillVfxKind.Fire),
                    AwakenedMark.Ember
                ),
                ["Embers.2"] = new AwakenedLook(Dawn, AwakenedMark.Glint),
                ["FlamePillar.1"] = new AwakenedLook(
                    ColorOf(BattleSkillVfxKind.Fire),
                    AwakenedMark.Core
                ),
                ["FlamePillar.2"] = new AwakenedLook(Gold, AwakenedMark.Chain),
                ["FireStorm.1"] = new AwakenedLook(Crimson, AwakenedMark.Ember),
                ["FireStorm.2"] = new AwakenedLook(
                    ColorOf(BattleSkillVfxKind.Fire),
                    AwakenedMark.Gale
                ),
                ["BlastSigil.1"] = new AwakenedLook(Crimson, AwakenedMark.Chain),
                ["BlastSigil.2"] = new AwakenedLook(
                    ColorOf(BattleSkillVfxKind.Fire),
                    AwakenedMark.Core
                ),
                ["Meteor.1"] = new AwakenedLook(Crimson, AwakenedMark.Ember),
                ["Meteor.2"] = new AwakenedLook(
                    ColorOf(BattleSkillVfxKind.Fire),
                    AwakenedMark.Quake
                ),
                ["FlameEnchant.1"] = new AwakenedLook(
                    ColorOf(BattleSkillVfxKind.Fire),
                    AwakenedMark.Glint
                ),
                ["FlameEnchant.2"] = new AwakenedLook(Crimson, AwakenedMark.Ember),
                ["Icicles.1"] = new AwakenedLook(
                    ColorOf(BattleSkillVfxKind.Ice),
                    AwakenedMark.Frost
                ),
                ["Icicles.2"] = new AwakenedLook(Starlight, AwakenedMark.Core),
                ["Blizzard.1"] = new AwakenedLook(
                    ColorOf(BattleSkillVfxKind.Ice),
                    AwakenedMark.Frost
                ),
                ["Blizzard.2"] = new AwakenedLook(Steel, AwakenedMark.Shield),
                ["IceMirror.1"] = new AwakenedLook(Starlight, AwakenedMark.Glint),
                ["IceMirror.2"] = new AwakenedLook(
                    ColorOf(BattleSkillVfxKind.Ice),
                    AwakenedMark.Shield
                ),
                ["AbsoluteZero.1"] = new AwakenedLook(
                    ColorOf(BattleSkillVfxKind.Ice),
                    AwakenedMark.Frost
                ),
                ["AbsoluteZero.2"] = new AwakenedLook(Starlight, AwakenedMark.Core),
                ["QuickCast.1"] = new AwakenedLook(Arcane, AwakenedMark.Glint),
                ["QuickCast.2"] = new AwakenedLook(Gold, AwakenedMark.Glint),
                ["VitalThrust.1"] = new AwakenedLook(Blood, AwakenedMark.Core),
                ["VitalThrust.2"] = new AwakenedLook(Teal, AwakenedMark.Gale),
                ["PoisonNeedle.1"] = new AwakenedLook(Venom, AwakenedMark.Venom),
                ["PoisonNeedle.2"] = new AwakenedLook(Violet, AwakenedMark.Venom),
                ["ShadowStitch.1"] = new AwakenedLook(Violet, AwakenedMark.Chain),
                ["ShadowStitch.2"] = new AwakenedLook(Arcane, AwakenedMark.Gale),
                ["InsightArrow.1"] = new AwakenedLook(Teal, AwakenedMark.Glint),
                ["InsightArrow.2"] = new AwakenedLook(Starlight, AwakenedMark.Glint),
                ["ArrowRain.1"] = new AwakenedLook(Venom, AwakenedMark.Venom),
                ["ArrowRain.2"] = new AwakenedLook(Steel, AwakenedMark.Gale),
                ["ShadowSnipe.1"] = new AwakenedLook(Teal, AwakenedMark.Core),
                ["ShadowSnipe.2"] = new AwakenedLook(Violet, AwakenedMark.Glint),
                ["LightningBolt.1"] = new AwakenedLook(Bolt8, AwakenedMark.Bolt),
                ["LightningBolt.2"] = new AwakenedLook(
                    ColorOf(BattleSkillVfxKind.Thunder),
                    AwakenedMark.Glint
                ),
                ["ChainLightning.1"] = new AwakenedLook(Bolt8, AwakenedMark.Bolt),
                ["ChainLightning.2"] = new AwakenedLook(Starlight, AwakenedMark.Chain),
                ["Thundercloud.1"] = new AwakenedLook(
                    ColorOf(BattleSkillVfxKind.Thunder),
                    AwakenedMark.Quake
                ),
                ["Thundercloud.2"] = new AwakenedLook(Bolt8, AwakenedMark.Glint),
                ["ThunderSpear.1"] = new AwakenedLook(Bolt8, AwakenedMark.Core),
                ["ThunderSpear.2"] = new AwakenedLook(
                    ColorOf(BattleSkillVfxKind.Thunder),
                    AwakenedMark.Quake
                ),
                ["Gale.1"] = new AwakenedLook(Teal, AwakenedMark.Gale),
                ["Gale.2"] = new AwakenedLook(Bolt8, AwakenedMark.Bolt),
                ["Scout.1"] = new AwakenedLook(Teal, AwakenedMark.Glint),
                ["Scout.2"] = new AwakenedLook(Leafy, AwakenedMark.Chain),
                ["StepPrayer.1"] = new AwakenedLook(Leafy, AwakenedMark.Holy),
                ["StepPrayer.2"] = new AwakenedLook(
                    ColorOf(BattleSkillVfxKind.Guard),
                    AwakenedMark.Shield
                ),
                ["Regen.1"] = new AwakenedLook(Leafy, AwakenedMark.Gale),
                ["Regen.2"] = new AwakenedLook(Blossom, AwakenedMark.Holy),
                ["Resurrection.1"] = new AwakenedLook(Dawn, AwakenedMark.Shield),
                ["Resurrection.2"] = new AwakenedLook(Gold, AwakenedMark.Holy),
                ["HolyHammer.1"] = new AwakenedLook(Dawn, AwakenedMark.Core),
                ["HolyHammer.2"] = new AwakenedLook(HolyGold, AwakenedMark.Holy),
                ["Protect.1"] = new AwakenedLook(Starlight, AwakenedMark.Holy),
                ["Protect.2"] = new AwakenedLook(Blossom, AwakenedMark.Shield),
                ["Purify.1"] = new AwakenedLook(Dawn, AwakenedMark.Shield),
                ["Purify.2"] = new AwakenedLook(Leafy, AwakenedMark.Gale),
                ["ManaPrayer.1"] = new AwakenedLook(Arcane, AwakenedMark.Glint),
                ["ManaPrayer.2"] = new AwakenedLook(Teal, AwakenedMark.Holy),
                ["Revelation.1"] = new AwakenedLook(Gold, AwakenedMark.Glint),
                ["Revelation.2"] = new AwakenedLook(Starlight, AwakenedMark.Chain),
                ["HolyLight.1"] = new AwakenedLook(Dawn, AwakenedMark.Holy),
                ["HolyLight.2"] = new AwakenedLook(HolyGold, AwakenedMark.Core),
                ["DivineShield.1"] = new AwakenedLook(Gold, AwakenedMark.Shield),
                ["DivineShield.2"] = new AwakenedLook(Blossom, AwakenedMark.Holy),
            };

        /// <summary>
        /// Plays an awakened stage: the wind-up, the statuses it lays before its blow, its card
        /// skill's effect on its blows (again for each round more than its card skill has), and
        /// what it does after them, each with its mark.
        /// </summary>
        private IEnumerator Awakened(
            string id,
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            Func<int, BattleHitWeight> land,
            bool cutIn
        )
        {
            var stage = CardSkillAwakenings.Find(id);
            var skill = CardSkillAwakenings.Of(id)?.Skill;
            var look = LookOf(id) ?? new AwakenedLook(Gold, AwakenedMark.Core);
            if (stage == null || skill == null)
            {
                yield return CardEffect(id, caster, hits, land, cutIn);
                yield break;
            }
            var landed = new bool[hits.Count];
            BattleHitWeight Land(int index)
            {
                if (index < 0 || index >= hits.Count || landed[index])
                    return BattleHitWeight.Normal;
                landed[index] = true;
                return land(index);
            }

            var main = MainWavesOf(stage, hits);
            var blows = BlowsOf(skill, hits);
            var empowered = Empower(hits, Land, look.Color, 1, main);
            yield return FirstStepWindUp(caster, look.Color, stage);

            // 打つ前に付ける状態（大地割り・崩の防御ダウンなど）は、先に相手を光らせて付ける。
            int first = main.Count > 0 ? main[0] : 0;
            for (int wave = 0; wave < first; wave++)
                yield return MarkWave(caster, hits, wave, look, Land, landed);

            // 元のスキルの演出を、主な当たりの波から始まる並びで流す。
            var played = Renumbered(hits, first);
            yield return CardEffect(
                skill.Id,
                caster,
                played.Hits,
                index => Marked(caster, hits, played.Index[index], look, empowered, main),
                cutIn
            );
            // 元のスキルより多い回数の当たりは、元の演出をその波だけで繰り返す。
            for (int round = blows; round < main.Count; round++)
            {
                var again = Single(hits, main[round]);
                if (again.Hits.Count == 0 || landed[again.Index[0]])
                    continue;
                yield return Wait(0.06f);
                yield return CardEffect(
                    skill.Id,
                    caster,
                    again.Hits,
                    index => Marked(caster, hits, again.Index[index], look, empowered, main),
                    cutIn
                );
            }
            // 当たりのあとに足した働き（ブロック・手札・状態など）は、しるしを付けて届ける。
            int last = 0;
            foreach (var hit in hits)
                last = Math.Max(last, hit.Wave);
            for (int wave = first; wave <= last; wave++)
                yield return MarkWave(caster, hits, wave, look, Land, landed);
            yield return Wait(0.2f);
        }

        // The hit lands as its card skill's effect reaches it, with the layers and its mark on a blow.
        private BattleHitWeight Marked(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            int index,
            AwakenedLook look,
            Func<int, BattleHitWeight> empowered,
            List<int> main
        )
        {
            var weight = empowered(index);
            if (index >= 0 && index < hits.Count && main.Contains(hits[index].Wave))
                Mark(look, caster, hits[index], 0);
            return weight;
        }

        /// <summary>Lands what is left of one wave, each place with the stage's mark, a beat apart from the next.</summary>
        private IEnumerator MarkWave(
            RectTransform caster,
            IReadOnlyList<BattleSkillHit> hits,
            int wave,
            AwakenedLook look,
            Func<int, BattleHitWeight> land,
            bool[] landed
        )
        {
            int order = 0;
            foreach (int index in Wave(hits, wave))
            {
                if (landed[index])
                    continue;
                land(index);
                Mark(look, caster, hits[index], order++);
            }
            if (order > 0)
                yield return Wait(0.16f);
        }

        /// <summary>
        /// The waves the stage's blows land in: those of its damage, or, for a card without damage,
        /// those of its first action (a heal, a guard, a buff).
        /// </summary>
        private static List<int> MainWavesOf(CardSkill stage, IReadOnlyList<BattleSkillHit> hits)
        {
            var plan = PlanOf(stage, hits);
            var damage = new List<int>();
            foreach (var hit in plan)
                if (
                    !damage.Contains(hit.Wave)
                    && hit.Actions.Exists(a => a.Kind == CardActionKind.Damage)
                )
                    damage.Add(hit.Wave);
            if (damage.Count == 0 && plan.Count > 0)
                damage.Add(plan[0].Wave);
            damage.Sort();
            return damage;
        }

        /// <summary>How many rounds of blows the card skill's own effect plays (one for a card without damage).</summary>
        private static int BlowsOf(CardSkill skill, IReadOnlyList<BattleSkillHit> hits) =>
            Math.Max(1, MainWavesOf(skill, hits).Count);

        // The card's waves as the battle plans them for as many enemies and allies as the hits reach.
        private static List<BattleCardPlan.Hit> PlanOf(
            CardSkill card,
            IReadOnlyList<BattleSkillHit> hits
        )
        {
            var enemies = new HashSet<RectTransform>();
            var allies = new HashSet<RectTransform>();
            foreach (var hit in hits)
                (hit.Ally ? allies : enemies).Add(hit.Area);
            var enemyList = new List<int>();
            for (int i = 0; i < Math.Max(1, enemies.Count); i++)
                enemyList.Add(i);
            var allyList = new List<int>();
            for (int i = 0; i < Math.Max(1, allies.Count); i++)
                allyList.Add(i);
            bool onEnemies =
                card.Target
                is CardTarget.OneEnemy
                    or CardTarget.AllEnemies
                    or CardTarget.RandomEnemies
                    or CardTarget.ChainEnemies;
            return BattleCardPlan.Plan(card, enemyList, allyList, 0, onEnemies, 0, _ => 0);
        }

        private readonly struct Renumbering
        {
            public readonly List<BattleSkillHit> Hits;
            public readonly List<int> Index;

            public Renumbering(List<BattleSkillHit> hits, List<int> index)
            {
                Hits = hits;
                Index = index;
            }
        }

        // The hits from wave `first` on, numbered from 0, with the index each came from.
        private static Renumbering Renumbered(IReadOnlyList<BattleSkillHit> hits, int first)
        {
            var list = new List<BattleSkillHit>();
            var index = new List<int>();
            for (int i = 0; i < hits.Count; i++)
                if (hits[i].Wave >= first)
                {
                    list.Add(new BattleSkillHit(hits[i].Area, hits[i].Ally, hits[i].Wave - first));
                    index.Add(i);
                }
            return new Renumbering(list, index);
        }

        // One wave's hits alone, as wave 0.
        private static Renumbering Single(IReadOnlyList<BattleSkillHit> hits, int wave)
        {
            var list = new List<BattleSkillHit>();
            var index = new List<int>();
            for (int i = 0; i < hits.Count; i++)
                if (hits[i].Wave == wave)
                {
                    list.Add(new BattleSkillHit(hits[i].Area, hits[i].Ally, 0));
                    index.Add(i);
                }
            return new Renumbering(list, index);
        }

        /// <summary>The stage's mark on one place it reaches (the <paramref name="order"/>th of its wave).</summary>
        private void Mark(AwakenedLook look, RectTransform caster, BattleSkillHit hit, int order)
        {
            if (hit.Area == null || FrontLayer == null)
                return;
            var color = look.Color;
            var at = Center(FrontLayer, hit.Area);
            var feet = Feet(FrontLayer, hit.Area);
            switch (look.Mark)
            {
                case AwakenedMark.Core:
                    HotFlash(at, color, 260f);
                    Star(at, Color.white, 260f, 0.22f);
                    break;
                case AwakenedMark.Petals:
                    Drops(at, color, 10);
                    Leaves(frontLight, at + Vector2.up * 40f, color, 8, 90f, 200f);
                    break;
                case AwakenedMark.Shield:
                    Shockwave(feet, 320f, 0.4f);
                    Rise(feet, color, 14);
                    Illuminate(at, color, 0.5f, 300f, 0.1f, 0.4f, spill: false);
                    break;
                case AwakenedMark.Frost:
                    Shockwave(feet, 420f, 0.5f);
                    Sparks(at, Color.Lerp(color, Color.white, 0.5f), 12, 0.7f, Vector2.up);
                    Illuminate(at, color, 0.45f, 320f, 0.1f, 0.4f, spill: false);
                    break;
                case AwakenedMark.Venom:
                    Rise(feet, color, 16);
                    Drops(at, color, 6);
                    break;
                case AwakenedMark.Bolt:
                    Sparks(at, color, 18, 1.3f, Vector2.up);
                    Star(at, color, 160f, 0.12f);
                    break;
                case AwakenedMark.Holy:
                    Rise(feet, color, 18);
                    Illuminate(at, color, 0.6f, 360f, 0.15f, 0.5f, spill: false);
                    break;
                case AwakenedMark.Gale:
                    Sparks(at + Vector2.up * 30f, color, 14, 1.4f, Vector2.right);
                    Sparks(at - Vector2.up * 30f, Color.white, 8, 1.2f, Vector2.right);
                    break;
                case AwakenedMark.Quake:
                    Shockwave(feet, 720f, 1f);
                    Shake(0.35f, Vector2.down);
                    break;
                case AwakenedMark.Glint:
                    if (caster != null && order == 0)
                    {
                        var head = Center(FrontLayer, caster) + new Vector2(30f, 90f);
                        Twinkle(head, color, 200f, 0.5f, 0f, 1f);
                        Rise(Feet(FrontLayer, caster), color, 10);
                    }
                    Twinkle(at + Vector2.up * 50f, color, 120f, 0.35f, 0.05f, 1f);
                    break;
                case AwakenedMark.Chain:
                    Twinkle(at + Vector2.up * 60f, color, 150f, 0.4f, order * 0.06f, 1f);
                    break;
                case AwakenedMark.Ember:
                    Rise(feet, color, 14);
                    Sparks(at, color, 10, 0.6f, Vector2.up);
                    break;
            }
        }
    }
}
