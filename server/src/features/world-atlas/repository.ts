import { and, eq, inArray, sql } from "drizzle-orm";
import type { Database } from "../../shared/db.js";
import { runeRecord, selectRuneBalance } from "../exercise-rewards/wallet.js";
import { squareOf } from "./catalog.js";
import { worldAtlasSquares } from "./db-schema.js";
import type { Open } from "./schema.js";

type SquareRow = typeof worldAtlasSquares.$inferSelect;

export function createWorldAtlasRepository(db: Database) {
  const openedRows = (userId: string) =>
    db
      .select({
        map: worldAtlasSquares.mapKey,
        square: worldAtlasSquares.squareId,
      })
      .from(worldAtlasSquares)
      .where(eq(worldAtlasSquares.userId, userId))
      .orderBy(worldAtlasSquares.createdAt, worldAtlasSquares.id);

  const balanceOf = (rows: { balance: number }[]) => rows[0]?.balance ?? 0;

  // 開けたマス（始まりのマスは含めない）と、今の残高を1回の往復で読む。
  async function read(userId: string) {
    const [opened, balance] = await db.batch([
      openedRows(userId),
      selectRuneBalance(db, userId),
    ]);
    return { opened, runes: balanceOf(balance) };
  }

  // まだ開けていないマスを1つ、ルーンと引き換えに開ける。値段とつながりはサーバーの定義で決める。
  // 開けられるのは、始まりのマスか開けたマスの隣だけ。確かめることのうち、残高・隣が開いていること・
  // まだ開けていないこと（一意インデックス）を書き込みの文に入れ、1回のbatchで記録・減算・読み込みを行う。
  // 要求IDで再送を見分け、二重に引かない。別の要求で開いていたマスは、払わずに開いたまま返す。
  async function open(userId: string, request: Open, now: string) {
    const square = squareOf(request.map, request.squareId);
    if (!square) return { error: "square_not_found" as const };

    const neighbours = and(
      eq(worldAtlasSquares.userId, userId),
      eq(worldAtlasSquares.mapKey, request.map),
      inArray(worldAtlasSquares.squareId, square.links),
    );
    const reachable = square.touchesStart
      ? sql`1`
      : sql`EXISTS (SELECT 1 FROM ${worldAtlasSquares} WHERE ${neighbours})`;
    const pay = runeRecord(db, userId, worldAtlasSquares, now);
    const results = await db.batch([
      db
        .insert(worldAtlasSquares)
        .select(
          db
            .select({
              id: sql<string>`${pay.id}`.as("id"),
              userId: sql<string>`${userId}`.as("user_id"),
              requestId: sql<string>`${request.requestId}`.as("request_id"),
              mapKey: sql<string>`${request.map}`.as("map_key"),
              squareId: sql<string>`${request.squareId}`.as("square_id"),
              runes: sql<number>`${square.price}`.as("runes"),
              balanceAfter: sql<number>`0`.as("balance_after"),
              createdAt: sql<string>`${now}`.as("created_at"),
            })
            .from(sql`(SELECT 1)`)
            .where(and(pay.affords(square.price), reachable)),
        )
        // 同じ要求IDの再送と、別の要求で開いたマスでは書かない。
        .onConflictDoNothing(),
      pay.debit(square.price),
      pay.settle,
      // 書けなかった理由を見分けるため、要求IDの記録・そのマスの行・隣の開けたマス・残高を読む。
      db
        .select()
        .from(worldAtlasSquares)
        .where(
          and(
            eq(worldAtlasSquares.userId, userId),
            eq(worldAtlasSquares.requestId, request.requestId),
          ),
        ),
      db
        .select({ id: worldAtlasSquares.id })
        .from(worldAtlasSquares)
        .where(
          and(
            eq(worldAtlasSquares.userId, userId),
            eq(worldAtlasSquares.mapKey, request.map),
            eq(worldAtlasSquares.squareId, request.squareId),
          ),
        ),
      db
        .select({ id: worldAtlasSquares.id })
        .from(worldAtlasSquares)
        .where(neighbours)
        .limit(1),
      selectRuneBalance(db, userId),
    ]);
    const [record] = results[3] as SquareRow[];
    const balance = balanceOf(results[6] as { balance: number }[]);
    if (record) {
      if (
        record.mapKey !== request.map ||
        record.squareId !== request.squareId
      ) {
        return { error: "request_conflict" as const };
      }
      return {
        map: record.mapKey,
        squareId: record.squareId,
        runes: record.runes,
        balanceAfter: record.balanceAfter,
      };
    }
    if ((results[4] as unknown[]).length > 0) {
      return {
        map: request.map,
        squareId: request.squareId,
        runes: 0,
        balanceAfter: balance,
      };
    }
    if (!square.touchesStart && (results[5] as unknown[]).length === 0) {
      return { error: "square_not_reachable" as const };
    }
    return { error: "insufficient_runes" as const };
  }

  return { read, open };
}
