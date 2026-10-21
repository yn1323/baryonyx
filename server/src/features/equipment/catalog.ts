import type { CharacterId } from "../party/catalog.js";

// 装備の種類と、キャラごとの装備枠。名前・レア度・効果はクライアントの仮データが持ち、
// サーバーは持っている装備と1つずつの倍率、どのキャラのどの枠に付けているかの整合を判定する（doc/features/equipment.md）。

export const EQUIPMENT_SLOTS = ["weapon", "armor"] as const;

export type EquipmentSlot = (typeof EQUIPMENT_SLOTS)[number];

// 装備の分類。アクセサリーも武器・防具と同じく1つずつ倍率を持って持ち物に入るが、付ける枠はまだない。
export type EquipmentKind = EquipmentSlot | "accessory";

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
