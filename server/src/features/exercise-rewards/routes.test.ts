import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  createHealthScenario,
  type HealthScenario,
} from "../../../tests/support/health-scenario.js";

const sourceId = "abababab-abab-4bab-8bab-abababababab";
const requestId = "cdcdcdcd-cdcd-4dcd-8dcd-cdcdcdcdcdcd";

describe("運動報酬API", () => {
  let scenario: HealthScenario;

  beforeAll(async () => {
    scenario = await createHealthScenario();
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  it("セッションのない要求を401で拒否する", async () => {
    const { request } = scenario;
    const responses = await Promise.all([
      request("/exercise/rewards/claim", "POST", { sourceId, requestId }),
      request(`/exercise/rewards/days?sourceId=${sourceId}`),
      request("/runes/balance"),
    ]);
    expect(responses.map((response) => response.status)).toEqual([
      401, 401, 401,
    ]);
  });

  it("不正な請求と取得元IDを400で拒否する", async () => {
    const { login, request } = scenario;
    const user = await login("exercise-rewards-invalid");
    for (const body of [
      null,
      { sourceId },
      { sourceId: "invalid", requestId },
      { sourceId, requestId: "invalid" },
    ]) {
      const response = await request(
        "/exercise/rewards/claim",
        "POST",
        body,
        user.token,
      );
      expect(response.status).toBe(400);
    }
    expect(
      (
        await request(
          "/exercise/rewards/days?sourceId=invalid",
          "GET",
          undefined,
          user.token,
        )
      ).status,
    ).toBe(400);
  });

  it("未登録または他人の取得元への請求を404で拒否する", async () => {
    const { login, request } = scenario;
    const owner = await login("exercise-rewards-owner");
    const other = await login("exercise-rewards-other");
    expect(
      (
        await request(
          "/health/syncs",
          "POST",
          { sourceId, provider: "health_connect" },
          owner.token,
        )
      ).status,
    ).toBe(200);
    const unknownSourceId = "efefefef-efef-4fef-8fef-efefefefefef";
    for (const [target, token] of [
      [sourceId, other.token],
      [unknownSourceId, owner.token],
    ] as const) {
      const claim = await request(
        "/exercise/rewards/claim",
        "POST",
        { sourceId: target, requestId },
        token,
      );
      expect(claim.status).toBe(404);
      const days = await request(
        `/exercise/rewards/days?sourceId=${target}`,
        "GET",
        undefined,
        token,
      );
      expect(days.status).toBe(404);
    }
  });

  it("請求前の残高を0として返す", async () => {
    const { login, request } = scenario;
    const user = await login("exercise-rewards-empty");
    const response = await request(
      "/runes/balance",
      "GET",
      undefined,
      user.token,
    );
    expect(response.status).toBe(200);
    expect(await response.json()).toMatchObject({ balance: 0 });
  });
});
