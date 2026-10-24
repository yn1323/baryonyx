import { and, eq, inArray, type SQL, sql } from "drizzle-orm";
import type { Database, Statement } from "../../shared/db.js";
import { slotOf } from "../equipment/catalog.js";
import {
  equipmentCharacterSlots,
  equipmentItems,
} from "../equipment/db-schema.js";
import { runeRecord } from "../exercise-rewards/wallet.js";
import { itemHoldings } from "../items/db-schema.js";
import { partyCharacterCards, skillCards } from "../party/db-schema.js";
import { sellItemPrice, sellPrice } from "./catalog.js";
import { materialKey } from "./craft.js";
import { workshopSales } from "./db-schema.js";
import type { Sell } from "./schema.js";

type SaleRow = typeof workshopSales.$inferSelect;

// 売った物。スキル・装備は持ち物の行のID、アイテムは種類ごとの数。
type SaleGoods = Pick<Sell, "cardIds" | "equipmentIds" | "items">;

// 売った物を、記録と要求で比べられる形にする。選んだ順は問わない。
function goodsKey(goods: SaleGoods) {
  return JSON.stringify([
    materialKey(goods.cardIds),
    materialKey(goods.equipmentIds),
    materialKey(goods.items),
  ]);
}

const goodsOf = ({ cardIds, equipmentIds, items }: Sell): SaleGoods => ({
  cardIds,
  equipmentIds,
  items,
});

// 行のIDと、倍率（アイテムは数）を「:」でつないだ値。書くときにも同じ状態かをこの値の一覧で確かめる。
const keyOf = (id: string, value: number) => `${id}:${value}`;
const listOf = (keys: readonly string[]) =>
  sql.join(
    keys.map((key) => sql`${key}`),
    sql`, `,
  );

export function createSellOperation(db: Database) {
  const findSale = (userId: string, requestId: string) =>
    db
      .select()
      .from(workshopSales)
      .where(
        and(
          eq(workshopSales.userId, userId),
          eq(workshopSales.requestId, requestId),
        ),
      );

  function sold(request: Sell, row: SaleRow) {
    if (goodsKey(row.goods) !== goodsKey(request)) {
      return { error: "request_conflict" as const };
    }
    return {
      sale: { runes: row.runes, balanceAfter: row.balanceAfter, ...row.goods },
    };
  }

  // 売る物を読む文。IDの一覧が空なら空の結果になる。付けていないかは、書くときの条件（ready）で確かめる。
  function goodsReads(userId: string, request: Sell) {
    return [
      db
        .select({
          id: skillCards.id,
          entryId: skillCards.skillId,
          percent: skillCards.multiplierPercent,
        })
        .from(skillCards)
        .where(
          and(
            eq(skillCards.userId, userId),
            inArray(skillCards.id, request.cardIds),
          ),
        ),
      db
        .select({
          id: equipmentItems.id,
          entryId: equipmentItems.equipmentId,
          percent: equipmentItems.multiplierPercent,
        })
        .from(equipmentItems)
        .where(
          and(
            eq(equipmentItems.userId, userId),
            inArray(equipmentItems.id, request.equipmentIds),
          ),
        ),
      db
        .select({ id: itemHoldings.itemId, count: itemHoldings.count })
        .from(itemHoldings)
        .where(
          and(
            eq(itemHoldings.userId, userId),
            inArray(
              itemHoldings.itemId,
              request.items.map((item) => item.id),
            ),
          ),
        ),
    ] as const;
  }

  // 持ち物を売り、ルーンを得る。スキル・装備はキャラに付けていない物を1つずつ、アイテムはデッキに入れていない
  // 種類を、持っている数のまま売る。値段はサーバーが決める（catalog.ts の sellPrice・sellItemPrice）。
  // 売った記録・ルーンの加算・持ち物の消去を、売る物が読んだときと同じ状態（同じ倍率・同じ数で持っていて、
  // 付けていない）であることを条件にした1回のbatchで行う。同時の要求で同じ物を二重に売らない。
  // 要求IDで再送を見分ける。要求IDの記録と売る物を1回の往復で読み、書き込みと書いた記録をもう1回の往復で済ませる。
  async function sell(userId: string, request: Sell, now: string) {
    const [sales, cards, gear, held] = await db.batch([
      findSale(userId, request.requestId),
      ...goodsReads(userId, request),
    ]);
    const replay = sales[0];
    if (replay) return sold(request, replay);

    const valid =
      cards.length === request.cardIds.length &&
      gear.length === request.equipmentIds.length &&
      request.items.every(
        (item) => held.find((row) => row.id === item.id)?.count === item.count,
      );
    if (!valid) return { error: "invalid_goods" as const };

    const runes =
      cards.reduce(
        (sum, row) => sum + sellPrice("skill", row.entryId, row.percent),
        0,
      ) +
      gear.reduce(
        (sum, row) =>
          sum +
          sellPrice(slotOf(row.entryId) ?? "weapon", row.entryId, row.percent),
        0,
      ) +
      request.items.reduce(
        (sum, item) => sum + sellItemPrice(item.id, item.count),
        0,
      );

    // 書くときにも、読んだときと同じ倍率・数で持っていて、付けていない（デッキに入れていない）こと。
    const ready: SQL[] = [];
    if (cards.length > 0) {
      const keys = cards.map((row) => keyOf(row.id, row.percent));
      ready.push(
        sql`(SELECT COUNT(*) FROM ${skillCards} WHERE ${skillCards.userId} = ${userId} AND ${skillCards.id} || ':' || ${skillCards.multiplierPercent} IN (${listOf(keys)}) AND NOT EXISTS (SELECT 1 FROM ${partyCharacterCards} WHERE ${partyCharacterCards.userId} = ${skillCards.userId} AND ${partyCharacterCards.cardId} = ${skillCards.id})) = ${keys.length}`,
      );
    }
    if (gear.length > 0) {
      const keys = gear.map((row) => keyOf(row.id, row.percent));
      ready.push(
        sql`(SELECT COUNT(*) FROM ${equipmentItems} WHERE ${equipmentItems.userId} = ${userId} AND ${equipmentItems.id} || ':' || ${equipmentItems.multiplierPercent} IN (${listOf(keys)}) AND NOT EXISTS (SELECT 1 FROM ${equipmentCharacterSlots} WHERE ${equipmentCharacterSlots.userId} = ${equipmentItems.userId} AND ${equipmentCharacterSlots.itemId} = ${equipmentItems.id})) = ${keys.length}`,
      );
    }
    if (request.items.length > 0) {
      const keys = request.items.map((item) => keyOf(item.id, item.count));
      ready.push(
        sql`(SELECT COUNT(*) FROM ${itemHoldings} WHERE ${itemHoldings.userId} = ${userId} AND ${itemHoldings.itemId} || ':' || ${itemHoldings.count} IN (${listOf(keys)}) AND NOT EXISTS (SELECT 1 FROM ${partyCharacterCards} WHERE ${partyCharacterCards.userId} = ${itemHoldings.userId} AND ${partyCharacterCards.itemId} = ${itemHoldings.itemId})) = ${keys.length}`,
      );
    }

    const pay = runeRecord(db, userId, workshopSales, now);
    const consume: Statement[] = [];
    if (request.cardIds.length > 0) {
      consume.push(
        db
          .delete(skillCards)
          .where(
            and(
              eq(skillCards.userId, userId),
              inArray(skillCards.id, request.cardIds),
              pay.written,
            ),
          ),
      );
    }
    if (request.equipmentIds.length > 0) {
      consume.push(
        db
          .delete(equipmentItems)
          .where(
            and(
              eq(equipmentItems.userId, userId),
              inArray(equipmentItems.id, request.equipmentIds),
              pay.written,
            ),
          ),
      );
    }
    if (request.items.length > 0) {
      consume.push(
        db.delete(itemHoldings).where(
          and(
            eq(itemHoldings.userId, userId),
            inArray(
              itemHoldings.itemId,
              request.items.map((item) => item.id),
            ),
            pay.written,
          ),
        ),
      );
    }
    const results = await db.batch([
      db
        .insert(workshopSales)
        .select(
          db
            .select({
              id: sql<string>`${pay.id}`.as("id"),
              userId: sql<string>`${userId}`.as("user_id"),
              requestId: sql<string>`${request.requestId}`.as("request_id"),
              runes: sql<number>`${runes}`.as("runes"),
              balanceAfter: sql<number>`0`.as("balance_after"),
              goods: sql<string>`${JSON.stringify(goodsOf(request))}`.as(
                "goods",
              ),
              createdAt: sql<string>`${now}`.as("created_at"),
            })
            .from(sql`(SELECT 1)`)
            .where(and(...ready)),
        )
        // 同時に届いた同じ要求IDの再送では書かない。
        .onConflictDoNothing(),
      ...pay.credit(runes),
      // 売った物は、記録を書けたときだけ消す。
      ...consume,
      pay.settle,
      findSale(userId, request.requestId),
    ]);

    const row = (results.at(-1) as SaleRow[])[0];
    if (row) return sold(request, row);
    // 書けなかったのは、読んだあとに売る物が変わった（付けた・強化した・使った）ため。
    return { error: "invalid_goods" as const };
  }

  return { sell };
}
