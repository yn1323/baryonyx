import type { Context, Hono } from "hono";
import { bodyLimit } from "hono/body-limit";
import type { Env as HonoEnv } from "hono/types";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import type { z } from "zod";

// 公開APIに共通の応答設定。認証は機能ごとのパスへ付ける。
export function applyApiDefaults<E extends HonoEnv>(api: Hono<E>) {
  api.use("*", async (c, next) => {
    c.header("Cache-Control", "no-store");
    await next();
  });
  api.use(
    "*",
    bodyLimit({
      maxSize: 16_384,
      onError: (c) => c.json({ error: "request_too_large" }, 413),
    }),
  );
  api.onError(
    () =>
      new Response(JSON.stringify({ error: "service_unavailable" }), {
        status: 503,
        headers: {
          "Content-Type": "application/json",
          "Cache-Control": "no-store",
        },
      }),
  );
  return api;
}

// 本文のJSONをスキーマで検証する。本文がJSONでないときも失敗として返す。
export async function parseJson<T extends z.ZodType>(c: Context, schema: T) {
  return schema.safeParse(await c.req.json().catch(() => null));
}

export function invalidRequest(c: Context) {
  return c.json({ error: "invalid_request" }, 400);
}

// 業務処理のエラーのうち、指した物がない（持っていない・見つからない・進行中の冒険がない）ものは404で返す。
// そのほか（今の状態や決まりと合わない要求、ルーンや素材の不足、要求IDの使い回し）は409で返す。
// 同じエラーは、どの機能から返しても同じステータスにする。
const NOT_FOUND_ERRORS = new Set([
  "unknown_destination",
  "no_adventure",
  "character_not_owned",
  "card_not_owned",
  "item_not_owned",
  "target_not_found",
  "goods_not_found",
  "square_not_found",
]);

export function errorStatus(error: string): ContentfulStatusCode {
  return NOT_FOUND_ERRORS.has(error) ? 404 : 409;
}

// 業務処理の結果を返す。`{ error }` を返したときは、errorStatus で決めたステータスで返す。
export function respond(c: Context, result: object) {
  if ("error" in result && typeof result.error === "string") {
    return c.json({ error: result.error }, errorStatus(result.error));
  }
  return c.json(result);
}
