using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>
    /// The turn order worked out from speed (doc/features/combat.md, the turn order, provisional):
    /// the party acts as one, at the average speed of the allies standing, and in each round every
    /// enemy standing acts once, the faster first: an enemy faster than the party before the
    /// party's turn, one as fast or slower after it. So the battle starts with the enemies faster
    /// than the party (<see cref="Opening"/>), and between two party turns come the slower enemies
    /// of the round and the faster ones of the next. One side <see cref="ConsecutiveRatio"/> times
    /// as fast as the other gets turns in a row: such an enemy acts once more after the party's
    /// turn, and an enemy that slow only in every second round.
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
        /// not on the field) acting between the party's turn <paramref name="round"/> (1 for the
        /// battle's first) and its next, in order: the slower ones of the round, the faster first
        /// and by place when as fast; those acting twice once more; then the ones faster than the
        /// party, whose next round comes before the party's next turn.
        /// </summary>
        public static List<int> After(int round, double party, IReadOnlyList<double> enemies)
        {
            var order = new List<int>();
            var again = new List<int>();
            var standing = Standing(enemies);
            foreach (int enemy in standing)
            {
                int times = ActionsAfter(round, party, enemies[enemy]);
                if (!Faster(party, enemies[enemy]) && times >= 1)
                    order.Add(enemy);
                if (times >= 2)
                    again.Add(enemy);
            }
            order.AddRange(again);
            order.AddRange(standing.FindAll(enemy => Faster(party, enemies[enemy])));
            return order;
        }

        /// <summary>
        /// The enemies faster than the party, the faster first: they act before the party's first
        /// turn, and before each of its turns after it.
        /// </summary>
        public static List<int> Opening(double party, IReadOnlyList<double> enemies) =>
            Standing(enemies).FindAll(enemy => Faster(party, enemies[enemy]));

        // パーティより速い敵は、そのラウンドの味方の番より先に動く。速度がわからなければ後に回す。
        private static bool Faster(double party, double enemy) => party > 0 && enemy > party;

        // 場にいる敵を、速い順（同じなら並びの順）に。
        private static List<int> Standing(IReadOnlyList<double> enemies)
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
            return standing;
        }

        /// <summary>The party's speed: the average of the speeds given, 0 for none.</summary>
        public static double PartySpeed(IEnumerable<double> speeds)
        {
            double total = 0;
            int count = 0;
            foreach (double speed in speeds)
            {
                total += Math.Max(0, speed);
                count++;
            }
            return count == 0 ? 0 : total / count;
        }
    }
}
