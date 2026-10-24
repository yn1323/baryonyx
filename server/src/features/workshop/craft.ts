import { and, count, eq, inArray, type SQL, sql } from "drizzle-orm";
import type { Database, Statement } from "../../shared/db.js";
import { multiplierOf, qualityOf } from "../../shared/quality.js";
import { slotOf } from "../equipment/catalog.js";
import {
  equipmentCharacterSlots,
  equipmentItems,
} from "../equipment/db-schema.js";
import { runeRecord } from "../exercise-rewards/wallet.js";
import { maxPercentOf } from "../inventory/catalog.js";
import { inventoryGrants } from "../inventory/grants.js";
import { itemHoldings } from "../items/db-schema.js";
import { partyCharacterCards, skillCards } from "../party/db-schema.js";
import { discoveredSql, findDiscovery } from "./buy.js";
import {
  CRAFT_STAR_RUNES,
  craftedPercent,
  craftStarsOf,
  workshopStarsOf,
} from "./catalog.js";
import { workshopCrafts } from "./db-schema.js";
import type { Craft } from "./schema.js";

type CraftRow = typeof workshopCrafts.$inferSelect;
type ItemCraft = Extract<Craft, { category: "item" }>;
type HeldCraft = Exclude<Craft, ItemCraft>;
type ItemMaterial = { id: string; count: number };

// 使った材料。スキル・装備は持ち物の行のID、アイテムは種類ごとの数。
type CraftMaterials = string[] | ItemMaterial[];

// 材料を、記録と要求で比べられる形にする。選んだ順は問わない。
export function materialKey(materials: CraftMaterials) {
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
// （減らす）文。state は書けなかった理由を見分けるときに比べる。
type Prepared = {
  percent: number | null;
  ready: SQL;
  consume: (written: SQL) => Statement[];
  state: string;
};

// 材料の状態の読み込み。スキル・装備は持っている行と付けている行、アイテムは持っている数とデッキに入れている数。
type MaterialRows = [
  { id: string | null; entryId?: string; percent?: number; count?: number }[],
  { id: string | null; count?: number }[],
];

export function createCraftOperation(db: Database) {
  const findCraft = (userId: string, requestId: string) =>
    db
      .select()
      .from(workshopCrafts)
      .where(
        and(
          eq(workshopCrafts.userId, userId),
          eq(workshopCrafts.requestId, requestId),
        ),
      );

  // 合成の応答。倍率と品質は、記録の倍率と、出来上がった種類の上限から求める。アイテムは倍率を持たない（null）。
  function crafted(request: Craft, row: CraftRow) {
    if (
      row.category !== request.category ||
      row.entryId !== request.id ||
      materialKey(row.materials) !== materialKey(materialsOf(request))
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
        materials: row.materials,
      },
    };
  }

  // 材料の状態を読む文。
  function materialReads(userId: string, request: Craft) {
    if (request.category === "item") {
      const ids = request.items.map((item) => item.id);
      return [
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
      ] as const;
    }
    const ids = request.materialIds;
    return request.category === "skill"
      ? ([
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
        ] as const)
      : ([
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
        ] as const);
  }

  // スキル・装備の材料を確かめる。持っていて、同じ分類で☆がちょうど1つ低く、キャラに付けていない3つだけを使う。
  // アクセサリーを付ける枠はまだないが、付け替えの枠（equipment_character_slots）にないことは同じく条件にする。
  function prepareHeld(
    userId: string,
    request: HeldCraft,
    stars: number,
    [held, equipped]: MaterialRows,
  ): Prepared | { error: "invalid_materials" } {
    const ids = request.materialIds;
    const isSkill = request.category === "skill";
    const rows = held as { id: string; entryId: string; percent: number }[];
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
    return {
      percent,
      ready,
      state: JSON.stringify(
        rows.map((row) => `${row.id}:${row.percent}`).sort(),
      ),
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
    };
  }

  // 持っている数のうち、キャラのデッキ（カスタムスキルの枠）に入れていない数のSQLの式。
  const spareItems = (userId: string, id: string) =>
    sql`(COALESCE((SELECT ${itemHoldings.count} FROM ${itemHoldings} WHERE ${itemHoldings.userId} = ${userId} AND ${itemHoldings.itemId} = ${id}), 0) - (SELECT COUNT(*) FROM ${partyCharacterCards} WHERE ${partyCharacterCards.userId} = ${userId} AND ${partyCharacterCards.itemId} = ${id}))`;

  // アイテムの材料を確かめる。☆がちょうど1つ低い種類を、デッキに入れている数を残して持っている分だけ使う。
  // デッキの枠は持っている数まで同じ種類を入れられるため、入れている枠の数（入れていれば1つ以上）を残す。
  function prepareItems(
    userId: string,
    request: ItemCraft,
    stars: number,
    now: string,
    [held, used]: MaterialRows,
  ): Prepared | { error: "invalid_materials" } {
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
    };
  }

  function prepare(
    userId: string,
    request: Craft,
    stars: number,
    now: string,
    rows: MaterialRows,
  ) {
    return request.category === "item"
      ? prepareItems(userId, request, stars, now, rows)
      : prepareHeld(userId, request, stars, rows);
  }

  // ☆が1つ低い同じ分類の物を合計3つ使い、これまでに手に入れた☆2以上の物を1つ作る。費用と倍率はサーバーが決める。
  // 合成の記録・ルーンの減算・材料の消去（アイテムは減算）・持ち物への追加を、残高と見つけた記録と材料の状態
  // （持っていて、付けていない・デッキに残す数を除いて足りる）を条件にした1回のbatchで行う。
  // 材料の状態を記録の条件に入れるため、同時の要求で同じ材料を二重に使わない。要求IDで再送を見分ける。
  // 要求IDの記録と材料を1回の往復で読み、書き込みと、書けなかった理由を見分ける読み込みをもう1回の往復で済ませる。
  async function craft(userId: string, request: Craft, now: string) {
    const stars = craftStarsOf(request.category, request.id);
    const [crafts, ...rows] = await db.batch([
      findCraft(userId, request.requestId),
      ...materialReads(userId, request),
    ]);
    const replay = crafts[0];
    if (replay) return crafted(request, replay);
    if (stars === undefined) return { error: "goods_not_found" as const };

    const prepared = prepare(
      userId,
      request,
      stars,
      now,
      rows as unknown as MaterialRows,
    );
    if ("error" in prepared) return { error: prepared.error };

    // アイテムは種類ごとに数を持つため、持ち物の行のIDの代わりに種類のIDを記録する。
    const itemId =
      request.category === "item" ? request.id : crypto.randomUUID();
    const runes = CRAFT_STAR_RUNES[stars];
    const pay = runeRecord(db, userId, workshopCrafts, now);
    const results = await db.batch([
      db
        .insert(workshopCrafts)
        .select(
          db
            .select({
              id: sql<string>`${pay.id}`.as("id"),
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
              and(
                pay.affords(runes),
                discoveredSql(userId, request.category, request.id),
                prepared.ready,
              ),
            ),
        )
        // 同時に届いた同じ要求IDの再送では書かない。
        .onConflictDoNothing(),
      pay.debit(runes),
      // 材料は、合成を書けたときだけ消す（減らす）。出来上がった物も、書けたときだけ持ち物へ足す。
      ...prepared.consume(pay.written),
      ...inventoryGrants(
        db,
        pay.written,
        userId,
        [
          {
            category: request.category,
            id: request.id,
            multiplierPercent: prepared.percent,
            rowId: itemId,
          },
        ],
        now,
        { discover: false },
      ),
      pay.settle,
      findDiscovery(db, userId, request.category, request.id),
      ...materialReads(userId, request),
      findCraft(userId, request.requestId),
    ]);
    const row = (results.at(-1) as CraftRow[])[0];
    if (row) return crafted(request, row);
    // 書けなかった理由を、材料が書く前と同じ状態か、見つけた記録があるか、の順に見分ける。
    const again = prepare(
      userId,
      request,
      stars,
      now,
      results.slice(-3, -1) as unknown as MaterialRows,
    );
    if ("error" in again || again.state !== prepared.state) {
      return { error: "invalid_materials" as const };
    }
    return {
      error:
        (results.at(-4) as unknown[]).length > 0
          ? ("insufficient_runes" as const)
          : ("not_discovered" as const),
    };
  }

  return { craft };
}
