import assert from "node:assert/strict";
import { execFile } from "node:child_process";
import { test } from "node:test";
import { promisify } from "node:util";

test("入れ子の地図のカタログが、クライアントの地図の配置と食い違っていない", async () => {
  // 配置（値段・道）を変えたのに squares.ts を書き直し忘れると、端末とサーバーの値段がずれる。
  await assert.doesNotReject(
    promisify(execFile)(process.execPath, [
      "scripts/world-atlas-catalog.mjs",
      "--check",
    ]),
  );
});
