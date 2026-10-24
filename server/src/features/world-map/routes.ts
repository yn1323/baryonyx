import { Hono } from "hono";
import { invalidRequest, parseJson } from "../../shared/http.js";
import { requireSession, type SessionEnv } from "../accounts/session.js";
import { createWorldMapRepository } from "./repository.js";
import { moveSchema } from "./schema.js";

export function createWorldMapApi() {
  const api = new Hono<SessionEnv>();
  // Honoでは `/world-map/*` が `/world-map` にも一致する。
  api.use("/world-map/*", requireSession);

  // 街道の進み具合（doc/features/stage-progression.md の世界地図の形と試作）。
  api.get("/world-map", async (c) => {
    const state = await createWorldMapRepository(c.env.DB).read(
      c.get("userId"),
    );
    return c.json(state);
  });

  // マスへ着いたことを残す。マスと道の形はクライアントが持つため、つながりは確かめない（試作）。
  api.post("/world-map/move", async (c) => {
    const body = await parseJson(c, moveSchema);
    if (!body.success) return invalidRequest(c);
    const state = await createWorldMapRepository(c.env.DB).move(
      c.get("userId"),
      body.data,
      new Date().toISOString(),
    );
    return c.json(state);
  });

  return api;
}
