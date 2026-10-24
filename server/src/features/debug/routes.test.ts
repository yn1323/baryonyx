import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  type ApiScenario,
  createApiScenario,
} from "../../../tests/support/api-scenario.js";
import { DEBUG_RUNES, debugToolsEnabled } from "./routes.js";

describe("デバッグAPI", () => {
  let scenario: ApiScenario;

  beforeAll(async () => {
    scenario = await createApiScenario();
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  it("ルーンを10000足し、何度でも足せる。セッションがなければ401", async () => {
    expect((await scenario.request("/debug/runes", "POST", {})).status).toBe(
      401,
    );
    const user = await scenario.login("debug-runes");
    await scenario.read("/library", user.token);
    await scenario.setRunes(user.userId, 120);
    const first = await scenario.request(
      "/debug/runes",
      "POST",
      {},
      user.token,
    );
    expect(first.status).toBe(200);
    expect(await first.json()).toEqual({
      runes: DEBUG_RUNES,
      balanceAfter: 120 + DEBUG_RUNES,
    });
    const second = await scenario.request(
      "/debug/runes",
      "POST",
      {},
      user.token,
    );
    expect(await second.json()).toMatchObject({
      balanceAfter: 120 + DEBUG_RUNES * 2,
    });
    expect(
      (await scenario.read<{ runes: number }>("/adventure", user.token)).runes,
    ).toBe(120 + DEBUG_RUNES * 2);
  });

  it("DEBUG_TOOLS が on のときだけ使える", () => {
    expect(debugToolsEnabled({ DEBUG_TOOLS: "on" })).toBe(true);
    expect(debugToolsEnabled({ DEBUG_TOOLS: "off" })).toBe(false);
    expect(debugToolsEnabled({})).toBe(false);
  });
});
