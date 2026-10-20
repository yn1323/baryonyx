// 品質（doc/features/inventory.md の品質）。スキル・装備・ACTボーナスは1つずつ倍率を持ち、
// 種類ごとの幅（×1.00〜上限）の中の位置で、品質を S・A・B・C に分ける。上限は種類ごとの定義が持つ。
// 倍率は誤差なく比べられるよう、百分率の整数（×1.37 なら 137）で持つ。

export const QUALITIES = ["C", "B", "A", "S"] as const;

export type Quality = (typeof QUALITIES)[number];

// 倍率の下限。種類の基本の効果量をそのまま使う。
export const MIN_MULTIPLIER_PERCENT = 100;

// 品質ごとの、幅の中の位置（下限0〜上限1）の下端。S は上位10%、A は上位10〜30%、B は上位30〜60%。
const QUALITY_FLOORS: Record<Quality, number> = {
  C: 0,
  B: 0.4,
  A: 0.7,
  S: 0.9,
};

// 境目ちょうどの倍率は上の品質に入れる。小数の誤差で境目の下へ落ちないよう、わずかに足して比べる。
const EPSILON = 1e-9;

export function qualityOf(percent: number, maxPercent: number): Quality {
  if (maxPercent <= MIN_MULTIPLIER_PERCENT) return "C";
  const position =
    (percent - MIN_MULTIPLIER_PERCENT) / (maxPercent - MIN_MULTIPLIER_PERCENT);
  for (const quality of ["S", "A", "B"] as const) {
    if (position + EPSILON >= QUALITY_FLOORS[quality]) return quality;
  }
  return "C";
}

// 品質の幅に入る倍率の、百分率の整数での最小と最大。
export function percentRange(
  quality: Quality,
  maxPercent: number,
): { min: number; max: number } {
  const width = maxPercent - MIN_MULTIPLIER_PERCENT;
  const at = (position: number) =>
    Math.ceil(MIN_MULTIPLIER_PERCENT + width * position - EPSILON);
  const index = QUALITIES.indexOf(quality);
  const min = at(QUALITY_FLOORS[quality]);
  const max =
    quality === "S" ? maxPercent : at(QUALITY_FLOORS[QUALITIES[index + 1]]) - 1;
  return { min, max: Math.max(min, max) };
}

// 品質の幅の中から、倍率を均等に1つ選ぶ。
export function rollMultiplierPercent(
  quality: Quality,
  random: () => number,
  maxPercent: number,
): number {
  const { min, max } = percentRange(quality, maxPercent);
  return min + Math.min(max - min, Math.floor(random() * (max - min + 1)));
}

// 応答に出す倍率（×1.37 なら 1.37）。
export function multiplierOf(percent: number): number {
  return percent / 100;
}

// 重みに従って品質を1つ選ぶ。
export function pickQuality(
  weights: Partial<Record<Quality, number>>,
  random: () => number,
): Quality {
  const entries = Object.entries(weights) as [Quality, number][];
  const total = entries.reduce((sum, [, weight]) => sum + weight, 0);
  let roll = random() * total;
  for (const [quality, weight] of entries) {
    if (roll < weight) return quality;
    roll -= weight;
  }
  return entries[entries.length - 1][0];
}
