import { sql } from "drizzle-orm";
import {
  check,
  integer,
  primaryKey,
  sqliteTable,
  text,
} from "drizzle-orm/sqlite-core";
import { appUsers } from "../accounts/db-schema.js";

export const healthSources = sqliteTable(
  "health_sources",
  {
    id: text("id").primaryKey(),
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    provider: text("provider", {
      enum: ["health_connect", "healthkit"],
    }).notNull(),
    revision: integer("revision").notNull().default(0),
  },
  (table) => [
    check(
      "health_sources_provider_check",
      sql`${table.provider} IN ('health_connect', 'healthkit')`,
    ),
  ],
);

export const healthDays = sqliteTable(
  "health_days",
  {
    sourceId: text("source_id")
      .notNull()
      .references(() => healthSources.id),
    day: text("day").notNull(),
    zone: text("zone").notNull(),
    startAt: text("start_at").notNull(),
    endAt: text("end_at").notNull(),
    hasValue: integer("has_value", { mode: "boolean" }).notNull(),
    steps: integer("steps"),
    lastKnownSteps: integer("last_known_steps"),
    observedAt: text("observed_at").notNull(),
    receivedAt: text("received_at").notNull(),
    revision: integer("revision").notNull(),
    lastKnownObservedAt: text("last_known_observed_at"),
  },
  (table) => [
    primaryKey({ columns: [table.sourceId, table.day, table.zone] }),
    check("health_days_has_value_check", sql`${table.hasValue} IN (0, 1)`),
    check("health_days_steps_check", sql`${table.steps} >= 0`),
    check(
      "health_days_last_known_steps_check",
      sql`${table.lastKnownSteps} >= 0`,
    ),
    check(
      "health_days_value_check",
      sql`(${table.hasValue} = 1 AND ${table.steps} IS NOT NULL) OR (${table.hasValue} = 0 AND ${table.steps} IS NULL)`,
    ),
  ],
);
