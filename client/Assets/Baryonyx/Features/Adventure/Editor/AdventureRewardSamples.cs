using System.Collections.Generic;
using System.Linq;
using Baryonyx.Enhancement;
using Baryonyx.Inventory;
using Baryonyx.UI;

namespace Baryonyx.Adventure.Editor
{
    /// <summary>
    /// What the debugging rooms' drops and chests are made of (doc/features/screens/debug-rooms.md),
    /// kind by kind: the weapons, armour, accessories, items and skills the adventures drop, the
    /// rarest first, and the materials, each shown as a real drop of it is
    /// (<see cref="AdventureDropItems"/>).
    /// </summary>
    public static class AdventureRewardSamples
    {
        // How many things of each kind the rooms take in turn.
        private const int PerKind = 6;

        private static readonly AdventureDropCategory[] Kinds =
        {
            AdventureDropCategory.Weapon,
            AdventureDropCategory.Armor,
            AdventureDropCategory.Accessory,
            AdventureDropCategory.Item,
            AdventureDropCategory.Skill,
            AdventureDropCategory.Material,
        };

        private static readonly string[] Materials =
        {
            EnhancementMaterials.Gold,
            EnhancementMaterials.Purple,
            EnhancementMaterials.Blue,
            EnhancementMaterials.White,
            EnhancementMaterials.Shard,
        };

        public static RewardSampleSet[] Sets(ItemArt art)
        {
            var sets = new List<RewardSampleSet>();
            foreach (var kind in Kinds)
            {
                var ids =
                    kind == AdventureDropCategory.Material
                        ? Materials
                        : AdventureDrops
                            .Pools.Values.SelectMany(pool => pool)
                            .Where(entry => entry.Category == kind)
                            .GroupBy(entry => entry.Id)
                            .Select(entry => entry.First())
                            .OrderByDescending(entry => entry.Stars)
                            .ThenBy(entry => entry.Id)
                            .Take(PerKind)
                            .Select(entry => entry.Id)
                            .ToArray();
                bool graded = kind
                    is not (AdventureDropCategory.Item or AdventureDropCategory.Material);
                var items = ids.Select(id =>
                        AdventureDropItems.From(
                            new AdventureDrop(
                                "lab",
                                kind,
                                id,
                                graded ? 1f : 0f,
                                graded ? ItemQuality.S : null
                            ),
                            art
                        )
                    )
                    .Where(item => item != null)
                    .ToArray();
                if (items.Length > 0)
                    sets.Add(
                        new RewardSampleSet { Name = AdventureDrops.NameOf(kind), Items = items }
                    );
            }
            return sets.ToArray();
        }
    }
}
