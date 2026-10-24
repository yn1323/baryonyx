import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  type ApiScenario,
  createApiScenario,
} from "../../../tests/support/api-scenario.js";
import {
  CARD_SLOT_COUNT,
  COST_PER_LEVEL,
  EVOLUTION_GRACE_MINUTES,
  levelUpCost,
  MAX_LEVEL,
  STARTER_CHARACTERS,
  STARTER_SLOTS,
  UNIQUE_SKILL_COUNT,
  UNIQUE_UNLOCK_LEVELS,
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
  characters: {
    id: string;
    level: number;
    cards: (string | null)[];
    uniques: { index: number; set: number; acquired: number[] }[];
  }[];
  slots: { slot: number; characterId: string | null }[];
  skillCards: {
    id: string;
    skillId: string;
    multiplier: number;
    quality: string;
    awakenedBranch: number | null;
  }[];
  items: { itemId: string; count: number }[];
  runes: number;
  rules: { maxLevel: number; costPerLevel: number };
};

const missingCardId = "bbbbbbbb-1111-4111-8111-bbbbbbbbbbbb";
const missingCard = `card:${missingCardId}`;

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

  // 固有スキルにセットする段階を選ぶ・冒険の戦闘での進化を保存する。
  const setUnique = (token: string, id: string, index: number, stage: number) =>
    scenario.request(
      `/party/characters/${id}/uniques/${index}`,
      "PUT",
      { stage },
      token,
    );
  const evolve = (token: string, id: string, index: number, stage: number) =>
    scenario.request(
      `/party/characters/${id}/uniques/${index}/evolve`,
      "POST",
      { stage },
      token,
    );
  const awakenCard = (token: string, cardId: string, branch: unknown) =>
    scenario.request(
      `/party/skill-cards/${cardId}/awaken`,
      "POST",
      { branch },
      token,
    );
  const cardOf = (state: State, cardId: string) =>
    state.skillCards.find((card) => card.id === cardId);
  // 進化は冒険の戦闘でだけ起きるため、進化を送る前に冒険を始めておく。
  const startAdventure = async (token: string) => {
    await read(token);
    const response = await scenario.request(
      "/adventure",
      "POST",
      { destinationId: "forest-ruins" },
      token,
    );
    expect(response.status).toBe(200);
  };
  const uniqueOf = (state: State, id: string, index: number) =>
    character(state, id)?.uniques[index];
  const initialUniques = Array.from(
    { length: UNIQUE_SKILL_COUNT },
    (_, index) => ({
      index,
      set: 0,
      acquired: [0],
      multiplier: 1,
      quality: "C",
    }),
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
      scenario.request("/party/characters/toma/uniques/0", "PUT", {
        stage: 0,
      }),
      scenario.request("/party/characters/toma/uniques/0/evolve", "POST", {
        stage: 1,
      }),
      scenario.request(`/party/skill-cards/${missingCardId}/awaken`, "POST", {
        branch: 1,
      }),
    ]);
    expect(responses.map((response) => response.status)).toEqual([
      401, 401, 401, 401, 401, 401, 401, 401,
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
      // 初めのカードのない後ろの枠は空き（null）になる。
      expect(skillsOf(first, entry.characterId)).toEqual(
        Array.from(
          { length: CARD_SLOT_COUNT },
          (_, slot) => entry.cards[slot] ?? null,
        ),
      );
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
    expect(skillsOf(replaced, "toma")).toEqual(["Embers", "Ice", null]);
    expect(
      replaced.skillCards.find((card) => card.id === embers),
    ).toMatchObject({ skillId: "Embers", multiplier: 1.72 });
    expect(replaced.items).toEqual([{ itemId: "herb", count: 1 }]);

    const swapped = (await (await put("toma", 0, ice)).json()) as State;
    expect(swapped.change).toBe("swap");
    expect(skillsOf(swapped, "toma")).toEqual(["Ice", "Embers", null]);
    expect(((await (await put("toma", 0, ice)).json()) as State).change).toBe(
      "none",
    );

    // ほかのキャラが付けている1枚は、そのキャラから外して付ける。
    const moved = (await (
      await put("luka", 0, `card:${embers}`)
    ).json()) as State;
    expect(moved.change).toBe("move");
    expect(moved.from).toBe("toma");
    expect(character(moved, "toma")?.cards).toEqual([ice, null, null]);
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
    expect(skillsOf(after, "aria")).toEqual(["item:herb", "ShieldBash", null]);
    expect(character(after, "mina")).toEqual(character(before, "mina"));
  });

  it("キャラの枠のスキルやアイテムを外して空きにし、空きの枠は変えない", async () => {
    const user = await scenario.login("party-card-clear");
    const before = await read(user.token);
    const clear = (id: string, slot: number) =>
      scenario.request(
        `/party/characters/${id}/cards/${slot}`,
        "DELETE",
        undefined,
        user.token,
      );
    const removed = (await (await clear("toma", 0)).json()) as State;
    expect(removed.change).toBe("remove");
    expect(character(removed, "toma")?.cards).toEqual([
      null,
      ...(character(before, "toma")?.cards.slice(1) ?? []),
    ]);
    expect(((await (await clear("toma", 0)).json()) as State).change).toBe(
      "none",
    );
    expect((await clear("toma", 9)).status).toBe(400);
    expect((await clear("nobody", 0)).status).toBe(400);
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
        `/party/characters/toma/cards/${CARD_SLOT_COUNT}`,
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
      setUnique(user.token, "toma", UNIQUE_SKILL_COUNT, 0),
      setUnique(user.token, "toma", 0, 7),
      setUnique(user.token, "toma", 0, -1),
      setUnique(user.token, "nobody", 0, 0),
      scenario.request(
        "/party/characters/toma/uniques/0",
        "PUT",
        { stage: "1" },
        user.token,
      ),
      scenario.request(
        "/party/characters/toma/uniques/a/evolve",
        "POST",
        { stage: 1 },
        user.token,
      ),
      evolve(user.token, "toma", 0, 0),
      evolve(user.token, "toma", 0, 7),
      evolve(user.token, "toma", 0, 1.5),
      evolve(user.token, "toma", -1, 1),
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
      setUnique(user.token, "ritsu", 0, 0),
      evolve(user.token, "ritsu", 0, 1),
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

  it("固有スキルは最初の段階をセットした状態から始まり、取得していない段階はセットできない", async () => {
    const user = await scenario.login("party-unique-start");
    const first = await read(user.token);
    for (const entry of first.characters) {
      expect(entry.uniques).toEqual(initialUniques);
    }

    const unacquired = await setUnique(user.token, "toma", 0, 1);
    expect(unacquired.status).toBe(409);
    expect(await unacquired.json()).toEqual({ error: "stage_not_acquired" });
    const same = (await (
      await setUnique(user.token, "toma", 0, 0)
    ).json()) as State;
    expect(same.change).toBe("none");
    expect(await read(user.token)).toEqual(first);
  });

  it("固有スキルは次の段階へ1段ずつ進化し、同じ進化の再送では何も変えない", async () => {
    const user = await scenario.login("party-unique-evolve");
    await startAdventure(user.token);
    const before = await read(user.token);

    // 枝の2段目へ、最初の段階から飛んでは進めない。
    const skipped = await evolve(user.token, "toma", 0, 2);
    expect(skipped.status).toBe(409);
    expect(await skipped.json()).toEqual({ error: "not_next_stage" });

    const first = await evolve(user.token, "toma", 0, 1);
    expect(first.status).toBe(200);
    const evolved = (await first.json()) as State;
    expect(evolved.change).toBe("evolve");
    expect(uniqueOf(evolved, "toma", 0)).toEqual({
      index: 0,
      set: 1,
      acquired: [0, 1],
      multiplier: 1,
      quality: "C",
    });

    // 通信が切れて送り直した同じ進化は、失敗させずに何も変えない。
    const replay = (await (
      await evolve(user.token, "toma", 0, 1)
    ).json()) as State;
    expect(replay.change).toBe("none");
    expect({ ...replay, change: undefined }).toEqual({
      ...evolved,
      change: undefined,
    });

    const second = (await (
      await evolve(user.token, "toma", 0, 2)
    ).json()) as State;
    expect(second.change).toBe("evolve");

    // 枝の2段目から先へは進まず、別の枝へも移れない。
    const other = await evolve(user.token, "toma", 0, 3);
    expect(other.status).toBe(409);
    expect(await other.json()).toEqual({ error: "not_next_stage" });

    const after = await read(user.token);
    expect(uniqueOf(after, "toma", 0)).toEqual({
      index: 0,
      set: 2,
      acquired: [0, 1, 2],
      multiplier: 1,
      quality: "C",
    });
    // ほかの固有スキルとキャラは変わらない。
    expect(uniqueOf(after, "toma", 1)).toEqual(uniqueOf(before, "toma", 1));
    expect(character(after, "luka")).toEqual(character(before, "luka"));
  });

  it("解放するLvに届いていない固有スキルは進化もセットもできない", async () => {
    const user = await scenario.login("party-unique-locked");
    await startAdventure(user.token);
    const before = await read(user.token);
    // トーマ（Lv12）の3つ目と、リタ（Lv3）の2つ目はまだ解放していない。
    expect(character(before, "toma")?.level).toBeLessThan(
      UNIQUE_UNLOCK_LEVELS[2],
    );
    expect(character(before, "rita")?.level).toBeLessThan(
      UNIQUE_UNLOCK_LEVELS[1],
    );
    const locked = await Promise.all([
      evolve(user.token, "toma", 2, 1),
      evolve(user.token, "rita", 1, 1),
      setUnique(user.token, "toma", 2, 0),
    ]);
    for (const response of locked) {
      expect(response.status).toBe(409);
      expect(await response.json()).toEqual({ error: "unique_locked" });
    }

    // ちょうど解放するLv（アリアのLv10）なら、2つ目を進化できる。1つ目はLv1から使える。
    expect(character(before, "aria")?.level).toBe(UNIQUE_UNLOCK_LEVELS[1]);
    expect(UNIQUE_UNLOCK_LEVELS[0]).toBe(1);
    expect((await evolve(user.token, "aria", 1, 5)).status).toBe(200);
    expect((await evolve(user.token, "ritsu", 0, 1)).status).toBe(200);

    const after = await read(user.token);
    expect(uniqueOf(after, "toma", 2)).toEqual(initialUniques[2]);
    expect(uniqueOf(after, "rita", 1)).toEqual(initialUniques[1]);
    expect(uniqueOf(after, "aria", 1)).toEqual({
      index: 1,
      set: 5,
      acquired: [0, 5],
      multiplier: 1,
      quality: "C",
    });
  });

  it("手前の段階にセットし直すと、そこから別の枝へ進化でき、取得した段階は残る", async () => {
    const user = await scenario.login("party-unique-branch");
    await startAdventure(user.token);
    await read(user.token);
    await evolve(user.token, "toma", 0, 1);
    await evolve(user.token, "toma", 0, 2);

    const reset = (await (
      await setUnique(user.token, "toma", 0, 0)
    ).json()) as State;
    expect(reset.change).toBe("set");
    expect(uniqueOf(reset, "toma", 0)).toEqual({
      index: 0,
      set: 0,
      acquired: [0, 1, 2],
      multiplier: 1,
      quality: "C",
    });

    const branched = (await (
      await evolve(user.token, "toma", 0, 3)
    ).json()) as State;
    expect(branched.change).toBe("evolve");
    expect(uniqueOf(branched, "toma", 0)).toEqual({
      index: 0,
      set: 3,
      acquired: [0, 1, 2, 3],
      multiplier: 1,
      quality: "C",
    });

    // 取得済みの段階へ進むのも進化として扱う。
    await setUnique(user.token, "toma", 0, 0);
    const again = (await (
      await evolve(user.token, "toma", 0, 1)
    ).json()) as State;
    expect(again.change).toBe("evolve");
    expect(uniqueOf(again, "toma", 0)).toEqual({
      index: 0,
      set: 1,
      acquired: [0, 1, 2, 3],
      multiplier: 1,
      quality: "C",
    });

    // 取得した段階は、どれでもセットできる。
    const picked = (await (
      await setUnique(user.token, "toma", 0, 2)
    ).json()) as State;
    expect(picked.change).toBe("set");
    expect(uniqueOf(await read(user.token), "toma", 0)).toEqual({
      index: 0,
      set: 2,
      acquired: [0, 1, 2, 3],
      multiplier: 1,
      quality: "C",
    });
  });

  it("ユーザーごとに別の固有スキルの段階を持つ", async () => {
    const owner = await scenario.login("party-unique-owner");
    const other = await scenario.login("party-unique-other");
    await startAdventure(owner.token);
    await read(owner.token);
    const before = await read(other.token);

    await evolve(owner.token, "toma", 0, 1);
    await evolve(owner.token, "luka", 0, 3);
    expect(uniqueOf(await read(owner.token), "toma", 0)?.set).toBe(1);
    expect(await read(other.token)).toEqual(before);

    // ほかのユーザーが取得した段階は、自分ではセットできない。
    const foreign = await setUnique(other.token, "toma", 0, 1);
    expect(foreign.status).toBe(409);
    expect(await foreign.json()).toEqual({ error: "stage_not_acquired" });
  });

  it("進化は冒険の途中か、踏破・敗北で終えた直後だけ受け付け、途中でやめた冒険では受け付けない", async () => {
    const user = await scenario.login("party-unique-adventure");
    await read(user.token);
    const outside = await evolve(user.token, "toma", 0, 1);
    expect(outside.status).toBe(409);
    expect(await outside.json()).toEqual({ error: "not_in_adventure" });

    // 途中でやめた（撤退した）冒険の戦闘の進化は残さない。
    await startAdventure(user.token);
    const retreat = await scenario.request(
      "/adventure/end",
      "POST",
      { reason: "retreat", enemies: [] },
      user.token,
    );
    expect(retreat.status).toBe(200);
    const retreated = await evolve(user.token, "toma", 0, 1);
    expect(retreated.status).toBe(409);
    expect(await retreated.json()).toEqual({ error: "not_in_adventure" });

    // 敗北で終えた直後は、そのマスの戦闘の進化を受け付ける。
    await startAdventure(user.token);
    const ended = (minutesAgo: number) =>
      scenario.env.DB.prepare(
        "UPDATE adventure_runs SET status = 'defeated', ended_at = ? WHERE user_id = ? AND status = 'active'",
      )
        .bind(
          new Date(Date.now() - minutesAgo * 60_000).toISOString(),
          user.userId,
        )
        .run();
    await ended(1);
    expect((await evolve(user.token, "toma", 0, 1)).status).toBe(200);

    // 終えてから時間が経った冒険では受け付けない。
    await scenario.env.DB.prepare(
      "UPDATE adventure_runs SET ended_at = ? WHERE user_id = ?",
    )
      .bind(
        new Date(
          Date.now() - (EVOLUTION_GRACE_MINUTES + 1) * 60_000,
        ).toISOString(),
        user.userId,
      )
      .run();
    const late = await evolve(user.token, "toma", 0, 2);
    expect(late.status).toBe(409);
    expect(await late.json()).toEqual({ error: "not_in_adventure" });
    expect(uniqueOf(await read(user.token), "toma", 0)?.set).toBe(1);
  });

  it("持っているスキル1枚は冒険の戦闘で1回だけ覚醒し、同じ枝の再送は何も変えず、別の枝へは覚醒しない", async () => {
    const user = await scenario.login("party-card-awaken");
    const before = await read(user.token);
    // 覚醒していない1枚は、枝を null で返す。
    expect(before.skillCards.map((card) => card.awakenedBranch)).toEqual(
      before.skillCards.map(() => null),
    );
    const [target, untouched] = before.skillCards;

    await startAdventure(user.token);
    const first = await awakenCard(user.token, target.id, 2);
    expect(first.status).toBe(200);
    const awakened = (await first.json()) as State;
    expect(awakened.change).toBe("awaken");
    expect(cardOf(awakened, target.id)).toEqual({
      ...target,
      awakenedBranch: 2,
    });
    // ほかの1枚は覚醒しない。
    expect(cardOf(awakened, untouched.id)?.awakenedBranch).toBeNull();
    expect(cardOf(await read(user.token), target.id)?.awakenedBranch).toBe(2);

    // 同じ覚醒の再送は、何も変えずに成功として返す。
    const resent = await awakenCard(user.token, target.id, 2);
    expect(resent.status).toBe(200);
    expect(await resent.json()).toEqual({
      ...awakened,
      change: "none",
    });

    // 覚醒した1枚は、別の枝へ覚醒しない。
    const other = await awakenCard(user.token, target.id, 1);
    expect(other.status).toBe(409);
    expect(await other.json()).toEqual({ error: "already_awakened" });

    // 持っていない1枚とほかの人の1枚は404、不正な枝とIDは400で拒否する。
    const stranger = await scenario.login("party-card-awaken-other");
    await read(stranger.token);
    for (const [token, cardId] of [
      [user.token, missingCardId],
      [stranger.token, untouched.id],
    ]) {
      const missing = await awakenCard(token, cardId, 1);
      expect(missing.status).toBe(404);
      expect(await missing.json()).toEqual({ error: "card_not_owned" });
    }
    const invalid = await Promise.all([
      awakenCard(user.token, untouched.id, 0),
      awakenCard(user.token, untouched.id, 3),
      awakenCard(user.token, untouched.id, 1.5),
      awakenCard(user.token, untouched.id, "1"),
      awakenCard(user.token, "not-a-card", 1),
    ]);
    expect(invalid.map((response) => response.status)).toEqual([
      400, 400, 400, 400, 400,
    ]);
    expect(cardOf(await read(user.token), untouched.id)?.awakenedBranch).toBe(
      null,
    );
  });

  it("スキルの覚醒は冒険の途中か、踏破・敗北で終えた直後だけ受け付け、途中でやめた冒険では受け付けない", async () => {
    const user = await scenario.login("party-card-awaken-adventure");
    const [first, second, third] = (await read(user.token)).skillCards;
    const outside = await awakenCard(user.token, first.id, 1);
    expect(outside.status).toBe(409);
    expect(await outside.json()).toEqual({ error: "not_in_adventure" });

    // 途中でやめた（撤退した）冒険の戦闘の覚醒は残さない。
    await startAdventure(user.token);
    expect((await awakenCard(user.token, first.id, 1)).status).toBe(200);
    const retreat = await scenario.request(
      "/adventure/end",
      "POST",
      { reason: "retreat", enemies: [] },
      user.token,
    );
    expect(retreat.status).toBe(200);
    const retreated = await awakenCard(user.token, second.id, 1);
    expect(retreated.status).toBe(409);
    expect(await retreated.json()).toEqual({ error: "not_in_adventure" });
    // 保存済みの覚醒の再送は、冒険の外でも成功として返す。
    const resent = await awakenCard(user.token, first.id, 1);
    expect(resent.status).toBe(200);
    expect(((await resent.json()) as State).change).toBe("none");

    // 踏破で終えた直後は受け付け、時間が経てば受け付けない。
    await startAdventure(user.token);
    const ended = (minutesAgo: number) =>
      scenario.env.DB.prepare(
        "UPDATE adventure_runs SET status = 'cleared', ended_at = ? WHERE user_id = ? AND status = 'active'",
      )
        .bind(
          new Date(Date.now() - minutesAgo * 60_000).toISOString(),
          user.userId,
        )
        .run();
    await ended(1);
    expect((await awakenCard(user.token, second.id, 2)).status).toBe(200);
    await scenario.env.DB.prepare(
      "UPDATE adventure_runs SET ended_at = ? WHERE user_id = ?",
    )
      .bind(
        new Date(
          Date.now() - (EVOLUTION_GRACE_MINUTES + 1) * 60_000,
        ).toISOString(),
        user.userId,
      )
      .run();
    const late = await awakenCard(user.token, third.id, 1);
    expect(late.status).toBe(409);
    expect(await late.json()).toEqual({ error: "not_in_adventure" });

    const after = await read(user.token);
    expect(
      [first, second, third].map(
        (card) => cardOf(after, card.id)?.awakenedBranch,
      ),
    ).toEqual([1, 2, null]);
  });
});
