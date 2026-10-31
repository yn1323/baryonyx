import { and, eq, type SQL, sql } from "drizzle-orm";
import { batchThenRead, type Database } from "../../shared/db.js";
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
import {
  type CardSkillId,
  maxMultiplierPercent as skillMaxPercent,
  uniqueMaxPercent,
} from "../party/catalog.js";
import { partyUniqueSkills, skillCards } from "../party/db-schema.js";
import { POLISHES, polishCeilingPercent, polishedPercent } from "./catalog.js";
import { type RecordRow, type Records, uniqueTarget } from "./records.js";
import type { Polish } from "./schema.js";

export function createPolishOperation(db: Database, records: Records) {
  // 品質強化する物を読む文。固有スキルは持ち主のキャラ（Lv）と固有スキルの行、スキル・装備は持ち物の行。
  function targetReads(userId: string, request: Polish) {
    if (request.category === "unique") {
      return records.uniqueReads(userId, request.characterId, request.index);
    }
    return request.category === "skill"
      ? ([
          db
            .select({
              entryId: skillCards.skillId,
              multiplierPercent: skillCards.multiplierPercent,
            })
            .from(skillCards)
            .where(
              and(
                eq(skillCards.userId, userId),
                eq(skillCards.id, request.itemId),
              ),
            ),
        ] as const)
      : ([
          db
            .select({
              entryId: equipmentItems.equipmentId,
              multiplierPercent: equipmentItems.multiplierPercent,
            })
            .from(equipmentItems)
            .where(
              and(
                eq(equipmentItems.userId, userId),
                eq(equipmentItems.id, request.itemId),
              ),
            ),
        ] as const);
  }

  // 品質強化する物の今の倍率と上限、記録の target、書く前に今の倍率を確かめる条件、倍率を書き換える文。
  function targetOf(
    userId: string,
    request: Polish,
    rows: readonly unknown[],
    now: string,
  ) {
    if (request.category === "unique") {
      const { characterId, index } = request;
      const found = records.uniqueOf(
        characterId,
        index,
        rows as Parameters<Records["uniqueOf"]>[2],
      );
      if (found.error !== undefined) return { error: found.error };
      const percent =
        (rows[1] as { multiplierPercent: number }[])[0]?.multiplierPercent ??
        MIN_MULTIPLIER_PERCENT;
      return {
        percent,
        maxPercent: uniqueMaxPercent(characterId, index),
        target: uniqueTarget(characterId, index),
        current: and(
          sql`${records.uniqueColumn(userId, characterId, index, sql`${partyUniqueSkills.multiplierPercent}`, MIN_MULTIPLIER_PERCENT)} = ${percent}`,
          records.unlocked(userId, characterId, index),
        ) as SQL,
        write: (after: number, written: SQL) => [
          // 覚醒していない固有スキルは行がないため、最初の段階の行を作ってから倍率を書く。
          records.uniqueRowWhen(userId, characterId, index, now, written),
          db
            .update(partyUniqueSkills)
            .set({ multiplierPercent: after, updatedAt: now })
            .where(
              and(records.uniqueWhere(userId, characterId, index), written),
            ),
        ],
      };
    }
    const row = (
      rows[0] as { entryId: string; multiplierPercent: number }[]
    )[0];
    if (request.category === "skill") {
      if (!row) return { error: "target_not_found" as const };
      return {
        percent: row.multiplierPercent,
        maxPercent: skillMaxPercent(row.entryId as CardSkillId),
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
    if (!row || slotOf(row.entryId) !== request.category) {
      return { error: "target_not_found" as const };
    }
    return {
      percent: row.multiplierPercent,
      maxPercent: equipmentMaxPercent(row.entryId as EquipmentId),
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

  // 品質強化の応答。倍率と品質は、記録の前後の倍率と、対象の種類の上限から求める。
  function polished(
    row: RecordRow,
    maxPercent: number,
    materials: ReturnType<Records["materialsOf"]>,
  ) {
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
      materials,
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

  // 1つの物の倍率を、磨き石とルーンで上げる（doc/features/enhancement.md の品質強化）。色の上限を超える分は上限で止め、
  // 色の上限に届いた物と、最後の1つが何も上げない数は受け付けない。要求IDで再送を見分け、二重に使わない。
  async function polish(userId: string, request: Polish, now: string) {
    const [found, held, ...rows] = await db.batch([
      records.findRecord(userId, request.requestId),
      records.materialsRead(userId),
      ...targetReads(userId, request),
    ]);
    const replay = found[0];
    const target = targetOf(userId, request, rows, now);
    if (replay) {
      if (!samePolish(request, replay)) {
        return { error: "request_conflict" as const };
      }
      if (target.error !== undefined) return { error: target.error };
      return polished(replay, target.maxPercent, records.materialsOf(held));
    }
    if (target.error !== undefined) return { error: target.error };
    const { percent, maxPercent } = target;
    if (percent >= maxPercent) return { error: "already_max" as const };
    const ceiling = polishCeilingPercent(request.materialId, maxPercent);
    if (percent >= ceiling) return { error: "polish_ceiling" as const };
    if (
      request.count > 1 &&
      polishedPercent(
        percent,
        request.materialId,
        request.count - 1,
        maxPercent,
      ) >= ceiling
    ) {
      return { error: "too_many_materials" as const };
    }
    const after = polishedPercent(
      percent,
      request.materialId,
      request.count,
      maxPercent,
    );
    const statements = records.recordStatements(
      userId,
      {
        requestId: request.requestId,
        kind: "polish",
        category: request.category,
        target: target.target,
        materialId: request.materialId,
        materialCount: request.count,
        before: percent,
        after,
        runes: POLISHES[request.materialId].runes * request.count,
      },
      target.current,
      (written) => target.write(after, written),
      now,
    );
    const {
      reads: [written, heldAfter, ...rowsAfter],
    } = await batchThenRead(db, statements, [
      records.findRecord(userId, request.requestId),
      records.materialsRead(userId),
      ...targetReads(userId, request),
    ]);
    const row = written[0];
    const materials = records.materialsOf(heldAfter);
    if (row) return polished(row, maxPercent, materials);
    const again = targetOf(userId, request, rowsAfter, now);
    return records.reason(
      materials,
      request.materialId,
      request.count,
      again.error === undefined && again.percent === percent,
    );
  }

  return { polish };
}
