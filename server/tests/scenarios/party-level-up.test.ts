import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { days } from "../../src/features/health/fixtures.js";
import {
  type ApiScenario,
  createApiScenario,
} from "../support/api-scenario.js";

const sourceId = "f0f0f0f0-f0f0-4f0f-8f0f-f0f0f0f0f0f0";

type PartyResponse = {
  characters: { id: string; level: number }[];
  runes: number;
};

describe("歩数から得たルーンでのレベルアップ", () => {
  let scenario: ApiScenario;

  beforeAll(async () => {
    scenario = await createApiScenario();
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  it("請求したルーンでレベルを上げ、同時の要求でも1回分だけ引く", async () => {
    const { login, request } = scenario;
    const user = await login("party-level-up-scenario");
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
          { revision: 1, days: days(1000) },
          user.token,
        )
      ).status,
    ).toBe(200);
    expect(
      await (
        await request(
          "/exercise/rewards/claim",
          "POST",
          { sourceId, requestId: "f1f1f1f1-f1f1-4f1f-8f1f-f1f1f1f1f1f1" },
          user.token,
        )
      ).json(),
    ).toMatchObject({ grantedRunes: 7000, balance: 7000 });
    const party = (await (
      await request("/party", "GET", undefined, user.token)
    ).json()) as PartyResponse;
    expect(party.runes).toBe(7000);

    // トーマ：Lv 12 → 14 は 1,200 + 1,300 = 2,500ルーン。
    const raised = await request(
      "/party/characters/toma/level-up",
      "POST",
      {
        requestId: "f2f2f2f2-f2f2-4f2f-8f2f-f2f2f2f2f2f2",
        fromLevel: 12,
        toLevel: 14,
      },
      user.token,
    );
    expect(raised.status).toBe(200);
    expect(
      await (
        await request("/runes/balance", "GET", undefined, user.token)
      ).json(),
    ).toMatchObject({ balance: 4500 });

    // ルカ：Lv 11 → 12 の別々の要求が同時に届いても、上げるのは1回だけ。
    const responses = await Promise.all(
      [
        "f3f3f3f3-f3f3-4f3f-8f3f-f3f3f3f3f3f3",
        "f4f4f4f4-f4f4-4f4f-8f4f-f4f4f4f4f4f4",
      ].map((requestId) =>
        request(
          "/party/characters/luka/level-up",
          "POST",
          { requestId, fromLevel: 11, toLevel: 12 },
          user.token,
        ),
      ),
    );
    expect(responses.map((response) => response.status).sort()).toEqual([
      200, 409,
    ]);

    const after = (await (
      await request("/party", "GET", undefined, user.token)
    ).json()) as PartyResponse;
    expect(after.runes).toBe(3400);
    expect(
      after.characters.find((character) => character.id === "luka")?.level,
    ).toBe(12);
    expect(
      await (
        await request("/runes/balance", "GET", undefined, user.token)
      ).json(),
    ).toMatchObject({ balance: 3400 });

    // 増えた歩数のない再請求では、使ったルーンを戻さない。
    expect(
      await (
        await request(
          "/exercise/rewards/claim",
          "POST",
          { sourceId, requestId: "f5f5f5f5-f5f5-4f5f-8f5f-f5f5f5f5f5f5" },
          user.token,
        )
      ).json(),
    ).toMatchObject({ grantedRunes: 0, balance: 3400 });
  });
});
