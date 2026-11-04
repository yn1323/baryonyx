// 入れ子の地図（doc/features/world-atlas.md）。マスを開くルーンの値段とマスのつながりは、
// クライアントの地図の配置から生成した squares.ts が持つ。

import { POLISH_ID } from "../enhancement/catalog.js";
import type { ChestEntry } from "../world-map/catalog.js";
import { ATLAS_MAPS } from "./squares.js";

/** 1枚の地図：始まりのマス（タダで開いている）と、マスごとの [値段, ...隣のマス]。 */
export type AtlasMap = {
  start: string;
  squares: Record<string, readonly [number, ...string[]]>;
};

// 地図のキー（world・marina など）とマスのID（world-33・gate-marina・portoria など）。
export const MAP_KEY_PATTERN = /^[a-z]{2,20}$/;
export const SQUARE_ID_PATTERN = /^[a-z]{2,20}(?:-[a-z0-9]{1,20}){0,2}$/;

/** 開けるマス。始まりのマスと、地図にないマスは undefined。 */
export function squareOf(map: string, squareId: string) {
  const atlas = Object.hasOwn(ATLAS_MAPS, map) ? ATLAS_MAPS[map] : undefined;
  if (!atlas || !Object.hasOwn(atlas.squares, squareId)) return undefined;
  const [price, ...links] = atlas.squares[squareId];
  return { price, links, touchesStart: links.includes(atlas.start) };
}

// 磨き石 count 個を、1つずつの中身として並べる。
const polishStones = (count: number): ChestEntry[] =>
  Array.from({ length: count }, () => ({
    category: "material",
    id: POLISH_ID,
  }));

// 地図ごとの宝箱のマスの中身。抽選せず、マスを初めて開けたときに1回だけもらう。
// その先の地域で役立つ物を置く（前の街道の宝箱の中身を、今の地図のマスへ移した）。
const CHESTS: Record<string, Record<string, readonly ChestEntry[]>> = {
  world: {
    // ジェーロ連峰の手前（推奨Lv18）。
    "world-21": [
      { category: "weapon", id: "frost-wand", quality: "A" },
      ...polishStones(6),
    ],
    // オーロ砂漠の手前（推奨Lv24）。
    "world-79": [
      { category: "armor", id: "clay-armor", quality: "A" },
      ...polishStones(6),
    ],
    // ボスコ大森林の手前（推奨Lv30）。
    "world-26": [
      { category: "accessory", id: "crystal-ring", quality: "A" },
      ...polishStones(8),
    ],
  },
};

/** そのマスの宝箱の中身。宝箱のないマスは undefined。 */
export function chestOf(map: string, squareId: string) {
  const chests = Object.hasOwn(CHESTS, map) ? CHESTS[map] : undefined;
  return chests && Object.hasOwn(chests, squareId)
    ? chests[squareId]
    : undefined;
}
