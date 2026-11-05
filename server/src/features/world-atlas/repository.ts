import { and, eq, inArray, sql } from "drizzle-orm";
import { batchThenRead, type Database } from "../../shared/db.js";
import { multiplierOf, percentRange } from "../../shared/quality.js";
import { runeRecord, selectRuneBalance } from "../exercise-rewards/wallet.js";
import { maxPercentOf } from "../inventory/catalog.js";
import { inventoryGrants } from "../inventory/grants.js";
import type { ChestEntry } from "../world-map/catalog.js";
import { chestOf, squareOf } from "./catalog.js";
import { worldAtlasSquares } from "./db-schema.js";
import type { Open } from "./schema.js";

type SquareRow = typeof worldAtlasSquares.$inferSelect;

// 宝箱の1つの中身の倍率（百分率の整数）。決めた品質の幅の下端にする。強化素材は倍率を持たない。
function percentOf(entry: ChestEntry): number | null {
  if (entry.category === "material") return null;
  return percentRange(
    entry.quality,
    maxPercentOf(entry.category, entry.id) as number,
  ).min;
}

// 応答の1つの中身。街道の宝箱（world-map の chestDrop）と同じ形にし、強化素材は倍率と品質の項目を省く
// （UnityのJsonUtilityが数値の null を読めないため）。
function chestDrop(entry: ChestEntry) {
  const percent = percentOf(entry);
  return {
    category: entry.category,
    id: entry.id,
    ...(percent === null || entry.category === "material"
      ? {}
      : { multiplier: multiplierOf(percent), quality: entry.quality }),
  };
}

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
  // 宝箱のマスは、この要求で開けたとき（記録の行を書けたとき）だけ、同じbatchで中身を持ち物に足す。
  // 開けた応答には中身（chest）を添える。再送や別の要求で開いていたときも添えるが、持ち物には足さない。
  async function open(userId: string, request: Open, now: string) {
    const square = squareOf(request.map, request.squareId);
    if (!square) return { error: "square_not_found" as const };
    const chest = chestOf(request.map, request.squareId);
    // 消費を止めた開発用の端末からは、払わずに開ける（受け付けるかはルートで確かめる）。
    const price = request.free ? 0 : square.price;

    const neighbours = and(
      eq(worldAtlasSquares.userId, userId),
      eq(worldAtlasSquares.mapKey, request.map),
      inArray(worldAtlasSquares.squareId, square.links),
    );
    // 消費を止めた開発用の端末は、つながりを確かめずに開ける（止めて歩いた位置がサーバーとずれていても進めるように）。
    const reachable =
      square.touchesStart || request.free
        ? sql`1`
        : sql`EXISTS (SELECT 1 FROM ${worldAtlasSquares} WHERE ${neighbours})`;
    const pay = runeRecord(db, userId, worldAtlasSquares, now);
    const {
      reads: [records, squares, opened, balanceRows],
    } = await batchThenRead(
      db,
      [
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
                runes: sql<number>`${price}`.as("runes"),
                balanceAfter: sql<number>`0`.as("balance_after"),
                createdAt: sql<string>`${now}`.as("created_at"),
              })
              .from(sql`(SELECT 1)`)
              .where(
                and(request.free ? sql`1` : pay.affords(price), reachable),
              ),
          )
          // 同じ要求IDの再送と、別の要求で開いたマスでは書かない。
          .onConflictDoNothing(),
        pay.debit(price),
        pay.settle,
        // 宝箱の中身は、この呼び出しで入れた記録の行があるときだけ足す。
        ...inventoryGrants(
          db,
          pay.written,
          userId,
          (chest ?? []).map((entry) => ({
            category: entry.category,
            id: entry.id,
            multiplierPercent: percentOf(entry),
          })),
          now,
        ),
      ],
      [
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
      ],
    );
    const [record] = records as SquareRow[];
    const chestField = chest ? { chest: chest.map(chestDrop) } : {};
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
        ...chestField,
      };
    }
    if (squares.length > 0) {
      return {
        map: request.map,
        squareId: request.squareId,
        runes: 0,
        balanceAfter: balanceOf(balanceRows),
        ...chestField,
      };
    }
    if (!square.touchesStart && opened.length === 0) {
      return { error: "square_not_reachable" as const };
    }
    return { error: "insufficient_runes" as const };
  }

  return { read, open };
}
