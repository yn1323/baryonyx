import { Hono } from "hono";
import { createDatabase } from "../../shared/db.js";
import { invalidRequest, parseJson, respond } from "../../shared/http.js";
import { requireSession, type SessionEnv } from "../accounts/session.js";
import { createWorldAtlasRepository } from "./repository.js";
import { openSchema } from "./schema.js";

export function createWorldAtlasApi() {
  const api = new Hono<SessionEnv>();
  // Honoでは `/world-atlas/*` が `/world-atlas` にも一致する。
  api.use("/world-atlas/*", requireSession);

  // 入れ子の地図で開けたマスと、今の残高（doc/features/world-atlas.md#マスを開くルーン）。
  api.get("/world-atlas", async (c) => {
    const state = await createWorldAtlasRepository(
      createDatabase(c.env.DB),
    ).read(c.get("userId"));
    return c.json(state);
  });

  // マスを1つ、ルーンを払って開ける。駒がそのマスへ跳ぶ直前に、1マスずつ送る。
  api.post("/world-atlas/open", async (c) => {
    const body = await parseJson(c, openSchema);
    if (!body.success) return invalidRequest(c);
    const result = await createWorldAtlasRepository(
      createDatabase(c.env.DB),
    ).open(c.get("userId"), body.data, new Date().toISOString());
    return respond(c, result);
  });

  return api;
}
