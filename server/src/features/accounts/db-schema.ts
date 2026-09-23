import { index, integer, sqliteTable, text } from "drizzle-orm/sqlite-core";

export const appUsers = sqliteTable("app_users", {
  id: text("id").primaryKey(),
  googleSub: text("google_sub").notNull().unique(),
});

export const appSessions = sqliteTable(
  "app_sessions",
  {
    tokenHash: text("token_hash").primaryKey(),
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    expiresAt: integer("expires_at").notNull(),
  },
  (table) => [index("app_sessions_expiry").on(table.expiresAt)],
);
