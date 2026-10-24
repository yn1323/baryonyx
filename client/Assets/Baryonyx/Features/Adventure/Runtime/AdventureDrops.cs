using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Enhancement;
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

        // 強化素材（磨き石・覚醒の欠片）。分類の重みでは選ばず、ふつうのドロップのあとに別に抽選する。
        Material,
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

        // 分類の重み。アイテム＞アクセサリー＞武器＝防具＞スキルの順に出やすい。
        private static readonly (AdventureDropCategory Category, int Weight)[] CategoryWeights =
        {
            (AdventureDropCategory.Item, 43),
            (AdventureDropCategory.Accessory, 22),
            (AdventureDropCategory.Weapon, 13),
            (AdventureDropCategory.Armor, 13),
            (AdventureDropCategory.Skill, 9),
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
                Item("icicle-shard", 1),
                Item("cavern-water", 1),
                Item("glowworm-lamp", 2),
                Item("frost-crystal", 3),
                Item("troll-stoneskin", 4, true),
                Accessory("penguin-feather", 1),
                Accessory("crystal-ring", 3),
                Accessory("troll-heartstone", 5, true),
                Weapon("thunder-spear", 3),
                Weapon("frost-wand", 3),
                Armor("miner-helm", 2),
                Armor("crystal-shield", 4),
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
                Item("mud-ball", 1),
                Item("lotus-root", 1),
                Item("wisp-wick", 2),
                Item("bog-venom", 3),
                Item("croc-tear", 4, true),
                Accessory("leech-bell", 2),
                Accessory("frog-crown", 3),
                Accessory("croc-fang", 5, true),
                Weapon("reed-bow", 2),
                Weapon("wisp-staff", 3),
                Armor("mudcloth-robe", 2),
                Armor("clay-armor", 3),
                Skill("Regen", 1),
                Skill("InsightArrow", 2),
                Skill("FlamePillar", 2),
                Skill("GuardianOath", 3),
                Skill("Whirlwind", 3),
                Skill("QuickCast", 5),
            },
            ["almaja-tomb"] = new[]
            {
                Item("cactus-fruit", 1),
                Item("scorpion-stinger", 1),
                Item("hourglass-sand", 2),
                Item("grimoire-page", 3),
                Item("brass-spring", 5, true),
                Accessory("scarab-amulet", 1),
                Accessory("gargoyle-eye", 3),
                Accessory("golden-mask", 5, true),
                Weapon("scorpion-bow", 2),
                Weapon("tomb-khopesh", 3),
                Armor("burial-wraps", 2),
                Armor("ancient-plate", 4),
                Skill("ArrowRain", 1),
                Skill("EarthSplitter", 2),
                Skill("BlastSigil", 2),
                Skill("HolyLight", 3),
                Skill("StrideStrike", 3),
                Skill("Revelation", 5),
            },
            ["moonshade-graveyard"] = new[]
            {
                Item("grave-candle", 1),
                Item("moonflower-honey", 1),
                Item("raven-feather", 2),
                Item("holy-water", 3),
                Item("soul-phial", 5, true),
                Accessory("bone-ring", 2),
                Accessory("moonstone-pendant", 3),
                Accessory("lich-signet", 5, true),
                Weapon("bone-axe", 2),
                Weapon("moonshade-staff", 4),
                Armor("hollow-helm", 2),
                Armor("grave-cloak", 3),
                Skill("ChainLightning", 1),
                Skill("FireStorm", 2),
                Skill("Blizzard", 2),
                Skill("Iai", 3),
                Skill("Resurrection", 4),
                Skill("DivineShield", 4),
            },
            ["volgan-volcano"] = new[]
            {
                Item("lava-rock", 1),
                Item("lizard-tail", 1),
                Item("sulfur-powder", 2),
                Item("flametail-plume", 3),
                Item("obsidian-core", 5, true),
                Accessory("ember-charm", 1),
                Accessory("magma-ring", 3),
                Accessory("obsidian-crown", 5, true),
                Weapon("firebird-bow", 2),
                Weapon("magma-hammer", 3),
                Armor("lizardscale-mail", 2),
                Armor("obsidian-shield", 4),
                Skill("FlameEnchant", 1),
                Skill("Thundercloud", 2),
                Skill("WarCry", 2),
                Skill("StepPrayer", 3),
                Skill("ShadowSnipe", 4),
                Skill("Meteor", 5),
            },
            ["neve-peaks"] = new[]
            {
                Item("hard-snowball", 1),
                Item("snowdrop", 1),
                Item("boar-jerky", 2),
                Item("crystal-horn", 3),
                Item("mistdragon-scale", 5, true),
                Accessory("snowflake-brooch", 2),
                Accessory("foxtail-charm", 3),
                Accessory("mistdragon-eye", 5, true),
                Weapon("hailstone-staff", 3),
                Weapon("glacier-greatsword", 4),
                Armor("snow-cloak", 2),
                Armor("mammoth-fur", 3),
                Skill("Gale", 2),
                Skill("BladeDance", 3),
                Skill("ThunderSpear", 3),
                Skill("GiantImpact", 4),
                Skill("AbsoluteZero", 5),
            },
        };

        /// <summary>The stars of a kind, from the lists (0 when no list has it).</summary>
        public static int StarsOf(AdventureDropCategory category, string id)
        {
            if (category == AdventureDropCategory.Material)
                return EnhancementMaterials.Find(id)?.Stars ?? 0;
            foreach (var entry in Pools.Values.SelectMany(pool => pool))
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
            return pool.Where(entry =>
                    (bossOnly || !entry.BossOnly) && weights[entry.Stars - 1] > 0
                )
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

        // 磨き石が落ちる確率（千分率）と色の重み、覚醒の欠片が落ちる確率（千分率）。サーバーの
        // POLISH_DROP_CHANCE・POLISH_DROP_WEIGHTS・SHARD_DROP_CHANCE と同じ値（doc/features/enhancement.md）。
        private static int PolishChance(AdventureRoomKind row) =>
            row switch
            {
                AdventureRoomKind.Battle => 300,
                AdventureRoomKind.Boss => 1000,
                _ => 600,
            };

        // 磨き石の色の重みを分ける行き先の深さ。サーバーの polishDepthOf と同じく、近い順の初めの2つを浅い、
        // 次の3つを中ほど、残りを奥とする。一覧にない行き先は浅いとする。
        private static int PolishDepthOf(string destinationId)
        {
            int index = 0;
            while (
                index < AdventureCatalog.Areas.Count
                && AdventureCatalog.Areas[index].Id != destinationId
            )
                index++;
            if (index == AdventureCatalog.Areas.Count)
                return 0;
            return index < 2 ? 0
                : index < 5 ? 1
                : 2;
        }

        // [深さ][魔物・強い魔物と宝箱・最奥の間]
        private static readonly (string Id, int Weight)[][][] PolishWeightTable =
        {
            new[]
            {
                new[]
                {
                    (EnhancementMaterials.White, 920),
                    (EnhancementMaterials.Blue, 80),
                    (EnhancementMaterials.Purple, 0),
                    (EnhancementMaterials.Gold, 0),
                },
                new[]
                {
                    (EnhancementMaterials.White, 780),
                    (EnhancementMaterials.Blue, 200),
                    (EnhancementMaterials.Purple, 20),
                    (EnhancementMaterials.Gold, 0),
                },
                new[]
                {
                    (EnhancementMaterials.White, 500),
                    (EnhancementMaterials.Blue, 400),
                    (EnhancementMaterials.Purple, 95),
                    (EnhancementMaterials.Gold, 5),
                },
            },
            new[]
            {
                new[]
                {
                    (EnhancementMaterials.White, 840),
                    (EnhancementMaterials.Blue, 150),
                    (EnhancementMaterials.Purple, 10),
                    (EnhancementMaterials.Gold, 0),
                },
                new[]
                {
                    (EnhancementMaterials.White, 640),
                    (EnhancementMaterials.Blue, 310),
                    (EnhancementMaterials.Purple, 47),
                    (EnhancementMaterials.Gold, 3),
                },
                new[]
                {
                    (EnhancementMaterials.White, 320),
                    (EnhancementMaterials.Blue, 500),
                    (EnhancementMaterials.Purple, 165),
                    (EnhancementMaterials.Gold, 15),
                },
            },
            new[]
            {
                new[]
                {
                    (EnhancementMaterials.White, 780),
                    (EnhancementMaterials.Blue, 205),
                    (EnhancementMaterials.Purple, 15),
                    (EnhancementMaterials.Gold, 0),
                },
                new[]
                {
                    (EnhancementMaterials.White, 560),
                    (EnhancementMaterials.Blue, 380),
                    (EnhancementMaterials.Purple, 55),
                    (EnhancementMaterials.Gold, 5),
                },
                new[]
                {
                    (EnhancementMaterials.White, 220),
                    (EnhancementMaterials.Blue, 545),
                    (EnhancementMaterials.Purple, 200),
                    (EnhancementMaterials.Gold, 35),
                },
            },
        };

        private static (string Id, int Weight)[] PolishWeights(
            string destinationId,
            AdventureRoomKind row
        ) =>
            PolishWeightTable[PolishDepthOf(destinationId)][
                row switch
                {
                    AdventureRoomKind.Battle => 0,
                    AdventureRoomKind.Boss => 2,
                    _ => 1,
                }
            ];

        private static int ShardChance(AdventureRoomKind row) =>
            row switch
            {
                AdventureRoomKind.Battle => 0,
                AdventureRoomKind.Boss => 150,
                _ => 30,
            };

        /// <summary>
        /// The enhancement materials of a room, rolled after its other drops as the server's
        /// rollRoomDrops does: a polishing stone and an awakening shard, each rolled once.
        /// </summary>
        public static IReadOnlyList<AdventureDrop> RollMaterials(
            string destinationId,
            AdventureRoomKind row,
            string roomId,
            Random random
        )
        {
            var drops = new List<AdventureDrop>();
            if (random.Next(1000) < PolishChance(row))
                drops.Add(
                    new AdventureDrop(
                        roomId,
                        AdventureDropCategory.Material,
                        Pick(PolishWeights(destinationId, row), entry => entry.Weight, random).Id
                    )
                );
            if (random.Next(1000) < ShardChance(row))
                drops.Add(
                    new AdventureDrop(
                        roomId,
                        AdventureDropCategory.Material,
                        EnhancementMaterials.Shard
                    )
                );
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
                AdventureDropCategory.Material => "material",
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
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
                AdventureDropCategory.Material => "強化素材",
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
            };
    }
}
