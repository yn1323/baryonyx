import { MIN_MULTIPLIER_PERCENT } from "../../shared/quality.js";
import { DROP_POOLS } from "../adventure/drops.js";
import type { Stars } from "../inventory/catalog.js";

// 商会の「買う」（doc/features/screens/shop.md の商会）。これまでに手に入れた（ライブラリに見つけた記録がある）
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

// 値段（ルーン）。☆1の1,000から、☆が1つ増えるごとに倍にする。品質を選べない代わりに、行商人の品質Cの値段
// （☆1で1,500）より安くした。仮の値（doc/features/economy.md のルーンの用途）。
// 2026-10-26に、1週間の歩数（約4万ルーン）に比べて軽すぎたため、☆1の200から5倍にした。
// クライアントの WorkshopGoods が同じ値を持つ。
export const WORKSHOP_STAR_PRICES: Record<Stars, number> = {
  1: 1000,
  2: 2000,
  3: 4000,
  4: 8000,
  5: 16000,
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

// 商会の「合成」（doc/features/screens/shop.md の商会）。同じ分類で☆がちょうど1つ低い物を合計3つ使い、
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

// 商会の「売る」（doc/features/screens/shop.md の商会）。持ち物のうち、キャラに付けていない（デッキに入れていない）
// スキル・装備を1つずつ、アイテムを種類ごとに持っている数のまま、ルーンと引き換えに手放す。仮の値。
// クライアントの WorkshopSellView.PriceOf が同じ値を持つ。

// 1回で売れる物の数（スキル・装備の行と、アイテムの種類の合計）。D1の1文に入れられる値の数に収める。
export const SELL_LIMIT = 50;

// アイテムの☆1の売値（1つ分）。☆が1つ増えるごとに倍にする。
export const SELL_ITEM_STAR1_RUNES = 10;

// スキル・装備の売値。買う値段の20分の1に倍率を掛け、四捨五入する。冒険のドロップの一覧にない物は☆1とする。
// 2026-10-26に買う値段を5倍にしたとき、売値は変えないよう4分の1から20分の1にした。
export function sellPrice(
  category: WorkshopCategory,
  id: string,
  percent: number,
): number {
  const base = WORKSHOP_STAR_PRICES[workshopStarsOf(category, id) ?? 1] / 20;
  return Math.round((base * percent) / 100);
}

// アイテムの売値。1つ分の値段に数を掛ける。冒険のドロップの一覧にない物は☆1とする。
export function sellItemPrice(id: string, count: number): number {
  const stars = workshopStarsOf("item", id) ?? 1;
  return SELL_ITEM_STAR1_RUNES * 2 ** (stars - 1) * count;
}
