using Baryonyx.Equipment;
using Baryonyx.Inventory;
using Baryonyx.StepBonus;
using Baryonyx.UI;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// A drop as the plate that shows what was found draws it (<see cref="RewardReveal"/>): the
    /// category and the stars above the name, the quality and the sparkle of the kind, and what
    /// became of it. The ACT bonuses show their icon, and the items, accessories, weapons and
    /// armour their own art (<see cref="AdventureTexts.DropIcon(StepBonusMockData, AdventureDrop, ItemArt)"/>).
    /// </summary>
    public static class AdventureDropItems
    {
        public static RewardItem From(
            StepBonusMockData bonuses,
            AdventureDrop drop,
            ItemArt art = null
        )
        {
            if (drop == null)
                return null;
            string stars = EquipmentCatalog.Stars(drop.Stars);
            return new RewardItem
            {
                Kind = $"{AdventureDrops.NameOf(drop.Category)}　{stars}".TrimEnd(),
                Name = AdventureTexts.DropName(bonuses, drop),
                Icon = AdventureTexts.DropIcon(bonuses, drop, art),
                Quality = drop.Quality ?? ItemQuality.C,
                Graded = drop.Quality.HasValue,
                FoilStyle = FoilStyleOf(bonuses, drop),
                Note = AdventureCatalog.Outcome(drop.Outcome),
            };
        }

        private static int FoilStyleOf(StepBonusMockData bonuses, AdventureDrop drop) =>
            drop.Category switch
            {
                AdventureDropCategory.Bonus => bonuses != null ? bonuses.FoilStyleOf(drop.Id) : 0,
                AdventureDropCategory.Weapon
                or AdventureDropCategory.Armor
                or AdventureDropCategory.Accessory => EquipmentCatalog.FoilStyleOf(drop.Id),
                _ => 0,
            };
    }
}
