using System;
using System.Collections.Generic;
using Baryonyx.Combat;
using static Baryonyx.Combat.CardAction;

namespace Baryonyx.SkillEvolution
{
    /// <summary>
    /// The unique skills' evolution trees (provisional mock data: doc/features/skill-evolution.md).
    /// Each tree has its first stage, the card the unique skill starts as; the other stages are
    /// made one character at a time, so only those the mock progress has acquired exist yet.
    /// The art is borrowed from the card skills until unique skills have their own.
    /// </summary>
    public static class UniqueSkillTrees
    {
        private const CardTarget Enemy = CardTarget.OneEnemy;
        private const CardTarget Enemies = CardTarget.AllEnemies;
        private const CardTarget Random = CardTarget.RandomEnemies;
        private const CardTarget Ally = CardTarget.OneAlly;
        private const CardTarget Allies = CardTarget.AllAllies;
        private const CardTarget Self = CardTarget.Self;
        private const CardStat Phy = CardStat.PhysicalAttack;
        private const CardStat Mag = CardStat.MagicAttack;
        private const CardStat Def = CardStat.PhysicalDefense;

        public static readonly IReadOnlyList<UniqueSkillTree> All = new[]
        {
            // --- The mage (トーマ・ルッツ) ---------------------------------------------------
            Tree(
                "ManaBurst",
                Stage(
                    "ManaBurst",
                    "マナバースト",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Attack,
                    Enemies,
                    3,
                    "Revelation",
                    "敵全体に{0}ダメージ。",
                    Damage(Enemies, Mag, 80)
                ),
                // 1本目の枝：威力を上げる。
                Stage(
                    "ManaBurst.1",
                    "マナバースト・烈",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Attack,
                    Enemies,
                    3,
                    "Revelation",
                    "敵全体に{0}ダメージ。",
                    Damage(Enemies, Mag, 100)
                ),
                Stage(
                    "ManaBurst.2",
                    "マナバースト・極",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Attack,
                    Enemies,
                    3,
                    "Revelation",
                    "敵全体に{0}ダメージ。",
                    Damage(Enemies, Mag, 130)
                ),
                // 2本目の枝：魔力の弾を散らす。
                Stage(
                    "ManaBurst.3",
                    "マナバースト・散",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Attack,
                    Random,
                    2,
                    "Revelation",
                    "ランダムな敵に{0}ダメージ×4。",
                    Damage(Random, Mag, 35, hits: 4)
                )
            ),
            Tree(
                "StarFall",
                Stage(
                    "StarFall",
                    "星降り",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Attack,
                    Random,
                    5,
                    "Meteor",
                    "ランダムな敵に{0}ダメージ×5。",
                    Damage(Random, Mag, 40, hits: 5)
                )
            ),
            // --- The archer (ルカ・グレタ・リツ) ----------------------------------------------
            Tree(
                "ThunderArrow",
                Stage(
                    "ThunderArrow",
                    "雷撃の矢",
                    CardUser.Luka,
                    CardElement.Thunder,
                    CardKind.Attack,
                    Enemy,
                    3,
                    "ThunderSpear",
                    "敵1体に{0}ダメージ。[麻痺]1ターン。",
                    Damage(Enemy, Phy, 120),
                    Apply(Enemy, CardStatus.Paralysis, 1)
                )
            ),
            Tree(
                "ArrowStorm",
                Stage(
                    "ArrowStorm",
                    "矢の雨",
                    CardUser.Luka,
                    CardElement.Pierce,
                    CardKind.Attack,
                    Enemies,
                    5,
                    "ArrowRain",
                    "敵全体に{0}ダメージ×3。",
                    Damage(Enemies, Phy, 45, hits: 3)
                )
            ),
            // --- The knight (アリア・アンセルム) ---------------------------------------------
            Tree(
                "ShieldStance",
                Stage(
                    "ShieldStance",
                    "盾の構え",
                    CardUser.Aria,
                    CardElement.None,
                    CardKind.Guard,
                    Self,
                    2,
                    "Protect",
                    "自分に[ブロック]{0}。",
                    Block(Self, Def, 80)
                )
            ),
            Tree(
                "Issen",
                Stage(
                    "Issen",
                    "一閃",
                    CardUser.Aria,
                    CardElement.Slash,
                    CardKind.Attack,
                    Enemy,
                    5,
                    "Iai",
                    "敵1体に{0}ダメージ。",
                    Damage(Enemy, Phy, 200)
                )
            ),
            // --- The healer (ミナ・リタ) -------------------------------------------------------
            Tree(
                "HealingWind",
                Stage(
                    "HealingWind",
                    "癒しの風",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Heal,
                    Allies,
                    3,
                    "Heal",
                    "味方全体のHPを{0}回復する。",
                    Heal(Allies, Mag, 60)
                )
            ),
            Tree(
                "Revive",
                Stage(
                    "Revive",
                    "蘇生",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Heal,
                    Ally,
                    6,
                    "Resurrection",
                    "倒れた味方1体を、HPを最大の半分にして復活させる。",
                    Simple(CardActionKind.Revive, Ally, 50)
                )
            ),
        };

        public static UniqueSkillTree Find(string id) =>
            string.IsNullOrEmpty(id)
                ? null
                : Array.Find((UniqueSkillTree[])All, tree => tree.Id == id);

        /// <summary>Every stage made so far, of every tree.</summary>
        public static IEnumerable<UniqueSkillStage> Stages()
        {
            foreach (var tree in All)
            foreach (var stage in tree.Stages)
                if (stage != null)
                    yield return stage;
        }

        /// <summary>The stage whose card has the id, or null.</summary>
        public static UniqueSkillStage FindStage(string card)
        {
            if (string.IsNullOrEmpty(card))
                return null;
            foreach (var stage in Stages())
                if (stage.Card.Id == card)
                    return stage;
            return null;
        }

        // 最初の段階に続けて、取得した順（1本目の枝の1段目、2段目、2本目の枝の1段目…）に段階を並べる。
        private static UniqueSkillTree Tree(string id, params UniqueSkillStage[] stages)
        {
            var all = new UniqueSkillStage[UniqueSkillTreeShape.Count];
            Array.Copy(stages, all, Math.Min(stages.Length, all.Length));
            return new UniqueSkillTree { Id = id, Stages = Array.AsReadOnly(all) };
        }

        private static UniqueSkillStage Stage(
            string id,
            string name,
            CardUser user,
            CardElement element,
            CardKind kind,
            CardTarget target,
            int cost,
            string art,
            string text,
            params CardAction[] actions
        ) =>
            new()
            {
                Art = "Card" + art,
                Card = new CardSkill
                {
                    Id = id,
                    Name = name,
                    User = user,
                    Element = element,
                    Kind = kind,
                    Target = target,
                    Cost = cost,
                    Text = text,
                    Actions = Array.AsReadOnly(actions),
                },
            };
    }
}
