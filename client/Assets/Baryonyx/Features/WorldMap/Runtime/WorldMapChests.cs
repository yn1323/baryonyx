using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.Inventory;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// What each treasure square of the highway holds (doc/features/stage-progression.md): always
    /// the same, given once when the square is first cleared. The server keeps the same contents
    /// (server/src/features/world-map/catalog.ts) and gives them; this copy shows them without a
    /// server (the showcase, or no server URL). Equipment and skills come with their quality, at
    /// the bottom of its range; materials are listed one by one. All contents are provisional.
    /// </summary>
    public static class WorldMapChests
    {
        private static readonly Dictionary<
            string,
            (AdventureDropCategory Category, string Id, ItemQuality? Quality)[]
        > BySquare = new()
        {
            // ピーノの西の寄り道。北の氷室村フレッダ（推奨Lv18）へ向かう前に。
            ["n8-5"] = new[]
            {
                Graded(AdventureDropCategory.Weapon, "frost-wand"),
                Material("polish-blue"),
                Material("polish-blue"),
            },
            // 内の輪の東の寄り道。鐘の町カンパネラ（推奨Lv30）へ向かう前に。
            ["n17-7"] = new[]
            {
                Graded(AdventureDropCategory.Accessory, "crystal-ring"),
                Material("polish-purple"),
            },
            // サッビアの北の寄り道。南の砂の町へ向かう前に。
            ["n15-15"] = new[]
            {
                Graded(AdventureDropCategory.Armor, "clay-armor"),
                Material("polish-blue"),
                Material("polish-blue"),
            },
            // ドゥーナの北の寄り道。
            ["n20-14"] = new[]
            {
                Graded(AdventureDropCategory.Weapon, "tomb-khopesh"),
                Material("polish-purple"),
            },
            // サッビアの南の寄り道。
            ["n16-18"] = new[]
            {
                Graded(AdventureDropCategory.Skill, "HolyLight"),
                Material("awakening-shard"),
            },
        };

        public static IEnumerable<string> Squares => BySquare.Keys;

        /// <summary>
        /// The contents of the treasure square <paramref name="squareId"/>, as drops of that
        /// square; empty when it is not a treasure square.
        /// </summary>
        public static IReadOnlyList<AdventureDrop> Of(string squareId)
        {
            if (squareId == null || !BySquare.TryGetValue(squareId, out var entries))
                return Array.Empty<AdventureDrop>();
            return entries
                .Select(entry =>
                    entry.Quality is { } quality
                        ? new AdventureDrop(
                            squareId,
                            entry.Category,
                            entry.Id,
                            Quality
                                .Range(
                                    quality,
                                    AdventureTexts.MaxMultiplierOf(entry.Category, entry.Id)
                                )
                                .Min,
                            quality
                        )
                        : new AdventureDrop(squareId, entry.Category, entry.Id)
                )
                .ToArray();
        }

        private static (AdventureDropCategory, string, ItemQuality?) Graded(
            AdventureDropCategory category,
            string id
        ) => (category, id, ItemQuality.A);

        private static (AdventureDropCategory, string, ItemQuality?) Material(string id) =>
            (AdventureDropCategory.Material, id, null);
    }
}
