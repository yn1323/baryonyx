import { z } from "zod";

export const sourceIdSchema = z.string().regex(/^[a-f0-9-]{36}$/);

export const googleAuthSchema = z.object({
  idToken: z.string().max(12_000),
});

export const beginSyncSchema = z.object({
  sourceId: sourceIdSchema,
  provider: z.enum(["health_connect", "healthkit"]),
});

const timestampSchema = z
  .string()
  .regex(/^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d{1,7})?Z$/)
  .refine((value) => Number.isFinite(Date.parse(value)));

const healthDaySchema = z.object({
  day: z.string().regex(/^\d{4}-\d{2}-\d{2}$/),
  zone: z.string().max(80),
  startAt: timestampSchema,
  endAt: timestampSchema,
  hasValue: z.boolean(),
  steps: z.number().int().min(0).max(1_000_000),
  observedAt: timestampSchema,
});

export type HealthDay = z.infer<typeof healthDaySchema>;

function validDay(day: HealthDay, now: number): boolean {
  if (!day.hasValue && day.steps !== 0) return false;
  const [start, end, observed] = [day.startAt, day.endAt, day.observedAt].map(
    Date.parse,
  );
  if (
    ![start, end, observed].every(Number.isFinite) ||
    start >= end ||
    end > observed ||
    observed > now + 300_000 ||
    start < now - 9 * 86_400_000 ||
    end - start > 26 * 3_600_000
  )
    return false;
  return startsAtLocalMidnight(day, start);
}

function startsAtLocalMidnight(day: HealthDay, start: number): boolean {
  try {
    const parts = new Intl.DateTimeFormat("en-CA", {
      timeZone: day.zone,
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
      hourCycle: "h23",
    }).formatToParts(new Date(start));
    const part = (key: string) => parts.find((p) => p.type === key)?.value;
    return (
      `${part("year")}-${part("month")}-${part("day")}` === day.day &&
      part("hour") === "00" &&
      part("minute") === "00" &&
      part("second") === "00"
    );
  } catch {
    return false;
  }
}

function validWeek(value: HealthDay[]): boolean {
  const keys = new Set<string>();
  let zone: string | undefined;
  let previousEnd: number | undefined;
  for (const day of value) {
    if (zone !== undefined && zone !== day.zone) return false;
    zone = day.zone;
    const start = Date.parse(day.startAt);
    const end = Date.parse(day.endAt);
    if (previousEnd !== undefined && previousEnd !== start) return false;
    previousEnd = end;
    if (keys.has(day.day)) return false;
    keys.add(day.day);
  }
  return true;
}

export function createDaysSchema(now = Date.now()) {
  return z
    .array(healthDaySchema.refine((day) => validDay(day, now)))
    .length(7)
    .refine(validWeek);
}

export function createSaveDaysSchema(now = Date.now()) {
  return z.object({
    revision: z.number().int().min(1),
    days: createDaysSchema(now),
  });
}
