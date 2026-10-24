import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { DESTINATIONS } from "../../src/features/adventure/catalog.js";
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

// 画面を開くたびに読むGETと書き込みのAPIは、DBとの往復の回数がそのまま待ち時間になる。増えたら気づけるように回数を決めておく。
// 確かめたセッションはWorkerのメモリに覚えるため、2回目の要求からはセッションの確認でDBへ問い合わせない。
describe("APIのDBとの往復", () => {
  let scenario: ApiScenario;

  beforeAll(async () => {
    scenario = await createApiScenario();
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  it("2回目からは、一覧を読むGETが読み込みのbatchの1回で済む", async () => {
    const { token } = await scenario.login("round-trips");
    const paths = {
      "/party": 1,
      "/equipment": 1,
      "/adventure": 1,
      "/library": 1,
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

  it("ログインで編成と装備を付与し、初めて読む装備もセッションの確認と読み込みの2回で済む", async () => {
    const { token } = await scenario.login("round-trips-first");

    const response = await scenario.request(
      "/equipment",
      "GET",
      undefined,
      token,
    );

    expect(response.status).toBe(200);
    expect(d1Calls(response)).toBe(2);
    const state = (await response.json()) as {
      items: unknown[];
      characters: { id: string }[];
    };
    expect(state.items.length).toBeGreaterThan(0);
    expect(state.characters.length).toBeGreaterThan(0);
  });

  it("冒険を始める・やめるは、確かめる読み込みと、書き込みと書いたあとの読み込みの2回で済む", async () => {
    const { token } = await scenario.login("round-trips-adventure");
    await scenario.read("/adventure", token);

    const started = await scenario.request(
      "/adventure",
      "POST",
      { destinationId: DESTINATIONS[0].id },
      token,
    );
    expect(started.status).toBe(200);
    const ended = await scenario.request(
      "/adventure/end",
      "POST",
      { reason: "retreat" },
      token,
    );
    expect(ended.status).toBe(200);

    expect([d1Calls(started), d1Calls(ended)]).toEqual([2, 2]);
  });
});
