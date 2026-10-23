import { and, count, eq, inArray, type SQL, sql } from "drizzle-orm";
import type { BatchItem } from "drizzle-orm/batch";
import { createDatabase } from "../../shared/db.js";
import { multiplierOf, qualityOf } from "../../shared/quality.js";
import { maxPercentOf } from "../adventure/drops.js";
import { slotOf } from "../equipment/catalog.js";
import {
  equipmentCharacterSlots,
  equipmentItems,
} from "../equipment/db-schema.js";
import { debitRunes, runeBalance } from "../exercise-rewards/wallet.js";
import { itemHoldings } from "../items/db-schema.js";
import { libraryDiscoveries } from "../library/db-schema.js";
import { partyCharacterCards, skillCards } from "../party/db-schema.js";
import {
  CRAFT_STAR_RUNES,
  type CraftCategory,
  craftedPercent,
  craftStarsOf,
  WORKSHOP_MULTIPLIER_PERCENT,
  workshopPrice,
  workshopStarsOf,
} from "./catalog.js";
import { workshopCrafts, workshopPurchases } from "./db-schema.js";
import type { Buy, Craft } from "./schema.js";

type PurchaseRow = typeof workshopPurchases.$inferSelect;
type CraftRow = typeof workshopCrafts.$inferSelect;
type ItemCraft = Extract<Craft, { category: "item" }>;
type HeldCraft = Exclude<Craft, ItemCraft>;
type ItemMaterial = { id: string; count: number };

// 使った材料。スキル・装備は持ち物の行のID、アイテムは種類ごとの数。
type CraftMaterials = string[] | ItemMaterial[];

// 材料を、記録と要求で比べられる形にする。選んだ順は問わない。
function materialKey(materials: CraftMaterials) {
  return JSON.stringify(
    materials
      .map((material) =>
        typeof material === "string"
          ? material
          : `${material.id}:${material.count}`,
      )
      .sort(),
  );
}

const materialsOf = (request: Craft): CraftMaterials =>
  request.category === "item" ? request.items : request.materialIds;

// 材料を書く前に確かめた結果。ready は書くときにも材料が同じ状態であることの条件、consume は材料を消す
// （減らす）文、grant は出来上がった物を持ち物へ足す文。state は書けなかった理由を見分けるときに比べる。
type Prepared = {
  percent: number | null;
  ready: SQL;
  consume: (written: SQL) => BatchItem<"sqlite">[];
  grant: (written: SQL) => BatchItem<"sqlite">;
  state: string;
};

export function createWorkshopRepository(binding: D1Database) {
  const db = createDatabase(binding);

  function findPurchase(userId: string, requestId: string) {
    return db
      .select()
      .from(workshopPurchases)
      .where(
        and(
          eq(workshopPurchases.userId, userId),
          eq(workshopPurchases.requestId, requestId),
        ),
      )
      .get();
  }

  function findDiscovery(userId: string, request: Buy) {
    return db
      .select({ entryId: libraryDiscoveries.entryId })
      .from(libraryDiscoveries)
      .where(
        and(
          eq(libraryDiscoveries.userId, userId),
          eq(libraryDiscoveries.kind, request.category),
          eq(libraryDiscoveries.entryId, request.id),
        ),
      )
      .get();
  }

  // 買った物は品質の幅の下限（×1.00）のため、どの種類も品質Cになる。
  function recorded(request: Buy, row: PurchaseRow) {
    if (row.category !== request.category || row.entryId !== request.id) {
      return { error: "request_conflict" as const };
    }
    return {
      purchase: {
        category: row.category,
        id: row.entryId,
        itemId: row.itemId,
        multiplier: multiplierOf(WORKSHOP_MULTIPLIER_PERCENT),
        quality: qualityOf(
          WORKSHOP_MULTIPLIER_PERCENT,
          WORKSHOP_MULTIPLIER_PERCENT,
        ),
        runes: row.runes,
        balanceAfter: row.balanceAfter,
      },
    };
  }

  // これまでに手に入れた物を1つ、品質の最低（×1.00）でルーンと引き換えに持ち物へ足す。値段はサーバーが決める。
  // 購入の記録・ルーンの減算・持ち物への追加を、残高と見つけた記録があることを条件にした1回のbatchで行う。
  // 見つけた記録は条件にあるため、書き足さない。要求IDで再送を見分け、二重に引かない。
  async function buy(userId: string, request: Buy, now: string) {
    const replay = await findPurchase(userId, request.requestId);
    if (replay) return recorded(request, replay);
    const price = workshopPrice(request.category, request.id);
    if (price === undefined) return { error: "goods_not_found" as const };

    const id = crypto.randomUUID();
    const itemId = crypto.randomUUID();
    const balance = runeBalance(userId);
    const written = sql`EXISTS (SELECT 1 FROM ${workshopPurchases} WHERE ${workshopPurchases.id} = ${id})`;
    const discovered = sql`EXISTS (SELECT 1 FROM ${libraryDiscoveries} WHERE ${libraryDiscoveries.userId} = ${userId} AND ${libraryDiscoveries.kind} = ${request.category} AND ${libraryDiscoveries.entryId} = ${request.id})`;
    await db.batch([
      db
        .insert(workshopPurchases)
        .select(
          db
            .select({
              id: sql<string>`${id}`.as("id"),
              userId: sql<string>`${userId}`.as("user_id"),
              requestId: sql<string>`${request.requestId}`.as("request_id"),
              category: sql<string>`${request.category}`.as("category"),
              entryId: sql<string>`${request.id}`.as("entry_id"),
              itemId: sql<string>`${itemId}`.as("item_id"),
              runes: sql<number>`${price}`.as("runes"),
              balanceAfter: sql<number>`0`.as("balance_after"),
              createdAt: sql<string>`${now}`.as("created_at"),
            })
            .from(sql`(SELECT 1)`)
            .where(and(sql`${balance} >= ${price}`, discovered)),
        )
        // 同時に届いた同じ要求IDの再送では書かない。
        .onConflictDoNothing(),
      debitRunes(db, userId, price, written, now),
      // 持ち物には、購入を書けたときだけ1つ足す。
      request.category === "skill"
        ? db.insert(skillCards).select(
            db
              .select({
                id: sql<string>`${itemId}`.as("id"),
                userId: sql<string>`${userId}`.as("user_id"),
                skillId: sql<string>`${request.id}`.as("skill_id"),
                multiplierPercent:
                  sql<number>`${WORKSHOP_MULTIPLIER_PERCENT}`.as(
                    "multiplier_percent",
                  ),
                acquiredAt: sql<string>`${now}`.as("acquired_at"),
              })
              .from(sql`(SELECT 1)`)
              .where(written),
          )
        : db.insert(equipmentItems).select(
            db
              .select({
                id: sql<string>`${itemId}`.as("id"),
                userId: sql<string>`${userId}`.as("user_id"),
                equipmentId: sql<string>`${request.id}`.as("equipment_id"),
                multiplierPercent:
                  sql<number>`${WORKSHOP_MULTIPLIER_PERCENT}`.as(
                    "multiplier_percent",
                  ),
                acquiredAt: sql<string>`${now}`.as("acquired_at"),
              })
              .from(sql`(SELECT 1)`)
              .where(written),
          ),
      db
        .update(workshopPurchases)
        .set({ balanceAfter: sql`COALESCE(${balance}, 0)` })
        .where(eq(workshopPurchases.id, id)),
    ]);

    const row = await findPurchase(userId, request.requestId);
    if (row) return recorded(request, row);
    // 書けなかった理由を、見つけた記録があるかで見分ける。
    return {
      error: (await findDiscovery(userId, request))
        ? ("insufficient_runes" as const)
        : ("not_discovered" as const),
    };
  }

  function findCraft(userId: string, requestId: string) {
    return db
      .select()
      .from(workshopCrafts)
      .where(
        and(
          eq(workshopCrafts.userId, userId),
          eq(workshopCrafts.requestId, requestId),
        ),
      )
      .get();
  }

  function discoveredBy(userId: string, category: CraftCategory, id: string) {
    return db
      .select({ entryId: libraryDiscoveries.entryId })
      .from(libraryDiscoveries)
      .where(
        and(
          eq(libraryDiscoveries.userId, userId),
          eq(libraryDiscoveries.kind, category),
          eq(libraryDiscoveries.entryId, id),
        ),
      )
      .get();
  }

  // 合成の応答。倍率と品質は、記録の倍率と、出来上がった種類の上限から求める。アイテムは倍率を持たない（null）。
  function crafted(request: Craft, row: CraftRow) {
    if (
      row.category !== request.category ||
      row.entryId !== request.id ||
      materialKey(JSON.parse(row.materials)) !==
        materialKey(materialsOf(request))
    ) {
      return { error: "request_conflict" as const };
    }
    const percent = row.multiplierPercent;
    const maxPercent = maxPercentOf(request.category, row.entryId);
    return {
      craft: {
        category: row.category,
        id: row.entryId,
        itemId: row.itemId,
        multiplier: percent === null ? null : multiplierOf(percent),
        quality:
          percent === null || maxPercent === null
            ? null
            : qualityOf(percent, maxPercent),
        runes: row.runes,
        balanceAfter: row.balanceAfter,
        materials: JSON.parse(row.materials) as CraftMaterials,
      },
    };
  }

  // スキル・装備の材料を確かめる。持っていて、同じ分類で☆がちょうど1つ低く、キャラに付けていない3つだけを使う。
  // アクセサリーを付ける枠はまだないが、付け替えの枠（equipment_character_slots）にないことは同じく条件にする。
  async function prepareHeld(
    userId: string,
    request: HeldCraft,
    stars: number,
    itemId: string,
    now: string,
  ): Promise<Prepared | { error: "invalid_materials" }> {
    const ids = request.materialIds;
    const isSkill = request.category === "skill";
    const [rows, equipped] = isSkill
      ? await Promise.all([
          db
            .select({
              id: skillCards.id,
              entryId: skillCards.skillId,
              percent: skillCards.multiplierPercent,
            })
            .from(skillCards)
            .where(
              and(eq(skillCards.userId, userId), inArray(skillCards.id, ids)),
            ),
          db
            .select({ id: partyCharacterCards.cardId })
            .from(partyCharacterCards)
            .where(
              and(
                eq(partyCharacterCards.userId, userId),
                inArray(partyCharacterCards.cardId, ids),
              ),
            ),
        ])
      : await Promise.all([
          db
            .select({
              id: equipmentItems.id,
              entryId: equipmentItems.equipmentId,
              percent: equipmentItems.multiplierPercent,
            })
            .from(equipmentItems)
            .where(
              and(
                eq(equipmentItems.userId, userId),
                inArray(equipmentItems.id, ids),
              ),
            ),
          db
            .select({ id: equipmentCharacterSlots.itemId })
            .from(equipmentCharacterSlots)
            .where(
              and(
                eq(equipmentCharacterSlots.userId, userId),
                inArray(equipmentCharacterSlots.itemId, ids),
              ),
            ),
        ]);
    const valid =
      rows.length === ids.length &&
      equipped.length === 0 &&
      rows.every(
        (row) =>
          (isSkill || slotOf(row.entryId) === request.category) &&
          workshopStarsOf(request.category, row.entryId) === stars - 1,
      );
    if (!valid) return { error: "invalid_materials" as const };

    const maxPercent = maxPercentOf(request.category, request.id) as number;
    const percent = craftedPercent(
      rows.map((row) => ({
        percent: row.percent,
        maxPercent: maxPercentOf(request.category, row.entryId) as number,
      })),
      maxPercent,
    );
    // 書くときにも、材料を同じ倍率で持っていて、付けていないこと。先に品質強化や付け替えをされたら書かない。
    const ready = and(
      ...rows.map((row) =>
        isSkill
          ? sql`EXISTS (SELECT 1 FROM ${skillCards} WHERE ${skillCards.userId} = ${userId} AND ${skillCards.id} = ${row.id} AND ${skillCards.multiplierPercent} = ${row.percent}) AND NOT EXISTS (SELECT 1 FROM ${partyCharacterCards} WHERE ${partyCharacterCards.userId} = ${userId} AND ${partyCharacterCards.cardId} = ${row.id})`
          : sql`EXISTS (SELECT 1 FROM ${equipmentItems} WHERE ${equipmentItems.userId} = ${userId} AND ${equipmentItems.id} = ${row.id} AND ${equipmentItems.multiplierPercent} = ${row.percent}) AND NOT EXISTS (SELECT 1 FROM ${equipmentCharacterSlots} WHERE ${equipmentCharacterSlots.userId} = ${userId} AND ${equipmentCharacterSlots.itemId} = ${row.id})`,
      ),
    ) as SQL;
    const state = JSON.stringify(
      rows.map((row) => `${row.id}:${row.percent}`).sort(),
    );
    return {
      percent,
      ready,
      state,
      consume: (written) => [
        isSkill
          ? db
              .delete(skillCards)
              .where(
                and(
                  eq(skillCards.userId, userId),
                  inArray(skillCards.id, ids),
                  written,
                ),
              )
          : db
              .delete(equipmentItems)
              .where(
                and(
                  eq(equipmentItems.userId, userId),
                  inArray(equipmentItems.id, ids),
                  written,
                ),
              ),
      ],
      grant: (written) =>
        isSkill
          ? db.insert(skillCards).select(
              db
                .select({
                  id: sql<string>`${itemId}`.as("id"),
                  userId: sql<string>`${userId}`.as("user_id"),
                  skillId: sql<string>`${request.id}`.as("skill_id"),
                  multiplierPercent: sql<number>`${percent}`.as(
                    "multiplier_percent",
                  ),
                  acquiredAt: sql<string>`${now}`.as("acquired_at"),
                })
                .from(sql`(SELECT 1)`)
                .where(written),
            )
          : db.insert(equipmentItems).select(
              db
                .select({
                  id: sql<string>`${itemId}`.as("id"),
                  userId: sql<string>`${userId}`.as("user_id"),
                  equipmentId: sql<string>`${request.id}`.as("equipment_id"),
                  multiplierPercent: sql<number>`${percent}`.as(
                    "multiplier_percent",
                  ),
                  acquiredAt: sql<string>`${now}`.as("acquired_at"),
                })
                .from(sql`(SELECT 1)`)
                .where(written),
            ),
    };
  }

  // 持っている数のうち、キャラのデッキ（カスタムスキルの枠）に入れていない数のSQLの式。
  const spareItems = (userId: string, id: string) =>
    sql`(COALESCE((SELECT ${itemHoldings.count} FROM ${itemHoldings} WHERE ${itemHoldings.userId} = ${userId} AND ${itemHoldings.itemId} = ${id}), 0) - (SELECT COUNT(*) FROM ${partyCharacterCards} WHERE ${partyCharacterCards.userId} = ${userId} AND ${partyCharacterCards.itemId} = ${id}))`;

  // アイテムの材料を確かめる。☆がちょうど1つ低い種類を、デッキに入れている数を残して持っている分だけ使う。
  // デッキの枠は持っている数まで同じ種類を入れられるため、入れている枠の数（入れていれば1つ以上）を残す。
  async function prepareItems(
    userId: string,
    request: ItemCraft,
    stars: number,
    now: string,
  ): Promise<Prepared | { error: "invalid_materials" }> {
    const ids = request.items.map((item) => item.id);
    const [held, used] = await Promise.all([
      db
        .select({ id: itemHoldings.itemId, count: itemHoldings.count })
        .from(itemHoldings)
        .where(
          and(
            eq(itemHoldings.userId, userId),
            inArray(itemHoldings.itemId, ids),
          ),
        ),
      db
        .select({ id: partyCharacterCards.itemId, count: count() })
        .from(partyCharacterCards)
        .where(
          and(
            eq(partyCharacterCards.userId, userId),
            inArray(partyCharacterCards.itemId, ids),
          ),
        )
        .groupBy(partyCharacterCards.itemId),
    ]);
    const spare = (id: string) =>
      (held.find((row) => row.id === id)?.count ?? 0) -
      (used.find((row) => row.id === id)?.count ?? 0);
    const valid = request.items.every(
      (item) =>
        workshopStarsOf("item", item.id) === stars - 1 &&
        spare(item.id) >= item.count,
    );
    if (!valid) return { error: "invalid_materials" as const };

    return {
      percent: null,
      ready: and(
        ...request.items.map(
          (item) => sql`${spareItems(userId, item.id)} >= ${item.count}`,
        ),
      ) as SQL,
      state: "",
      // 数を減らし、使いきった種類は行を消す（item_holdings は数が1以上の行だけを持つ）。
      consume: (written) =>
        request.items.flatMap((item) => {
          const where = and(
            eq(itemHoldings.userId, userId),
            eq(itemHoldings.itemId, item.id),
            written,
          );
          return [
            db
              .delete(itemHoldings)
              .where(and(where, eq(itemHoldings.count, item.count))),
            db
              .update(itemHoldings)
              .set({
                count: sql`${itemHoldings.count} - ${item.count}`,
                updatedAt: now,
              })
              .where(where),
          ];
        }),
      grant: (written) =>
        db
          .insert(itemHoldings)
          .select(
            db
              .select({
                userId: sql<string>`${userId}`.as("user_id"),
                itemId: sql<string>`${request.id}`.as("item_id"),
                count: sql<number>`1`.as("count"),
                acquiredAt: sql<string>`${now}`.as("acquired_at"),
                updatedAt: sql<string>`${now}`.as("updated_at"),
              })
              .from(sql`(SELECT 1)`)
              .where(written),
          )
          .onConflictDoUpdate({
            target: [itemHoldings.userId, itemHoldings.itemId],
            set: {
              count: sql`${itemHoldings.count} + excluded.count`,
              updatedAt: sql`excluded.updated_at`,
            },
          }),
    };
  }

  function prepare(
    userId: string,
    request: Craft,
    stars: number,
    itemId: string,
    now: string,
  ) {
    return request.category === "item"
      ? prepareItems(userId, request, stars, now)
      : prepareHeld(userId, request, stars, itemId, now);
  }

  // ☆が1つ低い同じ分類の物を合計3つ使い、これまでに手に入れた☆2以上の物を1つ作る。費用と倍率はサーバーが決める。
  // 合成の記録・ルーンの減算・材料の消去（アイテムは減算）・持ち物への追加を、残高と見つけた記録と材料の状態
  // （持っていて、付けていない・デッキに残す数を除いて足りる）を条件にした1回のbatchで行う。
  // 材料の状態を記録の条件に入れるため、同時の要求で同じ材料を二重に使わない。要求IDで再送を見分ける。
  async function craft(userId: string, request: Craft, now: string) {
    const replay = await findCraft(userId, request.requestId);
    if (replay) return crafted(request, replay);
    const stars = craftStarsOf(request.category, request.id);
    if (stars === undefined) return { error: "goods_not_found" as const };

    // アイテムは種類ごとに数を持つため、持ち物の行のIDの代わりに種類のIDを記録する。
    const itemId =
      request.category === "item" ? request.id : crypto.randomUUID();
    const prepared = await prepare(userId, request, stars, itemId, now);
    if ("error" in prepared) return { error: prepared.error };

    const id = crypto.randomUUID();
    const runes = CRAFT_STAR_RUNES[stars];
    const balance = runeBalance(userId);
    const written = sql`EXISTS (SELECT 1 FROM ${workshopCrafts} WHERE ${workshopCrafts.id} = ${id})`;
    const discovered = sql`EXISTS (SELECT 1 FROM ${libraryDiscoveries} WHERE ${libraryDiscoveries.userId} = ${userId} AND ${libraryDiscoveries.kind} = ${request.category} AND ${libraryDiscoveries.entryId} = ${request.id})`;
    await db.batch([
      db
        .insert(workshopCrafts)
        .select(
          db
            .select({
              id: sql<string>`${id}`.as("id"),
              userId: sql<string>`${userId}`.as("user_id"),
              requestId: sql<string>`${request.requestId}`.as("request_id"),
              category: sql<string>`${request.category}`.as("category"),
              entryId: sql<string>`${request.id}`.as("entry_id"),
              itemId: sql<string>`${itemId}`.as("item_id"),
              multiplierPercent: (prepared.percent === null
                ? sql<null>`NULL`
                : sql<number>`${prepared.percent}`
              ).as("multiplier_percent"),
              runes: sql<number>`${runes}`.as("runes"),
              balanceAfter: sql<number>`0`.as("balance_after"),
              materials:
                sql<string>`${JSON.stringify(materialsOf(request))}`.as(
                  "materials",
                ),
              createdAt: sql<string>`${now}`.as("created_at"),
            })
            .from(sql`(SELECT 1)`)
            .where(
              and(sql`${balance} >= ${runes}`, discovered, prepared.ready),
            ),
        )
        // 同時に届いた同じ要求IDの再送では書かない。
        .onConflictDoNothing(),
      debitRunes(db, userId, runes, written, now),
      // 材料は、合成を書けたときだけ消す（減らす）。出来上がった物も、書けたときだけ持ち物へ足す。
      ...prepared.consume(written),
      prepared.grant(written),
      db
        .update(workshopCrafts)
        .set({ balanceAfter: sql`COALESCE(${balance}, 0)` })
        .where(eq(workshopCrafts.id, id)),
    ]);

    const row = await findCraft(userId, request.requestId);
    if (row) return crafted(request, row);
    // 書けなかった理由を、材料が書く前と同じ状態か、見つけた記録があるか、の順に見分ける。
    const again = await prepare(userId, request, stars, itemId, now);
    if ("error" in again || again.state !== prepared.state) {
      return { error: "invalid_materials" as const };
    }
    return {
      error: (await discoveredBy(userId, request.category, request.id))
        ? ("insufficient_runes" as const)
        : ("not_discovered" as const),
    };
  }

  return { buy, craft };
}
