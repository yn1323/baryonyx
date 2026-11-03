namespace Baryonyx.Combat
{
    /// <summary>What a landed blow did, so its effect can hit as hard.</summary>
    public enum BattleBlowWeight
    {
        Normal,
        Weak,
        Defeat,
    }

    /// <summary>
    /// The battle as a card's actions see it (<see cref="CardHits"/>): an
    /// <see cref="IBattleWorld"/> that also works out a card's power and lands its blows,
    /// healing, block, statuses and the rest. The battle screen, its forecast and a battle
    /// without a screen are each one, so a card's actions are
    /// written once for all three.
    /// </summary>
    public interface ICardWorld : IBattleWorld
    {
        /// <summary>
        /// The power of <paramref name="action"/> for <paramref name="user"/>: a part of the stat,
        /// with today's ACT and the card's own boost (眠り札・次のカードの上乗せ).
        /// </summary>
        int CardPower(CardAction action, BattleUnit user);

        /// <summary>
        /// True when a blow of <paramref name="element"/> from <paramref name="user"/> hits a
        /// weakness of <paramref name="enemy"/> (a forecast counts only those revealed).
        /// </summary>
        bool IsWeak(BattleUnit enemy, CardElement element, BattleUnit user);

        /// <summary>Rolls whether <paramref name="user"/>'s blow is a critical hit (never in a forecast).</summary>
        bool RollCritical(BattleUnit user);

        /// <summary>
        /// Lands a card's blow of <paramref name="power"/> (after a critical hit, before the
        /// weakness, protection and block) and tells the effects.
        /// </summary>
        BattleBlowWeight LandCardBlow(BattleEvent blow, int power, bool weak, bool critical);

        /// <summary><paramref name="healer"/>'s card heals <paramref name="target"/>.</summary>
        void LandCardHeal(BattleUnit healer, BattleUnit target, int amount);

        /// <summary>Heals <paramref name="percent"/>% of the max HP, bringing a fallen ally back.</summary>
        void LandCardRevive(BattleUnit target, int percent);

        /// <summary>
        /// <paramref name="target"/> gains a card's block; <paramref name="party"/> when it is for
        /// the whole party (shown once, on the <paramref name="first"/> landing).
        /// </summary>
        void LandCardBlock(BattleUnit user, BattleUnit target, int block, bool party, bool first);

        /// <summary>A card's status lands on <paramref name="target"/> (a bad one on an enemy by its chance).</summary>
        void LandCardStatus(BattleUnit user, BattleUnit target, CardAction action);

        /// <summary>
        /// A card's action on the energy, the hand, the next card or what is known of the target
        /// (エネルギー・引く・コスト・次のカード・弱点・遅らせる・浄化・解除); a forecast leaves them out.
        /// </summary>
        void LandCardSupport(BattleUnit user, BattleUnit target, CardAction action, bool first);
    }
}
