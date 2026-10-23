import { z } from "zod";
import { CHARACTER_IDS, UNIQUE_SKILL_COUNT } from "../party/catalog.js";
import { POLISH_IDS } from "./catalog.js";

const requestId = z.string().regex(/^[a-f0-9-]{36}$/);
const characterId = z.enum(CHARACTER_IDS);
const uniqueIndex = z
  .number()
  .int()
  .min(0)
  .max(UNIQUE_SKILL_COUNT - 1);

// 品質強化。スキル・装備は持ち物の行のID（itemId）で、固有スキルはキャラと番号で選ぶ。
// 磨き石は1回に1色だけを、count 個使う。
export const polishSchema = z.discriminatedUnion("category", [
  z.object({
    requestId,
    category: z.enum(["skill", "weapon", "armor", "accessory"]),
    itemId: z.string().min(1).max(64),
    materialId: z.enum(POLISH_IDS),
    count: z.number().int().min(1).max(99),
  }),
  z.object({
    requestId,
    category: z.literal("unique"),
    characterId,
    index: uniqueIndex,
    materialId: z.enum(POLISH_IDS),
    count: z.number().int().min(1).max(99),
  }),
]);

export type Polish = z.infer<typeof polishSchema>;

// 覚醒。固有スキルを、まだ取得していない段階（1〜6）へ1段進める。
export const awakenSchema = z.object({
  requestId,
  characterId,
  index: uniqueIndex,
  stage: z.number().int().min(1).max(6),
});

export type Awaken = z.infer<typeof awakenSchema>;
