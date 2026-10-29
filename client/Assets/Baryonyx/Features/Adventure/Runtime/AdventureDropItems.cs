using Baryonyx.Equipment;
using Baryonyx.Inventory;
using Baryonyx.UI;

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
