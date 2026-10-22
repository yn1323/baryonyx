using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Inventory;

namespace Baryonyx.Adventure
{
    /// <summary>What a drop is (doc/features/drops.md).</summary>
    public enum AdventureDropCategory
    {
        Item,
        Accessory,
        Weapon,
        Armor,
        Skill,
        Bonus,
    }

    /// <summary>
    /// The drop lists and how a room's drops are picked, the same as the server
    /// (server/src/features/adventure/drops.ts): how many fall, then the category, the kind (by its
    /// stars) and the quality, each by the room's weights. The server picks the real drops; this
    /// is for the adventure kept without a server, and for the stars the screens show.
    /// </summary>
    public static class AdventureDrops
    {
        /// <summary>One kind in a destination's list. A boss-only kind falls only in the boss's room.</summary>
        public readonly struct Entry
        {
            public Entry(
                AdventureDropCategory category,
                string id,
                int stars,
                bool bossOnly = false
            )
            {
                Category = category;
                Id = id;
                Stars = stars;
                BossOnly = bossOnly;
            }

            public AdventureDropCategory Category { get; }
            public string Id { get; }
            public int Stars { get; }
            public bool BossOnly { get; }
        }

        // 分類の重み。アイテム＞アクセサリー＞武器＝防具＞スキル＞ACTボーナスの順に出やすい。
        private static readonly (AdventureDropCategory Category, int Weight)[] CategoryWeights =
        {
            (AdventureDropCategory.Item, 40),
            (AdventureDropCategory.Accessory, 22),
            (AdventureDropCategory.Weapon, 13),
            (AdventureDropCategory.Armor, 13),
            (AdventureDropCategory.Skill, 9),
            (AdventureDropCategory.Bonus, 3),
        };

        /// <summary>
        /// Which row of weights a room's kind borrows, and how many more fall, as the server's
        /// rewardOf; null for a room that drops nothing. Until their contents are made, a trial
        /// gives what a win without the condition gives, a step door what fewer than 3,000 steps
        /// give, a shady chest is a real one, and an altar gives a chest's drops
        /// (doc/features/stage-progression.md).
        /// </summary>
        public static (AdventureRoomKind Row, int Extra)? RewardOf(AdventureRoomKind kind) =>
            kind switch
            {
                AdventureRoomKind.Battle
                or AdventureRoomKind.Elite
                or AdventureRoomKind.Treasure
                or AdventureRoomKind.Boss => (kind, 0),
                AdventureRoomKind.ShadyChest => (AdventureRoomKind.Treasure, 1),
                AdventureRoomKind.Trial or AdventureRoomKind.StepDoor => (
                    AdventureRoomKind.Battle,
                    0
                ),
                AdventureRoomKind.Altar => (AdventureRoomKind.Treasure, 0),
                _ => null,
            };

        private static (int Count, int Weight)[] CountWeights(AdventureRoomKind kind) =>
            kind switch
            {
                AdventureRoomKind.Battle => new[] { (1, 70), (2, 30) },
                AdventureRoomKind.Boss => new[] { (2, 30), (3, 45), (4, 25) },
                _ => new[] { (1, 25), (2, 50), (3, 25) },
            };

        // ☆1〜☆5の物1つあたりの重み（千分率）。最奥の間は☆3〜5だけが落ちる。
        private static int[] StarWeights(AdventureRoomKind kind) =>
            kind switch
            {
                AdventureRoomKind.Battle => new[] { 550, 300, 130, 17, 3 },
                AdventureRoomKind.Boss => new[] { 0, 0, 650, 280, 70 },
                _ => new[] { 300, 380, 260, 50, 10 },
            };

        // 品質の重み（千分率）。行き先によっては変えない。
        private static (ItemQuality Quality, int Weight)[] QualityWeights(AdventureRoomKind kind) =>
            kind switch
            {
                AdventureRoomKind.Battle => new[]
                {
                    (ItemQuality.C, 720),
                    (ItemQuality.B, 220),
                    (ItemQuality.A, 55),
                    (ItemQuality.S, 5),
                },
                AdventureRoomKind.Boss => new[]
                {
                    (ItemQuality.C, 150),
                    (ItemQuality.B, 350),
                    (ItemQuality.A, 380),
                    (ItemQuality.S, 120),
                },
                _ => new[]
                {
                    (ItemQuality.C, 400),
                    (ItemQuality.B, 350),
                    (ItemQuality.A, 210),
                    (ItemQuality.S, 40),
                },
            };

        private static Entry Item(string id, int stars, bool bossOnly = false) =>
            new(AdventureDropCategory.Item, id, stars, bossOnly);

        private static Entry Accessory(string id, int stars, bool bossOnly = false) =>
            new(AdventureDropCategory.Accessory, id, stars, bossOnly);

        private static Entry Weapon(string id, int stars) =>
            new(AdventureDropCategory.Weapon, id, stars);

        private static Entry Armor(string id, int stars) =>
            new(AdventureDropCategory.Armor, id, stars);

        private static Entry Skill(string id, int stars) =>
            new(AdventureDropCategory.Skill, id, stars);

        private static Entry Bonus(string id, int stars) =>
            new(AdventureDropCategory.Bonus, id, stars);

        /// <summary>The destinations' lists, the same as the server's DROP_POOLS.</summary>
        public static readonly IReadOnlyDictionary<string, Entry[]> Pools = new Dictionary<
            string,
            Entry[]
        >
        {
            ["forest-ruins"] = new[]
            {
                Item("herb", 1),
                Item("acorn", 1),
                Item("moss-wad", 2),
                Item("dew-vial", 2),
                Item("spore-powder", 3),
                Item("guardian-seed", 5, true),
                Accessory("moss-charm", 1),
                Accessory("acorn-brooch", 2),
                Accessory("dew-pendant", 3),
                Accessory("guardian-circlet", 5, true),
                Weapon("wooden-sword", 1),
                Weapon("short-bow", 1),
                Weapon("oak-staff", 1),
                Armor("leather-armor", 1),
                Armor("wooden-shield", 1),
                Armor("iron-helm", 2),
                Skill("Slash", 1),
                Skill("Fire", 1),
                Skill("Heal", 1),
                Skill("VitalThrust", 1),
                Skill("ShieldBash", 2),
                Skill("Embers", 2),
                Skill("Hone", 4),
            },
            ["port-portoria"] = new[]
            {
                Item("dried-fish", 1),
                Item("shell-shard", 1),
                Item("sailor-rope", 2),
                Item("sea-salt", 2),
                Item("ink-pot", 3),
                Item("pirate-rum", 4, true),
                Accessory("shell-necklace", 1),
                Accessory("anchor-bangle", 2),
                Accessory("gold-earring", 3),
                Accessory("kraken-pearl", 5, true),
                Weapon("iron-sword", 2),
                Weapon("war-hammer", 2),
                Weapon("flame-dagger", 4),
                Armor("chainmail", 2),
                Armor("magic-robe", 2),
                Armor("traveler-cloak", 3),
                Skill("Ice", 1),
                Skill("Guard", 1),
                Skill("HolyHammer", 1),
                Skill("Thunder", 2),
                Skill("PoisonNeedle", 2),
                Skill("Scout", 3),
                Skill("ManaPrayer", 4),
            },
            ["glaciera-cave"] = new[]
            {
                Weapon("thunder-spear", 3),
                Weapon("frost-wand", 3),
                Skill("Icicles", 1),
                Skill("LightningBolt", 1),
                Skill("Purify", 1),
                Skill("ArmorBreak", 2),
                Skill("Protect", 2),
                Skill("ShadowStitch", 3),
                Skill("IceMirror", 4),
            },
            ["hush-mire"] = new[]
            {
                Skill("Regen", 1),
                Skill("InsightArrow", 2),
                Skill("FlamePillar", 2),
                Skill("GuardianOath", 3),
                Skill("Whirlwind", 3),
                Skill("QuickCast", 5),
            },
            ["almaja-tomb"] = new[]
            {
                Skill("ArrowRain", 1),
                Skill("EarthSplitter", 2),
                Skill("BlastSigil", 2),
                Skill("HolyLight", 3),
                Skill("StrideStrike", 3),
                Skill("Revelation", 5),
            },
            ["moonshade-graveyard"] = new[]
            {
                Skill("ChainLightning", 1),
                Skill("FireStorm", 2),
                Skill("Blizzard", 2),
                Skill("Iai", 3),
                Skill("Resurrection", 4),
                Skill("DivineShield", 4),
            },
            ["volgan-volcano"] = new[]
            {
                Skill("FlameEnchant", 1),
                Skill("Thundercloud", 2),
                Skill("WarCry", 2),
                Skill("StepPrayer", 3),
                Skill("ShadowSnipe", 4),
                Skill("Meteor", 5),
            },
            ["neve-peaks"] = new[]
            {
                Skill("Gale", 2),
                Skill("BladeDance", 3),
                Skill("ThunderSpear", 3),
                Skill("GiantImpact", 4),
                Skill("AbsoluteZero", 5),
            },
        };

        /// <summary>The ACT bonuses, found in every destination.</summary>
        public static readonly IReadOnlyList<Entry> Common = new[]
        {
            Bonus("guard", 1),
            Bonus("fighting", 1),
            Bonus("foresight", 2),
            Bonus("omen", 2),
            Bonus("treasure-sight", 3),
            Bonus("luck", 3),
            Bonus("appraisal", 4),
        };

        /// <summary>The stars of a kind, from the lists (0 when no list has it).</summary>
        public static int StarsOf(AdventureDropCategory category, string id)
        {
            foreach (var entry in Pools.Values.SelectMany(pool => pool).Concat(Common))
                if (entry.Category == category && entry.Id == id)
                    return entry.Stars;
            return 0;
        }

        /// <summary>The kinds that may fall in a room: no boss-only kind on the way, only stars 3–5 at the boss.</summary>
        public static IReadOnlyList<Entry> Eligible(string destinationId, AdventureRoomKind kind) =>
            RewardOf(kind) is { } reward
                ? EligibleIn(destinationId, reward.Row, kind == AdventureRoomKind.Boss)
                : Array.Empty<Entry>();

        // 報酬の行で落ちうる物。主の物は最奥の間でだけ落ちる。
        private static IReadOnlyList<Entry> EligibleIn(
            string destinationId,
            AdventureRoomKind row,
            bool bossOnly
        )
        {
            var weights = StarWeights(row);
            var pool = Pools.TryGetValue(destinationId ?? "", out var found)
                ? found
                : Array.Empty<Entry>();
            return pool.Concat(Common)
                .Where(entry => (bossOnly || !entry.BossOnly) && weights[entry.Stars - 1] > 0)
                .ToArray();
        }

        /// <summary>Picks the drops of a room, as the server's rollDrops does.</summary>
        public static IReadOnlyList<AdventureDrop> Roll(
            string destinationId,
            AdventureRoomKind kind,
            string roomId,
            Func<AdventureDropCategory, string, float> maxMultiplierOf,
            Random random
        ) =>
            RewardOf(kind) is { } reward
                ? Roll(destinationId, kind, reward, roomId, maxMultiplierOf, random)
                : Array.Empty<AdventureDrop>();

        /// <summary>
        /// Picks the drops of a room of <paramref name="kind"/> by another row of weights: a mimic
        /// beaten, a trial met or a step door opened by many steps (doc/features/stage-progression.md).
        /// </summary>
        public static IReadOnlyList<AdventureDrop> Roll(
            string destinationId,
            AdventureRoomKind kind,
            (AdventureRoomKind Row, int Extra) reward,
            string roomId,
            Func<AdventureDropCategory, string, float> maxMultiplierOf,
            Random random
        )
        {
            var row = reward.Row;
            var pool = EligibleIn(destinationId, row, kind == AdventureRoomKind.Boss);
            if (pool.Count == 0)
                return Array.Empty<AdventureDrop>();
            var categories = CategoryWeights
                .Where(entry => pool.Any(choice => choice.Category == entry.Category))
                .ToArray();
            int count = Pick(CountWeights(row), entry => entry.Weight, random).Count + reward.Extra;
            var drops = new List<AdventureDrop>();
            for (int i = 0; i < count; i++)
            {
                var category = Pick(categories, entry => entry.Weight, random).Category;
                drops.Add(PickIn(pool, category, row, roomId, maxMultiplierOf, random));
            }
            return drops;
        }

        /// <summary>
        /// One thing of <paramref name="category"/> a merchant of the destination sells: its stars
        /// and quality by the strong reward's weights. Null when the destination has none.
        /// </summary>
        public static AdventureDrop PickWare(
            string destinationId,
            AdventureDropCategory category,
            Func<AdventureDropCategory, string, float> maxMultiplierOf,
            Random random
        )
        {
            var pool = EligibleIn(destinationId, AdventureRoomKind.Elite, false);
            return pool.Any(entry => entry.Category == category)
                ? PickIn(pool, category, AdventureRoomKind.Elite, "", maxMultiplierOf, random)
                : null;
        }

        private static AdventureDrop PickIn(
            IReadOnlyList<Entry> pool,
            AdventureDropCategory category,
            AdventureRoomKind row,
            string roomId,
            Func<AdventureDropCategory, string, float> maxMultiplierOf,
            Random random
        )
        {
            var stars = StarWeights(row);
            var entry = Pick(
                pool.Where(choice => choice.Category == category).ToArray(),
                choice => stars[choice.Stars - 1],
                random
            );
            if (category == AdventureDropCategory.Item)
                return new AdventureDrop(roomId, category, entry.Id);
            var quality = Pick(QualityWeights(row), choice => choice.Weight, random).Quality;
            var (low, high) = Quality.Range(quality, maxMultiplierOf(category, entry.Id));
            return new AdventureDrop(
                roomId,
                category,
                entry.Id,
                random.Next(Quality.Percent(low), Quality.Percent(high) + 1) / 100f,
                quality
            );
        }

        private static T Pick<T>(IReadOnlyList<T> choices, Func<T, int> weightOf, Random random)
        {
            int roll = random.Next(choices.Sum(weightOf));
            foreach (var choice in choices)
            {
                int weight = weightOf(choice);
                if (roll < weight)
                    return choice;
                roll -= weight;
            }
            return choices[choices.Count - 1];
        }

        /// <summary>The category's name in the server's API.</summary>
        public static string KeyOf(AdventureDropCategory category) =>
            category switch
            {
                AdventureDropCategory.Item => "item",
                AdventureDropCategory.Accessory => "accessory",
                AdventureDropCategory.Weapon => "weapon",
                AdventureDropCategory.Armor => "armor",
                AdventureDropCategory.Skill => "skill",
                _ => "bonus",
            };

        public static bool TryParse(string key, out AdventureDropCategory category)
        {
            foreach (AdventureDropCategory value in Enum.GetValues(typeof(AdventureDropCategory)))
            {
                if (KeyOf(value) == key)
                {
                    category = value;
                    return true;
                }
            }
            category = default;
            return false;
        }

        /// <summary>The category as the screens name it.</summary>
        public static string NameOf(AdventureDropCategory category) =>
            category switch
            {
                AdventureDropCategory.Item => "アイテム",
                AdventureDropCategory.Accessory => "アクセサリー",
                AdventureDropCategory.Weapon => "武器",
                AdventureDropCategory.Armor => "防具",
                AdventureDropCategory.Skill => "スキル",
                _ => "ACTボーナス",
            };
    }
}
