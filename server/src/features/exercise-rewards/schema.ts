import { z } from "zod";
import { beginSyncSchema, createDaysSchema } from "../health/schema.js";

export const rewardSourceIdSchema = z.string().regex(/^[a-f0-9-]{36}$/);
export const rewardRequestIdSchema = z.string().regex(/^[a-f0-9-]{36}$/);

export const claimRewardsSchema = z.object({
  sourceId: rewardSourceIdSchema,
  requestId: rewardRequestIdSchema,
});

export type ClaimRewards = z.infer<typeof claimRewardsSchema>;

// 歩数の保存とルーンへの変換を1回の要求で行う（Homeの歩数のパネルを押したとき）。
export function createSyncRewardsSchema(now = Date.now()) {
  return beginSyncSchema.extend({
    requestId: rewardRequestIdSchema,
    days: createDaysSchema(now),
  });
}
