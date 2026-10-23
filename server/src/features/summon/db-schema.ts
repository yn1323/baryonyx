import { sql } from "drizzle-orm";
import {
  check,
  integer,
  primaryKey,
  sqliteTable,
  text,
  uniqueIndex,
} from "drizzle-orm/sqlite-core";
import { appUsers } from "../accounts/db-schema.js";

// 神殿で召喚した1回ごとの記録（1回召喚・10回召喚の1回の押下で1行）。使ったルーンの記録を兼ね、
// 要求IDで再送を見分ける。残高（rune_wallets）と持ち物は、この記録を書けたときだけ変える。
// results は出た物のJSON（分類・ID・☆・倍率・足した持ち物の行のID・絆・かけら）で、再送にはこれを返す。
export const summonPulls = sqliteTable(
  "summon_pulls",
  {
    id: text("id").primaryKey(),
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    requestId: text("request_id").notNull(),
    count: integer("count").notNull(),
    runes: integer("runes").notNull(),
    results: text("results").notNull(),
    balanceAfter: integer("balance_after").notNull(),
    createdAt: text("created_at").notNull(),
  },
  (table) => [
    uniqueIndex("summon_pulls_user_request").on(table.userId, table.requestId),
    check("summon_pulls_count_check", sql`${table.count} IN (1, 10)`),
    check("summon_pulls_runes_check", sql`${table.runes} > 0`),
    check("summon_pulls_balance_after_check", sql`${table.balanceAfter} >= 0`),
  ],
);

// キャラの絆（doc/features/monetization.md のキャラが被ったとき）。持っているキャラが被るたびに絆のしるし
// （tokens）が1つ増え、育成の画面でしるしを1つ使うと絆（bond）が1段上がる。段としるしの合計は上限（6段）まで。
// 行がなければ0段でしるしもない。
export const characterBonds = sqliteTable(
  "character_bonds",
  {
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    characterId: text("character_id").notNull(),
    bond: integer("bond").notNull(),
    tokens: integer("tokens").notNull().default(0),
    updatedAt: text("updated_at").notNull(),
  },
  (table) => [
    primaryKey({ columns: [table.userId, table.characterId] }),
    check("character_bonds_bond_check", sql`${table.bond} BETWEEN 0 AND 6`),
    check(
      "character_bonds_tokens_check",
      sql`${table.tokens} >= 0 AND ${table.bond} + ${table.tokens} <= 6`,
    ),
  ],
);

// 全キャラ共通のかけら。絆の段としるしの合計が上限に達したあとに被った分が入る。神殿の交換所で使う（交換所は未実装）。
export const summonShards = sqliteTable(
  "summon_shards",
  {
    userId: text("user_id")
      .primaryKey()
      .references(() => appUsers.id),
    shards: integer("shards").notNull(),
    updatedAt: text("updated_at").notNull(),
  },
  (table) => [check("summon_shards_shards_check", sql`${table.shards} >= 0`)],
);
