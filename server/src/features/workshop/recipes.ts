import type { HoldingCategory } from "../inventory/catalog.js";
import type { WorkshopCategory } from "./catalog.js";

// 合成のレシピ（doc/features/synthesis.md のレシピ）。作る物ごとに、材料と数が決まっている。
// 材料はすべてそろえて、すべて使う。正本は作る物の定義の文書（doc/catalog/weapons・armor・accessories の
// README の「合成のレシピ」と、doc/catalog/skills の各スキルの「合成の材料」）で、ここはその写しである。
// クライアントの WorkshopRecipes が同じ値を持つ。

// 材料の分類。アイテムは種類ごとの数、それ以外（特別な材料）は持ち物の1つずつ。
export type RecipeMaterialCategory = Exclude<HoldingCategory, "material">;

export type CraftRecipe = {
  category: WorkshopCategory;
  id: string;
  materials: readonly {
    category: RecipeMaterialCategory;
    id: string;
    count: number;
  }[];
};

export const CRAFT_RECIPES: readonly CraftRecipe[] = [
  {
    category: "weapon",
    id: "iron-sword",
    materials: [
      { category: "item", id: "shell-shard", count: 2 },
      { category: "item", id: "sailor-rope", count: 1 },
    ],
  },
  {
    category: "weapon",
    id: "war-hammer",
    materials: [
      { category: "item", id: "dried-fish", count: 1 },
      { category: "item", id: "shell-shard", count: 1 },
      { category: "item", id: "sailor-rope", count: 1 },
    ],
  },
  {
    category: "weapon",
    id: "flame-dagger",
    materials: [
      { category: "weapon", id: "iron-sword", count: 1 },
      { category: "item", id: "pirate-rum", count: 1 },
      { category: "item", id: "ink-pot", count: 1 },
      { category: "item", id: "sailor-rope", count: 2 },
    ],
  },
  {
    category: "weapon",
    id: "thunder-spear",
    materials: [
      { category: "item", id: "icicle-shard", count: 2 },
      { category: "item", id: "glowworm-lamp", count: 1 },
      { category: "item", id: "cavern-water", count: 1 },
    ],
  },
  {
    category: "weapon",
    id: "frost-wand",
    materials: [
      { category: "item", id: "frost-crystal", count: 1 },
      { category: "item", id: "cavern-water", count: 2 },
      { category: "item", id: "icicle-shard", count: 1 },
    ],
  },
  {
    category: "weapon",
    id: "reed-bow",
    materials: [
      { category: "item", id: "lotus-root", count: 2 },
      { category: "item", id: "mud-ball", count: 1 },
    ],
  },
  {
    category: "weapon",
    id: "wisp-staff",
    materials: [
      { category: "item", id: "wisp-wick", count: 2 },
      { category: "item", id: "lotus-root", count: 1 },
      { category: "item", id: "mud-ball", count: 1 },
    ],
  },
  {
    category: "weapon",
    id: "scorpion-bow",
    materials: [
      { category: "item", id: "scorpion-stinger", count: 2 },
      { category: "item", id: "cactus-fruit", count: 1 },
    ],
  },
  {
    category: "weapon",
    id: "tomb-khopesh",
    materials: [
      { category: "item", id: "scorpion-stinger", count: 2 },
      { category: "item", id: "hourglass-sand", count: 1 },
      { category: "item", id: "cactus-fruit", count: 1 },
    ],
  },
  {
    category: "weapon",
    id: "bone-axe",
    materials: [
      { category: "item", id: "grave-candle", count: 1 },
      { category: "item", id: "moonflower-honey", count: 1 },
      { category: "item", id: "raven-feather", count: 1 },
    ],
  },
  {
    category: "weapon",
    id: "moonshade-staff",
    materials: [
      { category: "weapon", id: "oak-staff", count: 1 },
      { category: "item", id: "holy-water", count: 1 },
      { category: "item", id: "moonflower-honey", count: 2 },
      { category: "item", id: "raven-feather", count: 1 },
    ],
  },
  {
    category: "weapon",
    id: "firebird-bow",
    materials: [
      { category: "item", id: "lizard-tail", count: 2 },
      { category: "item", id: "sulfur-powder", count: 1 },
    ],
  },
  {
    category: "weapon",
    id: "magma-hammer",
    materials: [
      { category: "item", id: "lava-rock", count: 3 },
      { category: "item", id: "sulfur-powder", count: 1 },
    ],
  },
  {
    category: "weapon",
    id: "hailstone-staff",
    materials: [
      { category: "item", id: "crystal-horn", count: 1 },
      { category: "item", id: "hard-snowball", count: 3 },
    ],
  },
  {
    category: "weapon",
    id: "glacier-greatsword",
    materials: [
      { category: "weapon", id: "iron-sword", count: 1 },
      { category: "item", id: "crystal-horn", count: 2 },
      { category: "item", id: "hard-snowball", count: 2 },
    ],
  },
  {
    category: "armor",
    id: "iron-helm",
    materials: [
      { category: "item", id: "acorn", count: 2 },
      { category: "item", id: "herb", count: 1 },
    ],
  },
  {
    category: "armor",
    id: "chainmail",
    materials: [{ category: "item", id: "sailor-rope", count: 3 }],
  },
  {
    category: "armor",
    id: "magic-robe",
    materials: [
      { category: "item", id: "sea-salt", count: 2 },
      { category: "item", id: "sailor-rope", count: 1 },
    ],
  },
  {
    category: "armor",
    id: "traveler-cloak",
    materials: [
      { category: "item", id: "sailor-rope", count: 2 },
      { category: "item", id: "dried-fish", count: 1 },
      { category: "item", id: "sea-salt", count: 1 },
    ],
  },
  {
    category: "armor",
    id: "miner-helm",
    materials: [
      { category: "item", id: "icicle-shard", count: 2 },
      { category: "item", id: "glowworm-lamp", count: 1 },
    ],
  },
  {
    category: "armor",
    id: "crystal-shield",
    materials: [
      { category: "armor", id: "wooden-shield", count: 1 },
      { category: "item", id: "frost-crystal", count: 2 },
      { category: "item", id: "troll-stoneskin", count: 1 },
      { category: "item", id: "cavern-water", count: 1 },
    ],
  },
  {
    category: "armor",
    id: "mudcloth-robe",
    materials: [{ category: "item", id: "mud-ball", count: 3 }],
  },
  {
    category: "armor",
    id: "clay-armor",
    materials: [
      { category: "item", id: "mud-ball", count: 2 },
      { category: "item", id: "wisp-wick", count: 1 },
      { category: "item", id: "lotus-root", count: 1 },
    ],
  },
  {
    category: "armor",
    id: "burial-wraps",
    materials: [{ category: "item", id: "cactus-fruit", count: 3 }],
  },
  {
    category: "armor",
    id: "ancient-plate",
    materials: [
      { category: "armor", id: "burial-wraps", count: 1 },
      { category: "item", id: "grimoire-page", count: 1 },
      { category: "item", id: "hourglass-sand", count: 2 },
      { category: "item", id: "scorpion-stinger", count: 1 },
    ],
  },
  {
    category: "armor",
    id: "hollow-helm",
    materials: [{ category: "item", id: "grave-candle", count: 3 }],
  },
  {
    category: "armor",
    id: "grave-cloak",
    materials: [
      { category: "item", id: "raven-feather", count: 2 },
      { category: "item", id: "grave-candle", count: 2 },
    ],
  },
  {
    category: "armor",
    id: "lizardscale-mail",
    materials: [{ category: "item", id: "lizard-tail", count: 3 }],
  },
  {
    category: "armor",
    id: "obsidian-shield",
    materials: [
      { category: "item", id: "flametail-plume", count: 1 },
      { category: "item", id: "sulfur-powder", count: 2 },
      { category: "item", id: "lava-rock", count: 2 },
    ],
  },
  {
    category: "armor",
    id: "snow-cloak",
    materials: [
      { category: "item", id: "hard-snowball", count: 2 },
      { category: "item", id: "snowdrop", count: 1 },
    ],
  },
  {
    category: "armor",
    id: "mammoth-fur",
    materials: [
      { category: "item", id: "boar-jerky", count: 2 },
      { category: "item", id: "hard-snowball", count: 2 },
    ],
  },
  {
    category: "accessory",
    id: "acorn-brooch",
    materials: [{ category: "item", id: "acorn", count: 3 }],
  },
  {
    category: "accessory",
    id: "dew-pendant",
    materials: [
      { category: "item", id: "dew-vial", count: 2 },
      { category: "item", id: "moss-wad", count: 1 },
      { category: "item", id: "herb", count: 1 },
    ],
  },
  {
    category: "accessory",
    id: "anchor-bangle",
    materials: [
      { category: "item", id: "sailor-rope", count: 2 },
      { category: "item", id: "shell-shard", count: 1 },
    ],
  },
  {
    category: "accessory",
    id: "gold-earring",
    materials: [
      { category: "item", id: "ink-pot", count: 1 },
      { category: "item", id: "shell-shard", count: 2 },
      { category: "item", id: "sea-salt", count: 1 },
    ],
  },
  {
    category: "accessory",
    id: "crystal-ring",
    materials: [
      { category: "item", id: "frost-crystal", count: 1 },
      { category: "item", id: "icicle-shard", count: 3 },
    ],
  },
  {
    category: "accessory",
    id: "leech-bell",
    materials: [
      { category: "item", id: "lotus-root", count: 2 },
      { category: "item", id: "wisp-wick", count: 1 },
    ],
  },
  {
    category: "accessory",
    id: "frog-crown",
    materials: [
      { category: "item", id: "bog-venom", count: 1 },
      { category: "item", id: "lotus-root", count: 2 },
      { category: "item", id: "mud-ball", count: 1 },
    ],
  },
  {
    category: "accessory",
    id: "gargoyle-eye",
    materials: [
      { category: "item", id: "grimoire-page", count: 1 },
      { category: "item", id: "hourglass-sand", count: 1 },
      { category: "item", id: "cactus-fruit", count: 2 },
    ],
  },
  {
    category: "accessory",
    id: "bone-ring",
    materials: [
      { category: "item", id: "grave-candle", count: 2 },
      { category: "item", id: "moonflower-honey", count: 1 },
    ],
  },
  {
    category: "accessory",
    id: "moonstone-pendant",
    materials: [
      { category: "item", id: "holy-water", count: 1 },
      { category: "item", id: "moonflower-honey", count: 3 },
    ],
  },
  {
    category: "accessory",
    id: "magma-ring",
    materials: [
      { category: "item", id: "flametail-plume", count: 1 },
      { category: "item", id: "lava-rock", count: 2 },
      { category: "item", id: "lizard-tail", count: 1 },
    ],
  },
  {
    category: "accessory",
    id: "snowflake-brooch",
    materials: [{ category: "item", id: "snowdrop", count: 3 }],
  },
  {
    category: "accessory",
    id: "foxtail-charm",
    materials: [
      { category: "item", id: "crystal-horn", count: 1 },
      { category: "item", id: "snowdrop", count: 2 },
      { category: "item", id: "hard-snowball", count: 1 },
    ],
  },
  {
    category: "skill",
    id: "AbsoluteZero",
    materials: [
      { category: "skill", id: "Blizzard", count: 1 },
      { category: "item", id: "mistdragon-scale", count: 1 },
      { category: "item", id: "crystal-horn", count: 2 },
      { category: "item", id: "hard-snowball", count: 3 },
    ],
  },
  {
    category: "skill",
    id: "ArmorBreak",
    materials: [{ category: "item", id: "icicle-shard", count: 3 }],
  },
  {
    category: "skill",
    id: "BladeDance",
    materials: [
      { category: "item", id: "crystal-horn", count: 1 },
      { category: "item", id: "hard-snowball", count: 2 },
      { category: "item", id: "boar-jerky", count: 1 },
    ],
  },
  {
    category: "skill",
    id: "BlastSigil",
    materials: [
      { category: "item", id: "scorpion-stinger", count: 1 },
      { category: "item", id: "hourglass-sand", count: 1 },
      { category: "item", id: "cactus-fruit", count: 1 },
    ],
  },
  {
    category: "skill",
    id: "Blizzard",
    materials: [
      { category: "item", id: "moonflower-honey", count: 2 },
      { category: "item", id: "raven-feather", count: 1 },
    ],
  },
  {
    category: "skill",
    id: "DivineShield",
    materials: [
      { category: "skill", id: "Guard", count: 1 },
      { category: "item", id: "holy-water", count: 1 },
      { category: "item", id: "moonflower-honey", count: 2 },
      { category: "item", id: "raven-feather", count: 1 },
    ],
  },
  {
    category: "skill",
    id: "EarthSplitter",
    materials: [
      { category: "item", id: "hourglass-sand", count: 1 },
      { category: "item", id: "cactus-fruit", count: 2 },
    ],
  },
  {
    category: "skill",
    id: "Embers",
    materials: [
      { category: "item", id: "moss-wad", count: 2 },
      { category: "item", id: "herb", count: 1 },
    ],
  },
  {
    category: "skill",
    id: "FireStorm",
    materials: [
      { category: "item", id: "grave-candle", count: 2 },
      { category: "item", id: "raven-feather", count: 1 },
    ],
  },
  {
    category: "skill",
    id: "FlamePillar",
    materials: [
      { category: "item", id: "wisp-wick", count: 2 },
      { category: "item", id: "mud-ball", count: 1 },
    ],
  },
  {
    category: "skill",
    id: "Gale",
    materials: [
      { category: "item", id: "boar-jerky", count: 1 },
      { category: "item", id: "snowdrop", count: 2 },
    ],
  },
  {
    category: "skill",
    id: "GiantImpact",
    materials: [
      { category: "skill", id: "EarthSplitter", count: 1 },
      { category: "item", id: "crystal-horn", count: 2 },
      { category: "item", id: "boar-jerky", count: 2 },
    ],
  },
  {
    category: "skill",
    id: "GuardianOath",
    materials: [
      { category: "item", id: "lotus-root", count: 3 },
      { category: "item", id: "wisp-wick", count: 1 },
    ],
  },
  {
    category: "skill",
    id: "HolyLight",
    materials: [
      { category: "item", id: "grimoire-page", count: 2 },
      { category: "item", id: "cactus-fruit", count: 2 },
    ],
  },
  {
    category: "skill",
    id: "Hone",
    materials: [
      { category: "item", id: "spore-powder", count: 1 },
      { category: "item", id: "dew-vial", count: 2 },
      { category: "item", id: "acorn", count: 2 },
    ],
  },
  {
    category: "skill",
    id: "Iai",
    materials: [
      { category: "item", id: "raven-feather", count: 2 },
      { category: "item", id: "holy-water", count: 1 },
      { category: "item", id: "grave-candle", count: 1 },
    ],
  },
  {
    category: "skill",
    id: "IceMirror",
    materials: [
      { category: "skill", id: "Ice", count: 1 },
      { category: "item", id: "frost-crystal", count: 2 },
      { category: "item", id: "cavern-water", count: 2 },
    ],
  },
  {
    category: "skill",
    id: "InsightArrow",
    materials: [
      { category: "item", id: "wisp-wick", count: 1 },
      { category: "item", id: "lotus-root", count: 1 },
      { category: "item", id: "mud-ball", count: 1 },
    ],
  },
  {
    category: "skill",
    id: "ManaPrayer",
    materials: [
      { category: "item", id: "ink-pot", count: 2 },
      { category: "item", id: "sea-salt", count: 2 },
      { category: "item", id: "dried-fish", count: 1 },
    ],
  },
  {
    category: "skill",
    id: "Meteor",
    materials: [
      { category: "skill", id: "FireStorm", count: 1 },
      { category: "item", id: "obsidian-core", count: 1 },
      { category: "item", id: "flametail-plume", count: 2 },
      { category: "item", id: "lava-rock", count: 3 },
    ],
  },
  {
    category: "skill",
    id: "PoisonNeedle",
    materials: [{ category: "item", id: "shell-shard", count: 3 }],
  },
  {
    category: "skill",
    id: "Protect",
    materials: [
      { category: "item", id: "cavern-water", count: 2 },
      { category: "item", id: "glowworm-lamp", count: 1 },
    ],
  },
  {
    category: "skill",
    id: "QuickCast",
    materials: [
      { category: "skill", id: "ManaPrayer", count: 1 },
      { category: "item", id: "croc-tear", count: 1 },
      { category: "item", id: "bog-venom", count: 2 },
      { category: "item", id: "wisp-wick", count: 2 },
    ],
  },
  {
    category: "skill",
    id: "Resurrection",
    materials: [
      { category: "skill", id: "Heal", count: 1 },
      { category: "item", id: "holy-water", count: 2 },
      { category: "item", id: "grave-candle", count: 2 },
    ],
  },
  {
    category: "skill",
    id: "Revelation",
    materials: [
      { category: "skill", id: "Scout", count: 1 },
      { category: "item", id: "brass-spring", count: 1 },
      { category: "item", id: "grimoire-page", count: 2 },
      { category: "item", id: "hourglass-sand", count: 2 },
    ],
  },
  {
    category: "skill",
    id: "Scout",
    materials: [
      { category: "item", id: "ink-pot", count: 1 },
      { category: "item", id: "sailor-rope", count: 1 },
      { category: "item", id: "dried-fish", count: 2 },
    ],
  },
  {
    category: "skill",
    id: "ShadowSnipe",
    materials: [
      { category: "skill", id: "ShadowStitch", count: 1 },
      { category: "item", id: "flametail-plume", count: 1 },
      { category: "item", id: "sulfur-powder", count: 2 },
    ],
  },
  {
    category: "skill",
    id: "ShadowStitch",
    materials: [
      { category: "item", id: "glowworm-lamp", count: 2 },
      { category: "item", id: "cavern-water", count: 2 },
    ],
  },
  {
    category: "skill",
    id: "ShieldBash",
    materials: [
      { category: "item", id: "acorn", count: 2 },
      { category: "item", id: "moss-wad", count: 1 },
    ],
  },
  {
    category: "skill",
    id: "StepPrayer",
    materials: [
      { category: "item", id: "flametail-plume", count: 1 },
      { category: "item", id: "lizard-tail", count: 3 },
    ],
  },
  {
    category: "skill",
    id: "StrideStrike",
    materials: [
      { category: "item", id: "hourglass-sand", count: 2 },
      { category: "item", id: "scorpion-stinger", count: 2 },
    ],
  },
  {
    category: "skill",
    id: "ThunderSpear",
    materials: [
      { category: "item", id: "boar-jerky", count: 2 },
      { category: "item", id: "snowdrop", count: 1 },
      { category: "item", id: "hard-snowball", count: 1 },
    ],
  },
  {
    category: "skill",
    id: "Thunder",
    materials: [
      { category: "item", id: "sea-salt", count: 2 },
      { category: "item", id: "dried-fish", count: 1 },
    ],
  },
  {
    category: "skill",
    id: "Thundercloud",
    materials: [
      { category: "item", id: "sulfur-powder", count: 1 },
      { category: "item", id: "lava-rock", count: 2 },
    ],
  },
  {
    category: "skill",
    id: "WarCry",
    materials: [
      { category: "item", id: "lizard-tail", count: 1 },
      { category: "item", id: "lava-rock", count: 1 },
      { category: "item", id: "sulfur-powder", count: 1 },
    ],
  },
  {
    category: "skill",
    id: "Whirlwind",
    materials: [
      { category: "item", id: "bog-venom", count: 1 },
      { category: "item", id: "mud-ball", count: 2 },
      { category: "item", id: "wisp-wick", count: 1 },
    ],
  },
];

export function recipeOf(
  category: WorkshopCategory,
  id: string,
): CraftRecipe | undefined {
  return CRAFT_RECIPES.find(
    (recipe) => recipe.category === category && recipe.id === id,
  );
}
