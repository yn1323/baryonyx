import { and, eq, inArray, sql } from "drizzle-orm";
import { createDatabase } from "../../shared/db.js";
import { runeWallets } from "../exercise-rewards/db-schema.js";
import {
  CARD_SLOT_COUNT,
  type CardSkillId,
  CHARACTER_IDS,
  type CharacterId,
  COST_PER_LEVEL,
  levelUpCost,
  MAX_LEVEL,
  PARTY_SLOT_COUNT,
  STARTER_CHARACTERS,
  STARTER_SLOTS,
} from "./catalog.js";
import {
  partyCharacterCards,
  partyCharacters,
  partyLevelUps,
  partyProfiles,
  partySlots,
} from "./db-schema.js";
import type { LevelUp } from "./schema.js";

export type PartySlotChange = "join" | "replace" | "leave" | "none";
export type PartyCardChange = "replace" | "swap" | "none";

export function createPartyRepository(binding: D1Database) {
  const db = createDatabase(binding);

  // 持っているキャラ（Lvと4枠のカード）、4つの枠、所持ルーン、レベルの決まり。
  async function readState(userId: string) {
    const [characters, cards, slots, wallets] = await db.batch([
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
          characterId: partyCharacterCards.characterId,
          slot: partyCharacterCards.slot,
          skillId: partyCharacterCards.skillId,
        })
        .from(partyCharacterCards)
        .where(eq(partyCharacterCards.userId, userId)),
      db
        .select({ slot: partySlots.slot, characterId: partySlots.characterId })
        .from(partySlots)
        .where(eq(partySlots.userId, userId)),
      db
        .select({ balance: runeWallets.balance })
        .from(runeWallets)
        .where(eq(runeWallets.userId, userId)),
    ]);
    const order = (id: string) => CHARACTER_IDS.indexOf(id as CharacterId);
    return {
      // 入手の古い順。同時に入手したキャラは仮データの順。
      characters: [...characters]
        .sort(
          (a, b) =>
            a.acquiredAt.localeCompare(b.acquiredAt) ||
            order(a.characterId) - order(b.characterId),
        )
        .map((character) => ({
          id: character.characterId,
          level: character.level,
          cards: Array.from(
            { length: CARD_SLOT_COUNT },
            (_, slot) =>
              cards.find(
                (card) =>
                  card.characterId === character.characterId &&
                  card.slot === slot,
              )?.skillId ?? null,
          ),
        })),
      slots: Array.from({ length: PARTY_SLOT_COUNT }, (_, slot) => ({
        slot,
        characterId:
          slots.find((row) => row.slot === slot)?.characterId ?? null,
      })),
      runes: wallets[0]?.balance ?? 0,
      rules: { maxLevel: MAX_LEVEL, costPerLevel: COST_PER_LEVEL },
    };
  }

  // 初めて読むユーザーにだけ、仮のキャラ・カード・編成を1回だけ付与する。
  async function ensureStarter(userId: string, now: string) {
    const profile = await db
      .select({ userId: partyProfiles.userId })
      .from(partyProfiles)
      .where(eq(partyProfiles.userId, userId))
      .get();
    if (profile) return;
    await db.batch([
      db
        .insert(partyProfiles)
        .values({ userId, createdAt: now })
        .onConflictDoNothing(),
      db
        .insert(partyCharacters)
        .values(
          STARTER_CHARACTERS.map((character) => ({
            userId,
            characterId: character.characterId,
            level: character.level,
            acquiredAt: now,
            updatedAt: now,
          })),
        )
        .onConflictDoNothing(),
      // D1は1つの文に100個までしか値を渡せないため、カードはキャラごとの文に分ける。
      ...STARTER_CHARACTERS.map((character) =>
        db
          .insert(partyCharacterCards)
          .values(
            character.cards.map((skillId, slot) => ({
              userId,
              characterId: character.characterId,
              slot,
              skillId,
              updatedAt: now,
            })),
          )
          .onConflictDoNothing(),
      ),
      db
        .insert(partySlots)
        .values(
          STARTER_SLOTS.map((characterId, slot) => ({
            userId,
            slot,
            characterId,
            updatedAt: now,
          })),
        )
        .onConflictDoNothing(),
    ]);
  }

  async function getState(userId: string, now: string) {
    await ensureStarter(userId, now);
    return readState(userId);
  }

  // 枠にパーティにいないキャラを入れる。枠にいたキャラは外れる。
  async function setSlot(
    userId: string,
    slot: number,
    characterId: CharacterId,
    now: string,
  ) {
    await ensureStarter(userId, now);
    const state = await readState(userId);
    if (!state.characters.some((character) => character.id === characterId)) {
      return { error: "character_not_owned" as const };
    }
    const current = state.slots[slot].characterId;
    if (current === characterId) return { change: "none" as const, ...state };
    // 編成の画面と同じく、パーティ内での並べ替えはしない。
    if (state.slots.some((entry) => entry.characterId === characterId)) {
      return { error: "already_in_party" as const };
    }
    await db
      .insert(partySlots)
      .values({ userId, slot, characterId, updatedAt: now })
      .onConflictDoUpdate({
        target: [partySlots.userId, partySlots.slot],
        set: { characterId, updatedAt: now },
      });
    const change: PartySlotChange = current === null ? "join" : "replace";
    return { change, ...(await readState(userId)) };
  }

  // 枠のキャラを外して空きにする。最後の1人は外さない。
  async function clearSlot(userId: string, slot: number, now: string) {
    await ensureStarter(userId, now);
    const state = await readState(userId);
    if (state.slots[slot].characterId === null) {
      return { change: "none" as const, ...state };
    }
    // 確認と削除の間に別の要求が割り込んでも、1人を残す条件を削除と同じ文に入れる。
    await db
      .delete(partySlots)
      .where(
        and(
          eq(partySlots.userId, userId),
          eq(partySlots.slot, slot),
          sql`(SELECT COUNT(*) FROM ${partySlots} WHERE ${partySlots.userId} = ${userId}) > 1`,
        ),
      );
    const after = await readState(userId);
    if (after.slots[slot].characterId !== null) {
      return { error: "keep_one" as const };
    }
    return { change: "leave" as const, ...after };
  }

  // キャラの枠にカードを入れる。そのキャラのほかの枠にあれば、2つの枠の中身を入れ替える。
  async function setCard(
    userId: string,
    characterId: CharacterId,
    slot: number,
    skillId: CardSkillId,
    now: string,
  ) {
    await ensureStarter(userId, now);
    const state = await readState(userId);
    const character = state.characters.find(
      (entry) => entry.id === characterId,
    );
    if (!character) return { error: "character_not_owned" as const };
    const current = character.cards[slot];
    if (current === skillId) return { change: "none" as const, ...state };

    const from = character.cards.indexOf(skillId);
    const touched = from >= 0 ? [slot, from] : [slot];
    const inserts: (typeof partyCharacterCards.$inferInsert)[] = [
      { userId, characterId, slot, skillId, updatedAt: now },
    ];
    if (from >= 0 && current !== null) {
      inserts.push({
        userId,
        characterId,
        slot: from,
        skillId: current,
        updatedAt: now,
      });
    }
    await db.batch([
      db
        .delete(partyCharacterCards)
        .where(
          and(
            eq(partyCharacterCards.userId, userId),
            eq(partyCharacterCards.characterId, characterId),
            inArray(partyCharacterCards.slot, touched),
          ),
        ),
      db.insert(partyCharacterCards).values(inserts),
    ]);
    const change: PartyCardChange = from >= 0 ? "swap" : "replace";
    return { change, ...(await readState(userId)) };
  }

  async function findLevelUp(userId: string, requestId: string) {
    return db
      .select({
        characterId: partyLevelUps.characterId,
        fromLevel: partyLevelUps.fromLevel,
        toLevel: partyLevelUps.toLevel,
        runes: partyLevelUps.runes,
        balanceAfter: partyLevelUps.balanceAfter,
      })
      .from(partyLevelUps)
      .where(
        and(
          eq(partyLevelUps.userId, userId),
          eq(partyLevelUps.requestId, requestId),
        ),
      )
      .get();
  }

  // 記録したレベルアップを返す。同じ要求IDで中身の違う要求は受け付けない。
  async function recorded(
    userId: string,
    characterId: CharacterId,
    request: LevelUp,
    row: NonNullable<Awaited<ReturnType<typeof findLevelUp>>>,
  ) {
    if (
      row.characterId !== characterId ||
      row.fromLevel !== request.fromLevel ||
      row.toLevel !== request.toLevel
    ) {
      return { error: "request_conflict" as const };
    }
    return { levelUp: row, ...(await readState(userId)) };
  }

  // ルーンを使ってキャラのレベルを上げる。費用はサーバーで計算し、残高と今のレベルを
  // 条件にした1回のbatchで、記録・ルーンの減算・レベルの更新をまとめて行う。
  async function levelUp(
    userId: string,
    characterId: CharacterId,
    request: LevelUp,
    now: string,
  ) {
    await ensureStarter(userId, now);
    const replay = await findLevelUp(userId, request.requestId);
    if (replay) return recorded(userId, characterId, request, replay);
    const state = await readState(userId);
    if (!state.characters.some((character) => character.id === characterId)) {
      return { error: "character_not_owned" as const };
    }

    const id = crypto.randomUUID();
    const runes = levelUpCost(request.fromLevel, request.toLevel);
    const balance = sql`(SELECT ${runeWallets.balance} FROM ${runeWallets} WHERE ${runeWallets.userId} = ${userId})`;
    const written = sql`EXISTS (SELECT 1 FROM ${partyLevelUps} WHERE ${partyLevelUps.id} = ${id})`;
    await db.batch([
      db
        .insert(partyLevelUps)
        .select(
          db
            .select({
              id: sql<string>`${id}`.as("id"),
              userId: partyCharacters.userId,
              requestId: sql<string>`${request.requestId}`.as("request_id"),
              characterId: partyCharacters.characterId,
              fromLevel: partyCharacters.level,
              toLevel: sql<number>`${request.toLevel}`.as("to_level"),
              runes: sql<number>`${runes}`.as("runes"),
              balanceAfter: sql<number>`0`.as("balance_after"),
              createdAt: sql<string>`${now}`.as("created_at"),
            })
            .from(partyCharacters)
            .where(
              and(
                eq(partyCharacters.userId, userId),
                eq(partyCharacters.characterId, characterId),
                eq(partyCharacters.level, request.fromLevel),
                sql`${balance} >= ${runes}`,
              ),
            ),
        )
        .onConflictDoNothing({
          target: [partyLevelUps.userId, partyLevelUps.requestId],
        }),
      db
        .update(runeWallets)
        .set({
          balance: sql`${runeWallets.balance} - ${runes}`,
          updatedAt: now,
        })
        .where(and(eq(runeWallets.userId, userId), written)),
      db
        .update(partyCharacters)
        .set({ level: request.toLevel, updatedAt: now })
        .where(
          and(
            eq(partyCharacters.userId, userId),
            eq(partyCharacters.characterId, characterId),
            written,
          ),
        ),
      db
        .update(partyLevelUps)
        .set({ balanceAfter: sql`COALESCE(${balance}, 0)` })
        .where(eq(partyLevelUps.id, id)),
    ]);

    const row = await findLevelUp(userId, request.requestId);
    if (row) return recorded(userId, characterId, request, row);
    // 書き込めなかった理由を、今のレベルで見分ける。
    const after = await readState(userId);
    const level = after.characters.find(
      (character) => character.id === characterId,
    )?.level;
    return {
      error:
        level !== request.fromLevel
          ? ("level_changed" as const)
          : ("insufficient_runes" as const),
    };
  }

  return { getState, setSlot, clearSlot, setCard, levelUp };
}
