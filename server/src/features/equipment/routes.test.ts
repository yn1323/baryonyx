import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  type ApiScenario,
  createApiScenario,
} from "../../../tests/support/api-scenario.js";
import { CHARACTER_IDS } from "../party/catalog.js";
import { STARTER_EQUIPPED, STARTER_ITEMS } from "./catalog.js";

type State = {
  change?: string;
  from?: string;
  items: { id: string; equipmentId: string }[];
  characters: { id: string; weapon: string | null; armor: string | null }[];
};

const missingItem = "bbbbbbbb-1111-4111-8111-bbbbbbbbbbbb";

describe("装備API", () => {
  let scenario: ApiScenario;

  beforeAll(async () => {
    scenario = await createApiScenario();
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  const read = (token: string) => scenario.read<State>("/equipment", token);

  const put = (token: string, id: string, slot: string, itemId: string) =>
    scenario.request(
      `/equipment/characters/${id}/${slot}`,
      "PUT",
      { itemId },
      token,
    );
  const remove = (token: string, id: string, slot: string) =>
    scenario.request(
      `/equipment/characters/${id}/${slot}`,
      "DELETE",
      undefined,
      token,
    );

  const itemOf = (state: State, equipmentId: string) =>
    state.items.find((item) => item.equipmentId === equipmentId)?.id ?? "";
  const character = (state: State, id: string) =>
    state.characters.find((entry) => entry.id === id);
  // キャラの武器・防具を、装備の種類のIDで読む。
  const worn = (state: State, id: string) => {
    const entry = character(state, id);
    const kind = (itemId: string | null) =>
      state.items.find((item) => item.id === itemId)?.equipmentId ?? null;
    return {
      weapon: kind(entry?.weapon ?? null),
      armor: kind(entry?.armor ?? null),
    };
  };

  it("セッションのない要求を401で拒否する", async () => {
    const responses = await Promise.all([
      scenario.request("/equipment"),
      scenario.request("/equipment/characters/toma/weapon", "PUT", {
        itemId: missingItem,
      }),
      scenario.request("/equipment/characters/toma/weapon", "DELETE"),
    ]);
    expect(responses.map((response) => response.status)).toEqual([
      401, 401, 401,
    ]);
  });

  it("初めて読むユーザーに仮の装備と初めの4人の装備を1回だけ付与する", async () => {
    const user = await scenario.login("equipment-starter");
    // 同時に届いても、装備は1回分だけ入る。
    const [first, second] = await Promise.all([
      read(user.token),
      read(user.token),
    ]);
    expect(first.items.map((item) => item.equipmentId)).toEqual(STARTER_ITEMS);
    expect(new Set(first.items.map((item) => item.id)).size).toBe(
      STARTER_ITEMS.length,
    );
    expect(first.characters.map((entry) => entry.id)).toEqual(CHARACTER_IDS);
    for (const entry of STARTER_EQUIPPED) {
      expect(worn(first, entry.characterId)).toEqual({
        weapon: entry.weapon,
        armor: entry.armor,
      });
    }
    expect(character(first, "anselm")).toEqual({
      id: "anselm",
      weapon: null,
      armor: null,
    });
    expect(second).toEqual(first);
    expect(await read(user.token)).toEqual(first);
  });

  it("空いた枠に付け、付けている装備を替え、外す", async () => {
    const user = await scenario.login("equipment-change");
    const state = await read(user.token);
    const dagger = itemOf(state, "flame-dagger");
    const helm = itemOf(state, "iron-helm");

    const equipped = await put(user.token, "anselm", "weapon", dagger);
    expect(equipped.status).toBe(200);
    const afterEquip = (await equipped.json()) as State;
    expect(afterEquip.change).toBe("equip");
    expect(worn(afterEquip, "anselm").weapon).toBe("flame-dagger");

    const replaced = (await (
      await put(user.token, "toma", "armor", helm)
    ).json()) as State;
    expect(replaced.change).toBe("replace");
    expect(worn(replaced, "toma")).toEqual({
      weapon: "oak-staff",
      armor: "iron-helm",
    });
    // 外れた装備は持ち物に残る。
    expect(replaced.items).toHaveLength(STARTER_ITEMS.length);

    const same = (await (
      await put(user.token, "toma", "armor", helm)
    ).json()) as State;
    expect(same.change).toBe("none");

    const removed = await remove(user.token, "toma", "armor");
    expect(removed.status).toBe(200);
    const afterRemove = (await removed.json()) as State;
    expect(afterRemove.change).toBe("remove");
    expect(character(afterRemove, "toma")?.armor).toBeNull();
    const again = (await (
      await remove(user.token, "toma", "armor")
    ).json()) as State;
    expect(again.change).toBe("none");

    expect(await read(user.token)).toEqual({
      items: afterRemove.items,
      characters: afterRemove.characters,
    });
  });

  it("ほかのキャラが付けている装備は、そのキャラから外して付け替える", async () => {
    const user = await scenario.login("equipment-move");
    const state = await read(user.token);
    const sword = itemOf(state, "iron-sword");

    const moved = (await (
      await put(user.token, "toma", "weapon", sword)
    ).json()) as State;
    expect(moved.change).toBe("move");
    expect(moved.from).toBe("aria");
    expect(worn(moved, "toma").weapon).toBe("iron-sword");
    expect(character(moved, "aria")?.weapon).toBeNull();
    expect(
      moved.characters.filter((entry) => entry.weapon === sword),
    ).toHaveLength(1);
  });

  it("枠に合わない装備、持っていない装備、不正な入力を拒否する", async () => {
    const user = await scenario.login("equipment-invalid");
    const state = await read(user.token);
    const robe = itemOf(state, "magic-robe");

    const mismatch = await put(user.token, "toma", "weapon", robe);
    expect(mismatch.status).toBe(409);
    expect(await mismatch.json()).toEqual({ error: "slot_mismatch" });

    const missing = await put(user.token, "toma", "weapon", missingItem);
    expect(missing.status).toBe(404);
    expect(await missing.json()).toEqual({ error: "item_not_owned" });

    const invalid = await Promise.all([
      put(user.token, "toma", "ring", robe),
      put(user.token, "nobody", "armor", robe),
      put(user.token, "toma", "armor", "not-an-id"),
      remove(user.token, "toma", "ring"),
    ]);
    expect(invalid.map((response) => response.status)).toEqual([
      400, 400, 400, 400,
    ]);

    expect(await read(user.token)).toEqual(state);
  });

  it("ほかのユーザーの装備は付けられず、ユーザーごとに分かれる", async () => {
    const owner = await scenario.login("equipment-owner");
    const other = await scenario.login("equipment-other");
    const owned = await read(owner.token);
    const before = await read(other.token);

    const stolen = await put(
      other.token,
      "toma",
      "weapon",
      itemOf(owned, "flame-dagger"),
    );
    expect(stolen.status).toBe(404);
    expect(await stolen.json()).toEqual({ error: "item_not_owned" });

    await remove(owner.token, "toma", "weapon");
    expect(character(await read(other.token), "toma")?.weapon).toBe(
      character(before, "toma")?.weapon,
    );
    expect(
      before.items.some((item) =>
        owned.items.some((mine) => mine.id === item.id),
      ),
    ).toBe(false);
  });
});
