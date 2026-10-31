import { sql } from "drizzle-orm";
import { batchThenRead, type Database } from "../../shared/db.js";
import { materialGrants } from "../inventory/grants.js";
import { COMBINE_INPUTS, COMBINES } from "./catalog.js";
import type { RecordRow, Records } from "./records.js";
import type { Combine } from "./schema.js";

export function createCombineOperation(db: Database, records: Records) {
  // 石の合成の応答。target は作った色、materialId は使った色、after は作った数。
  function combined(
    row: RecordRow,
    materials: ReturnType<Records["materialsOf"]>,
  ) {
    return {
      combine: {
        materialId: row.target,
        from: row.materialId,
        count: row.after,
        runes: row.runes,
        balanceAfter: row.balanceAfter,
      },
      materials,
    };
  }

  // 1つ下の色の磨き石3個とルーンで、上の色を1個作る（doc/features/enhancement.md の磨き石の合成）。
  // count 個まとめて作れる。要求IDで再送を見分け、二重に使わない。
  async function combine(userId: string, request: Combine, now: string) {
    const { materialId, count } = request;
    const { from, runes } = COMBINES[materialId];
    const [found, held] = await db.batch([
      records.findRecord(userId, request.requestId),
      records.materialsRead(userId),
    ]);
    const replay = found[0];
    if (replay) {
      if (
        replay.kind !== "combine" ||
        replay.target !== materialId ||
        replay.after !== count
      ) {
        return { error: "request_conflict" as const };
      }
      return combined(replay, records.materialsOf(held));
    }
    const need = COMBINE_INPUTS * count;
    const {
      reads: [written, heldAfter],
    } = await batchThenRead(
      db,
      records.recordStatements(
        userId,
        {
          requestId: request.requestId,
          kind: "combine",
          category: "material",
          target: materialId,
          materialId: from,
          materialCount: need,
          before: 0,
          after: count,
          runes: runes * count,
        },
        sql`1 = 1`,
        (written) =>
          materialGrants(
            db,
            written,
            userId,
            Array<string>(count).fill(materialId),
            now,
          ),
        now,
      ),
      [
        records.findRecord(userId, request.requestId),
        records.materialsRead(userId),
      ],
    );
    const materials = records.materialsOf(heldAfter);
    const row = written[0];
    if (row) return combined(row, materials);
    return records.reason(materials, from, need, true);
  }

  return { combine };
}
