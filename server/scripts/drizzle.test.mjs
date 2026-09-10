import assert from "node:assert/strict";
import { execFile } from "node:child_process";
import { cp, mkdir, mkdtemp, readdir, rm } from "node:fs/promises";
import { join, resolve } from "node:path";
import { test } from "node:test";
import { promisify } from "node:util";

test("Drizzleスキーマとコミット対象のマイグレーションに差分がない", async () => {
  // Drizzle KitはWindowsの絶対パスを扱えないため、同じ作業場所の相対パスを渡す。
  await mkdir(".wrangler", { recursive: true });
  const output = await mkdtemp(".wrangler/drizzle-check-");
  try {
    await cp("migrations/meta", join(output, "meta"), { recursive: true });
    const { stdout, stderr } = await promisify(execFile)(process.execPath, [
      resolve("node_modules/drizzle-kit/bin.cjs"),
      "generate",
      "--dialect",
      "sqlite",
      "--schema",
      "./src/features/**/db-schema.ts",
      "--out",
      output,
      "--name",
      "schema_check",
    ]);
    assert.match(stdout, /No schema changes/, `${stdout}\n${stderr}`);
    assert.deepEqual(
      (await readdir(output)).filter((file) => file.endsWith(".sql")),
      [],
    );
  } finally {
    await rm(output, { recursive: true, force: true });
  }
});
