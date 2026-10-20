import type { CharacterId } from "../party/catalog.js";

// 装備の種類と、キャラごとの装備枠。名前・レア度・効果はクライアントの仮データが持ち、
// サーバーは持っている装備と1つずつの倍率、どのキャラのどの枠に付けているかの整合を判定する（doc/features/equipment.md）。

export const EQUIPMENT_SLOTS = ["weapon", "armor"] as const;

export type EquipmentSlot = (typeof EQUIPMENT_SLOTS)[number];

// クライアントの仮データ（EquipmentCatalog）の順。一覧は入手の古い順、同時に入手した装備はこの順に返す。
// maxPercent は、種類ごとの倍率の上限（百分率）。クライアントの仮データ（EquipmentDefinition の MaxMultiplier）と同じ値にする。
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
] as const satisfies readonly {
  id: string;
  slot: EquipmentSlot;
  maxPercent: number;
}[];

export type EquipmentId = (typeof EQUIPMENT)[number]["id"];

export const EQUIPMENT_IDS = EQUIPMENT.map((entry) => entry.id);

export function slotOf(id: string): EquipmentSlot | undefined {
  return EQUIPMENT.find((entry) => entry.id === id)?.slot;
}

// 装備の種類ごとの倍率の上限（百分率）。
export function maxMultiplierPercent(id: EquipmentId): number {
  return EQUIPMENT.find((entry) => entry.id === id)?.maxPercent ?? 100;
}

// 最初から持っている装備（1つずつ）と、初めの4人が付けている装備。冒険と商会での入手ができるまでの仮設定。
// 最初の装備は、どれも倍率×1.00（品質C）にする。
export const STARTER_ITEMS: readonly EquipmentId[] = EQUIPMENT_IDS;

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
