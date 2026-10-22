import { Hono } from "hono";
import { invalidRequest, parseJson, respond } from "../../shared/http.js";
import { requireSession, type SessionEnv } from "../accounts/session.js";
import { createPartyRepository } from "./repository.js";
import {
  cardSlotSchema,
  characterIdSchema,
  evolveUniqueSchema,
  levelUpSchema,
  partySlotSchema,
  setCardSchema,
  setPartySlotSchema,
  setUniqueSchema,
  uniqueIndexSchema,
} from "./schema.js";

// 持っていないキャラ・カードは404、決まりや今の状態と合わない変更は409で返す。
function errorStatus(error: string) {
  return error === "character_not_owned" || error === "card_not_owned"
    ? 404
    : 409;
}

export function createPartyApi() {
  const api = new Hono<SessionEnv>();
  // Honoでは `/party/*` が `/party` にも一致する。両方に付けると、セッションを2回確かめる。
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
      body.data.card,
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

  // 編成で、固有スキルにセットする段階を選ぶ。取得した段階だけを選べる。
  api.put("/party/characters/:characterId/uniques/:index", async (c) => {
    const characterId = characterIdSchema.safeParse(c.req.param("characterId"));
    const index = uniqueIndexSchema.safeParse(c.req.param("index"));
    const body = await parseJson(c, setUniqueSchema);
    if (!characterId.success || !index.success || !body.success) {
      return invalidRequest(c);
    }
    const result = await createPartyRepository(c.env.DB).setUnique(
      c.get("userId"),
      characterId.data,
      index.data,
      body.data.stage,
      new Date().toISOString(),
    );
    return respond(c, result, errorStatus);
  });

  // 冒険の戦闘で起きた固有スキルの進化を1段保存する。
  api.post(
    "/party/characters/:characterId/uniques/:index/evolve",
    async (c) => {
      const characterId = characterIdSchema.safeParse(
        c.req.param("characterId"),
      );
      const index = uniqueIndexSchema.safeParse(c.req.param("index"));
      const body = await parseJson(c, evolveUniqueSchema);
      if (!characterId.success || !index.success || !body.success) {
        return invalidRequest(c);
      }
      const result = await createPartyRepository(c.env.DB).evolveUnique(
        c.get("userId"),
        characterId.data,
        index.data,
        body.data.stage,
        new Date().toISOString(),
      );
      return respond(c, result, errorStatus);
    },
  );

  return api;
}
