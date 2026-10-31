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
    /// <summary>
    /// One thing the trading house can make from its recipe: a weapon, armor, accessory or
    /// custom skill of ☆2 or more, shown once any of its materials has been found.
    /// </summary>
    public sealed class WorkshopRecipe
    {
        public WorkshopRecipe(
            LibraryEntryData entry,
            RecipeDefinition definition,
            int stars,
            float maxMultiplier,
            bool found
        )
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Stars = stars;
            MaxMultiplier = maxMultiplier;
            Found = found;
        }

        public LibraryEntryData Entry { get; }
        public RecipeDefinition Definition { get; }
        public LibraryKind Kind => Entry.Kind;
        public string Id => Entry.Id;
        public string Name => string.IsNullOrEmpty(Entry.Name) ? Entry.Id : Entry.Name;
        public int Stars { get; }
        public float MaxMultiplier { get; }

        // 作る物を手に入れたことがあるか。まだなら、絵を影にし、名前と効果を伏せる。
        public bool Found { get; }
        public int Runes => WorkshopCraft.RunesOf(Stars);
        public IReadOnlyList<RecipeMaterial> Materials => Definition.Materials;
    }

    /// <summary>
    /// The trading house's craft (doc/features/synthesis.md): every material of the made thing's
    /// recipe, all used, and runes. Items are counted, keeping as many as the deck slots they are
    /// in; equipment and skills (special materials) are one held piece each, nobody using it.
    /// The made thing's quality is the special materials' average place in their ranges, ×1.00
    /// (quality C) without any, as items have no quality. The server decides the same again
    /// (server/src/features/workshop/catalog.ts and craft.ts).
    /// </summary>
    public static class WorkshopCraft
    {
        // 合成のタブの分類と、その順。アイテムは作らない。
        public static readonly IReadOnlyList<LibraryKind> Kinds = new[]
        {
            LibraryKind.Weapon,
            LibraryKind.Armor,
            LibraryKind.Accessory,
            LibraryKind.Skill,
        };

        // タブの見出し。分類のタブのあとに、磨き石の合成のタブを置く（doc/features/enhancement.md）。
        public static readonly string[] TabLabels =
        {
            "武器",
            "防具",
            "アクセサリー",
            "スキル",
            "磨き石",
        };

        // 費用（ルーン）：出来上がる物の☆で、買う値段の半分（仮）。
        private static readonly int[] StarRunes = { 0, 200, 400, 800, 1600 };

        public static int RunesOf(int stars) =>
            stars >= 2 && stars <= StarRunes.Length ? StarRunes[stars - 1] : 0;

        /// <summary>
        /// The things that can be made: those with a recipe, ☆2 or more and not a lord's, whose
        /// materials have been found at least one, kind by kind, the lower stars first.
        /// </summary>
        public static IReadOnlyList<WorkshopRecipe> Recipes(
            LibraryData data,
            LibraryRecords records
        )
        {
            if (data == null || records == null)
                return Array.Empty<WorkshopRecipe>();
            return Kinds
                .SelectMany(kind =>
                    WorkshopRecipes
                        .All.Where(definition =>
                            definition.Kind == kind
                            && definition.Materials.Any(material =>
                                records.IsFound(material.Kind, material.Id)
                            )
                        )
                        .Select(definition =>
                        {
                            var entry = data.Entries.FirstOrDefault(e =>
                                e != null && e.Kind == kind && e.Id == definition.Id
                            );
                            int stars = WorkshopStock.StarsOf(kind, definition.Id);
                            return entry == null || stars < 2 || BossOnly(kind, definition.Id)
                                ? null
                                : new WorkshopRecipe(
                                    entry,
                                    definition,
                                    stars,
                                    MaxOf(kind, definition.Id),
                                    records.IsFound(kind, definition.Id)
                                );
                        })
                        .Where(recipe => recipe != null)
                        .OrderBy(recipe => recipe.Stars)
                )
                .ToArray();
        }

        /// <summary>
        /// How many of <paramref name="material"/> can be used: an item's count less the deck
        /// slots it is in, or the held pieces of the kind nobody uses.
        /// </summary>
        public static int HaveOf(RecipeMaterial material, IEnumerable<WorkshopOwned> owned)
        {
            var things = (owned ?? Array.Empty<WorkshopOwned>()).Where(thing =>
                thing.Kind == material.Kind && thing.KindId == material.Id
            );
            return material.IsItem
                ? things.Sum(thing => Math.Max(0, thing.Count - thing.Reserved))
                : things.Count(thing => thing.User == null);
        }

        /// <summary>The held pieces a special material can be, nobody using them, the worse first.</summary>
        public static IReadOnlyList<WorkshopOwned> ChoicesOf(
            RecipeMaterial material,
            IEnumerable<WorkshopOwned> owned
        ) =>
            material.IsItem
                ? Array.Empty<WorkshopOwned>()
                : (owned ?? Array.Empty<WorkshopOwned>())
                    .Where(thing =>
                        thing.Kind == material.Kind
                        && thing.KindId == material.Id
                        && thing.User == null
                    )
                    .OrderBy(thing => Quality.Percent(thing.Multiplier))
                    .ThenBy(thing => thing.RowId, StringComparer.Ordinal)
                    .ToArray();

        /// <summary>
        /// Every piece held for a special material (those in use too, which cannot be chosen),
        /// the worse first, for the picker.
        /// </summary>
        public static IReadOnlyList<WorkshopOwned> PiecesOf(
            RecipeMaterial material,
            IEnumerable<WorkshopOwned> owned
        ) =>
            material.IsItem
                ? Array.Empty<WorkshopOwned>()
                : (owned ?? Array.Empty<WorkshopOwned>())
                    .Where(thing => thing.Kind == material.Kind && thing.KindId == material.Id)
                    .OrderBy(thing => Quality.Percent(thing.Multiplier))
                    .ThenBy(thing => thing.RowId, StringComparer.Ordinal)
                    .ToArray();

        /// <summary>Whether every material of <paramref name="recipe"/> is held enough.</summary>
        public static bool CanMake(WorkshopRecipe recipe, IEnumerable<WorkshopOwned> owned)
        {
            var things = owned?.ToArray() ?? Array.Empty<WorkshopOwned>();
            return recipe != null
                && recipe.Materials.All(material => HaveOf(material, things) >= material.Count);
        }

        /// <summary>
        /// The special materials chosen when a recipe opens, one per special material in the
        /// recipe's order: the worst piece nobody uses, or null when none is held.
        /// </summary>
        public static IReadOnlyList<WorkshopOwned> FirstChoice(
            WorkshopRecipe recipe,
            IEnumerable<WorkshopOwned> owned
        )
        {
            if (recipe == null)
                return Array.Empty<WorkshopOwned>();
            var things = owned?.ToArray() ?? Array.Empty<WorkshopOwned>();
            return recipe
                .Materials.Where(material => !material.IsItem)
                .Select(material => ChoicesOf(material, things).FirstOrDefault())
                .ToArray();
        }

        /// <summary>
        /// The made thing's multiplier: the special materials' average place in their ranges, put
        /// on the made thing's range and rounded to a whole hundredth; ×1.00 without any.
        /// </summary>
        public static float Multiplier(WorkshopRecipe recipe, IReadOnlyList<WorkshopOwned> special)
        {
            var held = special?.Where(thing => thing != null).ToArray();
            if (recipe == null || held == null || held.Length == 0)
                return Quality.MinMultiplier;
            // サーバーと同じく百分率の整数で計算する。
            double place = held.Average(thing => (double)PlaceOf(thing));
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
