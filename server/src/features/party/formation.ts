import { and, eq, inArray, sql } from "drizzle-orm";
import type { Database } from "../../shared/db.js";
import type { CharacterId } from "./catalog.js";
import { partyCharacterCards, partySlots } from "./db-schema.js";
import { batchWithState, contentOf, readState } from "./state.js";

type PartySlotChange = "join" | "replace" | "leave" | "none";
type PartyCardChange = "replace" | "swap" | "move" | "remove" | "none";

// 編成（4つの枠のキャラと、キャラごとのカスタムスキルの枠）を変える。
// 確かめる読み込みと、書き込みと書いたあとの読み込みの2回の往復で済ませる。
export function createFormationOperations(db: Database) {
  // 枠にパーティにいないキャラを入れる。枠にいたキャラは外れる。
  async function setSlot(
    userId: string,
    slot: number,
    characterId: CharacterId,
    now: string,
  ) {
    const state = await readState(db, userId);
    if (!state.characters.some((character) => character.id === characterId)) {
      return { error: "character_not_owned" as const };
    }
    const current = state.slots[slot].characterId;
    if (current === characterId) return { change: "none" as const, ...state };
    // 編成の画面と同じく、パーティ内での並べ替えはしない。
    if (state.slots.some((entry) => entry.characterId === characterId)) {
      return { error: "already_in_party" as const };
    }
    const after = await batchWithState(db, userId, [
      db
        .insert(partySlots)
        .values({ userId, slot, characterId, updatedAt: now })
        .onConflictDoUpdate({
          target: [partySlots.userId, partySlots.slot],
          set: { characterId, updatedAt: now },
        }),
    ]);
    const change: PartySlotChange = current === null ? "join" : "replace";
    return { change, ...after.state };
  }

  // 枠のキャラを外して空きにする。最後の1人は外さない。
  async function clearSlot(userId: string, slot: number) {
    const state = await readState(db, userId);
    if (state.slots[slot].characterId === null) {
      return { change: "none" as const, ...state };
    }
    // 確認と削除の間に別の要求が割り込んでも、1人を残す条件を削除と同じ文に入れる。
    const { state: after } = await batchWithState(db, userId, [
      db
        .delete(partySlots)
        .where(
          and(
            eq(partySlots.userId, userId),
            eq(partySlots.slot, slot),
            sql`(SELECT COUNT(*) FROM ${partySlots} WHERE ${partySlots.userId} = ${userId}) > 1`,
          ),
        ),
    ]);
    if (after.slots[slot].characterId !== null) {
      return { error: "keep_one" as const };
    }
    return { change: "leave" as const, ...after };
  }

  // キャラの枠に、持っているスキルの1枚かアイテムを入れる（key は `card:<ID>`・`item:<アイテムのID>` の形）。
  // そのキャラのほかの枠にあれば、2つの枠の中身を入れ替える。スキルの1枚をほかのキャラが付けていれば、
  // そのキャラから外して付け替える。アイテムは、ほかの枠に入れている数が持っている数に届いていれば入れられない。
  async function setCard(
    userId: string,
    characterId: CharacterId,
    slot: number,
    key: string,
    now: string,
  ) {
    const state = await readState(db, userId);
    const character = state.characters.find(
      (entry) => entry.id === characterId,
    );
    if (!character) return { error: "character_not_owned" as const };
    const { cardId, itemId } = contentOf(key);
    const item = state.items.find((entry) => entry.itemId === itemId);
    if (!item && !state.skillCards.some((card) => card.id === cardId)) {
      return { error: "card_not_owned" as const };
    }
    const current = character.cards[slot];
    if (current === key) return { change: "none" as const, ...state };

    const from = character.cards.indexOf(key);
    // ほかのキャラが付けているスキルの1枚。アイテムは動かさない。
    const holder =
      from >= 0 || cardId === null
        ? undefined
        : state.characters.find(
            (entry) => entry.id !== characterId && entry.cards.includes(key),
          );
    if (item && from < 0) {
      const used = state.characters
        .flatMap((entry) => entry.cards)
        .filter((card) => card === key).length;
      if (used >= item.count) return { error: "item_in_use" as const };
    }

    const row = (target: number, content: string) => ({
      userId,
      characterId,
      slot: target,
      ...contentOf(content),
      updatedAt: now,
    });
    const touched = from >= 0 ? [slot, from] : [slot];
    const inserts = [row(slot, key)];
    if (from >= 0 && current !== null) inserts.push(row(from, current));
    const after = await batchWithState(db, userId, [
      db
        .delete(partyCharacterCards)
        .where(
          and(
            eq(partyCharacterCards.userId, userId),
            eq(partyCharacterCards.characterId, characterId),
            inArray(partyCharacterCards.slot, touched),
          ),
        ),
      ...(holder
        ? [
            db
              .delete(partyCharacterCards)
              .where(
                and(
                  eq(partyCharacterCards.userId, userId),
                  eq(partyCharacterCards.cardId, cardId as string),
                ),
              ),
          ]
        : []),
      db.insert(partyCharacterCards).values(inserts),
    ]);
    const change: PartyCardChange =
      from >= 0 ? "swap" : holder ? "move" : "replace";
    return {
      change,
      ...(holder ? { from: holder.id } : {}),
      ...after.state,
    };
  }

  // キャラの枠を空きにする（スキル変更の「スキルを外す」）。空きの枠はそのまま返す。
  async function clearCard(
    userId: string,
    characterId: CharacterId,
    slot: number,
  ) {
    const state = await readState(db, userId);
    const character = state.characters.find(
      (entry) => entry.id === characterId,
    );
    if (!character) return { error: "character_not_owned" as const };
    if (character.cards[slot] === null) {
      return { change: "none" as const, ...state };
    }
    const after = await batchWithState(db, userId, [
      db
        .delete(partyCharacterCards)
        .where(
          and(
            eq(partyCharacterCards.userId, userId),
            eq(partyCharacterCards.characterId, characterId),
            eq(partyCharacterCards.slot, slot),
          ),
        ),
    ]);
    return { change: "remove" as const, ...after.state };
  }

  return { setSlot, clearSlot, setCard, clearCard };
}
