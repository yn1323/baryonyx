import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  createHealthScenario,
  type HealthScenario,
} from "../../../tests/support/health-scenario.js";
import { REVIVE_COST_BASE, reviveCost } from "./catalog.js";
import { createAdventureRepository } from "./repository.js";

type Reward = {
  roomId: string;
  bonusId: string;
  rank: string;
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
  rewards: Reward[];
};

type State = {
  reward?: Reward | null;
  revive?: { roomId: string; runes: number; balanceAfter: number };
  result?: Result;
  run: {
    id: string;
    destinationId: string;
    roomId: string;
    roomCleared: boolean;
    route: string[];
    revives: number;
    reviveCost: number;
    rooms: {
      id: string;
      floor: number;
      kind: string;
      encounter: string;
      next: string[];
    }[];
    rewards: Reward[];
  } | null;
  records: { destinationId: string; bestFloor: number; clears: number }[];
  runes: number;
};

const requestIds = {
  first: "bbbbbbbb-1111-4111-8111-bbbbbbbbbbbb",
  second: "bbbbbbbb-2222-4222-8222-bbbbbbbbbbbb",
};

describe("冒険API", () => {
  let scenario: HealthScenario;

  beforeAll(async () => {
    scenario = await createHealthScenario();
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
  const move = (token: string, roomId: string, expected = 200) =>
    call(token, "/adventure/move", { roomId }, expected);
  const clear = (token: string, roomId: string, expected = 200) =>
    call(token, "/adventure/clear", { roomId }, expected);

  async function setRunes(userId: string, balance: number) {
    await scenario.env.DB.prepare(
      "INSERT INTO rune_wallets (user_id, balance, updated_at) VALUES (?, ?, ?) ON CONFLICT(user_id) DO UPDATE SET balance = excluded.balance",
    )
      .bind(userId, balance, new Date().toISOString())
      .run();
  }

  it("セッションのない要求を401で拒否する", async () => {
    const responses = await Promise.all([
      scenario.request("/adventure"),
      scenario.request("/adventure", "POST", { destinationId: "forest-ruins" }),
      scenario.request("/adventure/move", "POST", { roomId: "moss-hall" }),
      scenario.request("/adventure/clear", "POST", { roomId: "moss-hall" }),
      scenario.request("/adventure/revive", "POST", {
        requestId: requestIds.first,
        roomId: "moss-hall",
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
    await call(user.token, "/adventure/move", { roomId: "BAD ID" }, 400);
    await call(user.token, "/adventure/end", { reason: "give-up" }, 400);
    await call(
      user.token,
      "/adventure/revive",
      { requestId: "short", roomId: "moss-hall" },
      400,
    );
  });

  it("冒険がないあいだは進行中の冒険を返さず、進む・終える要求を404で返す", async () => {
    const user = await scenario.login("adventure-none");
    const state = await call(user.token, "/adventure");
    expect(state.run).toBeNull();
    expect(state.records).toEqual([]);
    await move(user.token, "moss-hall", 404);
    await clear(user.token, "moss-hall", 404);
    await call(user.token, "/adventure/end", { reason: "retreat" }, 404);
  });

  it("入口の部屋から始め、選んだ部屋を保存して、出来事を終えるまで先へ進ませない", async () => {
    const user = await scenario.login("adventure-route");
    const started = await start(user.token);
    expect(started.run?.roomId).toBe("entrance");
    expect(started.run?.roomCleared).toBe(true);
    expect(started.run?.route).toEqual(["entrance"]);
    expect(started.run?.rooms.map((room) => room.id)).toContain("sanctum");

    // 届かなかった応答の再送は、始めたばかりの冒険を返す。
    expect((await start(user.token)).run?.id).toBe(started.run?.id);

    await move(user.token, "sanctum", 409);
    const moved = await move(user.token, "moss-hall");
    expect(moved.run?.roomId).toBe("moss-hall");
    expect(moved.run?.roomCleared).toBe(false);
    expect(moved.run?.route).toEqual(["entrance", "moss-hall"]);

    // 選んだ部屋は読み直しても残る（戦闘中にアプリが終わっても、ここから再開する）。
    const reread = await call(user.token, "/adventure");
    expect(reread.run?.roomId).toBe("moss-hall");
    expect(reread.run?.roomCleared).toBe(false);

    // 戦闘に勝つまで次の部屋へは進めない。同じ部屋の再送はそのまま返す。
    await move(user.token, "root-gallery", 409);
    expect((await move(user.token, "moss-hall")).run?.roomId).toBe("moss-hall");
  });

  it("出来事を終えると報酬のUPTボーナスを1回だけ手に入れ、持ち物に入れる", async () => {
    const user = await scenario.login("adventure-reward");
    await start(user.token);
    await move(user.token, "hidden-store");
    const cleared = await clear(user.token, "hidden-store");
    expect(cleared.run?.roomCleared).toBe(true);
    expect(cleared.reward?.roomId).toBe("hidden-store");
    expect(["D", "C", "B", "A"]).toContain(cleared.reward?.rank);
    expect(cleared.run?.rewards).toEqual([cleared.reward]);

    // 再送は、記録した報酬を返し、もう一度は手に入らない。
    const again = await clear(user.token, "hidden-store");
    expect(again.reward).toEqual(cleared.reward);
    expect(again.run?.rewards).toHaveLength(1);

    const bonus = await scenario.request(
      "/step-bonus",
      "GET",
      undefined,
      user.token,
    );
    const holdings = (
      (await bonus.json()) as { holdings: { bonusId: string; rank: string }[] }
    ).holdings;
    const held = holdings.find(
      (holding) => holding.bonusId === cleared.reward?.bonusId,
    );
    expect(held).toBeDefined();
    if (cleared.reward?.outcome !== "discarded") {
      expect(held?.rank).toBe(cleared.reward?.rank);
    }
  });

  it("手に入れたボーナスは、持っているものよりランクが高いときだけ入れ替える", async () => {
    const user = await scenario.login("adventure-rank");
    const now = new Date().toISOString();
    // guard は初期のボーナスでSランク。最小の乱数で guard と最低ランクを引かせる。
    const low = createAdventureRepository(scenario.env.DB, () => 0);
    await low.start(user.userId, "forest-ruins", now);
    await low.move(user.userId, "moss-hall", now);
    const result = await low.clear(user.userId, "moss-hall", now);
    expect("reward" in result && result.reward).toMatchObject({
      bonusId: "guard",
      rank: "E",
      outcome: "discarded",
    });
    const bonus = await scenario.request(
      "/step-bonus",
      "GET",
      undefined,
      user.token,
    );
    const holdings = (
      (await bonus.json()) as { holdings: { bonusId: string; rank: string }[] }
    ).holdings;
    expect(holdings.find((holding) => holding.bonusId === "guard")?.rank).toBe(
      "S",
    );
  });

  it("負けた戦闘からルーンで復活し、2回目は費用が上がり、再送では二重に引かない", async () => {
    const user = await scenario.login("adventure-revive");
    await setRunes(user.userId, 250);
    await start(user.token);
    await move(user.token, "moss-hall");
    expect((await call(user.token, "/adventure")).run?.reviveCost).toBe(
      REVIVE_COST_BASE,
    );

    const first = await call(user.token, "/adventure/revive", {
      requestId: requestIds.first,
      roomId: "moss-hall",
    });
    expect(first.revive).toEqual({
      roomId: "moss-hall",
      runes: reviveCost(0),
      balanceAfter: 150,
    });
    expect(first.runes).toBe(150);
    expect(first.run?.revives).toBe(1);
    expect(first.run?.reviveCost).toBe(reviveCost(1));

    const replay = await call(user.token, "/adventure/revive", {
      requestId: requestIds.first,
      roomId: "moss-hall",
    });
    expect(replay.runes).toBe(150);
    expect(replay.run?.revives).toBe(1);

    // 2回目は200ルーン要り、150では足りない。
    const short = await scenario.request(
      "/adventure/revive",
      "POST",
      { requestId: requestIds.second, roomId: "moss-hall" },
      user.token,
    );
    expect(short.status).toBe(409);
    expect(await short.json()).toEqual({ error: "insufficient_runes" });
  });

  it("戦闘のない部屋や終えた部屋からは復活できない", async () => {
    const user = await scenario.login("adventure-revive-room");
    await setRunes(user.userId, 1000);
    await start(user.token);
    await call(
      user.token,
      "/adventure/revive",
      { requestId: requestIds.first, roomId: "entrance" },
      409,
    );
    await move(user.token, "hidden-store");
    await call(
      user.token,
      "/adventure/revive",
      { requestId: requestIds.second, roomId: "hidden-store" },
      409,
    );
  });

  it("負けて帰還すると冒険を終え、最深到達を記録し、手に入れた物は残る", async () => {
    const user = await scenario.login("adventure-defeat");
    await start(user.token);
    await move(user.token, "moss-hall");
    const won = await clear(user.token, "moss-hall");
    await move(user.token, "guardian-gate");
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
    expect(ended.result?.rewards).toEqual([won.reward]);
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
    await move(user.token, "moss-hall");
    // 戦闘の途中でもやめられる。
    const ended = await call(user.token, "/adventure/end", {
      reason: "retreat",
    });
    expect(ended.result?.status).toBe("retreated");
  });

  it("最奥のボスを倒すと冒険を終え、踏破の回数を数える", async () => {
    const user = await scenario.login("adventure-clear");
    await start(user.token);
    for (const room of ["moss-hall", "root-gallery", "sanctum"]) {
      await move(user.token, room);
      const cleared = await clear(user.token, room);
      expect(cleared.reward?.roomId).toBe(room);
      if (room === "sanctum") {
        expect(cleared.run).toBeNull();
        expect(cleared.result).toMatchObject({
          status: "cleared",
          floor: 4,
          bestFloor: 4,
          newRecord: true,
          clears: 1,
        });
        expect(cleared.result?.rewards).toHaveLength(3);
        expect(["B", "A", "S"]).toContain(cleared.reward?.rank);
      }
    }
    expect((await call(user.token, "/adventure")).records).toEqual([
      { destinationId: "forest-ruins", bestFloor: 4, clears: 1 },
    ]);
  });

  it("進行中の冒険があるあいだは、別の冒険を始められずボーナスの枠も付け替えられない", async () => {
    const user = await scenario.login("adventure-lock");
    await start(user.token);
    await move(user.token, "moss-hall");
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
    await move(first.token, "moss-hall");
    expect((await call(second.token, "/adventure")).run).toBeNull();
    expect((await start(second.token)).run?.roomId).toBe("entrance");
  });
});
