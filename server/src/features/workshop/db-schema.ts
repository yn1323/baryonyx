import { sql } from "drizzle-orm";
import {
  check,
  integer,
  sqliteTable,
  text,
  uniqueIndex,
} from "drizzle-orm/sqlite-core";
import { appUsers } from "../accounts/db-schema.js";

// 商会で買った1回ごとの記録。使ったルーンの記録を兼ね、要求IDで再送を見分ける。
// 残高（rune_wallets）は、行商人の購入と同じく、この記録を書けたときだけ減らす。
// item_id は、持ち物に足した行（スキルは skill_cards、装備は equipment_items）のID。
export const workshopPurchases = sqliteTable(
  "workshop_purchases",
  {
    id: text("id").primaryKey(),
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    requestId: text("request_id").notNull(),
    category: text("category").notNull(),
    entryId: text("entry_id").notNull(),
    itemId: text("item_id").notNull(),
    runes: integer("runes").notNull(),
    balanceAfter: integer("balance_after").notNull(),
    createdAt: text("created_at").notNull(),
  },
  (table) => [
    uniqueIndex("workshop_purchases_user_request").on(
      table.userId,
      table.requestId,
    ),
    check(
      "workshop_purchases_category_check",
      sql`${table.category} IN ('skill', 'weapon', 'armor', 'accessory')`,
    ),
    check("workshop_purchases_runes_check", sql`${table.runes} > 0`),
    check(
      "workshop_purchases_balance_after_check",
      sql`${table.balanceAfter} >= 0`,
    ),
  ],
);

// 商会で合成した1回ごとの記録。使ったルーンと材料の記録を兼ね、要求IDで再送を見分ける。
// 残高（rune_wallets）・材料・持ち物は、この記録を書けたときだけ変える。
// item_id は、持ち物に足した行（スキルは skill_cards、装備は equipment_items）のID。アイテムは種類ごとに
// 数を持つため、アイテムの種類のID（entry_id と同じ）。multiplier_percent は出来上がった物の倍率で、
// アイテムは倍率を持たないため null。materials は使った材料のJSONで、スキル・装備は消した行のIDの配列、
// アイテムは種類ごとの数（{ id, count } の配列）。
export const workshopCrafts = sqliteTable(
  "workshop_crafts",
  {
    id: text("id").primaryKey(),
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    requestId: text("request_id").notNull(),
    category: text("category").notNull(),
    entryId: text("entry_id").notNull(),
    itemId: text("item_id").notNull(),
    multiplierPercent: integer("multiplier_percent"),
    runes: integer("runes").notNull(),
    balanceAfter: integer("balance_after").notNull(),
    materials: text("materials").notNull(),
    createdAt: text("created_at").notNull(),
  },
  (table) => [
    uniqueIndex("workshop_crafts_user_request").on(
      table.userId,
      table.requestId,
    ),
    check(
      "workshop_crafts_category_check",
      sql`${table.category} IN ('skill', 'weapon', 'armor', 'accessory', 'item')`,
    ),
    check(
      "workshop_crafts_multiplier_check",
      sql`${table.multiplierPercent} IS NULL OR ${table.multiplierPercent} >= 100`,
    ),
    check("workshop_crafts_runes_check", sql`${table.runes} > 0`),
    check(
      "workshop_crafts_balance_after_check",
      sql`${table.balanceAfter} >= 0`,
    ),
  ],
);
