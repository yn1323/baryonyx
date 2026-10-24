import { Hono } from "hono";
import { invalidRequest, parseJson, respond } from "../../shared/http.js";
import { requireSession, type SessionEnv } from "../accounts/session.js";
import { createEquipmentRepository } from "./repository.js";
import {
  characterIdSchema,
  equipmentSlotSchema,
  equipSchema,
} from "./schema.js";

export function createEquipmentApi() {
  const api = new Hono<SessionEnv>();
  // Honoでは `/equipment/*` が `/equipment` にも一致する。両方に付けると、セッションを2回確かめる。
  api.use("/equipment/*", requireSession);

  // 持っている装備と、キャラごとの武器・防具・アクセサリー。初めて読むユーザーには仮の初期データを付与する。
  api.get("/equipment", async (c) => {
    const state = await createEquipmentRepository(c.env.DB).getState(
      c.get("userId"),
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
    return respond(c, result);
  });

  api.delete("/equipment/characters/:characterId/:slot", async (c) => {
    const characterId = characterIdSchema.safeParse(c.req.param("characterId"));
    const slot = equipmentSlotSchema.safeParse(c.req.param("slot"));
    if (!characterId.success || !slot.success) return invalidRequest(c);
    const result = await createEquipmentRepository(c.env.DB).unequip(
      c.get("userId"),
      characterId.data,
      slot.data,
    );
    return respond(c, result);
  });

  return api;
}
