import { z } from "zod";

// 神殿で1回か10回召喚する。値段と出る物はサーバーが決める。
export const summonSchema = z.object({
  requestId: z.string().regex(/^[a-f0-9-]{36}$/),
  count: z.union([z.literal(1), z.literal(10)]),
});

export type Summon = z.infer<typeof summonSchema>;
