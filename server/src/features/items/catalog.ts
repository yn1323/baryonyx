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
] as const;

export type ItemId = (typeof ITEM_IDS)[number];
