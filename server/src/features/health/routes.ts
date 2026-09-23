import { Hono } from "hono";
import { requireSession, type SessionEnv } from "../accounts/session.js";
import { createHealthRepository } from "./repository.js";
import {
  beginSyncSchema,
  createSaveDaysSchema,
  sourceIdSchema,
} from "./schema.js";

export function createHealthApi() {
  const api = new Hono<SessionEnv>();
  api.use("/health/*", requireSession);
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
