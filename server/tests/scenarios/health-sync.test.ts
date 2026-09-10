import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { days } from "../../src/features/health/fixtures.js";
import {
  createHealthScenario,
  type HealthScenario,
} from "../support/health-scenario.js";

const sourceId = "aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa";

describe("歩数の同期", () => {
  let scenario: HealthScenario;
  beforeAll(async () => {
    scenario = await createHealthScenario();
  });
  afterAll(async () => {
    await scenario?.dispose();
  });

  it("本人の値を保存・取得し、再送、古い版、他ユーザーの操作を拒否する", async () => {
    const { login, request } = scenario;
    const user = await login();
    const other = await login("another-subject");
    const lease = () =>
      request(
        "/health/syncs",
        "POST",
        { sourceId, provider: "health_connect" },
        user.token,
      );
    expect(await (await lease()).json()).toEqual({ revision: 1 });
    const put = (revision: number, values = days()) =>
      request(
        `/health/sources/${sourceId}/days`,
        "PUT",
        { revision, days: values },
        user.token,
      );
    expect((await put(1)).status).toBe(200);
    expect((await put(1)).status).toBe(409);
    expect(await (await lease()).json()).toEqual({ revision: 2 });
    expect((await put(1, days(999))).status).toBe(409);
    const knownDays = days(12);
    expect((await put(2, knownDays)).status).toBe(200);
    const response = await request(
      `/health/sources/${sourceId}/days`,
      "GET",
      undefined,
      user.token,
    );
    expect(response.headers.get("cache-control")).toBe("no-store");
    const saved = (await response.json()) as { days: { steps: number }[] };
    expect(saved.days).toHaveLength(7);
    expect(saved.days.every((d) => d.steps === 12)).toBe(true);
    expect(
      (
        await request(
          `/health/sources/${sourceId}/days`,
          "GET",
          undefined,
          other.token,
        )
      ).status,
    ).toBe(404);
    expect(
      (
        await request(
          "/health/syncs",
          "POST",
          { sourceId, provider: "health_connect" },
          other.token,
        )
      ).status,
    ).toBe(404);
    expect(
      (
        await request(
          `/health/sources/${sourceId}/days`,
          "PUT",
          { revision: 2, days: days() },
          other.token,
        )
      ).status,
    ).toBe(409);
    await lease();
    expect(
      (
        await put(
          3,
          days(0).map((d) => ({ ...d, hasValue: false })),
        )
      ).status,
    ).toBe(200);
    const empty = (await (
      await request(
        `/health/sources/${sourceId}/days`,
        "GET",
        undefined,
        user.token,
      )
    ).json()) as {
      days: {
        steps: unknown;
        lastKnownSteps: number;
        hasValue: boolean;
        hasLastKnownValue: boolean;
        lastKnownObservedAt: string;
      }[];
    };
    expect(
      empty.days.every(
        (d) =>
          d.steps === null &&
          d.lastKnownSteps === 12 &&
          !d.hasValue &&
          d.hasLastKnownValue &&
          d.lastKnownObservedAt === knownDays[0].observedAt,
      ),
    ).toBe(true);
  });

  it("未取得と0歩を区別し、Providerの変更で取得元の版を進めない", async () => {
    const { login, request } = scenario;
    const user = await login("zero-steps-subject");
    const source = "bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb";
    const lease = (provider = "healthkit") =>
      request(
        "/health/syncs",
        "POST",
        { sourceId: source, provider },
        user.token,
      );
    expect(await (await lease()).json()).toEqual({ revision: 1 });
    const unknownDays = days(0).map((day) => ({ ...day, hasValue: false }));
    const put = (revision: number, values: ReturnType<typeof days>) =>
      request(
        `/health/sources/${source}/days`,
        "PUT",
        { revision, days: values },
        user.token,
      );
    expect((await put(1, unknownDays)).status).toBe(200);
    const read = async () =>
      (
        await request(
          `/health/sources/${source}/days`,
          "GET",
          undefined,
          user.token,
        )
      ).json() as Promise<{ days: Record<string, unknown>[] }>;
    const unknown = await read();
    expect(unknown.days).toHaveLength(7);
    for (const day of unknown.days) {
      expect(day).toMatchObject({
        hasValue: false,
        steps: null,
        hasLastKnownValue: false,
        lastKnownSteps: null,
        lastKnownObservedAt: null,
      });
    }
    expect((await lease("health_connect")).status).toBe(404);
    expect(await (await lease()).json()).toEqual({ revision: 2 });
    const zeroDays = days(0);
    expect((await put(2, zeroDays)).status).toBe(200);
    const saved = await read();
    expect(saved.days).toHaveLength(7);
    for (const day of saved.days) {
      expect(day).toMatchObject({
        hasValue: true,
        steps: 0,
        hasLastKnownValue: true,
        lastKnownSteps: 0,
        lastKnownObservedAt: zeroDays[0].observedAt,
      });
    }
    expect(saved.days.map((day) => day.day)).toEqual(
      zeroDays.map((day) => day.day).reverse(),
    );
  });
});
