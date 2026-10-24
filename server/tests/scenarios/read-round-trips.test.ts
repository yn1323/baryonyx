import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  type ApiScenario,
  createApiScenario,
} from "../support/api-scenario.js";

// 応答の Server-Timing から、D1と往復した回数を読む（src/shared/server-timing.ts）。
const d1Calls = (response: Response) =>
  Number(
    /d1;dur=[\d.]+;desc="(\d+) calls"/.exec(
      response.headers.get("server-timing") ?? "",
    )?.[1],
  );

// 画面を開くたびに読むGETは、DBとの往復の回数がそのまま待ち時間になる。増えたら気づけるように回数を決めておく。
describe("一覧を読むGETのDBとの往復", () => {
  let scenario: ApiScenario;

  beforeAll(async () => {
    scenario = await createApiScenario();
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  it("2回目からは、セッションの確認と読み込みのbatchで済む", async () => {
    const { token } = await scenario.login("round-trips");
    const paths = {
      "/party": 2,
      "/equipment": 2,
      "/adventure": 2,
      "/library": 3,
    };
    for (const path of Object.keys(paths)) {
      await scenario.read(path, token);
    }

    const calls: Record<string, number> = {};
    for (const path of Object.keys(paths)) {
      const response = await scenario.request(path, "GET", undefined, token);
      expect(response.status).toBe(200);
      calls[path] = d1Calls(response);
    }

    expect(calls).toEqual(paths);
  });

  it("初めて読んだ装備は、パーティと装備を付与してから読み直す", async () => {
    const { token } = await scenario.login("round-trips-first");

    const response = await scenario.request(
      "/equipment",
      "GET",
      undefined,
      token,
    );

    expect(response.status).toBe(200);
    // セッションの確認・読み込み・パーティの付与・装備の付与・読み直し。
    expect(d1Calls(response)).toBe(5);
    const state = (await response.json()) as {
      items: unknown[];
      characters: { id: string }[];
    };
    expect(state.items.length).toBeGreaterThan(0);
    expect(state.characters.length).toBeGreaterThan(0);
  });
});
