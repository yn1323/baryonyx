using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>
    /// One status on a character in battle: the party turns it has left, its amount (poison's
    /// stacks, a buff's percent), the damage or healing it does each time, and who put it on.
    /// </summary>
    public sealed class BattleStatusEntry
    {
        public CardStatus Status;
        public int Turns;

        /// <summary>Poison's stacks, or the part (percent) a buff or debuff changes.</summary>
        public int Amount;

        /// <summary>The damage or healing it does each time it works.</summary>
        public int Power;

        /// <summary>The ally who put it on (an index of the allies), or -1.</summary>
        public int Source = -1;

        /// <summary>
        /// The enemy who put it on an ally, or -1: such a status counts down as the party's turn
        /// ends, so it lasts through the party's turns it is for.
        /// </summary>
        public int Enemy = -1;
    }

    /// <summary>
    /// How statuses stack, tick and run out (provisional: doc/features/combat/cards.md,
    /// カードの状態の仮設定), the same for the battle screen and a battle without one.
    /// </summary>
    public static class BattleStatusRules
    {
        /// <summary>
        /// Puts <paramref name="status"/> on a character whose statuses are
        /// <paramref name="list"/>: on again, the longer turns and the stronger power stay, and
        /// poison stacks up. Returns the status as it is now.
        /// </summary>
        public static BattleStatusEntry Put(
            List<BattleStatusEntry> list,
            CardStatus status,
            int turns,
            int power,
            int amount,
            int source,
            int enemy = -1
        )
        {
            var entry = Find(list, status);
            if (entry == null)
            {
                entry = new BattleStatusEntry
                {
                    Status = status,
                    Source = source,
                    Enemy = enemy,
                };
                list.Add(entry);
            }
            entry.Turns = Math.Max(entry.Turns, turns);
            entry.Power = Math.Max(entry.Power, power);
            entry.Amount =
                status == CardStatus.Poison
                    ? entry.Amount + amount
                    : Math.Max(entry.Amount, amount);
            return entry;
        }

        public static BattleStatusEntry Find(List<BattleStatusEntry> list, CardStatus status)
        {
            foreach (var entry in list)
                if (entry.Status == status)
                    return entry;
            return null;
        }

        /// <summary>
        /// The damage a lasting status does each time, from the stat of the one who put it on
        /// (<see cref="CardRules.TickOf"/>), at least 1; 0 for a status without one.
        /// </summary>
        public static int TickPower(CardStatus status, int stat)
        {
            var (_, percent) = CardRules.TickOf(status);
            return percent > 0 ? Math.Max(1, (int)Math.Round(stat * percent / 100f)) : 0;
        }

        /// <summary>The damage a burn or poison does as the party's turn comes round (poison: by its stacks).</summary>
        public static int TickDamage(BattleStatusEntry entry) =>
            entry.Status == CardStatus.Poison ? entry.Power * entry.Amount : entry.Power;

        /// <summary>
        /// A party turn comes round: every lasting status counts down a turn and goes at 0;
        /// poison wanes by its stacks instead, and freezing and reflection wait to be used. With
        /// <paramref name="fromEnemies"/> (as the party's turn ends), only the statuses an enemy
        /// put on an ally count down, and without it only the others.
        /// </summary>
        public static void Countdown(List<BattleStatusEntry> list, bool fromEnemies = false)
        {
            for (int n = list.Count - 1; n >= 0; n--)
            {
                var entry = list[n];
                if (entry.Status is CardStatus.Poison or CardStatus.Freeze or CardStatus.Reflect)
                    continue;
                if ((entry.Enemy >= 0) != fromEnemies)
                    continue;
                if (--entry.Turns <= 0)
                    list.RemoveAt(n);
            }
        }
    }
}
