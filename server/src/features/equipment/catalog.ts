import type { CharacterId } from "../party/catalog.js";

// 装備の種類と、キャラごとの装備枠。名前・レア度・効果はクライアントの仮データが持ち、
// サーバーは持っている装備と1つずつの倍率、どのキャラのどの枠に付けているかの整合を判定する（doc/features/equipment.md）。

// 武器・防具は1つずつ、アクセサリーは3つ（doc/features/equipment.md のアクセサリー）。
export const EQUIPMENT_SLOTS = [
  "weapon",
  "armor",
  "accessory1",
  "accessory2",
  "accessory3",
] as const;

export type EquipmentSlot = (typeof EQUIPMENT_SLOTS)[number];

// 装備の分類。アクセサリーは、どのアクセサリーの枠にも付けられる。
export type EquipmentKind = "weapon" | "armor" | "accessory";

// アクセサリーの枠を開くレベル。1つ目は初めから開いている（2026-10-24、ユーザーの指示）。
// クライアントの AccessorySlots と同じ値にする。
export const ACCESSORY_UNLOCK_LEVELS = [1, 20, 40] as const;

export function kindOfSlot(slot: EquipmentSlot): EquipmentKind {
  return slot === "weapon" || slot === "armor" ? slot : "accessory";
}

// 枠を開くレベル。武器と防具の枠は初めから開いている。
export function unlockLevelOf(slot: EquipmentSlot): number {
  const index = ["accessory1", "accessory2", "accessory3"].indexOf(slot);
  return index < 0 ? 1 : ACCESSORY_UNLOCK_LEVELS[index];
}

// クライアントの仮データ（EquipmentCatalog）の順。一覧は入手の古い順、同時に入手した装備はこの順に返す。
// slot は分類（武器・防具・アクセサリー）。maxPercent は、種類ごとの倍率の上限（百分率）。
// クライアントの仮データ（EquipmentDefinition の MaxMultiplier）と同じ値にする。
export const EQUIPMENT = [
  { id: "flame-dagger", slot: "weapon", maxPercent: 180 },
  { id: "thunder-spear", slot: "weapon", maxPercent: 240 },
  { id: "frost-wand", slot: "weapon", maxPercent: 220 },
  { id: "war-hammer", slot: "weapon", maxPercent: 260 },
  { id: "iron-sword", slot: "weapon", maxPercent: 200 },
  { id: "short-bow", slot: "weapon", maxPercent: 200 },
  { id: "oak-staff", slot: "weapon", maxPercent: 200 },
  { id: "wooden-sword", slot: "weapon", maxPercent: 180 },
  { id: "traveler-cloak", slot: "armor", maxPercent: 220 },
  { id: "chainmail", slot: "armor", maxPercent: 240 },
  { id: "magic-robe", slot: "armor", maxPercent: 220 },
  { id: "iron-helm", slot: "armor", maxPercent: 200 },
  { id: "leather-armor", slot: "armor", maxPercent: 180 },
  { id: "wooden-shield", slot: "armor", maxPercent: 200 },
  // ミストラ遺跡のアクセサリー
  { id: "moss-charm", slot: "accessory", maxPercent: 200 },
  { id: "acorn-brooch", slot: "accessory", maxPercent: 200 },
  { id: "dew-pendant", slot: "accessory", maxPercent: 220 },
  { id: "guardian-circlet", slot: "accessory", maxPercent: 240 },
  // 港町ポルトリアのアクセサリー
  { id: "shell-necklace", slot: "accessory", maxPercent: 200 },
  { id: "anchor-bangle", slot: "accessory", maxPercent: 220 },
  { id: "gold-earring", slot: "accessory", maxPercent: 220 },
  { id: "kraken-pearl", slot: "accessory", maxPercent: 260 },
  // グラシエラ氷窟（武器は上の雷鳴の槍・氷晶のワンド）
  { id: "miner-helm", slot: "armor", maxPercent: 200 },
  { id: "crystal-shield", slot: "armor", maxPercent: 240 },
  { id: "penguin-feather", slot: "accessory", maxPercent: 200 },
  { id: "crystal-ring", slot: "accessory", maxPercent: 220 },
  { id: "troll-heartstone", slot: "accessory", maxPercent: 240 },
  // しじまの沼
  { id: "reed-bow", slot: "weapon", maxPercent: 200 },
  { id: "wisp-staff", slot: "weapon", maxPercent: 220 },
  { id: "mudcloth-robe", slot: "armor", maxPercent: 220 },
  { id: "clay-armor", slot: "armor", maxPercent: 240 },
  { id: "leech-bell", slot: "accessory", maxPercent: 200 },
  { id: "frog-crown", slot: "accessory", maxPercent: 220 },
  { id: "croc-fang", slot: "accessory", maxPercent: 260 },
  // アルマジャ王墓
  { id: "scorpion-bow", slot: "weapon", maxPercent: 200 },
  { id: "tomb-khopesh", slot: "weapon", maxPercent: 220 },
  { id: "burial-wraps", slot: "armor", maxPercent: 200 },
  { id: "ancient-plate", slot: "armor", maxPercent: 240 },
  { id: "scarab-amulet", slot: "accessory", maxPercent: 200 },
  { id: "gargoyle-eye", slot: "accessory", maxPercent: 220 },
  { id: "golden-mask", slot: "accessory", maxPercent: 260 },
  // 月影の墓所
  { id: "bone-axe", slot: "weapon", maxPercent: 240 },
  { id: "moonshade-staff", slot: "weapon", maxPercent: 220 },
  { id: "hollow-helm", slot: "armor", maxPercent: 200 },
  { id: "grave-cloak", slot: "armor", maxPercent: 220 },
  { id: "bone-ring", slot: "accessory", maxPercent: 200 },
  { id: "moonstone-pendant", slot: "accessory", maxPercent: 220 },
  { id: "lich-signet", slot: "accessory", maxPercent: 260 },
  // ヴォルガン火山
  { id: "firebird-bow", slot: "weapon", maxPercent: 200 },
  { id: "magma-hammer", slot: "weapon", maxPercent: 260 },
  { id: "lizardscale-mail", slot: "armor", maxPercent: 220 },
  { id: "obsidian-shield", slot: "armor", maxPercent: 240 },
  { id: "ember-charm", slot: "accessory", maxPercent: 200 },
  { id: "magma-ring", slot: "accessory", maxPercent: 220 },
  { id: "obsidian-crown", slot: "accessory", maxPercent: 260 },
  // 白嶺ネーヴェ
  { id: "hailstone-staff", slot: "weapon", maxPercent: 220 },
  { id: "glacier-greatsword", slot: "weapon", maxPercent: 240 },
  { id: "snow-cloak", slot: "armor", maxPercent: 220 },
  { id: "mammoth-fur", slot: "armor", maxPercent: 220 },
  { id: "snowflake-brooch", slot: "accessory", maxPercent: 200 },
  { id: "foxtail-charm", slot: "accessory", maxPercent: 220 },
  { id: "mistdragon-eye", slot: "accessory", maxPercent: 260 },
] as const satisfies readonly {
  id: string;
  slot: EquipmentKind;
  maxPercent: number;
}[];

export type EquipmentId = (typeof EQUIPMENT)[number]["id"];

export const EQUIPMENT_IDS = EQUIPMENT.map((entry) => entry.id);

export function slotOf(id: string): EquipmentKind | undefined {
  return EQUIPMENT.find((entry) => entry.id === id)?.slot;
}

// 装備の種類ごとの倍率の上限（百分率）。
export function maxMultiplierPercent(id: EquipmentId): number {
  return EQUIPMENT.find((entry) => entry.id === id)?.maxPercent ?? 100;
}

// 初めの4人が付けている装備。最初から持っている装備はこれだけで、ほかは冒険のドロップで手に入れる
// （doc/features/drops.md、2026-10-20）。最初の装備は、どれも倍率×1.00（品質C）にする。
export const STARTER_EQUIPPED: readonly {
  characterId: CharacterId;
  weapon: EquipmentId;
  armor: EquipmentId;
}[] = [
  { characterId: "toma", weapon: "oak-staff", armor: "magic-robe" },
  { characterId: "luka", weapon: "short-bow", armor: "traveler-cloak" },
  { characterId: "aria", weapon: "iron-sword", armor: "chainmail" },
  { characterId: "mina", weapon: "war-hammer", armor: "leather-armor" },
];

// 仮データの順に並べる。
export const STARTER_ITEMS: readonly EquipmentId[] = EQUIPMENT_IDS.filter(
  (id) =>
    STARTER_EQUIPPED.some((entry) => entry.weapon === id || entry.armor === id),
);
