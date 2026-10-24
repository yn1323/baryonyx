import { createLocalJWKSet, exportJWK, generateKeyPair, SignJWT } from "jose";
import { beforeAll, describe, expect, it, vi } from "vitest";
import {
  authenticateSession,
  createGoogleVerifier,
  forgetSession,
  hashToken,
  issueGuestSession,
  issueSession,
  type VerifyIdentity,
} from "./auth.js";

describe("セッションの発行と認証", () => {
  function repository() {
    return {
      findOrCreateUser: vi.fn(async () => "user"),
      findOrCreateGuest: vi.fn(async (_hash: string) => "guest"),
      createSession: vi.fn(async () => {}),
      findSession: vi.fn(
        async (
          _hash: string,
        ): Promise<{ userId: string; expiresAt: number } | undefined> => ({
          userId: "user",
          expiresAt: Date.now() + 3_600_000,
        }),
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
    expect(db.findSession).toHaveBeenCalledWith(tokenHash);
  });

  it("ゲストの秘密値はハッシュで照合し、そのユーザーのセッションを発行する", async () => {
    const db = repository();
    const secret = "1".repeat(64);
    const session = await issueGuestSession(db, secret);
    expect(db.findOrCreateGuest).toHaveBeenCalledWith(await hashToken(secret));
    expect(db.findOrCreateGuest.mock.calls[0]?.[0]).not.toBe(secret);
    expect(session.userId).toBe("guest");
    expect(db.createSession).toHaveBeenCalledWith(
      await hashToken(session.token),
      "guest",
      Date.parse(session.expiresAt),
    );
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
    expect(db.findSession).not.toHaveBeenCalled();
    db.findSession.mockResolvedValue(undefined);
    expect(
      await authenticateSession(db, `Bearer ${"a".repeat(64)}`),
    ).toBeUndefined();
  });

  it("確かめたセッションを短いあいだ覚え、有効期限とログアウトのあとはDBへ問い合わせ直す", async () => {
    const db = repository();
    const now = Date.now();
    const token = `Bearer ${"b".repeat(64)}`;
    const tokenHash = await hashToken("b".repeat(64));
    // 有効期限が覚える長さより先なら、覚える長さのあいだはDBへ問い合わせない。
    expect(await authenticateSession(db, token, now)).toEqual({
      userId: "user",
      tokenHash,
    });
    expect(await authenticateSession(db, token, now + 59_000)).toEqual({
      userId: "user",
      tokenHash,
    });
    expect(db.findSession).toHaveBeenCalledTimes(1);
    expect(await authenticateSession(db, token, now + 60_000)).toBeDefined();
    expect(db.findSession).toHaveBeenCalledTimes(2);
    forgetSession(tokenHash);
    db.findSession.mockResolvedValue(undefined);
    expect(await authenticateSession(db, token, now + 61_000)).toBeUndefined();

    // 有効期限が覚える長さより前なら、有効期限までしか覚えない。
    const soon = `Bearer ${"c".repeat(64)}`;
    db.findSession.mockResolvedValue({
      userId: "user",
      expiresAt: now + 10_000,
    });
    await authenticateSession(db, soon, now);
    db.findSession.mockResolvedValue(undefined);
    expect(await authenticateSession(db, soon, now + 9_000)).toBeDefined();
    expect(await authenticateSession(db, soon, now + 10_000)).toBeUndefined();
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
