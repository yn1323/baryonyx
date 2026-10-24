import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  type ApiScenario,
  createApiScenario,
} from "../../../tests/support/api-scenario.js";
import { START_SQUARE } from "./catalog.js";

type State = { at: string; cleared: string[] };

describe("街道の世界地図の進み具合のAPI", () => {
  let scenario: ApiScenario;

  beforeAll(async () => {
    scenario = await createApiScenario();
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  const read = (token: string) => scenario.read<State>("/world-map", token);

  const move = (token: string, body: unknown) =>
    scenario.request("/world-map/move", "POST", body, token);

  async function moved(token: string, squareId: string, clear: boolean) {
    const response = await move(token, { squareId, clear });
    expect(response.status).toBe(200);
    return (await response.json()) as State;
  }

  it("セッションのない要求を401で拒否する", async () => {
    const responses = await Promise.all([
      scenario.request("/world-map"),
      scenario.request("/world-map/move", "POST", {
        squareId: "n10-10",
        clear: true,
      }),
    ]);
    expect(responses.map((response) => response.status)).toEqual([401, 401]);
  });

  it("初めて読むユーザーは、最初の街をクリアした状態でそこにいる", async () => {
    const user = await scenario.login("world-map-new");
    expect(await read(user.token)).toEqual({
      at: START_SQUARE,
      cleared: [START_SQUARE],
    });
  });

  it("着いたマスとクリアしたマスを残し、送り直しても増やさない", async () => {
    const user = await scenario.login("world-map-move");
    await moved(user.token, "n10-10", true);
    await moved(user.token, "n9-10", true);
    // 送り直しても、クリアは1つのまま。
    await moved(user.token, "n9-10", true);
    // クリアしたマスの上を戻るだけなら、クリアは増えない。
    const back = await moved(user.token, "n10-10", false);
    expect(back).toEqual({
      at: "n10-10",
      cleared: [START_SQUARE, "n10-10", "n9-10"],
    });
    expect(await read(user.token)).toEqual(back);

    // 最初の街へ戻っても、最初の街は先頭に1つだけ。
    const home = await moved(user.token, START_SQUARE, true);
    expect(home.at).toBe(START_SQUARE);
    expect(home.cleared).toEqual([START_SQUARE, "n10-10", "n9-10"]);
  });

  it("ユーザーごとに進み具合を分ける", async () => {
    const first = await scenario.login("world-map-first");
    const second = await scenario.login("world-map-second");
    await moved(first.token, "n12-8", true);
    expect(await read(second.token)).toEqual({
      at: START_SQUARE,
      cleared: [START_SQUARE],
    });
  });

  it("マスのIDの形が違う、またはクリアの有無がない移動を400で拒否する", async () => {
    const user = await scenario.login("world-map-invalid");
    const responses = await Promise.all([
      move(user.token, { squareId: "N1-1", clear: true }),
      move(user.token, { squareId: "n1-1; drop", clear: true }),
      move(user.token, { squareId: "", clear: false }),
      move(user.token, { squareId: "n1-1" }),
    ]);
    expect(responses.map((response) => response.status)).toEqual([
      400, 400, 400, 400,
    ]);
    expect(await read(user.token)).toEqual({
      at: START_SQUARE,
      cleared: [START_SQUARE],
    });
  });
});
