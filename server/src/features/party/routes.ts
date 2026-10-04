import { Hono } from "hono";
import { invalidRequest, parseJson, respond } from "../../shared/http.js";
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
    const body = await parseJson(c, setPartySlotSchema);
    if (!slot.success || !body.success) return invalidRequest(c);
    const result = await createPartyRepository(c.env.DB).setSlot(
      c.get("userId"),
      slot.data,
      body.data.characterId,
      new Date().toISOString(),
    );
    return respond(c, result, errorStatus);
  });

  api.delete("/party/slots/:slot", async (c) => {
    const slot = partySlotSchema.safeParse(c.req.param("slot"));
    if (!slot.success) return invalidRequest(c);
    const result = await createPartyRepository(c.env.DB).clearSlot(
      c.get("userId"),
      slot.data,
      new Date().toISOString(),
    );
    return respond(c, result, errorStatus);
  });

  api.put("/party/characters/:characterId/cards/:slot", async (c) => {
    const characterId = characterIdSchema.safeParse(c.req.param("characterId"));
    const slot = cardSlotSchema.safeParse(c.req.param("slot"));
    const body = await parseJson(c, setCardSchema);
    if (!characterId.success || !slot.success || !body.success) {
      return invalidRequest(c);
    }
    const result = await createPartyRepository(c.env.DB).setCard(
      c.get("userId"),
      characterId.data,
      slot.data,
      body.data.skillId,
      new Date().toISOString(),
    );
    return respond(c, result, errorStatus);
  });

  api.post("/party/characters/:characterId/level-up", async (c) => {
    const characterId = characterIdSchema.safeParse(c.req.param("characterId"));
    const body = await parseJson(c, levelUpSchema);
    if (!characterId.success || !body.success) return invalidRequest(c);
    const result = await createPartyRepository(c.env.DB).levelUp(
      c.get("userId"),
      characterId.data,
      body.data,
      new Date().toISOString(),
    );
    return respond(c, result, errorStatus);
  });

  return api;
}
