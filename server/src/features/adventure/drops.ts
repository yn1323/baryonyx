import {
  pickQuality,
  type Quality,
  rollMultiplierPercent,
} from "../../shared/quality.js";
import {
  type EquipmentId,
  maxMultiplierPercent as equipmentMaxPercent,
} from "../equipment/catalog.js";
import type { ItemId } from "../items/catalog.js";
import {
  type CardSkillId,
  maxMultiplierPercent as skillMaxPercent,
} from "../party/catalog.js";
import {
  maxMultiplierPercent as bonusMaxPercent,
  type StepBonusId,
} from "../step-bonus/catalog.js";
import type { EventKind } from "./catalog.js";

// 冒険のドロップ（doc/features/drops.md）。出来事を終えた部屋で、行き先の一覧から物を落とす。
// 一覧の物はその行き先でしか出ず、物の強さは行き先のLvに合わせて決める。ACTボーナスだけは、どの行き先でも出る。
// 落ちる数・分類・物（☆）・品質の順に、部屋の種類ごとの重みで選ぶ。重みはどれも仮の値である。

export const DROP_CATEGORIES = [
  "item",
  "accessory",
  "weapon",
  "armor",
  "skill",
  "bonus",
] as const;

export type DropCategory = (typeof DROP_CATEGORIES)[number];

export type Stars = 1 | 2 | 3 | 4 | 5;

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
  | { category: "skill"; id: CardSkillId; stars: Stars; bossOnly?: true }
  | { category: "bonus"; id: StepBonusId; stars: Stars; bossOnly?: true };

// 1回の抽選で落ちた物。アイテムは倍率を持たない（null）。
export type Drop = {
  category: DropCategory;
  id: string;
  multiplierPercent: number | null;
};

// 部屋ごとの、落ちる数の重み。
export const DROP_COUNT_WEIGHTS: Record<EventKind, Record<number, number>> = {
  battle: { 1: 70, 2: 30 },
  elite: { 1: 25, 2: 50, 3: 25 },
  treasure: { 1: 25, 2: 50, 3: 25 },
  boss: { 2: 30, 3: 45, 4: 25 },
};

// 分類の重み。アイテム＞アクセサリー＞武器＝防具＞スキル＞ACTボーナスの順に出やすい。
// 部屋で落ちうる物がない分類は除き、残りの重みで選ぶ。
export const CATEGORY_WEIGHTS: Record<DropCategory, number> = {
  item: 40,
  accessory: 22,
  weapon: 13,
  armor: 13,
  skill: 9,
  bonus: 3,
};

// 部屋ごとの、☆1〜☆5の物1つあたりの重み（千分率）。最奥の間は☆3〜5だけが落ちる。
export const STAR_WEIGHTS: Record<
  EventKind,
  readonly [number, number, number, number, number]
> = {
  battle: [550, 300, 130, 17, 3],
  elite: [300, 380, 260, 50, 10],
  treasure: [300, 380, 260, 50, 10],
  boss: [0, 0, 650, 280, 70],
};

// 部屋ごとの品質の重み（千分率）。行き先によっては変えない（2026-10-20）。
export const QUALITY_WEIGHTS: Record<EventKind, Record<Quality, number>> = {
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
const bonus = entryOf("bonus");

// 行き先ごとの一覧（仮）。スキル50枚を強さで8つの行き先へ分け、武器・防具は今ある14種を浅い行き先へ置いた。
// アイテムとアクセサリーは、まずミストラ遺跡と港町ポルトリアの分だけを作った。
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
    weapon("thunder-spear", 3),
    weapon("frost-wand", 3),
    skill("Icicles", 1),
    skill("LightningBolt", 1),
    skill("Purify", 1),
    skill("ArmorBreak", 2),
    skill("Protect", 2),
    skill("ShadowStitch", 3),
    skill("IceMirror", 4),
  ],
  "hush-mire": [
    skill("Regen", 1),
    skill("InsightArrow", 2),
    skill("FlamePillar", 2),
    skill("GuardianOath", 3),
    skill("Whirlwind", 3),
    skill("QuickCast", 5),
  ],
  "almaja-tomb": [
    skill("ArrowRain", 1),
    skill("EarthSplitter", 2),
    skill("BlastSigil", 2),
    skill("HolyLight", 3),
    skill("StrideStrike", 3),
    skill("Revelation", 5),
  ],
  "moonshade-graveyard": [
    skill("ChainLightning", 1),
    skill("FireStorm", 2),
    skill("Blizzard", 2),
    skill("Iai", 3),
    skill("Resurrection", 4),
    skill("DivineShield", 4),
  ],
  "volgan-volcano": [
    skill("FlameEnchant", 1),
    skill("Thundercloud", 2),
    skill("WarCry", 2),
    skill("StepPrayer", 3),
    skill("ShadowSnipe", 4),
    skill("Meteor", 5),
  ],
  "neve-peaks": [
    skill("Gale", 2),
    skill("BladeDance", 3),
    skill("ThunderSpear", 3),
    skill("GiantImpact", 4),
    skill("AbsoluteZero", 5),
  ],
};

// どの行き先でも出るACTボーナス。
export const COMMON_POOL: readonly DropEntry[] = [
  bonus("guard", 1),
  bonus("fighting", 1),
  bonus("foresight", 2),
  bonus("omen", 2),
  bonus("treasure-sight", 3),
  bonus("luck", 3),
  bonus("appraisal", 4),
];

// 物の倍率の上限（百分率）。アイテムは倍率を持たない。抽選と、応答の品質を求めるのに使う。
export function maxPercentOf(
  category: DropCategory,
  id: string,
): number | null {
  switch (category) {
    case "item":
      return null;
    case "skill":
      return skillMaxPercent(id as CardSkillId);
    case "bonus":
      return bonusMaxPercent(id as StepBonusId);
    default:
      return equipmentMaxPercent(id as EquipmentId);
  }
}

function pickWeighted<T>(
  choices: readonly T[],
  weightOf: (choice: T) => number,
  random: () => number,
): T {
  const total = choices.reduce((sum, choice) => sum + weightOf(choice), 0);
  let roll = random() * total;
  for (const choice of choices) {
    const weight = weightOf(choice);
    if (roll < weight) return choice;
    roll -= weight;
  }
  return choices[choices.length - 1];
}

const starWeight = (kind: EventKind, entry: DropEntry) =>
  STAR_WEIGHTS[kind][entry.stars - 1];

// その部屋で落ちうる物。最奥の間だけの物と、重みが0の☆の物を除く。
export function eligibleEntries(
  destinationId: string,
  kind: EventKind,
): DropEntry[] {
  return [...(DROP_POOLS[destinationId] ?? []), ...COMMON_POOL].filter(
    (entry) =>
      (kind === "boss" || !entry.bossOnly) && starWeight(kind, entry) > 0,
  );
}

// 部屋で落ちる物を選ぶ。同じ物が2つ落ちることもある。
export function rollDrops(
  destinationId: string,
  kind: EventKind,
  random: () => number,
): Drop[] {
  const pool = eligibleEntries(destinationId, kind);
  if (pool.length === 0) return [];
  const categories = DROP_CATEGORIES.filter((category) =>
    pool.some((entry) => entry.category === category),
  );
  const counts = Object.entries(DROP_COUNT_WEIGHTS[kind]).map(
    ([count, weight]) => ({ count: Number(count), weight }),
  );
  const { count } = pickWeighted(counts, (entry) => entry.weight, random);
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
