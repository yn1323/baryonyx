import { z } from "zod";
import { ENEMY_IDS } from "../combat/catalog.js";
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

// 戦闘で遭遇した敵の種類と、倒した体数。ライブラリの記録に使う（doc/features/library.md の敵の記録）。
const enemiesSchema = z
  .array(
    z.object({
      enemyId: z.enum(ENEMY_IDS),
      defeated: z.number().int().min(0).max(20),
    }),
  )
  .max(20)
  .default([]);

// 今いる部屋の出来事（戦闘の勝利・宝箱）を終える。戦闘なら、遭遇した敵を添える。
// pass は怪しい宝箱を開けずに通り過ぎるとき。trialMet は試練の条件を満たして勝ったか（クライアントの判定）。
export const clearSchema = z.object({
  roomId: roomIdSchema,
  enemies: enemiesSchema,
  pass: z.boolean().default(false),
  trialMet: z.boolean().default(false),
  // 進化の祭壇で固有スキルを進化させた（進化の保存は編成のAPIが受け持つ）。
  evolved: z.boolean().default(false),
});

// 今いる怪しい宝箱を開ける。ミミックかどうかはサーバーが決める。
export const openSchema = z.object({
  roomId: roomIdSchema,
});

// 今いる行商人のマスで、品物を1つ買う。値段はサーバーが持つ。
export const buySchema = z.object({
  requestId: z.string().regex(/^[a-f0-9-]{36}$/),
  roomId: roomIdSchema,
  index: z.number().int().min(0).max(19),
});

// 負けた戦闘からルーンで復活する。費用はサーバーが計算する。
export const reviveSchema = z.object({
  requestId: z.string().regex(/^[a-f0-9-]{36}$/),
  roomId: roomIdSchema,
});

// 冒険を終える。負けて帰還する（defeat）か、自分からやめる（retreat）。
// 負けたときは、負けた戦闘で遭遇した敵を添える。やめたときの敵は記録しない。
export const endSchema = z.object({
  reason: z.enum(["defeat", "retreat"]),
  enemies: enemiesSchema,
});

export type Move = z.infer<typeof moveSchema>;
export type Revive = z.infer<typeof reviveSchema>;
export type Clear = z.infer<typeof clearSchema>;
export type Buy = z.infer<typeof buySchema>;
export type EndReason = z.infer<typeof endSchema>["reason"];
export type Enemies = z.infer<typeof enemiesSchema>;
