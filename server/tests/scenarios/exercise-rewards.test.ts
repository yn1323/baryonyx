import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { days } from "../../src/features/health/fixtures.js";
import {
  createHealthScenario,
  type HealthScenario,
} from "../support/health-scenario.js";

const sourceId = "cccccccc-cccc-4ccc-8ccc-cccccccccccc";
const incrementSourceId = "dddddddd-dddd-4ddd-8ddd-dddddddddddd";

type RewardClaimResponse = {
  grantedRunes: number;
  balance: number;
  days: Array<{ creditedRunes: number }>;
};

type RewardDaysResponse = {
  days: Array<{ creditedRunes: number }>;
};

describe("運動報酬", () => {
  let scenario: HealthScenario;

  beforeAll(async () => {
    scenario = await createHealthScenario();
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  it("過去7日分を初回付与し、同じ請求を冪等に処理する", async () => {
    const { login, request } = scenario;
    const user = await login("exercise-reward-subject");
    expect(
      (
        await request(
          "/health/syncs",
          "POST",
          { sourceId, provider: "health_connect" },
          user.token,
        )
      ).status,
    ).toBe(200);
    expect(
      (
        await request(
          `/health/sources/${sourceId}/days`,
          "PUT",
          { revision: 1, days: days(123) },
          user.token,
        )
      ).status,
    ).toBe(200);

    const requestId = "11111111-1111-4111-8111-111111111111";
    const claim = async (id: string) =>
      request(
        "/exercise/rewards/claim",
        "POST",
        { sourceId, requestId: id },
        user.token,
      );
    const first = (await (
      await claim(requestId)
    ).json()) as RewardClaimResponse;
    expect(first).toMatchObject({
      grantedRunes: 861,
      balance: 861,
    });
    expect(first.days).toHaveLength(7);
    expect(await (await claim(requestId)).json()).toEqual(first);

    const balance = await (
      await request("/runes/balance", "GET", undefined, user.token)
    ).json();
    expect(balance).toMatchObject({ balance: 861 });
    const stored = (await (
      await request(
        `/exercise/rewards/days?sourceId=${sourceId}`,
        "GET",
        undefined,
        user.token,
      )
    ).json()) as RewardDaysResponse;
    expect(stored.days).toHaveLength(7);
    expect(
      stored.days.every(
        (day: { creditedRunes: number }) => day.creditedRunes === 123,
      ),
    ).toBe(true);
  });

  it("再同期で増えた歩数だけ付与し、後から減っても取り消さない", async () => {
    const { login, request } = scenario;
    const user = await login("exercise-reward-increment-subject");
    expect(
      (
        await request(
          "/health/syncs",
          "POST",
          { sourceId: incrementSourceId, provider: "health_connect" },
          user.token,
        )
      ).status,
    ).toBe(200);
    expect(
      (
        await request(
          `/health/sources/${incrementSourceId}/days`,
          "PUT",
          { revision: 1, days: days(200) },
          user.token,
        )
      ).status,
    ).toBe(200);
    const claim = (requestId: string) =>
      request(
        "/exercise/rewards/claim",
        "POST",
        { sourceId: incrementSourceId, requestId },
        user.token,
      );
    expect(
      await (await claim("22222222-2222-4222-8222-222222222222")).json(),
    ).toMatchObject({ grantedRunes: 1400, balance: 1400 });

    expect(
      (
        await request(
          "/health/syncs",
          "POST",
          { sourceId: incrementSourceId, provider: "health_connect" },
          user.token,
        )
      ).status,
    ).toBe(200);
    expect(
      (
        await request(
          `/health/sources/${incrementSourceId}/days`,
          "PUT",
          { revision: 2, days: days(50) },
          user.token,
        )
      ).status,
    ).toBe(200);
    expect(
      await (await claim("33333333-3333-4333-8333-333333333333")).json(),
    ).toMatchObject({ grantedRunes: 0, balance: 1400 });
  });
});
