import { z } from "zod";

export const googleAuthSchema = z.object({
  idToken: z.string().max(12_000),
});

// 端末が生成して保持する32バイトの乱数。サーバーにはハッシュだけを保存する。
export const guestAuthSchema = z.object({
  secret: z.string().regex(/^[a-f0-9]{64}$/),
});
