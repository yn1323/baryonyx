import assert from "node:assert/strict";
import { execFile } from "node:child_process";
import {
  mkdir,
  mkdtemp,
  readdir,
  readFile,
  rm,
  writeFile,
} from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { test } from "node:test";
import { promisify } from "node:util";

test("公開成果物は現行のSQLだけを含み、古いSQLとDrizzleのmetaを除外する", async () => {
  const fixture = await mkdtemp(join(tmpdir(), "baryonyx-artifact-"));
  try {
    await mkdir(join(fixture, "migrations/meta"), { recursive: true });
    await mkdir(join(fixture, "dist/migrations"), { recursive: true });
    const sql = "SELECT 1;\n";
    await writeFile(join(fixture, "migrations/0000_initial.sql"), sql);
    await writeFile(join(fixture, "migrations/meta/_journal.json"), "{}");
    await writeFile(
      join(fixture, "dist/migrations/0002_obsolete.sql"),
      "SELECT 2;",
    );
    await writeFile(
      join(fixture, "wrangler.json"),
      JSON.stringify({ compatibility_date: "2026-09-10" }),
    );
    await promisify(execFile)(
      process.execPath,
      [resolve("scripts/artifact.mjs")],
      { cwd: fixture },
    );
    assert.deepEqual(await readdir(join(fixture, "dist/migrations")), [
      "0000_initial.sql",
    ]);
    assert.equal(
      await readFile(join(fixture, "dist/migrations/0000_initial.sql"), "utf8"),
      sql,
    );
    assert.deepEqual(await readdir(join(fixture, "migrations/meta")), [
      "_journal.json",
    ]);
  } finally {
    await rm(fixture, { recursive: true, force: true });
  }
});
