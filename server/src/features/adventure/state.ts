import { and, eq, getTableColumns } from "drizzle-orm";
import type { BatchResponse } from "drizzle-orm/batch";
import {
  batchThenRead,
  type Database,
  type Statement,
} from "../../shared/db.js";
import { qualityFields, qualityOf } from "../../shared/quality.js";
import { selectRuneBalance } from "../exercise-rewards/wallet.js";
import { maxPercentOf } from "../inventory/catalog.js";
import {
  findDestination,
  isBattle,
  type RoomKind,
  reviveCost,
  stepDoorTier,
} from "./catalog.js";
import {
  adventureDrops,
  adventureMerchantGoods,
  adventurePurchases,
  adventureRecords,
  adventureRuns,
} from "./db-schema.js";

// 冒険の読み込み。D1との往復の回数がそのまま待ち時間になるため、書き込みと同じ batch の後ろに
// 読み込みの文を並べ、1回の往復で書いて読む（doc/rules/backend-design.md のDrizzleによるDB操作）。

export type RunRow = typeof adventureRuns.$inferSelect;
export type DropRow = typeof adventureDrops.$inferSelect;
export type EndStatus = "cleared" | "defeated" | "retreated";

export const kindOf = (run: RunRow) => run.roomKind as RoomKind;

// 戦闘のマスか。開けてミミックだった怪しい宝箱も、勝つまで戦闘のマスとして扱う（復活・負けて帰還ができる）。
export const battleRoom = (run: RunRow) =>
  isBattle(kindOf(run)) ||
  (kindOf(run) === "shady-chest" && run.mimic === true);

const activeOf = (userId: string) =>
  and(eq(adventureRuns.userId, userId), eq(adventureRuns.status, "active"));

// 応答の1つのドロップ。アイテムと強化素材は倍率と品質を持たないため、その項目を返さない。
export const dropOf = (row: DropRow) => ({
  roomId: row.roomId,
  category: row.category,
  id: row.entryId,
  ...qualityFields(
    row.multiplierPercent,
    maxPercentOf(row.category, row.entryId),
  ),
});

/** その冒険で落ちた物を、落ちた順に読む文。 */
export function dropsOfRun(db: Database, runId: string) {
  return db
    .select()
    .from(adventureDrops)
    .where(eq(adventureDrops.runId, runId))
    .orderBy(adventureDrops.createdAt, adventureDrops.seq);
}

/**
 * 冒険の状態を読む文。進行中の冒険・行き先ごとの記録・所持ルーン・進行中の冒険のドロップ・今いる行商人のマスの品物。
 * 進行中の冒険のIDを知らなくても読めるよう、ドロップと品物は進行中の冒険と結んで読む。
 */
export function stateReads(db: Database, userId: string) {
  return [
    db.select().from(adventureRuns).where(activeOf(userId)),
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
    db
      .select(getTableColumns(adventureDrops))
      .from(adventureDrops)
      .innerJoin(adventureRuns, eq(adventureRuns.id, adventureDrops.runId))
      .where(activeOf(userId))
      .orderBy(adventureDrops.createdAt, adventureDrops.seq),
    db
      .select({
        goods: getTableColumns(adventureMerchantGoods),
        purchaseId: adventurePurchases.id,
      })
      .from(adventureMerchantGoods)
      .innerJoin(
        adventureRuns,
        and(
          eq(adventureRuns.id, adventureMerchantGoods.runId),
          eq(adventureRuns.roomId, adventureMerchantGoods.roomId),
        ),
      )
      .leftJoin(
        adventurePurchases,
        and(
          eq(adventurePurchases.runId, adventureMerchantGoods.runId),
          eq(adventurePurchases.roomId, adventureMerchantGoods.roomId),
          eq(adventurePurchases.idx, adventureMerchantGoods.idx),
        ),
      )
      .where(activeOf(userId))
      .orderBy(adventureMerchantGoods.idx),
  ] as const;
}

type StateRows = BatchResponse<ReturnType<typeof stateReads>>;

// 行商人のマスの品物と、売れたか。
function merchantOf(rows: StateRows[4]) {
  return rows.map(({ goods, purchaseId }) => ({
    index: goods.idx,
    category: goods.category,
    id: goods.entryId,
    multiplierPercent: goods.multiplierPercent,
    quality: qualityOf(
      goods.multiplierPercent,
      maxPercentOf(goods.category, goods.entryId) as number,
    ),
    price: goods.price,
    sold: purchaseId !== null,
  }));
}

/**
 * 読み込みの結果から、冒険の応答を作る。進行中の冒険（道を作る種と部屋の数、今いる部屋、この冒険で手に入れた物を含む）、
 * 行き先ごとの記録、所持ルーン。今いるマスで決まったことも返す。mimic は開けた怪しい宝箱がミミックだったか、
 * trial は試練のマスの条件、merchant は行商人のマスの品物、awakening は宝箱から固有スキルの覚醒が出るか。
 * trial・merchant・awakening は、そのマスにいるときだけ項目を返す（awakening は出るときだけ）。
 * activeRun は進行中の冒険の行で、続く判定に使う。
 */
export function stateOf([runs, records, wallets, drops, goods]: StateRows) {
  const run = runs[0];
  const destination = run ? findDestination(run.destinationId) : undefined;
  return {
    activeRun: run as RunRow | undefined,
    // 進行中の冒険で落ちた物と、今いる行商人のマスの品物の行。マスごとのドロップや品物を確かめるときに使う。
    dropRows: drops,
    goodsRows: goods,
    state: {
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
              route: run.route,
              revives: run.revives,
              reviveCost: reviveCost(run.revives),
              mimic: run.mimic === true,
              ...(run.roomKind === "trial" && run.trial !== null
                ? { trial: run.trial }
                : {}),
              ...(run.roomKind === "treasure" && run.awakening === true
                ? { awakening: true }
                : {}),
              ...(run.roomKind === "merchant"
                ? { merchant: merchantOf(goods) }
                : {}),
              drops: drops.map(dropOf),
            }
          : null,
      records,
      runes: wallets[0]?.balance ?? 0,
    },
  };
}

/**
 * statements のあとに冒険の状態を読む文を足し、1回の往復で書いて読む。results は statements の結果。
 */
export async function batchWithState<const T extends readonly Statement[]>(
  db: Database,
  userId: string,
  statements: T,
) {
  const { results, reads } = await batchThenRead(
    db,
    statements,
    stateReads(db, userId),
  );
  return { results, ...stateOf(reads) };
}

export type AdventureRead = Awaited<ReturnType<typeof batchWithState>>;

// 歩数の扉を開けたときの今日の歩数と段。扉のマスにいて、開けたあとだけ返す。
export const stepDoorOf = (run: RunRow) =>
  kindOf(run) === "step-door" && run.doorSteps !== null
    ? {
        stepDoor: {
          steps: run.doorSteps,
          tier: stepDoorTier(run.doorSteps),
        },
      }
    : {};
