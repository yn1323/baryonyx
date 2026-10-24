import { z } from "zod";
import { SQUARE_ID_PATTERN } from "./catalog.js";

// マスへ着いたことを送る。clear は、そのマスをクリアしたか（挑めるマスを越えたとき）。
export const moveSchema = z.object({
  squareId: z.string().regex(SQUARE_ID_PATTERN),
  clear: z.boolean(),
});

export type Move = z.infer<typeof moveSchema>;
