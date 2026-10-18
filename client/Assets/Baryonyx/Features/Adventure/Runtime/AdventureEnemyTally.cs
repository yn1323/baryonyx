using System;
using System.Collections.Generic;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// One kind of enemy met in a battle and how many of them the party beat, sent with the
    /// room's end for the library (doc/features/library.md, the enemies' record).
    /// </summary>
    public readonly struct AdventureEnemyTally
    {
        public AdventureEnemyTally(string enemyId, int defeated)
        {
            EnemyId = enemyId ?? "";
            Defeated = Math.Max(0, defeated);
        }

        public string EnemyId { get; }
        public int Defeated { get; }

        /// <summary>
        /// Counts the enemies of a battle by kind: each (kind, beaten) pair is one enemy that
        /// took part. Kinds without an id are left out.
        /// </summary>
        public static IReadOnlyList<AdventureEnemyTally> Count(
            IEnumerable<(string EnemyId, bool Beaten)> enemies
        )
        {
            var order = new List<string>();
            var beaten = new Dictionary<string, int>();
            foreach (var (id, down) in enemies ?? Array.Empty<(string, bool)>())
            {
                if (string.IsNullOrEmpty(id))
                    continue;
                if (!beaten.ContainsKey(id))
                {
                    beaten[id] = 0;
                    order.Add(id);
                }
                if (down)
                    beaten[id]++;
            }
            var tallies = new AdventureEnemyTally[order.Count];
            for (int i = 0; i < order.Count; i++)
                tallies[i] = new AdventureEnemyTally(order[i], beaten[order[i]]);
            return tallies;
        }
    }
}
