import { index, integer, sqliteTable, text } from "drizzle-orm/sqlite-core";

export const appUsers = sqliteTable("app_users", {
  id: text("id").primaryKey(),
  // ゲストはGoogle接続なしで始め、端末が保持する秘密値のハッシュで識別する。
  googleSub: text("google_sub").unique(),
  guestSecretHash: text("guest_secret_hash").unique(),
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
