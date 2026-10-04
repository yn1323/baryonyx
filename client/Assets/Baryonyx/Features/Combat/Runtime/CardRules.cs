using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>
    /// How a card's numbers and words are worked out (provisional: doc/features/combat.md). A
    /// power is a part of the user's stat, grown by today's ACT for the cards that walk with it.
    /// </summary>
    public static class CardRules
    {
        /// <summary>ACT counted per step of a card's ACT bonus.</summary>
        public const int ActPerStep = 1000;

        /// <summary>
        /// The power of an action from its user's <paramref name="stat"/>, with today's
        /// <paramref name="act"/> for the cards that grow with it. Rounded half away from zero.
        /// </summary>
        public static int Power(CardAction action, int stat, int act = 0)
        {
            if (action == null || !action.HasPower)
                return 0;
            double power = stat * action.Percent / 100.0;
            power *= 1 + ActBonusPercent(action, act) / 100.0;
            return (int)Math.Round(power, MidpointRounding.AwayFromZero);
        }

        /// <summary>How much today's <paramref name="act"/> grows the action, in percent (0 to its cap).</summary>
        public static int ActBonusPercent(CardAction action, int act)
        {
            if (action == null || action.ActStepPercent <= 0 || act <= 0)
                return 0;
            int bonus = act / ActPerStep * action.ActStepPercent;
            return action.ActCapPercent > 0 ? Math.Min(bonus, action.ActCapPercent) : bonus;
        }

        /// <summary>The power when its bonus holds (a weakness hit, a burning target).</summary>
        public static int WithBonus(CardAction action, int power) =>
            action == null || action.BonusPercent <= 0
                ? power
                : (int)
                    Math.Round(
                        power * (1 + action.BonusPercent / 100.0),
                        MidpointRounding.AwayFromZero
                    );

        /// <summary>
        /// The description with each powered action's number put in by <paramref name="paint"/>
        /// (given the action and its power), e.g. to colour it.
        /// </summary>
        public static string Describe(
            CardSkill card,
            Func<CardStat, int> stat,
            int act,
            Func<CardAction, int, string> paint = null
        )
        {
            var numbers = new List<object>();
            foreach (var action in card.Powered())
            {
                int power = Power(action, stat(action.Stat), act);
                numbers.Add(paint != null ? paint(action, power) : power.ToString());
            }
            return string.Format(card.Text, numbers.ToArray());
        }

        /// <summary>The kind as shown on the card ("攻撃").</summary>
        public static string KindName(CardKind kind) =>
            kind switch
            {
                CardKind.Attack => "攻撃",
                CardKind.Heal => "回復",
                CardKind.Guard => "防御",
                CardKind.Buff => "強化",
                CardKind.Debuff => "弱体",
                _ => "支援",
            };

        /// <summary>Who or what the card reaches, as shown after its kind ("敵単体").</summary>
        public static string ScopeName(CardSkill card)
        {
            switch (card.Target)
            {
                case CardTarget.OneEnemy:
                    return "敵単体";
                case CardTarget.AllEnemies:
                    return "敵全体";
                case CardTarget.RandomEnemies:
                case CardTarget.ChainEnemies:
                    return "敵ランダム";
                case CardTarget.OneAlly:
                    return "味方単体";
                case CardTarget.AllAllies:
                    return "味方全体";
                case CardTarget.Self:
                    return "自分";
            }
            // A card for no one is named by what it works on.
            var first = card.Actions.Count > 0 ? card.Actions[0] : null;
            return first?.Kind switch
            {
                CardActionKind.CostDown => "手札",
                CardActionKind.Draw => "山札",
                CardActionKind.Energy => "エネルギー",
                CardActionKind.NextCardFree => "次のカード",
                CardActionKind.Status when first.Status == CardStatus.Thundercloud => "敵ランダム",
                _ => "全体",
            };
        }

        /// <summary>The name of a status as it pops up over a character.</summary>
        public static string StatusName(CardStatus status) =>
            status switch
            {
                CardStatus.Bleed => "出血",
                CardStatus.Burn => "やけど",
                CardStatus.Poison => "毒",
                CardStatus.Chill => "凍え",
                CardStatus.Freeze => "凍結",
                CardStatus.Paralysis => "麻痺",
                CardStatus.Vulnerable => "防御ダウン",
                CardStatus.BlastSigil => "爆炎の刻印",
                CardStatus.Thundercloud => "雷雲",
                CardStatus.AttackUp => "攻撃力アップ",
                CardStatus.SpeedUp => "速度アップ",
                CardStatus.Regen => "リジェネ",
                CardStatus.Taunt => "挑発",
                CardStatus.Reflect => "反射",
                CardStatus.FireBlade => "炎の加護",
                CardStatus.Protect => "プロテクト",
                CardStatus.Invincible => "無敵",
                _ => "",
            };

        /// <summary>True for the statuses that hurt or hinder (cleared by a cleanse).</summary>
        public static bool IsBad(CardStatus status) =>
            status
                is CardStatus.Bleed
                    or CardStatus.Burn
                    or CardStatus.Poison
                    or CardStatus.Chill
                    or CardStatus.Freeze
                    or CardStatus.Paralysis
                    or CardStatus.Vulnerable
                    or CardStatus.BlastSigil;

        /// <summary>
        /// The damage over time a status deals each turn, as a part of the stat of the ally who
        /// put it on (poison: per stack). Zero for the others.
        /// </summary>
        public static (CardStat stat, int percent) TickOf(CardStatus status) =>
            status switch
            {
                CardStatus.Burn => (CardStat.MagicAttack, 20),
                CardStatus.Poison => (CardStat.PhysicalAttack, 10),
                CardStatus.Bleed => (CardStat.PhysicalAttack, 20),
                _ => (CardStat.None, 0),
            };

        /// <summary>
        /// The targets of a chain of <paramref name="hits"/> from <paramref name="first"/> among
        /// <paramref name="count"/> targets: each next hit jumps to another at random (from
        /// <paramref name="pick"/>, given how many to choose from), never the one just hit while
        /// another is left.
        /// </summary>
        public static int[] Chain(int first, int count, int hits, Func<int, int> pick)
        {
            var order = new int[Math.Max(0, hits)];
            if (order.Length == 0 || count <= 0)
                return order;
            order[0] = first;
            for (int i = 1; i < order.Length; i++)
            {
                if (count == 1)
                {
                    order[i] = order[i - 1];
                    continue;
                }
                // Pick among the others, skipping the last one hit.
                int next = pick(count - 1);
                order[i] = next >= order[i - 1] ? next + 1 : next;
            }
            return order;
        }
    }
}
