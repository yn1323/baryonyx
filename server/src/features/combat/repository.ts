import { asc, eq } from "drizzle-orm";
import { createDatabase } from "../../shared/db.js";
import { combatRevealedWeaknesses } from "./db-schema.js";
import type { Weakness } from "./schema.js";

export function createCombatRepository(binding: D1Database) {
  const db = createDatabase(binding);

  // 開示した順（同時なら敵・属性の順）に返す。
  async function readWeaknesses(userId: string) {
    const weaknesses = await db
      .select({
        enemyId: combatRevealedWeaknesses.enemyId,
        element: combatRevealedWeaknesses.element,
        revealedAt: combatRevealedWeaknesses.revealedAt,
      })
      .from(combatRevealedWeaknesses)
      .where(eq(combatRevealedWeaknesses.userId, userId))
      .orderBy(
        asc(combatRevealedWeaknesses.revealedAt),
        asc(combatRevealedWeaknesses.enemyId),
        asc(combatRevealedWeaknesses.element),
      )
      .all();
    return { weaknesses };
  }

  // 開示した弱点を足す。記録済みの組は最初に開示した日時のまま残すため、再送しても変わらない。
  async function reveal(userId: string, weaknesses: Weakness[], now: string) {
    await db
      .insert(combatRevealedWeaknesses)
      .values(
        weaknesses.map((weakness) => ({
          userId,
          enemyId: weakness.enemyId,
          element: weakness.element,
          revealedAt: now,
        })),
      )
      .onConflictDoNothing();
    return readWeaknesses(userId);
  }

  return { readWeaknesses, reveal };
}
