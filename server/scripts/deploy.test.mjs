import assert from "node:assert/strict";
import { test } from "node:test";
import {
  cleanupPreview,
  cloudflareClient,
  ensureDatabase,
  targetNames,
  verifyHttp,
} from "./deploy.mjs";

const database = {
  name: "baryonyx-server-pr-12",
  uuid: "01234567-89ab-cdef-0123-456789abcdef",
};

test("PreviewのPR番号を検証し、他環境を指定した削除対象を生成しない", () => {
  for (const pr of [
    undefined,
    "",
    "prod",
    "../dev",
    "12/../../prod",
    "0",
    "-1",
  ]) {
    assert.throws(() => targetNames("preview", pr));
  }
  assert.throws(() => targetNames("unknown"));
  assert.notEqual(targetNames("dev").database, targetNames("prod").database);
});

test("Preview再公開では既存DBを保持し、作成・削除しない", async () => {
  const calls = [];
  const result = await ensureDatabase(async (path, options) => {
    calls.push({ path, options });
    return [database];
  }, database.name);
  assert.deepEqual(result, database);
  assert.equal(calls.length, 1);
  assert.equal(calls[0].options, undefined);
});

test("名前が部分一致する別DBを再利用しない", async () => {
  const calls = [];
  await ensureDatabase(async (path, options) => {
    calls.push({ path, options });
    return options?.method === "POST"
      ? database
      : [{ ...database, name: `${database.name}3` }];
  }, database.name);
  assert.equal(calls.length, 2);
  assert.deepEqual(JSON.parse(calls[1].options.body), { name: database.name });
});

test("PR終了時はWorkerを先に削除し、そのPRのDBだけを削除する", async () => {
  const calls = [];
  await cleanupPreview(async (path, options) => {
    calls.push({ path, options });
    return options?.method === "DELETE" ? null : [database];
  }, "12");
  assert.equal(calls[0].path, "/workers/scripts/baryonyx-server-pr-12");
  assert.equal(calls[2].path, `/d1/database/${database.uuid}`);
  assert.equal(calls[2].options.method, "DELETE");
});

test("Worker削除が失敗した場合はDB削除へ進まない", async () => {
  let calls = 0;
  await assert.rejects(
    cleanupPreview(async () => {
      calls++;
      throw new Error("API unavailable");
    }, "12"),
  );
  assert.equal(calls, 1);
});

test("既に削除済みのPreviewを再度削除できる", async () => {
  const calls = [];
  await cleanupPreview(async (path, options) => {
    calls.push(path);
    return options?.method === "DELETE" ? null : [];
  }, "12");
  assert.equal(calls.length, 2);
});

test("認証エラーをリソース不存在と誤認せず、API応答やトークンをエラーへ含めない", async () => {
  const api = cloudflareClient(
    "a".repeat(32),
    "test-token",
    async () =>
      new Response(
        JSON.stringify({
          success: false,
          errors: [{ code: 10000, message: "private account information" }],
        }),
        { status: 403 },
      ),
  );
  await assert.rejects(
    api("/workers/scripts/example", { allowMissing: true }),
    (error) => {
      assert.match(error.message, /HTTP 403/);
      assert.doesNotMatch(error.message, /test-token|private account/);
      return true;
    },
  );
});

test("公開確認は対象コミットとD1正常応答の両方を要求する", async () => {
  const revision = "a".repeat(40);
  let calls = 0;
  await verifyHttp("https://preview.example", revision, async (url) => {
    calls++;
    return new Response(
      JSON.stringify(
        url.endsWith("/ready")
          ? { status: "ok", database: "ok" }
          : { status: "ok" },
      ),
      {
        headers: { "x-build-sha": revision },
      },
    );
  });
  assert.equal(calls, 2);
  await assert.rejects(
    verifyHttp(
      "https://preview.example",
      revision,
      async () =>
        new Response('{"status":"ok"}', {
          headers: { "x-build-sha": "old-commit" },
        }),
      async () => {},
    ),
  );
  await assert.rejects(
    verifyHttp(
      "https://preview.example",
      revision,
      async (url) =>
        new Response('{"status":"ok"}', {
          status: url.endsWith("/ready") ? 503 : 200,
          headers: { "x-build-sha": revision },
        }),
      async () => {},
    ),
  );
});
