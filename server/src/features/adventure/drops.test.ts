import { describe, expect, it } from "vitest";
import { EQUIPMENT } from "../equipment/catalog.js";
import { ITEM_IDS } from "../items/catalog.js";
import { CARD_SKILL_IDS } from "../party/catalog.js";
import { STEP_BONUS_IDS } from "../step-bonus/catalog.js";
import { DESTINATION_IDS, EVENT_KINDS } from "./catalog.js";
import {
  CATEGORY_WEIGHTS,
  COMMON_POOL,
  DROP_POOLS,
  type DropEntry,
  eligibleEntries,
  rollDrops,
} from "./drops.js";

// 同じ種から同じ並びを返す乱数（線形合同法）。
function seeded(seed: number) {
  let state = seed;
  return () => {
    state = (state * 1103515245 + 12345) % 2147483648;
    return state / 2147483648;
  };
}

const allEntries = (): DropEntry[] => [
  ...Object.values(DROP_POOLS).flat(),
  ...COMMON_POOL,
];

describe("ドロップの一覧", () => {
  it("どの行き先にも一覧があり、物は1つの行き先にだけ出る", () => {
    expect(Object.keys(DROP_POOLS).sort()).toEqual([...DESTINATION_IDS].sort());
    const ids = Object.values(DROP_POOLS)
      .flat()
      .map((entry) => `${entry.category}:${entry.id}`);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it("スキル50枚とアイテムのすべてが、どこかの行き先に出る", () => {
    const of = (category: string) =>
      Object.values(DROP_POOLS)
        .flat()
        .filter((entry) => entry.category === category)
        .map((entry) => entry.id)
        .sort();
    expect(of("skill")).toEqual([...CARD_SKILL_IDS].sort());
    expect(of("item")).toEqual([...ITEM_IDS].sort());
    expect(of("accessory")).toEqual(
      EQUIPMENT.filter((entry) => entry.slot === "accessory")
        .map((entry) => entry.id)
        .sort(),
    );
  });

  it("装備の分類は装備の定義と一致し、ACTボーナスはすべての種類がどこでも出る", () => {
    for (const entry of allEntries()) {
      if (
        entry.category === "weapon" ||
        entry.category === "armor" ||
        entry.category === "accessory"
      ) {
        expect(EQUIPMENT.find((item) => item.id === entry.id)?.slot).toBe(
          entry.category,
        );
      }
    }
    expect(COMMON_POOL.map((entry) => entry.id).sort()).toEqual(
      [...STEP_BONUS_IDS].sort(),
    );
  });

  it("最奥の間だけの物は道中で落ちず、最奥の間は☆3〜5だけが落ちる", () => {
    for (const destination of DESTINATION_IDS) {
      for (const kind of EVENT_KINDS) {
        const entries = eligibleEntries(destination, kind);
        expect(entries.length).toBeGreaterThan(0);
        if (kind === "boss") {
          expect(entries.every((entry) => entry.stars >= 3)).toBe(true);
        } else {
          expect(entries.some((entry) => entry.bossOnly)).toBe(false);
        }
      }
    }
    expect(
      eligibleEntries("forest-ruins", "boss").map((entry) => entry.id),
    ).toContain("guardian-circlet");
  });
});

describe("ドロップの抽選", () => {
  it("部屋の種類ごとの数だけ落とし、アイテムだけが倍率を持たない", () => {
    const random = seeded(7);
    const counts: Record<string, Set<number>> = {};
    for (const kind of EVENT_KINDS) {
      counts[kind] = new Set();
      for (let i = 0; i < 300; i++) {
        const drops = rollDrops("forest-ruins", kind, random);
        counts[kind].add(drops.length);
        for (const drop of drops) {
          expect(drop.multiplierPercent === null).toBe(
            drop.category === "item",
          );
        }
      }
    }
    expect([...counts.battle].sort()).toEqual([1, 2]);
    expect([...counts.elite].sort()).toEqual([1, 2, 3]);
    expect([...counts.treasure].sort()).toEqual([1, 2, 3]);
    expect([...counts.boss].sort()).toEqual([2, 3, 4]);
  });

  it("分類はアイテム＞アクセサリー＞武器・防具＞スキル＞ACTボーナスの順に出やすい", () => {
    const random = seeded(11);
    const tally: Record<string, number> = {};
    for (let i = 0; i < 20000; i++) {
      for (const drop of rollDrops("forest-ruins", "battle", random)) {
        tally[drop.category] = (tally[drop.category] ?? 0) + 1;
      }
    }
    const total = Object.values(tally).reduce((sum, count) => sum + count, 0);
    for (const [category, weight] of Object.entries(CATEGORY_WEIGHTS)) {
      expect(tally[category] / total).toBeCloseTo(weight / 100, 1);
    }
  });

  it("道中でも、まれに☆4・5と品質Sが落ちる", () => {
    const random = seeded(3);
    const stars = new Map<string, number>(
      allEntries().map((entry) => [entry.id, entry.stars]),
    );
    let rare = 0;
    let best = 0;
    for (let i = 0; i < 20000; i++) {
      for (const drop of rollDrops("forest-ruins", "battle", random)) {
        if ((stars.get(drop.id) ?? 0) >= 4) rare++;
        best = Math.max(best, drop.multiplierPercent ?? 0);
      }
    }
    expect(rare).toBeGreaterThan(0);
    // 森の武器の上限は×1.8〜×2.0で、品質S（上位10%）なら×1.72以上になる。
    expect(best).toBeGreaterThanOrEqual(172);
  });
});
