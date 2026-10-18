import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  type ApiScenario,
  createApiScenario,
} from "../../../tests/support/api-scenario.js";

type State = {
  weaknesses: { enemyId: string; element: string; revealedAt: string }[];
};

describe("開示した敵の弱点のAPI", () => {
  let scenario: ApiScenario;

  beforeAll(async () => {
    scenario = await createApiScenario();
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  const read = (token: string) =>
    scenario.read<State>("/combat/weaknesses", token);

  const reveal = (token: string, body: unknown) =>
    scenario.request("/combat/weaknesses", "POST", body, token);

  const pairs = (state: State) =>
    state.weaknesses.map(({ enemyId, element }) => `${enemyId}:${element}`);

  it("セッションのない要求を401で拒否する", async () => {
    const responses = await Promise.all([
      scenario.request("/combat/weaknesses"),
      scenario.request("/combat/weaknesses", "POST", {
        weaknesses: [{ enemyId: "moss-slime", element: "fire" }],
      }),
    ]);
    expect(responses.map((response) => response.status)).toEqual([401, 401]);
  });

  it("初めて読むユーザーには、開示した弱点がない", async () => {
    const user = await scenario.login("combat-empty");
    expect(await read(user.token)).toEqual({ weaknesses: [] });
  });

  it("開示した弱点を残し、再送や重複では増やさず、最初の日時を保つ", async () => {
    const user = await scenario.login("combat-reveal");
    const first = await reveal(user.token, {
      weaknesses: [
        { enemyId: "moss-slime", element: "thunder" },
        { enemyId: "moss-slime", element: "thunder" },
      ],
    });
    expect(first.status).toBe(200);
    const afterFirst = (await first.json()) as State;
    expect(pairs(afterFirst)).toEqual(["moss-slime:thunder"]);

    const second = await reveal(user.token, {
      weaknesses: [
        { enemyId: "moss-slime", element: "thunder" },
        { enemyId: "moss-wolf", element: "ice" },
      ],
    });
    const afterSecond = (await second.json()) as State;
    expect(pairs(afterSecond)).toEqual(["moss-slime:thunder", "moss-wolf:ice"]);
    expect(afterSecond.weaknesses[0].revealedAt).toBe(
      afterFirst.weaknesses[0].revealedAt,
    );
    expect(await read(user.token)).toEqual(afterSecond);
  });

  it("知らない敵・属性や、空・形の違う本文を400で拒否する", async () => {
    const user = await scenario.login("combat-invalid");
    const bodies = [
      { weaknesses: [{ enemyId: "dragon", element: "fire" }] },
      { weaknesses: [{ enemyId: "moss-slime", element: "water" }] },
      { weaknesses: [] },
      { weaknesses: "moss-slime" },
      {},
    ];
    for (const body of bodies) {
      expect((await reveal(user.token, body)).status).toBe(400);
    }
    const tooMany = {
      weaknesses: Array.from({ length: 19 }, () => ({
        enemyId: "moss-slime",
        element: "fire",
      })),
    };
    expect((await reveal(user.token, tooMany)).status).toBe(400);
    expect(await read(user.token)).toEqual({ weaknesses: [] });
  });

  it("ユーザーごとに記録を分ける", async () => {
    const one = await scenario.login("combat-one");
    const other = await scenario.login("combat-other");
    await reveal(one.token, {
      weaknesses: [{ enemyId: "forest-guardian", element: "slash" }],
    });
    expect(pairs(await read(one.token))).toEqual(["forest-guardian:slash"]);
    expect(await read(other.token)).toEqual({ weaknesses: [] });
  });
});
