using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.Inventory;
using Baryonyx.Library;

namespace Baryonyx.Temple
{
    /// <summary>One kind that the summoning can give. Characters have no stars (0).</summary>
    public readonly struct SummonEntry
    {
        public SummonEntry(LibraryKind kind, string id, int stars)
        {
            Kind = kind;
            Id = id;
            Stars = stars;
        }

        public LibraryKind Kind { get; }
        public string Id { get; }
        public int Stars { get; }
    }

    /// <summary>
    /// What a character that came out did (doc/features/monetization.md, when a character comes
    /// again): joined the party, or gave one bond token to deepen its bond with on the training
    /// screen; once its bond and tokens reach the top, it turned into shards.
    /// </summary>
    public sealed class SummonBond
    {
        public SummonBond(bool isNew, int bond, int shards, int tokens = 0)
        {
            IsNew = isNew;
            Bond = bond;
            Shards = shards;
            Tokens = tokens;
        }

        public bool IsNew { get; }

        // 今の絆の段（召喚では変わらない）と、引いたあとの絆のしるしの数。
        public int Bond { get; }
        public int Tokens { get; }
        public int Shards { get; }
    }

    /// <summary>One thing the summoning gave. Only equipment has a quality.</summary>
    public readonly struct SummonResult
    {
        public SummonResult(SummonEntry entry, ItemQuality? quality, SummonBond bond = null)
        {
            Entry = entry;
            Quality = quality;
            Bond = bond;
        }

        public SummonEntry Entry { get; }
        public ItemQuality? Quality { get; }

        // キャラが出たときの絆。サーバーが決めるため、仮に引いた結果ではnull。
        public SummonBond Bond { get; }

        // キャラ・☆4以上・品質Sは、結果の札に金の縁と光を出す。
        public bool Highlight =>
            Entry.Kind == LibraryKind.Character || Entry.Stars >= 4 || Quality == ItemQuality.S;
    }

    /// <summary>
    /// The temple's summoning (doc/features/monetization.md): one summoning that mixes characters,
    /// equipment and items, paid with runes, at the rates shown on the screen. A draw picks the
    /// category, then the stars, then one kind of those stars at even odds, then the quality of
    /// equipment. Picking the stars before the kind keeps the rates the same whatever the pool
    /// holds; stars the category has nothing of are left out. Ten at once guarantee one with
    /// ☆3 or more (or a character).
    /// The server does not summon yet: <see cref="Roll"/> draws the screen's mock results, and
    /// nothing is paid or kept.
    /// </summary>
    public static class TempleSummon
    {
        public const int SinglePrice = 300;
        public const int TenPrice = 3000;
        public const int TenCount = 10;
        public const int GuaranteedStars = 3;

        /// <summary>The runes <paramref name="count"/> pulls cost: 300 each, ten without a discount.</summary>
        public static int PriceOf(int count) => count >= TenCount ? TenPrice : SinglePrice * count;

        // 分類の割合（千分率）。並びは画面の表の順。
        public static readonly IReadOnlyList<(
            LibraryKind Kind,
            string Name,
            int Permille
        )> Categories = new[]
        {
            (LibraryKind.Character, "キャラ", 20),
            (LibraryKind.Weapon, "武器", 150),
            (LibraryKind.Armor, "防具", 150),
            (LibraryKind.Accessory, "アクセサリー", 200),
            (LibraryKind.Item, "アイテム", 480),
        };

        // ☆1〜☆5の割合（千分率）。装備とアイテムに使う。
        public static readonly IReadOnlyList<int> StarPermille = new[] { 450, 350, 160, 35, 5 };

        // 品質の割合（千分率）。装備（武器・防具・アクセサリー）に使う。並びは画面の表の順。
        public static readonly IReadOnlyList<(ItemQuality Quality, int Permille)> QualityPermille =
            new[]
            {
                (ItemQuality.S, 20),
                (ItemQuality.A, 50),
                (ItemQuality.B, 180),
                (ItemQuality.C, 750),
            };

        /// <summary>"48%" or "0.5%": a rate in per mille as the screen shows it.</summary>
        public static string PercentText(int permille) =>
            (permille / 10.0).ToString("0.#", CultureInfo.InvariantCulture) + "%";

        public static bool HasQuality(LibraryKind kind) =>
            kind is LibraryKind.Weapon or LibraryKind.Armor or LibraryKind.Accessory;

        /// <summary>
        /// The mock pool: the characters, and every item and piece of equipment in the
        /// adventure's destinations, the boss-only ones too, once each.
        /// </summary>
        public static IReadOnlyList<SummonEntry> MockPool(IEnumerable<string> characterIds)
        {
            var pool = (characterIds ?? Array.Empty<string>())
                .Select(id => new SummonEntry(LibraryKind.Character, id, 0))
                .ToList();
            var seen = new HashSet<(LibraryKind, string)>();
            foreach (var entry in AdventureDrops.Pools.Values.SelectMany(list => list))
            {
                var kind = KindOf(entry.Category);
                if (kind == null || !seen.Add((kind.Value, entry.Id)))
                    continue;
                pool.Add(new SummonEntry(kind.Value, entry.Id, entry.Stars));
            }
            return pool;
        }

        /// <summary>Draws <paramref name="count"/> from <paramref name="pool"/>; <paramref name="random"/> gives [0, 1).</summary>
        public static IReadOnlyList<SummonResult> Roll(
            IReadOnlyList<SummonEntry> pool,
            int count,
            Func<double> random
        )
        {
            if (pool == null || pool.Count == 0 || count <= 0)
                return Array.Empty<SummonResult>();
            var results = new List<SummonResult>();
            for (int i = 0; i < count; i++)
                results.Add(Draw(pool, random, 1));
            if (count >= TenCount && !results.Any(Guaranteed))
            {
                var last = Draw(pool, random, GuaranteedStars);
                if (Guaranteed(last))
                    results[^1] = last;
            }
            return results;
        }

        private static bool Guaranteed(SummonResult result) =>
            result.Entry.Kind == LibraryKind.Character || result.Entry.Stars >= GuaranteedStars;

        // 分類→☆→物→品質の順に選ぶ。minStars が1より大きいときは、キャラを除き、その☆以上だけから選ぶ。
        private static SummonResult Draw(
            IReadOnlyList<SummonEntry> pool,
            Func<double> random,
            int minStars
        )
        {
            var categories = Categories
                .Where(category =>
                    pool.Any(entry =>
                        entry.Kind == category.Kind
                        && (
                            category.Kind == LibraryKind.Character
                                ? minStars <= 1
                                : entry.Stars >= minStars
                        )
                    )
                )
                .ToArray();
            if (categories.Length == 0)
                return Draw(pool, random, 1);
            var kind = Pick(categories, category => category.Permille, random).Kind;
            var entries = pool.Where(entry => entry.Kind == kind).ToArray();
            if (kind != LibraryKind.Character)
            {
                var stars = entries
                    .Select(entry => entry.Stars)
                    .Where(star => star >= minStars && star >= 1 && star <= StarPermille.Count)
                    .Distinct()
                    .ToArray();
                int star = Pick(stars, s => StarPermille[s - 1], random);
                entries = entries.Where(entry => entry.Stars == star).ToArray();
            }
            var chosen = entries[Math.Min((int)(random() * entries.Length), entries.Length - 1)];
            ItemQuality? quality = HasQuality(kind)
                ? Pick(QualityPermille, rate => rate.Permille, random).Quality
                : null;
            return new SummonResult(chosen, quality);
        }

        private static T Pick<T>(
            IReadOnlyList<T> choices,
            Func<T, int> weightOf,
            Func<double> random
        )
        {
            int total = choices.Sum(weightOf);
            double roll = random() * total;
            foreach (var choice in choices)
            {
                int weight = weightOf(choice);
                if (roll < weight)
                    return choice;
                roll -= weight;
            }
            return choices[choices.Count - 1];
        }

        private static LibraryKind? KindOf(AdventureDropCategory category) =>
            category switch
            {
                AdventureDropCategory.Item => LibraryKind.Item,
                AdventureDropCategory.Accessory => LibraryKind.Accessory,
                AdventureDropCategory.Weapon => LibraryKind.Weapon,
                AdventureDropCategory.Armor => LibraryKind.Armor,
                _ => null,
            };
    }
}
