import { and, eq, type SQL, sql } from "drizzle-orm";
import type { createDatabase } from "../../shared/db.js";
import { runeWallets } from "./db-schema.js";

type Database = ReturnType<typeof createDatabase>;

// ほかの機能（レベルアップ・復活・商会）が所持ルーンを読む・使う・得るための文と式。
// 使うときは、使った記録を書けたときだけ残高を減らし、記録へ減らしたあとの残高を書き戻す。得るときも同じ。

/** 今の残高を読む文。行がなければ空の結果になる（残高0として扱う）。 */
export function selectRuneBalance(db: Database, userId: string) {
  return db
    .select({ balance: runeWallets.balance })
    .from(runeWallets)
    .where(eq(runeWallets.userId, userId));
}

/** 今の残高のSQLの式。行がなければNULL。 */
export function runeBalance(userId: string) {
  return sql`(SELECT ${runeWallets.balance} FROM ${runeWallets} WHERE ${runeWallets.userId} = ${userId})`;
}

/** 使った記録（written）を書けたときだけ、残高から runes を減らす文。 */
export function debitRunes(
  db: Database,
  userId: string,
  runes: number,
  written: SQL,
  now: string,
) {
  return db
    .update(runeWallets)
    .set({ balance: sql`${runeWallets.balance} - ${runes}`, updatedAt: now })
    .where(and(eq(runeWallets.userId, userId), written));
}

/** 得た記録（written）を書けたときだけ、残高へ runes を足す文。残高の行がなければ作ってから足す。 */
export function creditRunes(
  db: Database,
  userId: string,
  runes: number,
  written: SQL,
  now: string,
) {
  return [
    db
      .insert(runeWallets)
      .select(
        db
          .select({
            userId: sql<string>`${userId}`.as("user_id"),
            balance: sql<number>`0`.as("balance"),
            updatedAt: sql<string>`${now}`.as("updated_at"),
          })
          .from(sql`(SELECT 1)`)
          .where(written),
      )
      .onConflictDoNothing({ target: runeWallets.userId }),
    db
      .update(runeWallets)
      .set({ balance: sql`${runeWallets.balance} + ${runes}`, updatedAt: now })
      .where(and(eq(runeWallets.userId, userId), written)),
  ] as const;
}
