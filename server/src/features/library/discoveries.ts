import { type SQL, sql } from "drizzle-orm";
import type { createDatabase } from "../../shared/db.js";
import type { DiscoveryKind } from "./catalog.js";
import { libraryDiscoveries } from "./db-schema.js";

type Database = ReturnType<typeof createDatabase>;

// ほかの機能が、物を付与したり戦闘を終えたりする書き込みと同じ batch に入れる文。
// D1は1つの文に100個までしか値を渡せないため、1つの文に入れる行の数を抑える。
const ROWS_PER_STATEMENT = 20;

function chunks<T>(items: readonly T[]) {
  const result: T[][] = [];
  for (let start = 0; start < items.length; start += ROWS_PER_STATEMENT) {
    result.push(items.slice(start, start + ROWS_PER_STATEMENT));
  }
  return result;
}

/** 見つけた物を記録する文。記録済みの物は、初めて見つけた日時のまま残す。 */
export function discoveryStatements(
  db: Database,
  userId: string,
  kind: Exclude<DiscoveryKind, "enemy">,
  entryIds: readonly string[],
  now: string,
) {
  return chunks([...new Set(entryIds)]).map((ids) =>
    db
      .insert(libraryDiscoveries)
      .values(
        ids.map((entryId) => ({ userId, kind, entryId, discoveredAt: now })),
      )
      .onConflictDoNothing(),
  );
}

export type EnemyEncounter = { enemyId: string; defeated: number };

/**
 * 戦闘で遭遇した敵と倒した体数を記録する文。冒険の部屋を終えた記録（written）を書けたときだけ足すため、
 * 同じ知らせを送り直しても倒した数を二重に数えない。
 */
export function enemyStatements(
  db: Database,
  userId: string,
  enemies: readonly EnemyEncounter[],
  now: string,
  written: SQL,
) {
  const defeated = new Map<string, number>();
  for (const enemy of enemies) {
    defeated.set(
      enemy.enemyId,
      (defeated.get(enemy.enemyId) ?? 0) + enemy.defeated,
    );
  }
  return [...defeated].map(([enemyId, count]) =>
    db
      .insert(libraryDiscoveries)
      .select(
        db
          .select({
            userId: sql<string>`${userId}`.as("user_id"),
            kind: sql<string>`'enemy'`.as("kind"),
            entryId: sql<string>`${enemyId}`.as("entry_id"),
            discoveredAt: sql<string>`${now}`.as("discovered_at"),
            defeated: sql<number>`${count}`.as("defeated"),
          })
          .from(sql`(SELECT 1)`)
          .where(written),
      )
      .onConflictDoUpdate({
        target: [
          libraryDiscoveries.userId,
          libraryDiscoveries.kind,
          libraryDiscoveries.entryId,
        ],
        set: {
          defeated: sql`${libraryDiscoveries.defeated} + excluded.defeated`,
        },
      }),
  );
}
