import { MIN_MULTIPLIER_PERCENT } from "../../shared/quality.js";
import { DROP_POOLS, type Stars } from "../adventure/drops.js";

// 商会の「買う」（doc/features/screens.md の商会）。これまでに手に入れた（ライブラリに見つけた記録がある）
// スキル・武器・防具・アクセサリーを、品質の最低（倍率×1.00）で、ルーンと引き換えに何度でも売る。
// 名前・説明はクライアントの定義が持ち、サーバーは売れる物かと値段だけを判定する。

export const WORKSHOP_CATEGORIES = [
  "skill",
  "weapon",
  "armor",
  "accessory",
] as const;

export type WorkshopCategory = (typeof WORKSHOP_CATEGORIES)[number];

// 売る物の倍率（百分率）。品質の幅の下限で、どの種類も品質Cになる。
export const WORKSHOP_MULTIPLIER_PERCENT = MIN_MULTIPLIER_PERCENT;

// 値段（ルーン）。☆1の200から、☆が1つ増えるごとに倍にする。品質を選べない代わりに、行商人の品質Cの値段
// （☆1で300）より安くした。仮の値（doc/features/economy.md のルーンの用途）。
// クライアントの WorkshopGoods が同じ値を持つ。
export const WORKSHOP_STAR_PRICES: Record<Stars, number> = {
  1: 200,
  2: 400,
  3: 800,
  4: 1600,
  5: 3200,
};

// 冒険のドロップの一覧（行き先ごと）での、種類の載り方。載っていない物は undefined。
function dropEntryOf(category: CraftCategory, id: string) {
  for (const pool of Object.values(DROP_POOLS)) {
    const entry = pool.find(
      (choice) => choice.category === category && choice.id === id,
    );
    if (entry) return entry;
  }
  return undefined;
}

// 種類の☆。冒険のドロップの一覧に載っている物だけを扱い、載っていない物は undefined。
// 合成の材料の☆を確かめるのにも使うため、アイテムも受け取る。
export function workshopStarsOf(
  category: CraftCategory,
  id: string,
): Stars | undefined {
  return dropEntryOf(category, id)?.stars;
}

// 値段。売らない物は undefined。
export function workshopPrice(
  category: WorkshopCategory,
  id: string,
): number | undefined {
  const stars = workshopStarsOf(category, id);
  return stars === undefined ? undefined : WORKSHOP_STAR_PRICES[stars];
}

// 商会の「合成」（doc/features/screens.md の商会）。同じ分類で☆がちょうど1つ低い物を合計3つ使い、
// これまでに手に入れた（ライブラリに見つけた記録がある）☆2以上の物を1つ作る。アイテムも作れる。
export const CRAFT_CATEGORIES = [...WORKSHOP_CATEGORIES, "item"] as const;

export type CraftCategory = (typeof CRAFT_CATEGORIES)[number];

// 1回の合成で使う材料の数。
export const CRAFT_MATERIAL_COUNT = 3;

// 合成の費用（ルーン）。出来上がる物の☆で決め、買う値段の半分にする。☆1は作れない。仮の値。
export const CRAFT_STAR_RUNES: Record<Exclude<Stars, 1>, number> = {
  2: 200,
  3: 400,
  4: 800,
  5: 1600,
};

// 作れる物の☆。冒険のドロップの一覧に載っていて、☆2以上で、主の物（bossOnly）ではない物だけを作る。
// 作れない物は undefined。
export function craftStarsOf(
  category: CraftCategory,
  id: string,
): Exclude<Stars, 1> | undefined {
  const entry = dropEntryOf(category, id);
  if (!entry || entry.bossOnly || entry.stars === 1) return undefined;
  return entry.stars;
}

// 四捨五入の境目ちょうどが、小数の誤差で下へ落ちないよう、わずかに足して丸める。
const EPSILON = 1e-9;

// 出来上がる物の倍率（百分率）。材料それぞれの、倍率の幅（×1.00〜上限）の中の位置を平均し、
// 出来上がる物の幅の同じ位置にする。上限が×1.00の種類の位置は0とする。
export function craftedPercent(
  materials: readonly { percent: number; maxPercent: number }[],
  maxPercent: number,
): number {
  const positionOf = (percent: number, max: number) =>
    max <= MIN_MULTIPLIER_PERCENT
      ? 0
      : (percent - MIN_MULTIPLIER_PERCENT) / (max - MIN_MULTIPLIER_PERCENT);
  const average =
    materials.reduce(
      (sum, material) =>
        sum + positionOf(material.percent, material.maxPercent),
      0,
    ) / materials.length;
  const percent =
    MIN_MULTIPLIER_PERCENT +
    Math.round(average * (maxPercent - MIN_MULTIPLIER_PERCENT) + EPSILON);
  return Math.min(maxPercent, Math.max(MIN_MULTIPLIER_PERCENT, percent));
}
