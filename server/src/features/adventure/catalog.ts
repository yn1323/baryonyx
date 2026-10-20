import type { Quality } from "../../shared/quality.js";

// 冒険先と、入口から最奥の間までに通る部屋の数、報酬の品質の上がり方（doc/features/stage-progression.md）。
// 行き先ごとの名前・背景・敵の組み合わせ・敵のLvはクライアント（AdventureCatalog.cs）が持つ。
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
  // 報酬の品質の上がり方（百分率）。奥の行き先ほど、品質Sの重みをこの分だけ、Aの重みをその半分だけ増やす。
  rewardBoost: number;
};

// 敵が強い順に奥の行き先とし、部屋の数と報酬の上がり方を増やす（2026-10-20）。
export const DESTINATIONS: readonly Destination[] = [
  { id: "forest-ruins", roomCount: 8, rewardBoost: 0 },
  { id: "port-portoria", roomCount: 9, rewardBoost: 20 },
  { id: "glaciera-cave", roomCount: 9, rewardBoost: 40 },
  { id: "hush-mire", roomCount: 10, rewardBoost: 60 },
  { id: "almaja-tomb", roomCount: 10, rewardBoost: 80 },
  { id: "moonshade-graveyard", roomCount: 11, rewardBoost: 100 },
  { id: "volgan-volcano", roomCount: 11, rewardBoost: 130 },
  { id: "neve-peaks", roomCount: 12, rewardBoost: 160 },
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

// 出来事を終えた部屋で手に入れるACTボーナスの品質の重み（仮設定）。倍率は、選んだ品質の幅の中から均等に選ぶ。
// ボーナスの種類は均等に選ぶ。冒険で手に入る場所と確率は doc/features/step-bonus.md の未決事項。
export const REWARD_QUALITY_WEIGHTS: Record<
  EventKind,
  Partial<Record<Quality, number>>
> = {
  battle: { C: 75, B: 20, A: 5 },
  elite: { C: 30, B: 40, A: 25, S: 5 },
  treasure: { C: 30, B: 40, A: 25, S: 5 },
  boss: { B: 40, A: 45, S: 15 },
};

// 行き先の報酬の上がり方を入れた、品質の重み。S は rewardBoost の分、A はその半分だけ重くする。
export function rewardQualityWeights(
  kind: EventKind,
  rewardBoost: number,
): Partial<Record<Quality, number>> {
  const base = REWARD_QUALITY_WEIGHTS[kind];
  const grow = (weight: number | undefined, percent: number) =>
    weight === undefined
      ? undefined
      : Math.round((weight * (100 + percent)) / 100);
  const weights: Partial<Record<Quality, number>> = { ...base };
  if (base.A !== undefined) weights.A = grow(base.A, rewardBoost / 2);
  if (base.S !== undefined) weights.S = grow(base.S, rewardBoost);
  return weights;
}
