import { z } from "zod";
import { ITEM_IDS } from "../items/catalog.js";
import { BOND_MAX } from "../summon/catalog.js";
import {
  CARD_SLOT_COUNT,
  CHARACTER_IDS,
  MAX_LEVEL,
  PARTY_SLOT_COUNT,
  UNIQUE_SKILL_COUNT,
  UNIQUE_STAGE_COUNT,
} from "./catalog.js";

// パスの枠番号。0から枠の数-1までの1桁だけを受け付ける。
function slotNumber(count: number) {
  return z
    .string()
    .regex(new RegExp(`^[0-${count - 1}]$`))
    .transform(Number);
}

export const partySlotSchema = slotNumber(PARTY_SLOT_COUNT);
export const cardSlotSchema = slotNumber(CARD_SLOT_COUNT);
export const uniqueIndexSchema = slotNumber(UNIQUE_SKILL_COUNT);
export const characterIdSchema = z.enum(CHARACTER_IDS);

export const setPartySlotSchema = z.object({
  characterId: characterIdSchema,
});

// 枠に入れる物。持っているスキルの1枚（`card:<ID>`）か、アイテム（`item:<アイテムのID>`）。
export const setCardSchema = z.object({
  card: z.union([
    z.string().regex(/^card:[a-f0-9-]{36}$/),
    z.enum(ITEM_IDS.map((id) => `item:${id}` as const)),
  ]),
});

// 今のレベルと上げたあとのレベル。費用はサーバーが計算する。
export const levelUpSchema = z
  .object({
    requestId: z.string().regex(/^[a-f0-9-]{36}$/),
    fromLevel: z.number().int().min(1),
    toLevel: z.number().int().max(MAX_LEVEL),
  })
  .refine((body) => body.toLevel > body.fromLevel);

export type LevelUp = z.infer<typeof levelUpSchema>;

// 編成でセットする固有スキルの段階。最初の段階（0）から最後の段階まで。
export const setUniqueSchema = z.object({
  stage: z
    .number()
    .int()
    .min(0)
    .max(UNIQUE_STAGE_COUNT - 1),
});

// 冒険の戦闘で進化した先の段階。最初の段階（0）へは進化しない。
export const evolveUniqueSchema = z.object({
  stage: z
    .number()
    .int()
    .min(1)
    .max(UNIQUE_STAGE_COUNT - 1),
});

// 絆を深めたあとの段。1段目から上限まで。
export const deepenBondSchema = z.object({
  bond: z.number().int().min(1).max(BOND_MAX),
});
