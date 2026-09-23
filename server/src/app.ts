import { sql } from "drizzle-orm";
import { Hono } from "hono";
import {
  type VerifyIdentity,
  verifyGoogleIdentity,
} from "./features/accounts/auth.js";
import { createAccountsApi } from "./features/accounts/routes.js";
import type { SessionEnv } from "./features/accounts/session.js";
import { createExerciseRewardsApi } from "./features/exercise-rewards/routes.js";
import { createHealthApi } from "./features/health/routes.js";
import { createDatabase } from "./shared/db.js";
import { applyApiDefaults } from "./shared/http.js";

// 公開APIを組み立てる。テストでは本人確認だけを差し替える。
export function createApi(
  verifyIdentity: VerifyIdentity = verifyGoogleIdentity,
) {
  const api = applyApiDefaults(new Hono<SessionEnv>());
  api.route("/", createAccountsApi(verifyIdentity));
  api.route("/", createHealthApi());
  api.route("/", createExerciseRewardsApi());
  return api;
}

const app = new Hono<{ Bindings: Env }>();

app.use("*", async (c, next) => {
  c.header("X-Build-SHA", c.env?.BUILD_SHA ?? "local");
  await next();
});

app.get("/health", (c) => c.json({ status: "ok" }));

app.get("/ready", async (c) => {
  c.header("Cache-Control", "no-store");
  try {
    const result = await createDatabase(c.env.DB).get<{ ok: number }>(
      sql`SELECT 1 AS ok`,
    );
    if (result?.ok === 1) {
      return c.json({ status: "ok", database: "ok" });
    }
  } catch {
    // 接続エラーの詳細やDB識別子はHTTP応答へ含めない。
  }
  return c.json({ status: "unavailable", database: "unavailable" }, 503);
});

app.route("/v1", createApi());

export default app;
