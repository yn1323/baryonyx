import { sql } from "drizzle-orm";
import {
  check,
  index,
  integer,
  primaryKey,
  sqliteTable,
  text,
  uniqueIndex,
} from "drizzle-orm/sqlite-core";
import { appUsers } from "../accounts/db-schema.js";

// 冒険1回ごとの状態。進行中（active）の冒険はユーザーごとに1つだけ。
// 部屋を選んだ時点で保存するため、戦闘中にアプリが終わっても、選んだ部屋から再開できる。
export const adventureRuns = sqliteTable(
  "adventure_runs",
  {
    id: text("id").primaryKey(),
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    destinationId: text("destination_id").notNull(),
    roomId: text("room_id").notNull(),
    // 今いる部屋の出来事（戦闘・宝箱）を終えたか。終えるまで次の部屋へ進めない。
    roomCleared: integer("room_cleared", { mode: "boolean" }).notNull(),
    // 通った部屋のID（JSONの配列）。最初は出発の部屋だけ。
    route: text("route").notNull(),
    revives: integer("revives").notNull().default(0),
    status: text("status").notNull(),
    startedAt: text("started_at").notNull(),
    updatedAt: text("updated_at").notNull(),
    endedAt: text("ended_at"),
  },
  (table) => [
    uniqueIndex("adventure_runs_user_active")
      .on(table.userId)
      .where(sql`${table.status} = 'active'`),
    index("adventure_runs_user_started").on(table.userId, table.startedAt),
    check(
      "adventure_runs_status_check",
      sql`${table.status} IN ('active', 'cleared', 'defeated', 'retreated')`,
    ),
    check("adventure_runs_revives_check", sql`${table.revives} >= 0`),
  ],
);

// 出来事を終えた部屋で手に入れたUPTボーナス。1つの部屋で1回だけ手に入る。
// 持ち物（step_bonus_holdings）には、ランクの高いほうだけが残る（outcome）。
export const adventureRewards = sqliteTable(
  "adventure_rewards",
  {
    runId: text("run_id")
      .notNull()
      .references(() => adventureRuns.id),
    roomId: text("room_id").notNull(),
    bonusId: text("bonus_id").notNull(),
    rank: text("rank").notNull(),
    outcome: text("outcome").notNull(),
    createdAt: text("created_at").notNull(),
  },
  (table) => [
    primaryKey({ columns: [table.runId, table.roomId] }),
    check(
      "adventure_rewards_rank_check",
      sql`${table.rank} IN ('E', 'D', 'C', 'B', 'A', 'S')`,
    ),
    check(
      "adventure_rewards_outcome_check",
      sql`${table.outcome} IN ('added', 'updated', 'discarded')`,
    ),
  ],
);

// ルーンでの復活1回ごとの記録。使ったルーンの記録を兼ね、要求IDで再送を見分ける。
// 残高（rune_wallets）は、ルーンの台帳の合計から、レベルアップと復活で使った合計を引いた値になる。
export const adventureRevives = sqliteTable(
  "adventure_revives",
  {
    id: text("id").primaryKey(),
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    runId: text("run_id")
      .notNull()
      .references(() => adventureRuns.id),
    requestId: text("request_id").notNull(),
    roomId: text("room_id").notNull(),
    runes: integer("runes").notNull(),
    balanceAfter: integer("balance_after").notNull(),
    createdAt: text("created_at").notNull(),
  },
  (table) => [
    uniqueIndex("adventure_revives_user_request").on(
      table.userId,
      table.requestId,
    ),
    check("adventure_revives_runes_check", sql`${table.runes} > 0`),
    check(
      "adventure_revives_balance_after_check",
      sql`${table.balanceAfter} >= 0`,
    ),
  ],
);

// 冒険先ごとの記録。最深到達の階と、深奥のボスを倒した回数。
export const adventureRecords = sqliteTable(
  "adventure_records",
  {
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    destinationId: text("destination_id").notNull(),
    bestFloor: integer("best_floor").notNull(),
    clears: integer("clears").notNull().default(0),
    updatedAt: text("updated_at").notNull(),
  },
  (table) => [
    primaryKey({ columns: [table.userId, table.destinationId] }),
    check("adventure_records_best_floor_check", sql`${table.bestFloor} >= 1`),
    check("adventure_records_clears_check", sql`${table.clears} >= 0`),
  ],
);
