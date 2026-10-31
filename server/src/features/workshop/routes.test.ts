import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  type ApiScenario,
  createApiScenario,
} from "../../../tests/support/api-scenario.js";
import { EQUIPMENT, slotOf } from "../equipment/catalog.js";
import { ITEM_IDS } from "../items/catalog.js";
import { CARD_SKILL_IDS } from "../party/catalog.js";
import { craftStarsOf, workshopPrice } from "./catalog.js";
import { CRAFT_RECIPES } from "./recipes.js";

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
    // ☆1の1,000から、☆が1つ増えるごとに倍になる。
    expect(workshopPrice("skill", "Fire")).toBe(1000);
    expect(workshopPrice("weapon", "iron-sword")).toBe(2000);
    expect(workshopPrice("armor", "traveler-cloak")).toBe(4000);
    expect(workshopPrice("skill", "Hone")).toBe(8000);
    expect(workshopPrice("accessory", "kraken-pearl")).toBe(16000);
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
    await scenario.setRunes(user.userId, 2500);

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
      runes: 2000,
      balanceAfter: 500,
    });
    expect(await scenario.runesOf(user.token)).toBe(500);
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
    expect(await scenario.runesOf(user.token)).toBe(500);
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
    expect(await scenario.runesOf(user.token)).toBe(500);
    expect(await skillCardsOf(user.userId, "Fire")).toHaveLength(
      firesBefore.length,
    );

    // まだ手に入れていない物は、ルーンがあっても買えない。
    await scenario.setRunes(user.userId, 5000);
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
    expect(await scenario.runesOf(user.token)).toBe(5000);

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
    expect(charm.purchase).toMatchObject({ runes: 1000, balanceAfter: 4000 });
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
    expect(await scenario.runesOf(user.token)).toBe(2000);
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
    expect(await scenario.runesOf(other.token)).toBe(5000);
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

  const addItems = async (userId: string, counts: Record<string, number>) => {
    for (const [itemId, count] of Object.entries(counts)) {
      await scenario.env.DB.prepare(
        "INSERT INTO item_holdings (user_id, item_id, count, acquired_at, updated_at) VALUES (?, ?, ?, ?, ?) ON CONFLICT (user_id, item_id) DO UPDATE SET count = excluded.count",
      )
        .bind(userId, itemId, count, now(), now())
        .run();
    }
  };

  const discovered = async (userId: string, kind: string, entryId: string) =>
    (
      await scenario.env.DB.prepare(
        "SELECT entry_id FROM library_discoveries WHERE user_id = ? AND kind = ? AND entry_id = ?",
      )
        .bind(userId, kind, entryId)
        .all()
    ).results.length > 0;

  it("レシピは、作れる☆の物だけを、あるアイテム・装備・スキルで作り、同じ装備・スキルは1つまで使う", () => {
    for (const recipe of CRAFT_RECIPES) {
      expect(craftStarsOf(recipe.category, recipe.id)).toBeDefined();
      for (const material of recipe.materials) {
        if (material.category === "item") {
          expect(ITEM_IDS).toContain(material.id);
        } else if (material.category === "skill") {
          expect(CARD_SKILL_IDS).toContain(material.id);
          expect(material.count).toBe(1);
        } else {
          expect(slotOf(material.id)).toBe(material.category);
          expect(material.count).toBe(1);
        }
      }
    }
    // ☆1・主の物・レシピのない物は作れない。
    expect(craftStarsOf("skill", "Fire")).toBeUndefined();
    expect(craftStarsOf("accessory", "guardian-circlet")).toBeUndefined();
    expect(craftStarsOf("weapon", "wooden-sword")).toBeUndefined();
  });

  it("作れない物を404で、不正な入力を400で拒否する", async () => {
    const user = await start("craft-invalid", 5000);
    for (const [category, id] of [
      ["skill", "Fire"],
      ["accessory", "guardian-circlet"],
      ["weapon", "Fire"],
    ]) {
      expect(
        await craft(
          user.token,
          { requestId: requestIds.first, category, id, materialIds: [] },
          404,
        ),
      ).toEqual({ error: "goods_not_found" });
    }
    // アイテムは作れない。材料の行のIDは重ねない。
    for (const body of [
      { category: "item", id: "moss-wad", materialIds: [] },
      { category: "weapon", id: "flame-dagger", materialIds: ["a", "a"] },
      { category: "unknown", id: "luck", materialIds: [] },
    ]) {
      await craft(user.token, { requestId: requestIds.first, ...body }, 400);
    }
    expect(await scenario.runesOf(user.token)).toBe(5000);
  });

  it("アイテムだけのレシピは、数から引いて使いきった種類を消し、デッキの数を残して、品質Cの物を作る。再送は二重に作らない", async () => {
    const user = await start("craft-items", 1000);
    // 鉄の剣（☆2）：貝殻のつぶて×2・船乗りのロープ×1。
    await addItems(user.userId, { "shell-shard": 2, "sailor-rope": 2 });
    await scenario.env.DB.prepare(
      "INSERT INTO party_character_cards (user_id, character_id, slot, card_id, item_id, updated_at) VALUES (?, 'toma', 2, NULL, 'sailor-rope', ?)",
    )
      .bind(user.userId, now())
      .run();
    const body = {
      requestId: requestIds.first,
      category: "weapon",
      id: "iron-sword",
      materialIds: [],
    };
    const made = await craft(user.token, body);
    expect(made.craft).toEqual({
      category: "weapon",
      id: "iron-sword",
      itemId: expect.any(String),
      multiplier: 1,
      quality: "C",
      runes: 200,
      balanceAfter: 800,
      materials: [],
    });
    // 貝殻のつぶては使いきって消え、ロープはデッキの1つが残る。
    expect(await itemCountsOf(user.userId)).toEqual({ "sailor-rope": 1 });
    expect(
      await craft(user.token, body), // 再送は同じ結果
    ).toEqual(made);
    expect(await scenario.runesOf(user.token)).toBe(800);

    // デッキに入れている1つは使えないため、もう作れない。
    await addItems(user.userId, { "shell-shard": 2 });
    expect(
      await craft(user.token, { ...body, requestId: requestIds.second }, 409),
    ).toEqual({ error: "invalid_materials" });
    // 特別な材料のないレシピに、行のIDは送れない。
    await addItems(user.userId, { "sailor-rope": 3 });
    const extra = await addEquipment(user.userId, "short-bow", 100);
    expect(
      await craft(
        user.token,
        { ...body, requestId: requestIds.second, materialIds: [extra] },
        409,
      ),
    ).toEqual({ error: "invalid_materials" });
  });

  it("特別な装備を使うレシピは、その装備1つの品質で作り、付けている物・違う種類は使えない", async () => {
    const user = await start("craft-weapon", 1000);
    // 炎のダガー（☆4・上限180）：鉄の剣×1（特別）・海賊のラム酒×1・墨つぼ×1・船乗りのロープ×2。
    await addItems(user.userId, {
      "pirate-rum": 1,
      "ink-pot": 1,
      "sailor-rope": 2,
    });
    const worn = await scenario.env.DB.prepare(
      "SELECT s.item_id AS id FROM equipment_character_slots s JOIN equipment_items i ON i.id = s.item_id WHERE s.user_id = ? AND i.equipment_id = 'iron-sword'",
    )
      .bind(user.userId)
      .first<{ id: string }>();
    const bow = await addEquipment(user.userId, "short-bow", 150);
    const sword = await addEquipment(user.userId, "iron-sword", 150); // 位置0.5
    const request = (materialIds: string[]) => ({
      requestId: requestIds.first,
      category: "weapon",
      id: "flame-dagger",
      materialIds,
    });
    for (const materialIds of [
      [worn?.id as string],
      [bow],
      [],
      [sword, bow],
      [crypto.randomUUID()],
    ]) {
      expect(await craft(user.token, request(materialIds), 409)).toEqual({
        error: "invalid_materials",
      });
    }
    await scenario.setRunes(user.userId, 1000);
    expect(await scenario.runesOf(user.token)).toBe(1000);

    // 位置0.5を、炎のダガーの幅80に当てて140。
    const made = await craft(user.token, request([sword]));
    expect(made.craft).toMatchObject({
      category: "weapon",
      id: "flame-dagger",
      multiplier: 1.4,
      quality: "B",
      runes: 800,
      balanceAfter: 200,
      materials: [sword],
    });
    const items = (
      await scenario.read<{
        items: { id: string; equipmentId: string; multiplier: number }[];
      }>("/equipment", user.token)
    ).items;
    expect(items.map((item) => item.id)).not.toContain(sword);
    expect(items).toContainEqual(
      expect.objectContaining({
        id: made.craft?.itemId,
        equipmentId: "flame-dagger",
        multiplier: 1.4,
      }),
    );
    expect(await itemCountsOf(user.userId)).toEqual({});
    // まだ手に入れていなかった物も作れ、作ると見つけた記録に載る。
    expect(await discovered(user.userId, "weapon", "flame-dagger")).toBe(true);
  });

  it("特別なスキルを使うレシピは、デッキに入れている物を使えず、ルーン不足では材料も残高も変えない", async () => {
    const user = await start("craft-skill", 100);
    // リザレクション（☆4）：ヒール×1（特別）・聖水×2・墓前のろうそく×2。
    await addItems(user.userId, { "holy-water": 2, "grave-candle": 2 });
    const decked = await scenario.env.DB.prepare(
      "SELECT c.card_id AS id FROM party_character_cards c JOIN skill_cards s ON s.id = c.card_id WHERE c.user_id = ? AND s.skill_id = 'Heal'",
    )
      .bind(user.userId)
      .first<{ id: string }>();
    const heal = await addSkillCard(user.userId, "Heal", 250); // 上限250の位置1
    const body = (materialIds: string[]) => ({
      requestId: requestIds.first,
      category: "skill",
      id: "Resurrection",
      materialIds,
    });
    await scenario.setRunes(user.userId, 1000);
    expect(await craft(user.token, body([decked?.id as string]), 409)).toEqual({
      error: "invalid_materials",
    });

    // 費用800に、ルーンが足りない。断った要求IDも、あとで使える。
    await scenario.setRunes(user.userId, 799);
    expect(await craft(user.token, body([heal]), 409)).toEqual({
      error: "insufficient_runes",
    });
    expect(await scenario.runesOf(user.token)).toBe(799);
    expect(await skillCardsOf(user.userId, [heal])).toHaveLength(1);
    expect(await itemCountsOf(user.userId)).toEqual({
      "holy-water": 2,
      "grave-candle": 2,
    });

    await scenario.setRunes(user.userId, 800);
    const made = await craft(user.token, body([heal]));
    expect(made.craft).toMatchObject({
      category: "skill",
      id: "Resurrection",
      quality: "S",
      runes: 800,
      balanceAfter: 0,
    });
    expect(await skillCardsOf(user.userId, [heal])).toEqual([]);
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
    expect(await scenario.runesOf(user.token)).toBe(1325);
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
    expect(await scenario.runesOf(user.token)).toBe(1325);
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
    expect(await scenario.runesOf(user.token)).toBe(1325);
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
    expect(await scenario.runesOf(user.token)).toBe(100);
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
    expect(await scenario.runesOf(user.token)).toBe(10);
  });
});
