import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { hashToken } from "../../src/features/health/routes.js";
import {
  createHealthScenario,
  type HealthScenario,
} from "../support/health-scenario.js";

const sourceId = "aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa";

describe("セッションの失効", () => {
  let scenario: HealthScenario;
  beforeAll(async () => {
    scenario = await createHealthScenario();
  });
  afterAll(async () => {
    await scenario?.dispose();
  });

  it("期限切れ・ログアウトしたセッションを拒否する", async () => {
    const { env, login, request } = scenario;
    const user = await login("logout-subject");
    expect(
      (await request("/auth/logout", "POST", undefined, user.token)).status,
    ).toBe(204);
    expect(
      (await request("/auth/logout", "POST", undefined, user.token)).status,
    ).toBe(401);
    const expired = await login("expired-subject");
    await env.DB.prepare(
      "UPDATE app_sessions SET expires_at = 0 WHERE token_hash = ?",
    )
      .bind(await hashToken(expired.token))
      .run();
    expect(
      (
        await request(
          `/health/sources/${sourceId}/days`,
          "GET",
          undefined,
          expired.token,
        )
      ).status,
    ).toBe(401);
  });
});
