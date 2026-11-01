import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  type ApiScenario,
  createApiScenario,
} from "../../../tests/support/api-scenario.js";
import { DEBUG_RUNES, debugToolsEnabled } from "./routes.js";

describe("デバッグAPI", () => {
  let scenario: ApiScenario;

  beforeAll(async () => {
    scenario = await createApiScenario();
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  it("ルーンを10000足し、何度でも足せる。セッションがなければ401", async () => {
    expect((await scenario.request("/debug/runes", "POST", {})).status).toBe(
      401,
    );
    const user = await scenario.login("debug-runes");
    await scenario.read("/library", user.token);
    await scenario.setRunes(user.userId, 120);
    const first = await scenario.request(
      "/debug/runes",
      "POST",
      {},
      user.token,
    );
    expect(first.status).toBe(200);
    expect(await first.json()).toEqual({
      runes: DEBUG_RUNES,
      balanceAfter: 120 + DEBUG_RUNES,
    });
    const second = await scenario.request(
      "/debug/runes",
      "POST",
      {},
      user.token,
    );
    expect(await second.json()).toMatchObject({
      balanceAfter: 120 + DEBUG_RUNES * 2,
    });
    expect(
      (await scenario.read<{ runes: number }>("/adventure", user.token)).runes,
    ).toBe(120 + DEBUG_RUNES * 2);
  });

  it("持ち物を品質の幅から足し、見つけた記録も書く。知らない種類は404、不正な入力は400", async () => {
    const user = await scenario.login("debug-grant");
    const grant = (body: unknown) =>
      scenario.request("/debug/grant", "POST", body, user.token);
    const count = async (query: string, ...values: unknown[]) =>
      (
        await scenario.env.DB.prepare(query)
          .bind(user.userId, ...values)
          .first<{ n: number }>()
      )?.n ?? 0;

    const before = await count(
      "SELECT COUNT(*) AS n FROM equipment_items WHERE user_id = ? AND equipment_id = ?",
      "kraken-pearl",
    );
    const gear = await grant({
      category: "accessory",
      id: "kraken-pearl",
      quality: "A",
      count: 3,
    });
    expect(gear.status).toBe(200);
    const body = (await gear.json()) as {
      granted: { multiplier: number; quality: string }[];
    };
    expect(body.granted).toHaveLength(3);
    for (const each of body.granted) {
      expect(each.quality).toBe("A");
      // 上限×2.60のAは、幅の上から10〜30%（×2.12〜×2.44）。
      expect(each.multiplier).toBeGreaterThanOrEqual(2.12);
      expect(each.multiplier).toBeLessThanOrEqual(2.44);
    }
    expect(
      await count(
        "SELECT COUNT(*) AS n FROM equipment_items WHERE user_id = ? AND equipment_id = ?",
        "kraken-pearl",
      ),
    ).toBe(before + 3);
    expect(
      await count(
        "SELECT COUNT(*) AS n FROM library_discoveries WHERE user_id = ? AND kind = ? AND entry_id = ?",
        "accessory",
        "kraken-pearl",
      ),
    ).toBe(1);

    const skill = await grant({ category: "skill", id: "Fire", quality: "S" });
    expect(await skill.json()).toMatchObject({
      count: 1,
      granted: [{ quality: "S" }],
    });

    for (const times of [2, 10]) {
      const item = await grant({ category: "item", id: "herb", count: times });
      expect(await item.json()).toMatchObject({ count: times, granted: [] });
    }
    expect(
      await count(
        "SELECT count AS n FROM item_holdings WHERE user_id = ? AND item_id = ?",
        "herb",
      ),
    ).toBe(12);
    expect(
      (await grant({ category: "material", id: "polish-gold", count: 4 }))
        .status,
    ).toBe(200);
    expect(
      await count(
        "SELECT count AS n FROM enhancement_materials WHERE user_id = ? AND material_id = ?",
        "polish-gold",
      ),
    ).toBe(4);

    // 武器を防具として、知らない種類を足そうとしたとき。
    expect(
      (await grant({ category: "armor", id: "flame-dagger" })).status,
    ).toBe(404);
    expect((await grant({ category: "item", id: "dragon" })).status).toBe(404);
    expect(
      (await grant({ category: "item", id: "herb", count: 11 })).status,
    ).toBe(400);
    expect(
      (await grant({ category: "skill", id: "Fire", quality: "X" })).status,
    ).toBe(400);
  });

  it("DEBUG_TOOLS が on のときだけ使える", () => {
    expect(debugToolsEnabled({ DEBUG_TOOLS: "on" })).toBe(true);
    expect(debugToolsEnabled({ DEBUG_TOOLS: "off" })).toBe(false);
    expect(debugToolsEnabled({})).toBe(false);
  });
});
