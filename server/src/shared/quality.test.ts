import { describe, expect, it } from "vitest";
import {
  percentRange,
  pickQuality,
  QUALITIES,
  qualityOf,
  rollMultiplierPercent,
} from "./quality.js";

describe("品質", () => {
  it("上限×1.5の幅の中の位置で、品質を分ける", () => {
    expect(qualityOf(100, 150)).toBe("C");
    expect(qualityOf(119, 150)).toBe("C");
    expect(qualityOf(120, 150)).toBe("B");
    expect(qualityOf(134, 150)).toBe("B");
    expect(qualityOf(135, 150)).toBe("A");
    expect(qualityOf(144, 150)).toBe("A");
    expect(qualityOf(145, 150)).toBe("S");
    expect(qualityOf(150, 150)).toBe("S");
  });

  it("境目ちょうどの倍率は、上の品質に入れる", () => {
    // ×1.3 の上限では、A の下端は 100 + 30 × 0.7 = 121。
    expect(qualityOf(121, 130)).toBe("A");
    expect(qualityOf(120, 130)).toBe("B");
  });

  it("品質ごとの倍率の範囲は、すき間なく下限から上限までを埋める", () => {
    for (const max of [110, 130, 150, 200]) {
      let next = 100;
      for (const quality of QUALITIES) {
        const range = percentRange(quality, max);
        expect(range.min).toBe(next);
        for (let percent = range.min; percent <= range.max; percent++) {
          expect(qualityOf(percent, max)).toBe(quality);
        }
        next = range.max + 1;
      }
      expect(next - 1).toBe(max);
    }
  });

  it("選んだ倍率は、その品質の範囲に入る", () => {
    for (const quality of QUALITIES) {
      for (const roll of [0, 0.5, 0.999999]) {
        const percent = rollMultiplierPercent(quality, () => roll, 260);
        expect(qualityOf(percent, 260)).toBe(quality);
      }
    }
  });

  it("重みに従って品質を選ぶ", () => {
    const weights = { C: 75, B: 20, A: 5 };
    expect(pickQuality(weights, () => 0)).toBe("C");
    expect(pickQuality(weights, () => 0.8)).toBe("B");
    expect(pickQuality(weights, () => 0.99)).toBe("A");
  });
});
