using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.Library;

namespace Baryonyx.TravelOffice
{
    /// <summary>
    /// The treasures of a destination as the travel office shows them (doc/features/screens.md):
    /// the kinds in its drop list (<see cref="AdventureDrops.Pools"/>), in the sections' order and
    /// the list's order inside each, and how many of them the library has found.
    /// </summary>
    public static class TravelTreasures
    {
        /// <summary>One kind that may fall in the destination.</summary>
        public readonly struct Treasure
        {
            public Treasure(AdventureDrops.Entry entry)
            {
                Category = entry.Category;
                Id = entry.Id;
                Stars = entry.Stars;
                BossOnly = entry.BossOnly;
            }

            public AdventureDropCategory Category { get; }
            public string Id { get; }
            public int Stars { get; }

            // 最奥の間でだけ落ちる物（主の物）。
            public bool BossOnly { get; }

            public LibraryKind Kind => KindOf(Category);
        }

        /// <summary>The sections of the treasures' window, top to bottom. Weapons and armor share one.</summary>
        public static readonly IReadOnlyList<(
            string Title,
            AdventureDropCategory[] Categories
        )> Sections = new[]
        {
            ("アイテム", new[] { AdventureDropCategory.Item }),
            ("アクセサリー", new[] { AdventureDropCategory.Accessory }),
            ("武器・防具", new[] { AdventureDropCategory.Weapon, AdventureDropCategory.Armor }),
            ("スキル", new[] { AdventureDropCategory.Skill }),
        };

        /// <summary>The treasures of a section in the destination; empty for an unknown one.</summary>
        public static IReadOnlyList<Treasure> Of(string destinationId, int section)
        {
            if (section < 0 || section >= Sections.Count)
                return Array.Empty<Treasure>();
            var categories = Sections[section].Categories;
            var pool = AdventureDrops.Pools.TryGetValue(destinationId ?? "", out var found)
                ? found
                : Array.Empty<AdventureDrops.Entry>();
            return categories
                .SelectMany(category => pool.Where(entry => entry.Category == category))
                .Select(entry => new Treasure(entry))
                .ToArray();
        }

        /// <summary>All the destination's treasures, section by section.</summary>
        public static IReadOnlyList<Treasure> Of(string destinationId) =>
            Enumerable
                .Range(0, Sections.Count)
                .SelectMany(section => Of(destinationId, section))
                .ToArray();

        /// <summary>How many of the destination's treasures have been found, and how many there are.</summary>
        public static (int Found, int Total) Count(string destinationId, LibraryRecords records)
        {
            var treasures = Of(destinationId);
            records ??= LibraryRecords.Empty;
            return (treasures.Count(treasure => IsFound(treasure, records)), treasures.Count);
        }

        public static bool IsFound(Treasure treasure, LibraryRecords records) =>
            records != null && records.IsFound(treasure.Kind, treasure.Id);

        /// <summary>The row's count, e.g. "9/23", or "-/23" before the records are read.</summary>
        public static string CountText(string destinationId, LibraryRecords records)
        {
            if (records == null)
                return $"-/{Of(destinationId).Count}";
            var (found, total) = Count(destinationId, records);
            return $"{found}/{total}";
        }

        /// <summary>The library's kind of a drop. The enhancement materials are not in the library.</summary>
        public static LibraryKind KindOf(AdventureDropCategory category) =>
            category switch
            {
                AdventureDropCategory.Item => LibraryKind.Item,
                AdventureDropCategory.Accessory => LibraryKind.Accessory,
                AdventureDropCategory.Weapon => LibraryKind.Weapon,
                AdventureDropCategory.Armor => LibraryKind.Armor,
                AdventureDropCategory.Skill => LibraryKind.Skill,
                _ => throw new ArgumentOutOfRangeException(nameof(category)),
            };
    }
}
