import { and, eq, gt, lte } from "drizzle-orm";
import { createDatabase } from "../../shared/db.js";
import { appSessions, appUsers } from "./db-schema.js";

export function createAccountsRepository(binding: D1Database) {
  const db = createDatabase(binding);

  return {
    async findOrCreateUser(googleSub: string) {
      // 作成と読み込みを1回の往復で行う。作成済みなら作らずに読む。
      const [, users] = await db.batch([
        db
          .insert(appUsers)
          .values({ id: crypto.randomUUID(), googleSub })
          .onConflictDoNothing({ target: appUsers.googleSub }),
        db
          .select({ id: appUsers.id })
          .from(appUsers)
          .where(eq(appUsers.googleSub, googleSub)),
      ]);
      const user = users[0];
      if (!user) throw new Error("User was not found after insert");
      return user.id;
    },

    async findOrCreateGuest(guestSecretHash: string) {
      const [, users] = await db.batch([
        db
          .insert(appUsers)
          .values({ id: crypto.randomUUID(), guestSecretHash })
          .onConflictDoNothing({ target: appUsers.guestSecretHash }),
        db
          .select({ id: appUsers.id })
          .from(appUsers)
          .where(eq(appUsers.guestSecretHash, guestSecretHash)),
      ]);
      const user = users[0];
      if (!user) throw new Error("Guest was not found after insert");
      return user.id;
    },

    async createSession(tokenHash: string, userId: string, expiresAt: number) {
      await db.batch([
        db.delete(appSessions).where(lte(appSessions.expiresAt, Date.now())),
        db.insert(appSessions).values({ tokenHash, userId, expiresAt }),
      ]);
    },

    async findSession(tokenHash: string) {
      return db
        .select({
          userId: appSessions.userId,
          expiresAt: appSessions.expiresAt,
        })
        .from(appSessions)
        .where(
          and(
            eq(appSessions.tokenHash, tokenHash),
            gt(appSessions.expiresAt, Date.now()),
          ),
        )
        .get();
    },

    async deleteSession(tokenHash: string) {
      await db.delete(appSessions).where(eq(appSessions.tokenHash, tokenHash));
    },
  };
}
