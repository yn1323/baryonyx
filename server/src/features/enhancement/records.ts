import { and, eq, type SQL, sql } from "drizzle-orm";
import { type Database, type Statement, selectWhen } from "../../shared/db.js";
import { MIN_MULTIPLIER_PERCENT } from "../../shared/quality.js";
import { runeRecord } from "../exercise-rewards/wallet.js";
import {
  bitOf,
  type CharacterId,
  UNIQUE_UNLOCK_LEVELS,
} from "../party/catalog.js";
import { partyCharacters, partyUniqueSkills } from "../party/db-schema.js";
import { uniqueStateOf } from "../party/state.js";
import { MATERIAL_IDS, type MaterialId } from "./catalog.js";
import { enhancementMaterials, enhancementRecords } from "./db-schema.js";

export type RecordRow = typeof enhancementRecords.$inferSelect;

// 品質強化と覚醒に共通の、記録・素材・固有スキルの読み書き。どちらも、要求IDの記録・素材・対象を1回の往復で読み、
// 書き込みと、書いたあとの記録・素材・対象の読み込みをもう1回の往復で済ませる。
export function createRecords(db: Database) {
  const findRecord = (userId: string, requestId: string) =>
    db
      .select()
      .from(enhancementRecords)
      .where(
        and(
          eq(enhancementRecords.userId, userId),
          eq(enhancementRecords.requestId, requestId),
        ),
      );

  const materialsRead = (userId: string) =>
    db
      .select({
        materialId: enhancementMaterials.materialId,
        count: enhancementMaterials.count,
      })
      .from(enhancementMaterials)
      .where(eq(enhancementMaterials.userId, userId));

  // 持っている強化素材。定義の順に、持っていない種類も0で返す。
  const materialsOf = (
    rows: readonly { materialId: string; count: number }[],
  ) =>
    MATERIAL_IDS.map((id) => ({
      id,
      count: rows.find((row) => row.materialId === id)?.count ?? 0,
    }));

  const materialCount = (userId: string, materialId: MaterialId) =>
    sql`COALESCE((SELECT ${enhancementMaterials.count} FROM ${enhancementMaterials} WHERE ${enhancementMaterials.userId} = ${userId} AND ${enhancementMaterials.materialId} = ${materialId}), 0)`;

  /**
   * 記録・ルーンの減算・素材の減算・対象の書き換えを、残高と素材の数と対象の今の状態（current）を条件にした
   * 1回のbatchで行う文。
   */
  function recordStatements(
    userId: string,
    record: Omit<
      typeof enhancementRecords.$inferInsert,
      "id" | "userId" | "balanceAfter" | "createdAt"
    > & { materialId: MaterialId },
    current: SQL,
    targetWrites: (written: SQL) => Statement[],
    now: string,
  ): Statement[] {
    const pay = runeRecord(db, userId, enhancementRecords, now);
    return [
      db
        .insert(enhancementRecords)
        .select(
          db
            .select({
              id: sql<string>`${pay.id}`.as("id"),
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
                pay.affords(record.runes),
                sql`${materialCount(userId, record.materialId)} >= ${record.materialCount}`,
                current,
              ),
            ),
        )
        // 同時に届いた同じ要求IDの再送では書かない。
        .onConflictDoNothing(),
      pay.debit(record.runes),
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
            pay.written,
          ),
        ),
      ...targetWrites(pay.written),
      pay.settle,
    ];
  }

  // 書けなかった理由。素材が足りない、対象が先に変わった、ルーンが足りない、の順に見分ける。
  function reason(
    materials: ReturnType<typeof materialsOf>,
    materialId: MaterialId,
    need: number,
    unchanged: boolean,
  ) {
    const held = materials.find((entry) => entry.id === materialId)?.count ?? 0;
    if (held < need) return { error: "insufficient_materials" as const };
    if (!unchanged) return { error: "target_changed" as const };
    return { error: "insufficient_runes" as const };
  }

  // 固有スキルの持ち主のキャラ（Lv）と、固有スキルの行を読む文。
  const uniqueReads = (userId: string, characterId: string, index: number) =>
    [
      db
        .select({ level: partyCharacters.level })
        .from(partyCharacters)
        .where(
          and(
            eq(partyCharacters.userId, userId),
            eq(partyCharacters.characterId, characterId),
          ),
        ),
      db
        .select({
          setStage: partyUniqueSkills.setStage,
          acquiredMask: partyUniqueSkills.acquiredMask,
          multiplierPercent: partyUniqueSkills.multiplierPercent,
        })
        .from(partyUniqueSkills)
        .where(uniqueWhere(userId, characterId, index)),
    ] as const;

  // 固有スキルを確かめる。持っていないキャラと、解放するLvに届いていない固有スキルは扱わない。
  function uniqueOf(
    characterId: CharacterId,
    index: number,
    [characters, uniques]: [
      { level: number }[],
      Parameters<typeof uniqueStateOf>[2][],
    ],
  ) {
    const character = characters[0];
    if (!character) return { error: "character_not_owned" as const };
    if (character.level < UNIQUE_UNLOCK_LEVELS[index]) {
      return { error: "unique_locked" as const };
    }
    return { unique: uniqueStateOf(characterId, index, uniques[0]) };
  }

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
        selectWhen(
          db,
          written,
        )({
          userId: sql<string>`${userId}`.as("user_id"),
          characterId: sql<string>`${characterId}`.as("character_id"),
          uniqueIndex: sql<number>`${index}`.as("unique_index"),
          setStage: sql<number>`0`.as("set_stage"),
          acquiredMask: sql<number>`${bitOf(0)}`.as("acquired_mask"),
          multiplierPercent: sql<number>`${MIN_MULTIPLIER_PERCENT}`.as(
            "multiplier_percent",
          ),
          updatedAt: sql<string>`${now}`.as("updated_at"),
        }),
      )
      .onConflictDoNothing();
  }

  return {
    findRecord,
    materialsRead,
    materialsOf,
    recordStatements,
    reason,
    uniqueReads,
    uniqueOf,
    uniqueWhere,
    uniqueColumn,
    unlocked,
    uniqueRowWhen,
  };
}

export type Records = ReturnType<typeof createRecords>;

// 固有スキルの記録の target。
export const uniqueTarget = (characterId: string, index: number) =>
  `${characterId}:${index}`;
