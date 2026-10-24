import type { Quality } from "../../shared/quality.js";
import { DROP_POOLS } from "../adventure/drops.js";
import { CHARACTER_IDS } from "../party/catalog.js";

// 神殿の召喚（doc/features/monetization.md の召喚）。キャラ・装備・アイテムをまぜて引く。
// 割合と値段はクライアントの TempleSummon と同じ値にし、画面に出す割合をサーバーが守る。

export const SUMMON_COUNTS = [1, 10] as const;
export type SummonCount = (typeof SUMMON_COUNTS)[number];

// 1回300ルーン。10回は値引きせず、☆3以上（またはキャラ）を1つ確定にする。
export const SUMMON_RUNES_PER_PULL = 300;
export const SUMMON_GUARANTEED_STARS = 3;

export const summonPrice = (count: SummonCount) =>
  SUMMON_RUNES_PER_PULL * count;

export const SUMMON_CATEGORIES = [
  "character",
  "weapon",
  "armor",
  "accessory",
  "item",
] as const;
export type SummonCategory = (typeof SUMMON_CATEGORIES)[number];

// 分類の割合（千分率）。
export const SUMMON_CATEGORY_PERMILLE: Record<SummonCategory, number> = {
  character: 20,
  weapon: 150,
  armor: 150,
  accessory: 200,
  item: 480,
};

// ☆1〜☆5の割合（千分率）。装備とアイテムに使う。
export const SUMMON_STAR_PERMILLE = [450, 350, 160, 35, 5] as const;

// 品質の割合（千分率）。装備（武器・防具・アクセサリー）に使う。
export const SUMMON_QUALITY_PERMILLE: Record<Quality, number> = {
  C: 750,
  B: 180,
  A: 50,
  S: 20,
};

// 絆の上限（doc/features/monetization.md のキャラが被ったとき）。超えた分は、全キャラ共通のかけらにする。
export const BOND_MAX = 6;
// 絆の上限を超えて被った1人あたりのかけら（仮の値）。
export const SHARDS_PER_OVERFLOW = 1;

export type SummonEntry = {
  category: SummonCategory;
  id: string;
  // キャラは☆を持たないため0。
  stars: number;
};

// 引ける物：キャラと、冒険のすべての行き先の一覧にあるアイテム・装備（主の物を含む）。
// 引ける行き先を解放した所だけにするかは未決のため、今はすべての行き先から引く。
export const SUMMON_POOL: readonly SummonEntry[] = (() => {
  const pool: SummonEntry[] = CHARACTER_IDS.map((id) => ({
    category: "character" as const,
    id,
    stars: 0,
  }));
  const seen = new Set<string>();
  for (const entries of Object.values(DROP_POOLS)) {
    for (const entry of entries) {
      if (entry.category === "skill") continue;
      const key = `${entry.category}:${entry.id}`;
      if (seen.has(key)) continue;
      seen.add(key);
      pool.push({ category: entry.category, id: entry.id, stars: entry.stars });
    }
  }
  return pool;
})();
