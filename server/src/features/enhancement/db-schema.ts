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

// 持っている強化素材（磨き石・覚醒の欠片）。種類ごとに1行で数を持つ（doc/features/enhancement.md の強化素材）。
// 使いきっても行を残し、数を0にする。
export const enhancementMaterials = sqliteTable(
  "enhancement_materials",
  {
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    materialId: text("material_id").notNull(),
    count: integer("count").notNull(),
    acquiredAt: text("acquired_at").notNull(),
    updatedAt: text("updated_at").notNull(),
  },
  (table) => [
    primaryKey({ columns: [table.userId, table.materialId] }),
    check("enhancement_materials_count_check", sql`${table.count} >= 0`),
  ],
);

// 商会での強化1回ごとの記録。使った素材とルーンの記録を兼ね、要求IDで再送を見分ける。
// kind は polish（品質強化）か awaken（覚醒）。target は強化した物で、スキル・装備は持ち物の行のID、
// 固有スキルは「キャラID:番号」。before・after は、品質強化なら倍率（百分率）、覚醒なら段階。
// 残高（rune_wallets）と素材の数は、この記録を書けたときだけ減らす。
export const enhancementRecords = sqliteTable(
  "enhancement_records",
  {
    id: text("id").primaryKey(),
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    requestId: text("request_id").notNull(),
    kind: text("kind").notNull(),
    category: text("category").notNull(),
    target: text("target").notNull(),
    materialId: text("material_id").notNull(),
    materialCount: integer("material_count").notNull(),
    before: integer("before").notNull(),
    after: integer("after").notNull(),
    runes: integer("runes").notNull(),
    balanceAfter: integer("balance_after").notNull(),
    createdAt: text("created_at").notNull(),
  },
  (table) => [
    uniqueIndex("enhancement_records_user_request").on(
      table.userId,
      table.requestId,
    ),
    check(
      "enhancement_records_kind_check",
      sql`${table.kind} IN ('polish', 'awaken')`,
    ),
    check(
      "enhancement_records_category_check",
      sql`${table.category} IN ('skill', 'weapon', 'armor', 'accessory', 'unique')`,
    ),
    check(
      "enhancement_records_material_count_check",
      sql`${table.materialCount} > 0`,
    ),
    check("enhancement_records_runes_check", sql`${table.runes} > 0`),
    check(
      "enhancement_records_balance_after_check",
      sql`${table.balanceAfter} >= 0`,
    ),
  ],
);
