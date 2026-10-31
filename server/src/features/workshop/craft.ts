import { and, count, eq, inArray, type SQL, sql } from "drizzle-orm";
import type { Database, Statement } from "../../shared/db.js";
import { multiplierOf, qualityOf } from "../../shared/quality.js";
import { slotOf } from "../equipment/catalog.js";
import {
  equipmentCharacterSlots,
  equipmentItems,
} from "../equipment/db-schema.js";
import { runeRecord } from "../exercise-rewards/wallet.js";
import { type HoldingCategory, maxPercentOf } from "../inventory/catalog.js";
import { inventoryGrants } from "../inventory/grants.js";
import { itemHoldings } from "../items/db-schema.js";
import { partyCharacterCards, skillCards } from "../party/db-schema.js";
import { CRAFT_STAR_RUNES, craftedPercent, craftStarsOf } from "./catalog.js";
import { workshopCrafts } from "./db-schema.js";
import { type CraftRecipe, recipeOf } from "./recipes.js";
import type { Craft } from "./schema.js";

type CraftRow = typeof workshopCrafts.$inferSelect;
type HeldRow = { id: string; entryId: string; percent: number };
type Counted = { id: string | null; count: number };

// 使った特別な材料（持ち物の行のID）を、記録と要求で比べられる形にする。選んだ順は問わない。
export function materialKey(materials: readonly unknown[]) {
  return JSON.stringify(materials.map(String).sort());
}

// レシピの材料のうち、アイテムと、持ち物の1つずつで使う物（装備・スキル）。
const itemsOf = (recipe: CraftRecipe) =>
  recipe.materials.filter((material) => material.category === "item");
const heldOf = (recipe: CraftRecipe) =>
  recipe.materials.filter((material) => material.category !== "item");

// inArray に空の配列を渡さないよう、どの行にも当たらない値で埋める。
const orNone = (ids: readonly string[]) => (ids.length > 0 ? [...ids] : [""]);

// 材料を書く前に確かめた結果。ready は書くときにも材料が同じ状態であることの条件、consume は材料を消す
// （減らす）文。state は書けなかった理由を見分けるときに比べる。
type Prepared = {
  percent: number;
  ready: SQL;
  consume: (written: SQL) => Statement[];
  state: string;
};

// 材料の状態の読み込み：持っているスキルの行・装備の行、デッキに入れているスキル、付けている装備、
// 持っているアイテムの数、デッキに入れているアイテムの数。
type MaterialRows = [
  HeldRow[],
  HeldRow[],
  { id: string | null }[],
  { id: string | null }[],
  Counted[],
  Counted[],
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

  // 合成の応答。倍率と品質は、記録の倍率と、出来上がった種類の上限から求める。
  function crafted(request: Craft, row: CraftRow) {
    if (
      row.category !== request.category ||
      row.entryId !== request.id ||
      materialKey(row.materials) !== materialKey(request.materialIds)
    ) {
      return { error: "request_conflict" as const };
    }
    const percent = row.multiplierPercent ?? 100;
    const maxPercent = maxPercentOf(request.category, row.entryId) ?? 100;
    return {
      craft: {
        category: row.category,
        id: row.entryId,
        itemId: row.itemId,
        multiplier: multiplierOf(percent),
        quality: qualityOf(percent, maxPercent),
        runes: row.runes,
        balanceAfter: row.balanceAfter,
        materials: row.materials,
      },
    };
  }

  // 材料の状態を読む文。
  function materialReads(userId: string, request: Craft, recipe: CraftRecipe) {
    const ids = orNone(request.materialIds);
    const itemIds = orNone(itemsOf(recipe).map((material) => material.id));
    return [
      db
        .select({
          id: skillCards.id,
          entryId: skillCards.skillId,
          percent: skillCards.multiplierPercent,
        })
        .from(skillCards)
        .where(and(eq(skillCards.userId, userId), inArray(skillCards.id, ids))),
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
        .select({ id: partyCharacterCards.cardId })
        .from(partyCharacterCards)
        .where(
          and(
            eq(partyCharacterCards.userId, userId),
            inArray(partyCharacterCards.cardId, ids),
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
      db
        .select({ id: itemHoldings.itemId, count: itemHoldings.count })
        .from(itemHoldings)
        .where(
          and(
            eq(itemHoldings.userId, userId),
            inArray(itemHoldings.itemId, itemIds),
          ),
        ),
      db
        .select({ id: partyCharacterCards.itemId, count: count() })
        .from(partyCharacterCards)
        .where(
          and(
            eq(partyCharacterCards.userId, userId),
            inArray(partyCharacterCards.itemId, itemIds),
          ),
        )
        .groupBy(partyCharacterCards.itemId),
    ] as const;
  }

  // 持っている数のうち、キャラのデッキ（カスタムスキルの枠）に入れていない数のSQLの式。
  const spareItems = (userId: string, id: string) =>
    sql`(COALESCE((SELECT ${itemHoldings.count} FROM ${itemHoldings} WHERE ${itemHoldings.userId} = ${userId} AND ${itemHoldings.itemId} = ${id}), 0) - (SELECT COUNT(*) FROM ${partyCharacterCards} WHERE ${partyCharacterCards.userId} = ${userId} AND ${partyCharacterCards.itemId} = ${id}))`;

  // 材料を確かめる。
  // 装備・スキル：送られた行が、レシピの特別な材料と種類ごとに1つずつ合い、持っていて、付けていない（デッキに
  // 入れていない）こと。同じ種類の装備・スキルはレシピに1つまで（doc/features/synthesis.md）。
  // アイテム：レシピの数を、デッキに入れている数を残して持っていること。
  function prepare(
    userId: string,
    request: Craft,
    recipe: CraftRecipe,
    now: string,
    [skills, gear, decked, worn, held, used]: MaterialRows,
  ): Prepared | { error: "invalid_materials" } {
    const wanted = heldOf(recipe);
    const rows = [
      ...skills.map((row) => ({ ...row, category: "skill" as string })),
      ...gear.map((row) => ({
        ...row,
        category: slotOf(row.entryId) as string,
      })),
    ];
    const heldValid =
      request.materialIds.length === wanted.length &&
      rows.length === wanted.length &&
      decked.length === 0 &&
      worn.length === 0 &&
      wanted.every(
        (material) =>
          rows.filter(
            (row) =>
              row.category === material.category && row.entryId === material.id,
          ).length === material.count,
      );
    const spare = (id: string) =>
      (held.find((row) => row.id === id)?.count ?? 0) -
      (used.find((row) => row.id === id)?.count ?? 0);
    const items = itemsOf(recipe);
    const itemsValid = items.every((item) => spare(item.id) >= item.count);
    if (!heldValid || !itemsValid) return { error: "invalid_materials" };

    const percent = craftedPercent(
      rows.map((row) => ({
        percent: row.percent,
        maxPercent: maxPercentOf(
          row.category as HoldingCategory,
          row.entryId,
        ) as number,
      })),
      maxPercentOf(request.category, request.id) as number,
    );
    // 書くときにも、材料を同じ倍率で持っていて、付けていないこと。先に品質強化や付け替えをされたら書かない。
    const conditions: SQL[] = [
      ...rows.map((row) =>
        row.category === "skill"
          ? sql`EXISTS (SELECT 1 FROM ${skillCards} WHERE ${skillCards.userId} = ${userId} AND ${skillCards.id} = ${row.id} AND ${skillCards.multiplierPercent} = ${row.percent}) AND NOT EXISTS (SELECT 1 FROM ${partyCharacterCards} WHERE ${partyCharacterCards.userId} = ${userId} AND ${partyCharacterCards.cardId} = ${row.id})`
          : sql`EXISTS (SELECT 1 FROM ${equipmentItems} WHERE ${equipmentItems.userId} = ${userId} AND ${equipmentItems.id} = ${row.id} AND ${equipmentItems.multiplierPercent} = ${row.percent}) AND NOT EXISTS (SELECT 1 FROM ${equipmentCharacterSlots} WHERE ${equipmentCharacterSlots.userId} = ${userId} AND ${equipmentCharacterSlots.itemId} = ${row.id})`,
      ),
      ...items.map(
        (item) => sql`${spareItems(userId, item.id)} >= ${item.count}`,
      ),
    ];
    const skillIds = rows
      .filter((row) => row.category === "skill")
      .map((row) => row.id);
    const gearIds = rows
      .filter((row) => row.category !== "skill")
      .map((row) => row.id);
    return {
      percent,
      ready: conditions.length > 0 ? (and(...conditions) as SQL) : sql`1`,
      state: JSON.stringify([
        ...rows.map((row) => `${row.id}:${row.percent}`).sort(),
        ...items.map((item) => `${item.id}:${spare(item.id)}`),
      ]),
      // 装備・スキルは行を消し、アイテムは数を減らして、使いきった種類は行を消す
      // （item_holdings は数が1以上の行だけを持つ）。
      consume: (written) => [
        ...(skillIds.length > 0
          ? [
              db
                .delete(skillCards)
                .where(
                  and(
                    eq(skillCards.userId, userId),
                    inArray(skillCards.id, skillIds),
                    written,
                  ),
                ),
            ]
          : []),
        ...(gearIds.length > 0
          ? [
              db
                .delete(equipmentItems)
                .where(
                  and(
                    eq(equipmentItems.userId, userId),
                    inArray(equipmentItems.id, gearIds),
                    written,
                  ),
                ),
            ]
          : []),
        ...items.flatMap((item) => {
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
      ],
    };
  }

  // レシピの材料をすべて使い、武器・防具・アクセサリー・カスタムスキルを1つ作る。費用と倍率はサーバーが決める。
  // 作る物を手に入れたことがなくても作れ、作った物はライブラリの見つけた記録に足す（doc/features/synthesis.md）。
  // 合成の記録・ルーンの減算・材料の消去（アイテムは減算）・持ち物への追加を、残高と材料の状態（持っていて、
  // 付けていない・デッキに残す数を除いて足りる）を条件にした1回のbatchで行う。
  // 材料の状態を記録の条件に入れるため、同時の要求で同じ材料を二重に使わない。要求IDで再送を見分ける。
  // 要求IDの記録と材料を1回の往復で読み、書き込みと、書けなかった理由を見分ける読み込みをもう1回の往復で済ませる。
  async function craft(userId: string, request: Craft, now: string) {
    const recipe = recipeOf(request.category, request.id);
    const stars = craftStarsOf(request.category, request.id);
    if (!recipe || stars === undefined) {
      const [crafts] = await db.batch([findCraft(userId, request.requestId)]);
      return crafts[0]
        ? crafted(request, crafts[0])
        : { error: "goods_not_found" as const };
    }
    const [crafts, ...rows] = await db.batch([
      findCraft(userId, request.requestId),
      ...materialReads(userId, request, recipe),
    ]);
    const replay = crafts[0];
    if (replay) return crafted(request, replay);

    const prepared = prepare(
      userId,
      request,
      recipe,
      now,
      rows as unknown as MaterialRows,
    );
    if ("error" in prepared) return { error: prepared.error };

    const itemId = crypto.randomUUID();
    const runes = CRAFT_STAR_RUNES[stars];
    const pay = runeRecord(db, userId, workshopCrafts, now);
    const reads = materialReads(userId, request, recipe);
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
              multiplierPercent: sql<number>`${prepared.percent}`.as(
                "multiplier_percent",
              ),
              runes: sql<number>`${runes}`.as("runes"),
              balanceAfter: sql<number>`0`.as("balance_after"),
              materials: sql<string>`${JSON.stringify(request.materialIds)}`.as(
                "materials",
              ),
              createdAt: sql<string>`${now}`.as("created_at"),
            })
            .from(sql`(SELECT 1)`)
            .where(and(pay.affords(runes), prepared.ready)),
        )
        // 同時に届いた同じ要求IDの再送では書かない。
        .onConflictDoNothing(),
      pay.debit(runes),
      // 材料は、合成を書けたときだけ消す（減らす）。出来上がった物も、書けたときだけ持ち物と見つけた記録へ足す。
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
      ),
      pay.settle,
      ...reads,
      findCraft(userId, request.requestId),
    ]);
    const row = (results.at(-1) as CraftRow[])[0];
    if (row) return crafted(request, row);
    // 書けなかった理由を、材料が書く前と同じ状態かで見分ける。同じならルーンが足りなかった。
    const again = prepare(
      userId,
      request,
      recipe,
      now,
      results.slice(-1 - reads.length, -1) as unknown as MaterialRows,
    );
    if ("error" in again || again.state !== prepared.state) {
      return { error: "invalid_materials" as const };
    }
    return { error: "insufficient_runes" as const };
  }

  return { craft };
}
