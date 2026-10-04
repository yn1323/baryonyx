import assert from "node:assert/strict";
import { test } from "node:test";
import { dropTablesSql, listTablesSql, seedSql } from "./db-reset.mjs";

test("外部キーの検査を遅らせてから、すべての表を消す", () => {
  assert.equal(
    dropTablesSql(["app_users", 'odd"name']),
    [
      "PRAGMA defer_foreign_keys = ON;",
      'DROP TABLE IF EXISTS "app_users";',
      'DROP TABLE IF EXISTS "odd""name";',
    ].join("\n"),
  );
});

test("D1とSQLiteの内部の表は消す対象に含めない", () => {
  assert.match(listTablesSql, /NOT GLOB 'sqlite_\*'/);
  assert.match(listTablesSql, /NOT GLOB '_cf_\*'/);
});

test("seedの文を1つのSQLファイルにつなぐ", () => {
  assert.equal(seedSql(["INSERT 1", "INSERT 2"]), "INSERT 1;\nINSERT 2;\n");
});
