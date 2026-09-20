import { readdir, readFile } from "node:fs/promises";
import { Hono } from "hono";
import { convertV4MiniflareOptions, Miniflare } from "miniflare";
import { expect } from "vitest";
import { createExerciseRewardsApi } from "../../src/features/exercise-rewards/routes.js";
import {
  createHealthApi,
  type HealthEnv,
} from "../../src/features/health/routes.js";

export type HealthScenario = Awaited<ReturnType<typeof createHealthScenario>>;

export async function createHealthScenario() {
  // 永続化先を指定せず、呼び出しごとにMiniflare専用の一時DBを作る。
  const mf = new Miniflare(
    convertV4MiniflareOptions({
      modules: true,
      script: "export default {fetch() {return new Response('ok')}}",
      d1Databases: ["DB"],
    }),
  );
  try {
    const db = await mf.getD1Database("DB");
    const files = (await readdir("migrations"))
      .filter((name) => name.endsWith(".sql"))
      .sort();
    const sql = (
      await Promise.all(
        files.map((name) => readFile(`migrations/${name}`, "utf8")),
      )
    ).join("\n");
    await db.batch(
      sql
        .split(";")
        .filter((statement) => statement.trim())
        .map((statement) => db.prepare(statement)),
    );
    const env = {
      DB: db,
      BUILD_SHA: "local",
      GOOGLE_CLIENT_ID: "test-audience",
    } as unknown as HealthEnv["Bindings"];
    const api = new Hono<HealthEnv>();
    api.route(
      "/",
      createHealthApi(async (token) => {
        if (token === "invalid") throw new Error();
        return token;
      }),
    );
    api.route("/", createExerciseRewardsApi());
    const request = (
      path: string,
      method = "GET",
      body?: unknown,
      token?: string,
    ) =>
      api.request(
        path,
        {
          method,
          headers: {
            "Content-Type": "application/json",
            ...(token ? { Authorization: `Bearer ${token}` } : {}),
          },
          body: body === undefined ? undefined : JSON.stringify(body),
        },
        env,
      );
    const login = async (sub = "test-subject") => {
      const response = await request("/auth/google", "POST", { idToken: sub });
      expect(response.status).toBe(200);
      return (await response.json()) as { token: string; userId: string };
    };
    return { api, env, request, login, dispose: () => mf.dispose() };
  } catch (error) {
    await mf.dispose();
    throw error;
  }
}
