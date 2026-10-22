import { and, desc, eq, gte, lt, sql } from "drizzle-orm";
import { createDatabase } from "../../shared/db.js";
import { healthDays, healthSources } from "./db-schema.js";
import type { HealthDay } from "./schema.js";

export function createHealthRepository(binding: D1Database) {
  const db = createDatabase(binding);

  return {
    beginSync(
      sourceId: string,
      userId: string,
      provider: typeof healthSources.$inferInsert.provider,
    ) {
      // 所有者・Providerの確認と版の更新を一つのSQLで行う。
      return db
        .insert(healthSources)
        .values({ id: sourceId, userId, provider, revision: 1 })
        .onConflictDoUpdate({
          target: healthSources.id,
          set: { revision: sql`${healthSources.revision} + 1` },
          setWhere: and(
            eq(healthSources.userId, userId),
            eq(healthSources.provider, provider),
          ),
        })
        .returning({ revision: healthSources.revision })
        .get();
    },

    async saveDays(
      sourceId: string,
      userId: string,
      revision: number,
      days: HealthDay[],
      receivedAt: string,
    ) {
      const statements = days.map((day) => {
        const steps = day.hasValue ? day.steps : null;
        const lastKnownObservedAt = day.hasValue ? day.observedAt : null;
        // INSERT SELECT内で所有者と最新版を確認し、確認と書き込みの競合を防ぐ。
        return db
          .insert(healthDays)
          .select(
            db
              .select({
                sourceId: healthSources.id,
                day: sql<string>`${day.day}`.as("day"),
                zone: sql<string>`${day.zone}`.as("zone"),
                startAt: sql<string>`${day.startAt}`.as("start_at"),
                endAt: sql<string>`${day.endAt}`.as("end_at"),
                hasValue: sql<boolean>`${day.hasValue ? 1 : 0}`.as("has_value"),
                steps: sql<number | null>`${steps}`.as("steps"),
                lastKnownSteps: sql<number | null>`${steps}`.as(
                  "last_known_steps",
                ),
                observedAt: sql<string>`${day.observedAt}`.as("observed_at"),
                receivedAt: sql<string>`${receivedAt}`.as("received_at"),
                revision: healthSources.revision,
                lastKnownObservedAt: sql<
                  string | null
                >`${lastKnownObservedAt}`.as("last_known_observed_at"),
              })
              .from(healthSources)
              .where(
                and(
                  eq(healthSources.id, sourceId),
                  eq(healthSources.userId, userId),
                  eq(healthSources.revision, revision),
                ),
              ),
          )
          .onConflictDoUpdate({
            target: [healthDays.sourceId, healthDays.day, healthDays.zone],
            set: {
              startAt: day.startAt,
              endAt: day.endAt,
              hasValue: day.hasValue,
              steps,
              lastKnownSteps: sql`coalesce(${steps}, ${healthDays.lastKnownSteps})`,
              observedAt: day.observedAt,
              receivedAt,
              revision,
              lastKnownObservedAt: sql`coalesce(${lastKnownObservedAt}, ${healthDays.lastKnownObservedAt})`,
            },
            setWhere: lt(healthDays.revision, revision),
          });
      });
      const [first, ...rest] = statements;
      if (!first) return false;
      const results = await db.batch([first, ...rest]);
      return results.some((result) => result.meta.changes > 0);
    },

    findSource(sourceId: string, userId: string) {
      return db
        .select({
          provider: healthSources.provider,
          revision: healthSources.revision,
        })
        .from(healthSources)
        .where(
          and(eq(healthSources.id, sourceId), eq(healthSources.userId, userId)),
        )
        .get();
    },

    // ユーザーの今日（朝4時区切りのゲームの1日）の歩数。日ごとの記録は、端末の地域のその日の朝4時（startAt）から始まる。
    // 今日の記録は startAt から24時間のうちにある（夏時間で25時間になる日の最後の1時間は数えない）。
    // 今日の記録がなければ0歩とする。取得元が複数あれば多いほうを使い、歩数を読めなかった日は最後に読めた歩数を使う。
    async todaySteps(userId: string, now: string) {
      const at = Date.parse(now);
      const since = new Date(at - 2 * 86_400_000).toISOString().slice(0, 10);
      const rows = await db
        .select({
          startAt: healthDays.startAt,
          steps: healthDays.steps,
          lastKnownSteps: healthDays.lastKnownSteps,
        })
        .from(healthDays)
        .innerJoin(healthSources, eq(healthSources.id, healthDays.sourceId))
        .where(
          and(eq(healthSources.userId, userId), gte(healthDays.day, since)),
        )
        .all();
      return rows
        .filter((row) => {
          const start = Date.parse(row.startAt);
          return start <= at && at < start + 86_400_000;
        })
        .reduce(
          (best, row) => Math.max(best, row.steps ?? row.lastKnownSteps ?? 0),
          0,
        );
    },

    listDays(sourceId: string) {
      return db
        .select({
          day: healthDays.day,
          zone: healthDays.zone,
          startAt: healthDays.startAt,
          endAt: healthDays.endAt,
          hasValue: healthDays.hasValue,
          steps: healthDays.steps,
          lastKnownSteps: healthDays.lastKnownSteps,
          lastKnownObservedAt: healthDays.lastKnownObservedAt,
          observedAt: healthDays.observedAt,
          receivedAt: healthDays.receivedAt,
          revision: healthDays.revision,
        })
        .from(healthDays)
        .where(eq(healthDays.sourceId, sourceId))
        .orderBy(desc(healthDays.day))
        .limit(100)
        .all();
    },
  };
}
