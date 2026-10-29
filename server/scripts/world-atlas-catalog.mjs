// 入れ子の地図のマスの値段とつながりを、クライアントの地図の配置（Layouts/*.json）から
// src/features/world-atlas/squares.ts へ書き出す（doc/features/world-atlas.md#マスを開くルーン）。
// 値段は client/ArtSource/WorldMap/Atlas/scripts/price_squares.py が配置へ書いたものを写す。
// 配置を変えたら `node scripts/world-atlas-catalog.mjs` で書き直す。--check は書かずに古いかだけを確かめる。
import { execFileSync } from "node:child_process";
import { readdirSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const serverRoot = path.resolve(here, "..");
export const layoutsDir = path.resolve(
  serverRoot,
  "../client/Assets/Baryonyx/Features/WorldMap/Atlas/Layouts",
);
export const outputPath = path.resolve(
  serverRoot,
  "src/features/world-atlas/squares.ts",
);

const startOf = (key) => (key === "world" ? "world-start" : `gate-${key}`);

/** 配置の一覧から、地図ごとの始まりのマスと、マスごとの値段・隣のマスを作る。 */
export function buildCatalog(layouts) {
  const maps = {};
  for (const layout of [...layouts].sort((a, b) =>
    a.key.localeCompare(b.key),
  )) {
    const links = new Map(layout.nodes.map((node) => [node.id, []]));
    for (const line of layout.lines) {
      links.get(line.a).push(line.b);
      links.get(line.b).push(line.a);
    }
    const start = startOf(layout.key);
    const squares = {};
    for (const node of layout.nodes) {
      if (node.id === start) continue;
      if (!Number.isInteger(node.price) || node.price <= 0) {
        throw new Error(`${layout.key}/${node.id} has no price`);
      }
      squares[node.id] = [node.price, ...links.get(node.id).sort()];
    }
    maps[layout.key] = { start, squares };
  }
  return maps;
}

export function readLayouts(dir = layoutsDir) {
  return readdirSync(dir)
    .filter((name) => name.endsWith(".json"))
    .map((name) => JSON.parse(readFileSync(path.join(dir, name), "utf8")));
}

export function render(maps) {
  return `// 入れ子の地図のマスの値段とつながり（doc/features/world-atlas.md#マスを開くルーン）。
// scripts/world-atlas-catalog.mjs がクライアントの地図の配置から生成する。手で直さない。
// マスごとに [値段, ...隣のマス] を持つ。始まりのマスはタダで、ここに含めない。

import type { AtlasMap } from "./catalog.js";

export const ATLAS_MAPS: Record<string, AtlasMap> = ${JSON.stringify(maps)};
`;
}

function format(file) {
  execFileSync(
    path.join(serverRoot, "node_modules/.bin/biome"),
    ["format", "--write", file],
    { cwd: serverRoot, stdio: "ignore" },
  );
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const text = render(buildCatalog(readLayouts()));
  if (process.argv.includes("--check")) {
    const tmp = `${outputPath}.check.ts`;
    writeFileSync(tmp, text);
    format(tmp);
    const fresh = readFileSync(tmp, "utf8");
    execFileSync("rm", [tmp]);
    if (fresh !== readFileSync(outputPath, "utf8")) {
      console.error(
        "squares.ts is stale: run node scripts/world-atlas-catalog.mjs",
      );
      process.exit(1);
    }
  } else {
    writeFileSync(outputPath, text);
    format(outputPath);
  }
}
