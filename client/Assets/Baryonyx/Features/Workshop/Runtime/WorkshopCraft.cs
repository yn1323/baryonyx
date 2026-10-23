using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.Combat;
using Baryonyx.Equipment;
using Baryonyx.Inventory;
using Baryonyx.Library;

namespace Baryonyx.Workshop
{
    /// <summary>One thing the trading house can make: a found kind of ☆2 or more.</summary>
    public sealed class WorkshopRecipe
    {
        public WorkshopRecipe(LibraryEntryData entry, int stars, float maxMultiplier)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Stars = stars;
            MaxMultiplier = maxMultiplier;
        }

        public LibraryEntryData Entry { get; }
        public LibraryKind Kind => Entry.Kind;
        public string Id => Entry.Id;
        public string Name => string.IsNullOrEmpty(Entry.Name) ? Entry.Id : Entry.Name;
        public int Stars { get; }
        public float MaxMultiplier { get; }
        public int Runes => WorkshopCraft.RunesOf(Stars);

        // 材料の☆。出来上がる物より1つ低い。
        public int MaterialStars => Stars - 1;
    }

    /// <summary>
    /// The trading house's craft (doc/features/synthesis.md, provisional rules of 2026-10-24):
    /// three things of the same kind one star lower make one found thing of the kind, for runes.
    /// The made thing's quality is the materials' average place in their ranges, so better
    /// materials make a better thing. Items have no quality. Things in use are not materials,
    /// and an item keeps as many as the deck slots it is in. The server decides the same again
    /// (server/src/features/workshop/catalog.ts and repository.ts).
    /// </summary>
    public static class WorkshopCraft
    {
        public const int MaterialCount = 3;

        // 費用（ルーン）：出来上がる物の☆で、買う値段の半分（仮）。
        private static readonly int[] StarRunes = { 0, 200, 400, 800, 1600 };

        public static int RunesOf(int stars) =>
            stars >= 2 && stars <= StarRunes.Length ? StarRunes[stars - 1] : 0;

        /// <summary>
        /// The things that can be made: found entries of the stock's kinds with ☆2 or more, but
        /// not the lords' things (that fall only in the deepest room), kind by kind, the lower
        /// stars first.
        /// </summary>
        public static IReadOnlyList<WorkshopRecipe> Recipes(
            LibraryData data,
            LibraryRecords records
        )
        {
            if (data == null || records == null)
                return Array.Empty<WorkshopRecipe>();
            return WorkshopStock
                .Kinds.SelectMany(kind =>
                    data.Entries.Where(entry =>
                            entry != null && entry.Kind == kind && records.IsFound(kind, entry.Id)
                        )
                        .Select(entry => (entry, stars: WorkshopStock.StarsOf(kind, entry.Id)))
                        .Where(pair => pair.stars >= 2 && !BossOnly(kind, pair.entry.Id))
                        .OrderBy(pair => pair.stars)
                        .Select(pair => new WorkshopRecipe(
                            pair.entry,
                            pair.stars,
                            MaxOf(kind, pair.entry.Id)
                        ))
                )
                .ToArray();
        }

        /// <summary>
        /// What can go into <paramref name="recipe"/>: the held things of its kind one star lower
        /// that nobody uses, the worse first. Items come as one unit each, up to three of a kind,
        /// keeping as many as the deck slots they are in.
        /// </summary>
        public static IReadOnlyList<WorkshopOwned> Materials(
            WorkshopRecipe recipe,
            IEnumerable<WorkshopOwned> owned
        )
        {
            if (recipe == null)
                return Array.Empty<WorkshopOwned>();
            var fit = (owned ?? Array.Empty<WorkshopOwned>()).Where(thing =>
                thing.Kind == recipe.Kind && thing.Stars == recipe.MaterialStars
            );
            if (recipe.Kind != LibraryKind.Item)
                return fit.Where(thing => thing.User == null)
                    .OrderBy(thing => Quality.Percent(thing.Multiplier))
                    .ThenBy(thing => thing.KindId, StringComparer.Ordinal)
                    .ToArray();
            var units = new List<WorkshopOwned>();
            foreach (var item in fit.OrderByDescending(thing => thing.Count))
            {
                int spare = Math.Min(MaterialCount, item.Count - item.Reserved);
                for (int i = 0; i < spare; i++)
                {
                    // デッキに入れた分は残してあるので、1つずつの札は使っていない物として扱う。
                    var unit = item.Copy();
                    unit.Count = 1;
                    unit.User = null;
                    unit.Reserved = 0;
                    units.Add(unit);
                }
            }
            return units;
        }

        /// <summary>The materials chosen when a recipe opens: the worse ones first, up to three.</summary>
        public static IReadOnlyList<int> FirstChoice(IReadOnlyList<WorkshopOwned> materials) =>
            Enumerable.Range(0, Math.Min(MaterialCount, materials?.Count ?? 0)).ToArray();

        /// <summary>Whether enough materials are held to make <paramref name="recipe"/>.</summary>
        public static bool CanMake(WorkshopRecipe recipe, IEnumerable<WorkshopOwned> owned) =>
            Materials(recipe, owned).Count >= MaterialCount;

        /// <summary>
        /// The made thing's multiplier: the materials' average place in their ranges, put on the
        /// made thing's range and rounded to a whole hundredth. ×1.00 for items.
        /// </summary>
        public static float Multiplier(
            WorkshopRecipe recipe,
            IReadOnlyList<WorkshopOwned> materials
        )
        {
            if (
                recipe == null
                || recipe.Kind == LibraryKind.Item
                || materials == null
                || materials.Count == 0
            )
                return Quality.MinMultiplier;
            // サーバーと同じく百分率の整数で計算する。
            double place = materials.Average(thing => (double)PlaceOf(thing));
            int width = Quality.Percent(recipe.MaxMultiplier) - 100;
            return (100 + (int)Math.Round(place * width, MidpointRounding.AwayFromZero)) / 100f;
        }

        private static double PlaceOf(WorkshopOwned thing)
        {
            int width = Quality.Percent(thing.MaxMultiplier) - 100;
            return width <= 0 ? 0 : (Quality.Percent(thing.Multiplier) - 100) / (double)width;
        }

        /// <summary>The upper limit of a kind's multiplier (×1.00 for items).</summary>
        public static float MaxOf(LibraryKind kind, string id) =>
            kind switch
            {
                LibraryKind.Skill => CardSkillQuality.LimitOf(CardSkills.Find(id)),
                LibraryKind.Item => Quality.MinMultiplier,
                _ => EquipmentCatalog.Find(id)?.MaxMultiplier ?? Quality.MinMultiplier,
            };

        /// <summary>Whether the kind falls only in the deepest room (a lord's thing).</summary>
        public static bool BossOnly(LibraryKind kind, string id)
        {
            var category = CategoryOf(kind);
            return category != null
                && AdventureDrops
                    .Pools.Values.SelectMany(pool => pool)
                    .Any(entry => entry.Category == category && entry.Id == id && entry.BossOnly);
        }

        /// <summary>The server's name of the kind.</summary>
        public static string KeyOf(LibraryKind kind) =>
            CategoryOf(kind) is { } category ? AdventureDrops.KeyOf(category) : null;

        private static AdventureDropCategory? CategoryOf(LibraryKind kind) =>
            kind switch
            {
                LibraryKind.Weapon => AdventureDropCategory.Weapon,
                LibraryKind.Armor => AdventureDropCategory.Armor,
                LibraryKind.Accessory => AdventureDropCategory.Accessory,
                LibraryKind.Skill => AdventureDropCategory.Skill,
                LibraryKind.Item => AdventureDropCategory.Item,
                _ => null,
            };
    }
}
