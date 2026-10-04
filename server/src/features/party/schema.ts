import { z } from "zod";
import {
  CARD_SKILL_IDS,
  CARD_SLOT_COUNT,
  CHARACTER_IDS,
  MAX_LEVEL,
  PARTY_SLOT_COUNT,
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
export const characterIdSchema = z.enum(CHARACTER_IDS);

export const setPartySlotSchema = z.object({
  characterId: characterIdSchema,
});

export const setCardSchema = z.object({
  skillId: z.enum(CARD_SKILL_IDS),
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
