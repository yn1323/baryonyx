import { readdir, readFile } from "node:fs/promises";
import { convertV4MiniflareOptions, Miniflare } from "miniflare";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { days } from "../../src/features/health/fixtures.js";
import {
  createHealthApi,
  type HealthEnv,
  hashToken,
} from "../../src/features/health/routes.js";

const sourceId = "aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa";

describe("健康データAPI", () => {
  let mf: Miniflare;
  let env: HealthEnv["Bindings"];
  const api = createHealthApi(async (token) => {
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
  async function login(sub = "test-subject") {
    const response = await request("/auth/google", "POST", { idToken: sub });
    expect(response.status).toBe(200);
    return (await response.json()) as { token: string; userId: string };
  }
  beforeAll(async () => {
    mf = new Miniflare(
      convertV4MiniflareOptions({
        modules: true,
        script: "export default {fetch() {return new Response('ok')}}",
        d1Databases: ["DB"],
      }),
    );
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
        .filter((s) => s.trim())
        .map((s) => db.prepare(s)),
    );
    env = {
      DB: db,
      BUILD_SHA: "local",
      GOOGLE_CLIENT_ID: "test-audience",
    } as unknown as HealthEnv["Bindings"];
  });
  afterAll(async () => {
    await mf?.dispose();
  });
  it("認証設定がなければ失敗し、不正なトークンを拒否する", async () => {
    expect(
      (
        await api.request(
          "/auth/google",
          { method: "POST" },
          { ...env, GOOGLE_CLIENT_ID: undefined },
        )
      ).status,
    ).toBe(503);
    expect(
      (await request("/auth/google", "POST", { idToken: "invalid" })).status,
    ).toBe(401);
    expect((await request(`/health/sources/${sourceId}/days`)).status).toBe(
      401,
    );
  });
  it("本人の値を保存・取得し、再送、古い版、他ユーザーの操作を拒否する", async () => {
    const user = await login();
    const other = await login("another-subject");
    const lease = () =>
      request(
        "/health/syncs",
        "POST",
        { sourceId, provider: "health_connect" },
        user.token,
      );
    expect(await (await lease()).json()).toEqual({ revision: 1 });
    const put = (revision: number, values = days()) =>
      request(
        `/health/sources/${sourceId}/days`,
        "PUT",
        { revision, days: values },
        user.token,
      );
    expect((await put(1)).status).toBe(200);
    expect((await put(1)).status).toBe(409);
    expect(await (await lease()).json()).toEqual({ revision: 2 });
    expect((await put(1, days(999))).status).toBe(409);
    expect((await put(2, days(12))).status).toBe(200);
    const response = await request(
      `/health/sources/${sourceId}/days`,
      "GET",
      undefined,
      user.token,
    );
    expect(response.headers.get("cache-control")).toBe("no-store");
    const saved = (await response.json()) as { days: { steps: number }[] };
    expect(saved.days).toHaveLength(7);
    expect(saved.days.every((d) => d.steps === 12)).toBe(true);
    expect(
      (
        await request(
          `/health/sources/${sourceId}/days`,
          "GET",
          undefined,
          other.token,
        )
      ).status,
    ).toBe(404);
    expect(
      (
        await request(
          "/health/syncs",
          "POST",
          { sourceId, provider: "health_connect" },
          other.token,
        )
      ).status,
    ).toBe(404);
    expect(
      (
        await request(
          `/health/sources/${sourceId}/days`,
          "PUT",
          { revision: 2, days: days() },
          other.token,
        )
      ).status,
    ).toBe(409);
    await lease();
    expect(
      (
        await put(
          3,
          days(0).map((d) => ({ ...d, hasValue: false })),
        )
      ).status,
    ).toBe(200);
    const empty = (await (
      await request(
        `/health/sources/${sourceId}/days`,
        "GET",
        undefined,
        user.token,
      )
    ).json()) as {
      days: { steps: unknown; lastKnownSteps: number; hasValue: boolean }[];
    };
    expect(
      empty.days.every(
        (d) => d.steps === null && d.lastKnownSteps === 12 && !d.hasValue,
      ),
    ).toBe(true);
  });
  it("期限切れ・ログアウトしたセッションを拒否する", async () => {
    const user = await login("logout-subject");
    expect(
      (await request("/auth/logout", "POST", undefined, user.token)).status,
    ).toBe(204);
    expect(
      (await request("/auth/logout", "POST", undefined, user.token)).status,
    ).toBe(401);
    const expired = await login("expired-subject");
    await env.DB.prepare(
      "UPDATE app_sessions SET expires_at = 0 WHERE token_hash = ?",
    )
      .bind(await hashToken(expired.token))
      .run();
    expect(
      (
        await request(
          `/health/sources/${sourceId}/days`,
          "GET",
          undefined,
          expired.token,
        )
      ).status,
    ).toBe(401);
  });
  it("不正なJSONと大きすぎる要求を拒否する", async () => {
    expect(
      (await api.request("/auth/google", { method: "POST", body: "{" }, env))
        .status,
    ).toBe(400);
    expect(
      (await request("/auth/google", "POST", { idToken: "a".repeat(17_000) }))
        .status,
    ).toBe(413);
  });
});
