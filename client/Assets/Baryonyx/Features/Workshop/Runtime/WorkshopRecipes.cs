using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Library;

namespace Baryonyx.Workshop
{
    /// <summary>One material of a recipe: an item kind by count, or one held piece (a special material).</summary>
    public readonly struct RecipeMaterial
    {
        public RecipeMaterial(LibraryKind kind, string id, int count)
        {
            Kind = kind;
            Id = id;
            Count = count;
        }

        public LibraryKind Kind { get; }
        public string Id { get; }
        public int Count { get; }

        // アイテムは種類ごとの数で使い、それ以外（装備・スキル）は持ち物の1つずつを選んで使う。
        public bool IsItem => Kind == LibraryKind.Item;
    }

    /// <summary>What one thing is made from: its kind and id, and every material it takes.</summary>
    public sealed class RecipeDefinition
    {
        public RecipeDefinition(
            LibraryKind kind,
            string id,
            IReadOnlyList<RecipeMaterial> materials
        )
        {
            Kind = kind;
            Id = id;
            Materials = materials;
        }

        public LibraryKind Kind { get; }
        public string Id { get; }
        public IReadOnlyList<RecipeMaterial> Materials { get; }
    }

    /// <summary>
    /// The craft's recipes (doc/features/synthesis.md#レシピ). The documents of the made things
    /// (doc/catalog/weapons, armor and accessories, and each skill's) are the source; this is
    /// their copy, the same as the server's (server/src/features/workshop/recipes.ts).
    /// </summary>
    public static class WorkshopRecipes
    {
        public static readonly IReadOnlyList<RecipeDefinition> All = new[]
        {
            R(
                LibraryKind.Weapon,
                "iron-sword",
                M(LibraryKind.Item, "shell-shard", 2),
                M(LibraryKind.Item, "sailor-rope", 1)
            ),
            R(
                LibraryKind.Weapon,
                "war-hammer",
                M(LibraryKind.Item, "dried-fish", 1),
                M(LibraryKind.Item, "shell-shard", 1),
                M(LibraryKind.Item, "sailor-rope", 1)
            ),
            R(
                LibraryKind.Weapon,
                "flame-dagger",
                M(LibraryKind.Weapon, "iron-sword", 1),
                M(LibraryKind.Item, "pirate-rum", 1),
                M(LibraryKind.Item, "ink-pot", 1),
                M(LibraryKind.Item, "sailor-rope", 2)
            ),
            R(
                LibraryKind.Weapon,
                "thunder-spear",
                M(LibraryKind.Item, "icicle-shard", 2),
                M(LibraryKind.Item, "glowworm-lamp", 1),
                M(LibraryKind.Item, "cavern-water", 1)
            ),
            R(
                LibraryKind.Weapon,
                "frost-wand",
                M(LibraryKind.Item, "frost-crystal", 1),
                M(LibraryKind.Item, "cavern-water", 2),
                M(LibraryKind.Item, "icicle-shard", 1)
            ),
            R(
                LibraryKind.Weapon,
                "reed-bow",
                M(LibraryKind.Item, "lotus-root", 2),
                M(LibraryKind.Item, "mud-ball", 1)
            ),
            R(
                LibraryKind.Weapon,
                "wisp-staff",
                M(LibraryKind.Item, "wisp-wick", 2),
                M(LibraryKind.Item, "lotus-root", 1),
                M(LibraryKind.Item, "mud-ball", 1)
            ),
            R(
                LibraryKind.Weapon,
                "scorpion-bow",
                M(LibraryKind.Item, "scorpion-stinger", 2),
                M(LibraryKind.Item, "cactus-fruit", 1)
            ),
            R(
                LibraryKind.Weapon,
                "tomb-khopesh",
                M(LibraryKind.Item, "scorpion-stinger", 2),
                M(LibraryKind.Item, "hourglass-sand", 1),
                M(LibraryKind.Item, "cactus-fruit", 1)
            ),
            R(
                LibraryKind.Weapon,
                "bone-axe",
                M(LibraryKind.Item, "grave-candle", 1),
                M(LibraryKind.Item, "moonflower-honey", 1),
                M(LibraryKind.Item, "raven-feather", 1)
            ),
            R(
                LibraryKind.Weapon,
                "moonshade-staff",
                M(LibraryKind.Weapon, "oak-staff", 1),
                M(LibraryKind.Item, "holy-water", 1),
                M(LibraryKind.Item, "moonflower-honey", 2),
                M(LibraryKind.Item, "raven-feather", 1)
            ),
            R(
                LibraryKind.Weapon,
                "firebird-bow",
                M(LibraryKind.Item, "lizard-tail", 2),
                M(LibraryKind.Item, "sulfur-powder", 1)
            ),
            R(
                LibraryKind.Weapon,
                "magma-hammer",
                M(LibraryKind.Item, "lava-rock", 3),
                M(LibraryKind.Item, "sulfur-powder", 1)
            ),
            R(
                LibraryKind.Weapon,
                "hailstone-staff",
                M(LibraryKind.Item, "crystal-horn", 1),
                M(LibraryKind.Item, "hard-snowball", 3)
            ),
            R(
                LibraryKind.Weapon,
                "glacier-greatsword",
                M(LibraryKind.Weapon, "iron-sword", 1),
                M(LibraryKind.Item, "crystal-horn", 2),
                M(LibraryKind.Item, "hard-snowball", 2)
            ),
            R(
                LibraryKind.Armor,
                "iron-helm",
                M(LibraryKind.Item, "acorn", 2),
                M(LibraryKind.Item, "herb", 1)
            ),
            R(LibraryKind.Armor, "chainmail", M(LibraryKind.Item, "sailor-rope", 3)),
            R(
                LibraryKind.Armor,
                "magic-robe",
                M(LibraryKind.Item, "sea-salt", 2),
                M(LibraryKind.Item, "sailor-rope", 1)
            ),
            R(
                LibraryKind.Armor,
                "traveler-cloak",
                M(LibraryKind.Item, "sailor-rope", 2),
                M(LibraryKind.Item, "dried-fish", 1),
                M(LibraryKind.Item, "sea-salt", 1)
            ),
            R(
                LibraryKind.Armor,
                "miner-helm",
                M(LibraryKind.Item, "icicle-shard", 2),
                M(LibraryKind.Item, "glowworm-lamp", 1)
            ),
            R(
                LibraryKind.Armor,
                "crystal-shield",
                M(LibraryKind.Armor, "wooden-shield", 1),
                M(LibraryKind.Item, "frost-crystal", 2),
                M(LibraryKind.Item, "troll-stoneskin", 1),
                M(LibraryKind.Item, "cavern-water", 1)
            ),
            R(LibraryKind.Armor, "mudcloth-robe", M(LibraryKind.Item, "mud-ball", 3)),
            R(
                LibraryKind.Armor,
                "clay-armor",
                M(LibraryKind.Item, "mud-ball", 2),
                M(LibraryKind.Item, "wisp-wick", 1),
                M(LibraryKind.Item, "lotus-root", 1)
            ),
            R(LibraryKind.Armor, "burial-wraps", M(LibraryKind.Item, "cactus-fruit", 3)),
            R(
                LibraryKind.Armor,
                "ancient-plate",
                M(LibraryKind.Armor, "burial-wraps", 1),
                M(LibraryKind.Item, "grimoire-page", 1),
                M(LibraryKind.Item, "hourglass-sand", 2),
                M(LibraryKind.Item, "scorpion-stinger", 1)
            ),
            R(LibraryKind.Armor, "hollow-helm", M(LibraryKind.Item, "grave-candle", 3)),
            R(
                LibraryKind.Armor,
                "grave-cloak",
                M(LibraryKind.Item, "raven-feather", 2),
                M(LibraryKind.Item, "grave-candle", 2)
            ),
            R(LibraryKind.Armor, "lizardscale-mail", M(LibraryKind.Item, "lizard-tail", 3)),
            R(
                LibraryKind.Armor,
                "obsidian-shield",
                M(LibraryKind.Item, "flametail-plume", 1),
                M(LibraryKind.Item, "sulfur-powder", 2),
                M(LibraryKind.Item, "lava-rock", 2)
            ),
            R(
                LibraryKind.Armor,
                "snow-cloak",
                M(LibraryKind.Item, "hard-snowball", 2),
                M(LibraryKind.Item, "snowdrop", 1)
            ),
            R(
                LibraryKind.Armor,
                "mammoth-fur",
                M(LibraryKind.Item, "boar-jerky", 2),
                M(LibraryKind.Item, "hard-snowball", 2)
            ),
            R(LibraryKind.Accessory, "acorn-brooch", M(LibraryKind.Item, "acorn", 3)),
            R(
                LibraryKind.Accessory,
                "dew-pendant",
                M(LibraryKind.Item, "dew-vial", 2),
                M(LibraryKind.Item, "moss-wad", 1),
                M(LibraryKind.Item, "herb", 1)
            ),
            R(
                LibraryKind.Accessory,
                "anchor-bangle",
                M(LibraryKind.Item, "sailor-rope", 2),
                M(LibraryKind.Item, "shell-shard", 1)
            ),
            R(
                LibraryKind.Accessory,
                "gold-earring",
                M(LibraryKind.Item, "ink-pot", 1),
                M(LibraryKind.Item, "shell-shard", 2),
                M(LibraryKind.Item, "sea-salt", 1)
            ),
            R(
                LibraryKind.Accessory,
                "crystal-ring",
                M(LibraryKind.Item, "frost-crystal", 1),
                M(LibraryKind.Item, "icicle-shard", 3)
            ),
            R(
                LibraryKind.Accessory,
                "leech-bell",
                M(LibraryKind.Item, "lotus-root", 2),
                M(LibraryKind.Item, "wisp-wick", 1)
            ),
            R(
                LibraryKind.Accessory,
                "frog-crown",
                M(LibraryKind.Item, "bog-venom", 1),
                M(LibraryKind.Item, "lotus-root", 2),
                M(LibraryKind.Item, "mud-ball", 1)
            ),
            R(
                LibraryKind.Accessory,
                "gargoyle-eye",
                M(LibraryKind.Item, "grimoire-page", 1),
                M(LibraryKind.Item, "hourglass-sand", 1),
                M(LibraryKind.Item, "cactus-fruit", 2)
            ),
            R(
                LibraryKind.Accessory,
                "bone-ring",
                M(LibraryKind.Item, "grave-candle", 2),
                M(LibraryKind.Item, "moonflower-honey", 1)
            ),
            R(
                LibraryKind.Accessory,
                "moonstone-pendant",
                M(LibraryKind.Item, "holy-water", 1),
                M(LibraryKind.Item, "moonflower-honey", 3)
            ),
            R(
                LibraryKind.Accessory,
                "magma-ring",
                M(LibraryKind.Item, "flametail-plume", 1),
                M(LibraryKind.Item, "lava-rock", 2),
                M(LibraryKind.Item, "lizard-tail", 1)
            ),
            R(LibraryKind.Accessory, "snowflake-brooch", M(LibraryKind.Item, "snowdrop", 3)),
            R(
                LibraryKind.Accessory,
                "foxtail-charm",
                M(LibraryKind.Item, "crystal-horn", 1),
                M(LibraryKind.Item, "snowdrop", 2),
                M(LibraryKind.Item, "hard-snowball", 1)
            ),
            R(
                LibraryKind.Skill,
                "AbsoluteZero",
                M(LibraryKind.Skill, "Blizzard", 1),
                M(LibraryKind.Item, "mistdragon-scale", 1),
                M(LibraryKind.Item, "crystal-horn", 2),
                M(LibraryKind.Item, "hard-snowball", 3)
            ),
            R(LibraryKind.Skill, "ArmorBreak", M(LibraryKind.Item, "icicle-shard", 3)),
            R(
                LibraryKind.Skill,
                "BladeDance",
                M(LibraryKind.Item, "crystal-horn", 1),
                M(LibraryKind.Item, "hard-snowball", 2),
                M(LibraryKind.Item, "boar-jerky", 1)
            ),
            R(
                LibraryKind.Skill,
                "BlastSigil",
                M(LibraryKind.Item, "scorpion-stinger", 1),
                M(LibraryKind.Item, "hourglass-sand", 1),
                M(LibraryKind.Item, "cactus-fruit", 1)
            ),
            R(
                LibraryKind.Skill,
                "Blizzard",
                M(LibraryKind.Item, "moonflower-honey", 2),
                M(LibraryKind.Item, "raven-feather", 1)
            ),
            R(
                LibraryKind.Skill,
                "DivineShield",
                M(LibraryKind.Skill, "Guard", 1),
                M(LibraryKind.Item, "holy-water", 1),
                M(LibraryKind.Item, "moonflower-honey", 2),
                M(LibraryKind.Item, "raven-feather", 1)
            ),
            R(
                LibraryKind.Skill,
                "EarthSplitter",
                M(LibraryKind.Item, "hourglass-sand", 1),
                M(LibraryKind.Item, "cactus-fruit", 2)
            ),
            R(
                LibraryKind.Skill,
                "Embers",
                M(LibraryKind.Item, "moss-wad", 2),
                M(LibraryKind.Item, "herb", 1)
            ),
            R(
                LibraryKind.Skill,
                "FireStorm",
                M(LibraryKind.Item, "grave-candle", 2),
                M(LibraryKind.Item, "raven-feather", 1)
            ),
            R(
                LibraryKind.Skill,
                "FlamePillar",
                M(LibraryKind.Item, "wisp-wick", 2),
                M(LibraryKind.Item, "mud-ball", 1)
            ),
            R(
                LibraryKind.Skill,
                "Gale",
                M(LibraryKind.Item, "boar-jerky", 1),
                M(LibraryKind.Item, "snowdrop", 2)
            ),
            R(
                LibraryKind.Skill,
                "GiantImpact",
                M(LibraryKind.Skill, "EarthSplitter", 1),
                M(LibraryKind.Item, "crystal-horn", 2),
                M(LibraryKind.Item, "boar-jerky", 2)
            ),
            R(
                LibraryKind.Skill,
                "GuardianOath",
                M(LibraryKind.Item, "lotus-root", 3),
                M(LibraryKind.Item, "wisp-wick", 1)
            ),
            R(
                LibraryKind.Skill,
                "HolyLight",
                M(LibraryKind.Item, "grimoire-page", 2),
                M(LibraryKind.Item, "cactus-fruit", 2)
            ),
            R(
                LibraryKind.Skill,
                "Hone",
                M(LibraryKind.Item, "spore-powder", 1),
                M(LibraryKind.Item, "dew-vial", 2),
                M(LibraryKind.Item, "acorn", 2)
            ),
            R(
                LibraryKind.Skill,
                "Iai",
                M(LibraryKind.Item, "raven-feather", 2),
                M(LibraryKind.Item, "holy-water", 1),
                M(LibraryKind.Item, "grave-candle", 1)
            ),
            R(
                LibraryKind.Skill,
                "IceMirror",
                M(LibraryKind.Skill, "Ice", 1),
                M(LibraryKind.Item, "frost-crystal", 2),
                M(LibraryKind.Item, "cavern-water", 2)
            ),
            R(
                LibraryKind.Skill,
                "InsightArrow",
                M(LibraryKind.Item, "wisp-wick", 1),
                M(LibraryKind.Item, "lotus-root", 1),
                M(LibraryKind.Item, "mud-ball", 1)
            ),
            R(
                LibraryKind.Skill,
                "ManaPrayer",
                M(LibraryKind.Item, "ink-pot", 2),
                M(LibraryKind.Item, "sea-salt", 2),
                M(LibraryKind.Item, "dried-fish", 1)
            ),
            R(
                LibraryKind.Skill,
                "Meteor",
                M(LibraryKind.Skill, "FireStorm", 1),
                M(LibraryKind.Item, "obsidian-core", 1),
                M(LibraryKind.Item, "flametail-plume", 2),
                M(LibraryKind.Item, "lava-rock", 3)
            ),
            R(LibraryKind.Skill, "PoisonNeedle", M(LibraryKind.Item, "shell-shard", 3)),
            R(
                LibraryKind.Skill,
                "Protect",
                M(LibraryKind.Item, "cavern-water", 2),
                M(LibraryKind.Item, "glowworm-lamp", 1)
            ),
            R(
                LibraryKind.Skill,
                "QuickCast",
                M(LibraryKind.Skill, "ManaPrayer", 1),
                M(LibraryKind.Item, "croc-tear", 1),
                M(LibraryKind.Item, "bog-venom", 2),
                M(LibraryKind.Item, "wisp-wick", 2)
            ),
            R(
                LibraryKind.Skill,
                "Resurrection",
                M(LibraryKind.Skill, "Heal", 1),
                M(LibraryKind.Item, "holy-water", 2),
                M(LibraryKind.Item, "grave-candle", 2)
            ),
            R(
                LibraryKind.Skill,
                "Revelation",
                M(LibraryKind.Skill, "Scout", 1),
                M(LibraryKind.Item, "brass-spring", 1),
                M(LibraryKind.Item, "grimoire-page", 2),
                M(LibraryKind.Item, "hourglass-sand", 2)
            ),
            R(
                LibraryKind.Skill,
                "Scout",
                M(LibraryKind.Item, "ink-pot", 1),
                M(LibraryKind.Item, "sailor-rope", 1),
                M(LibraryKind.Item, "dried-fish", 2)
            ),
            R(
                LibraryKind.Skill,
                "ShadowSnipe",
                M(LibraryKind.Skill, "ShadowStitch", 1),
                M(LibraryKind.Item, "flametail-plume", 1),
                M(LibraryKind.Item, "sulfur-powder", 2)
            ),
            R(
                LibraryKind.Skill,
                "ShadowStitch",
                M(LibraryKind.Item, "glowworm-lamp", 2),
                M(LibraryKind.Item, "cavern-water", 2)
            ),
            R(
                LibraryKind.Skill,
                "ShieldBash",
                M(LibraryKind.Item, "acorn", 2),
                M(LibraryKind.Item, "moss-wad", 1)
            ),
            R(
                LibraryKind.Skill,
                "StepPrayer",
                M(LibraryKind.Item, "flametail-plume", 1),
                M(LibraryKind.Item, "lizard-tail", 3)
            ),
            R(
                LibraryKind.Skill,
                "StrideStrike",
                M(LibraryKind.Item, "hourglass-sand", 2),
                M(LibraryKind.Item, "scorpion-stinger", 2)
            ),
            R(
                LibraryKind.Skill,
                "ThunderSpear",
                M(LibraryKind.Item, "boar-jerky", 2),
                M(LibraryKind.Item, "snowdrop", 1),
                M(LibraryKind.Item, "hard-snowball", 1)
            ),
            R(
                LibraryKind.Skill,
                "Thunder",
                M(LibraryKind.Item, "sea-salt", 2),
                M(LibraryKind.Item, "dried-fish", 1)
            ),
            R(
                LibraryKind.Skill,
                "Thundercloud",
                M(LibraryKind.Item, "sulfur-powder", 1),
                M(LibraryKind.Item, "lava-rock", 2)
            ),
            R(
                LibraryKind.Skill,
                "WarCry",
                M(LibraryKind.Item, "lizard-tail", 1),
                M(LibraryKind.Item, "lava-rock", 1),
                M(LibraryKind.Item, "sulfur-powder", 1)
            ),
            R(
                LibraryKind.Skill,
                "Whirlwind",
                M(LibraryKind.Item, "bog-venom", 1),
                M(LibraryKind.Item, "mud-ball", 2),
                M(LibraryKind.Item, "wisp-wick", 1)
            ),
        };

        /// <summary>The recipe that makes the kind, or null for one not made by crafting.</summary>
        public static RecipeDefinition Find(LibraryKind kind, string id) =>
            All.FirstOrDefault(recipe => recipe.Kind == kind && recipe.Id == id);

        private static RecipeDefinition R(
            LibraryKind kind,
            string id,
            params RecipeMaterial[] materials
        ) => new(kind, id, Array.AsReadOnly(materials));

        private static RecipeMaterial M(LibraryKind kind, string id, int count) =>
            new(kind, id, count);
    }
}
