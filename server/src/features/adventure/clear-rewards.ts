import { and, eq, type SQL, sql } from "drizzle-orm";
import type { Database } from "../../shared/db.js";
import {
  type Quality,
  qualityFields,
  rollMultiplierPercent,
} from "../../shared/quality.js";
import { AWAKENING_SHARD_ID, POLISH_ID } from "../enhancement/catalog.js";
import { creditRunes } from "../exercise-rewards/wallet.js";
import { type HoldingCategory, maxPercentOf } from "../inventory/catalog.js";
import { type Grant, inventoryGrants } from "../inventory/grants.js";
import { adventureRecords } from "./db-schema.js";

// ダンジョンのクリア回数の報酬（doc/features/stage-progression.md のダンジョンのクリア回数の報酬）。
// 1〜5回目のクリアで、回ごとに決まった物と少しのルーンをもらう。6回目からはない。
// 型は全ダンジョン共通にし、物はその行き先の一覧から選ぶ（2026-11-01）：
// 1回目は主の物を除く☆の最も多い装備（品質A）、2回目は磨き石（浅い4つは10個、奥の4つは20個。2026-11-02）、
// 3回目は☆の最も多いスキル（品質A）、4回目は覚醒の欠片2個、5回目は主の☆5のアクセサリー（品質S）。
// ルーンは毎回、どの行き先でも同じ300（2026-11-01、ユーザー）。クライアントの AdventureClearRewards.cs と同じ表にする。

export const CLEAR_REWARD_COUNT = 5;

// どの回にも付けるルーン。
export const CLEAR_REWARD_RUNES = 300;

export type ClearReward = {
  category: HoldingCategory;
  id: string;
  count: number;
  // 確定する品質。倍率を持たない物（アイテム・強化素材）はない。
  quality?: Quality;
  runes: number;
};

type Table = readonly [
  ClearReward,
  ClearReward,
  ClearReward,
  ClearReward,
  ClearReward,
];

function rewards(
  gear: { category: "weapon" | "armor" | "accessory"; id: string },
  polishCount: number,
  skill: string,
  bossAccessory: string,
): Table {
  const runes = CLEAR_REWARD_RUNES;
  return [
    { ...gear, count: 1, quality: "A", runes },
    { category: "material", id: POLISH_ID, count: polishCount, runes },
    { category: "skill", id: skill, count: 1, quality: "A", runes },
    { category: "material", id: AWAKENING_SHARD_ID, count: 2, runes },
    {
      category: "accessory",
      id: bossAccessory,
      count: 1,
      quality: "S",
      runes,
    },
  ];
}

export const CLEAR_REWARDS: Record<string, Table> = {
  "forest-ruins": rewards(
    { category: "accessory", id: "dew-pendant" },
    10,
    "Hone",
    "guardian-circlet",
  ),
  "port-portoria": rewards(
    { category: "weapon", id: "flame-dagger" },
    10,
    "ManaPrayer",
    "kraken-pearl",
  ),
  "glaciera-cave": rewards(
    { category: "armor", id: "crystal-shield" },
    10,
    "IceMirror",
    "troll-heartstone",
  ),
  "hush-mire": rewards(
    { category: "accessory", id: "frog-crown" },
    10,
    "QuickCast",
    "croc-fang",
  ),
  "almaja-tomb": rewards(
    { category: "armor", id: "ancient-plate" },
    20,
    "Revelation",
    "golden-mask",
  ),
  "moonshade-graveyard": rewards(
    { category: "weapon", id: "moonshade-staff" },
    20,
    "Resurrection",
    "lich-signet",
  ),
  "volgan-volcano": rewards(
    { category: "armor", id: "obsidian-shield" },
    20,
    "Meteor",
    "obsidian-crown",
  ),
  "neve-peaks": rewards(
    { category: "weapon", id: "glacier-greatsword" },
    20,
    "AbsoluteZero",
    "mistdragon-eye",
  ),
};

/** clears 回目のクリアの報酬。1〜5回目でなければ、または表にない行き先なら null。 */
export function clearRewardOf(
  destinationId: string,
  clears: number,
): ClearReward | null {
  if (clears < 1 || clears > CLEAR_REWARD_COUNT) return null;
  return CLEAR_REWARDS[destinationId]?.[clears - 1] ?? null;
}

/** 渡す報酬。スキル・装備は品質の幅から倍率を決める。 */
export type RolledClearReward = ClearReward & {
  clears: number;
  multiplierPercent: number | null;
};

export function rollClearReward(
  destinationId: string,
  clears: number,
  random: () => number,
): RolledClearReward | null {
  const reward = clearRewardOf(destinationId, clears);
  if (!reward) return null;
  const maxPercent = maxPercentOf(reward.category, reward.id);
  return {
    ...reward,
    clears,
    multiplierPercent:
      reward.quality && maxPercent !== null
        ? rollMultiplierPercent(reward.quality, random, maxPercent)
        : null,
  };
}

/**
 * クリアを記録した（written）うえで、記録のクリアの回数がちょうど reward.clears になったときだけ、
 * 報酬の物とルーンを足す文。記録の更新（recordStatement）のあとに並べる。
 */
export function clearRewardStatements(
  db: Database,
  userId: string,
  destinationId: string,
  reward: RolledClearReward,
  now: string,
  written: SQL,
) {
  const reached = sql`(${written} AND EXISTS (SELECT 1 FROM ${adventureRecords} WHERE ${and(
    eq(adventureRecords.userId, userId),
    eq(adventureRecords.destinationId, destinationId),
    eq(adventureRecords.clears, reward.clears),
  )}))`;
  const grants: Grant[] = Array.from({ length: reward.count }, () => ({
    category: reward.category,
    id: reward.id,
    multiplierPercent: reward.multiplierPercent,
  }));
  return [
    ...inventoryGrants(db, reached, userId, grants, now),
    ...creditRunes(db, userId, reward.runes, reached, now),
  ];
}

/** 結果の応答に載せる、もらった報酬。倍率を持たない物は品質の項目を省く。 */
export const clearRewardResponse = (reward: RolledClearReward) => ({
  clears: reward.clears,
  category: reward.category,
  id: reward.id,
  count: reward.count,
  runes: reward.runes,
  ...qualityFields(
    reward.multiplierPercent,
    maxPercentOf(reward.category, reward.id),
  ),
});
