import { and, eq, sql } from "drizzle-orm";
import type { Database } from "../../shared/db.js";
import { runeRecord } from "../exercise-rewards/wallet.js";
import { inventoryGrants } from "../inventory/grants.js";
import { reviveCost } from "./catalog.js";
import {
  adventurePurchases,
  adventureRevives,
  adventureRuns,
} from "./db-schema.js";
import type { Buy, Revive } from "./schema.js";
import {
  type AdventureRead,
  batchWithState,
  battleRoom,
  kindOf,
} from "./state.js";

type PurchaseRow = typeof adventurePurchases.$inferSelect;
type ReviveRow = typeof adventureRevives.$inferSelect;

// 冒険の中でルーンを使う操作（行商人での購入・負けた戦闘からの復活）。要求IDで再送を見分け、二重に引かない。
// 要求IDの記録と冒険の状態を1回の往復で読み、書き込みと書いたあとの記録・状態をもう1回の往復で済ませる。
export function createPurchaseOperations(db: Database) {
  const findPurchase = (userId: string, requestId: string) =>
    db
      .select()
      .from(adventurePurchases)
      .where(
        and(
          eq(adventurePurchases.userId, userId),
          eq(adventurePurchases.requestId, requestId),
        ),
      );

  function recordedPurchase(
    request: Buy,
    row: PurchaseRow,
    state: AdventureRead["state"],
  ) {
    if (row.roomId !== request.roomId || row.idx !== request.index) {
      return { error: "request_conflict" as const };
    }
    return {
      purchase: {
        roomId: row.roomId,
        index: row.idx,
        runes: row.runes,
        balanceAfter: row.balanceAfter,
      },
      ...state,
    };
  }

  // 今いる行商人のマスで、品物を1つルーンで買う。値段は品ぞろえを作ったときにサーバーが決めた額で、
  // 購入の記録・ルーンの減算・持ち物への追加・ライブラリの見つけた記録を、残高と売れていないことを条件にした
  // 1回のbatchで行う。要求IDで再送を見分け、品物ごとの一意で同じ品物を二度売らない。
  // マスを終えたあとも、次のマスへ進むまでは買える。
  async function buy(userId: string, request: Buy, now: string) {
    const first = await batchWithState(db, userId, [
      findPurchase(userId, request.requestId),
    ]);
    const replay = first.results[0][0];
    if (replay) return recordedPurchase(request, replay, first.state);
    const run = first.activeRun;
    if (!run) return { error: "no_adventure" as const };
    if (run.roomId !== request.roomId || kindOf(run) !== "merchant") {
      return { error: "room_mismatch" as const };
    }
    const goods = first.goodsRows.find(
      (row) => row.goods.idx === request.index,
    )?.goods;
    if (!goods) return { error: "goods_not_found" as const };

    const pay = runeRecord(db, userId, adventurePurchases, now);
    const bought = await batchWithState(db, userId, [
      db
        .insert(adventurePurchases)
        .select(
          db
            .select({
              id: sql<string>`${pay.id}`.as("id"),
              userId: adventureRuns.userId,
              runId: adventureRuns.id,
              requestId: sql<string>`${request.requestId}`.as("request_id"),
              roomId: adventureRuns.roomId,
              idx: sql<number>`${goods.idx}`.as("idx"),
              runes: sql<number>`${goods.price}`.as("runes"),
              balanceAfter: sql<number>`0`.as("balance_after"),
              createdAt: sql<string>`${now}`.as("created_at"),
            })
            .from(adventureRuns)
            .where(
              and(
                eq(adventureRuns.id, run.id),
                eq(adventureRuns.status, "active"),
                eq(adventureRuns.roomId, request.roomId),
                eq(adventureRuns.roomKind, "merchant"),
                pay.affords(goods.price),
              ),
            ),
        )
        // 要求IDの再送と、売れた品物の二度目の購入のどちらでも書かない。
        .onConflictDoNothing(),
      pay.debit(goods.price),
      ...inventoryGrants(
        db,
        pay.written,
        userId,
        [
          {
            category: goods.category,
            id: goods.entryId,
            multiplierPercent: goods.multiplierPercent,
          },
        ],
        now,
      ),
      pay.settle,
      findPurchase(userId, request.requestId),
    ]);
    const row = (bought.results.at(-1) as PurchaseRow[])[0];
    if (row) return recordedPurchase(request, row, bought.state);
    // 書き込めなかった理由を、品物と今の冒険の状態で見分ける。
    const sold = bought.goodsRows.some(
      (entry) => entry.goods.idx === goods.idx && entry.purchaseId !== null,
    );
    if (sold) return { error: "sold_out" as const };
    const after = bought.activeRun;
    return {
      error:
        !after || after.id !== run.id || after.roomId !== request.roomId
          ? ("adventure_changed" as const)
          : ("insufficient_runes" as const),
    };
  }

  const findRevive = (userId: string, requestId: string) =>
    db
      .select()
      .from(adventureRevives)
      .where(
        and(
          eq(adventureRevives.userId, userId),
          eq(adventureRevives.requestId, requestId),
        ),
      );

  function recordedRevive(
    request: Revive,
    row: ReviveRow,
    state: AdventureRead["state"],
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
      ...state,
    };
  }

  // 負けた戦闘からルーンで復活する。費用はサーバーで計算し、残高と復活の回数を条件にした
  // 1回のbatchで、記録・ルーンの減算・回数の更新をまとめて行う。
  async function revive(userId: string, request: Revive, now: string) {
    const first = await batchWithState(db, userId, [
      findRevive(userId, request.requestId),
    ]);
    const replay = first.results[0][0];
    if (replay) return recordedRevive(request, replay, first.state);
    const run = first.activeRun;
    if (!run) return { error: "no_adventure" as const };
    if (run.roomId !== request.roomId || !battleRoom(run) || run.roomCleared) {
      return { error: "room_mismatch" as const };
    }

    const runes = reviveCost(run.revives);
    const pay = runeRecord(db, userId, adventureRevives, now);
    const revived = await batchWithState(db, userId, [
      db
        .insert(adventureRevives)
        .select(
          db
            .select({
              id: sql<string>`${pay.id}`.as("id"),
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
                pay.affords(runes),
              ),
            ),
        )
        .onConflictDoNothing({
          target: [adventureRevives.userId, adventureRevives.requestId],
        }),
      pay.debit(runes),
      db
        .update(adventureRuns)
        .set({ revives: sql`${adventureRuns.revives} + 1`, updatedAt: now })
        .where(and(eq(adventureRuns.id, run.id), pay.written)),
      pay.settle,
      findRevive(userId, request.requestId),
    ]);
    const row = (revived.results.at(-1) as ReviveRow[])[0];
    if (row) return recordedRevive(request, row, revived.state);
    // 書き込めなかった理由を、今の冒険の状態で見分ける。
    const after = revived.activeRun;
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

  return { buy, revive };
}
