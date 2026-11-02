import {
  MIN_MULTIPLIER_PERCENT,
  type Quality,
  qualityOf,
} from "../../shared/quality.js";
import type { RewardRow } from "../adventure/drops.js";
import type { Stars } from "../inventory/catalog.js";
import { uniqueParent } from "../party/catalog.js";

// 商会の強化（doc/features/enhancement.md）。強化素材は、品質を上げる磨き石と、固有スキルを覚醒させる
// 覚醒の欠片に分かれる。名前・絵はクライアントの定義が持ち、サーバーは種類ごとの数と、上げ幅・費用・落ち方を決める。
// 数値はどれも仮の値である。

// 磨き石は1種類だけにした（2026-11-02、ユーザーの指示）。IDはクライアントの絵と合わせるため "polish-white" のまま。
export const POLISH_ID = "polish-white";

export const AWAKENING_SHARD_ID = "awakening-shard";

export const MATERIAL_IDS = [POLISH_ID, AWAKENING_SHARD_ID] as const;

export type MaterialId = (typeof MATERIAL_IDS)[number];

export const POLISH_STARS: Stars = 1;

// 品質強化の1段階で上がる倍率の、倍率の幅（×1.00〜種類の上限）に対する百分率と、石1個あたりのルーン。
export const POLISH_STEP_RATE_PERCENT = 5;
export const POLISH_RUNES_PER_STONE = 100;

// 1段階に使う石の数。その段階を始める前の倍率の品質で決まり、品質が高いほど多く使う。
export const POLISH_STONES_PER_STEP: Record<Quality, number> = {
  C: 1,
  B: 2,
  A: 3,
  S: 6,
};

export const AWAKENING_SHARD_STARS: Stars = 4;

export function materialStars(id: MaterialId): Stars {
  return id === AWAKENING_SHARD_ID ? AWAKENING_SHARD_STARS : POLISH_STARS;
}

// 1段階で上がる倍率（百分率の整数）。倍率の幅に上げ幅を掛けて四捨五入する。
export function polishStep(maxPercent: number): number {
  return Math.round(
    ((maxPercent - MIN_MULTIPLIER_PERCENT) * POLISH_STEP_RATE_PERCENT) / 100,
  );
}

// steps 段階の品質強化。段階ごとに、始める前の倍率の品質で石の数を決めて足し、最後の段階は種類の上限で止める。
// after は強化したあとの倍率、stones は使う石の合計。lastGain は最後の段階で上がった倍率で、0なら段階が多すぎる。
export function polishPlan(
  percent: number,
  steps: number,
  maxPercent: number,
): { after: number; stones: number; lastGain: number } {
  const step = polishStep(maxPercent);
  let after = percent;
  let stones = 0;
  let lastGain = 0;
  for (let index = 0; index < steps; index++) {
    const next = Math.min(maxPercent, after + step);
    stones += POLISH_STONES_PER_STEP[qualityOf(after, maxPercent)];
    lastGain = next - after;
    after = next;
  }
  return { after, stones, lastGain };
}

// 覚醒で1段進めるのに払う欠片の数とルーン。1段目へ進めるときと2段目へ進めるときで、ルーンだけが違う。
export const AWAKENING_SHARDS_PER_STAGE = 1;
export const AWAKENING_RUNES = { first: 1000, second: 2000 } as const;

export function awakeningRunes(stage: number): number {
  return uniqueParent(stage) === 0
    ? AWAKENING_RUNES.first
    : AWAKENING_RUNES.second;
}

// 持っているスキル1枚の覚醒に払うルーン。欠片の数とともに、固有スキルの1段目と同じ（doc/features/skill-awakening.md）。
export const SKILL_AWAKENING_RUNES = AWAKENING_RUNES.first;

// 出来事を終えたマスごとの、磨き石が落ちる確率（千分率）。ふつうのドロップとは別に1回だけ抽選する。
// 落ちる回数は、1周（魔物6・強い魔物と宝箱あわせて3・最奥1）で約1.8回（2026-10-31、ユーザーの指示）。
// 1回に落ちる個数は POLISH_DROP_COUNTS。
export const POLISH_DROP_CHANCE: Record<RewardRow, number> = {
  battle: 100,
  elite: 200,
  treasure: 200,
  boss: 600,
};

// 落ちる磨き石の数を分ける、行き先の深さ。行き先と深さの対応は adventure/drops.ts の polishDepthOf。
export const POLISH_DEPTHS = ["shallow", "middle", "deep"] as const;

export type PolishDepth = (typeof POLISH_DEPTHS)[number];

// 磨き石が落ちたときの個数。行き先の深さと、マスごと（2026-11-02、ユーザーの指示）。
// 1周の平均は、浅い3.6個・中ほど4.2個・奥6.0個になる。
export const POLISH_DROP_COUNTS: Record<
  PolishDepth,
  Record<RewardRow, number>
> = {
  shallow: { battle: 1, elite: 2, treasure: 2, boss: 3 },
  middle: { battle: 1, elite: 2, treasure: 2, boss: 4 },
  deep: { battle: 2, elite: 3, treasure: 3, boss: 5 },
};

// 覚醒の欠片が落ちる確率（千分率）。磨き石とは別に抽選し、魔物のマスでは落ちない。
export const SHARD_DROP_CHANCE: Record<RewardRow, number> = {
  battle: 0,
  elite: 30,
  treasure: 30,
  boss: 150,
};
