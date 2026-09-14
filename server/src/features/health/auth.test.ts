import { createLocalJWKSet, exportJWK, generateKeyPair, SignJWT } from "jose";
import { beforeAll, describe, expect, it, vi } from "vitest";
import {
  authenticateSession,
  createGoogleVerifier,
  hashToken,
  issueSession,
  type VerifyIdentity,
} from "./auth.js";

describe("セッションの発行と認証", () => {
  function repository() {
    return {
      findOrCreateUser: vi.fn(async () => "user"),
      createSession: vi.fn(async () => {}),
      findSessionUser: vi.fn(
        async (_hash: string): Promise<string | undefined> => "user",
      ),
    };
  }

  it("返却トークンをハッシュで保存し、有効期限と本人を引き継ぐ", async () => {
    const db = repository();
    const before = Date.now();
    const session = await issueSession(db, "subject");
    expect(session.token).toMatch(/^[a-f0-9]{64}$/);
    expect(db.findOrCreateUser).toHaveBeenCalledWith("subject");
    const tokenHash = await hashToken(session.token);
    expect(tokenHash).not.toBe(session.token);
    const expiresAt = Date.parse(session.expiresAt);
    expect(expiresAt).toBeGreaterThanOrEqual(before + 3_600_000);
    expect(expiresAt).toBeLessThanOrEqual(Date.now() + 3_600_000);
    expect(db.createSession).toHaveBeenCalledWith(tokenHash, "user", expiresAt);
    expect(await authenticateSession(db, `Bearer ${session.token}`)).toEqual({
      userId: "user",
      tokenHash,
    });
    expect(db.findSessionUser).toHaveBeenCalledWith(tokenHash);
  });

  it("不正な認証形式はDBへ問い合わせず、失効済みセッションも拒否する", async () => {
    const db = repository();
    for (const header of [
      "",
      "Basic token",
      `Bearer ${"A".repeat(64)}`,
      `Bearer ${"a".repeat(63)}`,
    ]) {
      expect(await authenticateSession(db, header)).toBeUndefined();
    }
    expect(db.findSessionUser).not.toHaveBeenCalled();
    db.findSessionUser.mockResolvedValue(undefined);
    expect(
      await authenticateSession(db, `Bearer ${"a".repeat(64)}`),
    ).toBeUndefined();
  });
});

describe("Google IDトークンの検証", () => {
  let privateKey: CryptoKey;
  let verify: VerifyIdentity;
  beforeAll(async () => {
    const keys = await generateKeyPair("RS256");
    privateKey = keys.privateKey;
    const jwk = await exportJWK(keys.publicKey);
    verify = createGoogleVerifier(
      createLocalJWKSet({ keys: [{ ...jwk, kid: "test-key" }] }),
    );
  });
  async function sign(
    audience = "app-client",
    issuer = "https://accounts.google.com",
    expires = "1h",
    key = privateKey,
  ) {
    return new SignJWT({})
      .setProtectedHeader({ alg: "RS256", kid: "test-key" })
      .setSubject("subject")
      .setIssuer(issuer)
      .setAudience(audience)
      .setIssuedAt()
      .setExpirationTime(expires)
      .sign(key);
  }
  it("正しい署名と対象クライアントからユーザーIDを取得する", async () => {
    expect(await verify(await sign(), "app-client")).toBe("subject");
  });
  it("別クライアント、別発行元、期限切れ、署名違いを拒否する", async () => {
    await expect(
      verify(await sign("another-client"), "app-client"),
    ).rejects.toThrow();
    await expect(
      verify(await sign("app-client", "https://example.com"), "app-client"),
    ).rejects.toThrow();
    await expect(
      verify(
        await sign("app-client", "https://accounts.google.com", "-1h"),
        "app-client",
      ),
    ).rejects.toThrow();
    const wrong = await generateKeyPair("RS256");
    await expect(
      verify(
        await sign(
          "app-client",
          "https://accounts.google.com",
          "1h",
          wrong.privateKey,
        ),
        "app-client",
      ),
    ).rejects.toThrow();
  });
});
