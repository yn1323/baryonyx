import { Hono } from "hono";
import { requireSession, type SessionEnv } from "../accounts/session.js";
import { createHealthRepository } from "../health/repository.js";
import { createExerciseRewardsRepository } from "./repository.js";
import { claimRewardsSchema, rewardSourceIdSchema } from "./schema.js";

export function createExerciseRewardsApi() {
  const api = new Hono<SessionEnv>();
  api.use("/exercise/*", requireSession);
  api.use("/runes/*", requireSession);

  api.post("/exercise/rewards/claim", async (c) => {
    const parsed = claimRewardsSchema.safeParse(
      await c.req.json().catch(() => null),
    );
    if (!parsed.success) return c.json({ error: "invalid_request" }, 400);
    const { sourceId, requestId } = parsed.data;
    const userId = c.get("userId");
    const healthRepository = createHealthRepository(c.env.DB);
    const source = await healthRepository.findSource(sourceId, userId);
    if (!source) return c.json({ error: "source_unavailable" }, 404);
    const sourceDays = await healthRepository.listDays(sourceId);
    const result = await createExerciseRewardsRepository(c.env.DB).claim(
      userId,
      sourceId,
      requestId,
      sourceDays.slice(0, 7).map((day) => ({
        sourceId,
        day: day.day,
        zone: day.zone,
        hasValue: day.hasValue,
        steps: day.steps,
        observedAt: day.observedAt,
      })),
      new Date().toISOString(),
    );
    return c.json(result);
  });

  api.get("/exercise/rewards/days", async (c) => {
    const sourceId = rewardSourceIdSchema.safeParse(c.req.query("sourceId"));
    if (!sourceId.success) return c.json({ error: "invalid_request" }, 400);
    const userId = c.get("userId");
    const source = await createHealthRepository(c.env.DB).findSource(
      sourceId.data,
      userId,
    );
    if (!source) return c.json({ error: "source_unavailable" }, 404);
    const days = await createExerciseRewardsRepository(c.env.DB).listDays(
      userId,
      sourceId.data,
    );
    return c.json({ days });
  });

  api.get("/runes/balance", async (c) => {
    const balance = await createExerciseRewardsRepository(c.env.DB).getBalance(
      c.get("userId"),
    );
    return c.json(balance);
  });

  return api;
}
