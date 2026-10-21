import { sql } from "drizzle-orm";
import {
  check,
  integer,
  primaryKey,
  sqliteTable,
  text,
} from "drizzle-orm/sqlite-core";
import { appUsers } from "../accounts/db-schema.js";

// 持っているアイテム。同じ種類は同じ効果のため、種類ごとに1行で数を持つ（doc/features/items.md の数え方）。
export const itemHoldings = sqliteTable(
  "item_holdings",
  {
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    itemId: text("item_id").notNull(),
    count: integer("count").notNull(),
    acquiredAt: text("acquired_at").notNull(),
    updatedAt: text("updated_at").notNull(),
  },
  (table) => [
    primaryKey({ columns: [table.userId, table.itemId] }),
    check("item_holdings_count_check", sql`${table.count} >= 1`),
  ],
);
