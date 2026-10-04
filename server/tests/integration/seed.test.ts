import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  buildSeedStatements,
  SEED_PLAYERS,
  seedGuestSecret,
} from "../../scripts/seed.js";
import { levelUpCost } from "../../src/features/party/catalog.js";
import {
  type ApiScenario,
  createApiScenario,
} from "../support/api-scenario.js";

type Party = {
  characters: { id: string; level: number; cards: (string | null)[] }[];
  slots: { slot: number; characterId: string | null }[];
  runes: number;
};
type Equipment = {
  items: { id: string; equipmentId: string }[];
  characters: { id: string; weapon: string | null; armor: string | null }[];
};
type StepBonus = {
  holdings: { bonusId: string; rank: string }[];
  slots: { slot: number; bonusId: string | null }[];
  locked: boolean;
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
      expect(party.characters).toEqual(
        seed.characters.map((character) => ({
          id: character.id,
          level: character.level,
          cards: [...character.cards],
        })),
      );
      expect(party.slots.map((slot) => slot.characterId)).toEqual(seed.party);

      const equipment = await scenario.read<Equipment>("/equipment", token);
      expect(equipment.items.map((item) => item.equipmentId).sort()).toEqual(
        [...seed.items].sort(),
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
            expect.objectContaining({ bonusId: entry.id, rank: entry.rank }),
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
