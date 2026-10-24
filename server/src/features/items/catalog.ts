// アイテム（道具・素材）の種類。名前・レア度・戦闘での効果はクライアントの定義（ItemCards）が持ち、
// サーバーは種類ごとに持っている数だけを数える（doc/features/items.md）。アイテムは品質も倍率も持たない。
// どの行き先で見つかるかは、冒険のドロップ（adventure/drops.ts）が決める。

export const ITEM_IDS = [
  // ミストラ遺跡
  "herb",
  "acorn",
  "moss-wad",
  "dew-vial",
  "spore-powder",
  "guardian-seed",
  // 港町ポルトリア
  "dried-fish",
  "shell-shard",
  "sailor-rope",
  "sea-salt",
  "ink-pot",
  "pirate-rum",
  // グラシエラ氷窟
  "icicle-shard",
  "cavern-water",
  "glowworm-lamp",
  "frost-crystal",
  "troll-stoneskin",
  // しじまの沼
  "mud-ball",
  "lotus-root",
  "wisp-wick",
  "bog-venom",
  "croc-tear",
  // アルマジャ王墓
  "cactus-fruit",
  "scorpion-stinger",
  "hourglass-sand",
  "grimoire-page",
  "brass-spring",
  // 月影の墓所
  "grave-candle",
  "moonflower-honey",
  "raven-feather",
  "holy-water",
  "soul-phial",
  // ヴォルガン火山
  "lava-rock",
  "lizard-tail",
  "sulfur-powder",
  "flametail-plume",
  "obsidian-core",
  // 白嶺ネーヴェ
  "hard-snowball",
  "snowdrop",
  "boar-jerky",
  "crystal-horn",
  "mistdragon-scale",
] as const;

export type ItemId = (typeof ITEM_IDS)[number];
