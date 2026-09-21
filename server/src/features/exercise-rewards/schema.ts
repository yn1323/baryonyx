import { z } from "zod";

export const rewardSourceIdSchema = z.string().regex(/^[a-f0-9-]{36}$/);
export const rewardRequestIdSchema = z.string().regex(/^[a-f0-9-]{36}$/);

export const claimRewardsSchema = z.object({
  sourceId: rewardSourceIdSchema,
  requestId: rewardRequestIdSchema,
});

export type ClaimRewards = z.infer<typeof claimRewardsSchema>;
