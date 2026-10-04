// ACTボーナスの種類・ランク・枠の数。表示用の名前や効果量はクライアントの定義が持ち、
// サーバーは持ち物と枠の整合だけを判定する（doc/features/step-bonus.md）。

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

// 低い順。同じボーナスをもう一度手に入れたときは、高いほうだけを残す。
export const STEP_BONUS_RANKS = ["E", "D", "C", "B", "A", "S"] as const;

export type StepBonusRank = (typeof STEP_BONUS_RANKS)[number];

// 段階ごとの枠（1,000／2,000／3,000／5,000／8,000ACT）の数。
export const STEP_BONUS_SLOT_COUNT = 5;

export function rankOrder(rank: StepBonusRank): number {
  return STEP_BONUS_RANKS.indexOf(rank);
}

// 最初から持っているボーナスと枠の設定。冒険での入手ができるまでの仮設定。
export const STARTER_HOLDINGS: readonly {
  bonusId: StepBonusId;
  rank: StepBonusRank;
}[] = [
  { bonusId: "guard", rank: "S" },
  { bonusId: "luck", rank: "A" },
  { bonusId: "treasure-sight", rank: "B" },
  { bonusId: "appraisal", rank: "B" },
  { bonusId: "foresight", rank: "C" },
  { bonusId: "fighting", rank: "D" },
  { bonusId: "omen", rank: "E" },
];

export const STARTER_SLOTS: readonly StepBonusId[] = [
  "foresight",
  "luck",
  "treasure-sight",
  "fighting",
  "appraisal",
];
