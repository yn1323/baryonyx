using System;
using Baryonyx.Combat;

namespace Baryonyx.UI.Cards
{
    /// <summary>
    /// How a card skill is written wherever it is shown (the battle's cards, the tavern's card
    /// skills): the kind line with the kind in its colour, and the description with each number in
    /// the colour of what it does (damage yellow, healing green, block blue). Rich text.
    /// </summary>
    public static class CardText
    {
        /// <summary>The colour of the separator dot between the kind and the scope.</summary>
        public const string SeparatorHex = "9a9483";

        /// <summary>"攻撃・敵単体": the kind in its colour, then who it reaches (rich text).</summary>
        public static string KindLine(CardSkill skill) =>
            $"<color=#{KindHex(skill.Kind)}>{CardRules.KindName(skill.Kind)}</color>"
            + $"<color=#{SeparatorHex}>・</color>{CardRules.ScopeName(skill)}";

        /// <summary>The description with the powers from <paramref name="stat"/> put in, coloured (rich text).</summary>
        public static string Description(CardSkill skill, Func<CardStat, int> stat, int act) =>
            CardRules.Describe(
                skill,
                stat,
                act,
                (action, power) => $"<color=#{PowerHex(action)}>{power}</color>"
            );

        public static string KindHex(CardKind kind) =>
            kind switch
            {
                CardKind.Heal => "96e678",
                CardKind.Guard => "96c8ff",
                CardKind.Buff => "ffcc66",
                CardKind.Debuff => "d2a0ff",
                CardKind.Support => "c8d2dc",
                _ => "ff966e",
            };

        /// <summary>The colour of an action's number: healing green, block blue, damage yellow.</summary>
        public static string PowerHex(CardAction action) =>
            action.Kind switch
            {
                CardActionKind.Heal => "8ce878",
                CardActionKind.Status when action.Status == CardStatus.Regen => "8ce878",
                CardActionKind.Block => "96c8ff",
                _ => "ffce60",
            };
    }
}
