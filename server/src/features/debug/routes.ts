import { sql } from "drizzle-orm";
import { Hono } from "hono";
import { createDatabase } from "../../shared/db.js";
import { requireSession, type SessionEnv } from "../accounts/session.js";
import { creditRunes, selectRuneBalance } from "../exercise-rewards/wallet.js";

// 開発中の確かめ用の操作（Homeのデバッグルームの一覧から呼ぶ）。DEBUG_TOOLS が "on" の環境（ローカルとDev）でだけ
// 使え、Prodでは見つからない（404）として扱う。値は scripts/deploy.mjs が環境ごとに渡す。
export const DEBUG_RUNES = 10000;

export function debugToolsEnabled(env: { DEBUG_TOOLS?: string } | undefined) {
  return env?.DEBUG_TOOLS === "on";
}

export function createDebugApi() {
  const api = new Hono<SessionEnv>();
  api.use("/debug/*", async (c, next) => {
    if (!debugToolsEnabled(c.env)) return c.json({ error: "not_found" }, 404);
    await next();
  });
  api.use("/debug/*", requireSession);

  // 所持ルーンに DEBUG_RUNES を足し、足したあとの残高を返す。記録は残さない。
  api.post("/debug/runes", async (c) => {
    const db = createDatabase(c.env.DB);
    const userId = c.get("userId");
    const now = new Date().toISOString();
    const [, , rows] = await db.batch([
      ...creditRunes(db, userId, DEBUG_RUNES, sql`1`, now),
      selectRuneBalance(db, userId),
    ]);
    return c.json({ runes: DEBUG_RUNES, balanceAfter: rows[0]?.balance ?? 0 });
  });

  return api;
}
