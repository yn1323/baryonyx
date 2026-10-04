import { and, eq, sql } from "drizzle-orm";
import { createDatabase } from "../../shared/db.js";
import { createPartyRepository } from "../party/repository.js";
import {
  EQUIPMENT_IDS,
  type EquipmentId,
  type EquipmentSlot,
  STARTER_EQUIPPED,
  STARTER_ITEMS,
  slotOf,
} from "./catalog.js";
import {
  equipmentCharacterSlots,
  equipmentItems,
  equipmentProfiles,
} from "./db-schema.js";

export type EquipmentChange = "equip" | "replace" | "move" | "remove" | "none";

export function createEquipmentRepository(binding: D1Database) {
  const db = createDatabase(binding);
  const party = createPartyRepository(binding);

  // 持っている装備と、持っているキャラ（パーティの一覧と同じ順）ごとの武器・防具。
  async function readState(userId: string, characterIds: readonly string[]) {
    const [items, slots] = await db.batch([
      db
        .select({
          id: equipmentItems.id,
          equipmentId: equipmentItems.equipmentId,
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
    ]);
    const order = (id: string) => EQUIPMENT_IDS.indexOf(id as EquipmentId);
    const worn = (characterId: string, slot: EquipmentSlot) =>
      slots.find((row) => row.characterId === characterId && row.slot === slot)
        ?.itemId ?? null;
    return {
      // 入手の古い順。同時に入手した装備は仮データの順。
      items: [...items]
        .sort(
          (a, b) =>
            a.acquiredAt.localeCompare(b.acquiredAt) ||
            order(a.equipmentId) - order(b.equipmentId) ||
            a.id.localeCompare(b.id),
        )
        .map((item) => ({ id: item.id, equipmentId: item.equipmentId })),
      characters: characterIds.map((id) => ({
        id,
        weapon: worn(id, "weapon"),
        armor: worn(id, "armor"),
      })),
    };
  }

  // 初めて読むユーザーにだけ、仮の装備と初めの4人の装備を1回だけ付与する。
  // 装備のIDは毎回作るため、同時に届いた要求で二重に付与しないよう、印を書けた要求（grant_id）だけが装備を入れる。
  async function ensureStarter(userId: string, now: string) {
    const profile = await db
      .select({ userId: equipmentProfiles.userId })
      .from(equipmentProfiles)
      .where(eq(equipmentProfiles.userId, userId))
      .get();
    if (profile) return;
    const grantId = crypto.randomUUID();
    const granted = and(
      eq(equipmentProfiles.userId, userId),
      eq(equipmentProfiles.grantId, grantId),
    );
    const ids = STARTER_ITEMS.map(() => crypto.randomUUID());
    const itemOf = (equipmentId: EquipmentId) =>
      ids[STARTER_ITEMS.indexOf(equipmentId)];
    await db.batch([
      db
        .insert(equipmentProfiles)
        .values({ userId, grantId, createdAt: now })
        .onConflictDoNothing(),
      ...STARTER_ITEMS.map((equipmentId, index) =>
        db.insert(equipmentItems).select(
          db
            .select({
              id: sql<string>`${ids[index]}`.as("id"),
              userId: equipmentProfiles.userId,
              equipmentId: sql<string>`${equipmentId}`.as("equipment_id"),
              acquiredAt: sql<string>`${now}`.as("acquired_at"),
            })
            .from(equipmentProfiles)
            .where(granted),
        ),
      ),
      ...STARTER_EQUIPPED.flatMap((entry) =>
        (["weapon", "armor"] as const).map((slot) =>
          db.insert(equipmentCharacterSlots).select(
            db
              .select({
                userId: equipmentProfiles.userId,
                characterId: sql<string>`${entry.characterId}`.as(
                  "character_id",
                ),
                slot: sql<string>`${slot}`.as("slot"),
                itemId: sql<string>`${itemOf(entry[slot])}`.as("item_id"),
                updatedAt: sql<string>`${now}`.as("updated_at"),
              })
              .from(equipmentProfiles)
              .where(granted),
          ),
        ),
      ),
    ]);
  }

  // キャラの装備は持っているキャラにしか付けられないため、先にパーティの初期データを付与する。
  async function load(userId: string, now: string) {
    const characters = (await party.getState(userId, now)).characters.map(
      (character) => character.id,
    );
    await ensureStarter(userId, now);
    return { characters, state: await readState(userId, characters) };
  }

  async function getState(userId: string, now: string) {
    return (await load(userId, now)).state;
  }

  // キャラの枠に装備を付ける。ほかのキャラが付けていれば、そのキャラから外して付け替える。
  async function equip(
    userId: string,
    characterId: string,
    slot: EquipmentSlot,
    itemId: string,
    now: string,
  ) {
    const { characters, state } = await load(userId, now);
    const character = state.characters.find(
      (entry) => entry.id === characterId,
    );
    if (!character) return { error: "character_not_owned" as const };
    const item = state.items.find((entry) => entry.id === itemId);
    if (!item) return { error: "item_not_owned" as const };
    if (slotOf(item.equipmentId) !== slot) {
      return { error: "slot_mismatch" as const };
    }
    const current = character[slot];
    if (current === itemId) return { change: "none" as const, ...state };

    const holder = state.characters.find(
      (entry) => entry.id !== characterId && entry[slot] === itemId,
    );
    await db.batch([
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
      ...(await readState(userId, characters)),
    };
  }

  // キャラの枠の装備を外す。外した装備は持ち物に残る。
  async function unequip(
    userId: string,
    characterId: string,
    slot: EquipmentSlot,
    now: string,
  ) {
    const { characters, state } = await load(userId, now);
    const character = state.characters.find(
      (entry) => entry.id === characterId,
    );
    if (!character) return { error: "character_not_owned" as const };
    if (character[slot] === null) return { change: "none" as const, ...state };
    await db
      .delete(equipmentCharacterSlots)
      .where(
        and(
          eq(equipmentCharacterSlots.userId, userId),
          eq(equipmentCharacterSlots.characterId, characterId),
          eq(equipmentCharacterSlots.slot, slot),
        ),
      );
    return {
      change: "remove" as const,
      ...(await readState(userId, characters)),
    };
  }

  return { getState, equip, unequip };
}
