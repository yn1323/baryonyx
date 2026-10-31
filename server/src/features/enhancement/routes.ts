import { Hono } from "hono";
import { invalidRequest, parseJson, respond } from "../../shared/http.js";
import { requireSession, type SessionEnv } from "../accounts/session.js";
import { createEnhancementRepository } from "./repository.js";
import { awakenSchema, combineSchema, polishSchema } from "./schema.js";

export function createEnhancementApi() {
  const api = new Hono<SessionEnv>();
  api.use("/enhancement/*", requireSession);

  // 持っている強化素材（doc/features/enhancement.md の強化素材）。
  api.get("/enhancement", async (c) =>
    c.json(
      await createEnhancementRepository(c.env.DB).getState(c.get("userId")),
    ),
  );

  // 商会の品質強化。磨き石とルーンで、1つの物の倍率を上げる。
  api.post("/enhancement/polish", async (c) => {
    const body = await parseJson(c, polishSchema);
    if (!body.success) return invalidRequest(c);
    const result = await createEnhancementRepository(c.env.DB).polish(
      c.get("userId"),
      body.data,
      new Date().toISOString(),
    );
    return respond(c, result);
  });

  // 磨き石の合成。1つ下の色3個とルーンで、上の色を作る。
  api.post("/enhancement/combine", async (c) => {
    const body = await parseJson(c, combineSchema);
    if (!body.success) return invalidRequest(c);
    const result = await createEnhancementRepository(c.env.DB).combine(
      c.get("userId"),
      body.data,
      new Date().toISOString(),
    );
    return respond(c, result);
  });

  // 商会の覚醒。覚醒の欠片とルーンで、固有スキルを1段進めるか、持っているスキル1枚を覚醒させる。
  api.post("/enhancement/awaken", async (c) => {
    const body = await parseJson(c, awakenSchema);
    if (!body.success) return invalidRequest(c);
    const result = await createEnhancementRepository(c.env.DB).awaken(
      c.get("userId"),
      body.data,
      new Date().toISOString(),
    );
    return respond(c, result);
  });

  return api;
}
