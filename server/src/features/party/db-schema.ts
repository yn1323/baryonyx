import { sql } from "drizzle-orm";
import {
  check,
  foreignKey,
  index,
  integer,
  primaryKey,
  sqliteTable,
  text,
  uniqueIndex,
} from "drizzle-orm/sqlite-core";
import { appUsers } from "../accounts/db-schema.js";

// 初期のキャラと編成を付与済みのユーザー。付与を1回だけにするための印。
export const partyProfiles = sqliteTable("party_profiles", {
  userId: text("user_id")
    .primaryKey()
    .references(() => appUsers.id),
  createdAt: text("created_at").notNull(),
});

// 持っているキャラとそのレベル。
export const partyCharacters = sqliteTable(
  "party_characters",
  {
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    characterId: text("character_id").notNull(),
    level: integer("level").notNull(),
    acquiredAt: text("acquired_at").notNull(),
    updatedAt: text("updated_at").notNull(),
  },
  (table) => [
    primaryKey({ columns: [table.userId, table.characterId] }),
    check("party_characters_level_check", sql`${table.level} >= 1`),
  ],
);

// パーティの4つの枠。空いている枠は行を持たない。持っているキャラだけを、1つの枠にだけ入れられる。
export const partySlots = sqliteTable(
  "party_slots",
  {
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    slot: integer("slot").notNull(),
    characterId: text("character_id").notNull(),
    updatedAt: text("updated_at").notNull(),
  },
  (table) => [
    primaryKey({ columns: [table.userId, table.slot] }),
    uniqueIndex("party_slots_user_character").on(
      table.userId,
      table.characterId,
    ),
    foreignKey({
      columns: [table.userId, table.characterId],
      foreignColumns: [partyCharacters.userId, partyCharacters.characterId],
    }),
    check(
      "party_slots_slot_check",
      sql`${table.slot} >= 0 AND ${table.slot} < 4`,
    ),
  ],
);

// キャラごとの2枚のカスタムスキル。1人のキャラは同じカードを1つの枠にだけ付けられる。
export const partyCharacterCards = sqliteTable(
  "party_character_cards",
  {
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    characterId: text("character_id").notNull(),
    slot: integer("slot").notNull(),
    skillId: text("skill_id").notNull(),
    updatedAt: text("updated_at").notNull(),
  },
  (table) => [
    primaryKey({ columns: [table.userId, table.characterId, table.slot] }),
    uniqueIndex("party_character_cards_user_character_skill").on(
      table.userId,
      table.characterId,
      table.skillId,
    ),
    foreignKey({
      columns: [table.userId, table.characterId],
      foreignColumns: [partyCharacters.userId, partyCharacters.characterId],
    }),
    check(
      "party_character_cards_slot_check",
      sql`${table.slot} >= 0 AND ${table.slot} < 2`,
    ),
  ],
);

// レベルアップ1回ごとの記録。使ったルーンの記録を兼ね、要求IDで再送を見分ける。
// 残高（rune_wallets）は、ルーンの台帳の合計からここで使った合計を引いた値になる。
export const partyLevelUps = sqliteTable(
  "party_level_ups",
  {
    id: text("id").primaryKey(),
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    requestId: text("request_id").notNull(),
    characterId: text("character_id").notNull(),
    fromLevel: integer("from_level").notNull(),
    toLevel: integer("to_level").notNull(),
    runes: integer("runes").notNull(),
    balanceAfter: integer("balance_after").notNull(),
    createdAt: text("created_at").notNull(),
  },
  (table) => [
    uniqueIndex("party_level_ups_user_request").on(
      table.userId,
      table.requestId,
    ),
    index("party_level_ups_user_created").on(table.userId, table.createdAt),
    foreignKey({
      columns: [table.userId, table.characterId],
      foreignColumns: [partyCharacters.userId, partyCharacters.characterId],
    }),
    check(
      "party_level_ups_level_order_check",
      sql`${table.toLevel} > ${table.fromLevel} AND ${table.fromLevel} >= 1`,
    ),
    check("party_level_ups_runes_check", sql`${table.runes} > 0`),
    check(
      "party_level_ups_balance_after_check",
      sql`${table.balanceAfter} >= 0`,
    ),
  ],
);
