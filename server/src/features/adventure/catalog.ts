import type { StepBonusRank } from "../step-bonus/catalog.js";

// 冒険先と、分岐ルートの部屋。最初の1周を試すための仮の構成で、行き先の数・階の数・
// 敵の組み合わせ・報酬は決まっていない（doc/features/stage-progression.md）。
// 表示用の名前・背景・敵の絵はクライアントの定義が持ち、サーバーは進み方と報酬だけを判定する。

export const ROOM_KINDS = [
  "start",
  "battle",
  "elite",
  "treasure",
  "boss",
] as const;

export type RoomKind = (typeof ROOM_KINDS)[number];

export type Room = {
  id: string;
  floor: number;
  kind: RoomKind;
  // 戦う敵の組み合わせ。戦わない部屋は空。
  encounter: string;
  // 次に進める部屋。最奥のボスの部屋は空。
  next: readonly string[];
};

export type Destination = {
  id: string;
  rooms: readonly Room[];
};

export const DESTINATIONS: readonly Destination[] = [
  {
    id: "forest-ruins",
    rooms: [
      {
        id: "entrance",
        floor: 1,
        kind: "start",
        encounter: "",
        next: ["moss-hall", "hidden-store"],
      },
      {
        id: "moss-hall",
        floor: 2,
        kind: "battle",
        encounter: "forest-pack",
        next: ["root-gallery", "guardian-gate"],
      },
      {
        id: "hidden-store",
        floor: 2,
        kind: "treasure",
        encounter: "",
        next: ["root-gallery", "guardian-gate"],
      },
      {
        id: "root-gallery",
        floor: 3,
        kind: "battle",
        encounter: "forest-pack",
        next: ["sanctum"],
      },
      {
        id: "guardian-gate",
        floor: 3,
        kind: "elite",
        encounter: "forest-elite",
        next: ["sanctum"],
      },
      {
        id: "sanctum",
        floor: 4,
        kind: "boss",
        encounter: "forest-boss",
        next: [],
      },
    ],
  },
];

export const DESTINATION_IDS = DESTINATIONS.map(
  (destination) => destination.id,
) as [string, ...string[]];

export function findDestination(id: string) {
  return DESTINATIONS.find((destination) => destination.id === id);
}

export function findRoom(destination: Destination, roomId: string) {
  return destination.rooms.find((room) => room.id === roomId);
}

export function startRoom(destination: Destination) {
  const room = destination.rooms.find((entry) => entry.kind === "start");
  if (!room) throw new Error(`${destination.id} has no start room`);
  return room;
}

// 出来事（戦闘・宝箱）のある部屋。着いた時点では終わっておらず、終えるまで先へ進めない。
export function hasEvent(room: Room) {
  return room.kind !== "start";
}

export function isBattle(room: Room) {
  return (
    room.kind === "battle" || room.kind === "elite" || room.kind === "boss"
  );
}

// 復活に使うルーン。2回目以降は上がる（仮設定。doc/features/combat.md の未決事項）。
export const REVIVE_COST_BASE = 100;

export function reviveCost(revivesSoFar: number) {
  return REVIVE_COST_BASE * (revivesSoFar + 1);
}

// 出来事を終えた部屋で手に入れるUPTボーナスのランクの重み（仮設定）。
// ボーナスの種類は均等に選ぶ。冒険で手に入る場所と確率は doc/features/step-bonus.md の未決事項。
export const REWARD_RANK_WEIGHTS: Record<
  Exclude<RoomKind, "start">,
  Partial<Record<StepBonusRank, number>>
> = {
  battle: { E: 40, D: 35, C: 20, B: 5 },
  elite: { D: 30, C: 40, B: 25, A: 5 },
  treasure: { D: 30, C: 40, B: 25, A: 5 },
  boss: { B: 40, A: 45, S: 15 },
};
