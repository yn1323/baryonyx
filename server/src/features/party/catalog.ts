// キャラとスキルのID、レベルアップの決まり、初めて読むユーザーに付与する仮の状態。
// 名前・絵・ステータス・スキル・カードの属性はクライアントの仮データが持ち、
// サーバーは持っているキャラ・編成・カード・レベルの整合だけを判定する（doc/features/party.md）。

// クライアントの仮データ（PartyMockData）の順。一覧はこの順に返す。
export const CHARACTER_IDS = [
  "toma",
  "luka",
  "aria",
  "mina",
  "anselm",
  "greta",
  "lutz",
  "rita",
  "ritsu",
] as const;

export type CharacterId = (typeof CHARACTER_IDS)[number];

// クライアントのスキル（CardSkills）のID。属性の決まりはクライアントだけが判定する。
export const CARD_SKILL_IDS = [
  "Slash",
  "Fire",
  "Ice",
  "Thunder",
  "Heal",
  "Guard",
  "Whirlwind",
  "Iai",
  "BladeDance",
  "Hone",
  "ShieldBash",
  "ArmorBreak",
  "EarthSplitter",
  "GiantImpact",
  "StrideStrike",
  "GuardianOath",
  "WarCry",
  "Embers",
  "FlamePillar",
  "FireStorm",
  "BlastSigil",
  "Meteor",
  "FlameEnchant",
  "Icicles",
  "Blizzard",
  "IceMirror",
  "AbsoluteZero",
  "QuickCast",
  "VitalThrust",
  "PoisonNeedle",
  "ShadowStitch",
  "InsightArrow",
  "ArrowRain",
  "ShadowSnipe",
  "LightningBolt",
  "ChainLightning",
  "Thundercloud",
  "ThunderSpear",
  "Gale",
  "Scout",
  "StepPrayer",
  "Regen",
  "Resurrection",
  "HolyHammer",
  "Protect",
  "Purify",
  "ManaPrayer",
  "Revelation",
  "HolyLight",
  "DivineShield",
] as const;

export type CardSkillId = (typeof CARD_SKILL_IDS)[number];

// パーティの枠と、キャラごとのスキルの枠の数。
export const PARTY_SLOT_COUNT = 4;
export const CARD_SLOT_COUNT = 4;

// レベルの上限と、Lv n から n+1 へ上げるのに要るルーン（n × COST_PER_LEVEL）。仮の値。
export const MAX_LEVEL = 30;
export const COST_PER_LEVEL = 100;

export function levelUpCost(fromLevel: number, toLevel: number): number {
  let total = 0;
  for (let level = fromLevel; level < toLevel; level++) {
    total += level * COST_PER_LEVEL;
  }
  return total;
}

// 最初から持っているキャラ・レベル・カードと編成。キャラの加入ができるまでの仮設定。
export const STARTER_CHARACTERS: readonly {
  characterId: CharacterId;
  level: number;
  cards: readonly CardSkillId[];
}[] = [
  {
    characterId: "toma",
    level: 12,
    cards: ["Fire", "Meteor", "Ice", "Blizzard"],
  },
  {
    characterId: "luka",
    level: 11,
    cards: ["VitalThrust", "ArrowRain", "Thunder", "LightningBolt"],
  },
  {
    characterId: "aria",
    level: 10,
    cards: ["Slash", "Iai", "ShieldBash", "EarthSplitter"],
  },
  {
    characterId: "mina",
    level: 10,
    cards: ["Heal", "Protect", "HolyHammer", "HolyLight"],
  },
  {
    characterId: "anselm",
    level: 8,
    cards: ["EarthSplitter", "HolyHammer", "Fire", "Embers"],
  },
  {
    characterId: "greta",
    level: 7,
    cards: ["VitalThrust", "PoisonNeedle", "Ice", "Icicles"],
  },
  {
    characterId: "lutz",
    level: 5,
    cards: ["Thunder", "Thundercloud", "ShieldBash", "EarthSplitter"],
  },
  {
    characterId: "rita",
    level: 3,
    cards: ["Slash", "Whirlwind", "VitalThrust", "ShadowStitch"],
  },
  {
    characterId: "ritsu",
    level: 1,
    cards: ["Ice", "Blizzard", "Thunder", "Thundercloud"],
  },
];

export const STARTER_SLOTS: readonly CharacterId[] = [
  "toma",
  "luka",
  "aria",
  "mina",
];
