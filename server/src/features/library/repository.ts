import { and, asc, desc, eq, max } from "drizzle-orm";
import { createDatabase } from "../../shared/db.js";
import { ACTIVITY_TYPE, METRIC_TYPE } from "../exercise-rewards/catalog.js";
import { exerciseRewardDays } from "../exercise-rewards/db-schema.js";
import { libraryDiscoveries } from "./db-schema.js";

export function createLibraryRepository(binding: D1Database) {
  const db = createDatabase(binding);

  // 見つけた記録を、見つけた古い順に返す。
  async function getState(userId: string) {
    const discoveries = await db
      .select({
        kind: libraryDiscoveries.kind,
        entryId: libraryDiscoveries.entryId,
        discoveredAt: libraryDiscoveries.discoveredAt,
        defeated: libraryDiscoveries.defeated,
      })
      .from(libraryDiscoveries)
      .where(eq(libraryDiscoveries.userId, userId))
      .orderBy(
        asc(libraryDiscoveries.discoveredAt),
        asc(libraryDiscoveries.kind),
        asc(libraryDiscoveries.entryId),
      )
      .all();
    return { discoveries };
  }

  // 日ごとの歩数を新しい日から返す。端末を複数使った日は、いちばん多い端末の値にする。
  async function getSteps(userId: string) {
    const steps = max(exerciseRewardDays.observedValue);
    const days = await db
      .select({ day: exerciseRewardDays.day, steps })
      .from(exerciseRewardDays)
      .where(
        and(
          eq(exerciseRewardDays.userId, userId),
          eq(exerciseRewardDays.activityType, ACTIVITY_TYPE),
          eq(exerciseRewardDays.metricType, METRIC_TYPE),
          eq(exerciseRewardDays.hasValue, true),
        ),
      )
      .groupBy(exerciseRewardDays.day)
      .orderBy(desc(exerciseRewardDays.day))
      .all();
    return {
      days: days.map((entry) => ({ day: entry.day, steps: entry.steps ?? 0 })),
    };
  }

  return { getState, getSteps };
}
