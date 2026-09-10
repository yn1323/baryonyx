import { createLocalJWKSet, exportJWK, generateKeyPair, SignJWT } from "jose";
import { beforeAll, describe, expect, it } from "vitest";
import { createGoogleVerifier, type VerifyIdentity } from "./routes.js";

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
