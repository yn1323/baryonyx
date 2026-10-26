using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.Library;

namespace Baryonyx.Workshop
{
    /// <summary>One thing the trading house sells: a kind the player has found, at the lowest quality.</summary>
    public sealed class WorkshopGood
    {
        public WorkshopGood(LibraryEntryData entry, int stars, int price)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Stars = stars;
            Price = price;
        }

        public LibraryEntryData Entry { get; }
        public LibraryKind Kind => Entry.Kind;
        public string Id => Entry.Id;
        public string Name => string.IsNullOrEmpty(Entry.Name) ? Entry.Id : Entry.Name;
        public int Stars { get; }
        public int Price { get; }
    }

    /// <summary>
    /// What the trading house's "buy" sells (doc/features/screens/shop.md#商会): the skills, weapons,
    /// armor and accessories the library has found, at the lowest quality (×1.00, quality C),
    /// as many times as the player likes. The prices are the server's
    /// (server/src/features/workshop/catalog.ts); the server decides the price again when buying.
    /// </summary>
    public static class WorkshopGoods
    {
        public const float Multiplier = 1f;

        // 並べる分類と、その順（ライブラリの分類の順）。
        public static readonly IReadOnlyList<LibraryKind> Kinds = new[]
        {
            LibraryKind.Weapon,
            LibraryKind.Armor,
            LibraryKind.Accessory,
            LibraryKind.Skill,
        };

        // 値段（ルーン）：☆1の1,000から、☆が1つ増えるごとに倍にする（仮）。
        private static readonly int[] StarPrices = { 1000, 2000, 4000, 8000, 16000 };

        /// <summary>The stars of a kind, from the adventure's drop lists (0 when no list has it).</summary>
        public static int StarsOf(LibraryKind kind, string id) =>
            CategoryOf(kind) is { } category ? AdventureDrops.StarsOf(category, id) : 0;

        /// <summary>The price of a kind, or 0 when the trading house does not sell it.</summary>
        public static int PriceOf(LibraryKind kind, string id) => PriceOfStars(StarsOf(kind, id));

        public static int PriceOfStars(int stars) =>
            stars >= 1 && stars <= StarPrices.Length ? StarPrices[stars - 1] : 0;

        /// <summary>
        /// The goods: the found entries of <see cref="Kinds"/> that have a price, kind by kind,
        /// the cheaper first and in the definitions' order for the same price.
        /// </summary>
        public static IReadOnlyList<WorkshopGood> Of(LibraryData data, LibraryRecords records)
        {
            if (data == null || records == null)
                return Array.Empty<WorkshopGood>();
            return Kinds
                .SelectMany(kind =>
                    data.Entries.Where(entry =>
                            entry != null && entry.Kind == kind && records.IsFound(kind, entry.Id)
                        )
                        .Select(entry =>
                        {
                            int stars = StarsOf(kind, entry.Id);
                            return new WorkshopGood(entry, stars, PriceOfStars(stars));
                        })
                        .Where(good => good.Price > 0)
                        .OrderBy(good => good.Price)
                )
                .ToArray();
        }

        /// <summary>The server's name of the kind (server/src/features/workshop/catalog.ts).</summary>
        public static string KeyOf(LibraryKind kind) =>
            CategoryOf(kind) is { } category ? AdventureDrops.KeyOf(category) : null;

        private static AdventureDropCategory? CategoryOf(LibraryKind kind) =>
            kind switch
            {
                LibraryKind.Weapon => AdventureDropCategory.Weapon,
                LibraryKind.Armor => AdventureDropCategory.Armor,
                LibraryKind.Accessory => AdventureDropCategory.Accessory,
                LibraryKind.Skill => AdventureDropCategory.Skill,
                _ => null,
            };
    }
}
