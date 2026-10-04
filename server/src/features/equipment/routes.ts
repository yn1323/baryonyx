import { Hono } from "hono";
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
    const body = equipSchema.safeParse(await c.req.json().catch(() => null));
    if (!characterId.success || !slot.success || !body.success) {
      return c.json({ error: "invalid_request" }, 400);
    }
    const result = await createEquipmentRepository(c.env.DB).equip(
      c.get("userId"),
      characterId.data,
      slot.data,
      body.data.itemId,
      new Date().toISOString(),
    );
    if ("error" in result && result.error) {
      return c.json({ error: result.error }, errorStatus(result.error));
    }
    return c.json(result);
  });

  api.delete("/equipment/characters/:characterId/:slot", async (c) => {
    const characterId = characterIdSchema.safeParse(c.req.param("characterId"));
    const slot = equipmentSlotSchema.safeParse(c.req.param("slot"));
    if (!characterId.success || !slot.success) {
      return c.json({ error: "invalid_request" }, 400);
    }
    const result = await createEquipmentRepository(c.env.DB).unequip(
      c.get("userId"),
      characterId.data,
      slot.data,
      new Date().toISOString(),
    );
    if ("error" in result && result.error) {
      return c.json({ error: result.error }, errorStatus(result.error));
    }
    return c.json(result);
  });

  return api;
}
