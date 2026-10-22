using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>
    /// The turn order worked out from speed (doc/features/combat.md, the turn order, provisional):
    /// the party acts as one, at the average speed of the allies standing, and after each of its
    /// turns every enemy standing acts once, the faster first. One side
    /// <see cref="ConsecutiveRatio"/> times as fast as the other gets turns in a row: such an enemy
    /// acts twice after each party turn, and an enemy that slow only after every second one.
    /// </summary>
    public static class CombatTurnOrder
    {
        /// <summary>How many times as fast a side must be to act twice to the other's once (provisional).</summary>
        public const double ConsecutiveRatio = 2.5;

        /// <summary>
        /// The times an enemy of <paramref name="enemy"/> speed acts after the party's turn
        /// <paramref name="round"/> (1 for the battle's first), the party being of
        /// <paramref name="party"/> speed: 2, 1, or 1 and 0 by turns. Without speeds, once.
        /// </summary>
        public static int ActionsAfter(int round, double party, double enemy)
        {
            if (party <= 0 || enemy <= 0)
                return 1;
            if (enemy >= party * ConsecutiveRatio)
                return 2;
            if (party >= enemy * ConsecutiveRatio)
                return round % 2 == 0 ? 1 : 0;
            return 1;
        }

        /// <summary>
        /// The enemies (indexes of <paramref name="enemies"/>, their speeds; 0 or less for one
        /// not on the field) acting after the party's turn <paramref name="round"/>, in order:
        /// the faster first, by place when as fast, and those acting twice once more at the end.
        /// </summary>
        public static List<int> After(int round, double party, IReadOnlyList<double> enemies)
        {
            var standing = new List<int>();
            for (int i = 0; i < enemies.Count; i++)
                if (enemies[i] > 0)
                    standing.Add(i);
            standing.Sort(
                (a, b) =>
                {
                    int faster = enemies[b].CompareTo(enemies[a]);
                    return faster != 0 ? faster : a.CompareTo(b);
                }
            );
            var order = new List<int>();
            var again = new List<int>();
            foreach (int enemy in standing)
            {
                int times = ActionsAfter(round, party, enemies[enemy]);
                if (times >= 1)
                    order.Add(enemy);
                if (times >= 2)
                    again.Add(enemy);
            }
            order.AddRange(again);
            return order;
        }

        /// <summary>The party's speed: the average of the speeds given, 0 for none.</summary>
        public static double PartySpeed(IEnumerable<int> speeds)
        {
            double total = 0;
            int count = 0;
            foreach (int speed in speeds)
            {
                total += Math.Max(0, speed);
                count++;
            }
            return count == 0 ? 0 : total / count;
        }
    }
}
