import { Hono } from "hono";
import { requireSession, type SessionEnv } from "../accounts/session.js";
import { createStepBonusRepository } from "./repository.js";
import { setSlotSchema, slotSchema } from "./schema.js";

export function createStepBonusApi() {
  const api = new Hono<SessionEnv>();
  api.use("/step-bonus", requireSession);
  api.use("/step-bonus/*", requireSession);

  // 持っているボーナスと枠の設定。初めて読むユーザーには仮の初期ボーナスを付与する。
  api.get("/step-bonus", async (c) => {
    const state = await createStepBonusRepository(c.env.DB).getState(
      c.get("userId"),
      new Date().toISOString(),
    );
    return c.json(state);
  });

  api.put("/step-bonus/slots/:slot", async (c) => {
    const slot = slotSchema.safeParse(c.req.param("slot"));
    const body = setSlotSchema.safeParse(await c.req.json().catch(() => null));
    if (!slot.success || !body.success) {
      return c.json({ error: "invalid_request" }, 400);
    }
    const result = await createStepBonusRepository(c.env.DB).setSlot(
      c.get("userId"),
      slot.data,
      body.data.bonusId,
      new Date().toISOString(),
    );
    if ("error" in result) return c.json({ error: result.error }, 404);
    return c.json(result);
  });

  return api;
}
