import { and, eq, isNull, type SQL, sql } from "drizzle-orm";
import { batchThenRead, type Database } from "../../shared/db.js";
import {
  bitOf,
  type CharacterId,
  isAwakenableSkill,
  uniqueParent,
} from "../party/catalog.js";
import { partyUniqueSkills, skillCards } from "../party/db-schema.js";
import { skillCardStateOf } from "../party/state.js";
import {
  AWAKENING_SHARD_ID,
  AWAKENING_SHARDS_PER_STAGE,
  awakeningRunes,
  SKILL_AWAKENING_RUNES,
} from "./catalog.js";
import { type RecordRow, type Records, uniqueTarget } from "./records.js";
import type { Awaken, AwakenSkillCard, AwakenUnique } from "./schema.js";

type Materials = ReturnType<Records["materialsOf"]>;

export function createAwakenOperation(db: Database, records: Records) {
  // 覚醒の応答。覚醒した固有スキルの今の段階・倍率・品質を添える。
  function awakened(
    row: RecordRow,
    characterId: CharacterId,
    index: number,
    found: ReturnType<Records["uniqueOf"]>,
    materials: Materials,
  ) {
    return {
      awakening: {
        characterId,
        index,
        stage: row.after,
        runes: row.runes,
        balanceAfter: row.balanceAfter,
      },
      ...(found.error === undefined ? { unique: found.unique } : {}),
      materials,
    };
  }

  // 固有スキルか、持っているスキル1枚を覚醒させる。
  async function awaken(userId: string, request: Awaken, now: string) {
    return "cardId" in request
      ? awakenSkillCard(userId, request, now)
      : awakenUnique(userId, request, now);
  }

  const cardRead = (userId: string, cardId: string) =>
    db
      .select({
        id: skillCards.id,
        skillId: skillCards.skillId,
        multiplierPercent: skillCards.multiplierPercent,
        awakenedBranch: skillCards.awakenedBranch,
      })
      .from(skillCards)
      .where(and(eq(skillCards.userId, userId), eq(skillCards.id, cardId)));

  type CardRow = Awaited<ReturnType<typeof cardRead>>[number];

  // 持っているスキル1枚の覚醒の応答。覚醒した1枚の今の状態を添える。
  function skillCardAwakened(
    row: RecordRow,
    card: CardRow | undefined,
    materials: Materials,
  ) {
    return {
      awakening: {
        cardId: row.target,
        branch: row.after,
        runes: row.runes,
        balanceAfter: row.balanceAfter,
      },
      ...(card ? { card: skillCardStateOf(card) } : {}),
      materials,
    };
  }

  // まだ覚醒していない持っているスキル1枚を、覚醒の欠片とルーンで2つの枝のどちらかへ覚醒させる
  // （doc/features/skill-awakening.md）。覚醒は1枚に1回だけで、覚醒した1枚は受け付けない。
  async function awakenSkillCard(
    userId: string,
    request: AwakenSkillCard,
    now: string,
  ) {
    const { cardId, branch } = request;
    const [found, held, cards] = await db.batch([
      records.findRecord(userId, request.requestId),
      records.materialsRead(userId),
      cardRead(userId, cardId),
    ]);
    const replay = found[0];
    if (replay) {
      if (
        replay.kind !== "awaken" ||
        replay.category !== "skill" ||
        replay.target !== cardId ||
        replay.after !== branch
      ) {
        return { error: "request_conflict" as const };
      }
      return skillCardAwakened(replay, cards[0], records.materialsOf(held));
    }
    const card = cards[0];
    if (!card) return { error: "target_not_found" as const };
    if (card.awakenedBranch !== null) {
      return { error: "already_awakened" as const };
    }
    if (!isAwakenableSkill(card.skillId)) {
      return { error: "not_awakenable" as const };
    }
    const notAwakened = and(
      eq(skillCards.userId, userId),
      eq(skillCards.id, cardId),
      isNull(skillCards.awakenedBranch),
    );
    const {
      reads: [written, heldAfter, cardsAfter],
    } = await batchThenRead(
      db,
      records.recordStatements(
        userId,
        {
          requestId: request.requestId,
          kind: "awaken",
          category: "skill",
          target: cardId,
          materialId: AWAKENING_SHARD_ID,
          materialCount: AWAKENING_SHARDS_PER_STAGE,
          before: 0,
          after: branch,
          runes: SKILL_AWAKENING_RUNES,
        },
        sql`EXISTS (SELECT 1 FROM ${skillCards} WHERE ${notAwakened})`,
        (written) => [
          db
            .update(skillCards)
            .set({ awakenedBranch: branch })
            .where(and(notAwakened, written)),
        ],
        now,
      ),
      [
        records.findRecord(userId, request.requestId),
        records.materialsRead(userId),
        cardRead(userId, cardId),
      ],
    );
    const materials = records.materialsOf(heldAfter);
    const row = written[0];
    if (row) return skillCardAwakened(row, cardsAfter[0], materials);
    return records.reason(
      materials,
      AWAKENING_SHARD_ID,
      AWAKENING_SHARDS_PER_STAGE,
      cardsAfter[0] !== undefined && cardsAfter[0].awakenedBranch === null,
    );
  }

  // 固有スキルを、まだ取得していない段階へ覚醒の欠片とルーンで1段進める（doc/features/enhancement.md の覚醒）。
  // 冒険中の覚醒と違い、セットしている段階によらず、1つ手前の段階を取得していれば選べる。覚醒した段階をセットする。
  async function awakenUnique(
    userId: string,
    request: AwakenUnique,
    now: string,
  ) {
    const { characterId, index, stage } = request;
    const target = uniqueTarget(characterId, index);
    const [found, held, ...uniqueRows] = await db.batch([
      records.findRecord(userId, request.requestId),
      records.materialsRead(userId),
      ...records.uniqueReads(userId, characterId, index),
    ]);
    const unique = records.uniqueOf(characterId, index, uniqueRows);
    const replay = found[0];
    if (replay) {
      if (
        replay.kind !== "awaken" ||
        replay.category !== "unique" ||
        replay.target !== target ||
        replay.after !== stage
      ) {
        return { error: "request_conflict" as const };
      }
      return awakened(
        replay,
        characterId,
        index,
        unique,
        records.materialsOf(held),
      );
    }
    if (unique.error !== undefined) return { error: unique.error };
    if (unique.unique.acquired.includes(stage)) {
      return { error: "stage_acquired" as const };
    }
    const parent = uniqueParent(stage) as number;
    if (!unique.unique.acquired.includes(parent)) {
      return { error: "parent_not_acquired" as const };
    }
    const mask = records.uniqueColumn(
      userId,
      characterId,
      index,
      sql`${partyUniqueSkills.acquiredMask}`,
      bitOf(0),
    );
    const {
      reads: [written, heldAfter, ...uniqueRowsAfter],
    } = await batchThenRead(
      db,
      records.recordStatements(
        userId,
        {
          requestId: request.requestId,
          kind: "awaken",
          category: "unique",
          target,
          materialId: AWAKENING_SHARD_ID,
          materialCount: AWAKENING_SHARDS_PER_STAGE,
          before: unique.unique.set,
          after: stage,
          runes: awakeningRunes(stage),
        },
        and(
          sql`(${mask} & ${bitOf(parent)}) <> 0`,
          sql`(${mask} & ${bitOf(stage)}) = 0`,
          records.unlocked(userId, characterId, index),
        ) as SQL,
        (written) => [
          records.uniqueRowWhen(userId, characterId, index, now, written),
          db
            .update(partyUniqueSkills)
            .set({
              setStage: stage,
              acquiredMask: sql`${partyUniqueSkills.acquiredMask} | ${bitOf(stage)}`,
              updatedAt: now,
            })
            .where(
              and(records.uniqueWhere(userId, characterId, index), written),
            ),
        ],
        now,
      ),
      [
        records.findRecord(userId, request.requestId),
        records.materialsRead(userId),
        ...records.uniqueReads(userId, characterId, index),
      ],
    );
    const materials = records.materialsOf(heldAfter);
    const after = records.uniqueOf(characterId, index, uniqueRowsAfter);
    const row = written[0];
    if (row) return awakened(row, characterId, index, after, materials);
    return records.reason(
      materials,
      AWAKENING_SHARD_ID,
      AWAKENING_SHARDS_PER_STAGE,
      after.error === undefined && !after.unique.acquired.includes(stage),
    );
  }

  return { awaken };
}
