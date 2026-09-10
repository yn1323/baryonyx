export interface HealthDay {
  day: string;
  zone: string;
  startAt: string;
  endAt: string;
  hasValue: boolean;
  steps: number;
  observedAt: string;
}

export const validId = (value: unknown): value is string =>
  typeof value === "string" && /^[a-f0-9-]{36}$/.test(value);

export function parseDays(
  value: unknown,
  now = Date.now(),
): HealthDay[] | null {
  if (!Array.isArray(value) || value.length !== 7) return null;
  const keys = new Set<string>();
  let zone: string | undefined;
  let previousEnd: number | undefined;
  for (const day of value) {
    if (!day || typeof day !== "object") return null;
    if (typeof day.day !== "string" || !/^\d{4}-\d{2}-\d{2}$/.test(day.day))
      return null;
    if (
      typeof day.zone !== "string" ||
      day.zone.length > 80 ||
      (zone && zone !== day.zone)
    )
      return null;
    zone = day.zone;
    if (
      typeof day.hasValue !== "boolean" ||
      !Number.isSafeInteger(day.steps) ||
      day.steps < 0 ||
      day.steps > 1_000_000
    )
      return null;
    if (!day.hasValue && day.steps !== 0) return null;
    const times = [day.startAt, day.endAt, day.observedAt];
    if (
      times.some(
        (time) =>
          typeof time !== "string" ||
          !/^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d{1,7})?Z$/.test(time),
      )
    )
      return null;
    const [start, end, observed] = times.map(Date.parse);
    if (
      ![start, end, observed].every(Number.isFinite) ||
      start >= end ||
      end > observed ||
      observed > now + 300_000 ||
      start < now - 9 * 86_400_000 ||
      end - start > 26 * 3_600_000
    )
      return null;
    if (previousEnd !== undefined && previousEnd !== start) return null;
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
        return null;
    } catch {
      return null;
    }
    if (keys.has(day.day)) return null;
    keys.add(day.day);
  }
  return value as HealthDay[];
}
