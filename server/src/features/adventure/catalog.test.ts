import { describe, expect, it } from "vitest";
import {
  canPlace,
  DESTINATIONS,
  EVENT_KINDS,
  findDestination,
  fixedKindOf,
  pickTrialCondition,
  stepDoorTier,
  TRIAL_CONDITIONS,
} from "./catalog.js";

describe("冒険先", () => {
  it("奥の行き先ほど、マスの数が増える", () => {
    expect(DESTINATIONS.length).toBeGreaterThanOrEqual(8);
    expect(new Set(DESTINATIONS.map((entry) => entry.id)).size).toBe(
      DESTINATIONS.length,
    );
    for (let i = 1; i < DESTINATIONS.length; i++) {
      expect(DESTINATIONS[i].roomCount).toBeGreaterThanOrEqual(
        DESTINATIONS[i - 1].roomCount,
      );
    }
  });
});

describe("マスを置ける階", () => {
  const forest = findDestination("forest-ruins");
  if (!forest) throw new Error("forest-ruins がない");

  it("決まった階には、決まったマスだけを置ける", () => {
    // 道中8マス：第2層は魔物、第6層は宝箱、第9層は休憩、第10層は最奥の間。
    expect(fixedKindOf(forest, 2)).toBe("battle");
    expect(fixedKindOf(forest, 6)).toBe("treasure");
    expect(fixedKindOf(forest, 9)).toBe("rest");
    expect(fixedKindOf(forest, 10)).toBe("boss");
    for (const kind of EVENT_KINDS) {
      expect(canPlace(forest, 2, kind)).toBe(kind === "battle");
      expect(canPlace(forest, 6, kind)).toBe(kind === "treasure");
      expect(canPlace(forest, 9, kind)).toBe(kind === "rest");
      expect(canPlace(forest, 10, kind)).toBe(kind === "boss");
    }
  });

  it("ほかの階には、置ける階から先にだけ置ける", () => {
    // 第3層は2つ目、第4層は3つ目、第5層は4つ目。
    expect(canPlace(forest, 3, "battle")).toBe(true);
    expect(canPlace(forest, 3, "treasure")).toBe(true);
    expect(canPlace(forest, 3, "altar")).toBe(true);
    expect(canPlace(forest, 3, "elite")).toBe(false);
    expect(canPlace(forest, 3, "merchant")).toBe(false);
    expect(canPlace(forest, 4, "elite")).toBe(true);
    expect(canPlace(forest, 4, "shady-chest")).toBe(true);
    expect(canPlace(forest, 4, "step-door")).toBe(true);
    expect(canPlace(forest, 4, "trial")).toBe(false);
    expect(canPlace(forest, 4, "rest")).toBe(false);
    expect(canPlace(forest, 5, "trial")).toBe(true);
    expect(canPlace(forest, 5, "rest")).toBe(true);
    expect(canPlace(forest, 5, "boss")).toBe(false);
    expect(canPlace(forest, 1, "battle")).toBe(false);
    expect(canPlace(forest, 11, "boss")).toBe(false);
  });
});

describe("マスの中身", () => {
  it("試練の条件は3つから乱数で1つ選ぶ", () => {
    expect(TRIAL_CONDITIONS).toEqual([
      "within-6-turns",
      "no-one-falls",
      "half-hp",
    ]);
    expect(pickTrialCondition(() => 0)).toBe("within-6-turns");
    expect(pickTrialCondition(() => 0.5)).toBe("no-one-falls");
    expect(pickTrialCondition(() => 0.999999)).toBe("half-hp");
  });

  it("歩数の扉の段は、3,000歩・5,000歩・8,000歩で上がる", () => {
    const tiers = [0, 2999, 3000, 4999, 5000, 7999, 8000, 30000].map(
      stepDoorTier,
    );
    expect(tiers).toEqual([0, 0, 1, 1, 2, 2, 3, 3]);
  });
});
