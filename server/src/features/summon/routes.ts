import { Hono } from "hono";
import { invalidRequest, parseJson, respond } from "../../shared/http.js";
import { requireSession, type SessionEnv } from "../accounts/session.js";
import { createSummonRepository } from "./repository.js";
import { summonSchema } from "./schema.js";

export function createSummonApi() {
  const api = new Hono<SessionEnv>();
  api.use("/summon", requireSession);

  // キャラの絆とかけらの数。
  api.get("/summon", async (c) =>
    c.json(await createSummonRepository(c.env.DB).getState(c.get("userId"))),
  );

  // 神殿で1回か10回召喚する（doc/features/monetization.md の召喚）。ルーンが足りない・要求IDの使い回しは409で返す。
  api.post("/summon", async (c) => {
    const body = await parseJson(c, summonSchema);
    if (!body.success) return invalidRequest(c);
    const result = await createSummonRepository(c.env.DB).summon(
      c.get("userId"),
      body.data,
      new Date().toISOString(),
    );
    return respond(c, result);
  });

  return api;
}
