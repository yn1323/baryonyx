namespace Baryonyx.Combat
{
    /// <summary>
    /// When a rule of an effect works: something that happened in the battle
    /// (doc/plans/2026-11-03-battle-effect-system.md, きっかけ). The battle asks the effects at
    /// each of these, with what happened in a <see cref="BattleEvent"/>.
    /// </summary>
    public enum BattleTrigger
    {
        /// <summary>The battle's cards are dealt anew.</summary>
        BattleStart,

        /// <summary>The party's turn begins, once its energy is filled.</summary>
        PartyTurnStart,

        /// <summary>The party's turn ends; the amount is the energy left.</summary>
        PartyTurnEnd,

        /// <summary>The enemies begin to act.</summary>
        EnemyTurnStart,

        /// <summary>The enemies have acted.</summary>
        EnemyTurnEnd,

        /// <summary>A card is about to do what it does: its user, the card and its play.</summary>
        CardPlaying,

        /// <summary>A card has done what it does; the amount is the damage its blows dealt.</summary>
        CardPlayed,

        /// <summary>A blow of the party (a card's, an effect's or a lasting one) landed on an enemy.</summary>
        DealtDamage,

        /// <summary>An enemy fell to a blow of the party.</summary>
        EnemyDefeated,

        /// <summary>
        /// An enemy's blow is about to land on an ally (the amount is its power before the ally's
        /// defense); <see cref="BattleAct.Cancel"/> stops it (無敵・反射).
        /// </summary>
        BlowLanding,

        /// <summary>An enemy's blow landed on an ally (the actor is the enemy).</summary>
        TookBlow,

        /// <summary>An ally fell to an enemy's blow.</summary>
        AllyFell,

        /// <summary>A status is about to be put on someone; <see cref="BattleAct.Cancel"/> stops it.</summary>
        StatusApplying,

        /// <summary>A status was put on someone.</summary>
        StatusApplied,

        /// <summary>Someone's block grew; the amount is how much.</summary>
        GainedBlock,

        /// <summary>Someone was healed; the amount is the healing, the overflow what went past the max HP.</summary>
        Healed,

        /// <summary>
        /// The holder uses the effect: an enemy's skill whose working is an effect
        /// (<see cref="EnemyMove.Effect"/>), once as it is used.
        /// </summary>
        Used,

        /// <summary>
        /// An enemy's blow on one ally picks its target; <see cref="BattleAct.TakeInstead"/>
        /// turns it on the effect's owner.
        /// </summary>
        EnemyTargeting,
    }

    /// <summary>
    /// A number of the battle that effects change (doc/plans/2026-11-03-battle-effect-system.md,
    /// 値): their fixed amounts are added, then their percents summed and grown once.
    /// </summary>
    public enum BattleValue
    {
        /// <summary>A blow of the party, before the enemy's defense.</summary>
        Damage,

        /// <summary>An enemy's blow on an ally, before the ally's defense (プロテクト).</summary>
        Incoming,

        /// <summary>
        /// The damage a blow does to whom it lands on, before the block: on an ally after its
        /// defense and a critical hit, on an enemy after its weakness (プロテクト). Not for a
        /// status's lasting damage.
        /// </summary>
        DamageTaken,

        /// <summary>The healing of a card or of regen.</summary>
        Heal,

        /// <summary>The block a card gives.</summary>
        Block,

        /// <summary>The energy a card in hand costs.</summary>
        Cost,

        /// <summary>How many times a damage that lands more than once lands.</summary>
        Hits,

        /// <summary>The cards dealt as the battle begins.</summary>
        OpeningDraw,

        /// <summary>The cards dealt as a party turn begins.</summary>
        TurnDraw,

        /// <summary>The energy a party turn begins with.</summary>
        TurnEnergy,

        /// <summary>The chance in percent that an attack card does what it does once more.</summary>
        TwiceChance,

        /// <summary>
        /// The percent a stat of the actor grows by in battle (背水・先手・弔い), for the stat of
        /// <see cref="BattleEvent.StatIndex"/> (<see cref="BattleStatBoosts"/> order).
        /// </summary>
        StatPercent,
    }

    /// <summary>What dealt a blow, which effects tell apart in their conditions.</summary>
    public enum BattleDamageKind
    {
        None,

        /// <summary>A card's damage.</summary>
        Card,

        /// <summary>A blow an effect deals (a counter, a bolt).</summary>
        Effect,

        /// <summary>
        /// A status's damage: burn, poison, bleeding, a sigil, a cloud or a reflected blow, or one
        /// more of it (傷口を開く).
        /// </summary>
        Status,

        /// <summary>An enemy's blow.</summary>
        Enemy,
    }

    /// <summary>How a blow an effect deals is shown.</summary>
    public enum BattleStrikeLook
    {
        Plain,

        /// <summary>A bolt from above, as a thundercloud strikes.</summary>
        Bolt,
    }
}
