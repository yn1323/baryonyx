import {
  pickQuality,
  type Quality,
  rollMultiplierPercent,
} from "../../shared/quality.js";
import { pickWeighted, type Random } from "../../shared/random.js";
import {
  AWAKENING_SHARD_ID,
  POLISH_DROP_CHANCE,
  POLISH_DROP_WEIGHTS,
  POLISH_IDS,
  type PolishDepth,
  SHARD_DROP_CHANCE,
} from "../enhancement/catalog.js";
import type { EquipmentId } from "../equipment/catalog.js";
import { maxPercentOf, type Stars } from "../inventory/catalog.js";
import type { ItemId } from "../items/catalog.js";
import type { CardSkillId } from "../party/catalog.js";
import { DESTINATIONS, type EventKind, type StepDoorTier } from "./catalog.js";

// 報酬の重みの行（doc/features/drops.md の仮設定）。ほかのマスは、どれかの行を借りる。
export const REWARD_ROWS = ["battle", "elite", "treasure", "boss"] as const;

export type RewardRow = (typeof REWARD_ROWS)[number];

// マスで決まったこと。報酬の行を選ぶのに使う。
// mimic は怪しい宝箱がミミックだったか、trialMet は試練の条件を満たして勝ったか（クライアントの判定を信じる）、
// stepDoorTier は歩数の扉を開けたときの今日の歩数の段。
export type RoomOutcome = {
  mimic?: boolean;
  trialMet?: boolean;
  stepDoorTier?: StepDoorTier;
  evolved?: boolean;
};

// 歩数の扉の段ごとの報酬。3,000歩未満は魔物、3,000〜4,999歩は強い報酬、5,000〜7,999歩は強い報酬で1つ多く、
// 8,000歩以上は最大の報酬。
const STEP_DOOR_REWARDS: Record<
  StepDoorTier,
  { row: RewardRow; extra: number }
> = {
  0: { row: "battle", extra: 0 },
  1: { row: "elite", extra: 0 },
  2: { row: "elite", extra: 1 },
  3: { row: "boss", extra: 0 },
};

// マスの種類ごとの報酬の行と、落ちる数に足す数。報酬のないマスは null
// （doc/features/stage-progression.md のマスの中身）。「最大の報酬」は最奥の間の行を借りるが、
// 主の物（bossOnly）は最奥の間でだけ落ちるため、最奥の間のほかは入れない（rollDrops）。
// 怪しい宝箱は、ミミックに勝てば最大の報酬、本物なら強い報酬で1つ多い。開けずに通り過ぎたときは抽選しない。
// 試練は条件を満たして勝てば最大の報酬、満たさなければ魔物の報酬。歩数の扉は今日の歩数の段で決まる。
// 進化の祭壇は、固有スキルを進化させたら何も落とさず、進められる固有スキルがなければ宝箱と同じにする。
export function rewardOf(
  kind: EventKind,
  outcome: RoomOutcome = {},
): { row: RewardRow; extra: number } | null {
  switch (kind) {
    case "rest":
    case "merchant":
    // 「？」のマスは入ったときに中身の種類へ変わるため、この種類のまま報酬を出すことはない。
    case "mystery":
      return null;
    case "shady-chest":
      return outcome.mimic
        ? { row: "boss", extra: 0 }
        : { row: "treasure", extra: 1 };
    case "trial":
      return { row: outcome.trialMet ? "boss" : "battle", extra: 0 };
    case "step-door":
      return STEP_DOOR_REWARDS[outcome.stepDoorTier ?? 0];
    case "altar":
      return outcome.evolved ? null : { row: "treasure", extra: 0 };
    default:
      return { row: kind, extra: 0 };
  }
}

// 冒険のドロップ（doc/features/drops.md）。出来事を終えたマスで、行き先の一覧から物を落とす。
// 一覧の物はその行き先でしか出ず、物の強さは行き先のLvに合わせて決める。
// 落ちる数・分類・物（☆）・品質の順に、マスの種類ごとの重みで選ぶ。重みはどれも仮の値である。

export const DROP_CATEGORIES = [
  "item",
  "accessory",
  "weapon",
  "armor",
  "skill",
] as const;

export type DropCategory = (typeof DROP_CATEGORIES)[number];

// 一覧の1つの物。stars はレア度（☆）で、クライアントの定義と同じ値にする。
// bossOnly の物は、最奥の間でだけ落ちる。
export type DropEntry =
  | { category: "item"; id: ItemId; stars: Stars; bossOnly?: true }
  | {
      category: "accessory" | "weapon" | "armor";
      id: EquipmentId;
      stars: Stars;
      bossOnly?: true;
    }
  | { category: "skill"; id: CardSkillId; stars: Stars; bossOnly?: true };

// 1回の抽選で落ちた物。アイテムと強化素材（material）は倍率を持たない（null）。
// 強化素材は分類の重みで選ばず、ふつうのドロップとは別に抽選する（rollMaterials）。
export type Drop = {
  category: DropCategory | "material";
  id: string;
  multiplierPercent: number | null;
};

// マスごとの、落ちる数の重み。
export const DROP_COUNT_WEIGHTS: Record<RewardRow, Record<number, number>> = {
  battle: { 1: 70, 2: 30 },
  elite: { 1: 25, 2: 50, 3: 25 },
  treasure: { 1: 25, 2: 50, 3: 25 },
  boss: { 2: 30, 3: 45, 4: 25 },
};

// 分類の重み。アイテム＞アクセサリー＞武器＝防具＞スキルの順に出やすい。
// マスで落ちうる物がない分類は除き、残りの重みで選ぶ。
export const CATEGORY_WEIGHTS: Record<DropCategory, number> = {
  item: 43,
  accessory: 22,
  weapon: 13,
  armor: 13,
  skill: 9,
};

// マスごとの、☆1〜☆5の物1つあたりの重み（千分率）。最奥の間は☆3〜5だけが落ちる。
export const STAR_WEIGHTS: Record<
  RewardRow,
  readonly [number, number, number, number, number]
> = {
  battle: [550, 300, 130, 17, 3],
  elite: [300, 380, 260, 50, 10],
  treasure: [300, 380, 260, 50, 10],
  boss: [0, 0, 650, 280, 70],
};

// マスごとの品質の重み（千分率）。行き先によっては変えない（2026-10-20）。
export const QUALITY_WEIGHTS: Record<RewardRow, Record<Quality, number>> = {
  battle: { C: 720, B: 220, A: 55, S: 5 },
  elite: { C: 400, B: 350, A: 210, S: 40 },
  treasure: { C: 400, B: 350, A: 210, S: 40 },
  boss: { C: 150, B: 350, A: 380, S: 120 },
};

// 一覧の1つの物を作る。分類ごとに、その分類の種類のIDだけを受け取る。
const entryOf =
  <C extends DropCategory>(category: C) =>
  (id: (DropEntry & { category: C })["id"], stars: Stars, bossOnly?: true) =>
    ({ category, id, stars, ...(bossOnly ? { bossOnly } : {}) }) as DropEntry;
const item = entryOf("item");
const accessory = entryOf("accessory");
const weapon = entryOf("weapon");
const armor = entryOf("armor");
const skill = entryOf("skill");

// 行き先ごとの一覧（仮）。スキル50枚を強さで8つの行き先へ分けた。アイテム・アクセサリー・武器・防具は
// 行き先ごとに作り、奥の行き先ほど基本の効果量を大きくした。主の物（bossOnly）は各行き先のアイテムと
// アクセサリーに1つずつ置く。
export const DROP_POOLS: Record<string, readonly DropEntry[]> = {
  "forest-ruins": [
    item("herb", 1),
    item("acorn", 1),
    item("moss-wad", 2),
    item("dew-vial", 2),
    item("spore-powder", 3),
    item("guardian-seed", 5, true),
    accessory("moss-charm", 1),
    accessory("acorn-brooch", 2),
    accessory("dew-pendant", 3),
    accessory("guardian-circlet", 5, true),
    weapon("wooden-sword", 1),
    weapon("short-bow", 1),
    weapon("oak-staff", 1),
    armor("leather-armor", 1),
    armor("wooden-shield", 1),
    armor("iron-helm", 2),
    skill("Slash", 1),
    skill("Fire", 1),
    skill("Heal", 1),
    skill("VitalThrust", 1),
    skill("ShieldBash", 2),
    skill("Embers", 2),
    skill("Hone", 4),
  ],
  "port-portoria": [
    item("dried-fish", 1),
    item("shell-shard", 1),
    item("sailor-rope", 2),
    item("sea-salt", 2),
    item("ink-pot", 3),
    item("pirate-rum", 4, true),
    accessory("shell-necklace", 1),
    accessory("anchor-bangle", 2),
    accessory("gold-earring", 3),
    accessory("kraken-pearl", 5, true),
    weapon("iron-sword", 2),
    weapon("war-hammer", 2),
    weapon("flame-dagger", 4),
    armor("chainmail", 2),
    armor("magic-robe", 2),
    armor("traveler-cloak", 3),
    skill("Ice", 1),
    skill("Guard", 1),
    skill("HolyHammer", 1),
    skill("Thunder", 2),
    skill("PoisonNeedle", 2),
    skill("Scout", 3),
    skill("ManaPrayer", 4),
  ],
  "glaciera-cave": [
    item("icicle-shard", 1),
    item("cavern-water", 1),
    item("glowworm-lamp", 2),
    item("frost-crystal", 3),
    item("troll-stoneskin", 4, true),
    accessory("penguin-feather", 1),
    accessory("crystal-ring", 3),
    accessory("troll-heartstone", 5, true),
    weapon("thunder-spear", 3),
    weapon("frost-wand", 3),
    armor("miner-helm", 2),
    armor("crystal-shield", 4),
    skill("Icicles", 1),
    skill("LightningBolt", 1),
    skill("Purify", 1),
    skill("ArmorBreak", 2),
    skill("Protect", 2),
    skill("ShadowStitch", 3),
    skill("IceMirror", 4),
  ],
  "hush-mire": [
    item("mud-ball", 1),
    item("lotus-root", 1),
    item("wisp-wick", 2),
    item("bog-venom", 3),
    item("croc-tear", 4, true),
    accessory("leech-bell", 2),
    accessory("frog-crown", 3),
    accessory("croc-fang", 5, true),
    weapon("reed-bow", 2),
    weapon("wisp-staff", 3),
    armor("mudcloth-robe", 2),
    armor("clay-armor", 3),
    skill("Regen", 1),
    skill("InsightArrow", 2),
    skill("FlamePillar", 2),
    skill("GuardianOath", 3),
    skill("Whirlwind", 3),
    skill("QuickCast", 5),
  ],
  "almaja-tomb": [
    item("cactus-fruit", 1),
    item("scorpion-stinger", 1),
    item("hourglass-sand", 2),
    item("grimoire-page", 3),
    item("brass-spring", 5, true),
    accessory("scarab-amulet", 1),
    accessory("gargoyle-eye", 3),
    accessory("golden-mask", 5, true),
    weapon("scorpion-bow", 2),
    weapon("tomb-khopesh", 3),
    armor("burial-wraps", 2),
    armor("ancient-plate", 4),
    skill("ArrowRain", 1),
    skill("EarthSplitter", 2),
    skill("BlastSigil", 2),
    skill("HolyLight", 3),
    skill("StrideStrike", 3),
    skill("Revelation", 5),
  ],
  "moonshade-graveyard": [
    item("grave-candle", 1),
    item("moonflower-honey", 1),
    item("raven-feather", 2),
    item("holy-water", 3),
    item("soul-phial", 5, true),
    accessory("bone-ring", 2),
    accessory("moonstone-pendant", 3),
    accessory("lich-signet", 5, true),
    weapon("bone-axe", 2),
    weapon("moonshade-staff", 4),
    armor("hollow-helm", 2),
    armor("grave-cloak", 3),
    skill("ChainLightning", 1),
    skill("FireStorm", 2),
    skill("Blizzard", 2),
    skill("Iai", 3),
    skill("Resurrection", 4),
    skill("DivineShield", 4),
  ],
  "volgan-volcano": [
    item("lava-rock", 1),
    item("lizard-tail", 1),
    item("sulfur-powder", 2),
    item("flametail-plume", 3),
    item("obsidian-core", 5, true),
    accessory("ember-charm", 1),
    accessory("magma-ring", 3),
    accessory("obsidian-crown", 5, true),
    weapon("firebird-bow", 2),
    weapon("magma-hammer", 3),
    armor("lizardscale-mail", 2),
    armor("obsidian-shield", 4),
    skill("FlameEnchant", 1),
    skill("Thundercloud", 2),
    skill("WarCry", 2),
    skill("StepPrayer", 3),
    skill("ShadowSnipe", 4),
    skill("Meteor", 5),
  ],
  "neve-peaks": [
    item("hard-snowball", 1),
    item("snowdrop", 1),
    item("boar-jerky", 2),
    item("crystal-horn", 3),
    item("mistdragon-scale", 5, true),
    accessory("snowflake-brooch", 2),
    accessory("foxtail-charm", 3),
    accessory("mistdragon-eye", 5, true),
    weapon("hailstone-staff", 3),
    weapon("glacier-greatsword", 4),
    armor("snow-cloak", 2),
    armor("mammoth-fur", 3),
    skill("Gale", 2),
    skill("BladeDance", 3),
    skill("ThunderSpear", 3),
    skill("GiantImpact", 4),
    skill("AbsoluteZero", 5),
  ],
};

export const starWeight = (kind: RewardRow, entry: DropEntry) =>
  STAR_WEIGHTS[kind][entry.stars - 1];

// そのマスで落ちうる物。重みが0の☆の物と、最奥の間のほかでは最奥の間だけの物を除く。
// 最奥の間の行を借りるマス（ミミック・試練・歩数の扉）は bossItems を false にして、主の物を除く。
export function eligibleEntries(
  destinationId: string,
  kind: RewardRow,
  bossItems = kind === "boss",
): DropEntry[] {
  return (DROP_POOLS[destinationId] ?? []).filter(
    (entry) => (bossItems || !entry.bossOnly) && starWeight(kind, entry) > 0,
  );
}

// 磨き石の色の重みを分ける、行き先の深さ。近い順の8つの行き先のうち、初めの2つ（敵のLv10・14）を浅い、
// 次の3つ（Lv18〜26）を中ほど、残り（Lv30〜40）を奥とする。一覧にない行き先は浅いとする。
export function polishDepthOf(destinationId: string): PolishDepth {
  const index = DESTINATIONS.findIndex(
    (destination) => destination.id === destinationId,
  );
  return index < 2 ? "shallow" : index < 5 ? "middle" : "deep";
}

// 強化素材（doc/features/enhancement.md の磨き石の落ち方）。ふつうのドロップのあとに、磨き石と覚醒の欠片を
// それぞれ1回だけ抽選する。ふつうのドロップの分類に混ぜると、ほかの物が出にくくなるためである。
// 磨き石の色は、行き先の深さとマスで決まる重みで選ぶ。
export function rollMaterials(
  destinationId: string,
  kind: RewardRow,
  random: Random,
): Drop[] {
  const drops: Drop[] = [];
  if (random() * 1000 < POLISH_DROP_CHANCE[kind]) {
    const weights = POLISH_DROP_WEIGHTS[polishDepthOf(destinationId)][kind];
    drops.push({
      category: "material",
      id: pickWeighted(POLISH_IDS, (id) => weights[id], random),
      multiplierPercent: null,
    });
  }
  if (random() * 1000 < SHARD_DROP_CHANCE[kind]) {
    drops.push({
      category: "material",
      id: AWAKENING_SHARD_ID,
      multiplierPercent: null,
    });
  }
  return drops;
}

// マスで落ちる物を選ぶ。同じ物が2つ落ちることもある。強化素材は含めない（rollRoomDrops）。
export function rollDrops(
  destinationId: string,
  roomKind: EventKind,
  random: Random,
  outcome: RoomOutcome = {},
): Drop[] {
  const reward = rewardOf(roomKind, outcome);
  if (reward === null) return [];
  const kind = reward.row;
  const pool = eligibleEntries(destinationId, kind, roomKind === "boss");
  if (pool.length === 0) return [];
  const categories = DROP_CATEGORIES.filter((category) =>
    pool.some((entry) => entry.category === category),
  );
  const counts = Object.entries(DROP_COUNT_WEIGHTS[kind]).map(
    ([count, weight]) => ({ count: Number(count), weight }),
  );
  const count =
    pickWeighted(counts, (entry) => entry.weight, random).count + reward.extra;
  const drops: Drop[] = [];
  for (let index = 0; index < count; index++) {
    const category = pickWeighted(
      categories,
      (choice) => CATEGORY_WEIGHTS[choice],
      random,
    );
    const entry = pickWeighted(
      pool.filter((choice) => choice.category === category),
      (choice) => starWeight(kind, choice),
      random,
    );
    const maxPercent = maxPercentOf(category, entry.id);
    drops.push({
      category,
      id: entry.id,
      multiplierPercent:
        maxPercent === null
          ? null
          : rollMultiplierPercent(
              pickQuality(QUALITY_WEIGHTS[kind], random),
              random,
              maxPercent,
            ),
    });
  }
  return drops;
}

// マスで落ちる物と強化素材。ふつうのドロップのあとに強化素材を足す。報酬のないマスでは何も落とさない。
export function rollRoomDrops(
  destinationId: string,
  roomKind: EventKind,
  random: Random,
  outcome: RoomOutcome = {},
): Drop[] {
  const reward = rewardOf(roomKind, outcome);
  if (reward === null) return [];
  return [
    ...rollDrops(destinationId, roomKind, random, outcome),
    ...rollMaterials(destinationId, reward.row, random),
  ];
}
