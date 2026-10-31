import { Hono } from "hono";
import { invalidRequest, parseJson } from "../../shared/http.js";
import { requireSession, type SessionEnv } from "../accounts/session.js";
import { createHealthRepository } from "../health/repository.js";
import { createExerciseRewardsRepository } from "./repository.js";
import {
  claimRewardsSchema,
  createSyncRewardsSchema,
  rewardSourceIdSchema,
} from "./schema.js";

export function createExerciseRewardsApi() {
  const api = new Hono<SessionEnv>();
  api.use("/exercise/*", requireSession);
  api.use("/runes/*", requireSession);

  api.post("/exercise/rewards/claim", async (c) => {
    const parsed = await parseJson(c, claimRewardsSchema);
    if (!parsed.success) return invalidRequest(c);
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

  // 直近7日の歩数を保存し、同じ要求でルーンへ変換する。端末とWorkerの往復を1回にし、
  // 保存（POST /health/syncs・PUT /health/sources/:id/days）と請求（POST /exercise/rewards/claim）を順に呼ぶのと同じ結果を返す。
  api.post("/exercise/rewards/sync", async (c) => {
    const parsed = await parseJson(c, createSyncRewardsSchema());
    if (!parsed.success) return invalidRequest(c);
    const { sourceId, provider, requestId, days } = parsed.data;
    const userId = c.get("userId");
    const healthRepository = createHealthRepository(c.env.DB);
    // サーバー発行の版で、再起動・時計ずれ・遅延送信による上書きを防ぐ（POST /health/syncs と同じ）。
    const source = await healthRepository.beginSync(sourceId, userId, provider);
    if (!source) return c.json({ error: "source_unavailable" }, 404);
    const now = new Date().toISOString();
    const saved = await healthRepository.saveDays(
      sourceId,
      userId,
      source.revision,
      days,
      now,
    );
    if (!saved) return c.json({ error: "sync_conflict" }, 409);
    // 保存した7日をそのまま請求に使う（保存した行を読み直す往復を省く）。
    const result = await createExerciseRewardsRepository(c.env.DB).claim(
      userId,
      sourceId,
      requestId,
      days.map((day) => ({
        sourceId,
        day: day.day,
        zone: day.zone,
        hasValue: day.hasValue,
        steps: day.hasValue ? day.steps : null,
        observedAt: day.observedAt,
      })),
      now,
    );
    return c.json(result);
  });

  api.get("/exercise/rewards/days", async (c) => {
    const sourceId = rewardSourceIdSchema.safeParse(c.req.query("sourceId"));
    if (!sourceId.success) return invalidRequest(c);
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
