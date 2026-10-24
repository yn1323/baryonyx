import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  type ApiScenario,
  createApiScenario,
} from "../../../tests/support/api-scenario.js";
import { days } from "../health/fixtures.js";
import { REVIVE_COST_BASE, reviveCost } from "./catalog.js";
import { eligibleEntries } from "./drops.js";
import { createAdventureRepository } from "./repository.js";

// 決めた順に値を返し、尽きたら0を返す乱数。抽選は落ちる数・分類・物・品質・倍率の順に引く。
const fixed =
  (...values: number[]) =>
  () =>
    values.shift() ?? 0;

// 強化素材（material）は、ふつうのドロップとは別に抽選する（rollRoomDrops）。数や一覧の検査からは外す。
const withoutMaterials = (drops: Drop[] = []) =>
  drops.filter((drop) => drop.category !== "material");

type Drop = {
  roomId: string;
  category: string;
  id: string;
  // アイテムは倍率と品質を持たず、項目がない。
  multiplier?: number;
  quality?: string;
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

type Goods = {
  index: number;
  category: string;
  id: string;
  multiplierPercent: number;
  quality: string;
  price: number;
  sold: boolean;
};

type State = {
  drops?: Drop[];
  revive?: { roomId: string; runes: number; balanceAfter: number };
  purchase?: {
    roomId: string;
    index: number;
    runes: number;
    balanceAfter: number;
  };
  stepDoor?: { steps: number; tier: number };
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
    mimic: boolean;
    trial?: string;
    merchant?: Goods[];
    drops: Drop[];
  } | null;
  records: { destinationId: string; bestFloor: number; clears: number }[];
  runes: number;
};

type Room = { roomId: string; floor: number; kind: string };

// クライアントが種から作る道のマス（IDは「f階-番号」）。サーバーは階と種類だけを確かめる。
// 第2層は魔物だけで、宝箱は第3層から、強い魔物は第4層から置ける（doc/features/stage-progression.md の道の決まり）。
const rooms = {
  battle: { roomId: "f2-0", floor: 2, kind: "battle" },
  treasure: { roomId: "f3-0", floor: 3, kind: "treasure" },
  battle3: { roomId: "f3-1", floor: 3, kind: "battle" },
  elite: { roomId: "f4-0", floor: 4, kind: "elite" },
  boss: { roomId: "boss", floor: 10, kind: "boss" },
} satisfies Record<string, Room>;

const requestIds = {
  first: "bbbbbbbb-1111-4111-8111-bbbbbbbbbbbb",
  second: "bbbbbbbb-2222-4222-8222-bbbbbbbbbbbb",
  third: "bbbbbbbb-3333-4333-8333-bbbbbbbbbbbb",
};

// 最奥の間の行を借りるマス（ミミック・試練・歩数の扉）で落ちうる物。主の物を除く。
const bossRowPool = eligibleEntries("forest-ruins", "boss", false);
const inPool = (pool: typeof bossRowPool, drop: Drop) =>
  pool.some(
    (entry) => entry.category === drop.category && entry.id === drop.id,
  );

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

  // 失敗する要求を送り、ステータスとエラーの名前を確かめる。
  async function rejects(
    token: string,
    path: string,
    body: unknown,
    error: string,
    status = 409,
  ) {
    const response = await scenario.request(path, "POST", body, token);
    expect(response.status).toBe(status);
    expect(await response.json()).toEqual({ error });
  }

  // 第2層から until の手前の階まで、魔物（第6層は宝箱）のマス「f階-0」を進んで終える。
  async function walk(token: string, until: number) {
    for (let floor = 2; floor < until; floor++) {
      const roomId = `f${floor}-0`;
      await move(token, {
        roomId,
        floor,
        kind: floor === 6 ? "treasure" : "battle",
      });
      await clear(token, roomId);
    }
  }

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
      scenario.request("/adventure/open", "POST", { roomId: "f4-0" }),
      scenario.request("/adventure/buy", "POST", {
        requestId: requestIds.first,
        roomId: "f4-0",
        index: 0,
      }),
    ]);
    expect(responses.map((response) => response.status)).toEqual([
      401, 401, 401, 401, 401, 401, 401, 401,
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
    await call(user.token, "/adventure/open", { roomId: "BAD ID" }, 400);
    await call(
      user.token,
      "/adventure/clear",
      { roomId: "f2-0", pass: "yes" },
      400,
    );
    await call(
      user.token,
      "/adventure/clear",
      { roomId: "f2-0", trialMet: 1 },
      400,
    );
    for (const body of [
      { requestId: "short", roomId: "f4-0", index: 0 },
      { requestId: requestIds.first, roomId: "f4-0", index: -1 },
      { requestId: requestIds.first, roomId: "f4-0", index: 1.5 },
      { requestId: requestIds.first, roomId: "f4-0" },
    ]) {
      await call(user.token, "/adventure/buy", body, 400);
    }
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

  it("入口のマスから道の種とマスの数を決めて始め、選んだマスを保存して、出来事を終えるまで先へ進ませない", async () => {
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

  it("マスはその種類を置ける階でだけ選べる", async () => {
    const user = await scenario.login("adventure-kinds");
    await start(user.token);
    // 第2層は魔物だけ。
    await move(user.token, { roomId: "f2-0", floor: 2, kind: "rest" }, 409);
    await move(user.token, rooms.battle);
    await clear(user.token, "f2-0");
    // 第3層（2つ目）には、行商人・休憩・試練はまだ置けない。進化の祭壇は置ける。
    for (const kind of ["merchant", "rest", "trial", "elite", "step-door"]) {
      await move(user.token, { roomId: "f3-0", floor: 3, kind }, 409);
    }
    const altar = await move(user.token, {
      roomId: "f3-0",
      floor: 3,
      kind: "altar",
    });
    expect(altar.run).toMatchObject({ roomKind: "altar", roomCleared: false });
    // 祭壇は、進化を作るまでは宝箱と同じ1〜3つを落とす。
    const drops = withoutMaterials((await clear(user.token, "f3-0")).drops);
    expect(drops.length).toBeGreaterThanOrEqual(1);
    expect(drops.length).toBeLessThanOrEqual(3);
    // 第4層（3つ目）の行商人は何も落とさずに終える。
    await move(user.token, { roomId: "f4-0", floor: 4, kind: "merchant" });
    const merchant = await clear(user.token, "f4-0");
    expect(merchant.drops).toEqual([]);
    expect(merchant.run?.roomCleared).toBe(true);
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
    await move(user.token, rooms.battle);
    await clear(user.token, "f2-0");
    await move(user.token, rooms.treasure);
    const cleared = await clear(user.token, "f3-0");
    expect(cleared.run?.roomCleared).toBe(true);
    const drops = withoutMaterials(cleared.drops);
    // 宝箱は1〜3つ落ちる。
    expect(drops.length).toBeGreaterThanOrEqual(1);
    expect(drops.length).toBeLessThanOrEqual(3);
    const pool = eligibleEntries("forest-ruins", "treasure");
    for (const drop of drops) {
      expect(drop.roomId).toBe("f3-0");
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
    expect(
      cleared.run?.drops?.filter((drop) => drop.roomId === "f3-0"),
    ).toEqual(cleared.drops);

    // 再送は、記録したドロップを返し、もう一度は手に入らない。
    const again = await clear(user.token, "f3-0");
    expect(again.drops).toEqual(cleared.drops);
    expect(again.run?.drops).toEqual(cleared.run?.drops);
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
    // 魔物の部屋で、2つ落ちる（0.9）。分類は重みの累積（アイテム43・アクセサリー22・武器13・防具13・スキル9）で、
    // 武器（0.65）と、スキル（0.95）。どちらも一覧の最初の物（0）・品質C（0）・最小の倍率（0）。
    // 続く強化素材の抽選は乱数が尽きて0になり、磨き石（30%）は白が落ち、覚醒の欠片（魔物では0%）は落ちない。
    const repository = createAdventureRepository(
      scenario.env.DB,
      fixed(0.9, 0.65, 0, 0, 0, 0.95, 0, 0, 0),
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
      },
      {
        roomId: "f2-0",
        category: "skill",
        id: "Slash",
        multiplier: 1,
        quality: "C",
      },
      {
        roomId: "f2-0",
        category: "material",
        id: "polish-white",
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
    await move(user.token, rooms.treasure);
    const elite = createAdventureRepository(
      scenario.env.DB,
      // 宝箱（強い魔物と同じ重み）で3つ（0.99）：アイテム（0）の薬草（0）を2つと、アクセサリー（0.5）の苔玉のお守り（0）。
      // 強化素材は乱数が尽きて0になり、白い磨き石と覚醒の欠片（宝箱では3%）が落ちる。
      fixed(0.99, 0, 0, 0, 0, 0.5, 0, 0, 0),
    );
    const second = await elite.clear(user.userId, "f3-0", now);
    expect("drops" in second && second.drops.map((drop) => drop.id)).toEqual([
      "herb",
      "herb",
      "moss-charm",
      "polish-white",
      "awakening-shard",
    ]);
    // 強化素材は種類ごとの数を足す。
    expect(
      (
        await scenario.read<{ materials: { id: string; count: number }[] }>(
          "/enhancement",
          user.token,
        )
      ).materials,
    ).toEqual([
      { id: "polish-white", count: 2 },
      { id: "polish-blue", count: 0 },
      { id: "polish-purple", count: 0 },
      { id: "polish-gold", count: 0 },
      { id: "awakening-shard", count: 1 },
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

  it("戦闘のないマスや終えたマスからは復活できない", async () => {
    const user = await scenario.login("adventure-revive-room");
    await scenario.setRunes(user.userId, 1000);
    await start(user.token);
    await call(
      user.token,
      "/adventure/revive",
      { requestId: requestIds.first, roomId: "entrance" },
      409,
    );
    await move(user.token, rooms.battle);
    await clear(user.token, "f2-0");
    await move(user.token, rooms.treasure);
    await call(
      user.token,
      "/adventure/revive",
      { requestId: requestIds.second, roomId: "f3-0" },
      409,
    );
  });

  it("負けて帰還すると冒険を終え、最深到達を記録し、手に入れた物は残る", async () => {
    const user = await scenario.login("adventure-defeat");
    await start(user.token);
    await move(user.token, rooms.battle);
    const won = await clear(user.token, "f2-0");
    await move(user.token, rooms.battle3);
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

  it("戦闘のないマスでは負けて終えられないが、自分からはいつでもやめられる", async () => {
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
    // 第6層は宝箱、最奥の直前の第9層は休憩（落とす物はない）。
    for (let floor = 2; floor <= 9; floor++) {
      const kind = floor === 6 ? "treasure" : floor === 9 ? "rest" : "battle";
      const room = { roomId: `f${floor}-1`, floor, kind };
      await move(user.token, room);
      const drops = (await clear(user.token, room.roomId)).drops ?? [];
      if (kind === "rest") expect(drops).toEqual([]);
      else expect(drops.length).toBeGreaterThanOrEqual(1);
      expect(drops.every((drop) => drop.roomId === room.roomId)).toBe(true);
    }
    // 最奥の階には、ボスのマスだけがある。
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
    // 休憩（第9層）を除く、道中の7マスと最奥の間で落ちた。
    const dropped = new Set(cleared.result?.drops.map((drop) => drop.roomId));
    expect(dropped.size).toBe(8);
    // 最奥の間では2〜4つ落ち、☆3〜5と最奥の間だけの物から選ぶ。
    const bossDrops = withoutMaterials(cleared.drops);
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

  it("怪しい宝箱を開けてミミックなら戦闘のマスになり、勝つと最大の報酬（主の物を除く）を落とす", async () => {
    const user = await scenario.login("adventure-mimic");
    const now = new Date().toISOString();
    await scenario.setRunes(user.userId, 1000);
    await start(user.token);
    await walk(user.token, 4);
    const arrived = await move(user.token, {
      roomId: "f4-1",
      floor: 4,
      kind: "shady-chest",
    });
    expect(arrived.run).toMatchObject({
      roomKind: "shady-chest",
      roomCleared: false,
      mimic: false,
    });
    // 開ける前は、ミミックに勝ったとして終えられず、戦闘のマスでもない。
    await rejects(
      user.token,
      "/adventure/clear",
      { roomId: "f4-1" },
      "chest_not_opened",
    );
    await rejects(
      user.token,
      "/adventure/end",
      { reason: "defeat" },
      "room_mismatch",
    );
    await rejects(
      user.token,
      "/adventure/open",
      { roomId: "f4-0" },
      "room_mismatch",
    );

    // 乱数が半分より小さければミミック。マスはまだ終えていない。
    const opened = await createAdventureRepository(
      scenario.env.DB,
      fixed(0.1),
    ).open(user.userId, "f4-1", now);
    expect("run" in opened && opened.run).toMatchObject({
      roomCleared: false,
      mimic: true,
    });
    expect("drops" in opened && opened.drops).toEqual([]);
    // 再送は、決めた結果をそのまま返す（もう一度は決めない）。
    const replay = await call(user.token, "/adventure/open", {
      roomId: "f4-1",
    });
    expect(replay.run).toMatchObject({ roomCleared: false, mimic: true });
    expect(replay.drops).toEqual([]);
    expect((await call(user.token, "/adventure")).run?.mimic).toBe(true);
    // 開けたあとは通り過ぎられず、戦闘のマスとして復活できる。
    await rejects(
      user.token,
      "/adventure/clear",
      { roomId: "f4-1", pass: true },
      "pass_not_allowed",
    );
    const revived = await call(user.token, "/adventure/revive", {
      requestId: requestIds.first,
      roomId: "f4-1",
    });
    expect(revived.revive?.runes).toBe(REVIVE_COST_BASE);

    // 勝つと、最奥の間の行から2〜4つ落ち、遭遇した敵を記録する。
    const won = await call(user.token, "/adventure/clear", {
      roomId: "f4-1",
      enemies: [{ enemyId: "chest-mimic", defeated: 1 }],
    });
    const drops = withoutMaterials(won.drops);
    expect(drops.length).toBeGreaterThanOrEqual(2);
    expect(drops.length).toBeLessThanOrEqual(4);
    expect(drops.every((drop) => inPool(bossRowPool, drop))).toBe(true);
    expect(won.run).toMatchObject({ roomCleared: true, mimic: true });
    const library = await scenario.read<{
      discoveries: { kind: string; entryId: string; defeated: number }[];
    }>("/library", user.token);
    expect(library.discoveries).toContainEqual(
      expect.objectContaining({
        kind: "enemy",
        entryId: "chest-mimic",
        defeated: 1,
      }),
    );
    // 次のマスへ進むと、ミミックの印は消える。
    const next = await move(user.token, {
      roomId: "f5-0",
      floor: 5,
      kind: "battle",
    });
    expect(next.run?.mimic).toBe(false);
  });

  it("怪しい宝箱を開けて本物なら、その場でマスを終え、強い報酬を1つ多く落とし、再送では記録した物を返す", async () => {
    const user = await scenario.login("adventure-chest");
    const now = new Date().toISOString();
    await start(user.token);
    await walk(user.token, 4);
    await move(user.token, { roomId: "f4-1", floor: 4, kind: "shady-chest" });
    // 本物（0.9）で、宝箱の行の3つ（0.99）に1つ足した4つ。どれもアイテム（0）の薬草（0）。
    // 続く強化素材は乱数が尽きて0になり、白い磨き石と覚醒の欠片が落ちる。
    const opened = await createAdventureRepository(
      scenario.env.DB,
      fixed(0.9, 0.99),
    ).open(user.userId, "f4-1", now);
    expect("drops" in opened && opened.drops?.map((drop) => drop.id)).toEqual([
      "herb",
      "herb",
      "herb",
      "herb",
      "polish-white",
      "awakening-shard",
    ]);
    expect("run" in opened && opened.run).toMatchObject({
      roomCleared: true,
      mimic: false,
    });
    const items = await scenario.env.DB.prepare(
      "SELECT count FROM item_holdings WHERE user_id = ? AND item_id = 'herb'",
    )
      .bind(user.userId)
      .first<{ count: number }>();
    const herbs = items?.count ?? 0;
    expect(herbs).toBeGreaterThanOrEqual(4);

    const replay = await call(user.token, "/adventure/open", {
      roomId: "f4-1",
    });
    expect(replay.drops).toEqual("drops" in opened ? opened.drops : []);
    expect(replay.run?.mimic).toBe(false);
    expect((await clear(user.token, "f4-1")).drops).toEqual(replay.drops);
    const after = await scenario.env.DB.prepare(
      "SELECT count FROM item_holdings WHERE user_id = ? AND item_id = 'herb'",
    )
      .bind(user.userId)
      .first<{ count: number }>();
    expect(after?.count).toBe(herbs);
  });

  it("怪しい宝箱は開けずに通り過ぎられ、何も落とさず、そのあとは開けられない", async () => {
    const user = await scenario.login("adventure-pass");
    await start(user.token);
    await move(user.token, rooms.battle);
    // 通り過ぎるのは怪しい宝箱だけ。
    await rejects(
      user.token,
      "/adventure/clear",
      { roomId: "f2-0", pass: true },
      "pass_not_allowed",
    );
    await clear(user.token, "f2-0");
    await move(user.token, rooms.battle3);
    await clear(user.token, "f3-1");
    await move(user.token, { roomId: "f4-1", floor: 4, kind: "shady-chest" });
    const passed = await call(user.token, "/adventure/clear", {
      roomId: "f4-1",
      pass: true,
    });
    expect(passed.drops).toEqual([]);
    expect(passed.run).toMatchObject({ roomCleared: true, mimic: false });
    // 再送は同じ結果を返す。
    const again = await call(user.token, "/adventure/clear", {
      roomId: "f4-1",
      pass: true,
    });
    expect(again.drops).toEqual([]);
    await rejects(
      user.token,
      "/adventure/open",
      { roomId: "f4-1" },
      "room_cleared",
    );
    expect(passed.run?.drops.filter((drop) => drop.roomId === "f4-1")).toEqual(
      [],
    );
  });

  it("試練のマスに入ると条件を1つ決め、条件を満たして勝てば最大の報酬、満たさなければ魔物の報酬を落とす", async () => {
    const user = await scenario.login("adventure-trial");
    const now = new Date().toISOString();
    await start(user.token);
    await walk(user.token, 5);
    // 条件は乱数で選ぶ（0.5 は2つ目の「誰も倒れずに勝つ」）。
    const entered = await createAdventureRepository(
      scenario.env.DB,
      fixed(0.5),
    ).move(user.userId, { roomId: "f5-1", floor: 5, kind: "trial" }, now);
    expect("run" in entered && entered.run).toMatchObject({
      roomKind: "trial",
      trial: "no-one-falls",
      roomCleared: false,
    });
    // 読み直しても、同じ部屋の再送でも、条件は変わらない。
    expect((await call(user.token, "/adventure")).run?.trial).toBe(
      "no-one-falls",
    );
    expect(
      (await move(user.token, { roomId: "f5-1", floor: 5, kind: "trial" })).run
        ?.trial,
    ).toBe("no-one-falls");
    const met = await call(user.token, "/adventure/clear", {
      roomId: "f5-1",
      trialMet: true,
      enemies: [{ enemyId: "moss-slime", defeated: 2 }],
    });
    const metDrops = withoutMaterials(met.drops);
    expect(metDrops.length).toBeGreaterThanOrEqual(2);
    expect(metDrops.length).toBeLessThanOrEqual(4);
    expect(metDrops.every((drop) => inPool(bossRowPool, drop))).toBe(true);

    // 試練のほかのマスでは、条件を返さない。
    const treasure = await move(user.token, {
      roomId: "f6-0",
      floor: 6,
      kind: "treasure",
    });
    expect(treasure.run && "trial" in treasure.run).toBe(false);
    await clear(user.token, "f6-0");

    // 条件を満たさずに勝つと、魔物の行から1〜2つ落ちる。
    await move(user.token, { roomId: "f7-1", floor: 7, kind: "trial" });
    const missed = await call(user.token, "/adventure/clear", {
      roomId: "f7-1",
      trialMet: false,
    });
    const missedDrops = withoutMaterials(missed.drops);
    expect(missedDrops.length).toBeGreaterThanOrEqual(1);
    expect(missedDrops.length).toBeLessThanOrEqual(2);
    expect(
      missedDrops.every((drop) =>
        inPool(eligibleEntries("forest-ruins", "battle"), drop),
      ),
    ).toBe(true);
  });

  it("歩数の扉は、扉を開けたときの今日の歩数の段で報酬を決め、歩数と段を返す", async () => {
    const user = await scenario.login("adventure-step-door");
    const sourceId = "cccccccc-1111-4111-8111-cccccccccccc";
    let revision = 0;
    const sync = async (steps: number) => {
      const lease = await scenario.request(
        "/health/syncs",
        "POST",
        { sourceId, provider: "health_connect" },
        user.token,
      );
      revision = ((await lease.json()) as { revision: number }).revision;
      const put = await scenario.request(
        `/health/sources/${sourceId}/days`,
        "PUT",
        { revision, days: days(steps) },
        user.token,
      );
      expect(put.status).toBe(200);
    };
    const door = (floor: number) => ({
      roomId: `f${floor}-1`,
      floor,
      kind: "step-door",
    });
    await start(user.token);
    await walk(user.token, 4);

    // 歩数を同期していなければ0歩で、0段（魔物の報酬）になる。それでも扉は開く。
    await move(user.token, door(4));
    const none = await clear(user.token, "f4-1");
    expect(none.stepDoor).toEqual({ steps: 0, tier: 0 });
    expect(withoutMaterials(none.drops).length).toBeGreaterThanOrEqual(1);
    expect(withoutMaterials(none.drops).length).toBeLessThanOrEqual(2);
    // 再送は、開けたときの歩数と段を返す。
    expect((await clear(user.token, "f4-1")).stepDoor).toEqual({
      steps: 0,
      tier: 0,
    });

    // 5,000〜7,999歩は、強い報酬で1つ多い（2〜4つ）。
    await sync(5000);
    await move(user.token, door(5));
    const strong = await clear(user.token, "f5-1");
    expect(strong.stepDoor).toEqual({ steps: 5000, tier: 2 });
    expect(withoutMaterials(strong.drops).length).toBeGreaterThanOrEqual(2);
    expect(withoutMaterials(strong.drops).length).toBeLessThanOrEqual(4);
    await move(user.token, { roomId: "f6-0", floor: 6, kind: "treasure" });
    await clear(user.token, "f6-0");

    // 3,000〜4,999歩は強い報酬（1〜3つ）。
    await sync(3000);
    await move(user.token, door(7));
    const elite = await clear(user.token, "f7-1");
    expect(elite.stepDoor).toEqual({ steps: 3000, tier: 1 });
    expect(withoutMaterials(elite.drops).length).toBeLessThanOrEqual(3);

    // 8,000歩以上は、最大の報酬（主の物を除く）。
    await sync(8000);
    await move(user.token, door(8));
    const best = await clear(user.token, "f8-1");
    expect(best.stepDoor).toEqual({ steps: 8000, tier: 3 });
    const bestDrops = withoutMaterials(best.drops);
    expect(bestDrops.length).toBeGreaterThanOrEqual(2);
    expect(bestDrops.every((drop) => inPool(bossRowPool, drop))).toBe(true);
    // 扉のほかのマスでは、歩数の扉の項目を返さない。
    await move(user.token, { roomId: "f9-0", floor: 9, kind: "rest" });
    expect("stepDoor" in (await clear(user.token, "f9-0"))).toBe(false);
  });

  it("行商人のマスに入ると品ぞろえを作り、ルーンで買え、残高不足・二重購入・売り切れを拒む", async () => {
    const user = await scenario.login("adventure-merchant");
    const now = new Date().toISOString();
    await start(user.token);
    await walk(user.token, 4);
    // 乱数が0なら、スキル3つと装備2つが一覧の初めの物から、品質Cの最小の倍率で並ぶ。
    const entered = await createAdventureRepository(
      scenario.env.DB,
      fixed(),
    ).move(user.userId, { roomId: "f4-1", floor: 4, kind: "merchant" }, now);
    const goods = ("run" in entered && entered.run?.merchant) || [];
    expect(goods).toEqual([
      {
        index: 0,
        category: "skill",
        id: "Slash",
        multiplierPercent: 100,
        quality: "C",
        price: 300,
        sold: false,
      },
      {
        index: 1,
        category: "skill",
        id: "Fire",
        multiplierPercent: 100,
        quality: "C",
        price: 300,
        sold: false,
      },
      {
        index: 2,
        category: "skill",
        id: "Heal",
        multiplierPercent: 100,
        quality: "C",
        price: 300,
        sold: false,
      },
      {
        index: 3,
        category: "accessory",
        id: "moss-charm",
        multiplierPercent: 100,
        quality: "C",
        price: 300,
        sold: false,
      },
      {
        index: 4,
        category: "accessory",
        id: "acorn-brooch",
        multiplierPercent: 100,
        quality: "C",
        price: 600,
        sold: false,
      },
    ]);
    // 同じ部屋の再送で、品ぞろえは変わらない。
    expect(
      (await move(user.token, { roomId: "f4-1", floor: 4, kind: "merchant" }))
        .run?.merchant,
    ).toEqual(goods);

    const buy = (requestId: string, index: number, roomId = "f4-1") => ({
      requestId,
      roomId,
      index,
    });
    // ルーンが足りなければ買えず、残高は変わらない。
    await scenario.setRunes(user.userId, 500);
    await rejects(
      user.token,
      "/adventure/buy",
      buy(requestIds.first, 4),
      "insufficient_runes",
    );
    await rejects(
      user.token,
      "/adventure/buy",
      buy(requestIds.first, 9),
      "goods_not_found",
    );
    await rejects(
      user.token,
      "/adventure/buy",
      buy(requestIds.first, 0, "f4-0"),
      "room_mismatch",
    );
    expect((await call(user.token, "/adventure")).runes).toBe(500);

    // 買うと、値段を引いて持ち物に入れ、売れた印を付ける。道中で落ちた物とは別に1つ増える。
    const charms = async () =>
      (
        await scenario.read<{
          items: { equipmentId: string; multiplier: number; quality: string }[];
        }>("/equipment", user.token)
      ).items.filter((item) => item.equipmentId === "moss-charm");
    const charmsBefore = await charms();
    const bought = await call(
      user.token,
      "/adventure/buy",
      buy(requestIds.first, 3),
    );
    expect(bought.purchase).toEqual({
      roomId: "f4-1",
      index: 3,
      runes: 300,
      balanceAfter: 200,
    });
    expect(bought.runes).toBe(200);
    expect(bought.run?.merchant?.map((entry) => entry.sold)).toEqual([
      false,
      false,
      false,
      true,
      false,
    ]);
    const charmsAfter = await charms();
    expect(charmsAfter).toHaveLength(charmsBefore.length + 1);
    expect(charmsAfter).toContainEqual(
      expect.objectContaining({ multiplier: 1, quality: "C" }),
    );

    // 同じ要求の再送は、同じ結果を返し、二重に引かない。
    const replay = await call(
      user.token,
      "/adventure/buy",
      buy(requestIds.first, 3),
    );
    expect(replay.purchase).toEqual(bought.purchase);
    expect(replay.runes).toBe(200);
    // 同じ要求IDで別の品物は買えない。売れた品物は、別の要求でも買えない。
    await rejects(
      user.token,
      "/adventure/buy",
      buy(requestIds.first, 0),
      "request_conflict",
    );
    await rejects(
      user.token,
      "/adventure/buy",
      buy(requestIds.second, 3),
      "sold_out",
    );

    // マスを終えても、次のマスへ進むまでは買える。スキルは持ち物のカードに入り、ライブラリに記録する。
    const cleared = await clear(user.token, "f4-1");
    expect(cleared.drops).toEqual([]);
    expect(cleared.run?.merchant).toHaveLength(5);
    await scenario.setRunes(user.userId, 1000);
    const slashes = async () =>
      (
        await scenario.env.DB.prepare(
          "SELECT multiplier_percent FROM skill_cards WHERE user_id = ? AND skill_id = 'Slash' ORDER BY acquired_at",
        )
          .bind(user.userId)
          .all<{ multiplier_percent: number }>()
      ).results;
    const before = await slashes();
    const skill = await call(
      user.token,
      "/adventure/buy",
      buy(requestIds.third, 0),
    );
    expect(skill.runes).toBe(700);
    expect(await slashes()).toHaveLength(before.length + 1);
    const library = await scenario.read<{
      discoveries: { kind: string; entryId: string }[];
    }>("/library", user.token);
    for (const [kind, entryId] of [
      ["skill", "Slash"],
      ["accessory", "moss-charm"],
    ]) {
      expect(library.discoveries).toContainEqual(
        expect.objectContaining({ kind, entryId }),
      );
    }

    // 次のマスへ進むと、品ぞろえは返さず、もう買えない。
    const next = await move(user.token, {
      roomId: "f5-0",
      floor: 5,
      kind: "battle",
    });
    expect(next.run && "merchant" in next.run).toBe(false);
    await rejects(
      user.token,
      "/adventure/buy",
      buy("bbbbbbbb-4444-4444-8444-bbbbbbbbbbbb", 1),
      "room_mismatch",
    );
  });

  it("進行中の冒険があるあいだは、別の冒険を始められない", async () => {
    const user = await scenario.login("adventure-lock");
    await start(user.token);
    await move(user.token, rooms.battle);
    await call(
      user.token,
      "/adventure",
      { destinationId: "forest-ruins" },
      409,
    );
    await call(user.token, "/adventure/end", { reason: "retreat" });
    await start(user.token);
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
