import { describe, expect, it } from "vitest";
import { days } from "./fixtures.js";
import {
  beginSyncSchema,
  createDaysSchema,
  createSaveDaysSchema,
} from "./schema.js";

describe("日別歩数の検証", () => {
  it.each([
    [
      "取得区間の隙間",
      (values: ReturnType<typeof days>) => {
        values[0].endAt = new Date(
          Date.parse(values[0].endAt) - 1,
        ).toISOString();
      },
    ],
    [
      "異なるタイムゾーン名",
      (values: ReturnType<typeof days>) => {
        values[1].zone = "Etc/UTC";
      },
    ],
    [
      "日付順の逆転",
      (values: ReturnType<typeof days>) => {
        values.reverse();
      },
    ],
    [
      "取得日時より後の終了",
      (values: ReturnType<typeof days>) => {
        values[6].endAt = new Date(
          Date.parse(values[6].observedAt) + 1,
        ).toISOString();
      },
    ],
  ])("単日と週全体の制約を維持する: %s", (_, change) => {
    const values = days();
    change(values);
    expect(createDaysSchema().safeParse(values).success).toBe(false);
  });

  it("夏時間終了による25時間の日とUTC日時の表記差を受け入れる", () => {
    const starts = [
      "2026-10-29T04:00:00Z",
      "2026-10-30T04:00:00Z",
      "2026-10-31T04:00:00Z",
      "2026-11-01T04:00:00Z",
      "2026-11-02T05:00:00Z",
      "2026-11-03T05:00:00Z",
      "2026-11-04T05:00:00Z",
    ];
    const observedAt = "2026-11-04T16:00:00Z";
    const values = starts.map((startAt, index) => ({
      day: startAt.slice(0, 10),
      zone: "America/New_York",
      startAt,
      endAt: new Date(starts[index + 1] ?? observedAt).toISOString(),
      observedAt,
      hasValue: true,
      steps: 0,
    }));
    expect(
      createDaysSchema(Date.parse(observedAt)).safeParse(values).success,
    ).toBe(true);
  });

  it("夏時間による23時間の日を受け入れ、期間の重なりを拒否する", () => {
    const starts = [
      "2026-03-05T05:00:00Z",
      "2026-03-06T05:00:00Z",
      "2026-03-07T05:00:00Z",
      "2026-03-08T05:00:00Z",
      "2026-03-09T04:00:00Z",
      "2026-03-10T04:00:00Z",
      "2026-03-11T04:00:00Z",
    ];
    const observedAt = "2026-03-11T16:00:00Z";
    const values = starts.map((startAt, index) => ({
      day: startAt.slice(0, 10),
      zone: "America/New_York",
      startAt,
      endAt: starts[index + 1] ?? observedAt,
      observedAt,
      hasValue: true,
      steps: 0,
    }));
    const schema = createDaysSchema(Date.parse(observedAt));
    expect(schema.safeParse(values).success).toBe(true);
    values[3].endAt = "2026-03-09T05:00:00Z";
    expect(schema.safeParse(values).success).toBe(false);
  });
  it("欠損とゼロを区別し、負値・重複・不正な日時とタイムゾーンを拒否する", () => {
    const schema = createDaysSchema();
    expect(schema.safeParse(days(0)).success).toBe(true);
    expect(schema.safeParse(days(-1)).success).toBe(false);
    expect(
      schema.safeParse(days().map((d) => ({ ...d, hasValue: false }))).success,
    ).toBe(false);
    expect(
      schema.safeParse(days().map((d) => ({ ...d, zone: "bad-zone" }))).success,
    ).toBe(false);
    expect(
      schema.safeParse(days().map((d) => ({ ...d, day: "2026-13-50" })))
        .success,
    ).toBe(false);
    expect(schema.safeParse(Array(7).fill(days()[0])).success).toBe(false);
    expect(
      schema.safeParse(days().map((d) => ({ ...d, startAt: "bad-date" })))
        .success,
    ).toBe(false);
  });

  it.each([null, {}, [], Array(7).fill(null)])(
    "日別データの構造が不正な場合は拒否する: %j",
    (value) => {
      expect(createDaysSchema().safeParse(value).success).toBe(false);
    },
  );

  it.each([
    { steps: "123" },
    { steps: 1.5 },
    { steps: 1_000_001 },
    { hasValue: "true" },
    { observedAt: "2026-09-11T12:00:00+00:00" },
  ])("日別データの型と上限を検証する: %j", (change) => {
    const values = days().map((day) => ({ ...day, ...change }));
    expect(createDaysSchema().safeParse(values).success).toBe(false);
  });

  it("歩数の上限と欠損値を受け入れ、6日分と8日分を拒否する", () => {
    const schema = createDaysSchema();
    expect(schema.safeParse(days(1_000_000)).success).toBe(true);
    expect(
      schema.safeParse(days(0).map((day) => ({ ...day, hasValue: false })))
        .success,
    ).toBe(true);
    expect(schema.safeParse(days().slice(1)).success).toBe(false);
    expect(schema.safeParse([...days(), days()[0]]).success).toBe(false);
  });

  it("検証時刻を基準に古い期間と未来の取得日時を拒否する", () => {
    const values = days();
    const now = Date.parse(values[0].observedAt);
    expect(createDaysSchema(now).safeParse(values).success).toBe(true);
    expect(
      createDaysSchema(now + 10 * 86_400_000).safeParse(values).success,
    ).toBe(false);
    expect(createDaysSchema(now - 300_001).safeParse(values).success).toBe(
      false,
    );
  });
});

describe("同期要求の検証", () => {
  it("取得元IDとProviderを検証し、余分なプロパティを取り除く", () => {
    const sourceId = "aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa";
    for (const provider of ["health_connect", "healthkit"]) {
      expect(
        beginSyncSchema.parse({ sourceId, provider, extra: true }),
      ).toEqual({
        sourceId,
        provider,
      });
    }
    expect(
      beginSyncSchema.safeParse({ sourceId, provider: "other" }).success,
    ).toBe(false);
    expect(
      beginSyncSchema.safeParse({ sourceId: 123, provider: "healthkit" })
        .success,
    ).toBe(false);
    expect(
      beginSyncSchema.safeParse({ sourceId: "bad-id", provider: "healthkit" })
        .success,
    ).toBe(false);
  });

  it("更新版を正の安全な整数に制限する", () => {
    const schema = createSaveDaysSchema();
    for (const revision of [1, Number.MAX_SAFE_INTEGER]) {
      expect(schema.safeParse({ revision, days: days() }).success).toBe(true);
    }
    for (const revision of [
      undefined,
      "1",
      0,
      -1,
      1.5,
      Number.MAX_SAFE_INTEGER + 1,
    ]) {
      expect(schema.safeParse({ revision, days: days() }).success).toBe(false);
    }
  });
});
