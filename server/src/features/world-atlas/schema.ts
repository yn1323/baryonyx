import { z } from "zod";
import { MAP_KEY_PATTERN, SQUARE_ID_PATTERN } from "./catalog.js";

// マスを1つ開く。値段はサーバーが決めるため、端末は地図とマスと要求IDだけを送る。
// free は、開発用にルーンの消費を止めた端末が、払わずに（隣が開いているかも確かめずに）開けるときに付ける。
// DEBUG_TOOLS が on の環境（ローカルとDev）でだけ受け付ける。
export const openSchema = z.object({
  requestId: z.string().regex(/^[a-f0-9-]{36}$/),
  map: z.string().regex(MAP_KEY_PATTERN),
  squareId: z.string().regex(SQUARE_ID_PATTERN),
  free: z.boolean().optional(),
});

export type Open = z.infer<typeof openSchema>;
