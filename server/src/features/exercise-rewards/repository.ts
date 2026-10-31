import { and, desc, eq, type SQL, sql } from "drizzle-orm";
import {
  batchThenRead,
  createDatabase,
  type Statement,
} from "../../shared/db.js";
import { ACTIVITY_TYPE, METRIC_TYPE } from "./catalog.js";
import {
  exerciseRewardDays,
  runeClaims,
  runeLedger,
  runeWallets,
} from "./db-schema.js";

const RULE_VERSION = "steps_v1";

type RewardSourceDay = {
  sourceId: string;
  day: string;
  zone: string;
  hasValue: boolean;
  steps: number | null;
  observedAt: string;
};

export function createExerciseRewardsRepository(binding: D1Database) {
  const db = createDatabase(binding);

  function listDays(userId: string, sourceId: string | SQL) {
    return db
      .select({
        id: exerciseRewardDays.id,
        day: exerciseRewardDays.day,
        zone: exerciseRewardDays.zone,
        activityType: exerciseRewardDays.activityType,
        metricType: exerciseRewardDays.metricType,
        hasValue: exerciseRewardDays.hasValue,
        observedValue: exerciseRewardDays.observedValue,
        creditedThroughValue: exerciseRewardDays.creditedThroughValue,
        creditedRunes: exerciseRewardDays.creditedRunes,
        ruleVersion: exerciseRewardDays.ruleVersion,
        lastObservedAt: exerciseRewardDays.lastObservedAt,
        updatedAt: exerciseRewardDays.updatedAt,
      })
      .from(exerciseRewardDays)
      .where(
        and(
          eq(exerciseRewardDays.userId, userId),
          eq(exerciseRewardDays.sourceId, sourceId),
        ),
      )
      .orderBy(desc(exerciseRewardDays.day), desc(exerciseRewardDays.zone))
      .limit(7);
  }

  function findExistingClaim(userId: string, requestId: string) {
    return db
      .select({
        id: runeClaims.id,
        sourceId: runeClaims.sourceId,
        grantedRunes: runeClaims.grantedRunes,
        balanceAfter: runeClaims.balanceAfter,
      })
      .from(runeClaims)
      .where(
        and(eq(runeClaims.userId, userId), eq(runeClaims.requestId, requestId)),
      )
      .limit(1);
  }

  async function claim(
    userId: string,
    sourceId: string,
    requestId: string,
    sourceDays: RewardSourceDay[],
    now: string,
  ) {
    // 書き込みは、この請求の記録（claimId）を書けたときだけ進む。同じ要求IDの再送では記録を書けず、
    // 何も変えずに前回の記録を読んで返す。確かめる読み込みを先に行わず、1回の往復で済ませる。
    const claimId = crypto.randomUUID();
    const claimOwner = sql`EXISTS (
      SELECT 1
      FROM ${runeClaims}
      WHERE ${runeClaims.id} = ${claimId}
        AND ${runeClaims.userId} = ${userId}
        AND ${runeClaims.requestId} = ${requestId}
    )`;
    const claimInsert = db
      .insert(runeClaims)
      .values({
        id: claimId,
        userId,
        sourceId,
        requestId,
        grantedRunes: 0,
        balanceAfter: 0,
        createdAt: now,
      })
      .onConflictDoNothing({ target: runeClaims.requestId });
    const initialDayWrites = [];
    const ledgerWrites = [];
    const dayWrites = [];

    for (const sourceDay of sourceDays) {
      const observedValue = sourceDay.hasValue ? sourceDay.steps : null;
      const rewardDayId = crypto.randomUUID();
      const ledgerId = crypto.randomUUID();
      const dayKey = and(
        eq(exerciseRewardDays.userId, userId),
        eq(exerciseRewardDays.sourceId, sourceId),
        eq(exerciseRewardDays.day, sourceDay.day),
        eq(exerciseRewardDays.zone, sourceDay.zone),
        eq(exerciseRewardDays.activityType, ACTIVITY_TYPE),
        eq(exerciseRewardDays.metricType, METRIC_TYPE),
      );

      initialDayWrites.push(
        db
          .insert(exerciseRewardDays)
          .select(
            db
              .select({
                id: sql<string>`${rewardDayId}`.as("id"),
                userId: sql<string>`${userId}`.as("user_id"),
                sourceId: sql<string>`${sourceId}`.as("source_id"),
                day: sql<string>`${sourceDay.day}`.as("day"),
                zone: sql<string>`${sourceDay.zone}`.as("zone"),
                activityType: sql<string>`${ACTIVITY_TYPE}`.as("activity_type"),
                metricType: sql<string>`${METRIC_TYPE}`.as("metric_type"),
                hasValue: sql<boolean>`0`.as("has_value"),
                observedValue: sql<number | null>`NULL`.as("observed_value"),
                creditedThroughValue: sql<number>`0`.as(
                  "credited_through_value",
                ),
                creditedRunes: sql<number>`0`.as("credited_runes"),
                ruleVersion: sql<string>`${RULE_VERSION}`.as("rule_version"),
                lastObservedAt: sql<string | null>`NULL`.as("last_observed_at"),
                updatedAt: sql<string>`${now}`.as("updated_at"),
              })
              .from(runeClaims)
              .where(
                and(
                  eq(runeClaims.id, claimId),
                  eq(runeClaims.userId, userId),
                  eq(runeClaims.requestId, requestId),
                ),
              ),
          )
          .onConflictDoNothing({
            target: [
              exerciseRewardDays.userId,
              exerciseRewardDays.sourceId,
              exerciseRewardDays.day,
              exerciseRewardDays.zone,
              exerciseRewardDays.activityType,
              exerciseRewardDays.metricType,
            ],
          }),
      );

      if (observedValue !== null) {
        ledgerWrites.push(
          db.insert(runeLedger).select(
            db
              .select({
                id: sql<string>`${ledgerId}`.as("id"),
                userId: exerciseRewardDays.userId,
                claimId: sql<string>`${claimId}`.as("claim_id"),
                rewardDayId: exerciseRewardDays.id,
                delta:
                  sql<number>`${observedValue} - ${exerciseRewardDays.creditedThroughValue}`.as(
                    "delta",
                  ),
                reason: sql<string>`'exercise_steps'`.as("reason"),
                fromValue: exerciseRewardDays.creditedThroughValue,
                toValue: sql<number>`${observedValue}`.as("to_value"),
                ruleVersion: sql<string>`${RULE_VERSION}`.as("rule_version"),
                createdAt: sql<string>`${now}`.as("created_at"),
              })
              .from(exerciseRewardDays)
              .where(
                and(
                  dayKey,
                  sql`${exerciseRewardDays.creditedThroughValue} < ${observedValue}`,
                  claimOwner,
                ),
              ),
          ),
        );
      }

      dayWrites.push(
        db
          .update(exerciseRewardDays)
          .set({
            hasValue: sourceDay.hasValue,
            observedValue,
            creditedThroughValue:
              observedValue === null
                ? exerciseRewardDays.creditedThroughValue
                : sql`max(${exerciseRewardDays.creditedThroughValue}, ${observedValue})`,
            creditedRunes:
              observedValue === null
                ? exerciseRewardDays.creditedRunes
                : sql`${exerciseRewardDays.creditedRunes} + max(0, ${observedValue} - ${exerciseRewardDays.creditedThroughValue})`,
            ruleVersion: RULE_VERSION,
            lastObservedAt: sourceDay.hasValue
              ? sourceDay.observedAt
              : exerciseRewardDays.lastObservedAt,
            updatedAt: now,
          })
          .where(and(dayKey, claimOwner)),
      );
    }

    const statements = [
      claimInsert,
      ...initialDayWrites,
      ...ledgerWrites,
      ...dayWrites,
      db
        .insert(runeWallets)
        .values({ userId, balance: 0, updatedAt: now })
        .onConflictDoNothing({ target: runeWallets.userId }),
      db
        .update(runeWallets)
        .set({
          balance: sql`${runeWallets.balance} + COALESCE(
            (SELECT SUM(${runeLedger.delta})
             FROM ${runeLedger}
             WHERE ${runeLedger.claimId} = ${claimId}),
            0
          )`,
          updatedAt: now,
        })
        .where(and(eq(runeWallets.userId, userId), claimOwner)),
      db
        .update(runeClaims)
        .set({
          grantedRunes: sql`COALESCE(
            (SELECT SUM(${runeLedger.delta})
             FROM ${runeLedger}
             WHERE ${runeLedger.claimId} = ${claimId}),
            0
          )`,
          balanceAfter: sql`COALESCE(
            (SELECT ${runeWallets.balance}
             FROM ${runeWallets}
             WHERE ${runeWallets.userId} = ${userId}),
            0
          )`,
        })
        .where(
          and(
            eq(runeClaims.id, claimId),
            eq(runeClaims.userId, userId),
            eq(runeClaims.requestId, requestId),
          ),
        ),
    ];
    // 再送では、前回の請求の取得元の日ごとの記録を返す。
    const claimedSource = sql`(SELECT ${runeClaims.sourceId} FROM ${runeClaims}
      WHERE ${runeClaims.userId} = ${userId} AND ${runeClaims.requestId} = ${requestId})`;
    const {
      reads: [[completed], days],
    } = await batchThenRead(
      db,
      statements as unknown as [Statement, ...Statement[]],
      [findExistingClaim(userId, requestId), listDays(userId, claimedSource)],
    );
    if (!completed) throw new Error("Rune claim reservation was not created");
    return {
      grantedRunes: completed.grantedRunes,
      balance: completed.balanceAfter,
      days,
    };
  }

  async function getBalance(userId: string) {
    const wallet = await db
      .select({
        balance: runeWallets.balance,
        updatedAt: runeWallets.updatedAt,
      })
      .from(runeWallets)
      .where(eq(runeWallets.userId, userId))
      .get();
    return wallet ?? { balance: 0, updatedAt: null };
  }

  return { claim, findExistingClaim, getBalance, listDays };
}
