import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { days } from "../../src/features/health/fixtures.js";
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

  it("スキーマ違反を400で返し、不正な同期要求でDBを更新しない", async () => {
    const { request, login } = scenario;
    const user = await login("validation-subject");
    const expectInvalid = async (response: Response) => {
      expect(response.status).toBe(400);
      expect(response.headers.get("cache-control")).toBe("no-store");
      expect(await response.json()).toEqual({ error: "invalid_request" });
    };
    for (const body of [
      null,
      {},
      { idToken: 123 },
      { idToken: "a".repeat(12_001) },
    ]) {
      await expectInvalid(await request("/auth/google", "POST", body));
    }
    for (const body of [
      null,
      { sourceId: "bad-id", provider: "health_connect" },
      { sourceId, provider: "other" },
    ]) {
      await expectInvalid(
        await request("/health/syncs", "POST", body, user.token),
      );
    }
    expect(
      await (
        await request(
          "/health/syncs",
          "POST",
          {
            sourceId,
            provider: "health_connect",
          },
          user.token,
        )
      ).json(),
    ).toEqual({ revision: 1 });

    for (const body of [
      null,
      { revision: "1", days: days() },
      { revision: Number.MAX_SAFE_INTEGER + 1, days: days() },
      { revision: 1, days: days(-1) },
    ]) {
      await expectInvalid(
        await request(
          `/health/sources/${sourceId}/days`,
          "PUT",
          body,
          user.token,
        ),
      );
    }
    await expectInvalid(
      await request(
        "/health/sources/bad-id/days",
        "GET",
        undefined,
        user.token,
      ),
    );
    await expectInvalid(
      await request(
        "/health/sources/bad-id/days",
        "PUT",
        {
          revision: 1,
          days: days(),
        },
        user.token,
      ),
    );
    const saved = await request(
      `/health/sources/${sourceId}/days`,
      "PUT",
      {
        revision: 1,
        days: days(),
      },
      user.token,
    );
    expect(saved.status).toBe(200);
  });
});
