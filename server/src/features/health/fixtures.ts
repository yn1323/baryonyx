import { DAY_START_HOUR } from "./schema.js";

export function days(steps = 123) {
  const now = new Date();
  // UTCの朝4時で日を区切る。4時より前は前日の続きとして扱う。
  const boundary = new Date(now.toISOString().slice(0, 10));
  boundary.setUTCHours(DAY_START_HOUR);
  if (boundary > now) boundary.setUTCDate(boundary.getUTCDate() - 1);
  return Array.from({ length: 7 }, (_, index) => {
    const start = new Date(boundary.getTime() - (6 - index) * 86_400_000);
    const end = index === 6 ? now : new Date(start.getTime() + 86_400_000);
    return {
      day: start.toISOString().slice(0, 10),
      zone: "UTC",
      startAt: start.toISOString(),
      endAt: end.toISOString(),
      hasValue: true,
      steps,
      observedAt: now.toISOString(),
    };
  });
}
