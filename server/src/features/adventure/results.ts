import { and, eq, type SQL, sql } from "drizzle-orm";
import type { BatchResponse } from "drizzle-orm/batch";
import { type Database, selectWhen } from "../../shared/db.js";
import { adventureRecords, adventureRuns } from "./db-schema.js";
import {
  type DropRow,
  dropOf,
  dropsOfRun,
  type EndStatus,
  type RunRow,
} from "./state.js";

// 冒険を終えたときの記録と結果。最奥のボスを倒したとき（rooms.ts）と、負けて帰還する・やめるとき（run.ts）が使う。

/** 冒険の記録を、終えた冒険が条件どおり書き込まれたときだけ更新する文。 */
export function recordStatement(
  db: Database,
  run: RunRow,
  status: EndStatus,
  now: string,
  written: SQL,
) {
  return db
    .insert(adventureRecords)
    .select(
      selectWhen(
        db,
        written,
      )({
        userId: sql<string>`${run.userId}`.as("user_id"),
        destinationId: sql<string>`${run.destinationId}`.as("destination_id"),
        // 1階ずつしか進まないため、今いる階がこの冒険で着いた最も深い階になる。
        bestFloor: sql<number>`${run.floor}`.as("best_floor"),
        clears: sql<number>`${status === "cleared" ? 1 : 0}`.as("clears"),
        updatedAt: sql<string>`${now}`.as("updated_at"),
      }),
    )
    .onConflictDoUpdate({
      target: [adventureRecords.userId, adventureRecords.destinationId],
      set: {
        bestFloor: sql`MAX(${adventureRecords.bestFloor}, excluded.best_floor)`,
        clears: sql`${adventureRecords.clears} + excluded.clears`,
        updatedAt: sql`excluded.updated_at`,
      },
    });
}

/** 終えた冒険の行・行き先の記録・落ちた物を読む文。終えたあとの状態を、書き込みと同じ batch で読む。 */
export function resultReads(db: Database, run: RunRow) {
  return [
    db.select().from(adventureRuns).where(eq(adventureRuns.id, run.id)),
    db
      .select()
      .from(adventureRecords)
      .where(
        and(
          eq(adventureRecords.userId, run.userId),
          eq(adventureRecords.destinationId, run.destinationId),
        ),
      ),
    dropsOfRun(db, run.id),
  ] as const;
}

/** batch の最後に並べた resultReads の結果を取り出す。 */
export const resultRowsOf = (results: readonly unknown[]) =>
  results.slice(-3) as unknown as BatchResponse<ReturnType<typeof resultReads>>;

/** その行き先のこれまでの最深到達の階。記録がなければ0。 */
export const bestFloorOf = (
  records: readonly { destinationId: string; bestFloor: number }[],
  destinationId: string,
) =>
  records.find((record) => record.destinationId === destinationId)?.bestFloor ??
  0;

/** 冒険を終えた結果。最深到達の階と記録の更新、この冒険で手に入れた物。 */
export function resultOf(
  ended: RunRow,
  record: typeof adventureRecords.$inferSelect | undefined,
  drops: readonly DropRow[],
  previousBest: number,
) {
  const floor = ended.floor;
  return {
    runId: ended.id,
    destinationId: ended.destinationId,
    status: ended.status,
    floor,
    bestFloor: record?.bestFloor ?? floor,
    newRecord: floor > previousBest,
    clears: record?.clears ?? 0,
    revives: ended.revives,
    drops: drops.map(dropOf),
  };
}
