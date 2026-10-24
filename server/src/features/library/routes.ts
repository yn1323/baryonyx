import { Hono } from "hono";
import { requireSession, type SessionEnv } from "../accounts/session.js";
import { createLibraryRepository } from "./repository.js";

export function createLibraryApi() {
  const api = new Hono<SessionEnv>();
  // Honoでは `/library/*` が `/library` にも一致する。両方に付けると、セッションを2回確かめる。
  api.use("/library/*", requireSession);

  // 見つけた記録（doc/features/library.md）。定義は返さず、クライアントが各機能の正本と重ねる。
  api.get("/library", async (c) => {
    const state = await createLibraryRepository(c.env.DB).getState(
      c.get("userId"),
    );
    return c.json(state);
  });

  // 日々の歩数。運動報酬で記録した日ごとの歩数を読むだけで、書き込まない。
  api.get("/library/steps", async (c) => {
    const state = await createLibraryRepository(c.env.DB).getSteps(
      c.get("userId"),
    );
    return c.json(state);
  });

  return api;
}
