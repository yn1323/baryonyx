import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  createHealthScenario,
  type HealthScenario,
} from "../support/health-scenario.js";

const sourceId = "aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa";

describe("認証と入力エラー", () => {
  let scenario: HealthScenario;
  beforeAll(async () => {
    scenario = await createHealthScenario();
  });
  afterAll(async () => {
    await scenario?.dispose();
  });

  it("認証設定がなければ失敗し、不正なトークンを拒否する", async () => {
    const { api, env, request } = scenario;
    expect(
      (
        await api.request(
          "/auth/google",
          { method: "POST" },
          { ...env, GOOGLE_CLIENT_ID: undefined },
        )
      ).status,
    ).toBe(503);
    expect(
      (await request("/auth/google", "POST", { idToken: "invalid" })).status,
    ).toBe(401);
    expect((await request(`/health/sources/${sourceId}/days`)).status).toBe(
      401,
    );
  });

  it("不正なJSONと大きすぎる要求を拒否する", async () => {
    const { api, env, request } = scenario;
    expect(
      (await api.request("/auth/google", { method: "POST", body: "{" }, env))
        .status,
    ).toBe(400);
    expect(
      (await request("/auth/google", "POST", { idToken: "a".repeat(17_000) }))
        .status,
    ).toBe(413);
  });
});
