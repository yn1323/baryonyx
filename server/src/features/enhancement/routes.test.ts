import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  type ApiScenario,
  createApiScenario,
} from "../../../tests/support/api-scenario.js";
import { percentRange, qualityOf } from "../../shared/quality.js";
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
import {
  AWAKENING_RUNES,
  POLISH_RUNES_PER_STONE,
  polishStep,
  SKILL_AWAKENING_RUNES,
} from "./catalog.js";

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
  skillCards: {
    id: string;
    skillId: string;
    multiplier: number;
    quality: string;
    awakenedBranch: number | null;
  }[];
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
type CardAwakened = {
  awakening?: {
    cardId: string;
    branch: number;
    runes: number;
    balanceAfter: number;
  };
  card?: Party["skillCards"][number];
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

  // 倍率を直接書き換える。品質の境目の前後から強化する例を作るために使う。
  const setSkillPercent = (cardId: string, percent: number) =>
    scenario.env.DB.prepare(
      "UPDATE skill_cards SET multiplier_percent = ? WHERE id = ?",
    )
      .bind(percent, cardId)
      .run();

  // 段階ごとの石の数（C=1・B=2・A=3・S=6）を、仕様のとおりに数え直す。
  const STONES = { C: 1, B: 2, A: 3, S: 6 } as const;
  const expectedPlan = (percent: number, steps: number, maxPercent: number) => {
    const step = polishStep(maxPercent);
    let after = percent;
    let stones = 0;
    for (let index = 0; index < steps; index++) {
      stones += STONES[qualityOf(after, maxPercent)];
      after = Math.min(maxPercent, after + step);
    }
    return { after, stones };
  };

  it("セッションのない要求を401で、不正な入力を400で拒否する", async () => {
    expect((await scenario.request("/enhancement", "GET")).status).toBe(401);
    expect(
      (
        await scenario.request("/enhancement/polish", "POST", {
          requestId: requestId(1),
          category: "skill",
          itemId: "x",
          steps: 1,
        })
      ).status,
    ).toBe(401);

    const user = await scenario.login("enhancement-invalid");
    for (const body of [
      { requestId: "short", category: "skill", itemId: "x", steps: 1 },
      // 知らない分類は品質強化の対象にしない。
      {
        requestId: requestId(1),
        category: "unknown",
        itemId: "luck",
        steps: 1,
      },
      { requestId: requestId(1), category: "skill", itemId: "x", steps: 0 },
      { requestId: requestId(1), category: "skill", itemId: "x", steps: 100 },
      { requestId: requestId(1), category: "skill", itemId: "x", steps: 1.5 },
      // 石の種類と数を指す以前の形は受け付けない。
      {
        requestId: requestId(1),
        category: "skill",
        itemId: "x",
        materialId: "polish-white",
        count: 1,
      },
      {
        requestId: requestId(1),
        category: "unique",
        characterId: "toma",
        index: 3,
        steps: 1,
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
    // 磨き石の合成はなくした。
    expect(
      (
        await scenario.request(
          "/enhancement/combine",
          "POST",
          { requestId: requestId(1), materialId: "polish-blue", count: 1 },
          user.token,
        )
      ).status,
    ).toBe(404);
  });

  it("初めは強化素材をどれも0個持つ", async () => {
    const user = await scenario.login("enhancement-empty");
    expect(await materialsOf(user.token)).toEqual([
      { id: "polish-white", count: 0 },
      { id: "awakening-shard", count: 0 },
    ]);
  });

  it("スキルの倍率を段階で上げ、再送では二重に使わず、足りない・使い回しを拒む", async () => {
    const user = await scenario.login("enhancement-skill");
    const card = (await scenario.read<Party>("/party", user.token))
      .skillCards[0];
    const maxPercent = skillMaxPercent(card.skillId as CardSkillId);
    const step = polishStep(maxPercent);
    await give(user.userId, { "polish-white": 3 });
    await scenario.setRunes(user.userId, 1000);

    // ×1.00（C）から2段階：どちらの段階もCのうちに始めるため、石は1個ずつ。
    const body = {
      requestId: requestId(1),
      category: "skill",
      itemId: card.id,
      steps: 2,
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
      runes: POLISH_RUNES_PER_STONE * 2,
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
    expect(await scenario.runesOf(user.token)).toBe(800);
    expect(await countOf(user.token, "polish-white")).toBe(1);

    // 同じ要求IDで別の段階の数は拒む。
    expect((await polish(user.token, { ...body, steps: 1 }, 409)).error).toBe(
      "request_conflict",
    );
    // 持っている数より多くは使えない。
    expect(
      (
        await polish(
          user.token,
          { ...body, requestId: requestId(2), steps: 2 },
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
          { ...body, requestId: requestId(3), steps: 1 },
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

  it("1段階の石の数は、その段階を始める前の品質で決まり、品質の帯をまたぐと途中で増える", async () => {
    const user = await scenario.login("enhancement-bands");
    const card = (await scenario.read<Party>("/party", user.token))
      .skillCards[0];
    const maxPercent = skillMaxPercent(card.skillId as CardSkillId);
    const step = polishStep(maxPercent);
    await give(user.userId, { "polish-white": 100 });
    await scenario.setRunes(user.userId, 100_000);
    const body = { category: "skill", itemId: card.id };

    // Bの入口の1つ手前（C）から2段階：1段目はCで1個、2段目はBに入ってから始めるため2個。
    const bMin = percentRange("B", maxPercent).min;
    await setSkillPercent(card.id, bMin - 1);
    const cToB = await polish(user.token, {
      ...body,
      requestId: requestId(1),
      steps: 2,
    });
    expect(cToB.enhancement).toMatchObject({
      materialId: "polish-white",
      count: 3,
      before: { quality: "C" },
      multiplier: (bMin - 1 + step * 2) / 100,
      quality: "B",
      runes: POLISH_RUNES_PER_STONE * 3,
    });
    expect(await countOf(user.token, "polish-white")).toBe(97);

    // Aの入口の1つ手前（B）から2段階：2個と3個。
    const aMin = percentRange("A", maxPercent).min;
    await setSkillPercent(card.id, aMin - 1);
    const bToA = await polish(user.token, {
      ...body,
      requestId: requestId(2),
      steps: 2,
    });
    expect(bToA.enhancement).toMatchObject({
      count: 5,
      before: { quality: "B" },
      quality: "A",
      runes: POLISH_RUNES_PER_STONE * 5,
    });

    // Sの入口（S）から1段階：6個。
    const sMin = percentRange("S", maxPercent).min;
    await setSkillPercent(card.id, sMin);
    const inS = await polish(user.token, {
      ...body,
      requestId: requestId(3),
      steps: 1,
    });
    expect(inS.enhancement).toMatchObject({
      count: 6,
      before: { quality: "S" },
      multiplier: Math.min(maxPercent, sMin + step) / 100,
      quality: "S",
      runes: POLISH_RUNES_PER_STONE * 6,
    });
    expect(await countOf(user.token, "polish-white")).toBe(97 - 5 - 6);
  });

  it("最後の段階は種類の上限で止め、最後の段階が何も上げない数と、上限に届いた物は受け付けない", async () => {
    const user = await scenario.login("enhancement-equipment");
    const items = (
      await scenario.read<{
        items: { id: string; equipmentId: string; multiplier: number }[];
      }>("/equipment", user.token)
    ).items;
    const item = items[0];
    const category = slotOf(item.equipmentId) as string;
    const maxPercent = equipmentMaxPercent(item.equipmentId as EquipmentId);
    const step = polishStep(maxPercent);
    // ×1.00から上限に届く段階の数。最後の段階は上限で止まるため、端数でも1段階で届く。
    const toMax = Math.ceil((maxPercent - 100) / step);
    const full = expectedPlan(100, toMax, maxPercent);
    expect(full.after).toBe(maxPercent);
    await give(user.userId, { "polish-white": full.stones + 10 });
    await scenario.setRunes(user.userId, 100_000);

    const body = { category, itemId: item.id };
    // 上限に届く数より1つ多いと、最後の段階が何も上げない。石もルーンも使わない。
    expect(
      (
        await polish(
          user.token,
          { ...body, requestId: requestId(1), steps: toMax + 1 },
          409,
        )
      ).error,
    ).toBe("too_many_steps");
    expect(await countOf(user.token, "polish-white")).toBe(full.stones + 10);
    expect(await scenario.runesOf(user.token)).toBe(100_000);

    const maxed = await polish(user.token, {
      ...body,
      requestId: requestId(2),
      steps: toMax,
    });
    expect(maxed.enhancement).toMatchObject({
      count: full.stones,
      before: { multiplier: 1, quality: "C" },
      multiplier: maxPercent / 100,
      quality: "S",
      runes: POLISH_RUNES_PER_STONE * full.stones,
      balanceAfter: 100_000 - POLISH_RUNES_PER_STONE * full.stones,
    });
    // 同じ要求の再送は、上限に届いたあとでも記録した結果を返す。
    const replay = await polish(user.token, {
      ...body,
      requestId: requestId(2),
      steps: toMax,
    });
    expect(replay.enhancement).toEqual(maxed.enhancement);
    expect(
      (
        await polish(
          user.token,
          { ...body, requestId: requestId(3), steps: 1 },
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
          { ...body, category: other, requestId: requestId(4), steps: 1 },
          404,
        )
      ).error,
    ).toBe("target_not_found");
    expect(await countOf(user.token, "polish-white")).toBe(10);
  });

  it("上限の手前で端数が残るときは、最後の段階を上限で止める", async () => {
    const user = await scenario.login("enhancement-cap");
    const card = (await scenario.read<Party>("/party", user.token))
      .skillCards[0];
    const maxPercent = skillMaxPercent(card.skillId as CardSkillId);
    await give(user.userId, { "polish-white": 20 });
    await scenario.setRunes(user.userId, 100_000);
    await setSkillPercent(card.id, maxPercent - 1);
    const body = { category: "skill", itemId: card.id };

    // 上限の1つ手前（S）からは、1段階で上限に届く。2段階は多すぎる。
    expect(
      (
        await polish(
          user.token,
          { ...body, requestId: requestId(1), steps: 2 },
          409,
        )
      ).error,
    ).toBe("too_many_steps");
    const capped = await polish(user.token, {
      ...body,
      requestId: requestId(2),
      steps: 1,
    });
    expect(capped.enhancement).toMatchObject({
      count: 6,
      multiplier: maxPercent / 100,
      quality: "S",
    });
    expect(await countOf(user.token, "polish-white")).toBe(14);
  });

  it("固有スキルの倍率を上げ、編成の応答に倍率と品質を出す。解放前の固有スキルは扱わない", async () => {
    const user = await scenario.login("enhancement-unique");
    await scenario.read("/party", user.token);
    await give(user.userId, { "polish-white": 2 });
    await scenario.setRunes(user.userId, 10_000);
    const maxPercent = uniqueMaxPercent("toma", 0);
    const step = polishStep(maxPercent);

    const result = await polish(user.token, {
      requestId: requestId(1),
      category: "unique",
      characterId: "toma",
      index: 0,
      steps: 2,
    });
    expect(result.enhancement).toMatchObject({
      category: "unique",
      target: "toma:0",
      count: 2,
      multiplier: (100 + step * 2) / 100,
      quality: "C",
    });
    expect(await uniqueOf(user.token, "toma", 0)).toEqual({
      index: 0,
      set: 0,
      acquired: [0],
      multiplier: (100 + step * 2) / 100,
      quality: "C",
    });

    await give(user.userId, { "polish-white": 1 });
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
            steps: 1,
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

  it("覚醒の欠片1個とルーンで、まだ覚醒していないスキル1枚を枝の1つへ覚醒させ、覚醒した1枚と欠片の不足を拒む", async () => {
    const user = await scenario.login("enhancement-card-awaken");
    const [target, other] = (await scenario.read<Party>("/party", user.token))
      .skillCards;
    await give(user.userId, { "awakening-shard": 1 });
    await scenario.setRunes(user.userId, 1500);
    const cardAwaken = (body: unknown, expected = 200) =>
      post<CardAwakened>("/enhancement/awaken", user.token, body, expected);
    const cardOf = async (cardId: string) =>
      (await scenario.read<Party>("/party", user.token)).skillCards.find(
        (card) => card.id === cardId,
      );

    // 持っていない1枚は404で拒否する。
    expect(
      (
        await cardAwaken(
          {
            requestId: requestId(1),
            cardId: "bbbbbbbb-1111-4111-8111-bbbbbbbbbbbb",
            branch: 1,
          },
          404,
        )
      ).error,
    ).toBe("target_not_found");

    const first = await cardAwaken({
      requestId: requestId(2),
      cardId: target.id,
      branch: 1,
    });
    expect(first.awakening).toEqual({
      cardId: target.id,
      branch: 1,
      runes: SKILL_AWAKENING_RUNES,
      balanceAfter: 1500 - SKILL_AWAKENING_RUNES,
    });
    expect(first.card).toEqual({ ...target, awakenedBranch: 1 });
    expect(
      first.materials?.find((entry) => entry.id === "awakening-shard")?.count,
    ).toBe(0);
    expect(await scenario.runesOf(user.token)).toBe(
      1500 - SKILL_AWAKENING_RUNES,
    );
    expect((await cardOf(target.id))?.awakenedBranch).toBe(1);

    // 再送は、記録した結果を返し、もう一度は使わない。中身の違う再送は受け付けない。
    const replay = await cardAwaken({
      requestId: requestId(2),
      cardId: target.id,
      branch: 1,
    });
    expect(replay.awakening).toEqual(first.awakening);
    expect(
      (
        await cardAwaken(
          { requestId: requestId(2), cardId: target.id, branch: 2 },
          409,
        )
      ).error,
    ).toBe("request_conflict");
    expect(await scenario.runesOf(user.token)).toBe(
      1500 - SKILL_AWAKENING_RUNES,
    );

    // 覚醒した1枚は、同じ枝へも別の枝へも覚醒しない。欠片もルーンも使わない。
    await give(user.userId, { "awakening-shard": 1 });
    await scenario.setRunes(user.userId, 1500);
    for (const branch of [1, 2]) {
      expect(
        (
          await cardAwaken(
            { requestId: requestId(2 + branch), cardId: target.id, branch },
            409,
          )
        ).error,
      ).toBe("already_awakened");
    }
    expect(await countOf(user.token, "awakening-shard")).toBe(1);
    expect(await scenario.runesOf(user.token)).toBe(1500);

    // 欠片がなければ覚醒できず、何も変えない。
    await give(user.userId, { "awakening-shard": 0 });
    expect(
      (
        await cardAwaken(
          { requestId: requestId(5), cardId: other.id, branch: 2 },
          409,
        )
      ).error,
    ).toBe("insufficient_materials");
    expect((await cardOf(other.id))?.awakenedBranch).toBeNull();
    expect(await scenario.runesOf(user.token)).toBe(1500);

    // 不正な枝は400で拒否する。
    expect(
      (
        await scenario.request(
          "/enhancement/awaken",
          "POST",
          { requestId: requestId(6), cardId: other.id, branch: 3 },
          user.token,
        )
      ).status,
    ).toBe(400);
  });
});
