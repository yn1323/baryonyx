import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  type ApiScenario,
  createApiScenario,
} from "../../../tests/support/api-scenario.js";
import {
  type EquipmentId,
  maxMultiplierPercent as equipmentMaxPercent,
  slotOf,
} from "../equipment/catalog.js";
import {
  type CardSkillId,
  maxMultiplierPercent as skillMaxPercent,
  uniqueMaxPercent,
} from "../party/catalog.js";
import { AWAKENING_RUNES, POLISHES, polishStep } from "./catalog.js";

type Materials = { id: string; count: number }[];
type Unique = {
  index: number;
  set: number;
  acquired: number[];
  multiplier: number;
  quality: string;
};
type Party = {
  characters: { id: string; level: number; uniques: Unique[] }[];
  skillCards: { id: string; skillId: string; multiplier: number }[];
  runes: number;
};
type Polished = {
  enhancement?: {
    kind: string;
    category: string;
    target: string;
    materialId: string;
    count: number;
    before: { multiplier: number; quality: string };
    multiplier: number;
    quality: string;
    runes: number;
    balanceAfter: number;
  };
  materials?: Materials;
  error?: string;
};
type Awakened = {
  awakening?: {
    characterId: string;
    index: number;
    stage: number;
    runes: number;
    balanceAfter: number;
  };
  unique?: Unique;
  materials?: Materials;
  error?: string;
};

const requestId = (n: number) =>
  `eeeeeeee-${String(n).padStart(4, "0")}-4eee-8eee-eeeeeeeeeeee`;

describe("強化API", () => {
  let scenario: ApiScenario;

  beforeAll(async () => {
    scenario = await createApiScenario();
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  const post = async <T>(
    path: string,
    token: string,
    body: unknown,
    expected = 200,
  ) => {
    const response = await scenario.request(path, "POST", body, token);
    expect(response.status).toBe(expected);
    return (await response.json()) as T;
  };
  const polish = (token: string, body: unknown, expected = 200) =>
    post<Polished>("/enhancement/polish", token, body, expected);
  const awaken = (token: string, body: unknown, expected = 200) =>
    post<Awakened>("/enhancement/awaken", token, body, expected);

  const materialsOf = async (token: string) =>
    (await scenario.read<{ materials: Materials }>("/enhancement", token))
      .materials;
  const countOf = async (token: string, id: string) =>
    (await materialsOf(token)).find((entry) => entry.id === id)?.count;
  const runesOf = async (token: string) =>
    (await scenario.read<Party>("/party", token)).runes;
  const uniqueOf = async (token: string, characterId: string, index: number) =>
    (await scenario.read<Party>("/party", token)).characters.find(
      (character) => character.id === characterId,
    )?.uniques[index];

  // 強化素材を直接持たせる。冒険で集める手順を省くために使う。
  const give = async (userId: string, materials: Record<string, number>) => {
    const now = new Date().toISOString();
    for (const [id, count] of Object.entries(materials)) {
      await scenario.env.DB.prepare(
        "INSERT INTO enhancement_materials (user_id, material_id, count, acquired_at, updated_at) VALUES (?, ?, ?, ?, ?) ON CONFLICT(user_id, material_id) DO UPDATE SET count = excluded.count",
      )
        .bind(userId, id, count, now, now)
        .run();
    }
  };

  it("セッションのない要求を401で、不正な入力を400で拒否する", async () => {
    expect((await scenario.request("/enhancement", "GET")).status).toBe(401);
    expect(
      (
        await scenario.request("/enhancement/polish", "POST", {
          requestId: requestId(1),
          category: "skill",
          itemId: "x",
          materialId: "polish-white",
          count: 1,
        })
      ).status,
    ).toBe(401);

    const user = await scenario.login("enhancement-invalid");
    for (const body of [
      {
        requestId: "short",
        category: "skill",
        itemId: "x",
        materialId: "polish-white",
        count: 1,
      },
      // 知らない分類は品質強化の対象にしない。
      {
        requestId: requestId(1),
        category: "unknown",
        itemId: "luck",
        materialId: "polish-white",
        count: 1,
      },
      // 覚醒の欠片は品質強化に使えない。
      {
        requestId: requestId(1),
        category: "skill",
        itemId: "x",
        materialId: "awakening-shard",
        count: 1,
      },
      {
        requestId: requestId(1),
        category: "skill",
        itemId: "x",
        materialId: "polish-white",
        count: 0,
      },
      {
        requestId: requestId(1),
        category: "unique",
        characterId: "toma",
        index: 3,
        materialId: "polish-white",
        count: 1,
      },
    ]) {
      await polish(user.token, body, 400);
    }
    for (const body of [
      { requestId: requestId(1), characterId: "toma", index: 0, stage: 0 },
      { requestId: requestId(1), characterId: "toma", index: 0, stage: 7 },
      { requestId: requestId(1), characterId: "nobody", index: 0, stage: 1 },
    ]) {
      await awaken(user.token, body, 400);
    }
  });

  it("初めは強化素材をどれも0個持つ", async () => {
    const user = await scenario.login("enhancement-empty");
    expect(await materialsOf(user.token)).toEqual([
      { id: "polish-white", count: 0 },
      { id: "polish-blue", count: 0 },
      { id: "polish-purple", count: 0 },
      { id: "polish-gold", count: 0 },
      { id: "awakening-shard", count: 0 },
    ]);
  });

  it("スキルの倍率を磨き石とルーンで上げ、再送では二重に使わず、足りない・使い回しを拒む", async () => {
    const user = await scenario.login("enhancement-skill");
    const card = (await scenario.read<Party>("/party", user.token))
      .skillCards[0];
    const maxPercent = skillMaxPercent(card.skillId as CardSkillId);
    const step = polishStep("polish-white", maxPercent);
    await give(user.userId, { "polish-white": 3 });
    await scenario.setRunes(user.userId, 1000);

    const body = {
      requestId: requestId(1),
      category: "skill",
      itemId: card.id,
      materialId: "polish-white",
      count: 2,
    };
    const first = await polish(user.token, body);
    expect(first.enhancement).toEqual({
      kind: "polish",
      category: "skill",
      target: card.id,
      materialId: "polish-white",
      count: 2,
      before: { multiplier: 1, quality: "C" },
      multiplier: (100 + step * 2) / 100,
      quality: "C",
      runes: POLISHES["polish-white"].runes * 2,
      balanceAfter: 800,
    });
    expect(
      first.materials?.find((entry) => entry.id === "polish-white")?.count,
    ).toBe(1);
    const after = (
      await scenario.read<Party>("/party", user.token)
    ).skillCards.find((entry) => entry.id === card.id);
    expect(after?.multiplier).toBe((100 + step * 2) / 100);

    // 同じ要求の再送は、記録した結果を返し、もう一度は使わない。
    const replay = await polish(user.token, body);
    expect(replay.enhancement).toEqual(first.enhancement);
    expect(await runesOf(user.token)).toBe(800);
    expect(await countOf(user.token, "polish-white")).toBe(1);

    // 同じ要求IDで別の内容は拒む。
    expect((await polish(user.token, { ...body, count: 1 }, 409)).error).toBe(
      "request_conflict",
    );
    // 持っている数より多くは使えない。
    expect(
      (
        await polish(
          user.token,
          { ...body, requestId: requestId(2), count: 2 },
          409,
        )
      ).error,
    ).toBe("insufficient_materials");
    // ルーンが足りなければ使えない。
    await scenario.setRunes(user.userId, 50);
    expect(
      (
        await polish(
          user.token,
          { ...body, requestId: requestId(3), count: 1 },
          409,
        )
      ).error,
    ).toBe("insufficient_runes");
    expect(await countOf(user.token, "polish-white")).toBe(1);
    // 持っていないスキルは扱わない。
    expect(
      (
        await polish(
          user.token,
          { ...body, requestId: requestId(4), itemId: "missing" },
          404,
        )
      ).error,
    ).toBe("target_not_found");
  });

  it("装備は上限で止め、最後の1つが何も上げない数と、上限に届いた物は受け付けない", async () => {
    const user = await scenario.login("enhancement-equipment");
    const items = (
      await scenario.read<{
        items: { id: string; equipmentId: string; multiplier: number }[];
      }>("/equipment", user.token)
    ).items;
    const item = items[0];
    const category = slotOf(item.equipmentId) as string;
    const maxPercent = equipmentMaxPercent(item.equipmentId as EquipmentId);
    await give(user.userId, { "polish-gold": 4 });
    await scenario.setRunes(user.userId, 100_000);

    const body = {
      category,
      itemId: item.id,
      materialId: "polish-gold",
    };
    // 金は幅の50%を上げるため、×1.00からは2つで上限に届く。3つ目は何も上げない。
    expect(
      (
        await polish(
          user.token,
          { ...body, requestId: requestId(1), count: 3 },
          409,
        )
      ).error,
    ).toBe("too_many_materials");
    const maxed = await polish(user.token, {
      ...body,
      requestId: requestId(2),
      count: 2,
    });
    expect(maxed.enhancement).toMatchObject({
      multiplier: maxPercent / 100,
      quality: "S",
    });
    expect(
      (
        await polish(
          user.token,
          { ...body, requestId: requestId(3), count: 1 },
          409,
        )
      ).error,
    ).toBe("already_max");
    // 分類が違えば、その持ち物は扱わない。
    const other = category === "weapon" ? "armor" : "weapon";
    expect(
      (
        await polish(
          user.token,
          { ...body, category: other, requestId: requestId(4), count: 1 },
          404,
        )
      ).error,
    ).toBe("target_not_found");
    expect(await countOf(user.token, "polish-gold")).toBe(2);
  });

  it("固有スキルの倍率を上げ、編成の応答に倍率と品質を出す。解放前の固有スキルは扱わない", async () => {
    const user = await scenario.login("enhancement-unique");
    await scenario.read("/party", user.token);
    await give(user.userId, { "polish-purple": 2 });
    await scenario.setRunes(user.userId, 10_000);
    const maxPercent = uniqueMaxPercent("toma", 0);
    const step = polishStep("polish-purple", maxPercent);

    const result = await polish(user.token, {
      requestId: requestId(1),
      category: "unique",
      characterId: "toma",
      index: 0,
      materialId: "polish-purple",
      count: 2,
    });
    expect(result.enhancement).toMatchObject({
      category: "unique",
      target: "toma:0",
      multiplier: (100 + step * 2) / 100,
      quality: "B",
    });
    expect(await uniqueOf(user.token, "toma", 0)).toEqual({
      index: 0,
      set: 0,
      acquired: [0],
      multiplier: (100 + step * 2) / 100,
      quality: "B",
    });

    await give(user.userId, { "polish-purple": 1 });
    // トーマ（Lv12）の3つ目の固有スキルはLv30で解放する。
    expect(
      (
        await polish(
          user.token,
          {
            requestId: requestId(2),
            category: "unique",
            characterId: "toma",
            index: 2,
            materialId: "polish-purple",
            count: 1,
          },
          409,
        )
      ).error,
    ).toBe("unique_locked");
  });

  it("覚醒の欠片とルーンで、セットしている段階によらず、手前を取得した未取得の段階へ1段進める", async () => {
    const user = await scenario.login("enhancement-awaken");
    await scenario.read("/party", user.token);
    await give(user.userId, { "awakening-shard": 3 });
    await scenario.setRunes(user.userId, 3500);

    const first = await awaken(user.token, {
      requestId: requestId(1),
      characterId: "toma",
      index: 0,
      stage: 1,
    });
    expect(first.awakening).toEqual({
      characterId: "toma",
      index: 0,
      stage: 1,
      runes: AWAKENING_RUNES.first,
      balanceAfter: 3500 - AWAKENING_RUNES.first,
    });
    expect(first.unique).toMatchObject({ set: 1, acquired: [0, 1] });
    expect(
      first.materials?.find((entry) => entry.id === "awakening-shard")?.count,
    ).toBe(2);

    // 再送は、記録した結果を返し、もう一度は使わない。
    const replay = await awaken(user.token, {
      requestId: requestId(1),
      characterId: "toma",
      index: 0,
      stage: 1,
    });
    expect(replay.awakening).toEqual(first.awakening);
    expect(await countOf(user.token, "awakening-shard")).toBe(2);

    // 取得済みの段階と、手前を取得していない段階は選べない。
    expect(
      (
        await awaken(
          user.token,
          { requestId: requestId(2), characterId: "toma", index: 0, stage: 1 },
          409,
        )
      ).error,
    ).toBe("stage_acquired");
    expect(
      (
        await awaken(
          user.token,
          { requestId: requestId(3), characterId: "toma", index: 0, stage: 4 },
          409,
        )
      ).error,
    ).toBe("parent_not_acquired");

    // セットしている段階（1）の次でなくても、最初の段階から別の枝の1段目へ進められる。
    const branch = await awaken(user.token, {
      requestId: requestId(4),
      characterId: "toma",
      index: 0,
      stage: 3,
    });
    expect(branch.unique).toMatchObject({ set: 3, acquired: [0, 1, 3] });

    // 2段目は払うルーンが多い。残り1,500では足りない。
    expect(
      (
        await awaken(
          user.token,
          { requestId: requestId(5), characterId: "toma", index: 0, stage: 2 },
          409,
        )
      ).error,
    ).toBe("insufficient_runes");
    await scenario.setRunes(user.userId, AWAKENING_RUNES.second);
    const second = await awaken(user.token, {
      requestId: requestId(6),
      characterId: "toma",
      index: 0,
      stage: 2,
    });
    expect(second.awakening?.runes).toBe(AWAKENING_RUNES.second);
    expect(await uniqueOf(user.token, "toma", 0)).toMatchObject({
      set: 2,
      acquired: [0, 1, 2, 3],
    });

    // 欠片がなくなれば覚醒できない。
    expect(
      (
        await awaken(
          user.token,
          { requestId: requestId(7), characterId: "toma", index: 0, stage: 5 },
          409,
        )
      ).error,
    ).toBe("insufficient_materials");
  });
});
