import { describe, expect, it } from "vitest";
import { googleAuthSchema, guestAuthSchema } from "./schema.js";

describe("認証要求の検証", () => {
  it("IDトークンの型と長さを検証する", () => {
    expect(
      googleAuthSchema.safeParse({ idToken: "a".repeat(12_000) }).success,
    ).toBe(true);
    for (const idToken of [undefined, null, 123, "a".repeat(12_001)]) {
      expect(googleAuthSchema.safeParse({ idToken }).success).toBe(false);
    }
  });

  it("ゲストの秘密値は64桁の小文字16進数だけを受け付ける", () => {
    expect(guestAuthSchema.safeParse({ secret: "0f".repeat(32) }).success).toBe(
      true,
    );
    for (const secret of [
      undefined,
      "",
      "0F".repeat(32),
      "0f".repeat(31),
      `${"0f".repeat(32)}0`,
      "g".repeat(64),
    ]) {
      expect(guestAuthSchema.safeParse({ secret }).success).toBe(false);
    }
  });
});
