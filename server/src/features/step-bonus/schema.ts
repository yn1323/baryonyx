import { z } from "zod";
import { STEP_BONUS_IDS, STEP_BONUS_SLOT_COUNT } from "./catalog.js";

// パスの枠番号。0から枠の数-1までの1桁だけを受け付ける。
export const slotSchema = z
  .string()
  .regex(new RegExp(`^[0-${STEP_BONUS_SLOT_COUNT - 1}]$`))
  .transform(Number);

export const setSlotSchema = z.object({
  bonusId: z.enum(STEP_BONUS_IDS),
});

export type SetSlot = z.infer<typeof setSlotSchema>;
