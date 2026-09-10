import { Hono } from "hono";
import { bodyLimit } from "hono/body-limit";
import { createRemoteJWKSet, type JWTVerifyGetKey, jwtVerify } from "jose";
import { createHealthRepository } from "./repository.js";
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
    const repository = createHealthRepository(c.env.DB);
    const userId = await repository.findOrCreateUser(subject);
    const token = Array.from(crypto.getRandomValues(new Uint8Array(32)), (b) =>
      b.toString(16).padStart(2, "0"),
    ).join("");
    const expiresAt = Date.now() + 3_600_000;
    await repository.createSession(await hashToken(token), userId, expiresAt);
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
    const userId = await createHealthRepository(c.env.DB).findSessionUser(
      tokenHash,
    );
    if (!userId) return c.json({ error: "unauthorized" }, 401);
    c.set("userId", userId);
    c.set("tokenHash", tokenHash);
    await next();
  });
  api.post("/auth/logout", async (c) => {
    await createHealthRepository(c.env.DB).deleteSession(c.get("tokenHash"));
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
    const result = await createHealthRepository(c.env.DB).beginSync(
      body.sourceId,
      c.get("userId"),
      body.provider,
    );
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
    const now = new Date().toISOString();
    const saved = await createHealthRepository(c.env.DB).saveDays(
      sourceId,
      c.get("userId"),
      body.revision,
      days,
      now,
    );
    if (!saved) return c.json({ error: "sync_conflict" }, 409);
    return c.json({ revision: body.revision, receivedAt: now });
  });
  api.get("/health/sources/:sourceId/days", async (c) => {
    const sourceId = c.req.param("sourceId");
    if (!validId(sourceId)) return c.json({ error: "invalid_request" }, 400);
    const repository = createHealthRepository(c.env.DB);
    const source = await repository.findSource(sourceId, c.get("userId"));
    if (!source) return c.json({ error: "source_unavailable" }, 404);
    const days = await repository.listDays(sourceId);
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
