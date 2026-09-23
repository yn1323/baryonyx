import { Hono } from "hono";
import {
  issueSession,
  type VerifyIdentity,
  verifyGoogleIdentity,
} from "./auth.js";
import { createAccountsRepository } from "./repository.js";
import { googleAuthSchema } from "./schema.js";
import { requireSession, type SessionEnv } from "./session.js";

// テストでは本人確認だけを差し替える。HTTP設定からの認証バイパスは設けない。
export function createAccountsApi(
  verifyIdentity: VerifyIdentity = verifyGoogleIdentity,
) {
  const api = new Hono<SessionEnv>();

  api.post("/auth/google", async (c) => {
    const audience = c.env.GOOGLE_CLIENT_ID;
    if (!audience) return c.json({ error: "auth_not_configured" }, 503);
    const parsed = googleAuthSchema.safeParse(
      await c.req.json().catch(() => null),
    );
    if (!parsed.success) return c.json({ error: "invalid_request" }, 400);
    let subject: string;
    try {
      subject = await verifyIdentity(parsed.data.idToken, audience);
    } catch {
      return c.json({ error: "invalid_identity" }, 401);
    }
    const repository = createAccountsRepository(c.env.DB);
    return c.json(await issueSession(repository, subject));
  });

  api.post("/auth/logout", requireSession, async (c) => {
    await createAccountsRepository(c.env.DB).deleteSession(c.get("tokenHash"));
    return c.body(null, 204);
  });

  return api;
}
