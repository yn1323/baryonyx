import { and, eq, type SQL, sql } from "drizzle-orm";
import type { SelectedFields } from "drizzle-orm/sqlite-core";
import { createDatabase } from "../../shared/db.js";
import { multiplierOf, qualityOf } from "../../shared/quality.js";
import { maxPercentOf } from "../adventure/drops.js";
import { equipmentItems } from "../equipment/db-schema.js";
import { createEquipmentRepository } from "../equipment/repository.js";
import { debitRunes, runeBalance } from "../exercise-rewards/wallet.js";
import { itemHoldings } from "../items/db-schema.js";
import { discoveryStatementsWhen } from "../library/discoveries.js";
import { partyCharacters } from "../party/db-schema.js";
import {
  BOND_MAX,
  isEquipmentCategory,
  SHARDS_PER_OVERFLOW,
  SUMMON_POOL,
  type SummonCategory,
  summonPrice,
} from "./catalog.js";
import { characterBonds, summonPulls, summonShards } from "./db-schema.js";
import { type Random, rollSummon } from "./roll.js";
import type { Summon } from "./schema.js";

// 記録に残す、出た物1つ。itemId は足した装備の行のID（装備のときだけ）。
// キャラは、持っていなければ加わり（isNew）、持っていれば絆のしるしが1つ増える（tokens は引いたあとのしるしの数、
// bond は今の段）。段としるしの合計が上限に達していれば、かけら（shards）になる。
type PulledRecord = {
  category: SummonCategory;
  id: string;
  stars: number;
  multiplierPercent: number | null;
  itemId: string | null;
  isNew?: boolean;
  bond?: number;
  tokens?: number;
  shards?: number;
};

type PullRow = typeof summonPulls.$inferSelect;

export function createSummonRepository(
  binding: D1Database,
  random: Random = Math.random,
) {
  const db = createDatabase(binding);

  function findPull(userId: string, requestId: string) {
    return db
      .select()
      .from(summonPulls)
      .where(
        and(
          eq(summonPulls.userId, userId),
          eq(summonPulls.requestId, requestId),
        ),
      )
      .get();
  }

  // 装備は倍率と品質も返す。
  function recorded(request: Summon, row: PullRow) {
    if (row.count !== request.count) {
      return { error: "request_conflict" as const };
    }
    const results = (JSON.parse(row.results) as PulledRecord[]).map(
      (result) => {
        const max =
          result.multiplierPercent === null
            ? null
            : maxPercentOf(
                result.category as "weapon" | "armor" | "accessory",
                result.id,
              );
        return {
          ...result,
          multiplier:
            result.multiplierPercent === null
              ? null
              : multiplierOf(result.multiplierPercent),
          quality:
            result.multiplierPercent === null || max === null
              ? null
              : qualityOf(result.multiplierPercent, max),
        };
      },
    );
    return {
      summon: {
        count: row.count,
        runes: row.runes,
        balanceAfter: row.balanceAfter,
        results,
      },
    };
  }

  // 別の書き込み（written）が条件どおり入ったときだけ、1行を返す select。
  const writtenSelect =
    (written: SQL) =>
    <T extends SelectedFields>(columns: T) =>
      db.select(columns).from(sql`(SELECT 1)`).where(written);

  // 出た物を、持っているキャラと今の絆・しるしに当てはめて、記録に残す形にする。
  async function prepare(userId: string, count: number) {
    const draws = rollSummon(SUMMON_POOL, count, random);
    const [owned, bonds] = await Promise.all([
      db
        .select({ characterId: partyCharacters.characterId })
        .from(partyCharacters)
        .where(eq(partyCharacters.userId, userId))
        .all(),
      db
        .select({
          characterId: characterBonds.characterId,
          bond: characterBonds.bond,
          tokens: characterBonds.tokens,
        })
        .from(characterBonds)
        .where(eq(characterBonds.userId, userId))
        .all(),
    ]);
    const have = new Set(owned.map((row) => row.characterId));
    const bondOf = new Map(bonds.map((row) => [row.characterId, row.bond]));
    const tokensOf = new Map(bonds.map((row) => [row.characterId, row.tokens]));
    // この召喚で増えたしるしの数（キャラごと）。
    const added = new Map<string, number>();
    const records: PulledRecord[] = draws.map((draw) => {
      const record: PulledRecord = {
        category: draw.category,
        id: draw.id,
        stars: draw.stars,
        multiplierPercent: draw.multiplierPercent,
        itemId: isEquipmentCategory(draw.category) ? crypto.randomUUID() : null,
      };
      if (draw.category !== "character") return record;
      if (!have.has(draw.id)) {
        have.add(draw.id);
        return { ...record, isNew: true, bond: 0 };
      }
      const bond = bondOf.get(draw.id) ?? 0;
      const tokens = tokensOf.get(draw.id) ?? 0;
      if (bond + tokens >= BOND_MAX) {
        return { ...record, bond, tokens, shards: SHARDS_PER_OVERFLOW };
      }
      tokensOf.set(draw.id, tokens + 1);
      added.set(draw.id, (added.get(draw.id) ?? 0) + 1);
      return { ...record, bond, tokens: tokens + 1 };
    });
    return { records, bondOf, added };
  }

  // 1回か10回引き、出た物を持ち物へ足す。値段と出る物はサーバーが決める。
  // 召喚の記録・ルーンの減算・持ち物への追加・絆とかけら・見つけた記録を、残高を条件にした1回のbatchで行う。
  // 要求IDで再送を見分け、二重に引かない（再送には記録した結果を返す）。
  async function summon(userId: string, request: Summon, now: string) {
    // 初めて読むユーザーには、編成と装備の初期の付与を先に済ませる（あとから上書きされないように）。
    await createEquipmentRepository(binding).getState(userId, now);
    const replay = await findPull(userId, request.requestId);
    if (replay) return recorded(request, replay);

    const price = summonPrice(request.count);
    const { records, bondOf, added } = await prepare(userId, request.count);
    const id = crypto.randomUUID();
    const balance = runeBalance(userId);
    const written = sql`EXISTS (SELECT 1 FROM ${summonPulls} WHERE ${summonPulls.id} = ${id})`;
    const fromWritten = writtenSelect(written);

    const equipmentGrants = records
      .filter((record) => isEquipmentCategory(record.category))
      .map((record) =>
        db.insert(equipmentItems).select(
          fromWritten({
            id: sql<string>`${record.itemId}`.as("id"),
            userId: sql<string>`${userId}`.as("user_id"),
            equipmentId: sql<string>`${record.id}`.as("equipment_id"),
            multiplierPercent: sql<number>`${record.multiplierPercent}`.as(
              "multiplier_percent",
            ),
            acquiredAt: sql<string>`${now}`.as("acquired_at"),
          }),
        ),
      );
    const itemCounts = new Map<string, number>();
    for (const record of records) {
      if (record.category === "item") {
        itemCounts.set(record.id, (itemCounts.get(record.id) ?? 0) + 1);
      }
    }
    const itemGrants = [...itemCounts].map(([itemId, count]) =>
      db
        .insert(itemHoldings)
        .select(
          fromWritten({
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
    const characters = records.filter(
      (record) => record.category === "character",
    );
    const characterGrants = characters
      .filter((record) => record.isNew)
      .map((record) =>
        db
          .insert(partyCharacters)
          .select(
            fromWritten({
              userId: sql<string>`${userId}`.as("user_id"),
              characterId: sql<string>`${record.id}`.as("character_id"),
              level: sql<number>`1`.as("level"),
              acquiredAt: sql<string>`${now}`.as("acquired_at"),
              updatedAt: sql<string>`${now}`.as("updated_at"),
            }),
          )
          .onConflictDoNothing(),
      );
    // 増えたしるしを足す。段としるしの合計は上限を超えない。
    const tokenGrants = [...added].map(([characterId, count]) =>
      db
        .insert(characterBonds)
        .select(
          fromWritten({
            userId: sql<string>`${userId}`.as("user_id"),
            characterId: sql<string>`${characterId}`.as("character_id"),
            bond: sql<number>`${bondOf.get(characterId) ?? 0}`.as("bond"),
            tokens: sql<number>`${count}`.as("tokens"),
            updatedAt: sql<string>`${now}`.as("updated_at"),
          }),
        )
        .onConflictDoUpdate({
          target: [characterBonds.userId, characterBonds.characterId],
          set: {
            tokens: sql`MIN(${BOND_MAX} - ${characterBonds.bond}, ${characterBonds.tokens} + excluded.tokens)`,
            updatedAt: sql`excluded.updated_at`,
          },
        }),
    );
    const shards = characters.reduce(
      (sum, record) => sum + (record.shards ?? 0),
      0,
    );
    const shardGrants =
      shards > 0
        ? [
            db
              .insert(summonShards)
              .select(
                fromWritten({
                  userId: sql<string>`${userId}`.as("user_id"),
                  shards: sql<number>`${shards}`.as("shards"),
                  updatedAt: sql<string>`${now}`.as("updated_at"),
                }),
              )
              .onConflictDoUpdate({
                target: summonShards.userId,
                set: {
                  shards: sql`${summonShards.shards} + excluded.shards`,
                  updatedAt: sql`excluded.updated_at`,
                },
              }),
          ]
        : [];
    const discoveries = SUMMON_CATEGORY_KINDS.flatMap((category) =>
      discoveryStatementsWhen(
        db,
        userId,
        category,
        records
          .filter((record) => record.category === category)
          .map((record) => record.id),
        now,
        written,
      ),
    );

    await db.batch([
      db
        .insert(summonPulls)
        .select(
          db
            .select({
              id: sql<string>`${id}`.as("id"),
              userId: sql<string>`${userId}`.as("user_id"),
              requestId: sql<string>`${request.requestId}`.as("request_id"),
              count: sql<number>`${request.count}`.as("count"),
              runes: sql<number>`${price}`.as("runes"),
              results: sql<string>`${JSON.stringify(records)}`.as("results"),
              balanceAfter: sql<number>`0`.as("balance_after"),
              createdAt: sql<string>`${now}`.as("created_at"),
            })
            .from(sql`(SELECT 1)`)
            .where(sql`${balance} >= ${price}`),
        )
        // 同時に届いた同じ要求IDの再送では書かない。
        .onConflictDoNothing(),
      debitRunes(db, userId, price, written, now),
      ...equipmentGrants,
      ...itemGrants,
      ...characterGrants,
      ...tokenGrants,
      ...shardGrants,
      ...discoveries,
      db
        .update(summonPulls)
        .set({ balanceAfter: sql`COALESCE(${balance}, 0)` })
        .where(eq(summonPulls.id, id)),
    ]);

    const row = await findPull(userId, request.requestId);
    if (row) return recorded(request, row);
    return { error: "insufficient_runes" as const };
  }

  // キャラごとの絆としるし（どちらも0のキャラは返さない）と、かけらの数。
  async function getState(userId: string) {
    const [bonds, shards] = await db.batch([
      db
        .select({
          characterId: characterBonds.characterId,
          bond: characterBonds.bond,
          tokens: characterBonds.tokens,
        })
        .from(characterBonds)
        .where(eq(characterBonds.userId, userId))
        .orderBy(characterBonds.characterId),
      db
        .select({ shards: summonShards.shards })
        .from(summonShards)
        .where(eq(summonShards.userId, userId)),
    ]);
    return {
      bonds: bonds.filter((row) => row.bond > 0 || row.tokens > 0),
      shards: shards[0]?.shards ?? 0,
    };
  }

  return { summon, getState };
}

// 見つけた記録の種類は、召喚の分類と同じ名前。
const SUMMON_CATEGORY_KINDS = [
  "character",
  "weapon",
  "armor",
  "accessory",
  "item",
] as const;
