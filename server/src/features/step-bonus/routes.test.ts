import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  type ApiScenario,
  createApiScenario,
} from "../../../tests/support/api-scenario.js";
import { STARTER_HOLDINGS, STARTER_SLOTS } from "./catalog.js";
import { createStepBonusRepository } from "./repository.js";

type State = {
  change?: string;
  slots: { slot: number; bonusId: string | null }[];
  holdings: { bonusId: string; rank: string }[];
  locked: boolean;
};

describe("ACTボーナスAPI", () => {
  let scenario: ApiScenario;

  beforeAll(async () => {
    scenario = await createApiScenario();
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  const read = (token: string) => scenario.read<State>("/step-bonus", token);

  async function put(token: string, slot: number | string, body: unknown) {
    return scenario.request(`/step-bonus/slots/${slot}`, "PUT", body, token);
  }

  it("セッションのない要求を401で拒否する", async () => {
    const responses = await Promise.all([
      scenario.request("/step-bonus"),
      scenario.request("/step-bonus/slots/0", "PUT", { bonusId: "guard" }),
    ]);
    expect(responses.map((response) => response.status)).toEqual([401, 401]);
  });

  it("初めて読むユーザーに初期のボーナスと枠を1回だけ付与する", async () => {
    const user = await scenario.login("step-bonus-starter");
    const first = await read(user.token);
    expect(first.slots.map((slot) => slot.bonusId)).toEqual(STARTER_SLOTS);
    expect(first.slots.map((slot) => slot.slot)).toEqual([0, 1, 2, 3, 4]);
    expect(
      first.holdings.map(({ bonusId, rank }) => ({ bonusId, rank })),
    ).toEqual(expect.arrayContaining([...STARTER_HOLDINGS]));
    expect(first.holdings).toHaveLength(STARTER_HOLDINGS.length);

    const second = await read(user.token);
    expect(second).toEqual(first);
  });

  it("空いているボーナスを枠に入れ、ほかの枠のボーナスなら入れ替える", async () => {
    const user = await scenario.login("step-bonus-set");
    await read(user.token);

    const set = await put(user.token, 3, { bonusId: "guard" });
    expect(set.status).toBe(200);
    const afterSet = (await set.json()) as State;
    expect(afterSet.change).toBe("set");
    expect(afterSet.slots[3].bonusId).toBe("guard");
    // 外したボーナスも持ったままにする。
    expect(afterSet.slots.some((slot) => slot.bonusId === "fighting")).toBe(
      false,
    );
    expect(afterSet.holdings.map((holding) => holding.bonusId)).toContain(
      "fighting",
    );

    const swap = await put(user.token, 2, { bonusId: "luck" });
    const afterSwap = (await swap.json()) as State;
    expect(afterSwap.change).toBe("swap");
    expect(afterSwap.slots[2].bonusId).toBe("luck");
    expect(afterSwap.slots[1].bonusId).toBe("treasure-sight");

    const none = await put(user.token, 2, { bonusId: "luck" });
    expect(((await none.json()) as State).change).toBe("none");
    expect(await read(user.token)).toEqual({
      slots: afterSwap.slots,
      holdings: afterSwap.holdings,
      locked: false,
    });
  });

  it("不正な枠とボーナスを400で、持っていないボーナスを404で拒否する", async () => {
    const user = await scenario.login("step-bonus-invalid");
    await read(user.token);
    for (const [slot, body] of [
      [5, { bonusId: "guard" }],
      ["-1", { bonusId: "guard" }],
      ["a", { bonusId: "guard" }],
      [0, { bonusId: "healing" }],
      [0, null],
    ] as const) {
      expect((await put(user.token, slot, body)).status).toBe(400);
    }

    // 枠に入れていない「兆し」を持ち物から外すと、枠に入れられない。
    await scenario.env.DB.prepare(
      "DELETE FROM step_bonus_holdings WHERE user_id = ? AND bonus_id = 'omen'",
    )
      .bind(user.userId)
      .run();
    const missing = await put(user.token, 0, { bonusId: "omen" });
    expect(missing.status).toBe(404);
    expect(await missing.json()).toEqual({ error: "bonus_not_owned" });
  });

  it("ユーザーごとに別の持ち物と枠を持つ", async () => {
    const owner = await scenario.login("step-bonus-owner");
    const other = await scenario.login("step-bonus-other");
    await read(owner.token);
    const before = await read(other.token);

    await put(owner.token, 0, { bonusId: "guard" });
    expect((await read(owner.token)).slots[0].bonusId).toBe("guard");
    expect(await read(other.token)).toEqual(before);
  });

  it("同じボーナスを手に入れたらランクの高いほうだけを残す", async () => {
    const user = await scenario.login("step-bonus-acquire");
    await read(user.token);
    const repository = createStepBonusRepository(scenario.env.DB);
    const now = new Date().toISOString();

    // 幸運はAから始まる。
    expect(await repository.acquire(user.userId, "luck", "B", now)).toBe(
      "discarded",
    );
    expect(await repository.acquire(user.userId, "luck", "S", now)).toBe(
      "updated",
    );
    await scenario.env.DB.prepare(
      "DELETE FROM step_bonus_holdings WHERE user_id = ? AND bonus_id = 'omen'",
    )
      .bind(user.userId)
      .run();
    expect(await repository.acquire(user.userId, "omen", "C", now)).toBe(
      "added",
    );

    const holdings = (await read(user.token)).holdings;
    expect(holdings.find((holding) => holding.bonusId === "luck")?.rank).toBe(
      "S",
    );
    expect(holdings.find((holding) => holding.bonusId === "omen")?.rank).toBe(
      "C",
    );
    expect(
      holdings.filter((holding) => holding.bonusId === "luck"),
    ).toHaveLength(1);
  });
});
