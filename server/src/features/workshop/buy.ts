import { and, eq, sql } from "drizzle-orm";
import type { Database } from "../../shared/db.js";
import { multiplierOf, qualityOf } from "../../shared/quality.js";
import { runeRecord } from "../exercise-rewards/wallet.js";
import { inventoryGrants } from "../inventory/grants.js";
import { libraryDiscoveries } from "../library/db-schema.js";
import { WORKSHOP_MULTIPLIER_PERCENT, workshopPrice } from "./catalog.js";
import { workshopPurchases } from "./db-schema.js";
import type { Buy } from "./schema.js";

type PurchaseRow = typeof workshopPurchases.$inferSelect;

// 見つけた記録があることのSQLの式。商会で買う・作れるのは、これまでに手に入れた物だけ。
export const discoveredSql = (userId: string, kind: string, id: string) =>
  sql`EXISTS (SELECT 1 FROM ${libraryDiscoveries} WHERE ${libraryDiscoveries.userId} = ${userId} AND ${libraryDiscoveries.kind} = ${kind} AND ${libraryDiscoveries.entryId} = ${id})`;

export const findDiscovery = (
  db: Database,
  userId: string,
  kind: string,
  id: string,
) =>
  db
    .select({ entryId: libraryDiscoveries.entryId })
    .from(libraryDiscoveries)
    .where(
      and(
        eq(libraryDiscoveries.userId, userId),
        eq(libraryDiscoveries.kind, kind),
        eq(libraryDiscoveries.entryId, id),
      ),
    );

export function createBuyOperation(db: Database) {
  const findPurchase = (userId: string, requestId: string) =>
    db
      .select()
      .from(workshopPurchases)
      .where(
        and(
          eq(workshopPurchases.userId, userId),
          eq(workshopPurchases.requestId, requestId),
        ),
      );

  // 買った物は品質の幅の下限（×1.00）のため、どの種類も品質Cになる。
  function recorded(request: Buy, row: PurchaseRow) {
    if (row.category !== request.category || row.entryId !== request.id) {
      return { error: "request_conflict" as const };
    }
    return {
      purchase: {
        category: row.category,
        id: row.entryId,
        itemId: row.itemId,
        multiplier: multiplierOf(WORKSHOP_MULTIPLIER_PERCENT),
        quality: qualityOf(
          WORKSHOP_MULTIPLIER_PERCENT,
          WORKSHOP_MULTIPLIER_PERCENT,
        ),
        runes: row.runes,
        balanceAfter: row.balanceAfter,
      },
    };
  }

  // これまでに手に入れた物を1つ、品質の最低（×1.00）でルーンと引き換えに持ち物へ足す。値段はサーバーが決める。
  // 購入の記録・ルーンの減算・持ち物への追加を、残高と見つけた記録があることを条件にした1回のbatchで行う。
  // 見つけた記録は条件にあるため、書き足さない。要求IDで再送を見分け、二重に引かない。
  // 確かめることは値段（サーバーの定義）だけのため、書き込みと、書いた記録・見つけた記録の読み込みを1回の往復で済ませる。
  // 再送では要求IDが一意のため記録を書かず、書いてある記録を読んで返す。
  async function buy(userId: string, request: Buy, now: string) {
    const price = workshopPrice(request.category, request.id);
    if (price === undefined) return { error: "goods_not_found" as const };

    const itemId = crypto.randomUUID();
    const pay = runeRecord(db, userId, workshopPurchases, now);
    const results = await db.batch([
      db
        .insert(workshopPurchases)
        .select(
          db
            .select({
              id: sql<string>`${pay.id}`.as("id"),
              userId: sql<string>`${userId}`.as("user_id"),
              requestId: sql<string>`${request.requestId}`.as("request_id"),
              category: sql<string>`${request.category}`.as("category"),
              entryId: sql<string>`${request.id}`.as("entry_id"),
              itemId: sql<string>`${itemId}`.as("item_id"),
              runes: sql<number>`${price}`.as("runes"),
              balanceAfter: sql<number>`0`.as("balance_after"),
              createdAt: sql<string>`${now}`.as("created_at"),
            })
            .from(sql`(SELECT 1)`)
            .where(
              and(
                pay.affords(price),
                discoveredSql(userId, request.category, request.id),
              ),
            ),
        )
        // 同時に届いた同じ要求IDの再送では書かない。
        .onConflictDoNothing(),
      pay.debit(price),
      // 持ち物には、購入を書けたときだけ1つ足す。
      ...inventoryGrants(
        db,
        pay.written,
        userId,
        [
          {
            category: request.category,
            id: request.id,
            multiplierPercent: WORKSHOP_MULTIPLIER_PERCENT,
            rowId: itemId,
          },
        ],
        now,
        { discover: false },
      ),
      pay.settle,
      findDiscovery(db, userId, request.category, request.id),
      findPurchase(userId, request.requestId),
    ]);
    const row = (results.at(-1) as PurchaseRow[])[0];
    if (row) return recorded(request, row);
    // 書けなかった理由を、見つけた記録があるかで見分ける。
    return {
      error:
        (results.at(-2) as unknown[]).length > 0
          ? ("insufficient_runes" as const)
          : ("not_discovered" as const),
    };
  }

  return { buy };
}
