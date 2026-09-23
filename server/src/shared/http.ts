import type { Hono } from "hono";
import { bodyLimit } from "hono/body-limit";
import type { Env as HonoEnv } from "hono/types";

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
