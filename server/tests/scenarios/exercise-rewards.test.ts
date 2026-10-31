import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { days } from "../../src/features/health/fixtures.js";
import {
  type ApiScenario,
  createApiScenario,
} from "../support/api-scenario.js";

const sourceId = "cccccccc-cccc-4ccc-8ccc-cccccccccccc";
const incrementSourceId = "dddddddd-dddd-4ddd-8ddd-dddddddddddd";
const concurrentSourceId = "eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee";
const syncSourceId = "f1f1f1f1-f1f1-4f1f-8f1f-f1f1f1f1f1f1";

// 応答の Server-Timing から、D1と往復した回数を読む（src/shared/server-timing.ts）。
const d1Calls = (response: Response) =>
  Number(
    /d1;dur=[\d.]+;desc="(\d+) calls"/.exec(
      response.headers.get("server-timing") ?? "",
    )?.[1],
  );

type RewardClaimResponse = {
  grantedRunes: number;
  balance: number;
  days: Array<{ creditedRunes: number }>;
};

type RewardDaysResponse = {
  days: Array<{ creditedRunes: number }>;
};

describe("運動報酬", () => {
  let scenario: ApiScenario;

  beforeAll(async () => {
    scenario = await createApiScenario();
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

  it("異なる請求が同時に届いても歩数を二重付与しない", async () => {
    const { login, request } = scenario;
    const user = await login("exercise-reward-concurrent-subject");
    expect(
      (
        await request(
          "/health/syncs",
          "POST",
          { sourceId: concurrentSourceId, provider: "health_connect" },
          user.token,
        )
      ).status,
    ).toBe(200);
    expect(
      (
        await request(
          `/health/sources/${concurrentSourceId}/days`,
          "PUT",
          { revision: 1, days: days(123) },
          user.token,
        )
      ).status,
    ).toBe(200);

    const claim = (requestId: string) =>
      request(
        "/exercise/rewards/claim",
        "POST",
        { sourceId: concurrentSourceId, requestId },
        user.token,
      );
    const responses = await Promise.all([
      claim("44444444-4444-4444-8444-444444444444"),
      claim("55555555-5555-4555-8555-555555555555"),
    ]);
    expect(responses.map((response) => response.status)).toEqual([200, 200]);

    const results = await Promise.all(
      responses.map(
        (response) => response.json() as Promise<RewardClaimResponse>,
      ),
    );
    expect(results.reduce((sum, result) => sum + result.grantedRunes, 0)).toBe(
      861,
    );
    expect(results.every((result) => result.balance === 861)).toBe(true);
    expect(
      await (
        await request("/runes/balance", "GET", undefined, user.token)
      ).json(),
    ).toMatchObject({ balance: 861 });
  });
  it("保存と請求を1回の要求で行い、再送は前回の結果を返し、増えた分だけ付与する", async () => {
    const { login, request } = scenario;
    const user = await login("exercise-reward-sync-subject");
    const sync = (requestId: string, steps: number) =>
      request(
        "/exercise/rewards/sync",
        "POST",
        {
          sourceId: syncSourceId,
          provider: "health_connect",
          requestId,
          days: days(steps),
        },
        user.token,
      );
    const firstId = "66666666-6666-4666-8666-666666666666";
    const firstResponse = await sync(firstId, 123);
    expect(firstResponse.status).toBe(200);
    const first = (await firstResponse.json()) as RewardClaimResponse;
    expect(first).toMatchObject({ grantedRunes: 861, balance: 861 });
    expect(first.days).toHaveLength(7);
    expect(first.days.every((day) => day.creditedRunes === 123)).toBe(true);

    const retry = await sync(firstId, 123);
    expect(retry.status).toBe(200);
    expect(await retry.json()).toEqual(first);

    // 確かめたセッションは覚えているため、版の発行・保存・請求の3回で済む。
    const more = await sync("77777777-7777-4777-8777-777777777777", 200);
    expect(d1Calls(more)).toBe(3);
    expect(await more.json()).toMatchObject({
      grantedRunes: 7 * 77,
      balance: 7 * 200,
    });
    const saved = (await (
      await request(
        `/health/sources/${syncSourceId}/days`,
        "GET",
        undefined,
        user.token,
      )
    ).json()) as { days: Array<{ steps: number }> };
    expect(saved.days.map((day) => day.steps)).toEqual(Array(7).fill(200));
  });

  it("保存と請求の要求で、他人の取得元と不正な本文を拒否する", async () => {
    const { login, request } = scenario;
    const owner = await login("exercise-reward-sync-owner");
    const other = await login("exercise-reward-sync-other");
    const body = {
      sourceId: "f2f2f2f2-f2f2-4f2f-8f2f-f2f2f2f2f2f2",
      provider: "health_connect",
      requestId: "88888888-8888-4888-8888-888888888888",
      days: days(10),
    };
    expect(
      (await request("/exercise/rewards/sync", "POST", body, owner.token))
        .status,
    ).toBe(200);
    const stolen = await request(
      "/exercise/rewards/sync",
      "POST",
      {
        ...body,
        requestId: "99999999-9999-4999-8999-999999999999",
      },
      other.token,
    );
    expect(stolen.status).toBe(404);
    for (const invalid of [
      { ...body, days: days(10).slice(1) },
      { ...body, requestId: "invalid" },
      { ...body, provider: "unknown" },
    ]) {
      expect(
        (await request("/exercise/rewards/sync", "POST", invalid, owner.token))
          .status,
      ).toBe(400);
    }
    expect((await request("/exercise/rewards/sync", "POST", body)).status).toBe(
      401,
    );
  });
});
