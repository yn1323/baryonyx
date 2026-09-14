import { Hono } from "hono";
import { bodyLimit } from "hono/body-limit";
import {
  authenticateSession,
  issueSession,
  type VerifyIdentity,
  verifyGoogleIdentity,
} from "./auth.js";
import { createHealthRepository } from "./repository.js";
import {
  beginSyncSchema,
  createSaveDaysSchema,
  googleAuthSchema,
  sourceIdSchema,
} from "./schema.js";

export type HealthEnv = {
  Bindings: Env & { GOOGLE_CLIENT_ID?: string };
  Variables: { userId: string; tokenHash: string };
};

// テストでは本人確認だけを差し替える。HTTP設定からの認証バイパスは設けない。
export function createHealthApi(
  verifyIdentity: VerifyIdentity = verifyGoogleIdentity,
) {
  const api = new Hono<HealthEnv>();
  api.use("*", async (c, next) => {
    c.header("Cache-Control", "no-store");
    await next();
  });
  api.use(
    "*",
    bodyLimit({
      maxSize: 16_384,
      onError: (c) => c.json({ error: "request_too_large" }, 413),
    }),
  );
  api.onError(
    () =>
      new Response(JSON.stringify({ error: "service_unavailable" }), {
        status: 503,
        headers: {
          "Content-Type": "application/json",
          "Cache-Control": "no-store",
        },
      }),
  );

  api.post("/auth/google", async (c) => {
    const audience = c.env.GOOGLE_CLIENT_ID;
    if (!audience) return c.json({ error: "auth_not_configured" }, 503);
    const parsed = googleAuthSchema.safeParse(
      await c.req.json().catch(() => null),
    );
    if (!parsed.success) return c.json({ error: "invalid_request" }, 400);
    const body = parsed.data;
    let subject: string;
    try {
      subject = await verifyIdentity(body.idToken, audience);
    } catch {
      return c.json({ error: "invalid_identity" }, 401);
    }
    const repository = createHealthRepository(c.env.DB);
    return c.json(await issueSession(repository, subject));
  });

  api.use("*", async (c, next) => {
    const session = await authenticateSession(
      createHealthRepository(c.env.DB),
      c.req.header("Authorization") ?? "",
    );
    if (!session) return c.json({ error: "unauthorized" }, 401);
    c.set("userId", session.userId);
    c.set("tokenHash", session.tokenHash);
    await next();
  });
  api.post("/auth/logout", async (c) => {
    await createHealthRepository(c.env.DB).deleteSession(c.get("tokenHash"));
    return c.body(null, 204);
  });
  api.post("/health/syncs", async (c) => {
    const parsed = beginSyncSchema.safeParse(
      await c.req.json().catch(() => null),
    );
    if (!parsed.success) return c.json({ error: "invalid_request" }, 400);
    const body = parsed.data;
    // サーバー発行の版で、再起動・時計ずれ・遅延送信による上書きを防ぐ。
    const result = await createHealthRepository(c.env.DB).beginSync(
      body.sourceId,
      c.get("userId"),
      body.provider,
    );
    if (!result) return c.json({ error: "source_unavailable" }, 404);
    return c.json(result);
  });
  api.put("/health/sources/:sourceId/days", async (c) => {
    const source = sourceIdSchema.safeParse(c.req.param("sourceId"));
    const parsed = createSaveDaysSchema().safeParse(
      await c.req.json().catch(() => null),
    );
    if (!source.success || !parsed.success)
      return c.json({ error: "invalid_request" }, 400);
    const body = parsed.data;
    const now = new Date().toISOString();
    const saved = await createHealthRepository(c.env.DB).saveDays(
      source.data,
      c.get("userId"),
      body.revision,
      body.days,
      now,
    );
    if (!saved) return c.json({ error: "sync_conflict" }, 409);
    return c.json({ revision: body.revision, receivedAt: now });
  });
  api.get("/health/sources/:sourceId/days", async (c) => {
    const sourceId = sourceIdSchema.safeParse(c.req.param("sourceId"));
    if (!sourceId.success) return c.json({ error: "invalid_request" }, 400);
    const repository = createHealthRepository(c.env.DB);
    const source = await repository.findSource(sourceId.data, c.get("userId"));
    if (!source) return c.json({ error: "source_unavailable" }, 404);
    const days = await repository.listDays(sourceId.data);
    return c.json({
      ...source,
      days: days.map((row) => ({
        ...row,
        hasLastKnownValue: row.lastKnownSteps !== null,
      })),
    });
  });
  return api;
}
