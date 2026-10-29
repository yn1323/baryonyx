import { sql } from "drizzle-orm";
import {
  check,
  integer,
  sqliteTable,
  text,
  uniqueIndex,
} from "drizzle-orm/sqlite-core";
import { appUsers } from "../accounts/db-schema.js";

// 入れ子の地図で、ルーンを払って開けたマス（doc/features/world-atlas.md#マスを開くルーン）。
// マスごとに1行で、払ったルーンの記録を兼ね、要求IDで再送を見分ける。残高（rune_wallets）は、
// この行を書けたときだけ減らす。同じマスは1度しか開かず、各地図の始まりのマスは行を持たずに開いている。
export const worldAtlasSquares = sqliteTable(
  "world_atlas_squares",
  {
    id: text("id").primaryKey(),
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    requestId: text("request_id").notNull(),
    mapKey: text("map_key").notNull(),
    squareId: text("square_id").notNull(),
    runes: integer("runes").notNull(),
    balanceAfter: integer("balance_after").notNull(),
    createdAt: text("created_at").notNull(),
  },
  (table) => [
    uniqueIndex("world_atlas_squares_user_request").on(
      table.userId,
      table.requestId,
    ),
    uniqueIndex("world_atlas_squares_user_square").on(
      table.userId,
      table.mapKey,
      table.squareId,
    ),
    check("world_atlas_squares_runes_check", sql`${table.runes} > 0`),
    check(
      "world_atlas_squares_balance_after_check",
      sql`${table.balanceAfter} >= 0`,
    ),
  ],
);
