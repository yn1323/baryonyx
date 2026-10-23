import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  buildSeedStatements,
  SEED_PLAYERS,
  seedGuestSecret,
} from "../../scripts/seed.js";
import { slotOf } from "../../src/features/equipment/catalog.js";
import {
  CARD_SLOT_COUNT,
  type CharacterId,
  levelUpCost,
  UNIQUE_SKILL_COUNT,
  UNIQUE_UNLOCK_LEVELS,
  uniqueMaxPercent,
} from "../../src/features/party/catalog.js";
import { qualityOf } from "../../src/shared/quality.js";
import {
  type ApiScenario,
  createApiScenario,
} from "../support/api-scenario.js";

type Unique = {
  index: number;
  set: number;
  acquired: number[];
  multiplier: number;
  quality: string;
};
type Party = {
  characters: {
    id: string;
    level: number;
    cards: (string | null)[];
    uniques: Unique[];
    bond: number;
    tokens: number;
  }[];
  skillCards: { id: string; skillId: string; multiplier: number }[];
  items: { itemId: string; count: number }[];
  slots: { slot: number; characterId: string | null }[];
  runes: number;
};
type Equipment = {
  items: {
    id: string;
    equipmentId: string;
    multiplier: number;
    quality: string;
  }[];
  characters: { id: string; weapon: string | null; armor: string | null }[];
};
type StepBonus = {
  holdings: { bonusId: string; multiplier: number; quality: string }[];
  slots: { slot: number; bonusId: string | null }[];
  locked: boolean;
};
type Weaknesses = {
  weaknesses: { enemyId: string; element: string }[];
};
type Library = {
  discoveries: { kind: string; entryId: string; defeated: number }[];
};
type Adventure = {
  run: { destinationId: string; seed: number; roomId: string } | null;
  records: { destinationId: string; bestFloor: number; clears: number }[];
  runes: number;
};

const player = (id: string) => {
  const found = SEED_PLAYERS.find((entry) => entry.id === id);
  if (!found) throw new Error(id);
  return found;
};

// seedのSQLがいまのマイグレーションに入り、APIからseedどおりに読み書きできることを確かめる。
describe("開発用のプレイヤー（seed）", () => {
  let scenario: ApiScenario;

  beforeAll(async () => {
    scenario = await createApiScenario();
    const db = scenario.env.DB;
    await db.batch(
      buildSeedStatements("2026-10-01T00:00:00.000Z").map((statement) =>
        db.prepare(statement),
      ),
    );
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  async function signIn(id: string) {
    const response = await scenario.request("/auth/guest", "POST", {
      secret: seedGuestSecret(id),
    });
    expect(response.status).toBe(200);
    return ((await response.json()) as { token: string }).token;
  }

  it("どのプレイヤーも、seedの持ち物・編成・枠・記録をそのまま読める", async () => {
    for (const seed of SEED_PLAYERS) {
      const token = await signIn(seed.id);
      const party = await scenario.read<Party>("/party", token);
      expect(party.runes).toBe(seed.runes);
      // 枠は持っているスキルの1枚（card:<ID>）を指す。指した1枚のスキルがseedのカードになる。
      const skillOf = (key: string | null) =>
        party.skillCards.find((card) => `card:${card.id}` === key)?.skillId ??
        null;
      expect(
        party.characters.map((character) => ({
          ...character,
          cards: character.cards.map(skillOf),
        })),
      ).toEqual(
        seed.characters.map((character) => ({
          id: character.id,
          level: character.level,
          // 書かなかった後ろの枠は空き（null）として読める。
          cards: Array.from(
            { length: CARD_SLOT_COUNT },
            (_, slot) => character.cards[slot] ?? null,
          ),
          // 固有スキルの段階。seedに書いていない固有スキルは最初の段階だけを持つ。
          uniques: Array.from(
            { length: UNIQUE_SKILL_COUNT },
            (_, index): Unique => {
              const unique = character.uniques?.[index];
              const percent = unique?.multiplierPercent ?? 100;
              return {
                index,
                set: unique?.set ?? 0,
                acquired: unique
                  ? [...unique.acquired].sort((a, b) => a - b)
                  : [0],
                multiplier: percent / 100,
                quality: qualityOf(
                  percent,
                  uniqueMaxPercent(character.id as CharacterId, index),
                ),
              };
            },
          ),
          // 絆の段としるし。seedに書いていないキャラは0。
          bond: seed.bonds?.find((bond) => bond.id === character.id)?.bond ?? 0,
          tokens:
            seed.bonds?.find((bond) => bond.id === character.id)?.tokens ?? 0,
        })),
      );
      // 段階を書いた固有スキルは、解放するLvに届いている。
      for (const character of seed.characters) {
        (character.uniques ?? []).forEach((unique, index) => {
          if (unique) {
            expect(character.level).toBeGreaterThanOrEqual(
              UNIQUE_UNLOCK_LEVELS[index],
            );
          }
        });
      }
      expect(party.skillCards).toHaveLength(
        seed.characters.reduce(
          (total, character) => total + character.cards.length,
          0,
        ) + (seed.skillCards?.length ?? 0),
      );
      expect(party.items).toEqual(
        (seed.itemCounts ?? []).map((item) => ({
          itemId: item.id,
          count: item.count,
        })),
      );
      expect(party.slots.map((slot) => slot.characterId)).toEqual(seed.party);
      // 強化素材は、seedに書いた数を、持っていない種類は0で読める。
      const materials = await scenario.read<{
        materials: { id: string; count: number }[];
      }>("/enhancement", token);
      for (const material of materials.materials) {
        expect(material.count).toBe(
          seed.materials?.find((entry) => entry.id === material.id)?.count ?? 0,
        );
      }

      // 絆としるし、かけらも、seedに書いた値で読める。
      const summon = await scenario.read<{
        bonds: { characterId: string; bond: number; tokens: number }[];
        shards: number;
      }>("/summon", token);
      expect(summon.bonds).toEqual(
        [...(seed.bonds ?? [])]
          .sort((a, b) => a.id.localeCompare(b.id))
          .map((bond) => ({
            characterId: bond.id,
            bond: bond.bond,
            tokens: bond.tokens ?? 0,
          })),
      );
      expect(summon.shards).toBe(seed.shards ?? 0);

      const equipment = await scenario.read<Equipment>("/equipment", token);
      expect(equipment.items.map((item) => item.equipmentId).sort()).toEqual(
        [...seed.items].sort(),
      );
      // 装備ごとの倍率（品質）も、そのまま読める。
      const multipliers = (entries: { id: string; multiplier: number }[]) =>
        entries.map((entry) => `${entry.id}:${entry.multiplier}`).sort();
      expect(
        multipliers(
          equipment.items.map((item) => ({
            id: item.equipmentId,
            multiplier: item.multiplier,
          })),
        ),
      ).toEqual(
        multipliers(
          seed.items.map((id, index) => ({
            id,
            multiplier: (seed.itemMultipliers?.[index] ?? 100) / 100,
          })),
        ),
      );
      const kind = (itemId: string | null) =>
        equipment.items.find((item) => item.id === itemId)?.equipmentId ?? null;
      expect(
        equipment.characters.map((character) => ({
          id: character.id,
          weapon: kind(character.weapon),
          armor: kind(character.armor),
        })),
      ).toEqual(
        seed.characters.map((character) => ({
          id: character.id,
          weapon: character.weapon ?? null,
          armor: character.armor ?? null,
        })),
      );

      const bonus = await scenario.read<StepBonus>("/step-bonus", token);
      expect(bonus.holdings).toEqual(
        expect.arrayContaining(
          seed.bonuses.map((entry) =>
            expect.objectContaining({
              bonusId: entry.id,
              multiplier: entry.multiplierPercent / 100,
            }),
          ),
        ),
      );
      expect(bonus.holdings).toHaveLength(seed.bonuses.length);
      expect(bonus.slots.map((slot) => slot.bonusId)).toEqual(seed.bonusSlots);
      expect(bonus.locked).toBe(seed.adventure !== undefined);

      const adventure = await scenario.read<Adventure>("/adventure", token);
      expect(adventure.records).toEqual(seed.records);
      expect(adventure.run?.destinationId).toBe(seed.adventure?.destinationId);
      expect(adventure.run?.seed).toBe(seed.adventure?.seed);

      const revealed = await scenario.read<Weaknesses>(
        "/combat/weaknesses",
        token,
      );
      expect(
        revealed.weaknesses.map(({ enemyId, element }) => ({
          enemyId,
          element,
        })),
      ).toEqual(expect.arrayContaining([...seed.weaknesses]));
      expect(revealed.weaknesses).toHaveLength(seed.weaknesses.length);

      const library = await scenario.read<Library>("/library", token);
      const entries = (kind: string) =>
        library.discoveries
          .filter((entry) => entry.kind === kind)
          .map((entry) => entry.entryId)
          .sort();
      const unique = (ids: readonly string[]) => [...new Set(ids)].sort();
      expect(entries("character")).toEqual(
        unique(seed.characters.map((character) => character.id)),
      );
      expect(entries("skill")).toEqual(
        unique([
          ...seed.characters.flatMap((character) => character.cards),
          ...(seed.skillCards ?? []).map((card) => card.id),
        ]),
      );
      expect(entries("item")).toEqual(
        unique((seed.itemCounts ?? []).map((item) => item.id)),
      );
      for (const slot of ["weapon", "armor", "accessory"] as const) {
        expect(entries(slot)).toEqual(
          unique(seed.items.filter((id) => slotOf(id) === slot)),
        );
      }
      expect(
        library.discoveries
          .filter((entry) => entry.kind === "enemy")
          .map(({ entryId, defeated }) => ({ enemyId: entryId, defeated })),
      ).toEqual(expect.arrayContaining([...seed.enemies]));
      expect(entries("enemy")).toHaveLength(seed.enemies.length);
    }
  });

  it("始めたばかりのプレイヤーは、Lv2までは上げられ、その先はルーンが足りない", async () => {
    const token = await signIn("newcomer");
    const levelUp = (requestId: string, fromLevel: number) =>
      scenario.request(
        "/party/characters/toma/level-up",
        "POST",
        { requestId, fromLevel, toLevel: fromLevel + 1 },
        token,
      );
    const first = await levelUp("bbbbbbbb-1111-4111-8111-bbbbbbbbbbbb", 1);
    expect(first.status).toBe(200);
    expect(((await first.json()) as Party).runes).toBe(
      player("newcomer").runes - levelUpCost(1, 2),
    );
    const second = await levelUp("bbbbbbbb-2222-4222-8222-bbbbbbbbbbbb", 2);
    expect(second.status).toBe(409);
    expect(await second.json()).toEqual({ error: "insufficient_runes" });
  });

  it("冒険の途中のプレイヤーは、入口から次の階へ進める", async () => {
    const token = await signIn("adventurer");
    const moved = await scenario.request(
      "/adventure/move",
      "POST",
      { roomId: "f2-0", floor: 2, kind: "battle" },
      token,
    );
    expect(moved.status).toBe(200);
    expect(((await moved.json()) as Adventure).run?.roomId).toBe("f2-0");
  });

  it("遊び込んだプレイヤーは、ほかの人の装備を付け替えられる", async () => {
    const token = await signIn("veteran");
    const before = await scenario.read<Equipment>("/equipment", token);
    const anselm = before.characters.find((entry) => entry.id === "anselm");
    const response = await scenario.request(
      "/equipment/characters/rita/weapon",
      "PUT",
      { itemId: anselm?.weapon },
      token,
    );
    expect(response.status).toBe(200);
    const after = (await response.json()) as Equipment;
    expect(after.characters.find((entry) => entry.id === "rita")?.weapon).toBe(
      anselm?.weapon,
    );
    expect(
      after.characters.find((entry) => entry.id === "anselm")?.weapon,
    ).toBeNull();
  });
});
