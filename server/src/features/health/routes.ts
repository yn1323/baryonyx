import { Hono } from "hono";
import { bodyLimit } from "hono/body-limit";
import { createRemoteJWKSet, type JWTVerifyGetKey, jwtVerify } from "jose";
import { parseDays, validId } from "./schema.js";

export type HealthEnv = {
  Bindings: Env & { GOOGLE_CLIENT_ID?: string };
  Variables: { userId: string; tokenHash: string };
};
const googleKeys = createRemoteJWKSet(
  new URL("https://www.googleapis.com/oauth2/v3/certs"),
);
export type VerifyIdentity = (
  token: string,
  audience: string,
) => Promise<string>;
export function createGoogleVerifier(
  keys: JWTVerifyGetKey = googleKeys,
): VerifyIdentity {
  return async (token, audience) => {
    const { payload } = await jwtVerify(token, keys, {
      audience,
      issuer: ["accounts.google.com", "https://accounts.google.com"],
      algorithms: ["RS256"],
      requiredClaims: ["sub", "exp", "iat"],
      maxTokenAge: "1h",
    });
    if (!payload.sub || payload.sub.length > 255)
      throw new Error("Invalid subject");
    return payload.sub;
  };
}
export const verifyGoogleIdentity = createGoogleVerifier();
export async function hashToken(token: string): Promise<string> {
  const digest = await crypto.subtle.digest(
    "SHA-256",
    new TextEncoder().encode(token),
  );
  return Array.from(new Uint8Array(digest), (byte) =>
    byte.toString(16).padStart(2, "0"),
  ).join("");
}

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
    const body = await c.req.json().catch(() => null);
    if (typeof body?.idToken !== "string" || body.idToken.length > 12_000)
      return c.json({ error: "invalid_request" }, 400);
    let subject: string;
    try {
      subject = await verifyIdentity(body.idToken, audience);
    } catch {
      return c.json({ error: "invalid_identity" }, 401);
    }
    const db = c.env.DB;
    await db
      .prepare(
        "INSERT INTO app_users(id, google_sub) VALUES (?, ?) ON CONFLICT(google_sub) DO NOTHING",
      )
      .bind(crypto.randomUUID(), subject)
      .run();
    const userId = await db
      .prepare("SELECT id FROM app_users WHERE google_sub = ?")
      .bind(subject)
      .first<string>("id");
    const token = Array.from(crypto.getRandomValues(new Uint8Array(32)), (b) =>
      b.toString(16).padStart(2, "0"),
    ).join("");
    const expiresAt = Date.now() + 3_600_000;
    await db.batch([
      db
        .prepare("DELETE FROM app_sessions WHERE expires_at <= ?")
        .bind(Date.now()),
      db
        .prepare("INSERT INTO app_sessions VALUES (?, ?, ?)")
        .bind(await hashToken(token), userId, expiresAt),
    ]);
    return c.json({
      token,
      userId,
      expiresAt: new Date(expiresAt).toISOString(),
    });
  });

  api.use("*", async (c, next) => {
    const header = c.req.header("Authorization") ?? "";
    if (!/^Bearer [a-f0-9]{64}$/.test(header))
      return c.json({ error: "unauthorized" }, 401);
    const tokenHash = await hashToken(header.slice(7));
    const userId = await c.env.DB.prepare(
      "SELECT user_id FROM app_sessions WHERE token_hash = ? AND expires_at > ?",
    )
      .bind(tokenHash, Date.now())
      .first<string>("user_id");
    if (!userId) return c.json({ error: "unauthorized" }, 401);
    c.set("userId", userId);
    c.set("tokenHash", tokenHash);
    await next();
  });
  api.post("/auth/logout", async (c) => {
    await c.env.DB.prepare("DELETE FROM app_sessions WHERE token_hash = ?")
      .bind(c.get("tokenHash"))
      .run();
    return c.body(null, 204);
  });
  api.post("/health/syncs", async (c) => {
    const body = await c.req.json().catch(() => null);
    if (
      !validId(body?.sourceId) ||
      !["health_connect", "healthkit"].includes(body?.provider)
    )
      return c.json({ error: "invalid_request" }, 400);
    // サーバー発行の版で、再起動・時計ずれ・遅延送信による上書きを防ぐ。
    const result =
      await c.env.DB.prepare(`INSERT INTO health_sources(id, user_id, provider, revision) VALUES (?, ?, ?, 1)
      ON CONFLICT(id) DO UPDATE SET revision = revision + 1 WHERE user_id = excluded.user_id AND provider = excluded.provider
      RETURNING revision`)
        .bind(body.sourceId, c.get("userId"), body.provider)
        .first<{ revision: number }>();
    if (!result) return c.json({ error: "source_unavailable" }, 404);
    return c.json(result);
  });
  api.put("/health/sources/:sourceId/days", async (c) => {
    const sourceId = c.req.param("sourceId");
    const body = await c.req.json().catch(() => null);
    const days = parseDays(body?.days);
    if (
      !validId(sourceId) ||
      !days ||
      !Number.isSafeInteger(body?.revision) ||
      body.revision < 1
    )
      return c.json({ error: "invalid_request" }, 400);
    const db = c.env.DB;
    const now = new Date().toISOString();
    const statements = days.map((day) =>
      db
        .prepare(`INSERT INTO health_days
      (source_id, day, zone, start_at, end_at, has_value, steps, last_known_steps, last_known_observed_at, observed_at, received_at, revision)
      SELECT ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ? WHERE EXISTS
      (SELECT 1 FROM health_sources WHERE id = ? AND user_id = ? AND revision = ?)
      ON CONFLICT(source_id, day, zone) DO UPDATE SET start_at = excluded.start_at, end_at = excluded.end_at,
      has_value = excluded.has_value, steps = excluded.steps,
      last_known_steps = COALESCE(excluded.steps, health_days.last_known_steps), observed_at = excluded.observed_at,
      last_known_observed_at = COALESCE(excluded.last_known_observed_at, health_days.last_known_observed_at),
      received_at = excluded.received_at, revision = excluded.revision
      WHERE health_days.revision < excluded.revision`)
        .bind(
          sourceId,
          day.day,
          day.zone,
          day.startAt,
          day.endAt,
          day.hasValue ? 1 : 0,
          day.hasValue ? day.steps : null,
          day.hasValue ? day.steps : null,
          day.hasValue ? day.observedAt : null,
          day.observedAt,
          now,
          body.revision,
          sourceId,
          c.get("userId"),
          body.revision,
        ),
    );
    const results = await db.batch(statements);
    if (results.every((r) => r.meta.changes === 0))
      return c.json({ error: "sync_conflict" }, 409);
    return c.json({ revision: body.revision, receivedAt: now });
  });
  api.get("/health/sources/:sourceId/days", async (c) => {
    const sourceId = c.req.param("sourceId");
    if (!validId(sourceId)) return c.json({ error: "invalid_request" }, 400);
    const source = await c.env.DB.prepare(
      "SELECT provider, revision FROM health_sources WHERE id = ? AND user_id = ?",
    )
      .bind(sourceId, c.get("userId"))
      .first();
    if (!source) return c.json({ error: "source_unavailable" }, 404);
    const result =
      await c.env.DB.prepare(`SELECT day, zone, start_at AS startAt, end_at AS endAt,
      has_value AS hasValue, steps, last_known_steps AS lastKnownSteps, last_known_observed_at AS lastKnownObservedAt, observed_at AS observedAt,
      received_at AS receivedAt, revision FROM health_days WHERE source_id = ? ORDER BY day DESC LIMIT 100`)
        .bind(sourceId)
        .all();
    return c.json({
      ...source,
      days: result.results.map((row) => ({
        ...row,
        hasValue: row.hasValue === 1,
        hasLastKnownValue: row.lastKnownSteps !== null,
      })),
    });
  });
  return api;
}
