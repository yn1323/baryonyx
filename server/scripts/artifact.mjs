import {
  copyFile,
  mkdir,
  readdir,
  readFile,
  rm,
  writeFile,
} from "node:fs/promises";

// CIではビルド済みWorkerとSQLだけを、認証情報を持つ公開jobへ渡す。
const config = JSON.parse(await readFile("wrangler.json", "utf8"));
await rm("dist/migrations", { recursive: true, force: true });
await mkdir("dist/migrations", { recursive: true });
for (const file of await readdir("migrations", { withFileTypes: true })) {
  if (file.isFile() && file.name.endsWith(".sql")) {
    await copyFile(`migrations/${file.name}`, `dist/migrations/${file.name}`);
  }
}
await writeFile(
  "dist/build-info.json",
  JSON.stringify({
    revision: process.env.GITHUB_SHA ?? "local",
    compatibility_date: config.compatibility_date,
    compatibility_flags: config.compatibility_flags ?? [],
  }),
);
