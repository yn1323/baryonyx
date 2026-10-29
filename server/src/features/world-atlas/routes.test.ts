import { afterAll, beforeAll, describe, expect, it } from "vitest";
import {
  type ApiScenario,
  createApiScenario,
} from "../../../tests/support/api-scenario.js";
import { squareOf } from "./catalog.js";
import { ATLAS_MAPS } from "./squares.js";

type State = { opened: { map: string; square: string }[]; runes: number };
type Opened = {
  map: string;
  squareId: string;
  runes: number;
  balanceAfter: number;
};

// 応答の Server-Timing から、D1と往復した回数を読む（src/shared/server-timing.ts）。
const d1Calls = (response: Response) =>
  Number(
    /d1;dur=[\d.]+;desc="(\d+) calls"/.exec(
      response.headers.get("server-timing") ?? "",
    )?.[1],
  );

// 世界地図の始まりのマスの隣のマスと、その隣で始まりに接していないマス。
const world = ATLAS_MAPS.world;
const first = Object.keys(world.squares).find(
  (id) => squareOf("world", id)?.touchesStart,
) as string;
const second = (squareOf("world", first)?.links ?? []).find(
  (id) => id !== world.start && !squareOf("world", id)?.touchesStart,
) as string;
const far = Object.keys(world.squares).find(
  (id) =>
    id !== first &&
    id !== second &&
    !squareOf("world", id)?.touchesStart &&
    !squareOf("world", id)?.links.includes(first),
) as string;
const priceOf = (id: string) => squareOf("world", id)?.price as number;

describe("入れ子の地図のマスを開くルーンのAPI", () => {
  let scenario: ApiScenario;

  beforeAll(async () => {
    scenario = await createApiScenario();
  });

  afterAll(async () => {
    await scenario?.dispose();
  });

  const read = (token: string) => scenario.read<State>("/world-atlas", token);
  const open = (token: string, body: unknown) =>
    scenario.request("/world-atlas/open", "POST", body, token);
  const body = (squareId: string, map = "world") => ({
    requestId: crypto.randomUUID(),
    map,
    squareId,
  });
  async function opened(token: string, request: unknown) {
    const response = await open(token, request);
    expect(response.status).toBe(200);
    return (await response.json()) as Opened;
  }

  it("地図の定義のマスのIDを、要求のスキーマが受け付ける", () => {
    for (const [map, atlas] of Object.entries(ATLAS_MAPS)) {
      expect(map).toMatch(/^[a-z]{2,20}$/);
      for (const id of Object.keys(atlas.squares)) {
        expect(id).toMatch(/^[a-z]{2,20}(?:-[a-z0-9]{1,20}){0,2}$/);
      }
    }
  });

  it("セッションのない要求を401で拒否する", async () => {
    const responses = await Promise.all([
      scenario.request("/world-atlas"),
      scenario.request("/world-atlas/open", "POST", body(first)),
    ]);
    expect(responses.map((response) => response.status)).toEqual([401, 401]);
  });

  it("初めて読むユーザーは、開けたマスがなく、残高を読める", async () => {
    const user = await scenario.login("world-atlas-new");
    await scenario.setRunes(user.userId, 700);
    expect(await read(user.token)).toEqual({ opened: [], runes: 700 });
  });

  it("始まりの隣から1マスずつ開け、マスの値段だけ残高が減る", async () => {
    const user = await scenario.login("world-atlas-open");
    await scenario.setRunes(user.userId, 5000);

    const a = await opened(user.token, body(first));
    expect(a).toEqual({
      map: "world",
      squareId: first,
      runes: priceOf(first),
      balanceAfter: 5000 - priceOf(first),
    });
    const b = await opened(user.token, body(second));
    expect(b.balanceAfter).toBe(5000 - priceOf(first) - priceOf(second));

    expect(await read(user.token)).toEqual({
      opened: [
        { map: "world", square: first },
        { map: "world", square: second },
      ],
      runes: b.balanceAfter,
    });
  });

  it("同じ要求IDの再送では、二重に払わず同じ結果を返す", async () => {
    const user = await scenario.login("world-atlas-resend");
    await scenario.setRunes(user.userId, 5000);
    const request = body(first);
    const a = await opened(user.token, request);
    const b = await opened(user.token, request);
    expect(b).toEqual(a);
    expect((await read(user.token)).runes).toBe(5000 - priceOf(first));

    // 同じ要求IDで別のマスを送ると、使い回しとして拒否する。
    const conflict = await open(user.token, { ...request, squareId: second });
    expect(conflict.status).toBe(409);
    expect(await conflict.json()).toEqual({ error: "request_conflict" });
  });

  it("開けたマスを別の要求で送っても、払わずに開いたまま返す", async () => {
    const user = await scenario.login("world-atlas-again");
    await scenario.setRunes(user.userId, 5000);
    await opened(user.token, body(first));
    const again = await opened(user.token, body(first));
    expect(again).toEqual({
      map: "world",
      squareId: first,
      runes: 0,
      balanceAfter: 5000 - priceOf(first),
    });
  });

  it("同時に届いた同じマスの要求では、1回だけ払う", async () => {
    const user = await scenario.login("world-atlas-race");
    await scenario.setRunes(user.userId, 5000);
    const results = await Promise.all([
      opened(user.token, body(first)),
      opened(user.token, body(first)),
    ]);
    expect(results.map((r) => r.runes).sort()).toEqual([0, priceOf(first)]);
    expect((await read(user.token)).runes).toBe(5000 - priceOf(first));
  });

  it("開けたマスの隣でないマスは、払わずに拒否する", async () => {
    const user = await scenario.login("world-atlas-far");
    await scenario.setRunes(user.userId, 5000);
    for (const id of [second, far]) {
      const response = await open(user.token, body(id));
      expect(response.status).toBe(409);
      expect(await response.json()).toEqual({ error: "square_not_reachable" });
    }
    expect(await read(user.token)).toEqual({ opened: [], runes: 5000 });
  });

  it("ルーンが足りないときは開けず、残高も変えない", async () => {
    const user = await scenario.login("world-atlas-poor");
    await scenario.setRunes(user.userId, priceOf(first) - 1);
    const response = await open(user.token, body(first));
    expect(response.status).toBe(409);
    expect(await response.json()).toEqual({ error: "insufficient_runes" });
    expect(await read(user.token)).toEqual({
      opened: [],
      runes: priceOf(first) - 1,
    });
  });

  it("地図にないマス・始まりのマス・ない地図は404、不正な本文は400で拒否する", async () => {
    const user = await scenario.login("world-atlas-unknown");
    await scenario.setRunes(user.userId, 5000);
    for (const request of [
      body("world-999"),
      body(world.start),
      body(first, "nowhere"),
      body("constructor"),
    ]) {
      const response = await open(user.token, request);
      expect(response.status).toBe(404);
      expect(await response.json()).toEqual({ error: "square_not_found" });
    }
    for (const request of [
      { ...body(first), requestId: "x" },
      { ...body(first), map: "World" },
      { map: "world", squareId: first },
    ]) {
      expect((await open(user.token, request)).status).toBe(400);
    }
    expect((await read(user.token)).runes).toBe(5000);
  });

  it("開けたマスはユーザーごとに分かれる", async () => {
    const a = await scenario.login("world-atlas-user-a");
    const b = await scenario.login("world-atlas-user-b");
    await scenario.setRunes(a.userId, 5000);
    await scenario.setRunes(b.userId, 5000);
    await opened(a.token, body(first));
    // Bは始まりの隣を開けていないため、その先は開けられない。
    expect((await open(b.token, body(second))).status).toBe(409);
    expect(await read(b.token)).toEqual({ opened: [], runes: 5000 });
  });

  it("読むのも開けるのも、D1との往復は1回で済む", async () => {
    const user = await scenario.login("world-atlas-trips");
    await scenario.setRunes(user.userId, 5000);
    await read(user.token);
    const get = await scenario.request(
      "/world-atlas",
      "GET",
      undefined,
      user.token,
    );
    expect(d1Calls(get)).toBe(1);
    const post = await open(user.token, body(first));
    expect(post.status).toBe(200);
    expect(d1Calls(post)).toBe(1);
  });
});
