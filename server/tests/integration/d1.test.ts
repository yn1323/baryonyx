import { execFile } from "node:child_process";
import { mkdtemp, readdir, readFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { promisify } from "node:util";
import { eq } from "drizzle-orm";
import { convertV4MiniflareOptions, Miniflare } from "miniflare";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { appSessions, appUsers } from "../../src/features/health/db-schema.js";
import { createHealthRepository } from "../../src/features/health/repository.js";
import { createDatabase } from "../../src/shared/db.js";

describe("WorkersとD1の結合", () => {
  let worker: Miniflare;
  let stateDirectory: string;

  beforeAll(async () => {
    const config = JSON.parse(await readFile("wrangler.json", "utf8"));
    stateDirectory = await mkdtemp(join(tmpdir(), "baryonyx-d1-test-"));
    // 実運用と同じWranglerで全SQLを適用し、再適用で履歴が重複しないことも確認する。
    for (let attempt = 0; attempt < 2; attempt++) {
      await promisify(execFile)(
        process.execPath,
        [
          resolve("node_modules/wrangler/bin/wrangler.js"),
          "d1",
          "migrations",
          "apply",
          "DB",
          "--local",
          "--persist-to",
          stateDirectory,
        ],
        { env: { ...process.env, CI: "true", WRANGLER_SEND_METRICS: "false" } },
      );
    }
    worker = new Miniflare(
      convertV4MiniflareOptions({
        modules: true,
        scriptPath: "dist/index.js",
        compatibilityDate: config.compatibility_date,
        compatibilityFlags: config.compatibility_flags,
        bindings: config.vars,
        d1Databases: { DB: config.d1_databases[0].database_id },
        // 一時ディレクトリはこのテスト実行専用。Local・Preview・Dev・Prodと共有しない。
        resourcePersistencePath: join(stateDirectory, "v3"),
      }),
    );
  });

  afterAll(async () => {
    await worker?.dispose();
    if (stateDirectory)
      await rm(stateDirectory, { recursive: true, force: true });
  });

  it("空のDBへ全マイグレーションを適用でき、再適用しても履歴が重複しない", async () => {
    const db = await worker.getD1Database("DB");
    const expected = (await readdir("migrations"))
      .filter((name) => name.endsWith(".sql"))
      .sort();
    const applied = await db
      .prepare("SELECT name FROM d1_migrations ORDER BY name")
      .all<{ name: string }>();
    expect(applied.results.map((row: { name: string }) => row.name)).toEqual(
      expected,
    );
  });

  it("ビルド済みのHonoからD1へ接続できる", async () => {
    const response = await worker.dispatchFetch("http://localhost/ready");
    expect(response.status).toBe(200);
    expect(response.headers.get("cache-control")).toBe("no-store");
    expect(await response.json()).toEqual({ status: "ok", database: "ok" });
  });

  it("既存SQLのデータをDrizzleで扱い、セッション作成失敗時は期限切れ削除も戻す", async () => {
    const binding = (await worker.getD1Database("DB")) as unknown as D1Database;
    const db = createDatabase(binding);
    const repository = createHealthRepository(binding);
    const userId = "legacy-'quoted";
    await binding
      .prepare("INSERT INTO app_users(id, google_sub) VALUES (?, ?)")
      .bind(userId, "legacy-subject")
      .run();
    try {
      expect(await repository.findOrCreateUser("legacy-subject")).toBe(userId);
      await db
        .update(appUsers)
        .set({ googleSub: "updated-subject" })
        .where(eq(appUsers.id, userId));
      expect(
        await db.select().from(appUsers).where(eq(appUsers.id, userId)).get(),
      ).toEqual({
        id: userId,
        googleSub: "updated-subject",
      });
      await db
        .insert(appSessions)
        .values({ tokenHash: "expired", userId, expiresAt: 0 });

      await expect(
        repository.createSession("failed", "missing-user", Date.now() + 60_000),
      ).rejects.toThrow();
      expect(
        await db
          .select()
          .from(appSessions)
          .where(eq(appSessions.tokenHash, "expired"))
          .get(),
      ).toBeDefined();
      expect(
        await db
          .select()
          .from(appSessions)
          .where(eq(appSessions.tokenHash, "failed"))
          .get(),
      ).toBeUndefined();

      await repository.createSession("valid", userId, Date.now() + 60_000);
      expect(await repository.findSessionUser("valid")).toBe(userId);
      expect(
        await db
          .select()
          .from(appSessions)
          .where(eq(appSessions.tokenHash, "expired"))
          .get(),
      ).toBeUndefined();
      await repository.deleteSession("valid");
      expect(await repository.findSessionUser("valid")).toBeUndefined();
    } finally {
      await db.delete(appSessions).where(eq(appSessions.userId, userId));
      await db.delete(appUsers).where(eq(appUsers.id, userId));
    }
  });

  it("D1で作成・取得・更新・削除でき、失敗したバッチはロールバックする", async () => {
    const db = await worker.getD1Database("DB");
    await db.exec(
      "CREATE TABLE ci_probe (id TEXT PRIMARY KEY, value TEXT NOT NULL)",
    );
    const id = "probe-'quoted";
    try {
      await db
        .prepare("INSERT INTO ci_probe VALUES (?, ?)")
        .bind(id, "first")
        .run();
      expect(
        await db
          .prepare("SELECT value FROM ci_probe WHERE id = ?")
          .bind(id)
          .first("value"),
      ).toBe("first");
      await db
        .prepare("UPDATE ci_probe SET value = ? WHERE id = ?")
        .bind("updated", id)
        .run();
      expect(
        await db
          .prepare("SELECT value FROM ci_probe WHERE id = ?")
          .bind(id)
          .first("value"),
      ).toBe("updated");

      await expect(
        db.batch([
          db
            .prepare("INSERT INTO ci_probe VALUES (?, ?)")
            .bind("rollback", "temporary"),
          db
            .prepare("INSERT INTO ci_probe VALUES (?, ?)")
            .bind(id, "duplicate"),
        ]),
      ).rejects.toThrow();
      expect(
        await db
          .prepare("SELECT value FROM ci_probe WHERE id = ?")
          .bind("rollback")
          .first(),
      ).toBeNull();

      await db.prepare("DELETE FROM ci_probe WHERE id = ?").bind(id).run();
      expect(
        await db
          .prepare("SELECT value FROM ci_probe WHERE id = ?")
          .bind(id)
          .first(),
      ).toBeNull();
    } finally {
      await db.exec("DROP TABLE ci_probe");
    }
  });
});
