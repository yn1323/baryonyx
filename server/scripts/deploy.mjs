import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import {
  appendFile,
  mkdir,
  readdir,
  readFile,
  realpath,
  writeFile,
} from "node:fs/promises";
import { dirname, isAbsolute, relative, resolve } from "node:path";
import { setTimeout as delay } from "node:timers/promises";
import { fileURLToPath, pathToFileURL } from "node:url";

const serverRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

export function targetNames(stage, pr) {
  if (stage === "preview") {
    assert.match(pr ?? "", /^[1-9]\d{0,9}$/, "有効なPR番号が必要です");
    return {
      worker: `baryonyx-server-pr-${pr}`,
      database: `baryonyx-server-pr-${pr}`,
    };
  }
  assert.ok(
    stage === "dev" || stage === "prod",
    "環境はdev、prod、previewのみ指定できます",
  );
  return {
    worker: `baryonyx-server-${stage}`,
    database: `baryonyx-server-${stage}`,
  };
}

export function cloudflareClient(accountId, token, fetcher = fetch) {
  assert.match(
    accountId ?? "",
    /^[a-f0-9]{32}$/i,
    "CLOUDFLARE_ACCOUNT_IDが必要です",
  );
  assert.ok(token, "CLOUDFLARE_API_TOKENが必要です");
  return async (path, options = {}) => {
    const { allowMissing = false, ...init } = options;
    const response = await fetcher(
      `https://api.cloudflare.com/client/v4/accounts/${accountId}${path}`,
      {
        ...init,
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json",
        },
        redirect: "error",
        signal: AbortSignal.timeout(30_000),
      },
    );
    if (allowMissing && response.status === 404) return null;
    const body =
      response.status === 204
        ? { success: true, result: null }
        : await response.json();
    if (!response.ok || !body.success) {
      // API応答本文にはアカウント情報が含まれ得るため、ステータスとコードだけを報告する。
      throw new Error(
        `Cloudflare API: HTTP ${response.status}, codes=${body.errors?.map((error) => error.code).join(",") ?? "unknown"}`,
      );
    }
    return body.result;
  };
}

export async function findDatabase(api, name) {
  // APIのnameフィルターに加え、完全一致を確認して他環境を保護する。
  for (let page = 1; ; page++) {
    const databases = await api(
      `/d1/database?name=${encodeURIComponent(name)}&per_page=100&page=${page}`,
    );
    const match = databases.find((database) => database.name === name);
    if (match) return match;
    if (databases.length < 100) return null;
  }
}

export async function ensureDatabase(api, name) {
  const existing = await findDatabase(api, name);
  if (existing) return existing;
  return api("/d1/database", {
    method: "POST",
    body: JSON.stringify({ name }),
  });
}

export async function cleanupPreview(api, pr) {
  const names = targetNames("preview", pr);
  // Workerの削除に失敗した場合は、まだ接続中のDBを削除しない。
  await api(`/workers/scripts/${names.worker}`, {
    method: "DELETE",
    allowMissing: true,
  });
  const database = await findDatabase(api, names.database);
  if (database) {
    assert.match(database.uuid, /^[a-f0-9-]{36}$/i);
    await api(`/d1/database/${database.uuid}`, {
      method: "DELETE",
      allowMissing: true,
    });
  }
}

function wrangler(args) {
  const result = spawnSync(
    process.execPath,
    [resolve(serverRoot, "node_modules/wrangler/bin/wrangler.js"), ...args],
    {
      cwd: serverRoot,
      stdio: "inherit",
      env: { ...process.env, CI: "true", WRANGLER_SEND_METRICS: "false" },
    },
  );
  if (result.error) throw result.error;
  assert.equal(result.status, 0, "Wranglerの処理に失敗しました");
}

export async function verifyHttp(
  url,
  revision,
  fetcher = fetch,
  retry = delay,
) {
  for (let attempt = 0; attempt < 12; attempt++) {
    try {
      for (const path of ["/health", "/ready"]) {
        const response = await fetcher(`${url}${path}`, {
          redirect: "error",
          signal: AbortSignal.timeout(10_000),
        });
        assert.equal(response.status, 200);
        assert.equal(response.headers.get("x-build-sha"), revision);
        assert.deepEqual(
          await response.json(),
          path === "/ready"
            ? { status: "ok", database: "ok" }
            : { status: "ok" },
        );
      }
      return;
    } catch {
      if (attempt === 11)
        throw new Error("公開したコミットのHTTP・D1疎通を確認できませんでした");
      await retry(5_000);
    }
  }
}

async function deploy(api, stage, pr) {
  const names = targetNames(stage, pr);
  const artifact = resolve(
    serverRoot,
    process.env.SERVER_ARTIFACT_DIR ?? "dist",
  );
  const info = JSON.parse(
    await readFile(resolve(artifact, "build-info.json"), "utf8"),
  );
  assert.match(
    info.revision,
    /^[a-f0-9]{40}$/i,
    "ビルドしたGitコミットのSHAが必要です",
  );
  assert.match(info.compatibility_date, /^\d{4}-\d{2}-\d{2}$/);
  assert.ok(
    Array.isArray(info.compatibility_flags) &&
      info.compatibility_flags.every((flag) => typeof flag === "string"),
  );
  for (const path of ["index.js", "migrations"]) {
    const child = relative(
      await realpath(artifact),
      await realpath(resolve(artifact, path)),
    );
    assert.ok(
      !isAbsolute(child) && !child.startsWith(".."),
      "成果物の参照先が範囲外です",
    );
  }
  const files = await readdir(resolve(artifact, "migrations"), {
    withFileTypes: true,
  });
  assert.ok(
    files.length > 0 &&
      files.every(
        (file) => file.isFile() && /^\d{4}_[a-z0-9_-]+\.sql$/.test(file.name),
      ),
    "マイグレーションのファイル名が不正です",
  );
  const { subdomain } = await api("/workers/subdomain");
  assert.match(
    subdomain ?? "",
    /^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$/,
    "Cloudflareでworkers.devのサブドメインを設定してください",
  );
  const database = await ensureDatabase(api, names.database);
  assert.equal(database.name, names.database);
  assert.match(database.uuid, /^[a-f0-9-]{36}$/i);
  const configDir = resolve(serverRoot, ".wrangler/deploy");
  await mkdir(configDir, { recursive: true });
  const configPath = resolve(configDir, "wrangler.json");
  await writeFile(
    configPath,
    JSON.stringify({
      name: names.worker,
      main: resolve(artifact, "index.js"),
      compatibility_date: info.compatibility_date,
      compatibility_flags: info.compatibility_flags,
      workers_dev: true,
      preview_urls: false,
      vars: { BUILD_SHA: info.revision },
      d1_databases: [
        {
          binding: "DB",
          database_name: names.database,
          database_id: database.uuid,
          migrations_dir: resolve(artifact, "migrations"),
        },
      ],
    }),
  );
  wrangler([
    "d1",
    "migrations",
    "apply",
    names.database,
    "--remote",
    "--config",
    configPath,
  ]);
  // PRのpackage.json、Wrangler設定、ビルドフックは公開jobで実行しない。
  wrangler(["deploy", "--no-bundle", "--config", configPath]);
  const url = `https://${names.worker}.${subdomain}.workers.dev`;
  if (process.env.GITHUB_OUTPUT)
    await appendFile(process.env.GITHUB_OUTPUT, `url=${url}\n`);
  await verifyHttp(url, info.revision);
  if (process.env.GITHUB_STEP_SUMMARY) {
    await appendFile(
      process.env.GITHUB_STEP_SUMMARY,
      `### Server ${stage}\n\n[APIの疎通確認](${url}/ready)\n\nCommit: \`${info.revision}\`\n\nDBは保持し、未適用のマイグレーションだけを反映しました。\n`,
    );
  }
  console.info(`Server ${stage}: ${url}`);
}

async function main() {
  const [command, stage, pr] = process.argv.slice(2);
  assert.ok(
    command === "deploy" || command === "cleanup",
    "deployまたはcleanupを指定してください",
  );
  if (command === "cleanup")
    assert.equal(stage, "preview", "削除はPreviewだけに限定しています");
  targetNames(stage, pr);
  const api = cloudflareClient(
    process.env.CLOUDFLARE_ACCOUNT_ID,
    process.env.CLOUDFLARE_API_TOKEN,
  );
  if (command === "cleanup") await cleanupPreview(api, pr);
  else await deploy(api, stage, pr);
}

if (
  process.argv[1] &&
  import.meta.url === pathToFileURL(resolve(process.argv[1])).href
) {
  await main();
}
