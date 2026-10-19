import { DEFAULT_MAX_MULTIPLIER_PERCENT } from "../../shared/quality.js";
import type { CharacterId } from "../party/catalog.js";

// 装備の種類と、キャラごとの装備枠。名前・レア度・効果はクライアントの仮データが持ち、
// サーバーは持っている装備と1つずつの倍率、どのキャラのどの枠に付けているかの整合を判定する（doc/features/equipment.md）。

export const EQUIPMENT_SLOTS = ["weapon", "armor"] as const;

export type EquipmentSlot = (typeof EQUIPMENT_SLOTS)[number];

// クライアントの仮データ（EquipmentCatalog）の順。一覧は入手の古い順、同時に入手した装備はこの順に返す。
export const EQUIPMENT = [
  { id: "flame-dagger", slot: "weapon" },
  { id: "thunder-spear", slot: "weapon" },
  { id: "frost-wand", slot: "weapon" },
  { id: "war-hammer", slot: "weapon" },
  { id: "iron-sword", slot: "weapon" },
  { id: "short-bow", slot: "weapon" },
  { id: "oak-staff", slot: "weapon" },
  { id: "wooden-sword", slot: "weapon" },
  { id: "traveler-cloak", slot: "armor" },
  { id: "chainmail", slot: "armor" },
  { id: "magic-robe", slot: "armor" },
  { id: "iron-helm", slot: "armor" },
  { id: "leather-armor", slot: "armor" },
  { id: "wooden-shield", slot: "armor" },
] as const satisfies readonly { id: string; slot: EquipmentSlot }[];

export type EquipmentId = (typeof EQUIPMENT)[number]["id"];

export const EQUIPMENT_IDS = EQUIPMENT.map((entry) => entry.id);

export function slotOf(id: string): EquipmentSlot | undefined {
  return EQUIPMENT.find((entry) => entry.id === id)?.slot;
}

// 装備の種類ごとの倍率の上限（百分率）。種類ごとの値が決まるまで、どれも仮の×1.5にする。
// クライアントの仮データ（EquipmentDefinition の MaxMultiplier）と同じ値にする。
export function maxMultiplierPercent(_id: EquipmentId): number {
  return DEFAULT_MAX_MULTIPLIER_PERCENT;
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
