using System;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// How a card skill is written on a card of the battle mock: the kind line with the kind in
    /// its colour, and the description with each number in the colour of what it does (damage
    /// yellow, healing green, block blue). Used by the screen generator and the screen alike.
    /// </summary>
    public static class BattleCardText
    {
        /// <summary>The colour of the separator dot between the kind and the scope.</summary>
        public const string SeparatorHex = "9a9483";

        /// <summary>"攻撃・敵単体": the kind in its colour, then who it reaches (rich text).</summary>
        public static string KindLine(CardSkill skill) =>
            $"<color=#{KindHex(skill.Kind)}>{CardRules.KindName(skill.Kind)}</color>"
            + $"<color=#{SeparatorHex}>・</color>{CardRules.ScopeName(skill)}";

        /// <summary>The description with the powers from <paramref name="stat"/> put in, coloured (rich text).</summary>
        public static string Description(CardSkill skill, Func<CardStat, int> stat, int upt) =>
            CardRules.Describe(
                skill,
                stat,
                upt,
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

        /// <summary>The mock's element of a card's element.</summary>
        public static BattleInspectElement ElementOf(CardElement element) =>
            element switch
            {
                CardElement.Slash => BattleInspectElement.Slash,
                CardElement.Fire => BattleInspectElement.Fire,
                CardElement.Ice => BattleInspectElement.Ice,
                CardElement.Thunder => BattleInspectElement.Thunder,
                CardElement.Blunt => BattleInspectElement.Blunt,
                CardElement.Pierce => BattleInspectElement.Pierce,
                _ => BattleInspectElement.None,
            };

        /// <summary>A card's element from the mock's.</summary>
        public static CardElement ElementOf(BattleInspectElement element) =>
            element switch
            {
                BattleInspectElement.Slash => CardElement.Slash,
                BattleInspectElement.Fire => CardElement.Fire,
                BattleInspectElement.Ice => CardElement.Ice,
                BattleInspectElement.Thunder => CardElement.Thunder,
                BattleInspectElement.Blunt => CardElement.Blunt,
                BattleInspectElement.Pierce => CardElement.Pierce,
                _ => CardElement.None,
            };

        /// <summary>
        /// How the card is played in the mock: on one enemy, on the enemies' side, on one ally
        /// (healing or not), or for the party (its block, or anything else for itself or no one).
        /// </summary>
        public static BattleInspectCardEffect EffectOf(CardSkill skill) =>
            skill.Target switch
            {
                CardTarget.OneEnemy => BattleInspectCardEffect.DamageOne,
                CardTarget.AllEnemies or CardTarget.RandomEnemies or CardTarget.ChainEnemies =>
                    BattleInspectCardEffect.DamageAll,
                CardTarget.OneAlly => skill.Kind == CardKind.Heal
                    ? BattleInspectCardEffect.Heal
                    : BattleInspectCardEffect.OneAlly,
                _ => skill.Kind == CardKind.Guard && skill.Target == CardTarget.AllAllies
                    ? BattleInspectCardEffect.Guard
                    : BattleInspectCardEffect.Party,
            };
    }
}
