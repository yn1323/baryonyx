// ライブラリに載せる分類（doc/features/library.md）。名前・説明・絵は各機能の定義の正本にあり、
// サーバーは利用者ごとの見つけた記録だけを持つ。
export const DISCOVERY_KINDS = [
  "character",
  "weapon",
  "armor",
  "accessory",
  "skill",
  "item",
  "enemy",
] as const;

export type DiscoveryKind = (typeof DISCOVERY_KINDS)[number];
