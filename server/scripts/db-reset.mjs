// ローカルD1の全テーブルを消して、マイグレーションを当て直し、開発用のプレイヤー（seed）を入れる。
// 使い方と前提は doc/rules/backend-design.md の「開発用データ（seed）」。Local以外のDBには接続しない。

import { spawnSync } from "node:child_process";
import { mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const serverRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const wrangler = resolve(serverRoot, "node_modules/wrangler/bin/wrangler.js");

// D1のローカル実装が持つ内部テーブル（_cf_）とSQLite自身の表は消さない。
export const listTablesSql =
  "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT GLOB 'sqlite_*' AND name NOT GLOB '_cf_*'";

// 外部キーの検査をcommit時へ遅らせ、親子の順を気にせずに消す。
export function dropTablesSql(names) {
  return [
    "PRAGMA defer_foreign_keys = ON;",
    ...names.map(
      (name) => `DROP TABLE IF EXISTS "${name.replaceAll('"', '""')}";`,
    ),
  ].join("\n");
}

export function seedSql(statements) {
  return `${statements.join(";\n")};\n`;
}

// 成功したときの出力は省き、失敗したときだけwranglerの出力を見せる。
function run(args) {
  const result = spawnSync(process.execPath, [wrangler, ...args], {
    cwd: serverRoot,
    encoding: "utf8",
    env: { ...process.env, CI: "true", WRANGLER_SEND_METRICS: "false" },
  });
  if (result.status !== 0) {
    process.stderr.write(result.stdout + result.stderr);
    throw new Error(`wrangler ${args.slice(0, 3).join(" ")} が失敗しました`);
  }
  return result.stdout;
}

async function executeFile(directory, name, sql) {
  const file = join(directory, name);
  await writeFile(file, sql);
  run(["d1", "execute", "DB", "--local", "--yes", "--file", file]);
}

async function main() {
  const { SEED_PLAYERS, buildSeedStatements } = await import(
    pathToFileURL(resolve(serverRoot, "scripts/seed.ts")).href
  );
  const [listed] = JSON.parse(
    run([
      "d1",
      "execute",
      "DB",
      "--local",
      "--json",
      "--command",
      listTablesSql,
    ]),
  );
  const tables = listed.results.map((row) => row.name);
  const directory = await mkdtemp(join(tmpdir(), "baryonyx-db-reset-"));
  try {
    if (tables.length > 0) {
      console.log(`テーブルを${tables.length}個消します。`);
      await executeFile(directory, "drop.sql", dropTablesSql(tables));
    }
    run(["d1", "migrations", "apply", "DB", "--local"]);
    console.log("マイグレーションを当て直しました。");
    await executeFile(directory, "seed.sql", seedSql(buildSeedStatements()));
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
  console.log("\nローカルD1を作り直し、次のプレイヤーを入れました。");
  for (const player of SEED_PLAYERS) {
    console.log(`- ${player.id}: ${player.label}`);
  }
  console.log(
    "UnityのメニューBaryonyx > Server > Seed Playerで選ぶと、EditorのPlayでそのプレイヤーとして接続します。",
  );
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  await main();
}
