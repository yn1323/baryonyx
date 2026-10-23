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

// スキルの種類ごとの倍率の上限（百分率。doc/features/inventory.md の上限の決め方）。
// クライアントの定義（CardSkills の MaxMultiplier）と同じ値にする。
const CARD_SKILL_MAX_PERCENT: Record<CardSkillId, number> = {
  Slash: 240,
  Fire: 240,
  Ice: 220,
  Thunder: 220,
  Heal: 250,
  Guard: 250,
  Whirlwind: 200,
  Iai: 280,
  BladeDance: 160,
  Hone: 200,
  ShieldBash: 220,
  ArmorBreak: 220,
  EarthSplitter: 240,
  GiantImpact: 280,
  StrideStrike: 240,
  GuardianOath: 200,
  WarCry: 200,
  Embers: 220,
  FlamePillar: 240,
  FireStorm: 220,
  BlastSigil: 240,
  Meteor: 300,
  FlameEnchant: 200,
  Icicles: 170,
  Blizzard: 220,
  IceMirror: 160,
  AbsoluteZero: 240,
  QuickCast: 200,
  VitalThrust: 220,
  PoisonNeedle: 220,
  ShadowStitch: 200,
  InsightArrow: 220,
  ArrowRain: 180,
  ShadowSnipe: 260,
  LightningBolt: 220,
  ChainLightning: 180,
  Thundercloud: 200,
  ThunderSpear: 260,
  Gale: 200,
  Scout: 200,
  StepPrayer: 220,
  Regen: 220,
  Resurrection: 200,
  HolyHammer: 220,
  Protect: 200,
  Purify: 220,
  ManaPrayer: 150,
  Revelation: 200,
  HolyLight: 220,
  DivineShield: 160,
};

// スキルの種類ごとの倍率の上限（百分率）。冒険のドロップで倍率を選ぶときと、品質を求めるときに使う。
export function maxMultiplierPercent(id: CardSkillId): number {
  return CARD_SKILL_MAX_PERCENT[id] ?? 100;
}

// パーティの枠と、キャラごとのカスタムスキルの枠の数。固有スキル3枚の名前と効果はクライアントの仮データが持つ。
export const PARTY_SLOT_COUNT = 4;
export const CARD_SLOT_COUNT = 3;

// キャラごとの固有スキルの数と、それぞれを解放するLv（doc/features/skill-evolution.md）。
// 解放のLvはクライアントの仮データ（PartyMockData）と同じ値にし、判定はサーバーの値で行う。
export const UNIQUE_SKILL_COUNT = 3;
export const UNIQUE_UNLOCK_LEVELS: readonly number[] = [5, 20, 40];

// 固有スキルの進化の段階。どの固有スキルも同じ形のツリーで、0が最初の段階、
// 1・2が枝1、3・4が枝2、5・6が枝3の1段目と2段目になる。
export const UNIQUE_STAGE_COUNT = 7;

// 固有スキル3つの倍率の上限（百分率）を、キャラの職ごとに並べたもの（doc/features/inventory.md の上限の決め方）。
// クライアントの UniqueSkillTrees の limit と同じ値。覚醒しても同じ上限を使う。
const UNIQUE_MAX_PERCENT_BY_CLASS = {
  // マナバースト・星降り・魔眼
  mage: [220, 170, 200],
  // 雷撃の矢・矢の雨・速射
  archer: [220, 170, 180],
  // 盾の構え・一閃・鼓舞
  knight: [220, 260, 200],
  // 癒しの風・蘇生・裁きの光
  healer: [220, 200, 200],
} as const;

const CHARACTER_CLASSES: Record<
  CharacterId,
  keyof typeof UNIQUE_MAX_PERCENT_BY_CLASS
> = {
  toma: "mage",
  luka: "archer",
  aria: "knight",
  mina: "healer",
  anselm: "knight",
  greta: "archer",
  lutz: "mage",
  rita: "healer",
  ritsu: "archer",
};

// キャラの index 番目の固有スキルの倍率の上限（百分率）。品質強化と、品質を求めるときに使う。
export function uniqueMaxPercent(characterId: CharacterId, index: number) {
  return UNIQUE_MAX_PERCENT_BY_CLASS[CHARACTER_CLASSES[characterId]][index];
}

// 冒険の戦闘での進化は、マスの戦闘を終えたときに送られる。最奥のボスや全滅でそのマスと一緒に冒険も
// 終わるため、踏破・敗北で冒険を終えてからこの分のあいだに届いた進化も受け付ける。
export const EVOLUTION_GRACE_MINUTES = 10;

// 段階の1つ手前の段階。最初の段階（0）にはないためnullを返す。
// 各枝の1段目（1・3・5）は最初の段階から、2段目（2・4・6）はその枝の1段目から進む。
export function uniqueParent(stage: number): number | null {
  if (stage <= 0) return null;
  return stage % 2 === 1 ? 0 : stage - 1;
}

// レベルの上限（2026-10-23、ユーザー指示）と、Lv n から n+1 へ上げるのに要るルーン
// （n × COST_PER_LEVEL。仮の値）。
export const MAX_LEVEL = 100;
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
    cards: ["Fire", "Ice"],
  },
  {
    characterId: "luka",
    level: 11,
    cards: ["VitalThrust", "Thunder"],
  },
  {
    characterId: "aria",
    level: 10,
    cards: ["Slash", "ShieldBash"],
  },
  {
    characterId: "mina",
    level: 10,
    cards: ["Heal", "HolyHammer"],
  },
  {
    characterId: "anselm",
    level: 8,
    cards: ["EarthSplitter", "Fire"],
  },
  {
    characterId: "greta",
    level: 7,
    cards: ["VitalThrust", "Ice"],
  },
  {
    characterId: "lutz",
    level: 5,
    cards: ["Thunder", "ShieldBash"],
  },
  {
    characterId: "rita",
    level: 3,
    cards: ["Slash", "VitalThrust"],
  },
  {
    characterId: "ritsu",
    level: 1,
    cards: ["Ice", "Thunder"],
  },
];

export const STARTER_SLOTS: readonly CharacterId[] = [
  "toma",
  "luka",
  "aria",
  "mina",
];
