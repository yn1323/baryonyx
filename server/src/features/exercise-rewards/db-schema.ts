import { sql } from "drizzle-orm";
import {
  check,
  index,
  integer,
  sqliteTable,
  text,
  uniqueIndex,
} from "drizzle-orm/sqlite-core";
import { appUsers } from "../accounts/db-schema.js";
import { healthSources } from "../health/db-schema.js";

export const exerciseRewardDays = sqliteTable(
  "exercise_reward_days",
  {
    id: text("id").primaryKey(),
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    sourceId: text("source_id")
      .notNull()
      .references(() => healthSources.id),
    day: text("day").notNull(),
    zone: text("zone").notNull(),
    activityType: text("activity_type").notNull(),
    metricType: text("metric_type").notNull(),
    hasValue: integer("has_value", { mode: "boolean" }).notNull(),
    observedValue: integer("observed_value"),
    creditedThroughValue: integer("credited_through_value").notNull(),
    creditedRunes: integer("credited_runes").notNull(),
    ruleVersion: text("rule_version").notNull(),
    lastObservedAt: text("last_observed_at"),
    updatedAt: text("updated_at").notNull(),
  },
  (table) => [
    uniqueIndex("exercise_reward_days_key").on(
      table.userId,
      table.sourceId,
      table.day,
      table.zone,
      table.activityType,
      table.metricType,
    ),
    index("exercise_reward_days_user_day").on(table.userId, table.day),
    check(
      "exercise_reward_days_has_value_check",
      sql`${table.hasValue} IN (0, 1)`,
    ),
    check(
      "exercise_reward_days_observed_value_check",
      sql`${table.observedValue} >= 0`,
    ),
    check(
      "exercise_reward_days_credited_value_check",
      sql`${table.creditedThroughValue} >= 0`,
    ),
    check(
      "exercise_reward_days_credited_runes_check",
      sql`${table.creditedRunes} >= 0`,
    ),
    check(
      "exercise_reward_days_value_consistency_check",
      sql`(${table.hasValue} = 1 AND ${table.observedValue} IS NOT NULL) OR (${table.hasValue} = 0 AND ${table.observedValue} IS NULL)`,
    ),
  ],
);

export const runeWallets = sqliteTable("rune_wallets", {
  userId: text("user_id")
    .primaryKey()
    .references(() => appUsers.id),
  balance: integer("balance").notNull().default(0),
  updatedAt: text("updated_at").notNull(),
});

export const runeClaims = sqliteTable(
  "rune_claims",
  {
    id: text("id").primaryKey(),
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    sourceId: text("source_id")
      .notNull()
      .references(() => healthSources.id),
    requestId: text("request_id").notNull(),
    grantedRunes: integer("granted_runes").notNull(),
    balanceAfter: integer("balance_after").notNull(),
    createdAt: text("created_at").notNull(),
  },
  (table) => [
    uniqueIndex("rune_claims_request_id").on(table.requestId),
    index("rune_claims_user_created").on(table.userId, table.createdAt),
    check("rune_claims_granted_runes_check", sql`${table.grantedRunes} >= 0`),
    check("rune_claims_balance_after_check", sql`${table.balanceAfter} >= 0`),
  ],
);

export const runeLedger = sqliteTable(
  "rune_ledger",
  {
    id: text("id").primaryKey(),
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    claimId: text("claim_id")
      .notNull()
      .references(() => runeClaims.id),
    rewardDayId: text("reward_day_id")
      .notNull()
      .references(() => exerciseRewardDays.id),
    delta: integer("delta").notNull(),
    reason: text("reason").notNull(),
    fromValue: integer("from_value").notNull(),
    toValue: integer("to_value").notNull(),
    ruleVersion: text("rule_version").notNull(),
    createdAt: text("created_at").notNull(),
  },
  (table) => [
    uniqueIndex("rune_ledger_claim_day").on(table.claimId, table.rewardDayId),
    index("rune_ledger_user_created").on(table.userId, table.createdAt),
    check("rune_ledger_delta_check", sql`${table.delta} > 0`),
    check(
      "rune_ledger_value_order_check",
      sql`${table.toValue} > ${table.fromValue}`,
    ),
  ],
);
