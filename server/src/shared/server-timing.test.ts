import { Hono } from "hono";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  type ApiScenario,
  createApiScenario,
} from "../../tests/support/api-scenario.js";
import { applyServerTiming } from "./server-timing.js";

// Server-Timing の d1 の値（往復の回数）を読む。
const d1Calls = (header: string | null) =>
  Number(/d1;dur=[\d.]+;desc="(\d+) calls"/.exec(header ?? "")?.[1]);

describe("Server-Timing", () => {
  let scenario: ApiScenario;

  beforeAll(async () => {
    scenario = await createApiScenario();
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  it("D1の往復を数え、batchは文がいくつあっても1回とし、結果は変えない", async () => {
    const api = applyServerTiming(new Hono<{ Bindings: { DB: D1Database } }>());
    api.get("/probe", async (c) => {
      const one = await c.env.DB.prepare("SELECT 1 AS value").first();
      const [two, three] = await c.env.DB.batch([
        c.env.DB.prepare("SELECT 2 AS value"),
        c.env.DB.prepare("SELECT ?1 AS value").bind(3),
      ]);
      return c.json([one, two.results[0], three.results[0]]);
    });

    const response = await api.request("/probe", {}, scenario.env);

    expect(await response.json()).toEqual([
      { value: 1 },
      { value: 2 },
      { value: 3 },
    ]);
    const header = response.headers.get("server-timing");
    expect(d1Calls(header)).toBe(2);
    expect(header).toMatch(/total;dur=[\d.]+;desc="API"/);
  });

  it("APIの応答に、要求ごとのD1の往復の回数を返す", async () => {
    const { token } = await scenario.login("server-timing");
    await scenario.read("/party", token);

    const response = await scenario.request("/party", "GET", undefined, token);

    expect(response.status).toBe(200);
    expect(d1Calls(response.headers.get("server-timing"))).toBeGreaterThan(0);
  });
});
