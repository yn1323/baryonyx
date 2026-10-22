// ローカルD1へ入れる開発用のプレイヤー（seed）。`pnpm db:reset` が全テーブルを作り直したあとに投入する。
// 決まり（ID・枠の数・費用）は各機能の catalog.ts、手順と使い方は doc/rules/backend-design.md の「開発用データ（seed）」。
// 値は実在のプレイヤーを模した仮の値で、APIで読み書きできることを tests/integration/seed.test.ts が確かめる。

import { createHash } from "node:crypto";

type SeedCharacter = {
  id: string;
  level: number;
  // カスタムスキルの2枠に入れるスキル。属性は初期データ（party/catalog.ts の STARTER_CHARACTERS）と同じものにそろえる。
  // スキルは1枚ずつ（×1.00）持ち物に入れ、枠はその1枚を指す。
  cards: readonly [string, string];
  weapon?: string;
  armor?: string;
  // 2つの固有スキルの、セットしている段階と取得した段階（0〜6、party/catalog.ts の uniqueParent のツリー）。
  // 解放するLv（UNIQUE_UNLOCK_LEVELS：1つ目Lv5・2つ目Lv20）に届いた固有スキルだけに書く。省くと最初の段階だけ。
  uniques?: readonly ({ set: number; acquired: readonly number[] } | null)[];
};

export type SeedPlayer = {
  id: string;
  // Unityのメニュー `Baryonyx > Server > Seed Player` に出す名前。
  label: string;
  runes: number;
  characters: readonly SeedCharacter[];
  // パーティの4枠。空き枠はnull。
  party: readonly (string | null)[];
  // 持っている装備（同じ種類を複数持てる）。付けている装備は characters の weapon・armor で指す。
  items: readonly string[];
  // items と同じ順の、装備の倍率（百分率の整数。doc/features/inventory.md の品質）。省くと、どれも×1.00。
  itemMultipliers?: readonly number[];
  // 枠に入れていない、冒険で手に入れたスキルと倍率（百分率の整数）。
  skillCards?: readonly { id: string; multiplierPercent: number }[];
  // 持っているアイテムと数。
  itemCounts?: readonly { id: string; count: number }[];
  // 持っているボーナスと倍率（百分率の整数）。
  bonuses: readonly { id: string; multiplierPercent: number }[];
  // ボーナスの5枠。空き枠はnull。
  bonusSlots: readonly (string | null)[];
  records: readonly {
    destinationId: string;
    bestFloor: number;
    clears: number;
  }[];
  // 進行中の冒険。入口の部屋に着いたところから始める（道の部屋はクライアントが種から作るため）。
  adventure?: { destinationId: string; seed: number };
  // 戦闘で開示した敵の弱点。
  weaknesses: readonly { enemyId: string; element: string }[];
  // 戦闘で遭遇した敵と倒した数（ライブラリ）。キャラ・カード・装備の見つけた記録は、持ち物から作る。
  enemies: readonly { enemyId: string; defeated: number }[];
};

// 防具の種類。Nodeで直接読むこのファイルは src のTypeScriptを読み込めないため、equipment/catalog.ts の
// EQUIPMENT から防具だけを写す。食い違えば tests/integration/seed.test.ts のライブラリの確認が失敗する。
const ARMOR_IDS: readonly string[] = [
  "traveler-cloak",
  "chainmail",
  "magic-robe",
  "iron-helm",
  "leather-armor",
  "wooden-shield",
];

// アクセサリーの種類。防具と同じく equipment/catalog.ts の EQUIPMENT から写す。
const ACCESSORY_IDS: readonly string[] = [
  "moss-charm",
  "acorn-brooch",
  "dew-pendant",
  "guardian-circlet",
  "shell-necklace",
  "anchor-bangle",
  "gold-earring",
  "kraken-pearl",
];

const kindOf = (equipmentId: string) =>
  ACCESSORY_IDS.includes(equipmentId)
    ? "accessory"
    : ARMOR_IDS.includes(equipmentId)
      ? "armor"
      : "weapon";

export const SEED_PLAYERS: readonly SeedPlayer[] = [
  {
    id: "veteran",
    label: "遊び込んだプレイヤー",
    runes: 50_000,
    characters: [
      {
        id: "toma",
        level: 30,
        cards: ["FireStorm", "AbsoluteZero"],
        weapon: "frost-wand",
        armor: "magic-robe",
        // 1つ目は7つの段階をすべて取得済み、2つ目は枝1を取りきって枝2の1段目にいる。
        uniques: [
          { set: 6, acquired: [0, 1, 2, 3, 4, 5, 6] },
          { set: 3, acquired: [0, 1, 2, 3] },
        ],
      },
      {
        id: "luka",
        level: 28,
        cards: ["ShadowSnipe", "ChainLightning"],
        weapon: "thunder-spear",
        armor: "traveler-cloak",
        // 1つ目は枝1の2段目、2つ目は枝3の1段目まで進んだ。
        uniques: [
          { set: 2, acquired: [0, 1, 2] },
          { set: 5, acquired: [0, 5] },
        ],
      },
      {
        id: "aria",
        level: 27,
        cards: ["BladeDance", "GiantImpact"],
        weapon: "flame-dagger",
        armor: "chainmail",
        // 枝2と枝3の1段目を取得し、手前の最初の段階にセットし直している。
        uniques: [{ set: 0, acquired: [0, 3, 4, 5] }],
      },
      {
        id: "mina",
        level: 26,
        cards: ["HolyLight", "Resurrection"],
        weapon: "war-hammer",
        armor: "iron-helm",
      },
      {
        id: "anselm",
        level: 22,
        cards: ["StrideStrike", "FlamePillar"],
        weapon: "iron-sword",
        armor: "chainmail",
      },
      {
        id: "greta",
        level: 20,
        cards: ["ShadowStitch", "IceMirror"],
        weapon: "short-bow",
      },
      {
        id: "lutz",
        level: 18,
        cards: ["ThunderSpear", "ShieldBash"],
      },
      {
        id: "rita",
        level: 15,
        cards: ["Whirlwind", "ShadowSnipe"],
      },
      {
        id: "ritsu",
        level: 12,
        cards: ["Icicles", "LightningBolt"],
      },
    ],
    party: ["aria", "toma", "mina", "luka"],
    items: [
      "flame-dagger",
      "thunder-spear",
      "frost-wand",
      "war-hammer",
      "iron-sword",
      "iron-sword",
      "short-bow",
      "oak-staff",
      "wooden-sword",
      "traveler-cloak",
      "chainmail",
      "chainmail",
      "magic-robe",
      "iron-helm",
      "leather-armor",
      "wooden-shield",
      "dew-pendant",
      "guardian-circlet",
      "kraken-pearl",
    ],
    // 品質を見比べられるよう、S・A・B・C がそろう倍率にする（装備ごとの上限の中の位置で決めた）。
    itemMultipliers: [
      180, 229, 191, 212, 152, 140, 124, 100, 172, 196, 187, 150, 110, 100, 145,
      118, 130, 205, 250,
    ],
    skillCards: [
      { id: "Hone", multiplierPercent: 196 },
      { id: "ManaPrayer", multiplierPercent: 128 },
      { id: "Slash", multiplierPercent: 172 },
      { id: "Slash", multiplierPercent: 104 },
    ],
    itemCounts: [
      { id: "herb", count: 12 },
      { id: "spore-powder", count: 3 },
      { id: "guardian-seed", count: 1 },
      { id: "dried-fish", count: 5 },
      { id: "pirate-rum", count: 1 },
    ],
    bonuses: [
      { id: "guard", multiplierPercent: 147 },
      { id: "luck", multiplierPercent: 147 },
      { id: "treasure-sight", multiplierPercent: 138 },
      { id: "foresight", multiplierPercent: 138 },
      { id: "omen", multiplierPercent: 126 },
      { id: "appraisal", multiplierPercent: 138 },
      { id: "fighting", multiplierPercent: 147 },
    ],
    bonusSlots: ["fighting", "luck", "guard", "treasure-sight", "appraisal"],
    records: [{ destinationId: "forest-ruins", bestFloor: 10, clears: 3 }],
    weaknesses: [
      { enemyId: "moss-slime", element: "thunder" },
      { enemyId: "moss-slime", element: "fire" },
      { enemyId: "moss-wolf", element: "ice" },
      { enemyId: "moss-wolf", element: "fire" },
      { enemyId: "forest-guardian", element: "fire" },
      { enemyId: "forest-guardian", element: "slash" },
    ],
    enemies: [
      { enemyId: "moss-slime", defeated: 84 },
      { enemyId: "moss-wolf", defeated: 52 },
      { enemyId: "forest-guardian", defeated: 3 },
    ],
  },
  {
    id: "adventurer",
    label: "冒険の途中のプレイヤー",
    runes: 1_200,
    characters: [
      {
        id: "toma",
        level: 12,
        cards: ["Fire", "Ice"],
        weapon: "oak-staff",
        armor: "magic-robe",
        // 冒険の途中で、1つ目の固有スキルが枝1の1段目へ進化した。
        uniques: [{ set: 1, acquired: [0, 1] }],
      },
      {
        id: "luka",
        level: 11,
        cards: ["VitalThrust", "Thunder"],
        weapon: "short-bow",
        armor: "traveler-cloak",
      },
      {
        id: "aria",
        level: 10,
        cards: ["Slash", "ShieldBash"],
        weapon: "iron-sword",
        armor: "chainmail",
      },
      {
        id: "mina",
        level: 10,
        cards: ["Heal", "HolyHammer"],
        weapon: "war-hammer",
        armor: "leather-armor",
      },
    ],
    party: ["toma", "luka", "aria", "mina"],
    items: [
      "oak-staff",
      "short-bow",
      "iron-sword",
      "war-hammer",
      "wooden-sword",
      "magic-robe",
      "traveler-cloak",
      "chainmail",
      "leather-armor",
      "moss-charm",
    ],
    itemCounts: [
      { id: "herb", count: 2 },
      { id: "acorn", count: 1 },
    ],
    bonuses: [
      { id: "luck", multiplierPercent: 126 },
      { id: "foresight", multiplierPercent: 112 },
      { id: "fighting", multiplierPercent: 105 },
    ],
    bonusSlots: ["foresight", "luck", "fighting", null, null],
    records: [{ destinationId: "forest-ruins", bestFloor: 6, clears: 0 }],
    adventure: { destinationId: "forest-ruins", seed: 20_261_005 },
    weaknesses: [{ enemyId: "moss-slime", element: "thunder" }],
    enemies: [
      { enemyId: "moss-slime", defeated: 9 },
      { enemyId: "moss-wolf", defeated: 0 },
    ],
  },
  {
    id: "newcomer",
    label: "始めたばかりのプレイヤー",
    // Lv1から2へ上げられて、2から3へは足りない量。
    runes: 150,
    characters: [
      {
        id: "toma",
        level: 1,
        cards: ["Fire", "Ice"],
        weapon: "wooden-sword",
      },
    ],
    party: ["toma", null, null, null],
    items: ["wooden-sword", "leather-armor"],
    bonuses: [{ id: "luck", multiplierPercent: 100 }],
    bonusSlots: ["luck", null, null, null, null],
    records: [],
    weaknesses: [],
    enemies: [],
  },
];

// ゲストの秘密値は名前から決める。Unityの `SeedPlayerMenu` も同じ規則で作るため、規則を変えるときは両方を直す。
export function seedGuestSecret(playerId: string) {
  return createHash("sha256").update(`baryonyx-seed:${playerId}`).digest("hex");
}

// 同じ入力から同じUUIDの形の値を作る。投入し直しても、装備などのIDが変わらない。
function stableUuid(text: string) {
  const hex = createHash("sha256").update(text).digest("hex");
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20, 32)}`;
}

type Value = string | number | boolean | null;

function literal(value: Value) {
  if (value === null) return "NULL";
  if (typeof value === "boolean") return value ? "1" : "0";
  if (typeof value === "number") return String(value);
  return `'${value.replaceAll("'", "''")}'`;
}

function insert(table: string, rows: readonly Record<string, Value>[]) {
  if (rows.length === 0) return [];
  const columns = Object.keys(rows[0]);
  const values = rows.map(
    (row) => `(${columns.map((column) => literal(row[column])).join(", ")})`,
  );
  return [
    `INSERT INTO ${table} (${columns.join(", ")}) VALUES\n  ${values.join(",\n  ")}`,
  ];
}

function playerStatements(player: SeedPlayer, now: string) {
  const userId = stableUuid(`seed:${player.id}:user`);
  if (
    player.itemMultipliers &&
    player.itemMultipliers.length !== player.items.length
  ) {
    throw new Error(`${player.id}: itemMultipliers の数が items と違います`);
  }
  const grantId = stableUuid(`seed:${player.id}:grant`);
  const itemIds = player.items.map((_, index) =>
    stableUuid(`seed:${player.id}:item:${index}`),
  );
  // 枠に入れる初めのカード（持っているスキルの1枚）のID。
  const starterCardId = (characterId: string, slot: number) =>
    stableUuid(`seed:${player.id}:card:${characterId}:${slot}`);
  // 同じ種類を複数持つときは、まだ誰も付けていない物から順に付ける。
  const used = new Set<number>();
  const itemFor = (equipmentId: string) => {
    const index = player.items.findIndex(
      (id, i) => id === equipmentId && !used.has(i),
    );
    if (index < 0) {
      throw new Error(`${player.id}: ${equipmentId} を持っていません`);
    }
    used.add(index);
    return itemIds[index];
  };
  const discoveries = (kind: string, entryIds: readonly string[]) =>
    [...new Set(entryIds)].map((entryId) => ({
      user_id: userId,
      kind,
      entry_id: entryId,
      discovered_at: now,
      defeated: 0,
    }));
  const worn = player.characters.flatMap((character) =>
    (["weapon", "armor"] as const).flatMap((slot) => {
      const equipmentId = character[slot];
      return equipmentId
        ? [
            {
              user_id: userId,
              character_id: character.id,
              slot,
              item_id: itemFor(equipmentId),
              updated_at: now,
            },
          ]
        : [];
    }),
  );

  return [
    ...insert("app_users", [
      {
        id: userId,
        guest_secret_hash: createHash("sha256")
          .update(seedGuestSecret(player.id))
          .digest("hex"),
      },
    ]),
    ...insert("rune_wallets", [
      { user_id: userId, balance: player.runes, updated_at: now },
    ]),
    // 初期データの付与（ensureStarter）を済ませた扱いにして、seedの内容だけを残す。
    ...insert("party_profiles", [
      { user_id: userId, grant_id: grantId, created_at: now },
    ]),
    ...insert(
      "party_characters",
      player.characters.map((character) => ({
        user_id: userId,
        character_id: character.id,
        level: character.level,
        acquired_at: now,
        updated_at: now,
      })),
    ),
    ...insert(
      "skill_cards",
      player.characters.flatMap((character) =>
        character.cards.map((skillId, slot) => ({
          id: starterCardId(character.id, slot),
          user_id: userId,
          skill_id: skillId,
          multiplier_percent: 100,
          acquired_at: now,
        })),
      ),
    ),
    ...insert(
      "party_character_cards",
      player.characters.flatMap((character) =>
        character.cards.map((_, slot) => ({
          user_id: userId,
          character_id: character.id,
          slot,
          card_id: starterCardId(character.id, slot),
          updated_at: now,
        })),
      ),
    ),
    ...insert(
      "party_unique_skills",
      player.characters.flatMap((character) =>
        (character.uniques ?? []).flatMap((unique, index) => {
          if (!unique) return [];
          if (!unique.acquired.includes(0)) {
            throw new Error(
              `${player.id}: ${character.id} の固有スキル${index}に最初の段階がありません`,
            );
          }
          if (!unique.acquired.includes(unique.set)) {
            throw new Error(
              `${player.id}: ${character.id} の固有スキル${index}は取得していない段階をセットしています`,
            );
          }
          return [
            {
              user_id: userId,
              character_id: character.id,
              unique_index: index,
              set_stage: unique.set,
              // 段階 n を 1 << n のビットにまとめる（party/db-schema.ts の acquired_mask）。
              acquired_mask: unique.acquired.reduce(
                (mask, stage) => mask | (1 << stage),
                0,
              ),
              updated_at: now,
            },
          ];
        }),
      ),
    ),
    ...insert(
      "party_slots",
      player.party.flatMap((characterId, slot) =>
        characterId
          ? [
              {
                user_id: userId,
                slot,
                character_id: characterId,
                updated_at: now,
              },
            ]
          : [],
      ),
    ),
    ...insert("equipment_profiles", [
      {
        user_id: userId,
        grant_id: grantId,
        created_at: now,
      },
    ]),
    ...insert(
      "equipment_items",
      player.items.map((equipmentId, index) => ({
        id: itemIds[index],
        user_id: userId,
        equipment_id: equipmentId,
        multiplier_percent: player.itemMultipliers?.[index] ?? 100,
        acquired_at: now,
      })),
    ),
    ...insert("equipment_character_slots", worn),
    ...insert(
      "skill_cards",
      (player.skillCards ?? []).map((card, index) => ({
        id: stableUuid(`seed:${player.id}:skill:${index}`),
        user_id: userId,
        skill_id: card.id,
        multiplier_percent: card.multiplierPercent,
        acquired_at: now,
      })),
    ),
    ...insert(
      "item_holdings",
      (player.itemCounts ?? []).map((item) => ({
        user_id: userId,
        item_id: item.id,
        count: item.count,
        acquired_at: now,
        updated_at: now,
      })),
    ),
    ...insert("step_bonus_profiles", [{ user_id: userId, created_at: now }]),
    ...insert(
      "step_bonus_holdings",
      player.bonuses.map((bonus) => ({
        user_id: userId,
        bonus_id: bonus.id,
        multiplier_percent: bonus.multiplierPercent,
        acquired_at: now,
        updated_at: now,
      })),
    ),
    ...insert(
      "step_bonus_slots",
      player.bonusSlots.flatMap((bonusId, slot) =>
        bonusId
          ? [{ user_id: userId, slot, bonus_id: bonusId, updated_at: now }]
          : [],
      ),
    ),
    ...insert(
      "adventure_records",
      player.records.map((record) => ({
        user_id: userId,
        destination_id: record.destinationId,
        best_floor: record.bestFloor,
        clears: record.clears,
        updated_at: now,
      })),
    ),
    ...insert(
      "adventure_runs",
      player.adventure
        ? [
            {
              id: stableUuid(`seed:${player.id}:run`),
              user_id: userId,
              destination_id: player.adventure.destinationId,
              seed: player.adventure.seed,
              room_id: "entrance",
              floor: 1,
              room_kind: "start",
              room_cleared: true,
              route: JSON.stringify(["entrance"]),
              revives: 0,
              status: "active",
              started_at: now,
              updated_at: now,
            },
          ]
        : [],
    ),
    ...insert("library_discoveries", [
      ...discoveries(
        "character",
        player.characters.map((c) => c.id),
      ),
      ...discoveries("skill", [
        ...player.characters.flatMap((character) => character.cards),
        ...(player.skillCards ?? []).map((card) => card.id),
      ]),
      ...(["weapon", "armor", "accessory"] as const).flatMap((kind) =>
        discoveries(
          kind,
          player.items.filter((equipmentId) => kindOf(equipmentId) === kind),
        ),
      ),
      ...discoveries(
        "item",
        (player.itemCounts ?? []).map((item) => item.id),
      ),
      ...player.enemies.map((enemy) => ({
        user_id: userId,
        kind: "enemy",
        entry_id: enemy.enemyId,
        discovered_at: now,
        defeated: enemy.defeated,
      })),
    ]),
    ...insert(
      "combat_revealed_weaknesses",
      player.weaknesses.map((weakness) => ({
        user_id: userId,
        enemy_id: weakness.enemyId,
        element: weakness.element,
        revealed_at: now,
      })),
    ),
  ];
}

/** seedのプレイヤー全員を入れるSQL文。空のDB（マイグレーション適用直後）へ入れる前提。 */
export function buildSeedStatements(now = new Date().toISOString()) {
  return SEED_PLAYERS.flatMap((player) => playerStatements(player, now));
}
