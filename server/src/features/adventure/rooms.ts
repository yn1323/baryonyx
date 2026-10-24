import { and, eq, isNull, type SQL, sql } from "drizzle-orm";
import { type Database, selectWhen } from "../../shared/db.js";
import type { Random } from "../../shared/random.js";
import { todayStepsOf, todayStepsRows } from "../health/repository.js";
import { inventoryGrants } from "../inventory/grants.js";
import { enemyStatements } from "../library/discoveries.js";
import {
  type EventKind,
  hasEvent,
  MIMIC_CHANCE,
  stepDoorTier,
} from "./catalog.js";
import { adventureDrops, adventureRuns } from "./db-schema.js";
import { type Drop, rollRoomDrops } from "./drops.js";
import {
  bestFloorOf,
  recordStatement,
  resultOf,
  resultReads,
  resultRowsOf,
} from "./results.js";
import type { Enemies } from "./schema.js";
import {
  type AdventureRead,
  batchWithState,
  battleRoom,
  type DropRow,
  dropOf,
  kindOf,
  type RunRow,
  stepDoorOf,
} from "./state.js";

const dropsInRoom = (rows: readonly DropRow[], roomId: string) =>
  rows.filter((row) => row.roomId === roomId).map(dropOf);

// 今いるマスの出来事を終える（戦闘の勝利・宝箱・怪しい宝箱を開ける）。
export function createRoomOperations(db: Database, random: Random) {
  // 今いるマスを、物を落とさずに終えた印だけを付ける（休憩・行商人・開けずに通り過ぎた怪しい宝箱）。
  async function markCleared(
    userId: string,
    run: RunRow,
    now: string,
    guard?: SQL,
  ) {
    const marked = await batchWithState(db, userId, [
      db
        .update(adventureRuns)
        .set({ roomCleared: true, updatedAt: now })
        .where(
          and(
            eq(adventureRuns.id, run.id),
            eq(adventureRuns.status, "active"),
            eq(adventureRuns.roomId, run.roomId),
            eq(adventureRuns.roomCleared, false),
            guard,
          ),
        )
        .returning({ id: adventureRuns.id }),
    ]);
    if (marked.results[0].length === 0) {
      return { error: "adventure_changed" as const };
    }
    return { drops: [], ...marked.state };
  }

  // 落ちた物を記録して持ち物に入れ、今いるマスを終える（doc/features/drops.md）。最奥のボスなら冒険も終える。
  // マスの終了・ドロップの記録・持ち物・記録を1回のbatchで行い、同じbatchで書いたあとの冒険と、終えた冒険の結果を読む。
  // guard は1つ目の物を書く条件に足す冒険の条件、set はマスを終えるときに冒険へ一緒に書く値。
  // 書けなかったとき（再送や同時の要求）は、記録した物（recorded）が空になる。
  async function award(
    userId: string,
    run: RunRow,
    drops: Drop[],
    now: string,
    options: {
      enemies: Enemies;
      guard?: SQL;
      set?: Partial<typeof adventureRuns.$inferInsert>;
    },
  ) {
    const roomId = run.roomId;
    const boss = kindOf(run) === "boss";
    const grantId = crypto.randomUUID();
    const written = sql`EXISTS (SELECT 1 FROM ${adventureDrops} WHERE ${adventureDrops.runId} = ${run.id} AND ${adventureDrops.roomId} = ${roomId} AND ${adventureDrops.grantId} = ${grantId})`;
    const dropRow = (index: number) => {
      const entry = drops[index];
      return {
        seq: sql<number>`${index}`.as("seq"),
        grantId: sql<string>`${grantId}`.as("grant_id"),
        category: sql<string>`${entry.category}`.as("category"),
        entryId: sql<string>`${entry.id}`.as("entry_id"),
        multiplierPercent: sql<number | null>`${entry.multiplierPercent}`.as(
          "multiplier_percent",
        ),
        createdAt: sql<string>`${now}`.as("created_at"),
      };
    };

    const awarded = await batchWithState(db, userId, [
      // 1つ目の物は、部屋を終えていない進行中の冒険のときだけ書く。残りはそれが書けたときだけ書く。
      db
        .insert(adventureDrops)
        .select(
          db
            .select({
              runId: adventureRuns.id,
              roomId: adventureRuns.roomId,
              ...dropRow(0),
            })
            .from(adventureRuns)
            .where(
              and(
                eq(adventureRuns.id, run.id),
                eq(adventureRuns.status, "active"),
                eq(adventureRuns.roomId, roomId),
                eq(adventureRuns.roomCleared, false),
                options.guard,
              ),
            ),
        )
        .onConflictDoNothing(),
      ...drops.slice(1).map((_, offset) =>
        db
          .insert(adventureDrops)
          .select(
            selectWhen(
              db,
              written,
            )({
              runId: sql<string>`${run.id}`.as("run_id"),
              roomId: sql<string>`${roomId}`.as("room_id"),
              ...dropRow(offset + 1),
            }),
          )
          .onConflictDoNothing(),
      ),
      db
        .update(adventureRuns)
        .set(
          boss
            ? {
                ...options.set,
                roomCleared: true,
                status: "cleared",
                updatedAt: now,
                endedAt: now,
              }
            : { ...options.set, roomCleared: true, updatedAt: now },
        )
        .where(and(eq(adventureRuns.id, run.id), written)),
      ...inventoryGrants(db, written, userId, drops, now),
      ...(boss ? [recordStatement(db, run, "cleared", now, written)] : []),
      ...(battleRoom(run)
        ? enemyStatements(db, userId, options.enemies, now, written)
        : []),
      ...resultReads(db, run),
    ]);
    const [ended, records, rows] = resultRowsOf(awarded.results);
    return {
      recorded: dropsInRoom(rows, roomId),
      result: (previousBest: number) =>
        resultOf(ended[0], records[0], rows, previousBest),
      read: awarded,
    };
  }

  // 今いる部屋の出来事（戦闘の勝利・宝箱など）を終え、行き先の一覧から物を落とす（doc/features/drops.md）。
  // 報酬の行はマスで決まったことで選ぶ（ミミックか・試練の条件を満たしたか・歩数の扉の今日の歩数）。
  // 怪しい宝箱は、ミミックに勝ったか、開けずに通り過ぎる（pass）ときだけ終えられる。本物は開けたときに終わる。
  // 歩数の扉の今日の歩数は、冒険の状態と同じbatchで読む。
  async function clear(
    userId: string,
    roomId: string,
    now: string,
    enemies: Enemies = [],
    options: { pass?: boolean; trialMet?: boolean; evolved?: boolean } = {},
  ) {
    const first = await batchWithState(db, userId, [
      todayStepsRows(db, userId, now),
    ]);
    const run = first.activeRun;
    if (!run) return { error: "no_adventure" as const };
    const kind = kindOf(run);
    if (run.roomId !== roomId || !hasEvent(kind)) {
      return { error: "room_mismatch" as const };
    }
    if (options.pass && kind !== "shady-chest") {
      return { error: "pass_not_allowed" as const };
    }
    if (run.roomCleared) {
      // 届かなかった応答の再送は、記録したドロップを返す。
      return {
        drops: dropsInRoom(first.dropRows, roomId),
        ...stepDoorOf(run),
        ...first.state,
      };
    }
    if (kind === "shady-chest") {
      // 開けたあと（ミミック）は通り過ぎられない。開ける前は、ミミックに勝ったとは言えない。
      if (options.pass) {
        if (run.mimic !== null) return { error: "pass_not_allowed" as const };
        return markCleared(userId, run, now, isNull(adventureRuns.mimic));
      }
      if (run.mimic !== true) return { error: "chest_not_opened" as const };
    }

    // 歩数の扉は、扉を開けたとき（マスを終えたとき）の今日の歩数で報酬を決める。
    const steps =
      kind === "step-door" ? todayStepsOf(first.results[0], now) : null;
    const rolled = rollRoomDrops(run.destinationId, kind as EventKind, random, {
      mimic: run.mimic === true,
      trialMet: options.trialMet ?? false,
      evolved: kind === "altar" && (options.evolved ?? false),
      ...(steps === null ? {} : { stepDoorTier: stepDoorTier(steps) }),
    });
    // 落とす物のないマス（休憩・行商人）は、進行中の冒険のそのマスを終えた印だけを付ける。
    if (rolled.length === 0 && kind !== "boss") {
      return markCleared(userId, run, now);
    }

    const boss = kind === "boss";
    const previousBest = boss
      ? bestFloorOf(first.state.records, run.destinationId)
      : 0;
    const awarded = await award(userId, run, rolled, now, {
      enemies,
      ...(steps === null ? {} : { set: { doorSteps: steps } }),
    });
    if (awarded.recorded.length === 0) {
      return { error: "adventure_changed" as const };
    }
    const state = awarded.read.state;
    if (!boss) {
      const doorState =
        steps === null
          ? {}
          : { stepDoor: { steps, tier: stepDoorTier(steps) } };
      return { drops: awarded.recorded, ...doorState, ...state };
    }
    return {
      drops: awarded.recorded,
      result: awarded.result(previousBest),
      ...state,
    };
  }

  // 開けた怪しい宝箱の結果。ミミックなら戦闘のマスのまま、本物なら記録したドロップを返す。開ける前は null。
  function openedChest(
    run: RunRow,
    read: Pick<AdventureRead, "dropRows" | "state">,
  ) {
    if (run.mimic === null) return null;
    return {
      drops: run.mimic ? [] : dropsInRoom(read.dropRows, run.roomId),
      ...read.state,
    };
  }

  // 今いる怪しい宝箱を開ける。半分の確率でミミックとの戦闘になり、本物ならその場で強い報酬を1つ多く落として終える。
  // 結果は冒険に保存し、同じマスでの再送には決めた結果を返す。
  async function open(userId: string, roomId: string, now: string) {
    const first = await batchWithState(db, userId, []);
    const run = first.activeRun;
    if (!run) return { error: "no_adventure" as const };
    if (run.roomId !== roomId || kindOf(run) !== "shady-chest") {
      return { error: "room_mismatch" as const };
    }
    const opened = openedChest(run, first);
    if (opened) return opened;
    // 開けずに通り過ぎたマスは、もう開けられない。
    if (run.roomCleared) return { error: "room_cleared" as const };

    const after =
      random() < MIMIC_CHANCE
        ? await batchWithState(db, userId, [
            db
              .update(adventureRuns)
              .set({ mimic: true, updatedAt: now })
              .where(
                and(
                  eq(adventureRuns.id, run.id),
                  eq(adventureRuns.status, "active"),
                  eq(adventureRuns.roomId, roomId),
                  eq(adventureRuns.roomCleared, false),
                  isNull(adventureRuns.mimic),
                ),
              ),
          ])
        : (
            await award(
              userId,
              run,
              rollRoomDrops(run.destinationId, "shady-chest", random),
              now,
              {
                enemies: [],
                guard: isNull(adventureRuns.mimic),
                set: { mimic: false },
              },
            )
          ).read;
    // 同時の要求で先に決まった結果があれば、それを返す。
    const current = after.activeRun;
    const decided =
      current && current.id === run.id && current.roomId === roomId
        ? openedChest(current, after)
        : null;
    return decided ?? { error: "adventure_changed" as const };
  }

  return { clear, open };
}
