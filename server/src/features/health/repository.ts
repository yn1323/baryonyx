import { and, desc, eq, gt, lt, lte, sql } from "drizzle-orm";
import { createDatabase } from "../../shared/db.js";
import {
  appSessions,
  appUsers,
  healthDays,
  healthSources,
} from "./db-schema.js";
import type { HealthDay } from "./schema.js";

export function createHealthRepository(binding: D1Database) {
  const db = createDatabase(binding);

  return {
    async findOrCreateUser(googleSub: string) {
      await db
        .insert(appUsers)
        .values({ id: crypto.randomUUID(), googleSub })
        .onConflictDoNothing({ target: appUsers.googleSub });
      const user = await db
        .select({ id: appUsers.id })
        .from(appUsers)
        .where(eq(appUsers.googleSub, googleSub))
        .get();
      if (!user) throw new Error("User was not found after insert");
      return user.id;
    },

    async createSession(tokenHash: string, userId: string, expiresAt: number) {
      await db.batch([
        db.delete(appSessions).where(lte(appSessions.expiresAt, Date.now())),
        db.insert(appSessions).values({ tokenHash, userId, expiresAt }),
      ]);
    },

    async findSessionUser(tokenHash: string) {
      const session = await db
        .select({ userId: appSessions.userId })
        .from(appSessions)
        .where(
          and(
            eq(appSessions.tokenHash, tokenHash),
            gt(appSessions.expiresAt, Date.now()),
          ),
        )
        .get();
      return session?.userId;
    },

    async deleteSession(tokenHash: string) {
      await db.delete(appSessions).where(eq(appSessions.tokenHash, tokenHash));
    },

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
