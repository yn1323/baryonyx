import { Hono } from "hono";
import { invalidRequest, parseJson, respond } from "../../shared/http.js";
import { requireSession, type SessionEnv } from "../accounts/session.js";
import { createWorkshopRepository } from "./repository.js";
import { buySchema, craftSchema, sellSchema } from "./schema.js";

export function createWorkshopApi() {
  const api = new Hono<SessionEnv>();
  api.use("/workshop/*", requireSession);

  // 商会で、これまでに手に入れた物を品質の最低で1つ買う（doc/features/screens/shop.md の商会）。
  api.post("/workshop/buy", async (c) => {
    const body = await parseJson(c, buySchema);
    if (!body.success) return invalidRequest(c);
    const result = await createWorkshopRepository(c.env.DB).buy(
      c.get("userId"),
      body.data,
      new Date().toISOString(),
    );
    return respond(c, result);
  });

  // 商会で、☆が1つ低い同じ分類の物を3つ使い、これまでに手に入れた物を1つ作る（doc/features/screens/shop.md の商会）。
  api.post("/workshop/craft", async (c) => {
    const body = await parseJson(c, craftSchema);
    if (!body.success) return invalidRequest(c);
    const result = await createWorkshopRepository(c.env.DB).craft(
      c.get("userId"),
      body.data,
      new Date().toISOString(),
    );
    return respond(c, result);
  });

  // 商会で、キャラに付けていない持ち物を売り、ルーンを得る（doc/features/screens/shop.md の商会）。
  api.post("/workshop/sell", async (c) => {
    const body = await parseJson(c, sellSchema);
    if (!body.success) return invalidRequest(c);
    const result = await createWorkshopRepository(c.env.DB).sell(
      c.get("userId"),
      body.data,
      new Date().toISOString(),
    );
    return respond(c, result);
  });

  return api;
}
