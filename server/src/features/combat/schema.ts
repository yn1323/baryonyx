import { z } from "zod";
import { ELEMENTS, ENEMY_IDS } from "./catalog.js";

export const weaknessSchema = z.object({
  enemyId: z.enum(ENEMY_IDS),
  element: z.enum(ELEMENTS),
});

// 開示した弱点をまとめて送る。1回の戦闘で開示できる数より十分に多い上限を置く。
export const revealWeaknessesSchema = z.object({
  weaknesses: z
    .array(weaknessSchema)
    .min(1)
    .max(ENEMY_IDS.length * ELEMENTS.length),
});

export type Weakness = z.infer<typeof weaknessSchema>;
