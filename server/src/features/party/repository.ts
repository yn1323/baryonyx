import { and, eq, gte, inArray, or, sql } from "drizzle-orm";
import { createDatabase } from "../../shared/db.js";
import {
  MIN_MULTIPLIER_PERCENT,
  multiplierOf,
  qualityOf,
} from "../../shared/quality.js";
import { adventureRuns } from "../adventure/db-schema.js";
import {
  debitRunes,
  runeBalance,
  selectRuneBalance,
} from "../exercise-rewards/wallet.js";
import { ITEM_IDS, type ItemId } from "../items/catalog.js";
import { itemHoldings } from "../items/db-schema.js";
import { discoveryStatements } from "../library/discoveries.js";
import {
  CARD_SKILL_IDS,
  CARD_SLOT_COUNT,
  type CardSkillId,
  CHARACTER_IDS,
  type CharacterId,
  COST_PER_LEVEL,
  EVOLUTION_GRACE_MINUTES,
  levelUpCost,
  MAX_LEVEL,
  maxMultiplierPercent,
  PARTY_SLOT_COUNT,
  STARTER_CHARACTERS,
  STARTER_SLOTS,
  UNIQUE_SKILL_COUNT,
  UNIQUE_STAGE_COUNT,
  UNIQUE_UNLOCK_LEVELS,
  uniqueParent,
} from "./catalog.js";
import {
  partyCharacterCards,
  partyCharacters,
  partyLevelUps,
  partyProfiles,
  partySlots,
  partyUniqueSkills,
  skillCards,
} from "./db-schema.js";
import type { LevelUp } from "./schema.js";

export type PartySlotChange = "join" | "replace" | "leave" | "none";
export type PartyCardChange = "replace" | "swap" | "move" | "none";
export type PartyUniqueChange = "set" | "none";
export type PartyEvolveChange = "evolve" | "none";

// 取得した段階のビット（段階 n が 1 << n）と段階の一覧の変換。最初の段階（0）は常に取得している。
const bitOf = (stage: number) => 1 << stage;
const stagesOf = (mask: number) =>
  Array.from({ length: UNIQUE_STAGE_COUNT }, (_, stage) => stage).filter(
    (stage) => stage === 0 || (mask & bitOf(stage)) !== 0,
  );

// カスタムスキルの枠に入れる物の書き方。持っているスキルの1枚は `card:<ID>`、アイテムは `item:<アイテムのID>`。
type CardContent = { cardId: string | null; itemId: string | null };
const keyOf = (content: CardContent) =>
  content.cardId !== null
    ? `card:${content.cardId}`
    : content.itemId !== null
      ? `item:${content.itemId}`
      : null;
const contentOf = (key: string): CardContent => {
  const id = key.slice(key.indexOf(":") + 1);
  return key.startsWith("item:")
    ? { cardId: null, itemId: id }
    : { cardId: id, itemId: null };
};

export function createPartyRepository(binding: D1Database) {
  const db = createDatabase(binding);

  // 持っているキャラ（Lv・2つのカスタムスキルの枠・2つの固有スキルの段階）、4つの枠、持っているスキルとアイテム、
  // 所持ルーン、レベルの決まり。
  async function readState(userId: string) {
    const [characters, cards, uniques, slots, owned, items, wallets] =
      await db.batch([
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
            cardId: partyCharacterCards.cardId,
            itemId: partyCharacterCards.itemId,
          })
          .from(partyCharacterCards)
          .where(eq(partyCharacterCards.userId, userId)),
        db
          .select({
            characterId: partyUniqueSkills.characterId,
            uniqueIndex: partyUniqueSkills.uniqueIndex,
            setStage: partyUniqueSkills.setStage,
            acquiredMask: partyUniqueSkills.acquiredMask,
          })
          .from(partyUniqueSkills)
          .where(eq(partyUniqueSkills.userId, userId)),
        db
          .select({
            slot: partySlots.slot,
            characterId: partySlots.characterId,
          })
          .from(partySlots)
          .where(eq(partySlots.userId, userId)),
        db
          .select({
            id: skillCards.id,
            skillId: skillCards.skillId,
            multiplierPercent: skillCards.multiplierPercent,
            acquiredAt: skillCards.acquiredAt,
          })
          .from(skillCards)
          .where(eq(skillCards.userId, userId)),
        db
          .select({ itemId: itemHoldings.itemId, count: itemHoldings.count })
          .from(itemHoldings)
          .where(eq(itemHoldings.userId, userId)),
        selectRuneBalance(db, userId),
      ]);
    const order = (id: string) => CHARACTER_IDS.indexOf(id as CharacterId);
    const skillOrder = (id: string) =>
      CARD_SKILL_IDS.indexOf(id as CardSkillId);
    const itemOrder = (id: string) => ITEM_IDS.indexOf(id as ItemId);
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
          cards: Array.from({ length: CARD_SLOT_COUNT }, (_, slot) => {
            const row = cards.find(
              (card) =>
                card.characterId === character.characterId &&
                card.slot === slot,
            );
            return row ? keyOf(row) : null;
          }),
          // 固有スキルごとのセットしている段階と、取得した段階（小さい順）。行がなければ最初の段階だけ。
          uniques: Array.from({ length: UNIQUE_SKILL_COUNT }, (_, index) => {
            const row = uniques.find(
              (unique) =>
                unique.characterId === character.characterId &&
                unique.uniqueIndex === index,
            );
            return {
              index,
              set: row?.setStage ?? 0,
              acquired: stagesOf(row?.acquiredMask ?? bitOf(0)),
            };
          }),
        })),
      slots: Array.from({ length: PARTY_SLOT_COUNT }, (_, slot) => ({
        slot,
        characterId:
          slots.find((row) => row.slot === slot)?.characterId ?? null,
      })),
      // 持っているスキル。入手の古い順、同時に入手したスキルは仮データの順。
      skillCards: [...owned]
        .sort(
          (a, b) =>
            a.acquiredAt.localeCompare(b.acquiredAt) ||
            skillOrder(a.skillId) - skillOrder(b.skillId) ||
            a.id.localeCompare(b.id),
        )
        .map((card) => ({
          id: card.id,
          skillId: card.skillId,
          multiplier: multiplierOf(card.multiplierPercent),
          quality: qualityOf(
            card.multiplierPercent,
            maxMultiplierPercent(card.skillId as CardSkillId),
          ),
        })),
      // 持っているアイテムと数。アイテムの定義の順。
      items: [...items]
        .sort((a, b) => itemOrder(a.itemId) - itemOrder(b.itemId))
        .map((item) => ({ itemId: item.itemId, count: item.count })),
      runes: wallets[0]?.balance ?? 0,
      rules: { maxLevel: MAX_LEVEL, costPerLevel: COST_PER_LEVEL },
    };
  }

  // 初めて読むユーザーにだけ、仮のキャラ・カード・編成を1回だけ付与する。初めのカードは、品質C（×1.00）の
  // スキルを1枚ずつ持ち物に入れ、その1枚を枠に入れる。カードのIDは毎回作るため、同時に届いた要求で
  // 二重に付与しないよう、印を書けた要求（grant_id）だけがカードを入れる。
  async function ensureStarter(userId: string, now: string) {
    const profile = await db
      .select({ userId: partyProfiles.userId })
      .from(partyProfiles)
      .where(eq(partyProfiles.userId, userId))
      .get();
    if (profile) return;
    const grantId = crypto.randomUUID();
    const granted = and(
      eq(partyProfiles.userId, userId),
      eq(partyProfiles.grantId, grantId),
    );
    const starterCards = STARTER_CHARACTERS.flatMap((character) =>
      character.cards.map((skillId, slot) => ({
        id: crypto.randomUUID(),
        characterId: character.characterId,
        slot,
        skillId,
      })),
    );
    await db.batch([
      db
        .insert(partyProfiles)
        .values({ userId, grantId, createdAt: now })
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
      // D1は1つの文に100個までしか値を渡せないため、カードは1枚ずつの文に分ける。
      ...starterCards.map((card) =>
        db.insert(skillCards).select(
          db
            .select({
              id: sql<string>`${card.id}`.as("id"),
              userId: partyProfiles.userId,
              skillId: sql<string>`${card.skillId}`.as("skill_id"),
              multiplierPercent: sql<number>`${MIN_MULTIPLIER_PERCENT}`.as(
                "multiplier_percent",
              ),
              acquiredAt: sql<string>`${now}`.as("acquired_at"),
            })
            .from(partyProfiles)
            .where(granted),
        ),
      ),
      ...starterCards.map((card) =>
        db.insert(partyCharacterCards).select(
          db
            .select({
              userId: partyProfiles.userId,
              characterId: sql<string>`${card.characterId}`.as("character_id"),
              slot: sql<number>`${card.slot}`.as("slot"),
              cardId: sql<string>`${card.id}`.as("card_id"),
              itemId: sql<null>`NULL`.as("item_id"),
              updatedAt: sql<string>`${now}`.as("updated_at"),
            })
            .from(partyProfiles)
            .where(granted),
        ),
      ),
      ...discoveryStatements(
        db,
        userId,
        "character",
        STARTER_CHARACTERS.map((character) => character.characterId),
        now,
      ),
      ...discoveryStatements(
        db,
        userId,
        "skill",
        STARTER_CHARACTERS.flatMap((character) => character.cards),
        now,
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
    await ensureStarter(userId, now);
    const state = await readState(userId);
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
      ...(await readState(userId)),
    };
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
    const balance = runeBalance(userId);
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
      debitRunes(db, userId, runes, written, now),
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

  // 固有スキルを確かめる。持っていないキャラと、解放するLvに届いていない固有スキルは扱わない。
  function findUnique(
    state: Awaited<ReturnType<typeof readState>>,
    characterId: CharacterId,
    index: number,
  ) {
    const character = state.characters.find(
      (entry) => entry.id === characterId,
    );
    if (!character) return { error: "character_not_owned" as const };
    if (character.level < UNIQUE_UNLOCK_LEVELS[index]) {
      return { error: "unique_locked" as const };
    }
    return { unique: character.uniques[index] };
  }

  // 編成で、固有スキルに取得した段階のどれか1つをセットする。手前の段階に戻すのもこの操作で行う。
  async function setUnique(
    userId: string,
    characterId: CharacterId,
    index: number,
    stage: number,
    now: string,
  ) {
    await ensureStarter(userId, now);
    const state = await readState(userId);
    const found = findUnique(state, characterId, index);
    if ("error" in found) return found;
    if (!found.unique.acquired.includes(stage)) {
      return { error: "stage_not_acquired" as const };
    }
    if (found.unique.set === stage) {
      return { change: "none" as const, ...state };
    }
    // 最初の段階以外を取得していれば行がある。取得の確認を更新と同じ文に入れる。
    await db
      .update(partyUniqueSkills)
      .set({ setStage: stage, updatedAt: now })
      .where(
        and(
          eq(partyUniqueSkills.userId, userId),
          eq(partyUniqueSkills.characterId, characterId),
          eq(partyUniqueSkills.uniqueIndex, index),
          sql`(${partyUniqueSkills.acquiredMask} & ${bitOf(stage)}) <> 0`,
        ),
      );
    const after = await readState(userId);
    const unique = after.characters.find((entry) => entry.id === characterId)
      ?.uniques[index];
    if (unique?.set !== stage) return { error: "stage_not_acquired" as const };
    const change: PartyUniqueChange = "set";
    return { change, ...after };
  }

  // 冒険の戦闘で起きた1回の進化を保存する。進む先は、セットしている段階の次の段階だけで、
  // 進化した段階を取得し、そのままセットする。取得済みの段階へ進むのも進化として扱う。
  // 同じ進化の再送（すでにその段階をセットしている）は、何も変えずに成功として返す。
  // 進化は冒険の戦闘でだけ起きる。冒険の途中か、踏破・敗北で終えた直後（そのマスの戦闘の進化が、
  // 冒険を終えた知らせと前後して届く）だけ受け付ける。途中でやめた冒険の戦闘の進化は残さない。
  async function inAdventure(userId: string, now: string) {
    const since = new Date(
      Date.parse(now) - EVOLUTION_GRACE_MINUTES * 60_000,
    ).toISOString();
    const runs = await db
      .select({ id: adventureRuns.id })
      .from(adventureRuns)
      .where(
        and(
          eq(adventureRuns.userId, userId),
          or(
            eq(adventureRuns.status, "active"),
            and(
              inArray(adventureRuns.status, ["cleared", "defeated"]),
              gte(adventureRuns.endedAt, since),
            ),
          ),
        ),
      )
      .limit(1);
    return runs.length > 0;
  }

  async function evolveUnique(
    userId: string,
    characterId: CharacterId,
    index: number,
    stage: number,
    now: string,
  ) {
    await ensureStarter(userId, now);
    const state = await readState(userId);
    const found = findUnique(state, characterId, index);
    if ("error" in found) return found;
    if (found.unique.set === stage) {
      return { change: "none" as const, ...state };
    }
    const parent = uniqueParent(stage);
    if (parent !== found.unique.set) {
      return { error: "not_next_stage" as const };
    }
    if (!(await inAdventure(userId, now))) {
      return { error: "no_adventure" as const };
    }
    const target = and(
      eq(partyUniqueSkills.userId, userId),
      eq(partyUniqueSkills.characterId, characterId),
      eq(partyUniqueSkills.uniqueIndex, index),
    );
    // 確認と書き込みの間に別の進化やセットが割り込んでも、手前の段階をセットしていることと、
    // 解放するLvに届いていることを更新と同じ文に入れる。
    await db.batch([
      db
        .insert(partyUniqueSkills)
        .values({
          userId,
          characterId,
          uniqueIndex: index,
          setStage: 0,
          acquiredMask: bitOf(0),
          updatedAt: now,
        })
        .onConflictDoNothing(),
      db
        .update(partyUniqueSkills)
        .set({
          setStage: stage,
          acquiredMask: sql`${partyUniqueSkills.acquiredMask} | ${bitOf(stage)}`,
          updatedAt: now,
        })
        .where(
          and(
            target,
            eq(partyUniqueSkills.setStage, parent),
            sql`EXISTS (SELECT 1 FROM ${partyCharacters} WHERE ${partyCharacters.userId} = ${userId} AND ${partyCharacters.characterId} = ${characterId} AND ${partyCharacters.level} >= ${UNIQUE_UNLOCK_LEVELS[index]})`,
          ),
        ),
    ]);
    const after = await readState(userId);
    const unique = after.characters.find((entry) => entry.id === characterId)
      ?.uniques[index];
    if (unique?.set !== stage) return { error: "not_next_stage" as const };
    const change: PartyEvolveChange = "evolve";
    return { change, ...after };
  }

  return {
    getState,
    setSlot,
    clearSlot,
    setCard,
    levelUp,
    setUnique,
    evolveUnique,
  };
}
