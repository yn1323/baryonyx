import { and, eq, sql } from "drizzle-orm";
import { createDatabase } from "../../shared/db.js";
import { runeWallets } from "../exercise-rewards/db-schema.js";
import {
  rankOrder,
  STEP_BONUS_IDS,
  type StepBonusId,
  type StepBonusRank,
} from "../step-bonus/catalog.js";
import { stepBonusHoldings } from "../step-bonus/db-schema.js";
import { createStepBonusRepository } from "../step-bonus/repository.js";
import {
  type Destination,
  findDestination,
  findRoom,
  hasEvent,
  isBattle,
  REWARD_RANK_WEIGHTS,
  type Room,
  reviveCost,
  startRoom,
} from "./catalog.js";
import {
  adventureRecords,
  adventureRevives,
  adventureRewards,
  adventureRuns,
} from "./db-schema.js";
import type { EndReason, Revive } from "./schema.js";

type RunRow = typeof adventureRuns.$inferSelect;
type RewardRow = typeof adventureRewards.$inferSelect;
type EndStatus = "cleared" | "defeated" | "retreated";

// 0以上1未満の乱数。テストでは固定の値に差し替える。
export type Random = () => number;

export function createAdventureRepository(
  binding: D1Database,
  random: Random = Math.random,
) {
  const db = createDatabase(binding);

  function activeRun(userId: string) {
    return db
      .select()
      .from(adventureRuns)
      .where(
        and(
          eq(adventureRuns.userId, userId),
          eq(adventureRuns.status, "active"),
        ),
      )
      .get();
  }

  function rewardsOf(runId: string) {
    return db
      .select()
      .from(adventureRewards)
      .where(eq(adventureRewards.runId, runId))
      .orderBy(adventureRewards.createdAt);
  }

  const route = (run: RunRow) => JSON.parse(run.route) as string[];

  const reward = (row: RewardRow) => ({
    roomId: row.roomId,
    bonusId: row.bonusId,
    rank: row.rank,
    outcome: row.outcome,
  });

  // 進行中の冒険（行き先の部屋の構成と、この冒険で手に入れた物を含む）、行き先ごとの記録、所持ルーン。
  async function readState(userId: string) {
    const [runs, records, wallets] = await db.batch([
      db
        .select()
        .from(adventureRuns)
        .where(
          and(
            eq(adventureRuns.userId, userId),
            eq(adventureRuns.status, "active"),
          ),
        ),
      db
        .select({
          destinationId: adventureRecords.destinationId,
          bestFloor: adventureRecords.bestFloor,
          clears: adventureRecords.clears,
        })
        .from(adventureRecords)
        .where(eq(adventureRecords.userId, userId))
        .orderBy(adventureRecords.destinationId),
      db
        .select({ balance: runeWallets.balance })
        .from(runeWallets)
        .where(eq(runeWallets.userId, userId)),
    ]);
    const run = runs[0];
    const destination = run ? findDestination(run.destinationId) : undefined;
    return {
      run:
        run && destination
          ? {
              id: run.id,
              destinationId: run.destinationId,
              roomId: run.roomId,
              roomCleared: run.roomCleared,
              route: route(run),
              revives: run.revives,
              reviveCost: reviveCost(run.revives),
              rooms: destination.rooms.map((room) => ({
                ...room,
                next: [...room.next],
              })),
              rewards: (await rewardsOf(run.id)).map(reward),
            }
          : null,
      records,
      runes: wallets[0]?.balance ?? 0,
    };
  }

  async function getState(userId: string) {
    return readState(userId);
  }

  // 冒険先の入口の部屋から冒険を始める。進行中の冒険があるあいだは始められない。
  async function start(userId: string, destinationId: string, now: string) {
    const destination = findDestination(destinationId);
    if (!destination) return { error: "unknown_destination" as const };
    const current = await activeRun(userId);
    if (current) {
      // 届かなかった応答の再送は、始めたばかりの同じ冒険を返す。
      if (
        current.destinationId === destinationId &&
        route(current).length === 1
      ) {
        return readState(userId);
      }
      return { error: "adventure_in_progress" as const };
    }
    const entrance = startRoom(destination);
    await db
      .insert(adventureRuns)
      .values({
        id: crypto.randomUUID(),
        userId,
        destinationId,
        roomId: entrance.id,
        roomCleared: !hasEvent(entrance),
        route: JSON.stringify([entrance.id]),
        revives: 0,
        status: "active",
        startedAt: now,
        updatedAt: now,
      })
      .onConflictDoNothing();
    const started = await activeRun(userId);
    if (!started || started.destinationId !== destinationId) {
      return { error: "adventure_in_progress" as const };
    }
    return readState(userId);
  }

  // 今いる部屋の入口から、次の部屋へ進む。選んだ時点で保存する。
  async function move(userId: string, roomId: string, now: string) {
    const run = await activeRun(userId);
    if (!run) return { error: "no_adventure" as const };
    // 届かなかった応答の再送は、進んだあとの状態を返す。
    if (run.roomId === roomId) return readState(userId);
    const destination = findDestination(run.destinationId) as Destination;
    const current = findRoom(destination, run.roomId);
    const target = findRoom(destination, roomId);
    if (!current || !target || !current.next.includes(roomId)) {
      return { error: "room_not_reachable" as const };
    }
    if (!run.roomCleared) return { error: "room_not_cleared" as const };
    const moved = await db
      .update(adventureRuns)
      .set({
        roomId,
        roomCleared: !hasEvent(target),
        route: JSON.stringify([...route(run), roomId]),
        updatedAt: now,
      })
      .where(
        and(
          eq(adventureRuns.id, run.id),
          eq(adventureRuns.status, "active"),
          eq(adventureRuns.roomId, run.roomId),
          eq(adventureRuns.roomCleared, true),
        ),
      )
      .returning({ id: adventureRuns.id });
    if (moved.length === 0) return { error: "adventure_changed" as const };
    return readState(userId);
  }

  function pickRank(room: Room): StepBonusRank {
    const weights = Object.entries(
      REWARD_RANK_WEIGHTS[room.kind as keyof typeof REWARD_RANK_WEIGHTS],
    ) as [StepBonusRank, number][];
    const total = weights.reduce((sum, [, weight]) => sum + weight, 0);
    let roll = random() * total;
    for (const [rank, weight] of weights) {
      if (roll < weight) return rank;
      roll -= weight;
    }
    return weights[weights.length - 1][0];
  }

  function pickBonus(): StepBonusId {
    const index = Math.min(
      STEP_BONUS_IDS.length - 1,
      Math.floor(random() * STEP_BONUS_IDS.length),
    );
    return STEP_BONUS_IDS[index];
  }

  function floorOf(run: RunRow) {
    const destination = findDestination(run.destinationId) as Destination;
    return Math.max(
      ...route(run).map((id) => findRoom(destination, id)?.floor ?? 1),
    );
  }

  // 冒険を終えた結果。最深到達の階と記録の更新、この冒険で手に入れた物。
  async function result(run: RunRow, previousBest: number) {
    const floor = floorOf(run);
    const record = await db
      .select()
      .from(adventureRecords)
      .where(
        and(
          eq(adventureRecords.userId, run.userId),
          eq(adventureRecords.destinationId, run.destinationId),
        ),
      )
      .get();
    return {
      runId: run.id,
      destinationId: run.destinationId,
      status: run.status,
      floor,
      bestFloor: record?.bestFloor ?? floor,
      newRecord: floor > previousBest,
      clears: record?.clears ?? 0,
      revives: run.revives,
      rewards: (await rewardsOf(run.id)).map(reward),
    };
  }

  async function bestFloor(userId: string, destinationId: string) {
    const record = await db
      .select({ bestFloor: adventureRecords.bestFloor })
      .from(adventureRecords)
      .where(
        and(
          eq(adventureRecords.userId, userId),
          eq(adventureRecords.destinationId, destinationId),
        ),
      )
      .get();
    return record?.bestFloor ?? 0;
  }

  // 冒険の記録を、終えた冒険が条件どおり書き込まれたときだけ更新する文。
  function recordStatement(
    run: RunRow,
    status: EndStatus,
    now: string,
    written: ReturnType<typeof sql>,
  ) {
    const floor = floorOf(run);
    const clears = status === "cleared" ? 1 : 0;
    return db
      .insert(adventureRecords)
      .select(
        db
          .select({
            userId: sql<string>`${run.userId}`.as("user_id"),
            destinationId: sql<string>`${run.destinationId}`.as(
              "destination_id",
            ),
            bestFloor: sql<number>`${floor}`.as("best_floor"),
            clears: sql<number>`${clears}`.as("clears"),
            updatedAt: sql<string>`${now}`.as("updated_at"),
          })
          .from(sql`(SELECT 1)`)
          .where(written),
      )
      .onConflictDoUpdate({
        target: [adventureRecords.userId, adventureRecords.destinationId],
        set: {
          bestFloor: sql`MAX(${adventureRecords.bestFloor}, excluded.best_floor)`,
          clears: sql`${adventureRecords.clears} + excluded.clears`,
          updatedAt: sql`excluded.updated_at`,
        },
      });
  }

  // 今いる部屋の出来事（戦闘の勝利・宝箱）を終え、UPTボーナスを1つ手に入れる。
  // 最奥のボスを倒したら冒険を終える。部屋の終了・報酬の記録・持ち物・記録を1回のbatchで行う。
  async function clear(userId: string, roomId: string, now: string) {
    const run = await activeRun(userId);
    if (!run) return { error: "no_adventure" as const };
    const destination = findDestination(run.destinationId) as Destination;
    const room = findRoom(destination, run.roomId);
    if (run.roomId !== roomId || !room || !hasEvent(room)) {
      return { error: "room_mismatch" as const };
    }
    if (run.roomCleared) {
      // 届かなかった応答の再送は、記録した報酬を返す。
      const recorded = (await rewardsOf(run.id)).find(
        (row) => row.roomId === roomId,
      );
      return {
        reward: recorded ? reward(recorded) : null,
        ...(await readState(userId)),
      };
    }

    // 初めてのユーザーへ初期のボーナスを先に付与し、手に入れたボーナスを初期の付与で消さない。
    await createStepBonusRepository(binding).getState(userId, now);
    const bonusId = pickBonus();
    const rank = pickRank(room);
    const held = await db
      .select({ rank: stepBonusHoldings.rank })
      .from(stepBonusHoldings)
      .where(
        and(
          eq(stepBonusHoldings.userId, userId),
          eq(stepBonusHoldings.bonusId, bonusId),
        ),
      )
      .get();
    const outcome = !held
      ? "added"
      : rankOrder(rank) > rankOrder(held.rank as StepBonusRank)
        ? "updated"
        : "discarded";
    const boss = room.kind === "boss";
    const previousBest = boss ? await bestFloor(userId, run.destinationId) : 0;
    const written = sql`EXISTS (SELECT 1 FROM ${adventureRewards} WHERE ${adventureRewards.runId} = ${run.id} AND ${adventureRewards.roomId} = ${roomId} AND ${adventureRewards.createdAt} = ${now} AND ${adventureRewards.bonusId} = ${bonusId} AND ${adventureRewards.rank} = ${rank})`;

    await db.batch([
      db
        .insert(adventureRewards)
        .select(
          db
            .select({
              runId: adventureRuns.id,
              roomId: adventureRuns.roomId,
              bonusId: sql<string>`${bonusId}`.as("bonus_id"),
              rank: sql<string>`${rank}`.as("rank"),
              outcome: sql<string>`${outcome}`.as("outcome"),
              createdAt: sql<string>`${now}`.as("created_at"),
            })
            .from(adventureRuns)
            .where(
              and(
                eq(adventureRuns.id, run.id),
                eq(adventureRuns.status, "active"),
                eq(adventureRuns.roomId, roomId),
                eq(adventureRuns.roomCleared, false),
              ),
            ),
        )
        .onConflictDoNothing(),
      db
        .update(adventureRuns)
        .set(
          boss
            ? {
                roomCleared: true,
                status: "cleared",
                updatedAt: now,
                endedAt: now,
              }
            : { roomCleared: true, updatedAt: now },
        )
        .where(and(eq(adventureRuns.id, run.id), written)),
      db
        .insert(stepBonusHoldings)
        .select(
          db
            .select({
              userId: sql<string>`${userId}`.as("user_id"),
              bonusId: sql<string>`${bonusId}`.as("bonus_id"),
              rank: sql<string>`${rank}`.as("rank"),
              acquiredAt: sql<string>`${now}`.as("acquired_at"),
              updatedAt: sql<string>`${now}`.as("updated_at"),
            })
            .from(sql`(SELECT 1)`)
            .where(written),
        )
        .onConflictDoUpdate({
          target: [stepBonusHoldings.userId, stepBonusHoldings.bonusId],
          set: {
            rank: sql`excluded.rank`,
            updatedAt: sql`excluded.updated_at`,
          },
          // ランクの高いほうだけを残す（低い順に E D C B A S）。
          setWhere: sql`instr('EDCBAS', excluded.rank) > instr('EDCBAS', ${stepBonusHoldings.rank})`,
        }),
      ...(boss
        ? [recordStatement({ ...run, roomId }, "cleared", now, written)]
        : []),
    ]);

    const recorded = (await rewardsOf(run.id)).find(
      (row) => row.roomId === roomId,
    );
    if (!recorded) return { error: "adventure_changed" as const };
    const state = await readState(userId);
    if (!boss) return { reward: reward(recorded), ...state };
    const ended = await db
      .select()
      .from(adventureRuns)
      .where(eq(adventureRuns.id, run.id))
      .get();
    return {
      reward: reward(recorded),
      result: await result(ended as RunRow, previousBest),
      ...state,
    };
  }

  function findRevive(userId: string, requestId: string) {
    return db
      .select()
      .from(adventureRevives)
      .where(
        and(
          eq(adventureRevives.userId, userId),
          eq(adventureRevives.requestId, requestId),
        ),
      )
      .get();
  }

  async function recordedRevive(
    userId: string,
    request: Revive,
    row: NonNullable<Awaited<ReturnType<typeof findRevive>>>,
  ) {
    if (row.roomId !== request.roomId) {
      return { error: "request_conflict" as const };
    }
    return {
      revive: {
        roomId: row.roomId,
        runes: row.runes,
        balanceAfter: row.balanceAfter,
      },
      ...(await readState(userId)),
    };
  }

  // 負けた戦闘からルーンで復活する。費用はサーバーで計算し、残高と復活の回数を条件にした
  // 1回のbatchで、記録・ルーンの減算・回数の更新をまとめて行う。
  async function revive(userId: string, request: Revive, now: string) {
    const replay = await findRevive(userId, request.requestId);
    if (replay) return recordedRevive(userId, request, replay);
    const run = await activeRun(userId);
    if (!run) return { error: "no_adventure" as const };
    const destination = findDestination(run.destinationId) as Destination;
    const room = findRoom(destination, run.roomId);
    if (
      run.roomId !== request.roomId ||
      !room ||
      !isBattle(room) ||
      run.roomCleared
    ) {
      return { error: "room_mismatch" as const };
    }

    const id = crypto.randomUUID();
    const runes = reviveCost(run.revives);
    const balance = sql`(SELECT ${runeWallets.balance} FROM ${runeWallets} WHERE ${runeWallets.userId} = ${userId})`;
    const written = sql`EXISTS (SELECT 1 FROM ${adventureRevives} WHERE ${adventureRevives.id} = ${id})`;
    await db.batch([
      db
        .insert(adventureRevives)
        .select(
          db
            .select({
              id: sql<string>`${id}`.as("id"),
              userId: adventureRuns.userId,
              runId: adventureRuns.id,
              requestId: sql<string>`${request.requestId}`.as("request_id"),
              roomId: adventureRuns.roomId,
              runes: sql<number>`${runes}`.as("runes"),
              balanceAfter: sql<number>`0`.as("balance_after"),
              createdAt: sql<string>`${now}`.as("created_at"),
            })
            .from(adventureRuns)
            .where(
              and(
                eq(adventureRuns.id, run.id),
                eq(adventureRuns.status, "active"),
                eq(adventureRuns.roomId, request.roomId),
                eq(adventureRuns.roomCleared, false),
                eq(adventureRuns.revives, run.revives),
                sql`${balance} >= ${runes}`,
              ),
            ),
        )
        .onConflictDoNothing({
          target: [adventureRevives.userId, adventureRevives.requestId],
        }),
      db
        .update(runeWallets)
        .set({
          balance: sql`${runeWallets.balance} - ${runes}`,
          updatedAt: now,
        })
        .where(and(eq(runeWallets.userId, userId), written)),
      db
        .update(adventureRuns)
        .set({ revives: sql`${adventureRuns.revives} + 1`, updatedAt: now })
        .where(and(eq(adventureRuns.id, run.id), written)),
      db
        .update(adventureRevives)
        .set({ balanceAfter: sql`COALESCE(${balance}, 0)` })
        .where(eq(adventureRevives.id, id)),
    ]);

    const row = await findRevive(userId, request.requestId);
    if (row) return recordedRevive(userId, request, row);
    // 書き込めなかった理由を、今の冒険の状態で見分ける。
    const after = await activeRun(userId);
    return {
      error:
        !after ||
        after.revives !== run.revives ||
        after.roomId !== request.roomId ||
        after.roomCleared
          ? ("adventure_changed" as const)
          : ("insufficient_runes" as const),
    };
  }

  // 冒険を終える。負けて帰還する（defeat）か、自分からやめる（retreat）。手に入れた物は残る。
  async function end(userId: string, reason: EndReason, now: string) {
    const run = await activeRun(userId);
    if (!run) return { error: "no_adventure" as const };
    if (reason === "defeat") {
      const destination = findDestination(run.destinationId) as Destination;
      const room = findRoom(destination, run.roomId);
      if (!room || !isBattle(room) || run.roomCleared) {
        return { error: "room_mismatch" as const };
      }
    }
    const status: EndStatus = reason === "defeat" ? "defeated" : "retreated";
    const previousBest = await bestFloor(userId, run.destinationId);
    const written = sql`EXISTS (SELECT 1 FROM ${adventureRuns} WHERE ${adventureRuns.id} = ${run.id} AND ${adventureRuns.status} = ${status} AND ${adventureRuns.endedAt} = ${now})`;
    await db.batch([
      db
        .update(adventureRuns)
        .set({ status, updatedAt: now, endedAt: now })
        .where(
          and(eq(adventureRuns.id, run.id), eq(adventureRuns.status, "active")),
        ),
      recordStatement(run, status, now, written),
    ]);
    const ended = await db
      .select()
      .from(adventureRuns)
      .where(eq(adventureRuns.id, run.id))
      .get();
    if (!ended || ended.status !== status || ended.endedAt !== now) {
      return { error: "adventure_changed" as const };
    }
    return {
      result: await result(ended, previousBest),
      ...(await readState(userId)),
    };
  }

  // 冒険の途中かどうか。冒険中はUPTボーナスの枠を付け替えられない。
  async function inProgress(userId: string) {
    return (await activeRun(userId)) !== undefined;
  }

  return { getState, start, move, clear, revive, end, inProgress };
}
