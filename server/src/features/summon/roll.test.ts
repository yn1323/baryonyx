import { describe, expect, it } from "vitest";
import {
  SUMMON_CATEGORY_PERMILLE,
  SUMMON_POOL,
  SUMMON_QUALITY_PERMILLE,
  SUMMON_STAR_PERMILLE,
  type SummonEntry,
} from "./catalog.js";
import { rollSummon } from "./roll.js";

// 決まった種で、同じ並びの乱数を返す（線形合同法）。
function seeded(seed: number) {
  let state = seed >>> 0;
  return () => {
    state = (Math.imul(state, 1664525) + 1013904223) >>> 0;
    return state / 2 ** 32;
  };
}

describe("召喚の抽選", () => {
  it("分類・☆・品質の割合は、それぞれ合わせて100%になる", () => {
    const sum = (values: number[]) => values.reduce((a, b) => a + b, 0);
    expect(sum(Object.values(SUMMON_CATEGORY_PERMILLE))).toBe(1000);
    expect(sum([...SUMMON_STAR_PERMILLE])).toBe(1000);
    expect(sum(Object.values(SUMMON_QUALITY_PERMILLE))).toBe(1000);
  });

  it("引ける物はキャラ・装備・アイテムで、スキルを含まない", () => {
    expect(new Set(SUMMON_POOL.map((entry) => entry.category))).toEqual(
      new Set(["character", "weapon", "armor", "accessory", "item"]),
    );
    const keys = SUMMON_POOL.map((entry) => `${entry.category}:${entry.id}`);
    expect(new Set(keys).size).toBe(keys.length);
  });

  it("10回で☆3以上もキャラも出なければ、最後の1つを☆3以上から引き直す。1回では引き直さない", () => {
    const pool: SummonEntry[] = [
      { category: "item", id: "low", stars: 1 },
      { category: "item", id: "high", stars: 3 },
    ];
    const ten = rollSummon(pool, 10, () => 0);
    expect(ten.slice(0, 9).map((draw) => draw.id)).toEqual(
      Array(9).fill("low"),
    );
    expect(ten[9].id).toBe("high");
    expect(rollSummon(pool, 1, () => 0).map((draw) => draw.id)).toEqual([
      "low",
    ]);
  });

  it("倍率は装備だけが持つ", () => {
    for (const draw of rollSummon(SUMMON_POOL, 200, seeded(3))) {
      const equipment =
        draw.category === "weapon" ||
        draw.category === "armor" ||
        draw.category === "accessory";
      expect(draw.multiplierPercent !== null).toBe(equipment);
      if (equipment) expect(draw.multiplierPercent).toBeGreaterThanOrEqual(100);
    }
  });

  it("多く引くと、分類はおおよそ割合どおりに出る", () => {
    const draws = rollSummon(SUMMON_POOL, 20000, seeded(7));
    for (const [category, permille] of Object.entries(
      SUMMON_CATEGORY_PERMILLE,
    )) {
      const share =
        draws.filter((draw) => draw.category === category).length /
        draws.length;
      expect(Math.abs(share - permille / 1000)).toBeLessThan(0.015);
    }
  });
});
