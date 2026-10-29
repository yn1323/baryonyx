import { z } from "zod";
import { MAP_KEY_PATTERN, SQUARE_ID_PATTERN } from "./catalog.js";

// マスを1つ開く。値段はサーバーが決めるため、端末は地図とマスと要求IDだけを送る。
export const openSchema = z.object({
  requestId: z.string().regex(/^[a-f0-9-]{36}$/),
  map: z.string().regex(MAP_KEY_PATTERN),
  squareId: z.string().regex(SQUARE_ID_PATTERN),
});

export type Open = z.infer<typeof openSchema>;
