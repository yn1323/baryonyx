import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  type ApiScenario,
  createApiScenario,
} from "../../../tests/support/api-scenario.js";
import { CHARACTER_IDS } from "../party/catalog.js";
import { BOND_MAX } from "./catalog.js";
import { createSummonRepository } from "./repository.js";

type Result = {
  category: string;
  id: string;
  stars: number;
  itemId: string | null;
  multiplier: number | null;
  quality: string | null;
  isNew?: boolean;
  bond?: number;
  tokens?: number;
  shards?: number;
};

type Pull = {
  count: number;
  runes: number;
  balanceAfter: number;
  results: Result[];
};

const requestIds = {
  first: "dddddddd-1111-4111-8111-dddddddddddd",
  second: "dddddddd-2222-4222-8222-dddddddddddd",
  third: "dddddddd-3333-4333-8333-dddddddddddd",
  fourth: "dddddddd-4444-4444-8444-dddddddddddd",
};

describe("召喚API", () => {
  let scenario: ApiScenario;

  beforeAll(async () => {
    scenario = await createApiScenario();
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  const summon = async (
    token: string,
    body: unknown,
    expected = 200,
  ): Promise<{ summon?: Pull; error?: string }> => {
    const response = await scenario.request("/summon", "POST", body, token);
    expect(response.status).toBe(expected);
    return (await response.json()) as { summon?: Pull; error?: string };
  };

  const count = async (sql: string, ...values: string[]) =>
    (
      await scenario.env.DB.prepare(sql)
        .bind(...values)
        .first<{ n: number }>()
    )?.n ?? 0;

  it("セッションのない要求を401で、不正な入力を400で拒否する", async () => {
    const response = await scenario.request("/summon", "POST", {
      requestId: requestIds.first,
      count: 1,
    });
    expect(response.status).toBe(401);

    const user = await scenario.login("summon-invalid");
    for (const body of [
      { requestId: "short", count: 1 },
      { requestId: requestIds.first, count: 5 },
      { requestId: requestIds.first },
    ]) {
      await summon(user.token, body, 400);
    }
  });

  it("10回召喚でルーンを3,000使い、出た物を持ち物と見つけた記録に足し、再送では二重に引かない", async () => {
    const user = await scenario.login("summon-ten");
    // ライブラリを読むと、初めの装備とカードを配る。その数を除いて数える。
    await scenario.read("/library", user.token);
    await scenario.setRunes(user.userId, 3500);
    const before = await count(
      "SELECT COUNT(*) AS n FROM equipment_items WHERE user_id = ?",
      user.userId,
    );

    const first = await summon(user.token, {
      requestId: requestIds.first,
      count: 10,
    });
    const pull = first.summon as Pull;
    expect(pull.count).toBe(10);
    expect(pull.runes).toBe(3000);
    expect(pull.balanceAfter).toBe(500);
    expect(pull.results).toHaveLength(10);
    expect(await scenario.runesOf(user.token)).toBe(500);
    // 10回のうち1つは☆3以上かキャラ。
    expect(
      pull.results.some(
        (result) => result.category === "character" || result.stars >= 3,
      ),
    ).toBe(true);

    // 装備は1つずつ行が増え、倍率と品質を返す。
    const equipment = pull.results.filter((result) => result.itemId !== null);
    expect(
      await count(
        "SELECT COUNT(*) AS n FROM equipment_items WHERE user_id = ?",
        user.userId,
      ),
    ).toBe(before + equipment.length);
    for (const result of equipment) {
      expect(result.multiplier).toBeGreaterThanOrEqual(1);
      expect(["S", "A", "B", "C"]).toContain(result.quality);
    }
    // アイテムは種類ごとの数が増え、見つけた記録が残る。
    for (const result of pull.results.filter((r) => r.category === "item")) {
      expect(
        await count(
          "SELECT count AS n FROM item_holdings WHERE user_id = ? AND item_id = ?",
          user.userId,
          result.id,
        ),
      ).toBeGreaterThan(0);
    }
    for (const result of pull.results) {
      expect(
        await count(
          "SELECT COUNT(*) AS n FROM library_discoveries WHERE user_id = ? AND kind = ? AND entry_id = ?",
          user.userId,
          result.category,
          result.id,
        ),
      ).toBe(1);
    }

    // 同じ要求IDの再送は、同じ結果を返し、ルーンを二重に引かない。
    const replay = await summon(user.token, {
      requestId: requestIds.first,
      count: 10,
    });
    expect(replay.summon).toEqual(pull);
    expect(await scenario.runesOf(user.token)).toBe(500);
    // 回数を変えて使い回した要求IDは拒む。
    expect(
      (await summon(user.token, { requestId: requestIds.first, count: 1 }, 409))
        .error,
    ).toBe("request_conflict");
    // 残高が足りなければ引かない。
    expect(
      (
        await summon(
          user.token,
          { requestId: requestIds.second, count: 10 },
          409,
        )
      ).error,
    ).toBe("insufficient_runes");
    expect(await scenario.runesOf(user.token)).toBe(500);
    expect(
      await count(
        "SELECT COUNT(*) AS n FROM summon_pulls WHERE user_id = ?",
        user.userId,
      ),
    ).toBe(1);
  });

  it("持っているキャラが出ると絆のしるしが増え、段としるしの合計が上限に達したあとはかけらになる", async () => {
    const user = await scenario.login("summon-bond");
    // 編成を読むと、初めのキャラ（全員）を配る。
    await scenario.read("/party", user.token);
    await scenario.setRunes(user.userId, 3000);
    // キャラだけが出る乱数：分類の抽選の最初の値0で、いちばん前のキャラの分類を選ぶ。
    const repository = createSummonRepository(scenario.env.DB, () => 0);
    const now = new Date().toISOString();
    const first = await repository.summon(
      user.userId,
      { requestId: requestIds.third, count: 10 },
      now,
    );
    if (!("summon" in first) || first.summon === undefined)
      throw new Error(first.error);
    const results = first.summon.results;
    expect(results.every((result) => result.category === "character")).toBe(
      true,
    );
    const character = CHARACTER_IDS[0];
    // 10回とも同じキャラ：絆の段は0のまま、しるしが6つまで増え、残りの4回はかけらになる。
    expect(results.map((result) => result.bond)).toEqual(Array(10).fill(0));
    expect(results.map((result) => result.tokens)).toEqual([
      1, 2, 3, 4, 5, 6, 6, 6, 6, 6,
    ]);
    expect(results.filter((result) => result.shards === 1)).toHaveLength(4);

    const state = await scenario.read<{
      bonds: { characterId: string; bond: number; tokens: number }[];
      shards: number;
    }>("/summon", user.token);
    expect(state.bonds).toEqual([
      { characterId: character, bond: 0, tokens: BOND_MAX },
    ]);
    expect(state.shards).toBe(4);

    // 編成でも絆としるしが読め、しるしを1つ使って1段深める。同じ要求の再送は何も変えない。
    const deepen = async (bond: number, expected = 200) => {
      const response = await scenario.request(
        `/party/characters/${character}/bond`,
        "POST",
        { bond },
        user.token,
      );
      expect(response.status).toBe(expected);
      return (await response.json()) as {
        change?: string;
        error?: string;
        characters?: { id: string; bond: number; tokens: number }[];
      };
    };
    const deepened = await deepen(1);
    expect(deepened.change).toBe("deepen");
    expect(
      deepened.characters?.find((entry) => entry.id === character),
    ).toMatchObject({ bond: 1, tokens: BOND_MAX - 1 });
    expect((await deepen(1)).change).toBe("none");
    // 今の段の次でなければ拒む。
    expect((await deepen(3, 409)).error).toBe("not_next_bond");

    // 段としるしの合計が上限のままなので、次の被りもかけらになる。
    await scenario.setRunes(user.userId, 300);
    const second = await repository.summon(
      user.userId,
      { requestId: requestIds.fourth, count: 1 },
      now,
    );
    if (!("summon" in second) || second.summon === undefined)
      throw new Error(second.error);
    expect(second.summon.results[0]).toMatchObject({
      bond: 1,
      tokens: BOND_MAX - 1,
      shards: 1,
    });
  });

  it("しるしがなければ深めず、上限を超える段は400で拒む", async () => {
    const user = await scenario.login("summon-bond-empty");
    const party = await scenario.read<{
      characters: { id: string; bond: number; tokens: number }[];
    }>("/party", user.token);
    const first = party.characters[0];
    expect(first).toMatchObject({ bond: 0, tokens: 0 });
    const response = await scenario.request(
      `/party/characters/${first.id}/bond`,
      "POST",
      { bond: 1 },
      user.token,
    );
    expect(response.status).toBe(409);
    expect(((await response.json()) as { error: string }).error).toBe(
      "no_bond_token",
    );
    const invalid = await scenario.request(
      `/party/characters/${first.id}/bond`,
      "POST",
      { bond: 7 },
      user.token,
    );
    expect(invalid.status).toBe(400);
  });
});
