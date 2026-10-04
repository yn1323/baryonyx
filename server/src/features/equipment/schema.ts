import { z } from "zod";
import { characterIdSchema } from "../party/schema.js";
import { EQUIPMENT_SLOTS } from "./catalog.js";

export { characterIdSchema };

export const equipmentSlotSchema = z.enum(EQUIPMENT_SLOTS);

// 付ける装備。サーバーが作った装備1つずつのID。
export const equipSchema = z.object({
  itemId: z.string().regex(/^[a-f0-9-]{36}$/),
});
