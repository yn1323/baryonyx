import { sql } from "drizzle-orm";
import { check, primaryKey, sqliteTable, text } from "drizzle-orm/sqlite-core";
import { appUsers } from "../accounts/db-schema.js";

// 戦闘で開示した敵の弱点。敵の種類と属性の組ごとに1行で、伏せ直さない。
export const combatRevealedWeaknesses = sqliteTable(
  "combat_revealed_weaknesses",
  {
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    enemyId: text("enemy_id").notNull(),
    element: text("element").notNull(),
    revealedAt: text("revealed_at").notNull(),
  },
  (table) => [
    primaryKey({ columns: [table.userId, table.enemyId, table.element] }),
    check(
      "combat_revealed_weaknesses_element_check",
      sql`${table.element} IN ('slash', 'fire', 'ice', 'thunder', 'blunt', 'pierce')`,
    ),
  ],
);
