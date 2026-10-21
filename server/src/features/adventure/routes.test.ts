import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  type ApiScenario,
  createApiScenario,
} from "../../../tests/support/api-scenario.js";
import { REVIVE_COST_BASE, reviveCost } from "./catalog.js";
import { eligibleEntries } from "./drops.js";
import { createAdventureRepository } from "./repository.js";

// 決めた順に値を返し、尽きたら0を返す乱数。抽選は落ちる数・分類・物・品質・倍率の順に引く。
const fixed =
  (...values: number[]) =>
  () =>
    values.shift() ?? 0;

type Drop = {
  roomId: string;
  category: string;
  id: string;
  // アイテムは倍率と品質を持たず、項目がない。
  multiplier?: number;
  quality?: string;
  outcome: string;
};

type Result = {
  runId: string;
  destinationId: string;
  status: string;
  floor: number;
  bestFloor: number;
  newRecord: boolean;
  clears: number;
  revives: number;
  drops: Drop[];
};

type State = {
  drops?: Drop[];
  revive?: { roomId: string; runes: number; balanceAfter: number };
  result?: Result;
  run: {
    id: string;
    destinationId: string;
    seed: number;
    roomCount: number;
    roomId: string;
    floor: number;
    roomKind: string;
    roomCleared: boolean;
    route: string[];
    revives: number;
    reviveCost: number;
    drops: Drop[];
  } | null;
  records: { destinationId: string; bestFloor: number; clears: number }[];
  runes: number;
};

type Room = { roomId: string; floor: number; kind: string };

// クライアントが種から作る道の部屋（IDは「f階-道」）。サーバーは階と種類だけを確かめる。
const rooms = {
  battle: { roomId: "f2-0", floor: 2, kind: "battle" },
  treasure: { roomId: "f2-1", floor: 2, kind: "treasure" },
  elite: { roomId: "f3-0", floor: 3, kind: "elite" },
  boss: { roomId: "boss", floor: 10, kind: "boss" },
} satisfies Record<string, Room>;

const requestIds = {
  first: "bbbbbbbb-1111-4111-8111-bbbbbbbbbbbb",
  second: "bbbbbbbb-2222-4222-8222-bbbbbbbbbbbb",
};

describe("冒険API", () => {
  let scenario: ApiScenario;

  beforeAll(async () => {
    scenario = await createApiScenario();
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  async function call(
    token: string,
    path: string,
    body?: unknown,
    expected = 200,
  ) {
    const response = await scenario.request(
      path,
      body === undefined ? "GET" : "POST",
      body,
      token,
    );
    expect(response.status).toBe(expected);
    return (await response.json()) as State;
  }

  const start = (token: string) =>
    call(token, "/adventure", { destinationId: "forest-ruins" });
  const move = (token: string, room: Room, expected = 200) =>
    call(token, "/adventure/move", room, expected);
  const clear = (token: string, roomId: string, expected = 200) =>
    call(token, "/adventure/clear", { roomId }, expected);

  it("セッションのない要求を401で拒否する", async () => {
    const responses = await Promise.all([
      scenario.request("/adventure"),
      scenario.request("/adventure", "POST", { destinationId: "forest-ruins" }),
      scenario.request("/adventure/move", "POST", rooms.battle),
      scenario.request("/adventure/clear", "POST", { roomId: "f2-0" }),
      scenario.request("/adventure/revive", "POST", {
        requestId: requestIds.first,
        roomId: "f2-0",
      }),
      scenario.request("/adventure/end", "POST", { reason: "retreat" }),
    ]);
    expect(responses.map((response) => response.status)).toEqual([
      401, 401, 401, 401, 401, 401,
    ]);
  });

  it("不正な入力を400で拒否する", async () => {
    const user = await scenario.login("adventure-invalid");
    await call(user.token, "/adventure", { destinationId: "nowhere" }, 400);
    await move(user.token, { ...rooms.battle, roomId: "BAD ID" }, 400);
    await move(user.token, { ...rooms.battle, kind: "start" }, 400);
    await move(user.token, { ...rooms.battle, floor: 1 }, 400);
    await call(user.token, "/adventure/move", { roomId: "f2-0" }, 400);
    await call(user.token, "/adventure/end", { reason: "give-up" }, 400);
    await call(
      user.token,
      "/adventure/revive",
      { requestId: "short", roomId: "f2-0" },
      400,
    );
  });

  it("冒険がないあいだは進行中の冒険を返さず、進む・終える要求を404で返す", async () => {
    const user = await scenario.login("adventure-none");
    const state = await call(user.token, "/adventure");
    expect(state.run).toBeNull();
    expect(state.records).toEqual([]);
    await move(user.token, rooms.battle, 404);
    await clear(user.token, "f2-0", 404);
    await call(user.token, "/adventure/end", { reason: "retreat" }, 404);
  });

  it("入口の部屋から道の種と部屋の数を決めて始め、選んだ部屋を保存して、出来事を終えるまで先へ進ませない", async () => {
    const user = await scenario.login("adventure-route");
    const started = await start(user.token);
    expect(started.run).toMatchObject({
      roomId: "entrance",
      floor: 1,
      roomKind: "start",
      roomCleared: true,
      route: ["entrance"],
      roomCount: 8,
    });
    expect(started.run?.seed).toBeGreaterThan(0);

    // 届かなかった応答の再送は、始めたばかりの同じ冒険（同じ道）を返す。
    const replay = await start(user.token);
    expect(replay.run?.id).toBe(started.run?.id);
    expect(replay.run?.seed).toBe(started.run?.seed);

    // 1階ずつしか進めず、最奥のボスは最奥の階にだけいる。
    await move(user.token, rooms.elite, 409);
    await move(user.token, rooms.boss, 409);
    await move(user.token, { ...rooms.battle, kind: "boss" }, 409);
    const moved = await move(user.token, rooms.battle);
    expect(moved.run).toMatchObject({
      roomId: "f2-0",
      floor: 2,
      roomKind: "battle",
      roomCleared: false,
      route: ["entrance", "f2-0"],
    });

    // 選んだ部屋は読み直しても残る（戦闘中にアプリが終わっても、ここから再開する）。
    const reread = await call(user.token, "/adventure");
    expect(reread.run).toMatchObject({
      roomId: "f2-0",
      floor: 2,
      roomKind: "battle",
      roomCleared: false,
      seed: started.run?.seed,
    });

    // 戦闘に勝つまで次の部屋へは進めない。同じ部屋の再送はそのまま返す。
    await move(user.token, rooms.elite, 409);
    expect((await move(user.token, rooms.battle)).run?.roomId).toBe("f2-0");
  });

  it("冒険ごとに道の種が変わる", async () => {
    const user = await scenario.login("adventure-seed");
    const values = [0.25, 0.75];
    const repository = createAdventureRepository(
      scenario.env.DB,
      () => values.shift() ?? 0.5,
    );
    const now = new Date().toISOString();
    const first = await repository.start(user.userId, "forest-ruins", now);
    await repository.end(user.userId, "retreat", now);
    const second = await repository.start(user.userId, "forest-ruins", now);
    const seedOf = (state: typeof first) =>
      "run" in state ? state.run?.seed : undefined;
    expect(seedOf(first)).toBeGreaterThan(0);
    expect(seedOf(second)).toBeGreaterThan(0);
    expect(seedOf(second)).not.toBe(seedOf(first));
  });

  it("出来事を終えると、行き先の一覧から物を落とし、1回だけ持ち物に入れる", async () => {
    const user = await scenario.login("adventure-reward");
    await start(user.token);
    await move(user.token, rooms.treasure);
    const cleared = await clear(user.token, "f2-1");
    expect(cleared.run?.roomCleared).toBe(true);
    const drops = cleared.drops ?? [];
    // 宝箱は1〜3つ落ちる。
    expect(drops.length).toBeGreaterThanOrEqual(1);
    expect(drops.length).toBeLessThanOrEqual(3);
    const pool = eligibleEntries("forest-ruins", "treasure");
    for (const drop of drops) {
      expect(drop.roomId).toBe("f2-1");
      expect(
        pool.some(
          (entry) => entry.category === drop.category && entry.id === drop.id,
        ),
      ).toBe(true);
      if (drop.category === "item") {
        expect("multiplier" in drop || "quality" in drop).toBe(false);
      } else {
        expect(["C", "B", "A", "S"]).toContain(drop.quality);
        expect(drop.multiplier).toBeGreaterThanOrEqual(1);
      }
    }
    expect(cleared.run?.drops).toEqual(drops);

    // 再送は、記録したドロップを返し、もう一度は手に入らない。
    const again = await clear(user.token, "f2-1");
    expect(again.drops).toEqual(drops);
    expect(again.run?.drops).toEqual(drops);
  });

  it("落ちた装備・アクセサリー・スキル・アイテムを持ち物に足す", async () => {
    const user = await scenario.login("adventure-grant");
    const now = new Date().toISOString();
    const equipment = async () =>
      (
        await scenario.read<{
          items: { equipmentId: string; multiplier: number; quality: string }[];
        }>("/equipment", user.token)
      ).items;
    const before = await equipment();
    // 魔物の部屋で、2つ落ちる（0.9）。分類は重みの累積（アイテム40・アクセサリー22・武器13・防具13・スキル9・ボーナス3）で、
    // 武器（0.65）と、スキル（0.9）。どちらも一覧の最初の物（0）・品質C（0）・最小の倍率（0）。
    const repository = createAdventureRepository(
      scenario.env.DB,
      fixed(0.9, 0.65, 0, 0, 0, 0.9, 0, 0, 0),
    );
    await start(user.token);
    await move(user.token, rooms.battle);
    const result = await repository.clear(user.userId, "f2-0", now);
    expect("drops" in result && result.drops).toEqual([
      {
        roomId: "f2-0",
        category: "weapon",
        id: "wooden-sword",
        multiplier: 1,
        quality: "C",
        outcome: "added",
      },
      {
        roomId: "f2-0",
        category: "skill",
        id: "Slash",
        multiplier: 1,
        quality: "C",
        outcome: "added",
      },
    ]);
    const after = await equipment();
    expect(after).toHaveLength(before.length + 1);
    expect(after.filter((item) => item.equipmentId === "wooden-sword")).toEqual(
      [expect.objectContaining({ multiplier: 1, quality: "C" })],
    );
    const cards = await scenario.env.DB.prepare(
      "SELECT skill_id, multiplier_percent FROM skill_cards WHERE user_id = ? AND acquired_at = ?",
    )
      .bind(user.userId, now)
      .all();
    // 初めのカードとは別に、落ちた1枚が増える。
    expect(cards.results).toEqual([
      { skill_id: "Slash", multiplier_percent: 100 },
    ]);

    // 同じアイテムが2つ落ちれば、数を2つ足す。アクセサリーは装備の持ち物に入る。
    await move(user.token, rooms.elite);
    const elite = createAdventureRepository(
      scenario.env.DB,
      // 強い魔物の部屋で3つ（0.99）：アイテム（0）の薬草（0）を2つと、アクセサリー（0.5）の苔玉のお守り（0）。
      fixed(0.99, 0, 0, 0, 0, 0.5, 0, 0, 0),
    );
    const second = await elite.clear(user.userId, "f3-0", now);
    expect("drops" in second && second.drops.map((drop) => drop.id)).toEqual([
      "herb",
      "herb",
      "moss-charm",
    ]);
    const items = await scenario.env.DB.prepare(
      "SELECT item_id, count FROM item_holdings WHERE user_id = ?",
    )
      .bind(user.userId)
      .all();
    expect(items.results).toEqual([{ item_id: "herb", count: 2 }]);
    expect((await equipment()).map((item) => item.equipmentId)).toContain(
      "moss-charm",
    );
    const library = await scenario.read<{
      discoveries: { kind: string; entryId: string }[];
    }>("/library", user.token);
    for (const [kind, entryId] of [
      ["item", "herb"],
      ["accessory", "moss-charm"],
      ["skill", "Slash"],
    ]) {
      expect(library.discoveries).toContainEqual(
        expect.objectContaining({ kind, entryId }),
      );
    }
  });

  it("手に入れたボーナスは、持っているものより倍率が高いときだけ入れ替える", async () => {
    const user = await scenario.login("adventure-rank");
    const now = new Date().toISOString();
    // appraisal は初期のボーナスで×1.24。最大の乱数で、ボーナスの一覧の最後（appraisal）を、
    // 品質Sの最大の倍率（×1.5）で2つ引かせる。1つ目は入れ替え、2つ目は同じ倍率のため捨てる。
    const high = createAdventureRepository(scenario.env.DB, () => 0.999999);
    await start(user.token);
    await move(user.token, rooms.battle);
    const result = await high.clear(user.userId, "f2-0", now);
    expect("drops" in result && result.drops).toEqual([
      {
        roomId: "f2-0",
        category: "bonus",
        id: "appraisal",
        multiplier: 1.5,
        quality: "S",
        outcome: "updated",
      },
      {
        roomId: "f2-0",
        category: "bonus",
        id: "appraisal",
        multiplier: 1.5,
        quality: "S",
        outcome: "discarded",
      },
    ]);
    const { holdings } = await scenario.read<{
      holdings: { bonusId: string; multiplier: number; quality: string }[];
    }>("/step-bonus", user.token);
    expect(
      holdings.find((holding) => holding.bonusId === "appraisal"),
    ).toMatchObject({ multiplier: 1.5, quality: "S" });
  });

  it("負けた戦闘からルーンで復活し、2回目は費用が上がり、再送では二重に引かない", async () => {
    const user = await scenario.login("adventure-revive");
    await scenario.setRunes(user.userId, 250);
    await start(user.token);
    await move(user.token, rooms.battle);
    expect((await call(user.token, "/adventure")).run?.reviveCost).toBe(
      REVIVE_COST_BASE,
    );

    const first = await call(user.token, "/adventure/revive", {
      requestId: requestIds.first,
      roomId: "f2-0",
    });
    expect(first.revive).toEqual({
      roomId: "f2-0",
      runes: reviveCost(0),
      balanceAfter: 150,
    });
    expect(first.runes).toBe(150);
    expect(first.run?.revives).toBe(1);
    expect(first.run?.reviveCost).toBe(reviveCost(1));

    const replay = await call(user.token, "/adventure/revive", {
      requestId: requestIds.first,
      roomId: "f2-0",
    });
    expect(replay.runes).toBe(150);
    expect(replay.run?.revives).toBe(1);

    // 2回目は200ルーン要り、150では足りない。
    const short = await scenario.request(
      "/adventure/revive",
      "POST",
      { requestId: requestIds.second, roomId: "f2-0" },
      user.token,
    );
    expect(short.status).toBe(409);
    expect(await short.json()).toEqual({ error: "insufficient_runes" });
  });

  it("戦闘のない部屋や終えた部屋からは復活できない", async () => {
    const user = await scenario.login("adventure-revive-room");
    await scenario.setRunes(user.userId, 1000);
    await start(user.token);
    await call(
      user.token,
      "/adventure/revive",
      { requestId: requestIds.first, roomId: "entrance" },
      409,
    );
    await move(user.token, rooms.treasure);
    await call(
      user.token,
      "/adventure/revive",
      { requestId: requestIds.second, roomId: "f2-1" },
      409,
    );
  });

  it("負けて帰還すると冒険を終え、最深到達を記録し、手に入れた物は残る", async () => {
    const user = await scenario.login("adventure-defeat");
    await start(user.token);
    await move(user.token, rooms.battle);
    const won = await clear(user.token, "f2-0");
    await move(user.token, rooms.elite);
    const ended = await call(user.token, "/adventure/end", {
      reason: "defeat",
    });
    expect(ended.run).toBeNull();
    expect(ended.result).toMatchObject({
      status: "defeated",
      floor: 3,
      bestFloor: 3,
      newRecord: true,
      clears: 0,
    });
    expect(ended.result?.drops).toEqual(won.drops);
    expect(ended.records).toEqual([
      { destinationId: "forest-ruins", bestFloor: 3, clears: 0 },
    ]);

    // 次の冒険は入口から。浅い階で終えても最深到達は下がらない。
    const next = await start(user.token);
    expect(next.run?.roomId).toBe("entrance");
    const retreat = await call(user.token, "/adventure/end", {
      reason: "retreat",
    });
    expect(retreat.result).toMatchObject({
      status: "retreated",
      floor: 1,
      bestFloor: 3,
      newRecord: false,
    });
  });

  it("戦闘のない部屋では負けて終えられないが、自分からはいつでもやめられる", async () => {
    const user = await scenario.login("adventure-retreat");
    await start(user.token);
    await call(user.token, "/adventure/end", { reason: "defeat" }, 409);
    await move(user.token, rooms.battle);
    // 戦闘の途中でもやめられる。
    const ended = await call(user.token, "/adventure/end", {
      reason: "retreat",
    });
    expect(ended.result?.status).toBe("retreated");
  });

  it("最奥のボスを倒すと冒険を終え、踏破の回数を数える", async () => {
    const user = await scenario.login("adventure-clear");
    await start(user.token);
    for (let floor = 2; floor <= 9; floor++) {
      const room = { roomId: `f${floor}-1`, floor, kind: "battle" };
      await move(user.token, room);
      const drops = (await clear(user.token, room.roomId)).drops ?? [];
      expect(drops.length).toBeGreaterThanOrEqual(1);
      expect(drops.every((drop) => drop.roomId === room.roomId)).toBe(true);
    }
    // 最奥の階には、ボスの部屋だけがある。
    await move(user.token, { ...rooms.boss, kind: "battle" }, 409);
    await move(user.token, rooms.boss);
    const cleared = await clear(user.token, "boss");
    expect(cleared.run).toBeNull();
    expect(cleared.result).toMatchObject({
      status: "cleared",
      floor: 10,
      bestFloor: 10,
      newRecord: true,
      clears: 1,
    });
    const rooms9 = new Set(cleared.result?.drops.map((drop) => drop.roomId));
    expect(rooms9.size).toBe(9);
    // 最奥の間では2〜4つ落ち、☆3〜5と最奥の間だけの物から選ぶ。
    const bossDrops = cleared.drops ?? [];
    expect(bossDrops.length).toBeGreaterThanOrEqual(2);
    expect(bossDrops.length).toBeLessThanOrEqual(4);
    const bossPool = eligibleEntries("forest-ruins", "boss");
    expect(bossPool.every((entry) => entry.stars >= 3)).toBe(true);
    for (const drop of bossDrops) {
      expect(bossPool.some((entry) => entry.id === drop.id)).toBe(true);
    }
    expect((await call(user.token, "/adventure")).records).toEqual([
      { destinationId: "forest-ruins", bestFloor: 10, clears: 1 },
    ]);
  });

  it("進行中の冒険があるあいだは、別の冒険を始められずボーナスの枠も付け替えられない", async () => {
    const user = await scenario.login("adventure-lock");
    await start(user.token);
    await move(user.token, rooms.battle);
    await call(
      user.token,
      "/adventure",
      { destinationId: "forest-ruins" },
      409,
    );

    const bonus = await scenario.request(
      "/step-bonus",
      "GET",
      undefined,
      user.token,
    );
    expect(((await bonus.json()) as { locked: boolean }).locked).toBe(true);
    const put = await scenario.request(
      "/step-bonus/slots/0",
      "PUT",
      { bonusId: "guard" },
      user.token,
    );
    expect(put.status).toBe(409);
    expect(await put.json()).toEqual({ error: "adventure_in_progress" });

    await call(user.token, "/adventure/end", { reason: "retreat" });
    const unlocked = await scenario.request(
      "/step-bonus",
      "GET",
      undefined,
      user.token,
    );
    expect(((await unlocked.json()) as { locked: boolean }).locked).toBe(false);
  });

  it("ユーザーごとに冒険を分ける", async () => {
    const first = await scenario.login("adventure-user-a");
    const second = await scenario.login("adventure-user-b");
    await start(first.token);
    await move(first.token, rooms.battle);
    expect((await call(second.token, "/adventure")).run).toBeNull();
    expect((await start(second.token)).run?.roomId).toBe("entrance");
  });
});
