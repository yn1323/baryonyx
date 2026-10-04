import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  type ApiScenario,
  createApiScenario,
} from "../../../tests/support/api-scenario.js";
import {
  COST_PER_LEVEL,
  levelUpCost,
  MAX_LEVEL,
  STARTER_CHARACTERS,
  STARTER_SLOTS,
} from "./catalog.js";

type State = {
  change?: string;
  levelUp?: {
    characterId: string;
    fromLevel: number;
    toLevel: number;
    runes: number;
    balanceAfter: number;
  };
  characters: { id: string; level: number; cards: (string | null)[] }[];
  slots: { slot: number; characterId: string | null }[];
  runes: number;
  rules: { maxLevel: number; costPerLevel: number };
};

const requestIds = {
  first: "aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa",
  second: "aaaaaaaa-2222-4222-8222-aaaaaaaaaaaa",
  third: "aaaaaaaa-3333-4333-8333-aaaaaaaaaaaa",
};

describe("パーティAPI", () => {
  let scenario: ApiScenario;

  beforeAll(async () => {
    scenario = await createApiScenario();
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  const read = (token: string) => scenario.read<State>("/party", token);

  const slots = (state: State) => state.slots.map((slot) => slot.characterId);
  const character = (state: State, id: string) =>
    state.characters.find((entry) => entry.id === id);

  function levelUp(
    token: string,
    id: string,
    body: { requestId: string; fromLevel: number; toLevel: number },
  ) {
    return scenario.request(
      `/party/characters/${id}/level-up`,
      "POST",
      body,
      token,
    );
  }

  it("セッションのない要求を401で拒否する", async () => {
    const responses = await Promise.all([
      scenario.request("/party"),
      scenario.request("/party/slots/0", "PUT", { characterId: "toma" }),
      scenario.request("/party/slots/0", "DELETE"),
      scenario.request("/party/characters/toma/cards/0", "PUT", {
        skillId: "Fire",
      }),
      scenario.request("/party/characters/toma/level-up", "POST", {
        requestId: requestIds.first,
        fromLevel: 12,
        toLevel: 13,
      }),
    ]);
    expect(responses.map((response) => response.status)).toEqual([
      401, 401, 401, 401, 401,
    ]);
  });

  it("初めて読むユーザーに仮のキャラ・カード・編成を1回だけ付与する", async () => {
    const user = await scenario.login("party-starter");
    const first = await read(user.token);
    expect(first.characters).toEqual(
      STARTER_CHARACTERS.map((entry) => ({
        id: entry.characterId,
        level: entry.level,
        cards: [...entry.cards],
      })),
    );
    expect(first.slots.map((slot) => slot.slot)).toEqual([0, 1, 2, 3]);
    expect(slots(first)).toEqual(STARTER_SLOTS);
    expect(first.runes).toBe(0);
    expect(first.rules).toEqual({
      maxLevel: MAX_LEVEL,
      costPerLevel: COST_PER_LEVEL,
    });

    expect(await read(user.token)).toEqual(first);
  });

  it("枠にパーティにいないキャラを入れ、外し、最後の1人は残す", async () => {
    const user = await scenario.login("party-slots");
    await read(user.token);
    const put = (slot: number, characterId: string) =>
      scenario.request(
        `/party/slots/${slot}`,
        "PUT",
        { characterId },
        user.token,
      );
    const remove = (slot: number) =>
      scenario.request(`/party/slots/${slot}`, "DELETE", undefined, user.token);

    const replaced = (await (await put(3, "anselm")).json()) as State;
    expect(replaced.change).toBe("replace");
    expect(slots(replaced)).toEqual(["toma", "luka", "aria", "anselm"]);
    expect(((await (await put(3, "anselm")).json()) as State).change).toBe(
      "none",
    );

    // パーティ内での並べ替えはしない。
    const moved = await put(0, "luka");
    expect(moved.status).toBe(409);
    expect(await moved.json()).toEqual({ error: "already_in_party" });

    const left = (await (await remove(3)).json()) as State;
    expect(left.change).toBe("leave");
    expect(slots(left)).toEqual(["toma", "luka", "aria", null]);
    expect(((await (await remove(3)).json()) as State).change).toBe("none");

    const joined = (await (await put(3, "greta")).json()) as State;
    expect(joined.change).toBe("join");
    expect(slots(joined)).toEqual(["toma", "luka", "aria", "greta"]);

    for (const slot of [1, 2, 3]) {
      expect((await remove(slot)).status).toBe(200);
    }
    const last = await remove(0);
    expect(last.status).toBe(409);
    expect(await last.json()).toEqual({ error: "keep_one" });
    expect(slots(await read(user.token))).toEqual(["toma", null, null, null]);
  });

  it("キャラの枠にカードを入れ、そのキャラのほかの枠にあれば入れ替える", async () => {
    const user = await scenario.login("party-cards");
    const before = await read(user.token);
    const put = (id: string, slot: number, skillId: string) =>
      scenario.request(
        `/party/characters/${id}/cards/${slot}`,
        "PUT",
        { skillId },
        user.token,
      );

    const replaced = (await (await put("toma", 0, "Embers")).json()) as State;
    expect(replaced.change).toBe("replace");
    expect(character(replaced, "toma")?.cards).toEqual([
      "Embers",
      "Meteor",
      "Ice",
      "Blizzard",
    ]);

    const swapped = (await (await put("toma", 1, "Ice")).json()) as State;
    expect(swapped.change).toBe("swap");
    expect(character(swapped, "toma")?.cards).toEqual([
      "Embers",
      "Ice",
      "Meteor",
      "Blizzard",
    ]);
    expect(((await (await put("toma", 1, "Ice")).json()) as State).change).toBe(
      "none",
    );

    // ほかのキャラが同じカードを付けていても付けられ、ほかのキャラは変わらない。
    expect((await put("luka", 0, "Embers")).status).toBe(200);
    const after = await read(user.token);
    expect(character(after, "toma")?.cards).toEqual(
      character(swapped, "toma")?.cards,
    );
    expect(character(after, "aria")).toEqual(character(before, "aria"));
  });

  it("不正な枠・キャラ・カード・レベルを400で、持っていないキャラを404で拒否する", async () => {
    const user = await scenario.login("party-invalid");
    await read(user.token);
    const invalid = [
      scenario.request(
        "/party/slots/4",
        "PUT",
        { characterId: "toma" },
        user.token,
      ),
      scenario.request("/party/slots/-1", "DELETE", undefined, user.token),
      scenario.request(
        "/party/slots/a",
        "PUT",
        { characterId: "toma" },
        user.token,
      ),
      scenario.request(
        "/party/slots/0",
        "PUT",
        { characterId: "nobody" },
        user.token,
      ),
      scenario.request(
        "/party/slots/0",
        "PUT",
        { characterId: null },
        user.token,
      ),
      scenario.request("/party/slots/0", "PUT", null, user.token),
      scenario.request(
        "/party/characters/toma/cards/4",
        "PUT",
        { skillId: "Fire" },
        user.token,
      ),
      scenario.request(
        "/party/characters/nobody/cards/0",
        "PUT",
        { skillId: "Fire" },
        user.token,
      ),
      scenario.request(
        "/party/characters/toma/cards/0",
        "PUT",
        { skillId: "Unknown" },
        user.token,
      ),
      levelUp(user.token, "toma", {
        requestId: requestIds.first,
        fromLevel: 12,
        toLevel: 12,
      }),
      levelUp(user.token, "toma", {
        requestId: requestIds.first,
        fromLevel: 12,
        toLevel: MAX_LEVEL + 1,
      }),
      levelUp(user.token, "toma", {
        requestId: "invalid",
        fromLevel: 12,
        toLevel: 13,
      }),
      levelUp(user.token, "nobody", {
        requestId: requestIds.first,
        fromLevel: 1,
        toLevel: 2,
      }),
    ];
    for (const response of await Promise.all(invalid)) {
      expect(response.status).toBe(400);
    }

    // 持ち物から外したキャラは、枠にもカードにもレベルアップにも使えない。
    await scenario.env.DB.batch([
      scenario.env.DB.prepare(
        "DELETE FROM party_character_cards WHERE user_id = ? AND character_id = 'ritsu'",
      ).bind(user.userId),
      scenario.env.DB.prepare(
        "DELETE FROM party_characters WHERE user_id = ? AND character_id = 'ritsu'",
      ).bind(user.userId),
    ]);
    const missing = await Promise.all([
      scenario.request(
        "/party/slots/0",
        "PUT",
        { characterId: "ritsu" },
        user.token,
      ),
      scenario.request(
        "/party/characters/ritsu/cards/0",
        "PUT",
        { skillId: "Fire" },
        user.token,
      ),
      levelUp(user.token, "ritsu", {
        requestId: requestIds.first,
        fromLevel: 1,
        toLevel: 2,
      }),
    ]);
    for (const response of missing) {
      expect(response.status).toBe(404);
      expect(await response.json()).toEqual({ error: "character_not_owned" });
    }
  });

  it("ルーンを使ってレベルを上げ、同じ要求の再送では二重に引かない", async () => {
    const user = await scenario.login("party-level-up");
    await read(user.token);
    await scenario.setRunes(user.userId, 3000);
    const body = { requestId: requestIds.first, fromLevel: 12, toLevel: 14 };
    const cost = levelUpCost(12, 14);
    expect(cost).toBe(2500);

    const first = await levelUp(user.token, "toma", body);
    expect(first.status).toBe(200);
    const raised = (await first.json()) as State;
    expect(raised.levelUp).toEqual({
      characterId: "toma",
      fromLevel: 12,
      toLevel: 14,
      runes: cost,
      balanceAfter: 500,
    });
    expect(character(raised, "toma")?.level).toBe(14);
    expect(raised.runes).toBe(500);

    const replay = (await (
      await levelUp(user.token, "toma", body)
    ).json()) as State;
    expect(replay).toEqual(raised);
    expect((await read(user.token)).runes).toBe(500);

    // 同じ要求IDで中身の違う要求は受け付けない。
    const conflict = await levelUp(user.token, "toma", {
      ...body,
      toLevel: 13,
    });
    expect(conflict.status).toBe(409);
    expect(await conflict.json()).toEqual({ error: "request_conflict" });
  });

  it("今のレベルと違う要求とルーンの足りない要求を拒否し、何も変えない", async () => {
    const user = await scenario.login("party-level-up-rejected");
    const before = await read(user.token);

    // ルーンを1度も受け取っていないユーザーは、残高0として扱う。
    const empty = await levelUp(user.token, "ritsu", {
      requestId: requestIds.first,
      fromLevel: 1,
      toLevel: 2,
    });
    expect(empty.status).toBe(409);
    expect(await empty.json()).toEqual({ error: "insufficient_runes" });

    await scenario.setRunes(user.userId, 1000);
    const changed = await levelUp(user.token, "toma", {
      requestId: requestIds.second,
      fromLevel: 11,
      toLevel: 12,
    });
    expect(changed.status).toBe(409);
    expect(await changed.json()).toEqual({ error: "level_changed" });

    const short = await levelUp(user.token, "toma", {
      requestId: requestIds.third,
      fromLevel: 12,
      toLevel: 13,
    });
    expect(short.status).toBe(409);
    expect(await short.json()).toEqual({ error: "insufficient_runes" });

    expect(await read(user.token)).toEqual({ ...before, runes: 1000 });
  });

  it("ユーザーごとに別のキャラ・編成・カードを持つ", async () => {
    const owner = await scenario.login("party-owner");
    const other = await scenario.login("party-other");
    await read(owner.token);
    const before = await read(other.token);

    await scenario.request(
      "/party/slots/0",
      "PUT",
      { characterId: "rita" },
      owner.token,
    );
    await scenario.request(
      "/party/characters/toma/cards/0",
      "PUT",
      { skillId: "Heal" },
      owner.token,
    );
    await scenario.setRunes(owner.userId, 5000);
    await levelUp(owner.token, "luka", {
      requestId: requestIds.first,
      fromLevel: 11,
      toLevel: 12,
    });
    expect(await read(other.token)).toEqual(before);
  });
});
