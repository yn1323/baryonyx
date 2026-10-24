import { and, eq, sql } from "drizzle-orm";
import type { Database } from "../../shared/db.js";
import type { Random } from "../../shared/random.js";
import { enemyStatements } from "../library/discoveries.js";
import { START_SQUARE } from "../world-map/catalog.js";
import { worldMapSquares } from "../world-map/db-schema.js";
import {
  bossFloor,
  canPlace,
  type Destination,
  ENTRANCE_ROOM_ID,
  findDestination,
  hasEvent,
  newSeed,
  pickMysteryKind,
  pickTrialCondition,
  rollChestAwakening,
} from "./catalog.js";
import { adventureMerchantGoods, adventureRuns } from "./db-schema.js";
import { rollMerchantGoods } from "./merchant.js";
import {
  bestFloorOf,
  recordStatement,
  resultOf,
  resultReads,
  resultRowsOf,
} from "./results.js";
import type { EndReason, Enemies, Move } from "./schema.js";
import { batchWithState, battleRoom, type EndStatus } from "./state.js";

// 冒険を始める・次の階へ進む・終える。どれも、確かめる読み込みと、書き込みと書いたあとの読み込みの2回の往復で済ませる。
export function createRunOperations(db: Database, random: Random) {
  // 冒険先の入口の部屋から冒険を始める。進行中の冒険があるあいだは始められない。
  // ダンジョンは、その街のマスを街道でクリアするまで入れない（最初の街はいつもクリア済み）。
  async function start(userId: string, destinationId: string, now: string) {
    const destination = findDestination(destinationId);
    if (!destination) return { error: "unknown_destination" as const };
    const {
      results: [towns],
      activeRun,
      state,
    } = await batchWithState(db, userId, [
      db
        .select({ squareId: worldMapSquares.squareId })
        .from(worldMapSquares)
        .where(
          and(
            eq(worldMapSquares.userId, userId),
            eq(worldMapSquares.squareId, destination.town),
          ),
        ),
    ]);
    if (destination.town !== START_SQUARE && towns.length === 0) {
      return { error: "destination_locked" as const };
    }
    if (activeRun) {
      // 届かなかった応答の再送は、始めたばかりの同じ冒険を返す。
      if (
        activeRun.destinationId === destinationId &&
        activeRun.route.length === 1
      ) {
        return state;
      }
      return { error: "adventure_in_progress" as const };
    }
    const started = await batchWithState(db, userId, [
      db
        .insert(adventureRuns)
        .values({
          id: crypto.randomUUID(),
          userId,
          destinationId,
          seed: newSeed(random),
          roomId: ENTRANCE_ROOM_ID,
          floor: 1,
          roomKind: "start",
          roomCleared: true,
          route: [ENTRANCE_ROOM_ID],
          revives: 0,
          status: "active",
          startedAt: now,
          updatedAt: now,
        })
        .onConflictDoNothing(),
    ]);
    if (started.activeRun?.destinationId !== destinationId) {
      return { error: "adventure_in_progress" as const };
    }
    return started.state;
  }

  // 今いる部屋から、次の階の部屋へ進む。選んだ時点で保存する。
  // 部屋のつながりはクライアントが種から作った道で決まるため、サーバーは1階ずつ進むこと、
  // マスの種類をその階に置けること（最奥の間の階にだけボスがいることを含む）、出来事を終えてから
  // 進むことを確かめる。
  async function move(userId: string, target: Move, now: string) {
    const { activeRun: run, state } = await batchWithState(db, userId, []);
    if (!run) return { error: "no_adventure" as const };
    // 届かなかった応答の再送は、進んだあとの状態を返す。
    if (run.roomId === target.roomId) return state;
    const destination = findDestination(run.destinationId) as Destination;
    const boss = bossFloor(destination);
    if (
      target.floor !== run.floor + 1 ||
      target.floor > boss ||
      !canPlace(destination, target.floor, target.kind)
    ) {
      return { error: "room_not_reachable" as const };
    }
    if (!run.roomCleared) return { error: "room_not_cleared" as const };
    // 「？」のマスは、入ったときに中身の種類を決め、その種類のマスとして保存する。
    // 試練は入ったときに条件を、行商人は品ぞろえを決めて保存する（doc/features/stage-progression.md のマスの中身）。
    const kind =
      target.kind === "mystery" ? pickMysteryKind(random) : target.kind;
    const trial = kind === "trial" ? pickTrialCondition(random) : null;
    // 宝箱は、入ったときに覚醒が出るかを決める（「？」の中身が宝箱だったときも同じ）。
    const awakening = kind === "treasure" ? rollChestAwakening(random) : null;
    const goods =
      kind === "merchant" ? rollMerchantGoods(run.destinationId, random) : [];
    const moved = await batchWithState(db, userId, [
      db
        .update(adventureRuns)
        .set({
          roomId: target.roomId,
          floor: target.floor,
          roomKind: kind,
          roomCleared: !hasEvent(kind),
          mimic: null,
          trial,
          doorSteps: null,
          awakening,
          route: [...run.route, target.roomId],
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
        .returning({ id: adventureRuns.id }),
      // 品物は、冒険がそのマスへ進んだときだけ書く。同時の要求で先に書いた品物は残す。
      ...goods.map((entry, index) =>
        db
          .insert(adventureMerchantGoods)
          .select(
            db
              .select({
                runId: adventureRuns.id,
                roomId: adventureRuns.roomId,
                idx: sql<number>`${index}`.as("idx"),
                category: sql<string>`${entry.category}`.as("category"),
                entryId: sql<string>`${entry.id}`.as("entry_id"),
                multiplierPercent: sql<number>`${entry.multiplierPercent}`.as(
                  "multiplier_percent",
                ),
                price: sql<number>`${entry.price}`.as("price"),
                createdAt: sql<string>`${now}`.as("created_at"),
              })
              .from(adventureRuns)
              .where(
                and(
                  eq(adventureRuns.id, run.id),
                  eq(adventureRuns.status, "active"),
                  eq(adventureRuns.roomId, target.roomId),
                ),
              ),
          )
          .onConflictDoNothing(),
      ),
    ]);
    if (moved.results[0].length === 0) {
      return { error: "adventure_changed" as const };
    }
    return moved.state;
  }

  // 冒険を終える。負けて帰還する（defeat）か、自分からやめる（retreat）。手に入れた物は残る。
  async function end(
    userId: string,
    reason: EndReason,
    now: string,
    enemies: Enemies = [],
  ) {
    const { activeRun: run, state } = await batchWithState(db, userId, []);
    if (!run) return { error: "no_adventure" as const };
    if (reason === "defeat" && (!battleRoom(run) || run.roomCleared)) {
      return { error: "room_mismatch" as const };
    }
    const status: EndStatus = reason === "defeat" ? "defeated" : "retreated";
    const previousBest = bestFloorOf(state.records, run.destinationId);
    const written = sql`EXISTS (SELECT 1 FROM ${adventureRuns} WHERE ${adventureRuns.id} = ${run.id} AND ${adventureRuns.status} = ${status} AND ${adventureRuns.endedAt} = ${now})`;
    const ended = await batchWithState(db, userId, [
      db
        .update(adventureRuns)
        .set({ status, updatedAt: now, endedAt: now })
        .where(
          and(eq(adventureRuns.id, run.id), eq(adventureRuns.status, "active")),
        ),
      recordStatement(db, run, status, now, written),
      ...(reason === "defeat"
        ? enemyStatements(db, userId, enemies, now, written)
        : []),
      ...resultReads(db, run),
    ]);
    const [endedRuns, records, drops] = resultRowsOf(ended.results);
    const row = endedRuns[0];
    if (!row || row.status !== status || row.endedAt !== now) {
      return { error: "adventure_changed" as const };
    }
    return {
      result: resultOf(row, records[0], drops, previousBest),
      ...ended.state,
    };
  }

  return { start, move, end };
}
