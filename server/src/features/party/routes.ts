import { Hono } from "hono";
import { requireSession, type SessionEnv } from "../accounts/session.js";
import { createPartyRepository } from "./repository.js";
import {
  cardSlotSchema,
  characterIdSchema,
  levelUpSchema,
  partySlotSchema,
  setCardSchema,
  setPartySlotSchema,
} from "./schema.js";

// 持っていないキャラは404、決まりや今の状態と合わない変更は409で返す。
function errorStatus(error: string) {
  return error === "character_not_owned" ? 404 : 409;
}

export function createPartyApi() {
  const api = new Hono<SessionEnv>();
  api.use("/party", requireSession);
  api.use("/party/*", requireSession);

  // 持っているキャラ・編成・カード・所持ルーン。初めて読むユーザーには仮の初期データを付与する。
  api.get("/party", async (c) => {
    const state = await createPartyRepository(c.env.DB).getState(
      c.get("userId"),
      new Date().toISOString(),
    );
    return c.json(state);
  });

  api.put("/party/slots/:slot", async (c) => {
    const slot = partySlotSchema.safeParse(c.req.param("slot"));
    const body = setPartySlotSchema.safeParse(
      await c.req.json().catch(() => null),
    );
    if (!slot.success || !body.success) {
      return c.json({ error: "invalid_request" }, 400);
    }
    const result = await createPartyRepository(c.env.DB).setSlot(
      c.get("userId"),
      slot.data,
      body.data.characterId,
      new Date().toISOString(),
    );
    if ("error" in result && result.error) {
      return c.json({ error: result.error }, errorStatus(result.error));
    }
    return c.json(result);
  });

  api.delete("/party/slots/:slot", async (c) => {
    const slot = partySlotSchema.safeParse(c.req.param("slot"));
    if (!slot.success) return c.json({ error: "invalid_request" }, 400);
    const result = await createPartyRepository(c.env.DB).clearSlot(
      c.get("userId"),
      slot.data,
      new Date().toISOString(),
    );
    if ("error" in result && result.error) {
      return c.json({ error: result.error }, errorStatus(result.error));
    }
    return c.json(result);
  });

  api.put("/party/characters/:characterId/cards/:slot", async (c) => {
    const characterId = characterIdSchema.safeParse(c.req.param("characterId"));
    const slot = cardSlotSchema.safeParse(c.req.param("slot"));
    const body = setCardSchema.safeParse(await c.req.json().catch(() => null));
    if (!characterId.success || !slot.success || !body.success) {
      return c.json({ error: "invalid_request" }, 400);
    }
    const result = await createPartyRepository(c.env.DB).setCard(
      c.get("userId"),
      characterId.data,
      slot.data,
      body.data.skillId,
      new Date().toISOString(),
    );
    if ("error" in result && result.error) {
      return c.json({ error: result.error }, errorStatus(result.error));
    }
    return c.json(result);
  });

  api.post("/party/characters/:characterId/level-up", async (c) => {
    const characterId = characterIdSchema.safeParse(c.req.param("characterId"));
    const body = levelUpSchema.safeParse(await c.req.json().catch(() => null));
    if (!characterId.success || !body.success) {
      return c.json({ error: "invalid_request" }, 400);
    }
    const result = await createPartyRepository(c.env.DB).levelUp(
      c.get("userId"),
      characterId.data,
      body.data,
      new Date().toISOString(),
    );
    if ("error" in result && result.error) {
      return c.json({ error: result.error }, errorStatus(result.error));
    }
    return c.json(result);
  });

  return api;
}
