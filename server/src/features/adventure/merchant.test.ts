import { describe, expect, it } from "vitest";
import { qualityOf } from "../../shared/quality.js";
import { DESTINATION_IDS } from "./catalog.js";
import { eligibleEntries, maxPercentOf } from "./drops.js";
import { merchantPrice, rollMerchantGoods } from "./merchant.js";

// 同じ種から同じ並びを返す乱数（線形合同法）。
function seeded(seed: number) {
  let state = seed;
  return () => {
    state = (state * 1103515245 + 12345) % 2147483648;
    return state / 2147483648;
  };
}

describe("行商人の値段", () => {
  it("☆ごとの額に品質の倍率を掛け、10の位で丸める", () => {
    expect(merchantPrice(1, "C")).toBe(300);
    // 300 × 1.25 = 375 は380にする。
    expect(merchantPrice(1, "B")).toBe(380);
    expect(merchantPrice(2, "A")).toBe(900);
    expect(merchantPrice(3, "C")).toBe(1200);
    expect(merchantPrice(4, "B")).toBe(3000);
    expect(merchantPrice(5, "S")).toBe(9600);
  });
});

describe("行商人の品ぞろえ", () => {
  it("スキル3つと装備2つを、行き先の一覧から重ねずに並べ、主の物・アイテム・ACTボーナスは並べない", () => {
    const random = seeded(13);
    for (let i = 0; i < 200; i++) {
      const goods = rollMerchantGoods("forest-ruins", random);
      expect(goods.map((entry) => entry.category === "skill")).toEqual([
        true,
        true,
        true,
        false,
        false,
      ]);
      expect(new Set(goods.map((entry) => entry.id)).size).toBe(5);
      const pool = eligibleEntries("forest-ruins", "elite");
      for (const entry of goods) {
        const source = pool.find(
          (choice) =>
            choice.category === entry.category && choice.id === entry.id,
        );
        expect(source).toBeDefined();
        expect(source?.bossOnly).toBeUndefined();
        const maxPercent = maxPercentOf(entry.category, entry.id) as number;
        expect(entry.multiplierPercent).toBeGreaterThanOrEqual(100);
        expect(entry.multiplierPercent).toBeLessThanOrEqual(maxPercent);
        expect(entry.price).toBe(
          merchantPrice(
            source?.stars ?? 1,
            qualityOf(entry.multiplierPercent, maxPercent),
          ),
        );
      }
    }
  });

  it("分類に物が足りなければ、ほかの分類で5つまで埋める", () => {
    const random = seeded(17);
    const categoriesOf = (destination: string) =>
      rollMerchantGoods(destination, random).map((entry) => entry.category);
    // グラシエラ氷窟の装備は武器2つだけ、しじまの沼より奥は装備がない。
    expect(categoriesOf("glaciera-cave")).toEqual([
      "skill",
      "skill",
      "skill",
      "weapon",
      "weapon",
    ]);
    expect(categoriesOf("hush-mire")).toEqual(Array(5).fill("skill"));
    for (const destination of DESTINATION_IDS) {
      expect(rollMerchantGoods(destination, random)).toHaveLength(5);
    }
  });

  it("乱数が同じなら同じ品ぞろえになる", () => {
    expect(rollMerchantGoods("port-portoria", seeded(3))).toEqual(
      rollMerchantGoods("port-portoria", seeded(3)),
    );
    // 乱数が0なら、どの分類も一覧の初めの物から、品質Cの最小の倍率で並ぶ。
    expect(rollMerchantGoods("forest-ruins", () => 0)).toEqual([
      { category: "skill", id: "Slash", multiplierPercent: 100, price: 300 },
      { category: "skill", id: "Fire", multiplierPercent: 100, price: 300 },
      { category: "skill", id: "Heal", multiplierPercent: 100, price: 300 },
      {
        category: "accessory",
        id: "moss-charm",
        multiplierPercent: 100,
        price: 300,
      },
      {
        category: "accessory",
        id: "acorn-brooch",
        multiplierPercent: 100,
        price: 600,
      },
    ]);
  });
});
