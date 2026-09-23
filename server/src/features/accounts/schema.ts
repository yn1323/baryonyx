import { z } from "zod";

export const googleAuthSchema = z.object({
  idToken: z.string().max(12_000),
});
