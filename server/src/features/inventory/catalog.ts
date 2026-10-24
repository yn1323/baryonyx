import {
  type EquipmentId,
  maxMultiplierPercent as equipmentMaxPercent,
} from "../equipment/catalog.js";
import {
  type CardSkillId,
  maxMultiplierPercent as skillMaxPercent,
} from "../party/catalog.js";

// 持ち物の分類（doc/features/inventory.md）。スキル・装備は1つずつ倍率を持ち、アイテムと強化素材は種類ごとに数を持つ。
export type EquipmentCategory = "weapon" | "armor" | "accessory";

export type HoldingCategory = "skill" | EquipmentCategory | "item" | "material";

// レア度（☆）。クライアントの定義と同じ値にする。
export type Stars = 1 | 2 | 3 | 4 | 5;

export const isEquipmentCategory = (
  category: string,
): category is EquipmentCategory =>
  category === "weapon" || category === "armor" || category === "accessory";

// 物の倍率の上限（百分率）。アイテムと強化素材は倍率を持たない。抽選と、応答の品質を求めるのに使う。
export function maxPercentOf(
  category: HoldingCategory,
  id: string,
): number | null {
  switch (category) {
    case "item":
    case "material":
      return null;
    case "skill":
      return skillMaxPercent(id as CardSkillId);
    default:
      return equipmentMaxPercent(id as EquipmentId);
  }
}
