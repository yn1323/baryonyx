import { Hono } from "hono";
import { invalidRequest, parseJson } from "../../shared/http.js";
import { requireSession, type SessionEnv } from "../accounts/session.js";
import { createCombatRepository } from "./repository.js";
import { revealWeaknessesSchema } from "./schema.js";

export function createCombatApi() {
  const api = new Hono<SessionEnv>();
  api.use("/combat/*", requireSession);

  // 開示した敵の弱点（doc/features/combat.md の弱点の開示）。
  api.get("/combat/weaknesses", async (c) => {
    const state = await createCombatRepository(c.env.DB).readWeaknesses(
      c.get("userId"),
    );
    return c.json(state);
  });

  // 開示した弱点を足して、記録の全体を返す。弱点は減らないため、要求IDなしで再送してよい。
  api.post("/combat/weaknesses", async (c) => {
    const body = await parseJson(c, revealWeaknessesSchema);
    if (!body.success) return invalidRequest(c);
    const state = await createCombatRepository(c.env.DB).reveal(
      c.get("userId"),
      body.data.weaknesses,
      new Date().toISOString(),
    );
    return c.json(state);
  });

  return api;
}
