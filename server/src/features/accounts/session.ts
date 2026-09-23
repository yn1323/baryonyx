import { createMiddleware } from "hono/factory";
import { authenticateSession } from "./auth.js";
import { createAccountsRepository } from "./repository.js";

export type SessionEnv = {
  Bindings: Env & { GOOGLE_CLIENT_ID?: string };
  Variables: { userId: string; tokenHash: string };
};

// セッションが必要なパスへ機能ごとに付け、同じ接続先の他機能へ広げない。
export const requireSession = createMiddleware<SessionEnv>(async (c, next) => {
  const session = await authenticateSession(
    createAccountsRepository(c.env.DB),
    c.req.header("Authorization") ?? "",
  );
  if (!session) return c.json({ error: "unauthorized" }, 401);
  c.set("userId", session.userId);
  c.set("tokenHash", session.tokenHash);
  await next();
});
