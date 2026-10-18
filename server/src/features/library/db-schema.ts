import { sql } from "drizzle-orm";
import {
  check,
  integer,
  primaryKey,
  sqliteTable,
  text,
} from "drizzle-orm/sqlite-core";
import { appUsers } from "../accounts/db-schema.js";

// ライブラリの見つけた記録。持ち物の所持とは別に持ち、売ったり合成に使ったりしても消さない。
// 倒した数は敵だけが使い、ほかの分類は0のまま。
export const libraryDiscoveries = sqliteTable(
  "library_discoveries",
  {
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    kind: text("kind").notNull(),
    entryId: text("entry_id").notNull(),
    discoveredAt: text("discovered_at").notNull(),
    defeated: integer("defeated").notNull().default(0),
  },
  (table) => [
    primaryKey({ columns: [table.userId, table.kind, table.entryId] }),
    check(
      "library_discoveries_kind_check",
      sql`${table.kind} IN ('character', 'weapon', 'armor', 'accessory', 'skill', 'item', 'enemy')`,
    ),
    check("library_discoveries_defeated_check", sql`${table.defeated} >= 0`),
  ],
);
