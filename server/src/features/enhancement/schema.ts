import { z } from "zod";
import { CHARACTER_IDS, UNIQUE_SKILL_COUNT } from "../party/catalog.js";
import {
  skillAwakeningBranchSchema,
  skillCardIdSchema,
} from "../party/schema.js";

const requestId = z.string().regex(/^[a-f0-9-]{36}$/);
const characterId = z.enum(CHARACTER_IDS);
const steps = z.number().int().min(1).max(99);
const uniqueIndex = z
  .number()
  .int()
  .min(0)
  .max(UNIQUE_SKILL_COUNT - 1);

// 品質強化。スキル・装備は持ち物の行のID（itemId）で、固有スキルはキャラと番号で選ぶ。
// steps 段階だけ倍率を上げる。1段階に使う磨き石の数は、その段階を始める前の品質で決まる。
export const polishSchema = z.discriminatedUnion("category", [
  z.object({
    requestId,
    category: z.enum(["skill", "weapon", "armor", "accessory"]),
    itemId: z.string().min(1).max(64),
    steps,
  }),
  z.object({
    requestId,
    category: z.literal("unique"),
    characterId,
    index: uniqueIndex,
    steps,
  }),
]);

export type Polish = z.infer<typeof polishSchema>;

// 覚醒。固有スキルを、まだ取得していない段階（1〜6）へ1段進める。
export const awakenUniqueSchema = z.object({
  requestId,
  characterId,
  index: uniqueIndex,
  stage: z.number().int().min(1).max(6),
});

// 覚醒。まだ覚醒していない持っているスキル1枚（cardId）を、2つの枝のどちらかへ覚醒させる。
export const awakenSkillCardSchema = z.object({
  requestId,
  cardId: skillCardIdSchema,
  branch: skillAwakeningBranchSchema,
});

// 固有スキルの覚醒を先に確かめる。本文にどちらの項目もあれば、固有スキルとして扱う。
export const awakenSchema = z.union([
  awakenUniqueSchema,
  awakenSkillCardSchema,
]);

export type AwakenUnique = z.infer<typeof awakenUniqueSchema>;
export type AwakenSkillCard = z.infer<typeof awakenSkillCardSchema>;
export type Awaken = z.infer<typeof awakenSchema>;
