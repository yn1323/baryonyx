import { eq } from "drizzle-orm";
import type { BatchResponse } from "drizzle-orm/batch";
import {
  batchThenRead,
  type Database,
  type Statement,
} from "../../shared/db.js";
import {
  MIN_MULTIPLIER_PERCENT,
  multiplierOf,
  qualityOf,
} from "../../shared/quality.js";
import { selectRuneBalance } from "../exercise-rewards/wallet.js";
import { ITEM_IDS, type ItemId } from "../items/catalog.js";
import { itemHoldings } from "../items/db-schema.js";
import { characterBonds } from "../summon/db-schema.js";
import {
  bitOf,
  CARD_SKILL_IDS,
  CARD_SLOT_COUNT,
  type CardSkillId,
  CHARACTER_IDS,
  type CharacterId,
  COST_PER_LEVEL,
  MAX_LEVEL,
  maxMultiplierPercent,
  PARTY_SLOT_COUNT,
  stagesOf,
  UNIQUE_SKILL_COUNT,
  uniqueMaxPercent,
} from "./catalog.js";
import {
  partyCharacterCards,
  partyCharacters,
  partySlots,
  partyUniqueSkills,
  skillCards,
} from "./db-schema.js";

// 編成の読み込み。書き込みのAPIは、書き込みと同じ batch の後ろに読み込みの文を並べ、1回の往復で書いて読む。

// カスタムスキルの枠に入れる物の書き方。持っているスキルの1枚は `card:<ID>`、アイテムは `item:<アイテムのID>`。
type CardContent = { cardId: string | null; itemId: string | null };
export const keyOf = (content: CardContent) =>
  content.cardId !== null
    ? `card:${content.cardId}`
    : content.itemId !== null
      ? `item:${content.itemId}`
      : null;
export const contentOf = (key: string): CardContent => {
  const id = key.slice(key.indexOf(":") + 1);
  return key.startsWith("item:")
    ? { cardId: null, itemId: id }
    : { cardId: id, itemId: null };
};

// 持っているキャラの並び。入手の古い順で、同時に入手したキャラは仮データの順。装備の一覧も同じ順に並べる。
export function ownedInOrder<
  T extends { characterId: string; acquiredAt: string },
>(rows: readonly T[]) {
  const order = (id: string) => CHARACTER_IDS.indexOf(id as CharacterId);
  return [...rows].sort(
    (a, b) =>
      a.acquiredAt.localeCompare(b.acquiredAt) ||
      order(a.characterId) - order(b.characterId),
  );
}

// 固有スキルのセットしている段階と、取得した段階（小さい順）、倍率と品質。
// 行がなければ（覚醒も品質強化もしていない）最初の段階だけで、倍率は×1.00。商会の強化の応答も使う。
export function uniqueStateOf(
  characterId: string,
  index: number,
  row:
    | { setStage: number; acquiredMask: number; multiplierPercent: number }
    | undefined,
) {
  const percent = row?.multiplierPercent ?? MIN_MULTIPLIER_PERCENT;
  return {
    index,
    set: row?.setStage ?? 0,
    acquired: stagesOf(row?.acquiredMask ?? bitOf(0)),
    multiplier: multiplierOf(percent),
    quality: qualityOf(
      percent,
      uniqueMaxPercent(characterId as CharacterId, index),
    ),
  };
}

// 持っているスキルの1枚。商会の覚醒の応答も使う。
export function skillCardStateOf(card: {
  id: string;
  skillId: string;
  multiplierPercent: number;
  awakenedBranch: number | null;
}) {
  return {
    id: card.id,
    skillId: card.skillId,
    multiplier: multiplierOf(card.multiplierPercent),
    quality: qualityOf(
      card.multiplierPercent,
      maxMultiplierPercent(card.skillId as CardSkillId),
    ),
    // 覚醒した枝（1か2）。覚醒していなければ null（doc/features/skill-awakening.md）。
    awakenedBranch: card.awakenedBranch,
  };
}

/**
 * 編成を読む文。持っているキャラ（Lv・3つのカスタムスキルの枠・3つの固有スキルの段階・絆と絆のしるし）、
 * 4つの枠、持っているスキルとアイテム、所持ルーン。
 */
export function stateReads(db: Database, userId: string) {
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
        multiplierPercent: partyUniqueSkills.multiplierPercent,
      })
      .from(partyUniqueSkills)
      .where(eq(partyUniqueSkills.userId, userId)),
    db
      .select({
        characterId: characterBonds.characterId,
        bond: characterBonds.bond,
        tokens: characterBonds.tokens,
      })
      .from(characterBonds)
      .where(eq(characterBonds.userId, userId)),
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
        awakenedBranch: skillCards.awakenedBranch,
      })
      .from(skillCards)
      .where(eq(skillCards.userId, userId)),
    db
      .select({ itemId: itemHoldings.itemId, count: itemHoldings.count })
      .from(itemHoldings)
      .where(eq(itemHoldings.userId, userId)),
    selectRuneBalance(db, userId),
  ] as const;
}

/** 読み込みの結果から、編成の応答を作る。 */
export function stateOf([
  characters,
  cards,
  uniques,
  bonds,
  slots,
  owned,
  items,
  wallets,
]: BatchResponse<ReturnType<typeof stateReads>>) {
  const skillOrder = (id: string) => CARD_SKILL_IDS.indexOf(id as CardSkillId);
  const itemOrder = (id: string) => ITEM_IDS.indexOf(id as ItemId);
  return {
    characters: ownedInOrder(characters).map((character) => ({
      id: character.characterId,
      level: character.level,
      cards: Array.from({ length: CARD_SLOT_COUNT }, (_, slot) => {
        const row = cards.find(
          (card) =>
            card.characterId === character.characterId && card.slot === slot,
        );
        return row ? keyOf(row) : null;
      }),
      // 固有スキルごとのセットしている段階と、取得した段階（小さい順）、倍率と品質。
      // 行がなければ最初の段階だけで、倍率は×1.00。
      uniques: Array.from({ length: UNIQUE_SKILL_COUNT }, (_, index) =>
        uniqueStateOf(
          character.characterId,
          index,
          uniques.find(
            (unique) =>
              unique.characterId === character.characterId &&
              unique.uniqueIndex === index,
          ),
        ),
      ),
      // 絆の段と、持っている絆のしるし（doc/features/monetization.md のキャラが被ったとき）。行がなければ0。
      bond:
        bonds.find((row) => row.characterId === character.characterId)?.bond ??
        0,
      tokens:
        bonds.find((row) => row.characterId === character.characterId)
          ?.tokens ?? 0,
    })),
    slots: Array.from({ length: PARTY_SLOT_COUNT }, (_, slot) => ({
      slot,
      characterId: slots.find((row) => row.slot === slot)?.characterId ?? null,
    })),
    // 持っているスキル。入手の古い順、同時に入手したスキルは仮データの順。
    skillCards: [...owned]
      .sort(
        (a, b) =>
          a.acquiredAt.localeCompare(b.acquiredAt) ||
          skillOrder(a.skillId) - skillOrder(b.skillId) ||
          a.id.localeCompare(b.id),
      )
      .map(skillCardStateOf),
    // 持っているアイテムと数。アイテムの定義の順。
    items: [...items]
      .sort((a, b) => itemOrder(a.itemId) - itemOrder(b.itemId))
      .map((item) => ({ itemId: item.itemId, count: item.count })),
    runes: wallets[0]?.balance ?? 0,
    rules: { maxLevel: MAX_LEVEL, costPerLevel: COST_PER_LEVEL },
  };
}

export type PartyState = ReturnType<typeof stateOf>;

/** statements のあとに編成を読む文を足し、1回の往復で書いて読む。results は statements の結果。 */
export async function batchWithState<const T extends readonly Statement[]>(
  db: Database,
  userId: string,
  statements: T,
) {
  const { results, reads } = await batchThenRead(
    db,
    statements,
    stateReads(db, userId),
  );
  return { results, state: stateOf(reads) };
}

export async function readState(db: Database, userId: string) {
  return (await batchWithState(db, userId, [])).state;
}
