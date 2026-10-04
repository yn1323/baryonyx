import { sql } from "drizzle-orm";
import {
  check,
  foreignKey,
  integer,
  primaryKey,
  sqliteTable,
  text,
  uniqueIndex,
} from "drizzle-orm/sqlite-core";
import { appUsers } from "../accounts/db-schema.js";

// 初期のボーナスを付与済みのユーザー。付与を1回だけにするための印。
export const stepBonusProfiles = sqliteTable("step_bonus_profiles", {
  userId: text("user_id")
    .primaryKey()
    .references(() => appUsers.id),
  createdAt: text("created_at").notNull(),
});

// 持っているボーナス。1種類につき1つで、ランクの高いほうだけを残す。
export const stepBonusHoldings = sqliteTable(
  "step_bonus_holdings",
  {
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    bonusId: text("bonus_id").notNull(),
    rank: text("rank").notNull(),
    acquiredAt: text("acquired_at").notNull(),
    updatedAt: text("updated_at").notNull(),
  },
  (table) => [
    primaryKey({ columns: [table.userId, table.bonusId] }),
    check(
      "step_bonus_holdings_rank_check",
      sql`${table.rank} IN ('E', 'D', 'C', 'B', 'A', 'S')`,
    ),
  ],
);

// 段階ごとの枠に入れたボーナス。持っているボーナスだけを、1つの枠にだけ入れられる。
export const stepBonusSlots = sqliteTable(
  "step_bonus_slots",
  {
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    slot: integer("slot").notNull(),
    bonusId: text("bonus_id").notNull(),
    updatedAt: text("updated_at").notNull(),
  },
  (table) => [
    primaryKey({ columns: [table.userId, table.slot] }),
    uniqueIndex("step_bonus_slots_user_bonus").on(table.userId, table.bonusId),
    foreignKey({
      columns: [table.userId, table.bonusId],
      foreignColumns: [stepBonusHoldings.userId, stepBonusHoldings.bonusId],
    }),
    check(
      "step_bonus_slots_slot_check",
      sql`${table.slot} >= 0 AND ${table.slot} < 5`,
    ),
  ],
);
