// 入れ子の地図（doc/features/world-atlas.md）。マスを開くルーンの値段とマスのつながりは、
// クライアントの地図の配置から生成した squares.ts が持つ。

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
