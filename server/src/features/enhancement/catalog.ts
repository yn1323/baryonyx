import { MIN_MULTIPLIER_PERCENT } from "../../shared/quality.js";
import type { RewardRow, Stars } from "../adventure/drops.js";
import { uniqueParent } from "../party/catalog.js";

// 商会の強化（doc/features/enhancement.md）。強化素材は、品質を上げる磨き石と、固有スキルを覚醒させる
// 覚醒の欠片に分かれる。名前・絵はクライアントの定義が持ち、サーバーは種類ごとの数と、上げ幅・費用・落ち方を決める。
// 数値はどれも仮の値である。

export const POLISH_IDS = [
  "polish-white",
  "polish-blue",
  "polish-purple",
  "polish-gold",
] as const;

export type PolishId = (typeof POLISH_IDS)[number];

export const AWAKENING_SHARD_ID = "awakening-shard";

export const MATERIAL_IDS = [...POLISH_IDS, AWAKENING_SHARD_ID] as const;

export type MaterialId = (typeof MATERIAL_IDS)[number];

// 磨き石の色ごとの☆・上げ幅（倍率の幅に対する百分率）・1つあたりのルーン。
export const POLISHES: Record<
  PolishId,
  { stars: Stars; ratePercent: number; runes: number }
> = {
  "polish-white": { stars: 1, ratePercent: 5, runes: 100 },
  "polish-blue": { stars: 2, ratePercent: 10, runes: 200 },
  "polish-purple": { stars: 3, ratePercent: 25, runes: 500 },
  "polish-gold": { stars: 4, ratePercent: 50, runes: 1000 },
};

export const AWAKENING_SHARD_STARS: Stars = 4;

export function materialStars(id: MaterialId): Stars {
  return id === AWAKENING_SHARD_ID ? AWAKENING_SHARD_STARS : POLISHES[id].stars;
}

// 磨き石1つで上がる倍率（百分率の整数）。上限までの幅に上げ幅を掛けて四捨五入する。
export function polishStep(id: PolishId, maxPercent: number): number {
  return Math.round(
    ((maxPercent - MIN_MULTIPLIER_PERCENT) * POLISHES[id].ratePercent) / 100,
  );
}

// 磨き石を count 個使ったあとの倍率。上限を超える分は上限で止める。
export function polishedPercent(
  percent: number,
  id: PolishId,
  count: number,
  maxPercent: number,
): number {
  return Math.min(maxPercent, percent + polishStep(id, maxPercent) * count);
}

// 覚醒で1段進めるのに払う欠片の数とルーン。1段目へ進めるときと2段目へ進めるときで、ルーンだけが違う。
export const AWAKENING_SHARDS_PER_STAGE = 1;
export const AWAKENING_RUNES = { first: 1000, second: 2000 } as const;

export function awakeningRunes(stage: number): number {
  return uniqueParent(stage) === 0
    ? AWAKENING_RUNES.first
    : AWAKENING_RUNES.second;
}

// 出来事を終えたマスごとの、磨き石が落ちる確率（千分率）と色の重み。ふつうのドロップとは別に1回だけ抽選する。
export const POLISH_DROP_CHANCE: Record<RewardRow, number> = {
  battle: 300,
  elite: 600,
  treasure: 600,
  boss: 1000,
};

export const POLISH_DROP_WEIGHTS: Record<
  RewardRow,
  Record<PolishId, number>
> = {
  battle: {
    "polish-white": 80,
    "polish-blue": 18,
    "polish-purple": 2,
    "polish-gold": 0,
  },
  elite: {
    "polish-white": 55,
    "polish-blue": 35,
    "polish-purple": 9,
    "polish-gold": 1,
  },
  treasure: {
    "polish-white": 55,
    "polish-blue": 35,
    "polish-purple": 9,
    "polish-gold": 1,
  },
  boss: {
    "polish-white": 20,
    "polish-blue": 45,
    "polish-purple": 28,
    "polish-gold": 7,
  },
};

// 覚醒の欠片が落ちる確率（千分率）。磨き石とは別に抽選し、魔物のマスでは落ちない。
export const SHARD_DROP_CHANCE: Record<RewardRow, number> = {
  battle: 0,
  elite: 30,
  treasure: 30,
  boss: 150,
};
