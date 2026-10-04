import { Hono } from "hono";
import { invalidRequest, parseJson, respond } from "../../shared/http.js";
import { requireSession, type SessionEnv } from "../accounts/session.js";
import { createStepBonusRepository } from "./repository.js";
import { setSlotSchema, slotSchema } from "./schema.js";

// 持っていないボーナスは404、冒険の途中の付け替えは409で返す。
function errorStatus(error: string) {
  return error === "bonus_not_owned" ? 404 : 409;
}

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
    const body = await parseJson(c, setSlotSchema);
    if (!slot.success || !body.success) return invalidRequest(c);
    const result = await createStepBonusRepository(c.env.DB).setSlot(
      c.get("userId"),
      slot.data,
      body.data.bonusId,
      new Date().toISOString(),
    );
    return respond(c, result, errorStatus);
  });

  return api;
}
