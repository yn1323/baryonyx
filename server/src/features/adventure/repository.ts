import { and, eq, isNull, type SQL, sql } from "drizzle-orm";
import type { SelectedFields } from "drizzle-orm/sqlite-core";
import { createDatabase } from "../../shared/db.js";
import { multiplierOf, qualityOf } from "../../shared/quality.js";
import { materialGrants } from "../enhancement/repository.js";
import { equipmentItems } from "../equipment/db-schema.js";
import {
  debitRunes,
  runeBalance,
  selectRuneBalance,
} from "../exercise-rewards/wallet.js";
import { createHealthRepository } from "../health/repository.js";
import { itemHoldings } from "../items/db-schema.js";
import {
  discoveryStatementsWhen,
  enemyStatements,
} from "../library/discoveries.js";
import { skillCards } from "../party/db-schema.js";
import {
  bossFloor,
  canPlace,
  type Destination,
  ENTRANCE_ROOM_ID,
  type EventKind,
  findDestination,
  hasEvent,
  isBattle,
  MIMIC_CHANCE,
  newSeed,
  pickTrialCondition,
  type RoomKind,
  reviveCost,
  stepDoorTier,
} from "./catalog.js";
import {
  adventureDrops,
  adventureMerchantGoods,
  adventurePurchases,
  adventureRecords,
  adventureRevives,
  adventureRuns,
} from "./db-schema.js";
import {
  type Drop,
  type DropCategory,
  maxPercentOf,
  rollRoomDrops,
} from "./drops.js";
import { rollMerchantGoods } from "./merchant.js";
import type { Buy, EndReason, Enemies, Move, Revive } from "./schema.js";

type RunRow = typeof adventureRuns.$inferSelect;
type DropRow = typeof adventureDrops.$inferSelect;
type PurchaseRow = typeof adventurePurchases.$inferSelect;
type EndStatus = "cleared" | "defeated" | "retreated";

// 0以上1未満の乱数。テストでは固定の値に差し替える。
export type Random = () => number;

export function createAdventureRepository(
  binding: D1Database,
  random: Random = Math.random,
) {
  const db = createDatabase(binding);

  function activeRun(userId: string) {
    return db
      .select()
      .from(adventureRuns)
      .where(
        and(
          eq(adventureRuns.userId, userId),
          eq(adventureRuns.status, "active"),
        ),
      )
      .get();
  }

  function dropsOf(runId: string) {
    return db
      .select()
      .from(adventureDrops)
      .where(eq(adventureDrops.runId, runId))
      .orderBy(adventureDrops.createdAt, adventureDrops.seq);
  }

  const route = (run: RunRow) => JSON.parse(run.route) as string[];
  const kindOf = (run: RunRow) => run.roomKind as RoomKind;

  // 戦闘のマスか。開けてミミックだった怪しい宝箱も、勝つまで戦闘のマスとして扱う（復活・負けて帰還ができる）。
  const battleRoom = (run: RunRow) =>
    isBattle(kindOf(run)) ||
    (kindOf(run) === "shady-chest" && run.mimic === true);

  const isEquipment = (category: string) =>
    category === "weapon" || category === "armor" || category === "accessory";

  // 別の書き込み（written）が条件どおり入ったときだけ、1行を返す select。
  // 1つの文に入れる値の数を抑えるため、1つの物を1つの文で付与する。
  const writtenSelect =
    (written: SQL) =>
    <T extends SelectedFields>(columns: T) =>
      db.select(columns).from(sql`(SELECT 1)`).where(written);

  // 装備（武器・防具・アクセサリー）とスキルを1つずつ持ち物に足す文。ドロップと行商人の購入が使う。
  const equipmentGrant = (
    fromWritten: ReturnType<typeof writtenSelect>,
    userId: string,
    equipmentId: string,
    multiplierPercent: number,
    now: string,
  ) =>
    db.insert(equipmentItems).select(
      fromWritten({
        id: sql<string>`${crypto.randomUUID()}`.as("id"),
        userId: sql<string>`${userId}`.as("user_id"),
        equipmentId: sql<string>`${equipmentId}`.as("equipment_id"),
        multiplierPercent: sql<number>`${multiplierPercent}`.as(
          "multiplier_percent",
        ),
        acquiredAt: sql<string>`${now}`.as("acquired_at"),
      }),
    );
  const skillGrant = (
    fromWritten: ReturnType<typeof writtenSelect>,
    userId: string,
    skillId: string,
    multiplierPercent: number,
    now: string,
  ) =>
    db.insert(skillCards).select(
      fromWritten({
        id: sql<string>`${crypto.randomUUID()}`.as("id"),
        userId: sql<string>`${userId}`.as("user_id"),
        skillId: sql<string>`${skillId}`.as("skill_id"),
        multiplierPercent: sql<number>`${multiplierPercent}`.as(
          "multiplier_percent",
        ),
        acquiredAt: sql<string>`${now}`.as("acquired_at"),
      }),
    );

  // 応答の1つのドロップ。アイテムは倍率と品質を持たないため、その項目を返さない
  // （UnityのJsonUtilityが数値の null を読めないため、null ではなく項目を省く）。
  const drop = (row: DropRow) => {
    const maxPercent = maxPercentOf(row.category as DropCategory, row.entryId);
    const percent = row.multiplierPercent;
    return {
      roomId: row.roomId,
      category: row.category,
      id: row.entryId,
      ...(percent === null || maxPercent === null
        ? {}
        : {
            multiplier: multiplierOf(percent),
            quality: qualityOf(percent, maxPercent),
          }),
    };
  };

  const dropsInRoom = async (runId: string, roomId: string) =>
    (await dropsOf(runId)).filter((row) => row.roomId === roomId).map(drop);

  // 行商人のマスの品物と、売れたか。
  async function merchantOf(run: RunRow) {
    const rows = await db
      .select({
        goods: adventureMerchantGoods,
        purchaseId: adventurePurchases.id,
      })
      .from(adventureMerchantGoods)
      .leftJoin(
        adventurePurchases,
        and(
          eq(adventurePurchases.runId, adventureMerchantGoods.runId),
          eq(adventurePurchases.roomId, adventureMerchantGoods.roomId),
          eq(adventurePurchases.idx, adventureMerchantGoods.idx),
        ),
      )
      .where(
        and(
          eq(adventureMerchantGoods.runId, run.id),
          eq(adventureMerchantGoods.roomId, run.roomId),
        ),
      )
      .orderBy(adventureMerchantGoods.idx);
    return rows.map(({ goods, purchaseId }) => ({
      index: goods.idx,
      category: goods.category,
      id: goods.entryId,
      multiplierPercent: goods.multiplierPercent,
      quality: qualityOf(
        goods.multiplierPercent,
        maxPercentOf(goods.category as DropCategory, goods.entryId) as number,
      ),
      price: goods.price,
      sold: purchaseId !== null,
    }));
  }

  // 進行中の冒険（道を作る種と部屋の数、今いる部屋、この冒険で手に入れた物を含む）、行き先ごとの記録、所持ルーン。
  // 今いるマスで決まったことも返す。mimic は開けた怪しい宝箱がミミックだったか、trial は試練のマスの条件、
  // merchant は行商人のマスの品物。trial と merchant は、そのマスにいるときだけ項目を返す。
  async function readState(userId: string) {
    const [runs, records, wallets] = await db.batch([
      db
        .select()
        .from(adventureRuns)
        .where(
          and(
            eq(adventureRuns.userId, userId),
            eq(adventureRuns.status, "active"),
          ),
        ),
      db
        .select({
          destinationId: adventureRecords.destinationId,
          bestFloor: adventureRecords.bestFloor,
          clears: adventureRecords.clears,
        })
        .from(adventureRecords)
        .where(eq(adventureRecords.userId, userId))
        .orderBy(adventureRecords.destinationId),
      selectRuneBalance(db, userId),
    ]);
    const run = runs[0];
    const destination = run ? findDestination(run.destinationId) : undefined;
    return {
      run:
        run && destination
          ? {
              id: run.id,
              destinationId: run.destinationId,
              seed: run.seed,
              roomCount: destination.roomCount,
              roomId: run.roomId,
              floor: run.floor,
              roomKind: run.roomKind,
              roomCleared: run.roomCleared,
              route: route(run),
              revives: run.revives,
              reviveCost: reviveCost(run.revives),
              mimic: run.mimic === true,
              ...(run.roomKind === "trial" && run.trial !== null
                ? { trial: run.trial }
                : {}),
              ...(run.roomKind === "merchant"
                ? { merchant: await merchantOf(run) }
                : {}),
              drops: (await dropsOf(run.id)).map(drop),
            }
          : null,
      records,
      runes: wallets[0]?.balance ?? 0,
    };
  }

  async function getState(userId: string) {
    return readState(userId);
  }

  // 冒険先の入口の部屋から冒険を始める。進行中の冒険があるあいだは始められない。
  async function start(userId: string, destinationId: string, now: string) {
    const destination = findDestination(destinationId);
    if (!destination) return { error: "unknown_destination" as const };
    const current = await activeRun(userId);
    if (current) {
      // 届かなかった応答の再送は、始めたばかりの同じ冒険を返す。
      if (
        current.destinationId === destinationId &&
        route(current).length === 1
      ) {
        return readState(userId);
      }
      return { error: "adventure_in_progress" as const };
    }
    await db
      .insert(adventureRuns)
      .values({
        id: crypto.randomUUID(),
        userId,
        destinationId,
        seed: newSeed(random),
        roomId: ENTRANCE_ROOM_ID,
        floor: 1,
        roomKind: "start",
        roomCleared: true,
        route: JSON.stringify([ENTRANCE_ROOM_ID]),
        revives: 0,
        status: "active",
        startedAt: now,
        updatedAt: now,
      })
      .onConflictDoNothing();
    const started = await activeRun(userId);
    if (!started || started.destinationId !== destinationId) {
      return { error: "adventure_in_progress" as const };
    }
    return readState(userId);
  }

  // 今いる部屋から、次の階の部屋へ進む。選んだ時点で保存する。
  // 部屋のつながりはクライアントが種から作った道で決まるため、サーバーは1階ずつ進むこと、
  // マスの種類をその階に置けること（最奥の間の階にだけボスがいることを含む）、出来事を終えてから
  // 進むことを確かめる。
  async function move(userId: string, target: Move, now: string) {
    const run = await activeRun(userId);
    if (!run) return { error: "no_adventure" as const };
    // 届かなかった応答の再送は、進んだあとの状態を返す。
    if (run.roomId === target.roomId) return readState(userId);
    const destination = findDestination(run.destinationId) as Destination;
    const boss = bossFloor(destination);
    if (
      target.floor !== run.floor + 1 ||
      target.floor > boss ||
      !canPlace(destination, target.floor, target.kind)
    ) {
      return { error: "room_not_reachable" as const };
    }
    if (!run.roomCleared) return { error: "room_not_cleared" as const };
    // 試練は入ったときに条件を、行商人は品ぞろえを決めて保存する（doc/features/stage-progression.md のマスの中身）。
    const trial = target.kind === "trial" ? pickTrialCondition(random) : null;
    const goods =
      target.kind === "merchant"
        ? rollMerchantGoods(run.destinationId, random)
        : [];
    const [moved] = await db.batch([
      db
        .update(adventureRuns)
        .set({
          roomId: target.roomId,
          floor: target.floor,
          roomKind: target.kind,
          roomCleared: !hasEvent(target.kind),
          mimic: null,
          trial,
          doorSteps: null,
          route: JSON.stringify([...route(run), target.roomId]),
          updatedAt: now,
        })
        .where(
          and(
            eq(adventureRuns.id, run.id),
            eq(adventureRuns.status, "active"),
            eq(adventureRuns.roomId, run.roomId),
            eq(adventureRuns.roomCleared, true),
          ),
        )
        .returning({ id: adventureRuns.id }),
      // 品物は、冒険がそのマスへ進んだときだけ書く。同時の要求で先に書いた品物は残す。
      ...goods.map((entry, index) =>
        db
          .insert(adventureMerchantGoods)
          .select(
            db
              .select({
                runId: adventureRuns.id,
                roomId: adventureRuns.roomId,
                idx: sql<number>`${index}`.as("idx"),
                category: sql<string>`${entry.category}`.as("category"),
                entryId: sql<string>`${entry.id}`.as("entry_id"),
                multiplierPercent: sql<number>`${entry.multiplierPercent}`.as(
                  "multiplier_percent",
                ),
                price: sql<number>`${entry.price}`.as("price"),
                createdAt: sql<string>`${now}`.as("created_at"),
              })
              .from(adventureRuns)
              .where(
                and(
                  eq(adventureRuns.id, run.id),
                  eq(adventureRuns.status, "active"),
                  eq(adventureRuns.roomId, target.roomId),
                ),
              ),
          )
          .onConflictDoNothing(),
      ),
    ]);
    if (moved.length === 0) return { error: "adventure_changed" as const };
    return readState(userId);
  }

  // 1階ずつしか進まないため、今いる階がこの冒険で着いた最も深い階になる。
  const floorOf = (run: RunRow) => run.floor;

  // 冒険を終えた結果。最深到達の階と記録の更新、この冒険で手に入れた物。
  async function result(run: RunRow, previousBest: number) {
    const floor = floorOf(run);
    const record = await db
      .select()
      .from(adventureRecords)
      .where(
        and(
          eq(adventureRecords.userId, run.userId),
          eq(adventureRecords.destinationId, run.destinationId),
        ),
      )
      .get();
    return {
      runId: run.id,
      destinationId: run.destinationId,
      status: run.status,
      floor,
      bestFloor: record?.bestFloor ?? floor,
      newRecord: floor > previousBest,
      clears: record?.clears ?? 0,
      revives: run.revives,
      drops: (await dropsOf(run.id)).map(drop),
    };
  }

  async function bestFloor(userId: string, destinationId: string) {
    const record = await db
      .select({ bestFloor: adventureRecords.bestFloor })
      .from(adventureRecords)
      .where(
        and(
          eq(adventureRecords.userId, userId),
          eq(adventureRecords.destinationId, destinationId),
        ),
      )
      .get();
    return record?.bestFloor ?? 0;
  }

  // 冒険の記録を、終えた冒険が条件どおり書き込まれたときだけ更新する文。
  function recordStatement(
    run: RunRow,
    status: EndStatus,
    now: string,
    written: ReturnType<typeof sql>,
  ) {
    const floor = floorOf(run);
    const clears = status === "cleared" ? 1 : 0;
    return db
      .insert(adventureRecords)
      .select(
        db
          .select({
            userId: sql<string>`${run.userId}`.as("user_id"),
            destinationId: sql<string>`${run.destinationId}`.as(
              "destination_id",
            ),
            bestFloor: sql<number>`${floor}`.as("best_floor"),
            clears: sql<number>`${clears}`.as("clears"),
            updatedAt: sql<string>`${now}`.as("updated_at"),
          })
          .from(sql`(SELECT 1)`)
          .where(written),
      )
      .onConflictDoUpdate({
        target: [adventureRecords.userId, adventureRecords.destinationId],
        set: {
          bestFloor: sql`MAX(${adventureRecords.bestFloor}, excluded.best_floor)`,
          clears: sql`${adventureRecords.clears} + excluded.clears`,
          updatedAt: sql`excluded.updated_at`,
        },
      });
  }

  // 今いるマスを、物を落とさずに終えた印だけを付ける（休憩・行商人・開けずに通り過ぎた怪しい宝箱）。
  async function markCleared(
    userId: string,
    run: RunRow,
    now: string,
    guard?: SQL,
  ) {
    const marked = await db
      .update(adventureRuns)
      .set({ roomCleared: true, updatedAt: now })
      .where(
        and(
          eq(adventureRuns.id, run.id),
          eq(adventureRuns.status, "active"),
          eq(adventureRuns.roomId, run.roomId),
          eq(adventureRuns.roomCleared, false),
          guard,
        ),
      )
      .returning({ id: adventureRuns.id });
    if (marked.length === 0) return { error: "adventure_changed" as const };
    return { drops: [], ...(await readState(userId)) };
  }

  // 落ちた物を記録して持ち物に入れ、今いるマスを終える（doc/features/drops.md）。最奥のボスなら冒険も終える。
  // マスの終了・ドロップの記録・持ち物・記録を1回のbatchで行う。guard は1つ目の物を書く条件に足す冒険の条件、
  // set はマスを終えるときに冒険へ一緒に書く値。書けなかったとき（再送や同時の要求）は、記録した物が空になる。
  async function award(
    userId: string,
    run: RunRow,
    drops: Drop[],
    now: string,
    options: {
      enemies: Enemies;
      guard?: SQL;
      set?: Partial<typeof adventureRuns.$inferInsert>;
    },
  ) {
    const roomId = run.roomId;

    const boss = kindOf(run) === "boss";
    const grantId = crypto.randomUUID();
    const written = sql`EXISTS (SELECT 1 FROM ${adventureDrops} WHERE ${adventureDrops.runId} = ${run.id} AND ${adventureDrops.roomId} = ${roomId} AND ${adventureDrops.grantId} = ${grantId})`;
    const dropRow = (index: number) => {
      const entry = drops[index];
      return {
        seq: sql<number>`${index}`.as("seq"),
        grantId: sql<string>`${grantId}`.as("grant_id"),
        category: sql<string>`${entry.category}`.as("category"),
        entryId: sql<string>`${entry.id}`.as("entry_id"),
        multiplierPercent: sql<number | null>`${entry.multiplierPercent}`.as(
          "multiplier_percent",
        ),
        createdAt: sql<string>`${now}`.as("created_at"),
      };
    };
    const fromWritten = writtenSelect(written);
    const percentOf = (entry: (typeof drops)[number]) =>
      entry.multiplierPercent as number;
    const equipmentGrants = drops
      .filter((entry) => isEquipment(entry.category))
      .map((entry) =>
        equipmentGrant(fromWritten, userId, entry.id, percentOf(entry), now),
      );
    const skillGrants = drops
      .filter((entry) => entry.category === "skill")
      .map((entry) =>
        skillGrant(fromWritten, userId, entry.id, percentOf(entry), now),
      );
    const itemCounts = new Map<string, number>();
    for (const entry of drops) {
      if (entry.category === "item") {
        itemCounts.set(entry.id, (itemCounts.get(entry.id) ?? 0) + 1);
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
    // 強化素材は、アイテムと同じく種類ごとの数を増やす（doc/features/enhancement.md の強化素材）。
    const materialGrantStatements = materialGrants(
      db,
      userId,
      drops
        .filter((entry) => entry.category === "material")
        .map((entry) => entry.id),
      now,
      written,
    );
    const discoveries = (
      ["weapon", "armor", "accessory", "skill", "item"] as const
    ).flatMap((category) =>
      discoveryStatementsWhen(
        db,
        userId,
        category,
        drops
          .filter((entry) => entry.category === category)
          .map((entry) => entry.id),
        now,
        written,
      ),
    );

    await db.batch([
      // 1つ目の物は、部屋を終えていない進行中の冒険のときだけ書く。残りはそれが書けたときだけ書く。
      db
        .insert(adventureDrops)
        .select(
          db
            .select({
              runId: adventureRuns.id,
              roomId: adventureRuns.roomId,
              ...dropRow(0),
            })
            .from(adventureRuns)
            .where(
              and(
                eq(adventureRuns.id, run.id),
                eq(adventureRuns.status, "active"),
                eq(adventureRuns.roomId, roomId),
                eq(adventureRuns.roomCleared, false),
                options.guard,
              ),
            ),
        )
        .onConflictDoNothing(),
      ...drops.slice(1).map((_, offset) =>
        db
          .insert(adventureDrops)
          .select(
            fromWritten({
              runId: sql<string>`${run.id}`.as("run_id"),
              roomId: sql<string>`${roomId}`.as("room_id"),
              ...dropRow(offset + 1),
            }),
          )
          .onConflictDoNothing(),
      ),
      db
        .update(adventureRuns)
        .set(
          boss
            ? {
                ...options.set,
                roomCleared: true,
                status: "cleared",
                updatedAt: now,
                endedAt: now,
              }
            : { ...options.set, roomCleared: true, updatedAt: now },
        )
        .where(and(eq(adventureRuns.id, run.id), written)),
      ...equipmentGrants,
      ...skillGrants,
      ...itemGrants,
      ...materialGrantStatements,
      ...discoveries,
      ...(boss ? [recordStatement(run, "cleared", now, written)] : []),
      ...(battleRoom(run)
        ? enemyStatements(db, userId, options.enemies, now, written)
        : []),
    ]);
    return dropsInRoom(run.id, roomId);
  }

  // 歩数の扉を開けたときの今日の歩数と段。扉のマスにいて、開けたあとだけ返す。
  const stepDoorOf = (run: RunRow) =>
    kindOf(run) === "step-door" && run.doorSteps !== null
      ? {
          stepDoor: {
            steps: run.doorSteps,
            tier: stepDoorTier(run.doorSteps),
          },
        }
      : {};

  // 今いる部屋の出来事（戦闘の勝利・宝箱など）を終え、行き先の一覧から物を落とす（doc/features/drops.md）。
  // 報酬の行はマスで決まったことで選ぶ（ミミックか・試練の条件を満たしたか・歩数の扉の今日の歩数）。
  // 怪しい宝箱は、ミミックに勝ったか、開けずに通り過ぎる（pass）ときだけ終えられる。本物は開けたときに終わる。
  async function clear(
    userId: string,
    roomId: string,
    now: string,
    enemies: Enemies = [],
    options: { pass?: boolean; trialMet?: boolean; evolved?: boolean } = {},
  ) {
    const run = await activeRun(userId);
    if (!run) return { error: "no_adventure" as const };
    const kind = kindOf(run);
    if (run.roomId !== roomId || !hasEvent(kind)) {
      return { error: "room_mismatch" as const };
    }
    if (options.pass && kind !== "shady-chest") {
      return { error: "pass_not_allowed" as const };
    }
    if (run.roomCleared) {
      // 届かなかった応答の再送は、記録したドロップを返す。
      return {
        drops: await dropsInRoom(run.id, roomId),
        ...stepDoorOf(run),
        ...(await readState(userId)),
      };
    }
    if (kind === "shady-chest") {
      // 開けたあと（ミミック）は通り過ぎられない。開ける前は、ミミックに勝ったとは言えない。
      if (options.pass) {
        if (run.mimic !== null) return { error: "pass_not_allowed" as const };
        return markCleared(userId, run, now, isNull(adventureRuns.mimic));
      }
      if (run.mimic !== true) return { error: "chest_not_opened" as const };
    }

    // 歩数の扉は、扉を開けたとき（マスを終えたとき）の今日の歩数で報酬を決める。
    const steps =
      kind === "step-door"
        ? await createHealthRepository(binding).todaySteps(userId, now)
        : null;
    const rolled = rollRoomDrops(run.destinationId, kind as EventKind, random, {
      mimic: run.mimic === true,
      trialMet: options.trialMet ?? false,
      evolved: kind === "altar" && (options.evolved ?? false),
      ...(steps === null ? {} : { stepDoorTier: stepDoorTier(steps) }),
    });
    // 落とす物のないマス（休憩・行商人）は、進行中の冒険のそのマスを終えた印だけを付ける。
    if (rolled.length === 0 && kind !== "boss") {
      return markCleared(userId, run, now);
    }

    const boss = kind === "boss";
    const previousBest = boss ? await bestFloor(userId, run.destinationId) : 0;
    const recorded = await award(userId, run, rolled, now, {
      enemies,
      ...(steps === null ? {} : { set: { doorSteps: steps } }),
    });
    if (recorded.length === 0) return { error: "adventure_changed" as const };
    const doorState =
      steps === null ? {} : { stepDoor: { steps, tier: stepDoorTier(steps) } };
    const state = await readState(userId);
    if (!boss) return { drops: recorded, ...doorState, ...state };
    const ended = await db
      .select()
      .from(adventureRuns)
      .where(eq(adventureRuns.id, run.id))
      .get();
    return {
      drops: recorded,
      result: await result(ended as RunRow, previousBest),
      ...state,
    };
  }

  // 開けた怪しい宝箱の結果。ミミックなら戦闘のマスのまま、本物なら記録したドロップを返す。開ける前は null。
  async function openedChest(userId: string, run: RunRow) {
    if (run.mimic === null) return null;
    return {
      drops: run.mimic ? [] : await dropsInRoom(run.id, run.roomId),
      ...(await readState(userId)),
    };
  }

  // 今いる怪しい宝箱を開ける。半分の確率でミミックとの戦闘になり、本物ならその場で強い報酬を1つ多く落として終える。
  // 結果は冒険に保存し、同じマスでの再送には決めた結果を返す。
  async function open(userId: string, roomId: string, now: string) {
    const run = await activeRun(userId);
    if (!run) return { error: "no_adventure" as const };
    if (run.roomId !== roomId || kindOf(run) !== "shady-chest") {
      return { error: "room_mismatch" as const };
    }
    const opened = await openedChest(userId, run);
    if (opened) return opened;
    // 開けずに通り過ぎたマスは、もう開けられない。
    if (run.roomCleared) return { error: "room_cleared" as const };

    if (random() < MIMIC_CHANCE) {
      await db
        .update(adventureRuns)
        .set({ mimic: true, updatedAt: now })
        .where(
          and(
            eq(adventureRuns.id, run.id),
            eq(adventureRuns.status, "active"),
            eq(adventureRuns.roomId, roomId),
            eq(adventureRuns.roomCleared, false),
            isNull(adventureRuns.mimic),
          ),
        );
    } else {
      await award(
        userId,
        run,
        rollRoomDrops(run.destinationId, "shady-chest", random),
        now,
        {
          enemies: [],
          guard: isNull(adventureRuns.mimic),
          set: { mimic: false },
        },
      );
    }
    // 同時の要求で先に決まった結果があれば、それを返す。
    const after = await activeRun(userId);
    const decided =
      after && after.id === run.id && after.roomId === roomId
        ? await openedChest(userId, after)
        : null;
    return decided ?? { error: "adventure_changed" as const };
  }

  function findRevive(userId: string, requestId: string) {
    return db
      .select()
      .from(adventureRevives)
      .where(
        and(
          eq(adventureRevives.userId, userId),
          eq(adventureRevives.requestId, requestId),
        ),
      )
      .get();
  }

  async function recordedRevive(
    userId: string,
    request: Revive,
    row: NonNullable<Awaited<ReturnType<typeof findRevive>>>,
  ) {
    if (row.roomId !== request.roomId) {
      return { error: "request_conflict" as const };
    }
    return {
      revive: {
        roomId: row.roomId,
        runes: row.runes,
        balanceAfter: row.balanceAfter,
      },
      ...(await readState(userId)),
    };
  }

  // 負けた戦闘からルーンで復活する。費用はサーバーで計算し、残高と復活の回数を条件にした
  // 1回のbatchで、記録・ルーンの減算・回数の更新をまとめて行う。
  async function revive(userId: string, request: Revive, now: string) {
    const replay = await findRevive(userId, request.requestId);
    if (replay) return recordedRevive(userId, request, replay);
    const run = await activeRun(userId);
    if (!run) return { error: "no_adventure" as const };
    if (run.roomId !== request.roomId || !battleRoom(run) || run.roomCleared) {
      return { error: "room_mismatch" as const };
    }

    const id = crypto.randomUUID();
    const runes = reviveCost(run.revives);
    const balance = runeBalance(userId);
    const written = sql`EXISTS (SELECT 1 FROM ${adventureRevives} WHERE ${adventureRevives.id} = ${id})`;
    await db.batch([
      db
        .insert(adventureRevives)
        .select(
          db
            .select({
              id: sql<string>`${id}`.as("id"),
              userId: adventureRuns.userId,
              runId: adventureRuns.id,
              requestId: sql<string>`${request.requestId}`.as("request_id"),
              roomId: adventureRuns.roomId,
              runes: sql<number>`${runes}`.as("runes"),
              balanceAfter: sql<number>`0`.as("balance_after"),
              createdAt: sql<string>`${now}`.as("created_at"),
            })
            .from(adventureRuns)
            .where(
              and(
                eq(adventureRuns.id, run.id),
                eq(adventureRuns.status, "active"),
                eq(adventureRuns.roomId, request.roomId),
                eq(adventureRuns.roomCleared, false),
                eq(adventureRuns.revives, run.revives),
                sql`${balance} >= ${runes}`,
              ),
            ),
        )
        .onConflictDoNothing({
          target: [adventureRevives.userId, adventureRevives.requestId],
        }),
      debitRunes(db, userId, runes, written, now),
      db
        .update(adventureRuns)
        .set({ revives: sql`${adventureRuns.revives} + 1`, updatedAt: now })
        .where(and(eq(adventureRuns.id, run.id), written)),
      db
        .update(adventureRevives)
        .set({ balanceAfter: sql`COALESCE(${balance}, 0)` })
        .where(eq(adventureRevives.id, id)),
    ]);

    const row = await findRevive(userId, request.requestId);
    if (row) return recordedRevive(userId, request, row);
    // 書き込めなかった理由を、今の冒険の状態で見分ける。
    const after = await activeRun(userId);
    return {
      error:
        !after ||
        after.revives !== run.revives ||
        after.roomId !== request.roomId ||
        after.roomCleared
          ? ("adventure_changed" as const)
          : ("insufficient_runes" as const),
    };
  }

  function findPurchase(userId: string, requestId: string) {
    return db
      .select()
      .from(adventurePurchases)
      .where(
        and(
          eq(adventurePurchases.userId, userId),
          eq(adventurePurchases.requestId, requestId),
        ),
      )
      .get();
  }

  async function recordedPurchase(
    userId: string,
    request: Buy,
    row: PurchaseRow,
  ) {
    if (row.roomId !== request.roomId || row.idx !== request.index) {
      return { error: "request_conflict" as const };
    }
    return {
      purchase: {
        roomId: row.roomId,
        index: row.idx,
        runes: row.runes,
        balanceAfter: row.balanceAfter,
      },
      ...(await readState(userId)),
    };
  }

  // 今いる行商人のマスで、品物を1つルーンで買う。値段は品ぞろえを作ったときにサーバーが決めた額で、
  // 購入の記録・ルーンの減算・持ち物への追加・ライブラリの見つけた記録を、残高と売れていないことを条件にした
  // 1回のbatchで行う。要求IDで再送を見分け、品物ごとの一意で同じ品物を二度売らない。
  // マスを終えたあとも、次のマスへ進むまでは買える。
  async function buy(userId: string, request: Buy, now: string) {
    const replay = await findPurchase(userId, request.requestId);
    if (replay) return recordedPurchase(userId, request, replay);
    const run = await activeRun(userId);
    if (!run) return { error: "no_adventure" as const };
    if (run.roomId !== request.roomId || kindOf(run) !== "merchant") {
      return { error: "room_mismatch" as const };
    }
    const goods = await db
      .select()
      .from(adventureMerchantGoods)
      .where(
        and(
          eq(adventureMerchantGoods.runId, run.id),
          eq(adventureMerchantGoods.roomId, run.roomId),
          eq(adventureMerchantGoods.idx, request.index),
        ),
      )
      .get();
    if (!goods) return { error: "goods_not_found" as const };

    const id = crypto.randomUUID();
    const balance = runeBalance(userId);
    const written = sql`EXISTS (SELECT 1 FROM ${adventurePurchases} WHERE ${adventurePurchases.id} = ${id})`;
    const fromWritten = writtenSelect(written);
    const category = goods.category as
      | "skill"
      | "weapon"
      | "armor"
      | "accessory";
    await db.batch([
      db
        .insert(adventurePurchases)
        .select(
          db
            .select({
              id: sql<string>`${id}`.as("id"),
              userId: adventureRuns.userId,
              runId: adventureRuns.id,
              requestId: sql<string>`${request.requestId}`.as("request_id"),
              roomId: adventureRuns.roomId,
              idx: sql<number>`${goods.idx}`.as("idx"),
              runes: sql<number>`${goods.price}`.as("runes"),
              balanceAfter: sql<number>`0`.as("balance_after"),
              createdAt: sql<string>`${now}`.as("created_at"),
            })
            .from(adventureRuns)
            .where(
              and(
                eq(adventureRuns.id, run.id),
                eq(adventureRuns.status, "active"),
                eq(adventureRuns.roomId, request.roomId),
                eq(adventureRuns.roomKind, "merchant"),
                sql`${balance} >= ${goods.price}`,
              ),
            ),
        )
        // 要求IDの再送と、売れた品物の二度目の購入のどちらでも書かない。
        .onConflictDoNothing(),
      debitRunes(db, userId, goods.price, written, now),
      category === "skill"
        ? skillGrant(
            fromWritten,
            userId,
            goods.entryId,
            goods.multiplierPercent,
            now,
          )
        : equipmentGrant(
            fromWritten,
            userId,
            goods.entryId,
            goods.multiplierPercent,
            now,
          ),
      ...discoveryStatementsWhen(
        db,
        userId,
        category,
        [goods.entryId],
        now,
        written,
      ),
      db
        .update(adventurePurchases)
        .set({ balanceAfter: sql`COALESCE(${balance}, 0)` })
        .where(eq(adventurePurchases.id, id)),
    ]);

    const row = await findPurchase(userId, request.requestId);
    if (row) return recordedPurchase(userId, request, row);
    // 書き込めなかった理由を、品物と今の冒険の状態で見分ける。
    const sold = await db
      .select({ id: adventurePurchases.id })
      .from(adventurePurchases)
      .where(
        and(
          eq(adventurePurchases.runId, run.id),
          eq(adventurePurchases.roomId, goods.roomId),
          eq(adventurePurchases.idx, goods.idx),
        ),
      )
      .get();
    if (sold) return { error: "sold_out" as const };
    const after = await activeRun(userId);
    return {
      error:
        !after || after.id !== run.id || after.roomId !== request.roomId
          ? ("adventure_changed" as const)
          : ("insufficient_runes" as const),
    };
  }

  // 冒険を終える。負けて帰還する（defeat）か、自分からやめる（retreat）。手に入れた物は残る。
  async function end(
    userId: string,
    reason: EndReason,
    now: string,
    enemies: Enemies = [],
  ) {
    const run = await activeRun(userId);
    if (!run) return { error: "no_adventure" as const };
    if (reason === "defeat" && (!battleRoom(run) || run.roomCleared)) {
      return { error: "room_mismatch" as const };
    }
    const status: EndStatus = reason === "defeat" ? "defeated" : "retreated";
    const previousBest = await bestFloor(userId, run.destinationId);
    const written = sql`EXISTS (SELECT 1 FROM ${adventureRuns} WHERE ${adventureRuns.id} = ${run.id} AND ${adventureRuns.status} = ${status} AND ${adventureRuns.endedAt} = ${now})`;
    await db.batch([
      db
        .update(adventureRuns)
        .set({ status, updatedAt: now, endedAt: now })
        .where(
          and(eq(adventureRuns.id, run.id), eq(adventureRuns.status, "active")),
        ),
      recordStatement(run, status, now, written),
      ...(reason === "defeat"
        ? enemyStatements(db, userId, enemies, now, written)
        : []),
    ]);
    const ended = await db
      .select()
      .from(adventureRuns)
      .where(eq(adventureRuns.id, run.id))
      .get();
    if (!ended || ended.status !== status || ended.endedAt !== now) {
      return { error: "adventure_changed" as const };
    }
    return {
      result: await result(ended, previousBest),
      ...(await readState(userId)),
    };
  }

  return { getState, start, move, clear, open, buy, revive, end };
}
