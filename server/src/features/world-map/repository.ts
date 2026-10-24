import { asc, eq } from "drizzle-orm";
import { createDatabase } from "../../shared/db.js";
import { START_SQUARE } from "./catalog.js";
import { worldMapPositions, worldMapSquares } from "./db-schema.js";
import type { Move } from "./schema.js";

export function createWorldMapRepository(binding: D1Database) {
  const db = createDatabase(binding);

  // 今いるマスと、クリアしたマスをクリアした順に返す。最初の街はいつも先頭に入れる。
  async function read(userId: string) {
    const [positions, squares] = await db.batch([
      db
        .select({ squareId: worldMapPositions.squareId })
        .from(worldMapPositions)
        .where(eq(worldMapPositions.userId, userId)),
      db
        .select({ squareId: worldMapSquares.squareId })
        .from(worldMapSquares)
        .where(eq(worldMapSquares.userId, userId))
        .orderBy(asc(worldMapSquares.clearedAt), asc(worldMapSquares.squareId)),
    ]);
    const cleared = [
      START_SQUARE,
      ...squares
        .map((square) => square.squareId)
        .filter((id) => id !== START_SQUARE),
    ];
    return { at: positions[0]?.squareId ?? START_SQUARE, cleared };
  }

  // 着いたマスを今いるマスにし、クリアしたならクリアしたマスへ足す。
  // 同じ移動を送り直しても、今いるマスは同じで、クリアは最初の日時のまま残る。
  async function move(userId: string, move: Move, now: string) {
    const position = db
      .insert(worldMapPositions)
      .values({ userId, squareId: move.squareId, updatedAt: now })
      .onConflictDoUpdate({
        target: worldMapPositions.userId,
        set: { squareId: move.squareId, updatedAt: now },
      });
    if (move.clear && move.squareId !== START_SQUARE)
      await db.batch([
        position,
        db
          .insert(worldMapSquares)
          .values({ userId, squareId: move.squareId, clearedAt: now })
          .onConflictDoNothing(),
      ]);
    else await position;
    return read(userId);
  }

  return { read, move };
}
