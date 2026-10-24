import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  type ApiScenario,
  createApiScenario,
} from "../../../tests/support/api-scenario.js";
import { EQUIPMENT } from "../equipment/catalog.js";
import { CARD_SKILL_IDS } from "../party/catalog.js";
import { workshopPrice } from "./catalog.js";

type Purchase = {
  category: string;
  id: string;
  itemId: string;
  multiplier: number;
  quality: string;
  runes: number;
  balanceAfter: number;
};

const requestIds = {
  first: "cccccccc-1111-4111-8111-cccccccccccc",
  second: "cccccccc-2222-4222-8222-cccccccccccc",
  third: "cccccccc-3333-4333-8333-cccccccccccc",
  fourth: "cccccccc-4444-4444-8444-cccccccccccc",
};

describe("商会API", () => {
  let scenario: ApiScenario;

  beforeAll(async () => {
    scenario = await createApiScenario();
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  const buy = async (
    token: string,
    body: unknown,
    expected = 200,
  ): Promise<{ purchase?: Purchase; error?: string }> => {
    const response = await scenario.request(
      "/workshop/buy",
      "POST",
      body,
      token,
    );
    expect(response.status).toBe(expected);
    return (await response.json()) as { purchase?: Purchase; error?: string };
  };

  const runesOf = async (token: string) =>
    (await scenario.read<{ runes: number }>("/adventure", token)).runes;

  const equipmentOf = async (token: string, equipmentId: string) =>
    (
      await scenario.read<{
        items: {
          id: string;
          equipmentId: string;
          multiplier: number;
          quality: string;
        }[];
      }>("/equipment", token)
    ).items.filter((item) => item.equipmentId === equipmentId);

  const skillCardsOf = async (userId: string, skillId: string) =>
    (
      await scenario.env.DB.prepare(
        "SELECT id, multiplier_percent FROM skill_cards WHERE user_id = ? AND skill_id = ?",
      )
        .bind(userId, skillId)
        .all<{ id: string; multiplier_percent: number }>()
    ).results;

  it("冒険のドロップの一覧にあるスキル・武器・防具・アクセサリーは、すべて☆で値段が決まる", () => {
    for (const id of CARD_SKILL_IDS) {
      expect(workshopPrice("skill", id)).toBeGreaterThan(0);
    }
    for (const entry of EQUIPMENT) {
      expect(workshopPrice(entry.slot, entry.id)).toBeGreaterThan(0);
    }
    // ☆1の200から、☆が1つ増えるごとに倍になる。
    expect(workshopPrice("skill", "Fire")).toBe(200);
    expect(workshopPrice("weapon", "iron-sword")).toBe(400);
    expect(workshopPrice("armor", "traveler-cloak")).toBe(800);
    expect(workshopPrice("skill", "Hone")).toBe(1600);
    expect(workshopPrice("accessory", "kraken-pearl")).toBe(3200);
    // 分類が違う物は売らない。
    expect(workshopPrice("weapon", "Fire")).toBeUndefined();
    expect(workshopPrice("armor", "iron-sword")).toBeUndefined();
  });

  it("セッションのない要求を401で、不正な入力を400で拒否する", async () => {
    const response = await scenario.request("/workshop/buy", "POST", {
      requestId: requestIds.first,
      category: "weapon",
      id: "iron-sword",
    });
    expect(response.status).toBe(401);

    const user = await scenario.login("workshop-invalid");
    for (const body of [
      { requestId: "short", category: "weapon", id: "iron-sword" },
      { requestId: requestIds.first, category: "item", id: "herb" },
      { requestId: requestIds.first, category: "unknown", id: "luck" },
      { requestId: requestIds.first, category: "weapon", id: "" },
      { requestId: requestIds.first, category: "weapon" },
    ]) {
      await buy(user.token, body, 400);
    }
  });

  it("これまでに手に入れた物を品質の最低で買え、残高不足・手に入れていない物・要求IDの使い回しを拒む", async () => {
    const user = await scenario.login("workshop-buy");
    // ライブラリを読むと、初めの装備とカードを配り、見つけた記録を残す。
    await scenario.read("/library", user.token);
    await scenario.setRunes(user.userId, 500);

    // 売らない物は404。
    expect(
      await buy(
        user.token,
        { requestId: requestIds.first, category: "weapon", id: "Fire" },
        404,
      ),
    ).toEqual({ error: "goods_not_found" });

    // 初めに持っている武器（鉄の剣☆2）を買うと、値段を引いて×1.00の1つを持ち物に足す。
    const swordsBefore = await equipmentOf(user.token, "iron-sword");
    const bought = await buy(user.token, {
      requestId: requestIds.first,
      category: "weapon",
      id: "iron-sword",
    });
    expect(bought.purchase).toEqual({
      category: "weapon",
      id: "iron-sword",
      itemId: expect.any(String),
      multiplier: 1,
      quality: "C",
      runes: 400,
      balanceAfter: 100,
    });
    expect(await runesOf(user.token)).toBe(100);
    const swordsAfter = await equipmentOf(user.token, "iron-sword");
    expect(swordsAfter).toHaveLength(swordsBefore.length + 1);
    expect(swordsAfter).toContainEqual(
      expect.objectContaining({
        id: bought.purchase?.itemId,
        multiplier: 1,
        quality: "C",
      }),
    );

    // 同じ要求の再送は同じ結果を返し、二重に引かない。同じ要求IDで別の物は買えない。
    expect(
      await buy(user.token, {
        requestId: requestIds.first,
        category: "weapon",
        id: "iron-sword",
      }),
    ).toEqual(bought);
    expect(await runesOf(user.token)).toBe(100);
    expect(await equipmentOf(user.token, "iron-sword")).toHaveLength(
      swordsAfter.length,
    );
    expect(
      await buy(
        user.token,
        { requestId: requestIds.first, category: "skill", id: "Fire" },
        409,
      ),
    ).toEqual({ error: "request_conflict" });

    // ルーンが足りなければ買えず、残高も持ち物も変わらない。
    const firesBefore = await skillCardsOf(user.userId, "Fire");
    expect(
      await buy(
        user.token,
        { requestId: requestIds.second, category: "skill", id: "Fire" },
        409,
      ),
    ).toEqual({ error: "insufficient_runes" });
    expect(await runesOf(user.token)).toBe(100);
    expect(await skillCardsOf(user.userId, "Fire")).toHaveLength(
      firesBefore.length,
    );

    // まだ手に入れていない物は、ルーンがあっても買えない。
    await scenario.setRunes(user.userId, 1000);
    expect(
      await buy(
        user.token,
        {
          requestId: requestIds.second,
          category: "accessory",
          id: "moss-charm",
        },
        409,
      ),
    ).toEqual({ error: "not_discovered" });
    expect(await runesOf(user.token)).toBe(1000);

    // 冒険で見つけたアクセサリーは買える。断った要求IDも、あとで使える。
    await scenario.env.DB.prepare(
      "INSERT INTO library_discoveries (user_id, kind, entry_id, discovered_at) VALUES (?, 'accessory', 'moss-charm', ?)",
    )
      .bind(user.userId, new Date().toISOString())
      .run();
    const charm = await buy(user.token, {
      requestId: requestIds.second,
      category: "accessory",
      id: "moss-charm",
    });
    expect(charm.purchase).toMatchObject({ runes: 200, balanceAfter: 800 });
    expect(await equipmentOf(user.token, "moss-charm")).toEqual([
      expect.objectContaining({ multiplier: 1, quality: "C" }),
    ]);

    // スキルは持ち物のカードに×1.00で1枚足す。同じ物を何度でも買える。
    for (const requestId of [requestIds.third, requestIds.fourth]) {
      await buy(user.token, { requestId, category: "skill", id: "Fire" });
    }
    const fires = await skillCardsOf(user.userId, "Fire");
    expect(fires).toHaveLength(firesBefore.length + 2);
    expect(fires.filter((card) => card.multiplier_percent === 100).length).toBe(
      fires.length,
    );
    expect(await runesOf(user.token)).toBe(400);
  });

  it("ほかのユーザーが見つけた物は買えない", async () => {
    const finder = await scenario.login("workshop-finder");
    const other = await scenario.login("workshop-other");
    await scenario.read("/library", finder.token);
    await scenario.read("/library", other.token);
    await scenario.env.DB.prepare(
      "INSERT INTO library_discoveries (user_id, kind, entry_id, discovered_at) VALUES (?, 'weapon', 'flame-dagger', ?)",
    )
      .bind(finder.userId, new Date().toISOString())
      .run();
    await scenario.setRunes(other.userId, 5000);
    expect(
      await buy(
        other.token,
        { requestId: requestIds.first, category: "weapon", id: "flame-dagger" },
        409,
      ),
    ).toEqual({ error: "not_discovered" });
    expect(await runesOf(other.token)).toBe(5000);
  });
});

type Craft = {
  category: string;
  id: string;
  itemId: string;
  multiplier: number | null;
  quality: string | null;
  runes: number;
  balanceAfter: number;
  materials: string[] | { id: string; count: number }[];
};

describe("商会の合成API", () => {
  let scenario: ApiScenario;

  beforeAll(async () => {
    scenario = await createApiScenario();
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  const craft = async (
    token: string,
    body: unknown,
    expected = 200,
  ): Promise<{ craft?: Craft; error?: string }> => {
    const response = await scenario.request(
      "/workshop/craft",
      "POST",
      body,
      token,
    );
    expect(response.status).toBe(expected);
    return (await response.json()) as { craft?: Craft; error?: string };
  };

  const runesOf = async (token: string) =>
    (await scenario.read<{ runes: number }>("/adventure", token)).runes;

  const now = () => new Date().toISOString();

  // ライブラリを読むと、初めの装備とカードを配り（どれもキャラに付けている）、見つけた記録を残す。
  const start = async (sub: string, runes: number) => {
    const user = await scenario.login(sub);
    await scenario.read("/library", user.token);
    await scenario.setRunes(user.userId, runes);
    return user;
  };

  // キャラに付けていないスキルを1枚足し、行のIDを返す。
  const addSkillCard = async (
    userId: string,
    skillId: string,
    percent: number,
  ) => {
    const id = crypto.randomUUID();
    await scenario.env.DB.prepare(
      "INSERT INTO skill_cards (id, user_id, skill_id, multiplier_percent, acquired_at) VALUES (?, ?, ?, ?, ?)",
    )
      .bind(id, userId, skillId, percent, now())
      .run();
    return id;
  };

  // キャラに付けていない装備を1つ足し、行のIDを返す。
  const addEquipment = async (
    userId: string,
    equipmentId: string,
    percent: number,
  ) => {
    const id = crypto.randomUUID();
    await scenario.env.DB.prepare(
      "INSERT INTO equipment_items (id, user_id, equipment_id, multiplier_percent, acquired_at) VALUES (?, ?, ?, ?, ?)",
    )
      .bind(id, userId, equipmentId, percent, now())
      .run();
    return id;
  };

  const discover = async (userId: string, kind: string, entryId: string) => {
    await scenario.env.DB.prepare(
      "INSERT INTO library_discoveries (user_id, kind, entry_id, discovered_at) VALUES (?, ?, ?, ?)",
    )
      .bind(userId, kind, entryId, now())
      .run();
  };

  const skillCardsOf = async (userId: string, ids: readonly string[]) =>
    (
      await scenario.env.DB.prepare(
        `SELECT id FROM skill_cards WHERE user_id = ? AND id IN (${ids.map(() => "?").join(", ")})`,
      )
        .bind(userId, ...ids)
        .all<{ id: string }>()
    ).results;

  const itemCountsOf = async (userId: string) =>
    Object.fromEntries(
      (
        await scenario.env.DB.prepare(
          "SELECT item_id, count FROM item_holdings WHERE user_id = ?",
        )
          .bind(userId)
          .all<{ item_id: string; count: number }>()
      ).results.map((row) => [row.item_id, row.count]),
    );

  it("☆2以上で主の物でない物だけを作り、作れない物を404で、不正な入力を400で拒否する", async () => {
    const user = await start("craft-invalid", 5000);
    const materialIds = ["a", "b", "c"];
    // ☆1・主の物・一覧にない物は作れない。
    for (const [category, id] of [
      ["skill", "Fire"],
      ["accessory", "guardian-circlet"],
      ["weapon", "Fire"],
    ]) {
      expect(
        await craft(
          user.token,
          { requestId: requestIds.first, category, id, materialIds },
          404,
        ),
      ).toEqual({ error: "goods_not_found" });
    }
    expect(
      await craft(
        user.token,
        {
          requestId: requestIds.first,
          category: "item",
          id: "guardian-seed",
          items: [{ id: "spore-powder", count: 3 }],
        },
        404,
      ),
    ).toEqual({ error: "goods_not_found" });

    // 材料は重ならない3つ、アイテムは種類を重ねずに合計3個。
    for (const body of [
      { category: "weapon", id: "iron-sword", materialIds: ["a", "b"] },
      { category: "weapon", id: "iron-sword", materialIds: ["a", "a", "b"] },
      { category: "weapon", id: "iron-sword", items: [{ id: "a", count: 3 }] },
      { category: "item", id: "moss-wad", items: [{ id: "herb", count: 2 }] },
      {
        category: "item",
        id: "moss-wad",
        items: [
          { id: "herb", count: 2 },
          { id: "herb", count: 1 },
        ],
      },
      { category: "item", id: "moss-wad", materialIds },
      { category: "unknown", id: "luck", materialIds },
    ]) {
      await craft(user.token, { requestId: requestIds.first, ...body }, 400);
    }
    expect(await runesOf(user.token)).toBe(5000);
  });

  it("スキルを合成すると、材料の位置の平均で倍率が決まり、ルーンを引いて材料を消し、1枚足す。再送は二重に作らない", async () => {
    const user = await start("craft-skill", 1000);
    // 盾打ち（☆2・上限220）を、☆1のスキル3枚から作る。初めのカードにあるため見つけている。
    const materialIds = [
      await addSkillCard(user.userId, "Fire", 240), // 位置1
      await addSkillCard(user.userId, "Slash", 100), // 位置0
      await addSkillCard(user.userId, "Ice", 166), // 位置66/120
    ];
    const body = {
      requestId: requestIds.first,
      category: "skill",
      id: "ShieldBash",
      materialIds,
      // クライアント（JsonUtility）は、使わない方の配列も空で送る。
      items: [],
    };
    const made = await craft(user.token, body);
    // 位置の平均（1 + 0 + 0.55）/ 3 に幅120を掛けて62。
    expect(made.craft).toEqual({
      category: "skill",
      id: "ShieldBash",
      itemId: expect.any(String),
      multiplier: 1.62,
      quality: "B",
      runes: 200,
      balanceAfter: 800,
      materials: materialIds,
    });
    expect(await runesOf(user.token)).toBe(800);
    expect(await skillCardsOf(user.userId, materialIds)).toEqual([]);
    expect(
      await scenario.env.DB.prepare(
        "SELECT skill_id, multiplier_percent FROM skill_cards WHERE id = ?",
      )
        .bind(made.craft?.itemId)
        .first(),
    ).toEqual({ skill_id: "ShieldBash", multiplier_percent: 162 });

    // 同じ要求の再送は、材料の順が違っても同じ結果を返し、二重に引かない。
    expect(
      await craft(user.token, {
        ...body,
        materialIds: [...materialIds].reverse(),
      }),
    ).toEqual(made);
    expect(await runesOf(user.token)).toBe(800);
    // 同じ要求IDで中身の違う要求は断る。
    expect(
      await craft(
        user.token,
        { ...body, materialIds: [...materialIds.slice(0, 2), "other"] },
        409,
      ),
    ).toEqual({ error: "request_conflict" });
    expect(await craft(user.token, { ...body, id: "Embers" }, 409)).toEqual({
      error: "request_conflict",
    });
  });

  it("装備は付けている物・☆の違う物・分類の違う物を材料にできず、合う3つで作れる", async () => {
    const user = await start("craft-weapon", 1000);
    const worn = await scenario.env.DB.prepare(
      "SELECT s.item_id AS id FROM equipment_character_slots s JOIN equipment_items i ON i.id = s.item_id WHERE s.user_id = ? AND i.equipment_id = 'short-bow'",
    )
      .bind(user.userId)
      .first<{ id: string }>();
    const sword = await addEquipment(user.userId, "wooden-sword", 140); // 位置0.5
    const bow = await addEquipment(user.userId, "short-bow", 150); // 位置0.5
    const staff = await addEquipment(user.userId, "oak-staff", 200); // 位置1
    const hammer = await addEquipment(user.userId, "war-hammer", 100); // ☆2
    const leather = await addEquipment(user.userId, "leather-armor", 100); // 防具の☆1
    const request = (materialIds: string[]) => ({
      requestId: requestIds.first,
      category: "weapon",
      id: "iron-sword",
      materialIds,
    });
    for (const materialIds of [
      [sword, bow, worn?.id as string],
      [sword, bow, hammer],
      [sword, bow, leather],
      [sword, bow, crypto.randomUUID()],
    ]) {
      expect(await craft(user.token, request(materialIds), 409)).toEqual({
        error: "invalid_materials",
      });
    }
    expect(await runesOf(user.token)).toBe(1000);

    // 鉄の剣（☆2・上限200）を作る。位置の平均2/3に幅100を掛けて67。
    const made = await craft(user.token, request([sword, bow, staff]));
    expect(made.craft).toMatchObject({
      category: "weapon",
      id: "iron-sword",
      multiplier: 1.67,
      quality: "B",
      runes: 200,
      balanceAfter: 800,
      materials: [sword, bow, staff],
    });
    const items = (
      await scenario.read<{
        items: { id: string; equipmentId: string; multiplier: number }[];
      }>("/equipment", user.token)
    ).items;
    expect(items.map((item) => item.id)).not.toContain(sword);
    expect(items.map((item) => item.id)).not.toContain(staff);
    expect(items).toContainEqual(
      expect.objectContaining({
        id: made.craft?.itemId,
        equipmentId: "iron-sword",
        multiplier: 1.67,
      }),
    );

    // 消えた材料は、別の要求IDでも使えない。
    expect(
      await craft(
        user.token,
        { ...request([sword, bow, staff]), requestId: requestIds.second },
        409,
      ),
    ).toEqual({ error: "invalid_materials" });
  });

  it("アイテムは数から引き、使いきった種類を消し、デッキに入れている数を残す", async () => {
    const user = await start("craft-item", 1000);
    await discover(user.userId, "item", "moss-wad");
    await scenario.env.DB.batch([
      scenario.env.DB.prepare(
        "INSERT INTO item_holdings (user_id, item_id, count, acquired_at, updated_at) VALUES (?, 'herb', 3, ?, ?), (?, 'acorn', 1, ?, ?)",
      ).bind(user.userId, now(), now(), user.userId, now(), now()),
      // トーマの3つ目の枠に薬草を1つ入れている。
      scenario.env.DB.prepare(
        "INSERT INTO party_character_cards (user_id, character_id, slot, card_id, item_id, updated_at) VALUES (?, 'toma', 2, NULL, 'herb', ?)",
      ).bind(user.userId, now()),
    ]);

    // デッキに入れている1つを残すため、薬草は2つまでしか使えない。
    expect(
      await craft(
        user.token,
        {
          requestId: requestIds.first,
          category: "item",
          id: "moss-wad",
          items: [{ id: "herb", count: 3 }],
          materialIds: [],
        },
        409,
      ),
    ).toEqual({ error: "invalid_materials" });

    const made = await craft(user.token, {
      requestId: requestIds.first,
      category: "item",
      id: "moss-wad",
      items: [
        { id: "herb", count: 2 },
        { id: "acorn", count: 1 },
      ],
      // クライアント（JsonUtility）は、使わない方の配列も空で送る。
      materialIds: [],
    });
    expect(made.craft).toEqual({
      category: "item",
      id: "moss-wad",
      itemId: "moss-wad",
      multiplier: null,
      quality: null,
      runes: 200,
      balanceAfter: 800,
      materials: [
        { id: "herb", count: 2 },
        { id: "acorn", count: 1 },
      ],
    });
    // 使いきったどんぐりの行は消え、薬草はデッキの1つが残る。
    expect(await itemCountsOf(user.userId)).toEqual({
      herb: 1,
      "moss-wad": 1,
    });

    // ☆の違うアイテムは材料にできない。
    await scenario.env.DB.prepare(
      "INSERT INTO item_holdings (user_id, item_id, count, acquired_at, updated_at) VALUES (?, 'sea-salt', 3, ?, ?)",
    )
      .bind(user.userId, now(), now())
      .run();
    expect(
      await craft(
        user.token,
        {
          requestId: requestIds.second,
          category: "item",
          id: "moss-wad",
          items: [{ id: "sea-salt", count: 3 }],
        },
        409,
      ),
    ).toEqual({ error: "invalid_materials" });
    expect(await runesOf(user.token)).toBe(800);
  });

  it("見つけていない物とルーン不足を拒み、材料も残高も変えない", async () => {
    const user = await start("craft-reject", 100);
    const materialIds = [
      await addSkillCard(user.userId, "Fire", 100),
      await addSkillCard(user.userId, "Fire", 100),
      await addSkillCard(user.userId, "Heal", 100),
    ];

    // 火の粉（☆2）は、まだ見つけていない。
    await scenario.setRunes(user.userId, 1000);
    expect(
      await craft(
        user.token,
        {
          requestId: requestIds.first,
          category: "skill",
          id: "Embers",
          materialIds,
        },
        409,
      ),
    ).toEqual({ error: "not_discovered" });

    // 盾打ち（☆2）の費用200に、ルーンが足りない。断った要求IDも、あとで使える。
    await scenario.setRunes(user.userId, 199);
    const body = {
      requestId: requestIds.first,
      category: "skill",
      id: "ShieldBash",
      materialIds,
    };
    expect(await craft(user.token, body, 409)).toEqual({
      error: "insufficient_runes",
    });
    expect(await runesOf(user.token)).toBe(199);
    expect(await skillCardsOf(user.userId, materialIds)).toHaveLength(3);

    await scenario.setRunes(user.userId, 200);
    const made = await craft(user.token, body);
    expect(made.craft).toMatchObject({
      multiplier: 1,
      quality: "C",
      runes: 200,
      balanceAfter: 0,
    });
  });

  it("キャラのデッキに入れているスキルは材料にできない", async () => {
    const user = await start("craft-deck", 1000);
    // 初めのカードは、どれもキャラの枠に入れている。
    const fire = await scenario.env.DB.prepare(
      "SELECT c.card_id AS id FROM party_character_cards c JOIN skill_cards s ON s.id = c.card_id WHERE c.user_id = ? AND s.skill_id = 'Fire'",
    )
      .bind(user.userId)
      .first<{ id: string }>();
    const materialIds = [
      fire?.id as string,
      await addSkillCard(user.userId, "Fire", 100),
      await addSkillCard(user.userId, "Fire", 100),
    ];
    expect(
      await craft(
        user.token,
        {
          requestId: requestIds.first,
          category: "skill",
          id: "ShieldBash",
          materialIds,
        },
        409,
      ),
    ).toEqual({ error: "invalid_materials" });
    expect(await skillCardsOf(user.userId, materialIds)).toHaveLength(3);
    expect(await runesOf(user.token)).toBe(1000);
  });
});

type Sale = {
  runes: number;
  balanceAfter: number;
  cardIds: string[];
  equipmentIds: string[];
  items: { id: string; count: number }[];
};

describe("商会の売却API", () => {
  let scenario: ApiScenario;

  beforeAll(async () => {
    scenario = await createApiScenario();
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  const sell = async (
    token: string,
    body: unknown,
    expected = 200,
  ): Promise<{ sale?: Sale; error?: string }> => {
    const response = await scenario.request(
      "/workshop/sell",
      "POST",
      body,
      token,
    );
    expect(response.status).toBe(expected);
    return (await response.json()) as { sale?: Sale; error?: string };
  };

  const runesOf = async (token: string) =>
    (await scenario.read<{ runes: number }>("/adventure", token)).runes;

  const now = () => new Date().toISOString();

  // ライブラリを読むと、初めの装備とカードを配る（どれもキャラに付けている）。
  const start = async (sub: string, runes?: number) => {
    const user = await scenario.login(sub);
    await scenario.read("/library", user.token);
    if (runes !== undefined) await scenario.setRunes(user.userId, runes);
    return user;
  };

  const insert = async (
    table: string,
    userId: string,
    id: string,
    percent: number,
  ) => {
    const row = crypto.randomUUID();
    const column = table === "skill_cards" ? "skill_id" : "equipment_id";
    await scenario.env.DB.prepare(
      `INSERT INTO ${table} (id, user_id, ${column}, multiplier_percent, acquired_at) VALUES (?, ?, ?, ?, ?)`,
    )
      .bind(row, userId, id, percent, now())
      .run();
    return row;
  };

  const addItems = async (userId: string, id: string, count: number) => {
    await scenario.env.DB.prepare(
      "INSERT INTO item_holdings (user_id, item_id, count, acquired_at, updated_at) VALUES (?, ?, ?, ?, ?)",
    )
      .bind(userId, id, count, now(), now())
      .run();
  };

  const countOf = async (sql: string, ...values: string[]) =>
    (
      await scenario.env.DB.prepare(sql)
        .bind(...values)
        .first<{ n: number }>()
    )?.n;

  it("付けていないスキル・装備とアイテムを売り、値段の合計を残高へ足して持ち物から消す。再送は二重に足さない", async () => {
    const user = await start("sell-all", 1000);
    // 火の玉（☆1）は50×1.24で62、木の剣（☆1）は50×1.25の62.5を四捨五入して63、鉄の剣（☆2）は100×1.50で150。
    const card = await insert("skill_cards", user.userId, "Fire", 124);
    const sword = await insert(
      "equipment_items",
      user.userId,
      "wooden-sword",
      125,
    );
    const iron = await insert(
      "equipment_items",
      user.userId,
      "iron-sword",
      150,
    );
    // 薬草（☆1）は1つ10を3つで30、苔玉（☆2）は1つ20。
    await addItems(user.userId, "herb", 3);
    await addItems(user.userId, "moss-wad", 1);
    const body = {
      requestId: requestIds.first,
      cardIds: [card],
      equipmentIds: [sword, iron],
      items: [
        { id: "herb", count: 3 },
        { id: "moss-wad", count: 1 },
      ],
    };

    const sold = await sell(user.token, body);
    const { requestId: _, ...goods } = body;
    expect(sold.sale).toEqual({
      ...goods,
      runes: 325,
      balanceAfter: 1325,
    });
    expect(await runesOf(user.token)).toBe(1325);
    expect(
      await countOf("SELECT COUNT(*) AS n FROM skill_cards WHERE id = ?", card),
    ).toBe(0);
    expect(
      await countOf(
        "SELECT COUNT(*) AS n FROM equipment_items WHERE id IN (?, ?)",
        sword,
        iron,
      ),
    ).toBe(0);
    expect(
      await countOf(
        "SELECT COUNT(*) AS n FROM item_holdings WHERE user_id = ? AND item_id IN ('herb', 'moss-wad')",
        user.userId,
      ),
    ).toBe(0);

    // 同じ要求の再送は、同じ結果を返して二重に足さない。売った物を変えた使い回しは拒む。
    expect((await sell(user.token, body)).sale).toEqual(sold.sale);
    expect(await runesOf(user.token)).toBe(1325);
    expect(await sell(user.token, { ...body, cardIds: [] }, 409)).toEqual({
      error: "request_conflict",
    });

    // 売った物は、別の要求IDでも売れない。
    expect(
      await sell(
        user.token,
        { requestId: requestIds.second, equipmentIds: [sword] },
        409,
      ),
    ).toEqual({ error: "invalid_goods" });
    expect(await runesOf(user.token)).toBe(1325);
  });

  it("付けている装備・デッキのスキルとアイテム・数の違うアイテム・ほかの人の物は売れず、残高も持ち物も変えない", async () => {
    const user = await start("sell-reject", 100);
    const other = await start("sell-other", 100);
    const worn = await scenario.env.DB.prepare(
      "SELECT item_id AS id FROM equipment_character_slots WHERE user_id = ?",
    )
      .bind(user.userId)
      .first<{ id: string }>();
    const deckCard = await scenario.env.DB.prepare(
      "SELECT card_id AS id FROM party_character_cards WHERE user_id = ? AND card_id IS NOT NULL",
    )
      .bind(user.userId)
      .first<{ id: string }>();
    const free = await insert(
      "equipment_items",
      user.userId,
      "wooden-sword",
      100,
    );
    const others = await insert(
      "equipment_items",
      other.userId,
      "wooden-sword",
      100,
    );
    await addItems(user.userId, "herb", 2);
    await addItems(user.userId, "acorn", 2);
    // トーマの3つ目の枠にどんぐりを1つ入れている。
    await scenario.env.DB.prepare(
      "INSERT INTO party_character_cards (user_id, character_id, slot, card_id, item_id, updated_at) VALUES (?, 'toma', 2, NULL, 'acorn', ?)",
    )
      .bind(user.userId, now())
      .run();

    for (const goods of [
      { equipmentIds: [free, worn?.id as string] },
      { cardIds: [deckCard?.id as string] },
      { items: [{ id: "acorn", count: 2 }] },
      { items: [{ id: "herb", count: 1 }] },
      { equipmentIds: [free, others] },
      { equipmentIds: [crypto.randomUUID()] },
    ]) {
      expect(
        await sell(user.token, { requestId: requestIds.first, ...goods }, 409),
      ).toEqual({ error: "invalid_goods" });
    }
    expect(await runesOf(user.token)).toBe(100);
    expect(
      await countOf(
        "SELECT COUNT(*) AS n FROM equipment_items WHERE id IN (?, ?)",
        free,
        others,
      ),
    ).toBe(2);

    // 何も選ばない・同じ物を重ねた要求は400で拒む。
    for (const body of [
      { requestId: requestIds.first },
      { requestId: requestIds.first, equipmentIds: [free, free] },
      { requestId: "x", equipmentIds: [free] },
    ]) {
      await sell(user.token, body, 400);
    }

    // 断った要求IDも、あとで使える。
    expect(
      (
        await sell(user.token, {
          requestId: requestIds.first,
          equipmentIds: [free],
          items: [{ id: "herb", count: 2 }],
        })
      ).sale,
    ).toMatchObject({ runes: 70, balanceAfter: 170 });
  });

  it("残高の行がまだなくても、売ったルーンで残高を作る", async () => {
    const user = await start("sell-new-wallet");
    await scenario.env.DB.prepare("DELETE FROM rune_wallets WHERE user_id = ?")
      .bind(user.userId)
      .run();
    await addItems(user.userId, "herb", 1);
    expect(
      (
        await sell(user.token, {
          requestId: requestIds.first,
          items: [{ id: "herb", count: 1 }],
        })
      ).sale,
    ).toMatchObject({ runes: 10, balanceAfter: 10 });
    expect(await runesOf(user.token)).toBe(10);
  });
});
