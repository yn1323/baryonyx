import { sql } from "drizzle-orm";
import { Hono } from "hono";
import { z } from "zod";
import { createDatabase, type Statement } from "../../shared/db.js";
import { invalidRequest, parseJson, respond } from "../../shared/http.js";
import {
  multiplierOf,
  QUALITIES,
  qualityOf,
  rollMultiplierPercent,
} from "../../shared/quality.js";
import { requireSession, type SessionEnv } from "../accounts/session.js";
import { MATERIAL_IDS } from "../enhancement/catalog.js";
import { slotOf } from "../equipment/catalog.js";
import { creditRunes, selectRuneBalance } from "../exercise-rewards/wallet.js";
import { type HoldingCategory, maxPercentOf } from "../inventory/catalog.js";
import { type Grant, inventoryGrants } from "../inventory/grants.js";
import { ITEM_IDS } from "../items/catalog.js";
import { CARD_SKILL_IDS } from "../party/catalog.js";

// 開発中の確かめ用の操作（Homeのデバッグルームの一覧から呼ぶ）。DEBUG_TOOLS が "on" の環境（ローカルとDev）でだけ
// 使え、Prodでは見つからない（404）として扱う。値は scripts/deploy.mjs が環境ごとに渡す。
export const DEBUG_RUNES = 10000;

// 持ち物のデバッグルームで、1回に足せる数の上限。
export const DEBUG_GRANT_LIMIT = 10;

const HOLDING_CATEGORIES = [
  "skill",
  "weapon",
  "armor",
  "accessory",
  "item",
  "material",
] as const satisfies readonly HoldingCategory[];

// 持ち物を足す。スキル・装備は品質の幅から倍率を選び、アイテム・強化素材は品質を使わない。
export const grantSchema = z.object({
  category: z.enum(HOLDING_CATEGORIES),
  id: z.string().min(1).max(64),
  quality: z.enum(QUALITIES).default("S"),
  count: z.number().int().min(1).max(DEBUG_GRANT_LIMIT).default(1),
});

export function debugToolsEnabled(env: { DEBUG_TOOLS?: string } | undefined) {
  return env?.DEBUG_TOOLS === "on";
}

// 分類の定義にある種類か。
function knownHolding(category: HoldingCategory, id: string) {
  switch (category) {
    case "skill":
      return (CARD_SKILL_IDS as readonly string[]).includes(id);
    case "item":
      return (ITEM_IDS as readonly string[]).includes(id);
    case "material":
      return (MATERIAL_IDS as readonly string[]).includes(id);
    default:
      return slotOf(id) === category;
  }
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

  // 持ち物を count 個、無条件で足し、見つけた記録も書く。スキル・装備は1つずつ、選んだ品質の幅から倍率を選ぶ。
  // 足したスキル・装備の倍率と品質を返す（アイテム・強化素材は空）。記録は残さない。
  api.post("/debug/grant", async (c) => {
    const body = await parseJson(c, grantSchema);
    if (!body.success) return invalidRequest(c);
    const { category, id, quality, count } = body.data;
    if (!knownHolding(category, id))
      return respond(c, { error: "goods_not_found" });

    const maxPercent = maxPercentOf(category, id);
    const grants: Grant[] = Array.from({ length: count }, () => ({
      category,
      id,
      multiplierPercent:
        maxPercent === null
          ? null
          : rollMultiplierPercent(quality, Math.random, maxPercent),
    }));
    const db = createDatabase(c.env.DB);
    const statements = inventoryGrants(
      db,
      sql`1`,
      c.get("userId"),
      grants,
      new Date().toISOString(),
    );
    await db.batch(statements as unknown as [Statement, ...Statement[]]);
    return c.json({
      category,
      id,
      count,
      granted: grants.flatMap(({ multiplierPercent }) =>
        multiplierPercent === null || maxPercent === null
          ? []
          : [
              {
                multiplier: multiplierOf(multiplierPercent),
                quality: qualityOf(multiplierPercent, maxPercent),
              },
            ],
      ),
    });
  });

  return api;
}
