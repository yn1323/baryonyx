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
  REWARD_ROWS,
  rewardOf,
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
      for (const kind of REWARD_ROWS) {
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
  it("マスの種類ごとの数だけ落とし、アイテムだけが倍率を持たない", () => {
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
    // 休憩と行商人は落とさない。怪しい宝箱（本物）は宝箱より1つ多い。
    expect([...counts.rest]).toEqual([0]);
    expect([...counts.merchant]).toEqual([0]);
    expect([...counts["shady-chest"]].sort()).toEqual([2, 3, 4]);
    expect([...counts.trial].sort()).toEqual([1, 2]);
    expect([...counts["step-door"]].sort()).toEqual([1, 2]);
    expect([...counts.altar].sort()).toEqual([1, 2, 3]);
    // 祭壇で固有スキルを進化させたら、何も落とさない。
    expect(
      rollDrops("forest-ruins", "altar", random, { evolved: true }),
    ).toEqual([]);
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

describe("新しいマスの報酬の行", () => {
  it("怪しい宝箱・試練・歩数の扉は、マスで決まったことで行を選ぶ", () => {
    expect(rewardOf("shady-chest")).toEqual({ row: "treasure", extra: 1 });
    expect(rewardOf("shady-chest", { mimic: true })).toEqual({
      row: "boss",
      extra: 0,
    });
    expect(rewardOf("trial")).toEqual({ row: "battle", extra: 0 });
    expect(rewardOf("trial", { trialMet: true })).toEqual({
      row: "boss",
      extra: 0,
    });
    expect(
      ([0, 1, 2, 3] as const).map((stepDoorTier) =>
        rewardOf("step-door", { stepDoorTier }),
      ),
    ).toEqual([
      { row: "battle", extra: 0 },
      { row: "elite", extra: 0 },
      { row: "elite", extra: 1 },
      { row: "boss", extra: 0 },
    ]);
  });

  it("最奥の間の行を借りるマスは、☆3〜5を2〜4つ落とし、主の物を落とさない", () => {
    const random = seeded(5);
    const bossOnly = new Set<string>(
      allEntries()
        .filter((entry) => entry.bossOnly)
        .map((entry) => entry.id),
    );
    const stars = new Map<string, number>(
      allEntries().map((entry) => [entry.id, entry.stars]),
    );
    const outcomes = [
      ["shady-chest", { mimic: true }],
      ["trial", { trialMet: true }],
      ["step-door", { stepDoorTier: 3 }],
    ] as const;
    for (const destination of ["forest-ruins", "port-portoria"]) {
      for (const [kind, outcome] of outcomes) {
        const counts = new Set<number>();
        for (let i = 0; i < 500; i++) {
          const drops = rollDrops(destination, kind, random, outcome);
          counts.add(drops.length);
          for (const drop of drops) {
            expect(bossOnly.has(drop.id)).toBe(false);
            expect(stars.get(drop.id)).toBeGreaterThanOrEqual(3);
          }
        }
        expect([...counts].sort()).toEqual([2, 3, 4]);
      }
    }
    // 最奥の間そのものは、主の物も落とす。
    expect(
      eligibleEntries("forest-ruins", "boss", false).map((entry) => entry.id),
    ).not.toContain("guardian-circlet");
  });

  it("歩数の扉は、段が上がるほど落ちる数が増える", () => {
    const random = seeded(9);
    const countsOf = (stepDoorTier: 0 | 1 | 2) => {
      const counts = new Set<number>();
      for (let i = 0; i < 300; i++) {
        counts.add(
          rollDrops("forest-ruins", "step-door", random, { stepDoorTier })
            .length,
        );
      }
      return [...counts].sort();
    };
    expect(countsOf(0)).toEqual([1, 2]);
    expect(countsOf(1)).toEqual([1, 2, 3]);
    expect(countsOf(2)).toEqual([2, 3, 4]);
  });
});
