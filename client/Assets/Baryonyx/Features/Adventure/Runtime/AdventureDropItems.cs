using System.Collections.Generic;
using Baryonyx.Equipment;
using Baryonyx.Inventory;
using Baryonyx.UI;
using UnityEngine;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// A drop as the plate that shows what was found draws it (<see cref="RewardReveal"/>): the
    /// category and the stars above the name, and the quality and the sparkle of the kind. The
    /// items, accessories, weapons and armour show their own art
    /// (<see cref="AdventureTexts.DropIcon(AdventureDrop, ItemArt)"/>).
    /// </summary>
    public static class AdventureDropItems
    {
        public static RewardItem From(AdventureDrop drop, ItemArt art = null)
        {
            if (drop == null)
                return null;
            string stars = EquipmentCatalog.Stars(drop.Stars);
            return new RewardItem
            {
                Kind = $"{AdventureDrops.NameOf(drop.Category)}　{stars}".TrimEnd(),
                Name = AdventureTexts.DropName(drop),
                Icon = AdventureTexts.DropIcon(drop, art),
                Quality = drop.Quality ?? ItemQuality.C,
                Graded = drop.Quality.HasValue,
                FoilStyle = FoilStyleOf(drop),
            };
        }

        /// <summary>
        /// The drops as plates, in their order, with the same material (the polishing stones
        /// that fall several at once) on one plate whose name ends in its count ("磨き石 ×3").
        /// </summary>
        public static List<RewardItem> All(IEnumerable<AdventureDrop> drops, ItemArt art = null)
        {
            var items = new List<RewardItem>();
            var materials = new Dictionary<string, (RewardItem Item, int Count)>();
            foreach (var drop in drops ?? System.Array.Empty<AdventureDrop>())
            {
                if (drop?.Category == AdventureDropCategory.Material)
                {
                    if (materials.TryGetValue(drop.Id, out var held))
                    {
                        materials[drop.Id] = (held.Item, held.Count + 1);
                        continue;
                    }
                    var plate = From(drop, art);
                    materials[drop.Id] = (plate, 1);
                    items.Add(plate);
                    continue;
                }
                var item = From(drop, art);
                if (item != null)
                    items.Add(item);
            }
            foreach (var (item, count) in materials.Values)
                if (count > 1)
                    item.Name += $" ×{count}";
            return items;
        }

        /// <summary>
        /// A clear's reward as the plates shown with the boss's drops: the thing (with its count)
        /// and the runes, each tagged "N回目のクリア報酬" at the top right (2026-11-01, the user).
        /// None when <paramref name="reward"/> is null.
        /// </summary>
        public static IReadOnlyList<RewardItem> FromClearReward(
            AdventureClearReward reward,
            ItemArt art,
            Sprite runeIcon
        )
        {
            if (reward == null)
                return System.Array.Empty<RewardItem>();
            string tag = ClearRewardTag(reward.Clears);
            var thing = From(reward.Drop, art);
            if (reward.Count > 1)
                thing.Name += $" ×{reward.Count}";
            thing.Tag = tag;
            var items = new List<RewardItem> { thing };
            if (reward.Runes > 0)
                items.Add(
                    new RewardItem
                    {
                        Kind = RunesKind,
                        Name = reward.Runes.ToString("N0"),
                        Icon = runeIcon,
                        Graded = false,
                        Tag = tag,
                    }
                );
            return items;
        }

        public const string RunesKind = "ルーン";

        public static string ClearRewardTag(int clears) => $"{clears}回目のクリア報酬";

        private static int FoilStyleOf(AdventureDrop drop) =>
            drop.Category switch
            {
                AdventureDropCategory.Weapon
                or AdventureDropCategory.Armor
                or AdventureDropCategory.Accessory => EquipmentCatalog.FoilStyleOf(drop.Id),
                _ => 0,
            };
    }
}
