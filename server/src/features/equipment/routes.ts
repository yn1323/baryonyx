import { Hono } from "hono";
import { invalidRequest, parseJson, respond } from "../../shared/http.js";
import { requireSession, type SessionEnv } from "../accounts/session.js";
import { createEquipmentRepository } from "./repository.js";
import {
  characterIdSchema,
  equipmentSlotSchema,
  equipSchema,
} from "./schema.js";

// 持っていないキャラ・装備は404、枠に合わない装備は409で返す。
function errorStatus(error: string) {
  return error === "slot_mismatch" ? 409 : 404;
}

export function createEquipmentApi() {
  const api = new Hono<SessionEnv>();
  api.use("/equipment", requireSession);
  api.use("/equipment/*", requireSession);

  // 持っている装備と、キャラごとの武器・防具。初めて読むユーザーには仮の初期データを付与する。
  api.get("/equipment", async (c) => {
    const state = await createEquipmentRepository(c.env.DB).getState(
      c.get("userId"),
      new Date().toISOString(),
    );
    return c.json(state);
  });

  api.put("/equipment/characters/:characterId/:slot", async (c) => {
    const characterId = characterIdSchema.safeParse(c.req.param("characterId"));
    const slot = equipmentSlotSchema.safeParse(c.req.param("slot"));
    const body = await parseJson(c, equipSchema);
    if (!characterId.success || !slot.success || !body.success) {
      return invalidRequest(c);
    }
    const result = await createEquipmentRepository(c.env.DB).equip(
      c.get("userId"),
      characterId.data,
      slot.data,
      body.data.itemId,
      new Date().toISOString(),
    );
    return respond(c, result, errorStatus);
  });

  api.delete("/equipment/characters/:characterId/:slot", async (c) => {
    const characterId = characterIdSchema.safeParse(c.req.param("characterId"));
    const slot = equipmentSlotSchema.safeParse(c.req.param("slot"));
    if (!characterId.success || !slot.success) return invalidRequest(c);
    const result = await createEquipmentRepository(c.env.DB).unequip(
      c.get("userId"),
      characterId.data,
      slot.data,
      new Date().toISOString(),
    );
    return respond(c, result, errorStatus);
  });

  return api;
}
