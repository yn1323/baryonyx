import { primaryKey, sqliteTable, text } from "drizzle-orm/sqlite-core";
import { appUsers } from "../accounts/db-schema.js";

// 街道のクリアしたマス。マスごとに1行で、クリアを取り消さない。最初の街は行を持たずにクリア済みとする。
export const worldMapSquares = sqliteTable(
  "world_map_squares",
  {
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    squareId: text("square_id").notNull(),
    clearedAt: text("cleared_at").notNull(),
  },
  (table) => [primaryKey({ columns: [table.userId, table.squareId] })],
);

// 街道でパーティが今いるマス。ユーザーごとに1行で、まだ動いていなければ行がない（最初の街にいる）。
export const worldMapPositions = sqliteTable("world_map_positions", {
  userId: text("user_id")
    .primaryKey()
    .references(() => appUsers.id),
  squareId: text("square_id").notNull(),
  updatedAt: text("updated_at").notNull(),
});
