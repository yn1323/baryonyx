import { Hono } from "hono";
import { invalidRequest, parseJson, respond } from "../../shared/http.js";
import { requireSession, type SessionEnv } from "../accounts/session.js";
import { createAdventureRepository } from "./repository.js";
import {
  clearSchema,
  endSchema,
  moveSchema,
  reviveSchema,
  startSchema,
} from "./schema.js";

// 冒険がない・行き先が分からないときは404、今の冒険の状態と合わない要求は409で返す。
function errorStatus(error: string) {
  return error === "no_adventure" || error === "unknown_destination"
    ? 404
    : 409;
}

export function createAdventureApi() {
  const api = new Hono<SessionEnv>();
  api.use("/adventure", requireSession);
  api.use("/adventure/*", requireSession);

  // 進行中の冒険・行き先ごとの記録・所持ルーン。
  api.get("/adventure", async (c) => {
    const state = await createAdventureRepository(c.env.DB).getState(
      c.get("userId"),
    );
    return c.json(state);
  });

  api.post("/adventure", async (c) => {
    const body = await parseJson(c, startSchema);
    if (!body.success) return invalidRequest(c);
    const result = await createAdventureRepository(c.env.DB).start(
      c.get("userId"),
      body.data.destinationId,
      new Date().toISOString(),
    );
    return respond(c, result, errorStatus);
  });

  api.post("/adventure/move", async (c) => {
    const body = await parseJson(c, moveSchema);
    if (!body.success) return invalidRequest(c);
    const result = await createAdventureRepository(c.env.DB).move(
      c.get("userId"),
      body.data,
      new Date().toISOString(),
    );
    return respond(c, result, errorStatus);
  });

  api.post("/adventure/clear", async (c) => {
    const body = await parseJson(c, clearSchema);
    if (!body.success) return invalidRequest(c);
    const result = await createAdventureRepository(c.env.DB).clear(
      c.get("userId"),
      body.data.roomId,
      new Date().toISOString(),
    );
    return respond(c, result, errorStatus);
  });

  api.post("/adventure/revive", async (c) => {
    const body = await parseJson(c, reviveSchema);
    if (!body.success) return invalidRequest(c);
    const result = await createAdventureRepository(c.env.DB).revive(
      c.get("userId"),
      body.data,
      new Date().toISOString(),
    );
    return respond(c, result, errorStatus);
  });

  api.post("/adventure/end", async (c) => {
    const body = await parseJson(c, endSchema);
    if (!body.success) return invalidRequest(c);
    const result = await createAdventureRepository(c.env.DB).end(
      c.get("userId"),
      body.data.reason,
      new Date().toISOString(),
    );
    return respond(c, result, errorStatus);
  });

  return api;
}
