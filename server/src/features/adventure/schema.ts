import { z } from "zod";
import { DESTINATION_IDS } from "./catalog.js";

const roomIdSchema = z.string().regex(/^[a-z0-9-]{1,40}$/);

export const startSchema = z.object({
  destinationId: z.enum(DESTINATION_IDS),
});

// 入口を選んで次の部屋へ進む。選んだ時点で保存し、再開はこの部屋から始まる。
export const moveSchema = z.object({
  roomId: roomIdSchema,
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

export type Revive = z.infer<typeof reviveSchema>;
export type EndReason = z.infer<typeof endSchema>["reason"];
