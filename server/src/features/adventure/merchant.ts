import {
  pickQuality,
  type Quality,
  qualityOf,
  rollMultiplierPercent,
} from "../../shared/quality.js";
import {
  CATEGORY_WEIGHTS,
  type DropEntry,
  eligibleEntries,
  maxPercentOf,
  pickWeighted,
  QUALITY_WEIGHTS,
  type RewardRow,
  type Stars,
  starWeight,
} from "./drops.js";

// 行商人のマスの品ぞろえ（doc/features/stage-progression.md のマスの中身）。
// マスに入ったときにサーバーが作って保存し、そのマスにいるあいだルーンで買える。Homeの商会とは別の一覧で、
// 冒険・マスごとに変わる。品物は行き先のドロップの一覧から選び、主の物・アイテムは並べない。
// ☆と品質は強い報酬（強い魔物）の行の重みで選び、倍率の出し方はドロップと同じにする。

export type MerchantCategory = "skill" | "weapon" | "armor" | "accessory";

export type MerchantGoods = {
  category: MerchantCategory;
  id: string;
  multiplierPercent: number;
  price: number;
};

// 並べる数。スキル3つと装備（武器・防具・アクセサリー）2つ。分類に物が足りなければ、ほかの分類で埋める。
export const MERCHANT_SKILLS = 3;
export const MERCHANT_EQUIPMENT = 2;

const MERCHANT_ROW: RewardRow = "elite";

// 装備の分類は、ドロップと同じ順と重み（アクセサリー＞武器＝防具）で選ぶ。
const EQUIPMENT_CATEGORIES = ["accessory", "weapon", "armor"] as const;

// 値段（ルーン）。☆ごとの額に品質の倍率を掛け、10の位で丸める。
// 仮の値（額は未決。doc/features/economy.md のルーンの用途）。
export const MERCHANT_STAR_PRICES: Record<Stars, number> = {
  1: 300,
  2: 600,
  3: 1200,
  4: 2400,
  5: 4800,
};

export const MERCHANT_QUALITY_FACTORS: Record<Quality, number> = {
  C: 1,
  B: 1.25,
  A: 1.5,
  S: 2,
};

export function merchantPrice(stars: Stars, quality: Quality) {
  return (
    Math.round(
      (MERCHANT_STAR_PRICES[stars] * MERCHANT_QUALITY_FACTORS[quality]) / 10,
    ) * 10
  );
}

// 行商人の品ぞろえを選ぶ。同じ物は2つ並べない。スキルを先に、装備をあとに並べる。
// 乱数は、スキル1つにつき物・品質・倍率、装備1つにつき分類・物・品質・倍率の順に引く。
export function rollMerchantGoods(
  destinationId: string,
  random: () => number,
): MerchantGoods[] {
  const pool = eligibleEntries(destinationId, MERCHANT_ROW).filter(
    (entry) => entry.category !== "item",
  );
  const skills = pool.filter((entry) => entry.category === "skill");
  const equipment = pool.filter((entry) => entry.category !== "skill");
  const total = MERCHANT_SKILLS + MERCHANT_EQUIPMENT;
  let skillCount = Math.min(MERCHANT_SKILLS, skills.length);
  let equipmentCount = Math.min(MERCHANT_EQUIPMENT, equipment.length);
  skillCount += Math.min(
    skills.length - skillCount,
    total - skillCount - equipmentCount,
  );
  equipmentCount += Math.min(
    equipment.length - equipmentCount,
    total - skillCount - equipmentCount,
  );

  const goods: MerchantGoods[] = [];
  const add = (entry: DropEntry) => {
    const maxPercent = maxPercentOf(entry.category, entry.id) as number;
    const multiplierPercent = rollMultiplierPercent(
      pickQuality(QUALITY_WEIGHTS[MERCHANT_ROW], random),
      random,
      maxPercent,
    );
    goods.push({
      category: entry.category as MerchantCategory,
      id: entry.id,
      multiplierPercent,
      price: merchantPrice(
        entry.stars,
        qualityOf(multiplierPercent, maxPercent),
      ),
    });
  };
  const take = <T extends DropEntry>(from: T[]) => {
    const entry = pickWeighted(
      from,
      (choice) => starWeight(MERCHANT_ROW, choice),
      random,
    );
    from.splice(from.indexOf(entry), 1);
    return entry;
  };
  for (let index = 0; index < skillCount; index++) add(take(skills));
  for (let index = 0; index < equipmentCount; index++) {
    const categories = EQUIPMENT_CATEGORIES.filter((category) =>
      equipment.some((entry) => entry.category === category),
    );
    const category = pickWeighted(
      categories,
      (choice) => CATEGORY_WEIGHTS[choice],
      random,
    );
    const entry = take(
      equipment.filter((choice) => choice.category === category),
    );
    equipment.splice(equipment.indexOf(entry), 1);
    add(entry);
  }
  return goods;
}
