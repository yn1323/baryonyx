namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// How a card skill maps onto the battle mock: its element as the mock's element, and how it
    /// is played. How a card is written (the kind line, the coloured description) is shared with
    /// the tavern's card skills in <see cref="Baryonyx.UI.Cards.CardText"/>.
    /// </summary>
    public static class BattleCardText
    {
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
