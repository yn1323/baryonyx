import { pickWeighted, type Random } from "../../shared/random.js";

// 冒険先と、入口から最奥の間までに通るマスの数（doc/features/stage-progression.md）。
// マスで落ちる物は drops.ts（doc/features/drops.md）が決める。
// 行き先ごとの名前・背景・敵の組み合わせ・敵のLvはクライアント（AdventureCatalog.cs）が持つ。
// 分岐ルートの道は、冒険を始めたときにサーバーが決めた乱数の種から、クライアントが毎回同じ形に作る
// （doc/plans/2026-10-04-exploration-route-map.md）。サーバーは道そのものを持たず、
// 今いるマスの階と種類を受け取って、進み方と報酬だけを判定する。

// 入口の次から選んで進むマスの種類（doc/features/stage-progression.md のマスの種類）。
export const EVENT_KINDS = [
  "battle",
  "elite",
  "treasure",
  "boss",
  "rest",
  "merchant",
  "shady-chest",
  "trial",
  "step-door",
  "altar",
  "mystery",
] as const;

export const ROOM_KINDS = ["start", ...EVENT_KINDS] as const;

export type RoomKind = (typeof ROOM_KINDS)[number];

export type EventKind = (typeof EVENT_KINDS)[number];

export type Destination = {
  id: string;
  // 入口（第1層）と最奥の間のあいだに通るマスの数。1階ごとに1マスを通る。
  roomCount: number;
  // ダンジョンがある街（街道の世界地図の街のマスのID）。街のマスをクリアすると解放される
  // （doc/features/stage-progression.md の街とダンジョン）。
  town: string;
};

// 敵が強い順に奥の行き先とし、マスの数を増やす（2026-10-20）。
export const DESTINATIONS: readonly Destination[] = [
  { id: "forest-ruins", roomCount: 8, town: "porta" },
  { id: "port-portoria", roomCount: 9, town: "portoria" },
  { id: "glaciera-cave", roomCount: 9, town: "fredda" },
  { id: "hush-mire", roomCount: 10, town: "palude" },
  { id: "almaja-tomb", roomCount: 10, town: "duna" },
  { id: "moonshade-graveyard", roomCount: 11, town: "campanella" },
  { id: "volgan-volcano", roomCount: 11, town: "forgia" },
  { id: "neve-peaks", roomCount: 12, town: "cima" },
];

export const DESTINATION_IDS = DESTINATIONS.map(
  (destination) => destination.id,
) as [string, ...string[]];

export const ENTRANCE_ROOM_ID = "entrance";

export function findDestination(id: string) {
  return DESTINATIONS.find((destination) => destination.id === id);
}

// 最奥の間の階。入口が第1層、道中のマスが第2層から続く。
export function bossFloor(destination: Destination) {
  return destination.roomCount + 2;
}

// 道を作る乱数の種（1以上2^31未満）。
export function newSeed(random: Random) {
  return 1 + Math.floor(random() * 0x7ffffffe);
}

// 出来事（戦闘・宝箱など）のあるマス。着いた時点では終わっておらず、終えるまで先へ進めない。
export function hasEvent(kind: RoomKind) {
  return kind !== "start";
}

export function isBattle(kind: RoomKind) {
  return (
    kind === "battle" || kind === "elite" || kind === "boss" || kind === "trial"
  );
}

// 決まった階以外で、そのマスを置ける最初の階。道中の最初の階（第2層）を1つ目と数える
// （doc/features/stage-progression.md の置ける階）。進化の祭壇は、決まった階以外ならどこでも置き換える。
const EARLIEST: Record<Exclude<EventKind, "boss">, number> = {
  battle: 2,
  elite: 3,
  treasure: 2,
  rest: 4,
  merchant: 3,
  "shady-chest": 3,
  trial: 4,
  "step-door": 3,
  altar: 2,
  mystery: 3,
};

// 決まった階のマス。1つ目は魔物、道中が5マス以上なら真ん中は宝箱、3マス以上なら最後（最奥の直前）は休憩。
export function fixedKindOf(
  destination: Destination,
  floor: number,
): EventKind | null {
  const count = destination.roomCount;
  const index = floor - 1;
  if (floor === bossFloor(destination)) return "boss";
  if (index === 1) return "battle";
  if (count >= 5 && index === Math.floor(count / 2) + 1) return "treasure";
  if (count >= 3 && index === count) return "rest";
  return null;
}

// そのマスを、その階に置けるか。道はクライアントが種から作るため、サーバーは置ける階だけを確かめる。
export function canPlace(
  destination: Destination,
  floor: number,
  kind: EventKind,
) {
  if (floor < 2 || floor > bossFloor(destination)) return false;
  const fixed = fixedKindOf(destination, floor);
  if (fixed !== null) return kind === fixed;
  return kind !== "boss" && floor - 1 >= EARLIEST[kind];
}

// 「？」のマスの中身と重み（doc/features/stage-progression.md のマスの中身）。入ったときにサーバーが1つ選び、
// その種類のマスとして保存する。端末が決めると、選び直せてしまうため。
export const MYSTERY_KINDS = [
  { kind: "battle", weight: 45 },
  { kind: "treasure", weight: 25 },
  { kind: "merchant", weight: 15 },
  { kind: "rest", weight: 15 },
] as const satisfies readonly { kind: EventKind; weight: number }[];

export type MysteryKind = (typeof MYSTERY_KINDS)[number]["kind"];

export function pickMysteryKind(random: Random): MysteryKind {
  return pickWeighted(MYSTERY_KINDS, (entry) => entry.weight, random).kind;
}

// 怪しい宝箱がミミックである確率（doc/features/stage-progression.md のマスの中身）。開けたときにサーバーが決める。
export const MIMIC_CHANCE = 0.5;

// 宝箱から固有スキルの覚醒が出る確率（仮。doc/features/skill-evolution.md の覚醒が起きる場面）。
// 宝箱のマスに入ったときにサーバーが決める。覚醒はドロップに加えて出る。
export const CHEST_AWAKENING_CHANCE = 0.3;

export function rollChestAwakening(random: Random): boolean {
  return random() < CHEST_AWAKENING_CHANCE;
}

// 試練の条件。試練のマスに入ったときにサーバーが1つ選ぶ。満たしたかはクライアントが判定して clear で送る。
// 6ターン以内に勝つ・誰も倒れずに勝つ・HPを全員半分以上残して勝つ。
export const TRIAL_CONDITIONS = [
  "within-6-turns",
  "no-one-falls",
  "half-hp",
] as const;

export type TrialCondition = (typeof TRIAL_CONDITIONS)[number];

export function pickTrialCondition(random: Random): TrialCondition {
  return TRIAL_CONDITIONS[
    Math.min(
      TRIAL_CONDITIONS.length - 1,
      Math.floor(random() * TRIAL_CONDITIONS.length),
    )
  ];
}

// 歩数の扉の段。今日の歩数が段の歩数に届くほど報酬が増える（doc/features/stage-progression.md の歩数の扉の報酬）。
// 0段は3,000歩未満で、扉は歩いていなくても開く。
export const STEP_DOOR_STEPS = [3000, 5000, 8000] as const;

export type StepDoorTier = 0 | 1 | 2 | 3;

export function stepDoorTier(steps: number): StepDoorTier {
  return STEP_DOOR_STEPS.filter((threshold) => steps >= threshold)
    .length as StepDoorTier;
}

// 復活に使うルーン。2回目以降は上がる（仮設定。doc/features/combat.md の未決事項）。
export const REVIVE_COST_BASE = 100;

export function reviveCost(revivesSoFar: number) {
  return REVIVE_COST_BASE * (revivesSoFar + 1);
}
