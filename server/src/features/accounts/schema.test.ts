import { describe, expect, it } from "vitest";
import { googleAuthSchema } from "./schema.js";

describe("認証要求の検証", () => {
  it("IDトークンの型と長さを検証する", () => {
    expect(
      googleAuthSchema.safeParse({ idToken: "a".repeat(12_000) }).success,
    ).toBe(true);
    for (const idToken of [undefined, null, 123, "a".repeat(12_001)]) {
      expect(googleAuthSchema.safeParse({ idToken }).success).toBe(false);
    }
  });
});
