import type { StepBonusRank } from "../step-bonus/catalog.js";

// 冒険先と、入口から最奥の間までに通る部屋の数。行き先の数・敵の組み合わせ・報酬は決まっていない
// （doc/features/stage-progression.md）。
// 分岐ルートの道は、冒険を始めたときにサーバーが決めた乱数の種から、クライアントが毎回同じ形に作る
// （doc/plans/2026-10-04-exploration-route-map.md）。サーバーは道そのものを持たず、
// 今いる部屋の階と種類を受け取って、進み方と報酬だけを判定する。

export const ROOM_KINDS = [
  "start",
  "battle",
  "elite",
  "treasure",
  "boss",
] as const;

export type RoomKind = (typeof ROOM_KINDS)[number];

// 入口の次から選んで進む部屋の種類。
export const EVENT_KINDS = ["battle", "elite", "treasure", "boss"] as const;

export type EventKind = (typeof EVENT_KINDS)[number];

export type Destination = {
  id: string;
  // 入口（第1層）と最奥の間のあいだに通る部屋の数。1階ごとに1部屋を通る。
  roomCount: number;
};

export const DESTINATIONS: readonly Destination[] = [
  { id: "forest-ruins", roomCount: 8 },
];

export const DESTINATION_IDS = DESTINATIONS.map(
  (destination) => destination.id,
) as [string, ...string[]];

export const ENTRANCE_ROOM_ID = "entrance";

export function findDestination(id: string) {
  return DESTINATIONS.find((destination) => destination.id === id);
}

// 最奥の間の階。入口が第1層、道中の部屋が第2層から続く。
export function bossFloor(destination: Destination) {
  return destination.roomCount + 2;
}

// 道を作る乱数の種（1以上2^31未満）。
export function newSeed(random: () => number) {
  return 1 + Math.floor(random() * 0x7ffffffe);
}

// 出来事（戦闘・宝箱）のある部屋。着いた時点では終わっておらず、終えるまで先へ進めない。
export function hasEvent(kind: RoomKind) {
  return kind !== "start";
}

export function isBattle(kind: RoomKind) {
  return kind === "battle" || kind === "elite" || kind === "boss";
}

// 復活に使うルーン。2回目以降は上がる（仮設定。doc/features/combat.md の未決事項）。
export const REVIVE_COST_BASE = 100;

export function reviveCost(revivesSoFar: number) {
  return REVIVE_COST_BASE * (revivesSoFar + 1);
}

// 出来事を終えた部屋で手に入れるACTボーナスのランクの重み（仮設定）。
// ボーナスの種類は均等に選ぶ。冒険で手に入る場所と確率は doc/features/step-bonus.md の未決事項。
export const REWARD_RANK_WEIGHTS: Record<
  EventKind,
  Partial<Record<StepBonusRank, number>>
> = {
  battle: { E: 40, D: 35, C: 20, B: 5 },
  elite: { D: 30, C: 40, B: 25, A: 5 },
  treasure: { D: 30, C: 40, B: 25, A: 5 },
  boss: { B: 40, A: 45, S: 15 },
};
