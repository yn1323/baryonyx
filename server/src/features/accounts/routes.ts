import { Hono } from "hono";
import { invalidRequest, parseJson } from "../../shared/http.js";
import {
  issueGuestSession,
  issueSession,
  type VerifyIdentity,
  verifyGoogleIdentity,
} from "./auth.js";
import { createAccountsRepository } from "./repository.js";
import { googleAuthSchema, guestAuthSchema } from "./schema.js";
import { requireSession, type SessionEnv } from "./session.js";

// テストでは本人確認だけを差し替える。HTTP設定からの認証バイパスは設けない。
export function createAccountsApi(
  verifyIdentity: VerifyIdentity = verifyGoogleIdentity,
) {
  const api = new Hono<SessionEnv>();

  api.post("/auth/google", async (c) => {
    const audience = c.env.GOOGLE_CLIENT_ID;
    if (!audience) return c.json({ error: "auth_not_configured" }, 503);
    const parsed = await parseJson(c, googleAuthSchema);
    if (!parsed.success) return invalidRequest(c);
    let subject: string;
    try {
      subject = await verifyIdentity(parsed.data.idToken, audience);
    } catch {
      return c.json({ error: "invalid_identity" }, 401);
    }
    const repository = createAccountsRepository(c.env.DB);
    return c.json(await issueSession(repository, subject));
  });

  api.post("/auth/guest", async (c) => {
    const parsed = await parseJson(c, guestAuthSchema);
    if (!parsed.success) return invalidRequest(c);
    const repository = createAccountsRepository(c.env.DB);
    return c.json(await issueGuestSession(repository, parsed.data.secret));
  });

  api.post("/auth/logout", requireSession, async (c) => {
    await createAccountsRepository(c.env.DB).deleteSession(c.get("tokenHash"));
    return c.body(null, 204);
  });

  return api;
}
