import { describe, expect, it } from "vitest";
import { DESTINATIONS } from "./catalog.js";

describe("冒険先", () => {
  it("奥の行き先ほど、部屋の数が増える", () => {
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
