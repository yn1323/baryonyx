using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>
    /// The battle as the effects see it: what they read and what they change. The battle screen
    /// is one, and its forecast another that works on a copy (<see cref="Preview"/>), so an
    /// effect written once works in both. A world that changes something tells the effects
    /// about it (<see cref="BattleEffects.Fire"/>): a blow landing, block gained, healing, a
    /// status put on.
    /// </summary>
    public interface IBattleWorld
    {
        /// <summary>True for a forecast: nothing shows, nothing is used up, nothing is left to chance.</summary>
        bool Preview { get; }

        /// <summary>The party's turn, 1 for the battle's first.</summary>
        int Turn { get; }

        /// <summary>The ACT walked today.</summary>
        int TodayAct { get; }

        /// <summary>The party's energy now.</summary>
        int Energy { get; }

        /// <summary>The allies' places, or the enemies, on the field (standing or not).</summary>
        int Count(bool allies);

        /// <summary>True while <paramref name="unit"/> stands (an ally not down, an enemy alive).</summary>
        bool Stands(BattleUnit unit);

        int Hp(BattleUnit unit);
        int MaxHp(BattleUnit unit);
        int Block(BattleUnit unit);
        bool HasStatus(BattleUnit unit, CardStatus status);

        /// <summary>The turns <paramref name="status"/> has left on <paramref name="unit"/>; 0 without it.</summary>
        int StatusTurns(BattleUnit unit, CardStatus status);

        /// <summary>The amount of <paramref name="status"/> on <paramref name="unit"/> (a buff's percent, poison's stacks); 0 without it.</summary>
        int StatusAmount(BattleUnit unit, CardStatus status);

        /// <summary>Adds the statuses on <paramref name="unit"/> to <paramref name="into"/>, in the order they were put on.</summary>
        void StatusesOf(BattleUnit unit, List<CardStatus> into);

        /// <summary>True while <paramref name="unit"/> is in the battle, standing or fallen (an empty place is not).</summary>
        bool InBattle(BattleUnit unit);

        /// <summary>The damage or healing <paramref name="status"/> on <paramref name="unit"/> does each time; 0 without it.</summary>
        int StatusPower(BattleUnit unit, CardStatus status);

        /// <summary>The stat of <paramref name="unit"/> a power is a part of, as it is now.</summary>
        int Stat(BattleUnit unit, CardStat stat);

        /// <summary>The defense of <paramref name="unit"/> against a power of <paramref name="stat"/>.</summary>
        int Defense(BattleUnit unit, CardStat stat);

        int Level(BattleUnit unit);

        /// <summary>A whole number from 0 up to, but not including, <paramref name="count"/>.</summary>
        int Pick(int count);

        /// <summary>
        /// Lands a blow of <paramref name="power"/> (before the target's weakness, protection and
        /// block) from <see cref="BattleEvent.Actor"/> on <see cref="BattleEvent.Target"/>, and
        /// tells the effects once it lands.
        /// </summary>
        void Strike(BattleEvent blow, int power, BattleStrikeLook look);

        /// <summary>Adds block to <paramref name="unit"/>; returns how much it grew.</summary>
        int AddBlock(BattleUnit unit, int amount);

        /// <summary><paramref name="healer"/> heals <paramref name="target"/> by <paramref name="amount"/>.</summary>
        void Heal(BattleUnit healer, BattleUnit target, int amount);

        /// <summary>
        /// <paramref name="source"/> puts <paramref name="status"/> for <paramref name="turns"/>
        /// (and <paramref name="amount"/>: poison's stacks, a buff's percent) on
        /// <paramref name="target"/>; a bad one lands at <paramref name="chance"/>% moved by luck
        /// (0: always). Returns true when it was put on.
        /// </summary>
        bool ApplyStatus(
            BattleUnit source,
            BattleUnit target,
            CardStatus status,
            int turns,
            int chance,
            int amount
        );

        /// <summary>Takes <paramref name="status"/> off <paramref name="unit"/> (a reflection used up).</summary>
        void RemoveStatus(BattleUnit unit, CardStatus status);

        /// <summary>Adds energy, up to the battle's cap; returns how much it grew.</summary>
        int GainEnergy(int amount);

        /// <summary>Keeps up to <paramref name="most"/> more of the energy left for the next turn; returns how much.</summary>
        int CarryEnergy(int most);

        /// <summary>Deals <paramref name="count"/> cards; returns how many it will deal.</summary>
        int Draw(int count);

        /// <summary>Shows that <paramref name="effect"/> worked, over <paramref name="at"/>, with <paramref name="detail"/>.</summary>
        void Announce(BattleEffectInstance effect, BattleUnit at, string detail);
    }
}
