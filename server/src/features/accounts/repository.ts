import { and, eq, gt, lte } from "drizzle-orm";
import { createDatabase } from "../../shared/db.js";
import { appSessions, appUsers } from "./db-schema.js";

export function createAccountsRepository(binding: D1Database) {
  const db = createDatabase(binding);

  return {
    async findOrCreateUser(googleSub: string) {
      await db
        .insert(appUsers)
        .values({ id: crypto.randomUUID(), googleSub })
        .onConflictDoNothing({ target: appUsers.googleSub });
      const user = await db
        .select({ id: appUsers.id })
        .from(appUsers)
        .where(eq(appUsers.googleSub, googleSub))
        .get();
      if (!user) throw new Error("User was not found after insert");
      return user.id;
    },

    async findOrCreateGuest(guestSecretHash: string) {
      await db
        .insert(appUsers)
        .values({ id: crypto.randomUUID(), guestSecretHash })
        .onConflictDoNothing({ target: appUsers.guestSecretHash });
      const user = await db
        .select({ id: appUsers.id })
        .from(appUsers)
        .where(eq(appUsers.guestSecretHash, guestSecretHash))
        .get();
      if (!user) throw new Error("Guest was not found after insert");
      return user.id;
    },

    async createSession(tokenHash: string, userId: string, expiresAt: number) {
      await db.batch([
        db.delete(appSessions).where(lte(appSessions.expiresAt, Date.now())),
        db.insert(appSessions).values({ tokenHash, userId, expiresAt }),
      ]);
    },

    async findSessionUser(tokenHash: string) {
      const session = await db
        .select({ userId: appSessions.userId })
        .from(appSessions)
        .where(
          and(
            eq(appSessions.tokenHash, tokenHash),
            gt(appSessions.expiresAt, Date.now()),
          ),
        )
        .get();
      return session?.userId;
    },

    async deleteSession(tokenHash: string) {
      await db.delete(appSessions).where(eq(appSessions.tokenHash, tokenHash));
    },
  };
}
