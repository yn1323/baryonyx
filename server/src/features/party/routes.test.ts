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
  from?: string;
  characters: { id: string; level: number; cards: (string | null)[] }[];
  slots: { slot: number; characterId: string | null }[];
  skillCards: {
    id: string;
    skillId: string;
    multiplier: number;
    quality: string;
  }[];
  items: { itemId: string; count: number }[];
  runes: number;
  rules: { maxLevel: number; costPerLevel: number };
};

const missingCard = "card:bbbbbbbb-1111-4111-8111-bbbbbbbbbbbb";

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
  // 枠の中身を、スキルの種類のID（アイテムは item:<ID> のまま）で読む。
  const skillsOf = (state: State, id: string) =>
    character(state, id)?.cards.map((key) =>
      key?.startsWith("card:")
        ? (state.skillCards.find((card) => `card:${card.id}` === key)
            ?.skillId ?? key)
        : key,
    );

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
        card: "item:herb",
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
    // 同時に届いても、カードは1回分だけ入る。
    const [first, second] = await Promise.all([
      read(user.token),
      read(user.token),
    ]);
    expect(second).toEqual(first);
    expect(first.characters.map((entry) => entry.level)).toEqual(
      STARTER_CHARACTERS.map((entry) => entry.level),
    );
    for (const entry of STARTER_CHARACTERS) {
      expect(skillsOf(first, entry.characterId)).toEqual([...entry.cards]);
    }
    // 初めのカードは、1枚ずつ品質C（×1.00）で持ち物に入る。
    expect(first.skillCards).toHaveLength(STARTER_CHARACTERS.length * 2);
    expect(
      first.skillCards.every(
        (card) => card.multiplier === 1 && card.quality === "C",
      ),
    ).toBe(true);
    expect(first.items).toEqual([]);
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

  it("持っているスキルの1枚かアイテムを枠に入れ、ほかの枠やほかのキャラからは付け替える", async () => {
    const user = await scenario.login("party-cards");
    const before = await read(user.token);
    // 冒険で手に入れた火の粉（×1.72）と、薬草1つを持たせる。
    const embers = "cccccccc-1111-4111-8111-cccccccccccc";
    const now = new Date().toISOString();
    await scenario.env.DB.batch([
      scenario.env.DB.prepare(
        "INSERT INTO skill_cards (id, user_id, skill_id, multiplier_percent, acquired_at) VALUES (?, ?, 'Embers', 172, ?)",
      ).bind(embers, user.userId, now),
      scenario.env.DB.prepare(
        "INSERT INTO item_holdings (user_id, item_id, count, acquired_at, updated_at) VALUES (?, 'herb', 1, ?, ?)",
      ).bind(user.userId, now, now),
    ]);
    const put = (id: string, slot: number, card: string) =>
      scenario.request(
        `/party/characters/${id}/cards/${slot}`,
        "PUT",
        { card },
        user.token,
      );
    const ice = character(before, "toma")?.cards[1] as string;

    const replaced = (await (
      await put("toma", 0, `card:${embers}`)
    ).json()) as State;
    expect(replaced.change).toBe("replace");
    expect(skillsOf(replaced, "toma")).toEqual(["Embers", "Ice"]);
    expect(
      replaced.skillCards.find((card) => card.id === embers),
    ).toMatchObject({ skillId: "Embers", multiplier: 1.72 });
    expect(replaced.items).toEqual([{ itemId: "herb", count: 1 }]);

    const swapped = (await (await put("toma", 0, ice)).json()) as State;
    expect(swapped.change).toBe("swap");
    expect(skillsOf(swapped, "toma")).toEqual(["Ice", "Embers"]);
    expect(((await (await put("toma", 0, ice)).json()) as State).change).toBe(
      "none",
    );

    // ほかのキャラが付けている1枚は、そのキャラから外して付ける。
    const moved = (await (
      await put("luka", 0, `card:${embers}`)
    ).json()) as State;
    expect(moved.change).toBe("move");
    expect(moved.from).toBe("toma");
    expect(character(moved, "toma")?.cards).toEqual([ice, null]);
    expect(skillsOf(moved, "luka")?.[0]).toBe("Embers");

    // アイテムは持っている数まで入れられる。
    expect((await put("aria", 0, "item:herb")).status).toBe(200);
    const used = await put("mina", 0, "item:herb");
    expect(used.status).toBe(409);
    expect(await used.json()).toEqual({ error: "item_in_use" });

    // 持っていない1枚とアイテムは入れられない。
    for (const card of [missingCard, "item:acorn"]) {
      const missing = await put("mina", 0, card);
      expect(missing.status).toBe(404);
      expect(await missing.json()).toEqual({ error: "card_not_owned" });
    }
    const after = await read(user.token);
    expect(skillsOf(after, "aria")).toEqual(["item:herb", "ShieldBash"]);
    expect(character(after, "mina")).toEqual(character(before, "mina"));
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
        "/party/characters/toma/cards/2",
        "PUT",
        { card: missingCard },
        user.token,
      ),
      scenario.request(
        "/party/characters/nobody/cards/0",
        "PUT",
        { card: missingCard },
        user.token,
      ),
      scenario.request(
        "/party/characters/toma/cards/0",
        "PUT",
        { card: "Fire" },
        user.token,
      ),
      scenario.request(
        "/party/characters/toma/cards/0",
        "PUT",
        { card: "item:unknown" },
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
        { card: missingCard },
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
    const owned = await read(owner.token);
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
      { card: character(owned, "luka")?.cards[0] },
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
