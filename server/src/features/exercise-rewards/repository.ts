import { and, desc, eq, inArray } from "drizzle-orm";
import { createDatabase } from "../../shared/db.js";
import {
  exerciseRewardDays,
  runeClaims,
  runeLedger,
  runeWallets,
} from "./db-schema.js";

const ACTIVITY_TYPE = "all";
const METRIC_TYPE = "steps";
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

  async function listDays(userId: string, sourceId: string) {
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
      .limit(7)
      .all();
  }

  async function findExistingClaim(userId: string, requestId: string) {
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
      .get();
  }

  async function claim(
    userId: string,
    sourceId: string,
    requestId: string,
    sourceDays: RewardSourceDay[],
    now: string,
  ) {
    const existing = await findExistingClaim(userId, requestId);
    if (existing) {
      return {
        grantedRunes: existing.grantedRunes,
        balance: existing.balanceAfter,
        days: await listDays(userId, existing.sourceId),
      };
    }

    const previous =
      sourceDays.length === 0
        ? []
        : await db
            .select()
            .from(exerciseRewardDays)
            .where(
              and(
                eq(exerciseRewardDays.userId, userId),
                eq(exerciseRewardDays.sourceId, sourceId),
                eq(exerciseRewardDays.activityType, ACTIVITY_TYPE),
                eq(exerciseRewardDays.metricType, METRIC_TYPE),
                inArray(
                  exerciseRewardDays.day,
                  sourceDays.map((day) => day.day),
                ),
              ),
            )
            .all();
    const previousByKey = new Map(
      previous.map((row) => [`${row.day}\u0000${row.zone}`, row]),
    );
    const claimId = crypto.randomUUID();
    const dayWrites = [];
    const ledgerWrites = [];
    let grantedRunes = 0;

    for (const sourceDay of sourceDays) {
      const key = `${sourceDay.day}\u0000${sourceDay.zone}`;
      const current = previousByKey.get(key);
      const previousValue = current?.creditedThroughValue ?? 0;
      const observedValue = sourceDay.hasValue ? sourceDay.steps : null;
      const nextValue = Math.max(previousValue, observedValue ?? 0);
      const delta = nextValue - previousValue;
      const rewardDayId = current?.id ?? crypto.randomUUID();
      grantedRunes += delta;
      dayWrites.push(
        db
          .insert(exerciseRewardDays)
          .values({
            id: rewardDayId,
            userId,
            sourceId,
            day: sourceDay.day,
            zone: sourceDay.zone,
            activityType: ACTIVITY_TYPE,
            metricType: METRIC_TYPE,
            hasValue: sourceDay.hasValue,
            observedValue,
            creditedThroughValue: nextValue,
            creditedRunes: (current?.creditedRunes ?? 0) + delta,
            ruleVersion: RULE_VERSION,
            lastObservedAt: sourceDay.hasValue
              ? sourceDay.observedAt
              : (current?.lastObservedAt ?? null),
            updatedAt: now,
          })
          .onConflictDoUpdate({
            target: [
              exerciseRewardDays.userId,
              exerciseRewardDays.sourceId,
              exerciseRewardDays.day,
              exerciseRewardDays.zone,
              exerciseRewardDays.activityType,
              exerciseRewardDays.metricType,
            ],
            set: {
              hasValue: sourceDay.hasValue,
              observedValue,
              creditedThroughValue: nextValue,
              creditedRunes: (current?.creditedRunes ?? 0) + delta,
              ruleVersion: RULE_VERSION,
              lastObservedAt: sourceDay.hasValue
                ? sourceDay.observedAt
                : (current?.lastObservedAt ?? null),
              updatedAt: now,
            },
          }),
      );
      if (delta > 0) {
        ledgerWrites.push(
          db.insert(runeLedger).values({
            id: crypto.randomUUID(),
            userId,
            claimId,
            rewardDayId,
            delta,
            reason: "exercise_steps",
            fromValue: previousValue,
            toValue: nextValue,
            ruleVersion: RULE_VERSION,
            createdAt: now,
          }),
        );
      }
    }

    const wallet = await db
      .select({ balance: runeWallets.balance })
      .from(runeWallets)
      .where(eq(runeWallets.userId, userId))
      .get();
    const balance = (wallet?.balance ?? 0) + grantedRunes;
    const statements = [
      db.insert(runeClaims).values({
        id: claimId,
        userId,
        sourceId,
        requestId,
        grantedRunes,
        balanceAfter: balance,
        createdAt: now,
      }),
      db
        .insert(runeWallets)
        .values({ userId, balance, updatedAt: now })
        .onConflictDoUpdate({
          target: runeWallets.userId,
          set: { balance, updatedAt: now },
        }),
      ...dayWrites,
      ...ledgerWrites,
    ];
    await db.batch(statements as unknown as Parameters<typeof db.batch>[0]);
    return { grantedRunes, balance, days: await listDays(userId, sourceId) };
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
