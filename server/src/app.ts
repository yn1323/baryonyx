import { Hono } from "hono";
import { createHealthApi } from "./features/health/routes.js";

const app = new Hono<{ Bindings: Env }>();

app.use("*", async (c, next) => {
  c.header("X-Build-SHA", c.env?.BUILD_SHA ?? "local");
  await next();
});

app.get("/health", (c) => c.json({ status: "ok" }));

app.get("/ready", async (c) => {
  c.header("Cache-Control", "no-store");
  try {
    const result = await c.env.DB.prepare("SELECT 1 AS ok").first<{
      ok: number;
    }>();
    if (result?.ok === 1) {
      return c.json({ status: "ok", database: "ok" });
    }
  } catch {
    // 接続エラーの詳細やDB識別子はHTTP応答へ含めない。
  }
  return c.json({ status: "unavailable", database: "unavailable" }, 503);
});

app.route("/v1", createHealthApi());

export default app;
