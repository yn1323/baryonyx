import { and, asc, eq, sql } from "drizzle-orm";
import type { BatchResponse } from "drizzle-orm/batch";
import {
  batchThenRead,
  createDatabase,
  type Database,
  type Statement,
} from "../../shared/db.js";
import { multiplierOf, percentRange } from "../../shared/quality.js";
import { maxMultiplierPercent as equipmentMaxPercent } from "../equipment/catalog.js";
import { inventoryGrants } from "../inventory/grants.js";
import { maxMultiplierPercent as skillMaxPercent } from "../party/catalog.js";
import { type ChestEntry, chestOf, START_SQUARE } from "./catalog.js";
import { worldMapPositions, worldMapSquares } from "./db-schema.js";
import type { Move } from "./schema.js";

// 宝箱の1つの中身の倍率（百分率の整数）。決めた品質の幅の下端にする。強化素材は倍率を持たない。
function percentOf(entry: ChestEntry): number | null {
  switch (entry.category) {
    case "material":
      return null;
    case "skill":
      return percentRange(entry.quality, skillMaxPercent(entry.id)).min;
    default:
      return percentRange(entry.quality, equipmentMaxPercent(entry.id)).min;
  }
}

// 応答の1つの中身。冒険のドロップと同じ形にし、強化素材は倍率と品質の項目を省く
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

// 今いるマスと、クリアしたマスを読む文。
function stateReads(db: Database, userId: string) {
  return [
    db
      .select({ squareId: worldMapPositions.squareId })
      .from(worldMapPositions)
      .where(eq(worldMapPositions.userId, userId)),
    db
      .select({ squareId: worldMapSquares.squareId })
      .from(worldMapSquares)
      .where(eq(worldMapSquares.userId, userId))
      .orderBy(asc(worldMapSquares.clearedAt), asc(worldMapSquares.squareId)),
  ] as const;
}

// 今いるマスと、クリアしたマスをクリアした順に返す。最初の街はいつも先頭に入れる。
function stateOf([positions, squares]: BatchResponse<
  ReturnType<typeof stateReads>
>) {
  const cleared = [
    START_SQUARE,
    ...squares
      .map((square) => square.squareId)
      .filter((id) => id !== START_SQUARE),
  ];
  return { at: positions[0]?.squareId ?? START_SQUARE, cleared };
}

export function createWorldMapRepository(binding: D1Database) {
  const db = createDatabase(binding);

  // statements のあとに世界地図を読む文を足し、1回の往復で書いて読む。
  async function writeAndRead(
    userId: string,
    statements: readonly Statement[],
  ) {
    const { reads } = await batchThenRead(
      db,
      statements,
      stateReads(db, userId),
    );
    return stateOf(reads);
  }

  async function read(userId: string) {
    return writeAndRead(userId, []);
  }

  // 着いたマスを今いるマスにし、クリアしたならクリアしたマスへ足す。
  // 同じ移動を送り直しても、今いるマスは同じで、クリアは最初の日時のまま残る。
  // 宝箱のマスをクリアしたときは、初めてクリアしたときだけ中身を持ち物に足し、応答に中身を添える。
  // 送り直しでも中身は添えるが、持ち物には足さない。書き込みと読み込みを1回の往復で済ませる。
  async function move(userId: string, move: Move, now: string) {
    const position = db
      .insert(worldMapPositions)
      .values({ userId, squareId: move.squareId, updatedAt: now })
      .onConflictDoUpdate({
        target: worldMapPositions.userId,
        set: { squareId: move.squareId, updatedAt: now },
      });
    if (!move.clear || move.squareId === START_SQUARE) {
      return writeAndRead(userId, [position]);
    }
    const chest = chestOf(move.squareId);
    const grantId = chest ? crypto.randomUUID() : null;
    const square = db
      .insert(worldMapSquares)
      .values({ userId, squareId: move.squareId, clearedAt: now, grantId })
      .onConflictDoNothing();
    if (!chest) return writeAndRead(userId, [position, square]);
    const written = sql`EXISTS (SELECT 1 FROM ${worldMapSquares} WHERE ${and(
      eq(worldMapSquares.userId, userId),
      eq(worldMapSquares.squareId, move.squareId),
      eq(worldMapSquares.grantId, grantId as string),
    )})`;
    const state = await writeAndRead(userId, [
      position,
      square,
      ...inventoryGrants(
        db,
        written,
        userId,
        chest.map((entry) => ({
          category: entry.category,
          id: entry.id,
          multiplierPercent: percentOf(entry),
        })),
        now,
      ),
    ]);
    return { ...state, chest: chest.map(chestDrop) };
  }

  return { read, move };
}
