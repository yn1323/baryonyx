import { and, eq, sql } from "drizzle-orm";
import { type Database, insertRowsWhen } from "../../shared/db.js";
import { MIN_MULTIPLIER_PERCENT } from "../../shared/quality.js";
import { discoveryStatements } from "../library/discoveries.js";
import { STARTER_CHARACTERS, STARTER_SLOTS } from "./catalog.js";
import {
  partyCharacterCards,
  partyCharacters,
  partyProfiles,
  partySlots,
  skillCards,
} from "./db-schema.js";

/**
 * 仮のキャラ・カード・編成を付与する文。初めのカードは、品質C（×1.00）のスキルを1枚ずつ持ち物に入れ、
 * その1枚を枠に入れる。カードのIDは毎回作るため、同時に届いた要求で二重に付与しないよう、
 * 印を書けた要求（grant_id）だけがカードを入れる。外した枠を戻してしまうため、付与の印がないときにだけ使う
 * （equipment/starter.ts の grantStarters）。
 */
export function partyStarterStatements(
  db: Database,
  userId: string,
  now: string,
) {
  const grantId = crypto.randomUUID();
  const granted = sql`EXISTS (SELECT 1 FROM ${partyProfiles} WHERE ${and(
    eq(partyProfiles.userId, userId),
    eq(partyProfiles.grantId, grantId),
  )})`;
  const starterCards = STARTER_CHARACTERS.flatMap((character) =>
    character.cards.map((skillId, slot) => ({
      id: crypto.randomUUID(),
      characterId: character.characterId,
      slot,
      skillId,
    })),
  );
  return [
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
    // 初めのカードは、印を書けたときだけ、すべてを1つの文で入れる。
    insertRowsWhen(
      db,
      skillCards,
      starterCards.map((card) => ({
        id: card.id,
        userId,
        skillId: card.skillId,
        multiplierPercent: MIN_MULTIPLIER_PERCENT,
        acquiredAt: now,
        awakenedBranch: null,
      })),
      granted,
    ),
    insertRowsWhen(
      db,
      partyCharacterCards,
      starterCards.map((card) => ({
        userId,
        characterId: card.characterId,
        slot: card.slot,
        cardId: card.id,
        itemId: null,
        updatedAt: now,
      })),
      granted,
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
  ];
}
