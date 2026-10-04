import { sql } from "drizzle-orm";
import {
  check,
  foreignKey,
  primaryKey,
  sqliteTable,
  text,
  uniqueIndex,
} from "drizzle-orm/sqlite-core";
import { appUsers } from "../accounts/db-schema.js";
import { partyCharacters } from "../party/db-schema.js";

// 初期の装備を付与済みのユーザー。付与を1回だけにするための印で、付与した要求を grant_id で見分ける。
export const equipmentProfiles = sqliteTable("equipment_profiles", {
  userId: text("user_id")
    .primaryKey()
    .references(() => appUsers.id),
  grantId: text("grant_id").notNull(),
  createdAt: text("created_at").notNull(),
});

// 持っている装備。同じ種類でも1つずつ別の行にする（レア度や付与効果を個別に持てるようにするため）。
export const equipmentItems = sqliteTable(
  "equipment_items",
  {
    id: text("id").primaryKey(),
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    equipmentId: text("equipment_id").notNull(),
    acquiredAt: text("acquired_at").notNull(),
  },
  (table) => [
    uniqueIndex("equipment_items_user_item").on(table.userId, table.id),
  ],
);

// キャラごとの武器・防具の枠。空いている枠は行を持たない。持っている装備だけを、1人の1つの枠にだけ付けられる。
export const equipmentCharacterSlots = sqliteTable(
  "equipment_character_slots",
  {
    userId: text("user_id")
      .notNull()
      .references(() => appUsers.id),
    characterId: text("character_id").notNull(),
    slot: text("slot").notNull(),
    itemId: text("item_id").notNull(),
    updatedAt: text("updated_at").notNull(),
  },
  (table) => [
    primaryKey({ columns: [table.userId, table.characterId, table.slot] }),
    uniqueIndex("equipment_character_slots_user_item").on(
      table.userId,
      table.itemId,
    ),
    foreignKey({
      columns: [table.userId, table.characterId],
      foreignColumns: [partyCharacters.userId, partyCharacters.characterId],
    }),
    foreignKey({
      columns: [table.userId, table.itemId],
      foreignColumns: [equipmentItems.userId, equipmentItems.id],
    }),
    check(
      "equipment_character_slots_slot_check",
      sql`${table.slot} IN ('weapon', 'armor')`,
    ),
  ],
);
