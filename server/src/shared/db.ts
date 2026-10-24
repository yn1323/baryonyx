import { getTableColumns, type SQL, sql } from "drizzle-orm";
import type { BatchItem, BatchResponse } from "drizzle-orm/batch";
import { drizzle } from "drizzle-orm/d1";
import type { SelectedFields, SQLiteTable } from "drizzle-orm/sqlite-core";

export function createDatabase(binding: D1Database) {
  return drizzle(binding);
}

export type Database = ReturnType<typeof createDatabase>;

/**
 * 条件（written）を満たすときだけ、値の1行を返す select。INSERT ... SELECT に渡し、
 * 同じ batch の前の文が書けたときだけ書く。INSERT ... SELECT は順で列を当てるため、列は表の列の順に並べる。
 */
export function selectWhen(db: Database, written: SQL) {
  return <T extends SelectedFields>(columns: T) =>
    db.select(columns).from(sql`(SELECT 1)`).where(written);
}

/**
 * 何行もの値を、条件（written）を満たすときだけ1つの文で足す INSERT ... SELECT。行は1つのJSONの値で渡し、
 * SQLiteの json_each で行に戻す。D1の1つの文に渡せる値の数（100個）に縛られず、batch の文の数も増やさない。
 * 行は表のすべての列（TypeScriptの名前）を持つ。値のない列は NULL になり、列の既定値は使わない。
 */
export function insertRowsWhen<T extends SQLiteTable>(
  db: Database,
  table: T,
  rows: readonly T["$inferInsert"][],
  written: SQL,
) {
  const columns = Object.entries(getTableColumns(table))
    // INSERT の列は、drizzle が表の定義の順に並べる（書き込めない列を除く）。同じ順で値を取り出す。
    .filter(
      ([, column]) =>
        !(
          column as unknown as { shouldDisableInsert(): boolean }
        ).shouldDisableInsert(),
    )
    .map(([key]) => sql`json_extract(value, ${`$.${key}`})`);
  return db
    .insert(table)
    .select(
      sql`SELECT ${sql.join(columns, sql`, `)} FROM json_each(${JSON.stringify(rows)}) WHERE ${written}`,
    );
}

export type Statement = BatchItem<"sqlite">;

/**
 * 書き込み（statements）のあとに読み込み（reads）を並べた batch を、1回の往復で実行する。
 * D1の batch は文を順に実行するため、読み込みは書いたあとの状態になる。
 * 画面を開くたびに読むGETと書き込みのAPIは、D1との往復の回数がそのまま待ち時間になる（doc/rules/backend-design.md）。
 */
export async function batchThenRead<
  const T extends readonly Statement[],
  const R extends readonly Statement[],
>(db: Database, statements: T, reads: R) {
  const results = (await db.batch([...statements, ...reads] as unknown as [
    Statement,
    ...Statement[],
  ])) as unknown[];
  return {
    results: results.slice(0, statements.length) as BatchResponse<T>,
    reads: results.slice(statements.length) as BatchResponse<R>,
  };
}
