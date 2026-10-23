import { and, eq, type SQL, sql } from "drizzle-orm";
import type { BatchItem } from "drizzle-orm/batch";
import { createDatabase } from "../../shared/db.js";
import {
  MIN_MULTIPLIER_PERCENT,
  multiplierOf,
  qualityOf,
} from "../../shared/quality.js";
import {
  type EquipmentId,
  maxMultiplierPercent as equipmentMaxPercent,
  slotOf,
} from "../equipment/catalog.js";
import { equipmentItems } from "../equipment/db-schema.js";
import { debitRunes, runeBalance } from "../exercise-rewards/wallet.js";
import {
  type CardSkillId,
  type CharacterId,
  maxMultiplierPercent as skillMaxPercent,
  UNIQUE_UNLOCK_LEVELS,
  uniqueMaxPercent,
  uniqueParent,
} from "../party/catalog.js";
import {
  partyCharacters,
  partyUniqueSkills,
  skillCards,
} from "../party/db-schema.js";
import { createPartyRepository } from "../party/repository.js";
import {
  AWAKENING_SHARD_ID,
  AWAKENING_SHARDS_PER_STAGE,
  awakeningRunes,
  MATERIAL_IDS,
  type MaterialId,
  POLISHES,
  polishedPercent,
} from "./catalog.js";
import { enhancementMaterials, enhancementRecords } from "./db-schema.js";
import type { Awaken, Polish } from "./schema.js";

type Database = ReturnType<typeof createDatabase>;
type RecordRow = typeof enhancementRecords.$inferSelect;

const bitOf = (stage: number) => 1 << stage;

// 固有スキルの記録の target。
const uniqueTarget = (characterId: string, index: number) =>
  `${characterId}:${index}`;

// 落ちた強化素材を持ち物に足す文。written を書けたときだけ足す。ドロップが使う。
export function materialGrants(
  db: Database,
  userId: string,
  materialIds: readonly string[],
  now: string,
  written: SQL,
) {
  const counts = new Map<string, number>();
  for (const id of materialIds) counts.set(id, (counts.get(id) ?? 0) + 1);
  return [...counts].map(([materialId, count]) =>
    db
      .insert(enhancementMaterials)
      .select(
        db
          .select({
            userId: sql<string>`${userId}`.as("user_id"),
            materialId: sql<string>`${materialId}`.as("material_id"),
            count: sql<number>`${count}`.as("count"),
            acquiredAt: sql<string>`${now}`.as("acquired_at"),
            updatedAt: sql<string>`${now}`.as("updated_at"),
          })
          .from(sql`(SELECT 1)`)
          .where(written),
      )
      .onConflictDoUpdate({
        target: [enhancementMaterials.userId, enhancementMaterials.materialId],
        set: {
          count: sql`${enhancementMaterials.count} + excluded.count`,
          updatedAt: sql`excluded.updated_at`,
        },
      }),
  );
}

export function createEnhancementRepository(binding: D1Database) {
  const db = createDatabase(binding);

  // 持っている強化素材。定義の順に、持っていない種類も0で返す。
  async function materials(userId: string) {
    const rows = await db
      .select({
        materialId: enhancementMaterials.materialId,
        count: enhancementMaterials.count,
      })
      .from(enhancementMaterials)
      .where(eq(enhancementMaterials.userId, userId));
    return MATERIAL_IDS.map((id) => ({
      id,
      count: rows.find((row) => row.materialId === id)?.count ?? 0,
    }));
  }

  async function getState(userId: string) {
    return { materials: await materials(userId) };
  }

  function findRecord(userId: string, requestId: string) {
    return db
      .select()
      .from(enhancementRecords)
      .where(
        and(
          eq(enhancementRecords.userId, userId),
          eq(enhancementRecords.requestId, requestId),
        ),
      )
      .get();
  }

  const materialCount = (userId: string, materialId: MaterialId) =>
    sql`COALESCE((SELECT ${enhancementMaterials.count} FROM ${enhancementMaterials} WHERE ${enhancementMaterials.userId} = ${userId} AND ${enhancementMaterials.materialId} = ${materialId}), 0)`;

  const uniqueWhere = (userId: string, characterId: string, index: number) =>
    and(
      eq(partyUniqueSkills.userId, userId),
      eq(partyUniqueSkills.characterId, characterId),
      eq(partyUniqueSkills.uniqueIndex, index),
    );

  // 固有スキルの行の列。行がなければ（覚醒も品質強化もしていない）fallback。
  const uniqueColumn = (
    userId: string,
    characterId: string,
    index: number,
    column: SQL,
    fallback: number,
  ) =>
    sql`COALESCE((SELECT ${column} FROM ${partyUniqueSkills} WHERE ${partyUniqueSkills.userId} = ${userId} AND ${partyUniqueSkills.characterId} = ${characterId} AND ${partyUniqueSkills.uniqueIndex} = ${index}), ${fallback})`;

  const unlocked = (userId: string, characterId: string, index: number) =>
    sql`EXISTS (SELECT 1 FROM ${partyCharacters} WHERE ${partyCharacters.userId} = ${userId} AND ${partyCharacters.characterId} = ${characterId} AND ${partyCharacters.level} >= ${UNIQUE_UNLOCK_LEVELS[index]})`;

  // 固有スキルを確かめる。持っていないキャラと、解放するLvに届いていない固有スキルは扱わない。
  async function findUnique(
    userId: string,
    characterId: CharacterId,
    index: number,
    now: string,
  ) {
    const state = await createPartyRepository(binding).getState(userId, now);
    const character = state.characters.find(
      (entry) => entry.id === characterId,
    );
    if (!character) return { error: "character_not_owned" as const };
    if (character.level < UNIQUE_UNLOCK_LEVELS[index]) {
      return { error: "unique_locked" as const };
    }
    return { unique: character.uniques[index] };
  }

  // 品質強化する物の今の倍率と上限、記録の target、書く前に今の倍率を確かめる条件、倍率を書き換える文。
  async function polishTarget(userId: string, request: Polish, now: string) {
    if (request.category === "unique") {
      const { characterId, index } = request;
      const found = await findUnique(userId, characterId, index, now);
      if (found.error !== undefined) return { error: found.error };
      const row = await db
        .select({ multiplierPercent: partyUniqueSkills.multiplierPercent })
        .from(partyUniqueSkills)
        .where(uniqueWhere(userId, characterId, index))
        .get();
      const percent = row?.multiplierPercent ?? MIN_MULTIPLIER_PERCENT;
      return {
        percent,
        maxPercent: uniqueMaxPercent(characterId, index),
        target: uniqueTarget(characterId, index),
        current: and(
          sql`${uniqueColumn(userId, characterId, index, sql`${partyUniqueSkills.multiplierPercent}`, MIN_MULTIPLIER_PERCENT)} = ${percent}`,
          unlocked(userId, characterId, index),
        ) as SQL,
        write: (after: number, written: SQL) => [
          // 覚醒していない固有スキルは行がないため、最初の段階の行を作ってから倍率を書く。
          uniqueRowWhen(userId, characterId, index, now, written),
          db
            .update(partyUniqueSkills)
            .set({ multiplierPercent: after, updatedAt: now })
            .where(and(uniqueWhere(userId, characterId, index), written)),
        ],
      };
    }
    if (request.category === "skill") {
      const row = await db
        .select({
          skillId: skillCards.skillId,
          multiplierPercent: skillCards.multiplierPercent,
        })
        .from(skillCards)
        .where(
          and(eq(skillCards.userId, userId), eq(skillCards.id, request.itemId)),
        )
        .get();
      if (!row) return { error: "target_not_found" as const };
      return {
        percent: row.multiplierPercent,
        maxPercent: skillMaxPercent(row.skillId as CardSkillId),
        target: request.itemId,
        current: sql`EXISTS (SELECT 1 FROM ${skillCards} WHERE ${skillCards.userId} = ${userId} AND ${skillCards.id} = ${request.itemId} AND ${skillCards.multiplierPercent} = ${row.multiplierPercent})`,
        write: (after: number, written: SQL) => [
          db
            .update(skillCards)
            .set({ multiplierPercent: after })
            .where(
              and(
                eq(skillCards.userId, userId),
                eq(skillCards.id, request.itemId),
                written,
              ),
            ),
        ],
      };
    }
    const row = await db
      .select({
        equipmentId: equipmentItems.equipmentId,
        multiplierPercent: equipmentItems.multiplierPercent,
      })
      .from(equipmentItems)
      .where(
        and(
          eq(equipmentItems.userId, userId),
          eq(equipmentItems.id, request.itemId),
        ),
      )
      .get();
    if (!row || slotOf(row.equipmentId) !== request.category) {
      return { error: "target_not_found" as const };
    }
    return {
      percent: row.multiplierPercent,
      maxPercent: equipmentMaxPercent(row.equipmentId as EquipmentId),
      target: request.itemId,
      current: sql`EXISTS (SELECT 1 FROM ${equipmentItems} WHERE ${equipmentItems.userId} = ${userId} AND ${equipmentItems.id} = ${request.itemId} AND ${equipmentItems.multiplierPercent} = ${row.multiplierPercent})`,
      write: (after: number, written: SQL) => [
        db
          .update(equipmentItems)
          .set({ multiplierPercent: after })
          .where(
            and(
              eq(equipmentItems.userId, userId),
              eq(equipmentItems.id, request.itemId),
              written,
            ),
          ),
      ],
    };
  }

  // 固有スキルの行がなければ、最初の段階だけを取得した×1.00の行を作る文。written を書けたときだけ作る。
  function uniqueRowWhen(
    userId: string,
    characterId: string,
    index: number,
    now: string,
    written: SQL,
  ) {
    return db
      .insert(partyUniqueSkills)
      .select(
        db
          .select({
            userId: sql<string>`${userId}`.as("user_id"),
            characterId: sql<string>`${characterId}`.as("character_id"),
            uniqueIndex: sql<number>`${index}`.as("unique_index"),
            setStage: sql<number>`0`.as("set_stage"),
            acquiredMask: sql<number>`${bitOf(0)}`.as("acquired_mask"),
            multiplierPercent: sql<number>`${MIN_MULTIPLIER_PERCENT}`.as(
              "multiplier_percent",
            ),
            updatedAt: sql<string>`${now}`.as("updated_at"),
          })
          .from(sql`(SELECT 1)`)
          .where(written),
      )
      .onConflictDoNothing();
  }

  // 記録・ルーンの減算・素材の減算・対象の書き換えを、残高と素材の数と対象の今の状態を条件にした1回のbatchで行う。
  async function write(
    userId: string,
    record: Omit<
      typeof enhancementRecords.$inferInsert,
      "id" | "userId" | "balanceAfter" | "createdAt"
    > & { materialId: MaterialId },
    current: SQL,
    targetWrites: (written: SQL) => BatchItem<"sqlite">[],
    now: string,
  ) {
    const id = crypto.randomUUID();
    const balance = runeBalance(userId);
    const written = sql`EXISTS (SELECT 1 FROM ${enhancementRecords} WHERE ${enhancementRecords.id} = ${id})`;
    await db.batch([
      db
        .insert(enhancementRecords)
        .select(
          db
            .select({
              id: sql<string>`${id}`.as("id"),
              userId: sql<string>`${userId}`.as("user_id"),
              requestId: sql<string>`${record.requestId}`.as("request_id"),
              kind: sql<string>`${record.kind}`.as("kind"),
              category: sql<string>`${record.category}`.as("category"),
              target: sql<string>`${record.target}`.as("target"),
              materialId: sql<string>`${record.materialId}`.as("material_id"),
              materialCount: sql<number>`${record.materialCount}`.as(
                "material_count",
              ),
              before: sql<number>`${record.before}`.as("before"),
              after: sql<number>`${record.after}`.as("after"),
              runes: sql<number>`${record.runes}`.as("runes"),
              balanceAfter: sql<number>`0`.as("balance_after"),
              createdAt: sql<string>`${now}`.as("created_at"),
            })
            .from(sql`(SELECT 1)`)
            .where(
              and(
                sql`${balance} >= ${record.runes}`,
                sql`${materialCount(userId, record.materialId)} >= ${record.materialCount}`,
                current,
              ),
            ),
        )
        // 同時に届いた同じ要求IDの再送では書かない。
        .onConflictDoNothing(),
      debitRunes(db, userId, record.runes, written, now),
      db
        .update(enhancementMaterials)
        .set({
          count: sql`${enhancementMaterials.count} - ${record.materialCount}`,
          updatedAt: now,
        })
        .where(
          and(
            eq(enhancementMaterials.userId, userId),
            eq(enhancementMaterials.materialId, record.materialId),
            written,
          ),
        ),
      ...targetWrites(written),
      db
        .update(enhancementRecords)
        .set({ balanceAfter: sql`COALESCE(${balance}, 0)` })
        .where(eq(enhancementRecords.id, id)),
    ]);
    return findRecord(userId, record.requestId);
  }

  // 品質強化の応答。倍率と品質は、記録の前後の倍率と、対象の種類の上限から求める。
  async function polished(userId: string, row: RecordRow, maxPercent: number) {
    return {
      enhancement: {
        kind: row.kind,
        category: row.category,
        target: row.target,
        materialId: row.materialId,
        count: row.materialCount,
        before: {
          multiplier: multiplierOf(row.before),
          quality: qualityOf(row.before, maxPercent),
        },
        multiplier: multiplierOf(row.after),
        quality: qualityOf(row.after, maxPercent),
        runes: row.runes,
        balanceAfter: row.balanceAfter,
      },
      materials: await materials(userId),
    };
  }

  const polishTargetOf = (request: Polish) =>
    request.category === "unique"
      ? uniqueTarget(request.characterId, request.index)
      : request.itemId;

  const samePolish = (request: Polish, row: RecordRow) =>
    row.kind === "polish" &&
    row.category === request.category &&
    row.target === polishTargetOf(request) &&
    row.materialId === request.materialId &&
    row.materialCount === request.count;

  // 1つの物の倍率を、磨き石とルーンで上げる（doc/features/enhancement.md の品質強化）。上限を超える分は上限で止め、
  // 最後の1つが何も上げない数は受け付けない。要求IDで再送を見分け、二重に使わない。
  async function polish(userId: string, request: Polish, now: string) {
    const replay = await findRecord(userId, request.requestId);
    const found = await polishTarget(userId, request, now);
    if (replay) {
      if (!samePolish(request, replay)) {
        return { error: "request_conflict" as const };
      }
      if (found.error !== undefined) return { error: found.error };
      return polished(userId, replay, found.maxPercent);
    }
    if (found.error !== undefined) return { error: found.error };
    const { percent, maxPercent } = found;
    if (percent >= maxPercent) return { error: "already_max" as const };
    if (
      request.count > 1 &&
      polishedPercent(
        percent,
        request.materialId,
        request.count - 1,
        maxPercent,
      ) >= maxPercent
    ) {
      return { error: "too_many_materials" as const };
    }
    const after = polishedPercent(
      percent,
      request.materialId,
      request.count,
      maxPercent,
    );
    const row = await write(
      userId,
      {
        requestId: request.requestId,
        kind: "polish",
        category: request.category,
        target: found.target,
        materialId: request.materialId,
        materialCount: request.count,
        before: percent,
        after,
        runes: POLISHES[request.materialId].runes * request.count,
      },
      found.current,
      (written) => found.write(after, written),
      now,
    );
    if (row) return polished(userId, row, maxPercent);
    return reason(userId, request.materialId, request.count, async () => {
      const again = await polishTarget(userId, request, now);
      return again.error === undefined && again.percent === percent;
    });
  }

  // 書けなかった理由。素材が足りない、対象が先に変わった、ルーンが足りない、の順に見分ける。
  async function reason(
    userId: string,
    materialId: MaterialId,
    need: number,
    unchanged: () => Promise<boolean>,
  ) {
    const held =
      (await materials(userId)).find((entry) => entry.id === materialId)
        ?.count ?? 0;
    if (held < need) return { error: "insufficient_materials" as const };
    if (!(await unchanged())) return { error: "target_changed" as const };
    return { error: "insufficient_runes" as const };
  }

  // 覚醒の応答。覚醒した固有スキルの今の段階・倍率・品質を添える。
  async function awakened(
    userId: string,
    row: RecordRow,
    characterId: CharacterId,
    index: number,
    now: string,
  ) {
    const found = await findUnique(userId, characterId, index, now);
    return {
      awakening: {
        characterId,
        index,
        stage: row.after,
        runes: row.runes,
        balanceAfter: row.balanceAfter,
      },
      ...(found.error === undefined ? { unique: found.unique } : {}),
      materials: await materials(userId),
    };
  }

  // 固有スキルを、まだ取得していない段階へ覚醒の欠片とルーンで1段進める（doc/features/enhancement.md の覚醒）。
  // 冒険中の覚醒と違い、セットしている段階によらず、1つ手前の段階を取得していれば選べる。覚醒した段階をセットする。
  async function awaken(userId: string, request: Awaken, now: string) {
    const { characterId, index, stage } = request;
    const target = uniqueTarget(characterId, index);
    const replay = await findRecord(userId, request.requestId);
    if (replay) {
      if (
        replay.kind !== "awaken" ||
        replay.target !== target ||
        replay.after !== stage
      ) {
        return { error: "request_conflict" as const };
      }
      return awakened(userId, replay, characterId, index, now);
    }
    const found = await findUnique(userId, characterId, index, now);
    if (found.error !== undefined) return { error: found.error };
    if (found.unique.acquired.includes(stage)) {
      return { error: "stage_acquired" as const };
    }
    const parent = uniqueParent(stage) as number;
    if (!found.unique.acquired.includes(parent)) {
      return { error: "parent_not_acquired" as const };
    }
    const mask = uniqueColumn(
      userId,
      characterId,
      index,
      sql`${partyUniqueSkills.acquiredMask}`,
      bitOf(0),
    );
    const row = await write(
      userId,
      {
        requestId: request.requestId,
        kind: "awaken",
        category: "unique",
        target,
        materialId: AWAKENING_SHARD_ID,
        materialCount: AWAKENING_SHARDS_PER_STAGE,
        before: found.unique.set,
        after: stage,
        runes: awakeningRunes(stage),
      },
      and(
        sql`(${mask} & ${bitOf(parent)}) <> 0`,
        sql`(${mask} & ${bitOf(stage)}) = 0`,
        unlocked(userId, characterId, index),
      ) as SQL,
      (written) => [
        uniqueRowWhen(userId, characterId, index, now, written),
        db
          .update(partyUniqueSkills)
          .set({
            setStage: stage,
            acquiredMask: sql`${partyUniqueSkills.acquiredMask} | ${bitOf(stage)}`,
            updatedAt: now,
          })
          .where(and(uniqueWhere(userId, characterId, index), written)),
      ],
      now,
    );
    if (row) return awakened(userId, row, characterId, index, now);
    return reason(
      userId,
      AWAKENING_SHARD_ID,
      AWAKENING_SHARDS_PER_STAGE,
      async () => {
        const again = await findUnique(userId, characterId, index, now);
        return (
          again.error === undefined && !again.unique.acquired.includes(stage)
        );
      },
    );
  }

  return { getState, polish, awaken };
}
