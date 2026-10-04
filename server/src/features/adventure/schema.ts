import { z } from "zod";
import { DESTINATION_IDS, EVENT_KINDS } from "./catalog.js";

const roomIdSchema = z.string().regex(/^[a-z0-9-]{1,40}$/);

export const startSchema = z.object({
  destinationId: z.enum(DESTINATION_IDS),
});

// 次の階の部屋へ進む。部屋のIDと種類は、クライアントが冒険の種から作った道のもの。
// 選んだ時点で保存し、再開はこの部屋から始まる。
export const moveSchema = z.object({
  roomId: roomIdSchema,
  floor: z.number().int().min(2).max(100),
  kind: z.enum(EVENT_KINDS),
});

// 今いる部屋の出来事（戦闘の勝利・宝箱）を終える。
export const clearSchema = z.object({
  roomId: roomIdSchema,
});

// 負けた戦闘からルーンで復活する。費用はサーバーが計算する。
export const reviveSchema = z.object({
  requestId: z.string().regex(/^[a-f0-9-]{36}$/),
  roomId: roomIdSchema,
});

// 冒険を終える。負けて帰還する（defeat）か、自分からやめる（retreat）。
export const endSchema = z.object({
  reason: z.enum(["defeat", "retreat"]),
});

export type Move = z.infer<typeof moveSchema>;
export type Revive = z.infer<typeof reviveSchema>;
export type EndReason = z.infer<typeof endSchema>["reason"];
