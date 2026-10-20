import { describe, expect, it } from "vitest";
import {
  DESTINATIONS,
  REWARD_QUALITY_WEIGHTS,
  rewardQualityWeights,
} from "./catalog.js";

describe("冒険先", () => {
  it("奥の行き先ほど、部屋の数と報酬の上がり方が増える", () => {
    expect(DESTINATIONS.length).toBeGreaterThanOrEqual(8);
    expect(new Set(DESTINATIONS.map((entry) => entry.id)).size).toBe(
      DESTINATIONS.length,
    );
    for (let i = 1; i < DESTINATIONS.length; i++) {
      expect(DESTINATIONS[i].roomCount).toBeGreaterThanOrEqual(
        DESTINATIONS[i - 1].roomCount,
      );
      expect(DESTINATIONS[i].rewardBoost).toBeGreaterThan(
        DESTINATIONS[i - 1].rewardBoost,
      );
    }
  });

  it("報酬の上がり方は、品質Sをその分、Aを半分だけ重くし、C・Bは変えない", () => {
    expect(rewardQualityWeights("elite", 0)).toEqual(
      REWARD_QUALITY_WEIGHTS.elite,
    );
    expect(rewardQualityWeights("elite", 100)).toEqual({
      C: 30,
      B: 40,
      A: 38,
      S: 10,
    });
    // 品質Sの重みがない部屋には、Sを足さない。
    expect(rewardQualityWeights("battle", 160)).toEqual({ C: 75, B: 20, A: 9 });
  });
});
