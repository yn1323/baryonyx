import { cp, mkdir, readFile, writeFile } from "node:fs/promises";

// CIではビルド済みWorkerとSQLだけを、認証情報を持つ公開jobへ渡す。
const config = JSON.parse(await readFile("wrangler.json", "utf8"));
await mkdir("dist", { recursive: true });
await cp("migrations", "dist/migrations", { recursive: true });
await writeFile(
  "dist/build-info.json",
  JSON.stringify({
    revision: process.env.GITHUB_SHA ?? "local",
    compatibility_date: config.compatibility_date,
    compatibility_flags: config.compatibility_flags ?? [],
  }),
);
