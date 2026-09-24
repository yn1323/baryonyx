import { createRemoteJWKSet, type JWTVerifyGetKey, jwtVerify } from "jose";
import type { createAccountsRepository } from "./repository.js";

const googleKeys = createRemoteJWKSet(
  new URL("https://www.googleapis.com/oauth2/v3/certs"),
);

export type VerifyIdentity = (
  token: string,
  audience: string,
) => Promise<string>;

export function createGoogleVerifier(
  keys: JWTVerifyGetKey = googleKeys,
): VerifyIdentity {
  return async (token, audience) => {
    const { payload } = await jwtVerify(token, keys, {
      audience,
      issuer: ["accounts.google.com", "https://accounts.google.com"],
      algorithms: ["RS256"],
      requiredClaims: ["sub", "exp", "iat"],
      maxTokenAge: "1h",
    });
    if (!payload.sub || payload.sub.length > 255)
      throw new Error("Invalid subject");
    return payload.sub;
  };
}

export const verifyGoogleIdentity = createGoogleVerifier();

function hex(bytes: Uint8Array): string {
  return Array.from(bytes, (byte) => byte.toString(16).padStart(2, "0")).join(
    "",
  );
}

export async function hashToken(token: string): Promise<string> {
  const digest = await crypto.subtle.digest(
    "SHA-256",
    new TextEncoder().encode(token),
  );
  return hex(new Uint8Array(digest));
}

type SessionRepository = Pick<
  ReturnType<typeof createAccountsRepository>,
  "findOrCreateUser" | "createSession" | "findSessionUser"
>;

type GuestRepository = Pick<
  ReturnType<typeof createAccountsRepository>,
  "findOrCreateGuest" | "createSession"
>;

export async function issueSession(
  repository: SessionRepository,
  subject: string,
) {
  return createUserSession(
    repository,
    await repository.findOrCreateUser(subject),
  );
}

// 同じ秘密値からは同じユーザーを返し、応答が届かず再送した場合も重複作成しない。
export async function issueGuestSession(
  repository: GuestRepository,
  secret: string,
) {
  return createUserSession(
    repository,
    await repository.findOrCreateGuest(await hashToken(secret)),
  );
}

async function createUserSession(
  repository: Pick<SessionRepository, "createSession">,
  userId: string,
) {
  const token = hex(crypto.getRandomValues(new Uint8Array(32)));
  const expiresAt = Date.now() + 3_600_000;
  await repository.createSession(await hashToken(token), userId, expiresAt);
  return { token, userId, expiresAt: new Date(expiresAt).toISOString() };
}

export async function authenticateSession(
  repository: SessionRepository,
  authorization: string,
) {
  if (!/^Bearer [a-f0-9]{64}$/.test(authorization)) return undefined;
  const tokenHash = await hashToken(authorization.slice(7));
  const userId = await repository.findSessionUser(tokenHash);
  return userId ? { userId, tokenHash } : undefined;
}
