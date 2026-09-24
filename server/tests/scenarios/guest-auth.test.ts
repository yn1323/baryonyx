import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { days } from "../../src/features/health/fixtures.js";
import {
  createHealthScenario,
  type HealthScenario,
} from "../support/health-scenario.js";

const sourceId = "bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb";
const secret = "a".repeat(64);

describe("ゲストの開始", () => {
  let scenario: HealthScenario;
  beforeAll(async () => {
    scenario = await createHealthScenario();
  });
  afterAll(async () => {
    await scenario?.dispose();
  });

  const guest = async (value: string) => {
    const response = await scenario.request("/auth/guest", "POST", {
      secret: value,
    });
    expect(response.status).toBe(200);
    return (await response.json()) as {
      token: string;
      userId: string;
      expiresAt: string;
    };
  };

  it("同じ秘密値では同じユーザーを返し、別の秘密値やGoogleとは分ける", async () => {
    const first = await guest(secret);
    const again = await guest(secret);
    const other = await guest("b".repeat(64));
    const google = await scenario.login(secret);
    expect(first.token).toMatch(/^[a-f0-9]{64}$/);
    expect(again.userId).toBe(first.userId);
    expect(again.token).not.toBe(first.token);
    expect(other.userId).not.toBe(first.userId);
    expect(google.userId).not.toBe(first.userId);
  });

  it("形式が違う秘密値を400で拒否する", async () => {
    for (const body of [
      null,
      {},
      { secret: "A".repeat(64) },
      { secret: "a".repeat(63) },
      { secret: 123 },
    ]) {
      const response = await scenario.request("/auth/guest", "POST", body);
      expect(response.status).toBe(400);
      expect(await response.json()).toEqual({ error: "invalid_request" });
    }
  });

  it("ゲストのセッションで歩数を保存・取得し、ログアウト後は拒否する", async () => {
    const { request } = scenario;
    const user = await guest("c".repeat(64));
    expect(
      (
        await request(
          `/health/sources/${sourceId}/days`,
          "GET",
          undefined,
          user.token,
        )
      ).status,
    ).toBe(404);
    const lease = await request(
      "/health/syncs",
      "POST",
      { sourceId, provider: "health_connect" },
      user.token,
    );
    expect(await lease.json()).toEqual({ revision: 1 });
    expect(
      (
        await request(
          `/health/sources/${sourceId}/days`,
          "PUT",
          { revision: 1, days: days(4321) },
          user.token,
        )
      ).status,
    ).toBe(200);
    const read = await request(
      `/health/sources/${sourceId}/days`,
      "GET",
      undefined,
      user.token,
    );
    expect(read.status).toBe(200);
    const body = (await read.json()) as { days: { steps: number }[] };
    expect(body.days).toHaveLength(7);
    expect(body.days.every((day) => day.steps === 4321)).toBe(true);
    expect(
      (await request("/auth/logout", "POST", undefined, user.token)).status,
    ).toBe(204);
    expect(
      (
        await request(
          `/health/sources/${sourceId}/days`,
          "GET",
          undefined,
          user.token,
        )
      ).status,
    ).toBe(401);
  });
});
