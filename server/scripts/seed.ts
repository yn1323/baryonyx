// ローカルD1へ入れる開発用のプレイヤー（seed）。`pnpm db:reset` が全テーブルを作り直したあとに投入する。
// 決まり（ID・枠の数・費用）は各機能の catalog.ts、手順と使い方は doc/rules/backend-design.md の「開発用データ（seed）」。
// 値は実在のプレイヤーを模した仮の値で、APIで読み書きできることを tests/integration/seed.test.ts が確かめる。

import { createHash } from "node:crypto";

type Rank = "E" | "D" | "C" | "B" | "A" | "S";

type SeedCharacter = {
  id: string;
  level: number;
  // カスタムスキルの2枠。属性は初期データ（party/catalog.ts の STARTER_CHARACTERS）と同じものにそろえる。
  cards: readonly [string, string];
  weapon?: string;
  armor?: string;
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
  bonuses: readonly { id: string; rank: Rank }[];
  // ボーナスの5枠。空き枠はnull。
  bonusSlots: readonly (string | null)[];
  records: readonly {
    destinationId: string;
    bestFloor: number;
    clears: number;
  }[];
  // 進行中の冒険。入口の部屋に着いたところから始める（道の部屋はクライアントが種から作るため）。
  adventure?: { destinationId: string; seed: number };
};

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
      },
      {
        id: "luka",
        level: 28,
        cards: ["ShadowSnipe", "ChainLightning"],
        weapon: "thunder-spear",
        armor: "traveler-cloak",
      },
      {
        id: "aria",
        level: 27,
        cards: ["BladeDance", "GiantImpact"],
        weapon: "flame-dagger",
        armor: "chainmail",
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
    ],
    bonuses: [
      { id: "guard", rank: "S" },
      { id: "luck", rank: "S" },
      { id: "treasure-sight", rank: "A" },
      { id: "foresight", rank: "A" },
      { id: "omen", rank: "B" },
      { id: "appraisal", rank: "A" },
      { id: "fighting", rank: "S" },
    ],
    bonusSlots: ["fighting", "luck", "guard", "treasure-sight", "appraisal"],
    records: [{ destinationId: "forest-ruins", bestFloor: 10, clears: 3 }],
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
    ],
    bonuses: [
      { id: "luck", rank: "B" },
      { id: "foresight", rank: "C" },
      { id: "fighting", rank: "D" },
    ],
    bonusSlots: ["foresight", "luck", "fighting", null, null],
    records: [{ destinationId: "forest-ruins", bestFloor: 6, clears: 0 }],
    adventure: { destinationId: "forest-ruins", seed: 20_261_005 },
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
    bonuses: [{ id: "luck", rank: "E" }],
    bonusSlots: ["luck", null, null, null, null],
    records: [],
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
  const itemIds = player.items.map((_, index) =>
    stableUuid(`seed:${player.id}:item:${index}`),
  );
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
    ...insert("party_profiles", [{ user_id: userId, created_at: now }]),
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
      "party_character_cards",
      player.characters.flatMap((character) =>
        character.cards.map((skillId, slot) => ({
          user_id: userId,
          character_id: character.id,
          slot,
          skill_id: skillId,
          updated_at: now,
        })),
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
        grant_id: stableUuid(`seed:${player.id}:grant`),
        created_at: now,
      },
    ]),
    ...insert(
      "equipment_items",
      player.items.map((equipmentId, index) => ({
        id: itemIds[index],
        user_id: userId,
        equipment_id: equipmentId,
        acquired_at: now,
      })),
    ),
    ...insert("equipment_character_slots", worn),
    ...insert("step_bonus_profiles", [{ user_id: userId, created_at: now }]),
    ...insert(
      "step_bonus_holdings",
      player.bonuses.map((bonus) => ({
        user_id: userId,
        bonus_id: bonus.id,
        rank: bonus.rank,
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
  ];
}

/** seedのプレイヤー全員を入れるSQL文。空のDB（マイグレーション適用直後）へ入れる前提。 */
export function buildSeedStatements(now = new Date().toISOString()) {
  return SEED_PLAYERS.flatMap((player) => playerStatements(player, now));
}
