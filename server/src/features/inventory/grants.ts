import { type SQL, sql } from "drizzle-orm";
import { type Database, selectWhen } from "../../shared/db.js";
import { enhancementMaterials } from "../enhancement/db-schema.js";
import { equipmentItems } from "../equipment/db-schema.js";
import { itemHoldings } from "../items/db-schema.js";
import type { DiscoveryKind } from "../library/catalog.js";
import { discoveryStatementsWhen } from "../library/discoveries.js";
import { skillCards } from "../party/db-schema.js";
import { type HoldingCategory, isEquipmentCategory } from "./catalog.js";

// 持ち物（スキル・装備・アイテム・強化素材）を足す文。ドロップ・宝箱・行商人・商会・召喚が、
// 記録の書き込みと同じ batch に入れ、記録（written）を書けたときだけ足す。
// D1は1つの文に100個までしか値を渡せないため、スキルと装備は1つずつ、アイテムと強化素材は種類ごとに1つの文にする。

/** 足す物1つ。rowId はスキル・装備の持ち物の行のID（応答や記録に残すときに渡す。省けば作る）。 */
export type Grant = {
  category: HoldingCategory;
  id: string;
  multiplierPercent: number | null;
  rowId?: string;
};

export function skillGrant(
  db: Database,
  written: SQL,
  userId: string,
  grant: Omit<Grant, "category">,
  now: string,
) {
  return db.insert(skillCards).select(
    selectWhen(
      db,
      written,
    )({
      id: sql<string>`${grant.rowId ?? crypto.randomUUID()}`.as("id"),
      userId: sql<string>`${userId}`.as("user_id"),
      skillId: sql<string>`${grant.id}`.as("skill_id"),
      multiplierPercent: sql<number>`${grant.multiplierPercent}`.as(
        "multiplier_percent",
      ),
      acquiredAt: sql<string>`${now}`.as("acquired_at"),
      awakenedBranch: sql<null>`NULL`.as("awakened_branch"),
    }),
  );
}

export function equipmentGrant(
  db: Database,
  written: SQL,
  userId: string,
  grant: Omit<Grant, "category">,
  now: string,
) {
  return db.insert(equipmentItems).select(
    selectWhen(
      db,
      written,
    )({
      id: sql<string>`${grant.rowId ?? crypto.randomUUID()}`.as("id"),
      userId: sql<string>`${userId}`.as("user_id"),
      equipmentId: sql<string>`${grant.id}`.as("equipment_id"),
      multiplierPercent: sql<number>`${grant.multiplierPercent}`.as(
        "multiplier_percent",
      ),
      acquiredAt: sql<string>`${now}`.as("acquired_at"),
    }),
  );
}

// 同じIDが並んだ数を数える。
function countsOf(ids: readonly string[]) {
  const counts = new Map<string, number>();
  for (const id of ids) counts.set(id, (counts.get(id) ?? 0) + 1);
  return [...counts];
}

/** アイテムを種類ごとの数で足す文。同じIDを並べた数だけ増やす。 */
export function itemGrants(
  db: Database,
  written: SQL,
  userId: string,
  itemIds: readonly string[],
  now: string,
) {
  return countsOf(itemIds).map(([itemId, count]) =>
    db
      .insert(itemHoldings)
      .select(
        selectWhen(
          db,
          written,
        )({
          userId: sql<string>`${userId}`.as("user_id"),
          itemId: sql<string>`${itemId}`.as("item_id"),
          count: sql<number>`${count}`.as("count"),
          acquiredAt: sql<string>`${now}`.as("acquired_at"),
          updatedAt: sql<string>`${now}`.as("updated_at"),
        }),
      )
      .onConflictDoUpdate({
        target: [itemHoldings.userId, itemHoldings.itemId],
        set: {
          count: sql`${itemHoldings.count} + excluded.count`,
          updatedAt: sql`excluded.updated_at`,
        },
      }),
  );
}

/** 強化素材を種類ごとの数で足す文（doc/features/enhancement.md の強化素材）。同じIDを並べた数だけ増やす。 */
export function materialGrants(
  db: Database,
  written: SQL,
  userId: string,
  materialIds: readonly string[],
  now: string,
) {
  return countsOf(materialIds).map(([materialId, count]) =>
    db
      .insert(enhancementMaterials)
      .select(
        selectWhen(
          db,
          written,
        )({
          userId: sql<string>`${userId}`.as("user_id"),
          materialId: sql<string>`${materialId}`.as("material_id"),
          count: sql<number>`${count}`.as("count"),
          acquiredAt: sql<string>`${now}`.as("acquired_at"),
          updatedAt: sql<string>`${now}`.as("updated_at"),
        }),
      )
      .onConflictDoUpdate({
        target: [enhancementMaterials.userId, enhancementMaterials.materialId],
        set: {
          count: sql`${enhancementMaterials.count} + excluded.count`,
          updatedAt: sql`excluded.updated_at`,
        },
      }),
  );
}

// ライブラリに載せる持ち物の分類。強化素材は載せない。
const DISCOVERED_CATEGORIES = [
  "weapon",
  "armor",
  "accessory",
  "skill",
  "item",
] as const satisfies readonly Exclude<DiscoveryKind, "enemy">[];

/**
 * 持ち物をまとめて足す文。分類ごとの文に分け、discover なら見つけた記録も同じ条件で書く。
 * 商会の購入・合成のように、見つけた記録を書く前の条件にしている書き込みは discover を false にする。
 */
export function inventoryGrants(
  db: Database,
  written: SQL,
  userId: string,
  grants: readonly Grant[],
  now: string,
  { discover = true }: { discover?: boolean } = {},
) {
  const idsOf = (category: HoldingCategory) =>
    grants.filter((grant) => grant.category === category).map(({ id }) => id);
  return [
    ...grants
      .filter((grant) => grant.category === "skill")
      .map((grant) => skillGrant(db, written, userId, grant, now)),
    ...grants
      .filter((grant) => isEquipmentCategory(grant.category))
      .map((grant) => equipmentGrant(db, written, userId, grant, now)),
    ...itemGrants(db, written, userId, idsOf("item"), now),
    ...materialGrants(db, written, userId, idsOf("material"), now),
    ...(discover
      ? DISCOVERED_CATEGORIES.flatMap((category) =>
          discoveryStatementsWhen(
            db,
            userId,
            category,
            idsOf(category),
            now,
            written,
          ),
        )
      : []),
  ];
}
