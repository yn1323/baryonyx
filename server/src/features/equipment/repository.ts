import { and, eq } from "drizzle-orm";
import type { BatchResponse } from "drizzle-orm/batch";
import {
  batchThenRead,
  createDatabase,
  type Database,
  type Statement,
} from "../../shared/db.js";
import { qualityFields } from "../../shared/quality.js";
import { partyCharacters } from "../party/db-schema.js";
import { ownedInOrder } from "../party/state.js";
import {
  EQUIPMENT_IDS,
  EQUIPMENT_SLOTS,
  type EquipmentId,
  type EquipmentSlot,
  kindOfSlot,
  maxMultiplierPercent,
  slotOf,
  unlockLevelOf,
} from "./catalog.js";
import { equipmentCharacterSlots, equipmentItems } from "./db-schema.js";

type EquipmentChange = "equip" | "replace" | "move" | "remove" | "none";

// 持っているキャラ（Lvと入手の日時）、持っている装備、キャラごとの装備を読む文。
function stateReads(db: Database, userId: string) {
  return [
    db
      .select({
        characterId: partyCharacters.characterId,
        level: partyCharacters.level,
        acquiredAt: partyCharacters.acquiredAt,
      })
      .from(partyCharacters)
      .where(eq(partyCharacters.userId, userId)),
    db
      .select({
        id: equipmentItems.id,
        equipmentId: equipmentItems.equipmentId,
        multiplierPercent: equipmentItems.multiplierPercent,
        acquiredAt: equipmentItems.acquiredAt,
      })
      .from(equipmentItems)
      .where(eq(equipmentItems.userId, userId)),
    db
      .select({
        characterId: equipmentCharacterSlots.characterId,
        slot: equipmentCharacterSlots.slot,
        itemId: equipmentCharacterSlots.itemId,
      })
      .from(equipmentCharacterSlots)
      .where(eq(equipmentCharacterSlots.userId, userId)),
  ] as const;
}

// 持っている装備と、持っているキャラ（パーティの一覧と同じ順）ごとの武器・防具・アクセサリー。
function stateOf([owned, items, slots]: BatchResponse<
  ReturnType<typeof stateReads>
>) {
  const order = (id: string) => EQUIPMENT_IDS.indexOf(id as EquipmentId);
  const worn = (characterId: string, slot: EquipmentSlot) =>
    slots.find((row) => row.characterId === characterId && row.slot === slot)
      ?.itemId ?? null;
  const characterIds = ownedInOrder(owned).map((row) => row.characterId);
  return {
    levels: new Map(owned.map((row) => [row.characterId, row.level])),
    state: {
      // 入手の古い順。同時に入手した装備は仮データの順。
      items: [...items]
        .sort(
          (a, b) =>
            a.acquiredAt.localeCompare(b.acquiredAt) ||
            order(a.equipmentId) - order(b.equipmentId) ||
            a.id.localeCompare(b.id),
        )
        .map((item) => ({
          id: item.id,
          equipmentId: item.equipmentId,
          ...qualityFields(
            item.multiplierPercent,
            maxMultiplierPercent(item.equipmentId as EquipmentId),
          ),
        })),
      characters: characterIds.map((id) => ({
        id,
        weapon: worn(id, "weapon"),
        armor: worn(id, "armor"),
        // アクセサリーの3つの枠。空いている枠はnull。
        accessories: (["accessory1", "accessory2", "accessory3"] as const).map(
          (slot) => worn(id, slot),
        ),
      })),
    },
  };
}

// 初期データはログインしたときに付与する（starter.ts の grantStarters）。
export function createEquipmentRepository(binding: D1Database) {
  const db = createDatabase(binding);

  // statements のあとに装備を読む文を足し、1回の往復で書いて読む。
  async function batchWithState<const T extends readonly Statement[]>(
    userId: string,
    statements: T,
  ) {
    const { reads } = await batchThenRead(
      db,
      statements,
      stateReads(db, userId),
    );
    return stateOf(reads);
  }

  async function getState(userId: string) {
    return (await batchWithState(userId, [])).state;
  }

  // キャラの枠に装備を付ける。ほかのキャラが付けていれば、そのキャラから外して付け替える。
  async function equip(
    userId: string,
    characterId: string,
    slot: EquipmentSlot,
    itemId: string,
    now: string,
  ) {
    const { levels, state } = await batchWithState(userId, []);
    const character = state.characters.find(
      (entry) => entry.id === characterId,
    );
    if (!character) return { error: "character_not_owned" as const };
    const item = state.items.find((entry) => entry.id === itemId);
    if (!item) return { error: "item_not_owned" as const };
    if (slotOf(item.equipmentId) !== kindOfSlot(slot)) {
      return { error: "slot_mismatch" as const };
    }
    // アクセサリーの2つ目と3つ目の枠は、キャラのレベルで開く。
    if ((levels.get(characterId) ?? 1) < unlockLevelOf(slot)) {
      return { error: "slot_locked" as const };
    }
    const current = wornIn(character, slot);
    if (current === itemId) return { change: "none" as const, ...state };

    // 同じキャラのほかのアクセサリーの枠に付けていても、選んだ枠へ付け替える。
    const holder = state.characters.find((entry) =>
      EQUIPMENT_SLOTS.some(
        (other) =>
          wornIn(entry, other) === itemId &&
          (entry.id !== characterId || other !== slot),
      ),
    );
    const after = await batchWithState(userId, [
      db
        .delete(equipmentCharacterSlots)
        .where(
          and(
            eq(equipmentCharacterSlots.userId, userId),
            eq(equipmentCharacterSlots.itemId, itemId),
          ),
        ),
      db
        .insert(equipmentCharacterSlots)
        .values({ userId, characterId, slot, itemId, updatedAt: now })
        .onConflictDoUpdate({
          target: [
            equipmentCharacterSlots.userId,
            equipmentCharacterSlots.characterId,
            equipmentCharacterSlots.slot,
          ],
          set: { itemId, updatedAt: now },
        }),
    ]);
    const change: EquipmentChange = holder
      ? "move"
      : current === null
        ? "equip"
        : "replace";
    return {
      change,
      ...(holder ? { from: holder.id } : {}),
      ...after.state,
    };
  }

  // キャラの枠の装備を外す。外した装備は持ち物に残る。
  async function unequip(
    userId: string,
    characterId: string,
    slot: EquipmentSlot,
  ) {
    const { state } = await batchWithState(userId, []);
    const character = state.characters.find(
      (entry) => entry.id === characterId,
    );
    if (!character) return { error: "character_not_owned" as const };
    if (wornIn(character, slot) === null) {
      return { change: "none" as const, ...state };
    }
    const after = await batchWithState(userId, [
      db
        .delete(equipmentCharacterSlots)
        .where(
          and(
            eq(equipmentCharacterSlots.userId, userId),
            eq(equipmentCharacterSlots.characterId, characterId),
            eq(equipmentCharacterSlots.slot, slot),
          ),
        ),
    ]);
    return { change: "remove" as const, ...after.state };
  }

  return { getState, equip, unequip };
}

type WornCharacter = {
  weapon: string | null;
  armor: string | null;
  accessories: (string | null)[];
};

// キャラがその枠に付けている装備のID。空いていればnull。
function wornIn(character: WornCharacter, slot: EquipmentSlot) {
  if (slot === "weapon" || slot === "armor") return character[slot];
  return character.accessories[EQUIPMENT_SLOTS.indexOf(slot) - 2] ?? null;
}
