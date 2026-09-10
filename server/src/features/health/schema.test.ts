import { describe, expect, it } from "vitest";
import { days } from "./fixtures.js";
import { parseDays } from "./schema.js";

describe("日別歩数の検証", () => {
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
    expect(parseDays(values, Date.parse(observedAt))).not.toBeNull();
    values[3].endAt = "2026-03-09T05:00:00Z";
    expect(parseDays(values, Date.parse(observedAt))).toBeNull();
  });
  it("欠損とゼロを区別し、負値・重複・不正な日時とタイムゾーンを拒否する", () => {
    expect(parseDays(days(0))).not.toBeNull();
    expect(parseDays(days(-1))).toBeNull();
    expect(
      parseDays(days().map((d) => ({ ...d, hasValue: false }))),
    ).toBeNull();
    expect(
      parseDays(days().map((d) => ({ ...d, zone: "bad-zone" }))),
    ).toBeNull();
    expect(
      parseDays(days().map((d) => ({ ...d, day: "2026-13-50" }))),
    ).toBeNull();
    expect(parseDays(Array(7).fill(days()[0]))).toBeNull();
    expect(
      parseDays(days().map((d) => ({ ...d, startAt: "bad-date" }))),
    ).toBeNull();
  });
});
