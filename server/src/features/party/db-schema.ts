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

// 初期のキャラと編成を付与済みのユーザー。付与を1回だけにするための印で、付与した要求を grant_id で見分ける。
export const partyProfiles = sqliteTable("party_profiles", {
  userId: text("user_id")
    .primaryKey()
    .references(() => appUsers.id),
  grantId: text("grant_id").notNull().default(""),
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

// 持っているスキル。同じ種類でも1枚ずつ別の行にし、1枚ずつ倍率（百分率の整数）を持つ
// （doc/features/party.md の持っているスキル、doc/features/inventory.md の品質）。
// 初めのカードとして付与し、冒険のドロップで増える。カスタムスキルの枠は、この1枚を指す。
export const skillCards = sqliteTable(
  "skill_cards",
  {
    id: text("id").primaryKey(),
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    skillId: text("skill_id").notNull(),
    multiplierPercent: integer("multiplier_percent").notNull(),
    acquiredAt: text("acquired_at").notNull(),
  },
  (table) => [
    index("skill_cards_user").on(table.userId, table.acquiredAt),
    check(
      "skill_cards_multiplier_check",
      sql`${table.multiplierPercent} >= 100`,
    ),
  ],
);

// キャラごとの2つのカスタムスキルの枠。持っているスキル1枚（card_id）か、持っているアイテム（item_id）の
// どちらかを入れる（doc/features/party.md）。スキルの1枚は1人の1つの枠にだけ入れられる。アイテムは、
// 持っている数まで別々の枠に入れられる（数の確かめは処理で行う）。空いている枠は行を持たない。
export const partyCharacterCards = sqliteTable(
  "party_character_cards",
  {
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    characterId: text("character_id").notNull(),
    slot: integer("slot").notNull(),
    cardId: text("card_id").references(() => skillCards.id),
    itemId: text("item_id"),
    updatedAt: text("updated_at").notNull(),
  },
  (table) => [
    primaryKey({ columns: [table.userId, table.characterId, table.slot] }),
    uniqueIndex("party_character_cards_user_card").on(
      table.userId,
      table.cardId,
    ),
    foreignKey({
      columns: [table.userId, table.characterId],
      foreignColumns: [partyCharacters.userId, partyCharacters.characterId],
    }),
    check(
      "party_character_cards_slot_check",
      sql`${table.slot} >= 0 AND ${table.slot} < 2`,
    ),
    check(
      "party_character_cards_content_check",
      sql`(${table.cardId} IS NULL) <> (${table.itemId} IS NULL)`,
    ),
  ],
);

// キャラごとの2つの固有スキルの、取得した段階と編成でセットしている段階（doc/features/skill-evolution.md）。
// acquired_mask は段階ごとのビット（段階 n が 1 << n）で、最初の段階（ビット0）は常に立てる。
// セットできるのは取得した段階だけ。進化していない固有スキルは行を持たず、最初の段階をセットした扱いにする。
export const partyUniqueSkills = sqliteTable(
  "party_unique_skills",
  {
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    characterId: text("character_id").notNull(),
    uniqueIndex: integer("unique_index").notNull(),
    setStage: integer("set_stage").notNull(),
    acquiredMask: integer("acquired_mask").notNull(),
    updatedAt: text("updated_at").notNull(),
  },
  (table) => [
    primaryKey({
      columns: [table.userId, table.characterId, table.uniqueIndex],
    }),
    foreignKey({
      columns: [table.userId, table.characterId],
      foreignColumns: [partyCharacters.userId, partyCharacters.characterId],
    }),
    check(
      "party_unique_skills_index_check",
      sql`${table.uniqueIndex} >= 0 AND ${table.uniqueIndex} < 2`,
    ),
    check(
      "party_unique_skills_stage_check",
      sql`${table.setStage} >= 0 AND ${table.setStage} < 7`,
    ),
    check(
      "party_unique_skills_mask_check",
      sql`${table.acquiredMask} >= 1 AND ${table.acquiredMask} < 128 AND (${table.acquiredMask} & 1) = 1`,
    ),
    check(
      "party_unique_skills_set_acquired_check",
      sql`((${table.acquiredMask} >> ${table.setStage}) & 1) = 1`,
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
