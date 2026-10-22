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
// 分岐ルートの道は保存せず、乱数の種（seed）からクライアントが毎回同じ形に作る。
export const adventureRuns = sqliteTable(
  "adventure_runs",
  {
    id: text("id").primaryKey(),
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    destinationId: text("destination_id").notNull(),
    seed: integer("seed").notNull().default(0),
    roomId: text("room_id").notNull(),
    // 今いる部屋の階と種類。クライアントが道から送った値で、進み方と報酬の判定に使う。
    floor: integer("floor").notNull().default(1),
    roomKind: text("room_kind").notNull().default("start"),
    // 今いる部屋の出来事（戦闘・宝箱）を終えたか。終えるまで次の部屋へ進めない。
    roomCleared: integer("room_cleared", { mode: "boolean" }).notNull(),
    // 今いるマスで決まったこと。マスを移ると消す（doc/features/stage-progression.md のマスの中身）。
    // mimic は怪しい宝箱を開けた結果（開ける前は null）。true のあいだ、そのマスは戦闘のマスになる。
    // trial は試練のマスに入ったときに選んだ条件。door_steps は歩数の扉を開けたときの今日の歩数。
    mimic: integer("mimic", { mode: "boolean" }),
    trial: text("trial"),
    doorSteps: integer("door_steps"),
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

// 出来事を終えた部屋で落ちた物（doc/features/drops.md）。1つの部屋で1回だけ抽選し、落ちた数だけ行を持つ（seq は0から）。
// grant_id は抽選1回ごとのIDで、部屋を終えた書き込みが条件どおり入ったかを見分ける。
// 倍率は百分率の整数で、アイテムは持たない（null）。ACTボーナスは、持ち物に倍率の高いほうだけが残る（outcome）。
// ほかの物は落ちるたびに持ち物へ足すため、outcome は added になる。
export const adventureDrops = sqliteTable(
  "adventure_drops",
  {
    runId: text("run_id")
      .notNull()
      .references(() => adventureRuns.id),
    roomId: text("room_id").notNull(),
    seq: integer("seq").notNull(),
    grantId: text("grant_id").notNull(),
    category: text("category").notNull(),
    entryId: text("entry_id").notNull(),
    multiplierPercent: integer("multiplier_percent"),
    outcome: text("outcome").notNull(),
    createdAt: text("created_at").notNull(),
  },
  (table) => [
    primaryKey({ columns: [table.runId, table.roomId, table.seq] }),
    check(
      "adventure_drops_category_check",
      sql`${table.category} IN ('item', 'accessory', 'weapon', 'armor', 'skill', 'bonus')`,
    ),
    check(
      "adventure_drops_multiplier_check",
      sql`(${table.category} = 'item' AND ${table.multiplierPercent} IS NULL) OR (${table.category} <> 'item' AND ${table.multiplierPercent} >= 100)`,
    ),
    check(
      "adventure_drops_outcome_check",
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

// 行商人のマスの品物（doc/features/stage-progression.md のマスの中身）。マスに入ったときに作り、1つのマスに並べた数だけ行を持つ（idx は0から）。
// 倍率は百分率の整数。値段はルーンで、作ったときの☆と品質で決める。売れたかは adventure_purchases の行で分かる。
export const adventureMerchantGoods = sqliteTable(
  "adventure_merchant_goods",
  {
    runId: text("run_id")
      .notNull()
      .references(() => adventureRuns.id),
    roomId: text("room_id").notNull(),
    idx: integer("idx").notNull(),
    category: text("category").notNull(),
    entryId: text("entry_id").notNull(),
    multiplierPercent: integer("multiplier_percent").notNull(),
    price: integer("price").notNull(),
    createdAt: text("created_at").notNull(),
  },
  (table) => [
    primaryKey({ columns: [table.runId, table.roomId, table.idx] }),
    check(
      "adventure_merchant_goods_category_check",
      sql`${table.category} IN ('skill', 'weapon', 'armor', 'accessory')`,
    ),
    check(
      "adventure_merchant_goods_multiplier_check",
      sql`${table.multiplierPercent} >= 100`,
    ),
    check("adventure_merchant_goods_price_check", sql`${table.price} > 0`),
  ],
);

// 行商人での購入1回ごとの記録。使ったルーンの記録を兼ね、要求IDで再送を、品物ごとの一意で二重の購入を見分ける。
// 残高（rune_wallets）は、復活と同じく使った記録を書けたときだけ減らす。
export const adventurePurchases = sqliteTable(
  "adventure_purchases",
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
    idx: integer("idx").notNull(),
    runes: integer("runes").notNull(),
    balanceAfter: integer("balance_after").notNull(),
    createdAt: text("created_at").notNull(),
  },
  (table) => [
    uniqueIndex("adventure_purchases_user_request").on(
      table.userId,
      table.requestId,
    ),
    uniqueIndex("adventure_purchases_goods").on(
      table.runId,
      table.roomId,
      table.idx,
    ),
    check("adventure_purchases_runes_check", sql`${table.runes} > 0`),
    check(
      "adventure_purchases_balance_after_check",
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
