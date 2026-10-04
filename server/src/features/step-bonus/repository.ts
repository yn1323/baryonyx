import { and, asc, eq, inArray } from "drizzle-orm";
import { createDatabase } from "../../shared/db.js";
import {
  rankOrder,
  STARTER_HOLDINGS,
  STARTER_SLOTS,
  STEP_BONUS_SLOT_COUNT,
  type StepBonusId,
  type StepBonusRank,
} from "./catalog.js";
import {
  stepBonusHoldings,
  stepBonusProfiles,
  stepBonusSlots,
} from "./db-schema.js";

export type StepBonusChange = "set" | "swap" | "none";
export type StepBonusAcquired = "added" | "updated" | "discarded";

export function createStepBonusRepository(binding: D1Database) {
  const db = createDatabase(binding);

  async function readState(userId: string) {
    const holdings = await db
      .select({
        bonusId: stepBonusHoldings.bonusId,
        rank: stepBonusHoldings.rank,
        acquiredAt: stepBonusHoldings.acquiredAt,
        updatedAt: stepBonusHoldings.updatedAt,
      })
      .from(stepBonusHoldings)
      .where(eq(stepBonusHoldings.userId, userId))
      .orderBy(
        asc(stepBonusHoldings.acquiredAt),
        asc(stepBonusHoldings.bonusId),
      )
      .all();
    const rows = await db
      .select({ slot: stepBonusSlots.slot, bonusId: stepBonusSlots.bonusId })
      .from(stepBonusSlots)
      .where(eq(stepBonusSlots.userId, userId))
      .all();
    const slots = Array.from({ length: STEP_BONUS_SLOT_COUNT }, (_, slot) => ({
      slot,
      bonusId: rows.find((row) => row.slot === slot)?.bonusId ?? null,
    }));
    return { slots, holdings };
  }

  // 初めて読むユーザーにだけ、仮の初期ボーナスと枠の設定を1回だけ付与する。
  async function ensureStarter(userId: string, now: string) {
    const profile = await db
      .select({ userId: stepBonusProfiles.userId })
      .from(stepBonusProfiles)
      .where(eq(stepBonusProfiles.userId, userId))
      .get();
    if (profile) return;
    await db.batch([
      db
        .insert(stepBonusProfiles)
        .values({ userId, createdAt: now })
        .onConflictDoNothing(),
      db
        .insert(stepBonusHoldings)
        .values(
          STARTER_HOLDINGS.map((holding) => ({
            userId,
            bonusId: holding.bonusId,
            rank: holding.rank,
            acquiredAt: now,
            updatedAt: now,
          })),
        )
        .onConflictDoNothing(),
      db
        .insert(stepBonusSlots)
        .values(
          STARTER_SLOTS.map((bonusId, slot) => ({
            userId,
            slot,
            bonusId,
            updatedAt: now,
          })),
        )
        .onConflictDoNothing(),
    ]);
  }

  async function getState(userId: string, now: string) {
    await ensureStarter(userId, now);
    return readState(userId);
  }

  // 枠にボーナスを入れる。ほかの枠に入っていれば、2つの枠の中身を入れ替える。
  async function setSlot(
    userId: string,
    slot: number,
    bonusId: StepBonusId,
    now: string,
  ) {
    await ensureStarter(userId, now);
    const state = await readState(userId);
    if (!state.holdings.some((holding) => holding.bonusId === bonusId)) {
      return { error: "bonus_not_owned" as const };
    }
    const current = state.slots[slot].bonusId;
    if (current === bonusId) return { change: "none" as const, ...state };

    const from = state.slots.findIndex((entry) => entry.bonusId === bonusId);
    const touched = from >= 0 ? [slot, from] : [slot];
    const inserts: (typeof stepBonusSlots.$inferInsert)[] = [
      { userId, slot, bonusId, updatedAt: now },
    ];
    if (from >= 0 && current !== null) {
      inserts.push({ userId, slot: from, bonusId: current, updatedAt: now });
    }
    await db.batch([
      db
        .delete(stepBonusSlots)
        .where(
          and(
            eq(stepBonusSlots.userId, userId),
            inArray(stepBonusSlots.slot, touched),
          ),
        ),
      db.insert(stepBonusSlots).values(inserts),
    ]);
    const change: StepBonusChange = from >= 0 ? "swap" : "set";
    return { change, ...(await readState(userId)) };
  }

  // 冒険で手に入れたボーナスを持ち物へ入れる。同じボーナスはランクの高いほうだけを残す。
  async function acquire(
    userId: string,
    bonusId: StepBonusId,
    rank: StepBonusRank,
    now: string,
  ): Promise<StepBonusAcquired> {
    const existing = await db
      .select({ rank: stepBonusHoldings.rank })
      .from(stepBonusHoldings)
      .where(
        and(
          eq(stepBonusHoldings.userId, userId),
          eq(stepBonusHoldings.bonusId, bonusId),
        ),
      )
      .get();
    if (!existing) {
      await db
        .insert(stepBonusHoldings)
        .values({ userId, bonusId, rank, acquiredAt: now, updatedAt: now });
      return "added";
    }
    if (rankOrder(rank) <= rankOrder(existing.rank as StepBonusRank)) {
      return "discarded";
    }
    await db
      .update(stepBonusHoldings)
      .set({ rank, updatedAt: now })
      .where(
        and(
          eq(stepBonusHoldings.userId, userId),
          eq(stepBonusHoldings.bonusId, bonusId),
        ),
      );
    return "updated";
  }

  return { getState, setSlot, acquire };
}
