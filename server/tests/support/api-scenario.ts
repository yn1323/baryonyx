import { readdir, readFile } from "node:fs/promises";
import { convertV4MiniflareOptions, Miniflare } from "miniflare";
import { expect } from "vitest";
import { createApi } from "../../src/app.js";
import type { SessionEnv } from "../../src/features/accounts/session.js";

export type ApiScenario = Awaited<ReturnType<typeof createApiScenario>>;

export async function createApiScenario() {
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
    } as unknown as SessionEnv["Bindings"];
    const api = createApi(async (token) => {
      if (token === "invalid") throw new Error();
      return token;
    });
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
    // GETで読み、200で返ったことを確かめて本文を返す。
    const read = async <T>(path: string, token: string) => {
      const response = await request(path, "GET", undefined, token);
      expect(response.status).toBe(200);
      return (await response.json()) as T;
    };
    // 所持ルーンを直接書く。費用と残高の検査で、ルーンを貯める手順を省くために使う。
    const setRunes = async (userId: string, balance: number) => {
      await db
        .prepare(
          "INSERT INTO rune_wallets (user_id, balance, updated_at) VALUES (?, ?, ?) ON CONFLICT(user_id) DO UPDATE SET balance = excluded.balance",
        )
        .bind(userId, balance, new Date().toISOString())
        .run();
    };
    return {
      api,
      env,
      request,
      login,
      read,
      setRunes,
      dispose: () => mf.dispose(),
    };
  } catch (error) {
    await mf.dispose();
    throw error;
  }
}
