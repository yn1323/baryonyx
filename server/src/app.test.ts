import { describe, expect, it } from "vitest";
import app from "./app.js";

describe("HTTPの疎通確認", () => {
  it("GET /health が正常応答を返す", async () => {
    const response = await app.request("/health");

    expect(response.status).toBe(200);
    expect(response.headers.get("content-type")).toMatch(
      /^application\/json\b/,
    );
    expect(await response.json()).toEqual({ status: "ok" });
  });

  it("未定義のパスが404を返す", async () => {
    const response = await app.request("/missing");

    expect(response.status).toBe(404);
  });
});
