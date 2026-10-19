import {
  DEFAULT_MAX_MULTIPLIER_PERCENT,
  MIN_MULTIPLIER_PERCENT,
} from "../../shared/quality.js";

// ACTボーナスの種類・倍率の上限・枠の数。表示用の名前や効果量はクライアントの定義が持ち、
// サーバーは持ち物と枠の整合と、手に入れたボーナスの倍率を判定する（doc/features/step-bonus.md）。

export const STEP_BONUS_IDS = [
  "guard",
  "luck",
  "treasure-sight",
  "foresight",
  "omen",
  "appraisal",
  "fighting",
] as const;

export type StepBonusId = (typeof STEP_BONUS_IDS)[number];

// ボーナスごとの倍率の上限（百分率）。種類ごとの値が決まるまで、どれも仮の×1.5にする。
// クライアントの定義（StepBonusDefinition の MaxMultiplier）と同じ値にする。
export function maxMultiplierPercent(_bonusId: StepBonusId): number {
  return DEFAULT_MAX_MULTIPLIER_PERCENT;
}

// 段階ごとの枠（1,000／2,000／3,000／5,000／8,000ACT）の数。
export const STEP_BONUS_SLOT_COUNT = 5;

// 最初から持っているボーナスと、その倍率（百分率）と枠の設定。冒険での入手ができるまでの仮設定。
// 品質を見比べられるよう、S・A・B・C がそろう倍率にしている。
export const STARTER_HOLDINGS: readonly {
  bonusId: StepBonusId;
  multiplierPercent: number;
}[] = [
  { bonusId: "guard", multiplierPercent: 148 },
  { bonusId: "luck", multiplierPercent: 140 },
  { bonusId: "treasure-sight", multiplierPercent: 128 },
  { bonusId: "appraisal", multiplierPercent: 124 },
  { bonusId: "foresight", multiplierPercent: 115 },
  { bonusId: "fighting", multiplierPercent: 108 },
  { bonusId: "omen", multiplierPercent: MIN_MULTIPLIER_PERCENT },
];

export const STARTER_SLOTS: readonly StepBonusId[] = [
  "foresight",
  "luck",
  "treasure-sight",
  "fighting",
  "appraisal",
];
