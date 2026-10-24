import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  type ApiScenario,
  createApiScenario,
} from "../../../tests/support/api-scenario.js";
import { CHESTS, START_SQUARE } from "./catalog.js";

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

  describe("宝箱のマス", () => {
    type Chest = State & {
      chest?: {
        category: string;
        id: string;
        multiplier?: number;
        quality?: string;
      }[];
    };

    async function opened(token: string, squareId: string, clear = true) {
      const response = await move(token, { squareId, clear });
      expect(response.status).toBe(200);
      return (await response.json()) as Chest;
    }

    // 持っている装備・スキル・強化素材の数。
    async function holdings(token: string) {
      const [equipment, party, enhancement] = await Promise.all([
        scenario.read<{ items: { equipmentId: string; quality: string }[] }>(
          "/equipment",
          token,
        ),
        scenario.read<{ skillCards: { skillId: string; quality: string }[] }>(
          "/party",
          token,
        ),
        scenario.read<{ materials: { id: string; count: number }[] }>(
          "/enhancement",
          token,
        ),
      ]);
      const countOf = (id: string) =>
        enhancement.materials.find((entry) => entry.id === id)?.count ?? 0;
      return {
        wands: equipment.items.filter(
          (item) => item.equipmentId === "frost-wand",
        ),
        holyLights: party.skillCards.filter(
          (card) => card.skillId === "HolyLight",
        ),
        blue: countOf("polish-blue"),
        shards: countOf("awakening-shard"),
      };
    }

    it("初めてクリアしたときに決まった中身を持ち物に足し、応答に中身を添える", async () => {
      const user = await scenario.login("world-map-chest");
      const before = await holdings(user.token);
      const state = await opened(user.token, "n8-5");
      expect(state.cleared).toContain("n8-5");
      expect(state.chest?.map((entry) => [entry.category, entry.id])).toEqual(
        CHESTS["n8-5"].map((entry) => [entry.category, entry.id]),
      );
      // 装備は決めた品質で、強化素材は倍率と品質を持たない。
      expect(state.chest?.[0]).toMatchObject({ quality: "A" });
      expect(state.chest?.[1]).not.toHaveProperty("quality");

      const after = await holdings(user.token);
      expect(after.wands.length).toBe(before.wands.length + 1);
      expect(after.wands.at(-1)?.quality).toBe("A");
      expect(after.blue).toBe(before.blue + 2);
    });

    it("送り直しや戻ってきたときは、持ち物に足し直さない", async () => {
      const user = await scenario.login("world-map-chest-again");
      await opened(user.token, "n16-18");
      const once = await holdings(user.token);
      // 送り直しても中身は添えるが、持ち物は増えない。
      const again = await opened(user.token, "n16-18");
      expect(again.chest).toHaveLength(CHESTS["n16-18"].length);
      // クリアしたマスへ戻るだけなら、中身を添えない。
      const back = await opened(user.token, "n16-18", false);
      expect(back.chest).toBeUndefined();
      const after = await holdings(user.token);
      expect(after.holyLights).toHaveLength(once.holyLights.length);
      expect(after.shards).toBe(once.shards);
      expect(once.holyLights.at(-1)?.quality).toBe("A");
    });

    it("宝箱でないマスは中身を添えない", async () => {
      const user = await scenario.login("world-map-chest-none");
      const state = await opened(user.token, "n10-10");
      expect(state.chest).toBeUndefined();
    });
  });
});
