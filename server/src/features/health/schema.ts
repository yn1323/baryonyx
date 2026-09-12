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

function validDays(value: HealthDay[], now: number): boolean {
  const keys = new Set<string>();
  let zone: string | undefined;
  let previousEnd: number | undefined;
  for (const day of value) {
    if (zone !== undefined && zone !== day.zone) return false;
    zone = day.zone;
    if (!day.hasValue && day.steps !== 0) return false;
    const times = [day.startAt, day.endAt, day.observedAt];
    const [start, end, observed] = times.map(Date.parse);
    if (
      ![start, end, observed].every(Number.isFinite) ||
      start >= end ||
      end > observed ||
      observed > now + 300_000 ||
      start < now - 9 * 86_400_000 ||
      end - start > 26 * 3_600_000
    )
      return false;
    if (previousEnd !== undefined && previousEnd !== start) return false;
    previousEnd = end;
    try {
      const parts = new Intl.DateTimeFormat("en-CA", {
        timeZone: zone,
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
        hourCycle: "h23",
      }).formatToParts(new Date(start));
      const part = (key: string) => parts.find((p) => p.type === key)?.value;
      if (
        `${part("year")}-${part("month")}-${part("day")}` !== day.day ||
        part("hour") !== "00" ||
        part("minute") !== "00" ||
        part("second") !== "00"
      )
        return false;
    } catch {
      return false;
    }
    if (keys.has(day.day)) return false;
    keys.add(day.day);
  }
  return true;
}

export function createDaysSchema(now = Date.now()) {
  return z
    .array(healthDaySchema)
    .length(7)
    .refine((value) => validDays(value, now));
}

export function createSaveDaysSchema(now = Date.now()) {
  return z.object({
    revision: z.number().int().min(1),
    days: createDaysSchema(now),
  });
}
