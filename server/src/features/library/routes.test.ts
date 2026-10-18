import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  type ApiScenario,
  createApiScenario,
} from "../../../tests/support/api-scenario.js";
import { EQUIPMENT } from "../equipment/catalog.js";
import { STARTER_CHARACTERS } from "../party/catalog.js";

type Discovery = {
  kind: string;
  entryId: string;
  discoveredAt: string;
  defeated: number;
};
type State = { discoveries: Discovery[] };
type Steps = { days: { day: string; steps: number }[] };

// クライアントが種から作る道の部屋（IDは「f階-道」）。サーバーは階と種類だけを確かめる。
const battleRoom = { roomId: "f2-0", floor: 2, kind: "battle" };
const treasureRoom = { roomId: "f2-1", floor: 2, kind: "treasure" };

describe("ライブラリのAPI", () => {
  let scenario: ApiScenario;

  beforeAll(async () => {
    scenario = await createApiScenario();
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  const read = (token: string) => scenario.read<State>("/library", token);

  async function post(token: string, path: string, body: unknown) {
    const response = await scenario.request(path, "POST", body, token);
    expect(response.status).toBe(200);
    return response.json();
  }

  const entries = (state: State, kind: string) =>
    state.discoveries
      .filter((entry) => entry.kind === kind)
      .map((entry) => entry.entryId)
      .sort();

  const enemies = (state: State) =>
    state.discoveries
      .filter((entry) => entry.kind === "enemy")
      .map(({ entryId, defeated }) => `${entryId}:${defeated}`)
      .sort();

  async function enterBattle(token: string) {
    await post(token, "/adventure", { destinationId: "forest-ruins" });
    await post(token, "/adventure/move", battleRoom);
  }

  it("セッションのない要求を401で拒否する", async () => {
    const responses = await Promise.all([
      scenario.request("/library"),
      scenario.request("/library/steps"),
    ]);
    expect(responses.map((response) => response.status)).toEqual([401, 401]);
  });

  it("初めて読むユーザーには、初めに配るキャラ・カード・装備を見つけた物として返し、敵はまだない", async () => {
    const user = await scenario.login("library-starter");
    const state = await read(user.token);
    const unique = (ids: readonly string[]) => [...new Set(ids)].sort();
    expect(entries(state, "character")).toEqual(
      unique(STARTER_CHARACTERS.map((character) => character.characterId)),
    );
    expect(entries(state, "skill")).toEqual(
      unique(STARTER_CHARACTERS.flatMap((character) => character.cards)),
    );
    for (const slot of ["weapon", "armor"] as const) {
      expect(entries(state, slot)).toEqual(
        unique(
          EQUIPMENT.filter((entry) => entry.slot === slot).map(
            (entry) => entry.id,
          ),
        ),
      );
    }
    expect(entries(state, "enemy")).toEqual([]);
    expect(state.discoveries.every((entry) => entry.defeated === 0)).toBe(true);

    // 読み直しても増えず、初めて見つけた日時も変わらない。
    expect(await read(user.token)).toEqual(state);
  });

  it("編成でカードを付けると、そのカードを見つけたスキルとして残し、外しても消さない", async () => {
    const user = await scenario.login("library-card");
    const before = await read(user.token);
    expect(entries(before, "skill")).not.toContain("FireStorm");

    const path = "/party/characters/toma/cards/0";
    const set = (skillId: string) =>
      scenario.request(path, "PUT", { skillId }, user.token);
    expect((await set("FireStorm")).status).toBe(200);
    expect((await set("Fire")).status).toBe(200);

    const after = await read(user.token);
    expect(entries(after, "skill")).toContain("FireStorm");
    const fire = (state: State) =>
      state.discoveries.find(
        (entry) => entry.kind === "skill" && entry.entryId === "Fire",
      );
    expect(fire(after)).toEqual(fire(before));
  });

  it("戦闘を終えると遭遇した敵と倒した体数を足し、終えた知らせの再送では二重に数えない", async () => {
    const user = await scenario.login("library-clear");
    await enterBattle(user.token);
    const body = {
      roomId: battleRoom.roomId,
      enemies: [
        { enemyId: "moss-slime", defeated: 2 },
        { enemyId: "moss-wolf", defeated: 1 },
        { enemyId: "moss-slime", defeated: 1 },
      ],
    };
    await post(user.token, "/adventure/clear", body);
    await post(user.token, "/adventure/clear", body);
    expect(enemies(await read(user.token))).toEqual([
      "moss-slime:3",
      "moss-wolf:1",
    ]);
  });

  it("負けて帰還したときは負けた戦闘で倒した敵を数え、自分からやめたときは数えない", async () => {
    const user = await scenario.login("library-defeat");
    await enterBattle(user.token);
    await post(user.token, "/adventure/end", {
      reason: "defeat",
      enemies: [
        { enemyId: "moss-wolf", defeated: 1 },
        { enemyId: "forest-guardian", defeated: 0 },
      ],
    });
    expect(enemies(await read(user.token))).toEqual([
      "forest-guardian:0",
      "moss-wolf:1",
    ]);

    await enterBattle(user.token);
    await post(user.token, "/adventure/end", {
      reason: "retreat",
      enemies: [{ enemyId: "moss-wolf", defeated: 5 }],
    });
    expect(enemies(await read(user.token))).toEqual([
      "forest-guardian:0",
      "moss-wolf:1",
    ]);
  });

  it("戦闘のない部屋を終えた知らせに添えた敵は記録しない", async () => {
    const user = await scenario.login("library-treasure");
    await post(user.token, "/adventure", { destinationId: "forest-ruins" });
    await post(user.token, "/adventure/move", treasureRoom);
    await post(user.token, "/adventure/clear", {
      roomId: treasureRoom.roomId,
      enemies: [{ enemyId: "moss-slime", defeated: 1 }],
    });
    expect(enemies(await read(user.token))).toEqual([]);
  });

  it("知らない敵や不正な体数を400で拒否する", async () => {
    const user = await scenario.login("library-invalid");
    await enterBattle(user.token);
    const statuses = await Promise.all(
      [
        [{ enemyId: "dragon", defeated: 1 }],
        [{ enemyId: "moss-slime", defeated: -1 }],
        [{ enemyId: "moss-slime", defeated: 1.5 }],
      ].map(async (list) => {
        const response = await scenario.request(
          "/adventure/clear",
          "POST",
          { roomId: battleRoom.roomId, enemies: list },
          user.token,
        );
        return response.status;
      }),
    );
    expect(statuses).toEqual([400, 400, 400]);
  });

  it("日々の歩数を新しい日から返し、端末を複数使った日はいちばん多い値にして、ユーザーごとに分ける", async () => {
    const user = await scenario.login("library-steps");
    const other = await scenario.login("library-steps-other");
    const db = scenario.env.DB;
    const now = new Date().toISOString();
    const source = async (id: string, userId: string) => {
      await db
        .prepare(
          "INSERT INTO health_sources (id, user_id, provider, revision) VALUES (?, ?, 'health_connect', 0)",
        )
        .bind(id, userId)
        .run();
    };
    const day = async (
      sourceId: string,
      userId: string,
      date: string,
      steps: number | null,
    ) => {
      await db
        .prepare(
          `INSERT INTO exercise_reward_days
            (id, user_id, source_id, day, zone, activity_type, metric_type, has_value, observed_value,
             credited_through_value, credited_runes, rule_version, last_observed_at, updated_at)
           VALUES (?, ?, ?, ?, 'Asia/Tokyo', 'all', 'steps', ?, ?, 0, 0, 'steps_v1', NULL, ?)`,
        )
        .bind(
          crypto.randomUUID(),
          userId,
          sourceId,
          date,
          steps === null ? 0 : 1,
          steps,
          now,
        )
        .run();
    };
    await source("library-phone", user.userId);
    await source("library-tablet", user.userId);
    await source("library-other", other.userId);
    await day("library-phone", user.userId, "2026-10-16", 4_200);
    await day("library-phone", user.userId, "2026-10-17", 8_100);
    await day("library-tablet", user.userId, "2026-10-17", 1_300);
    await day("library-phone", user.userId, "2026-10-18", null);
    await day("library-other", other.userId, "2026-10-17", 99_999);

    expect(await scenario.read<Steps>("/library/steps", user.token)).toEqual({
      days: [
        { day: "2026-10-17", steps: 8_100 },
        { day: "2026-10-16", steps: 4_200 },
      ],
    });
  });
});
