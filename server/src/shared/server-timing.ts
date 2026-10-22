import type { Context, Hono } from "hono";
import { setMetric, timing } from "hono/timing";
import type { Env as HonoEnv } from "hono/types";

// 1つの要求でD1と往復した回数と、待った時間の合計（ミリ秒）。
type D1Usage = { calls: number; ms: number };

type D1Env = HonoEnv & { Bindings: { DB: D1Database } };

const STATEMENT_CALLS = new Set(["first", "all", "run", "raw"]);

async function measure<T>(usage: D1Usage, run: () => Promise<T>) {
  const start = performance.now();
  try {
    return await run();
  } finally {
    usage.calls += 1;
    usage.ms += performance.now() - start;
  }
}

// 包んだ文から元の文を引く。batchへは元の文を渡す。
type Originals = WeakMap<object, D1PreparedStatement>;

function timedStatement(
  statement: D1PreparedStatement,
  usage: D1Usage,
  originals: Originals,
): D1PreparedStatement {
  const timed = new Proxy(statement, {
    get(target, property) {
      if (property === "bind") {
        return (...values: unknown[]) =>
          timedStatement(target.bind(...values), usage, originals);
      }
      const value = Reflect.get(target, property, target);
      if (typeof value !== "function") return value;
      if (typeof property === "string" && STATEMENT_CALLS.has(property)) {
        return (...args: unknown[]) =>
          measure(usage, () => value.apply(target, args));
      }
      return value.bind(target);
    },
  });
  originals.set(timed, statement);
  return timed;
}

/**
 * D1の呼び出しを数え、待った時間を足す。batchは文がいくつあっても1回の往復として数える。
 */
export function timedD1(binding: D1Database, usage: D1Usage): D1Database {
  const originals: Originals = new WeakMap();
  return new Proxy(binding, {
    get(target, property) {
      if (property === "prepare") {
        return (query: string) =>
          timedStatement(target.prepare(query), usage, originals);
      }
      if (property === "batch") {
        return (statements: D1PreparedStatement[]) =>
          measure(usage, () =>
            target.batch(
              statements.map(
                (statement) => originals.get(statement) ?? statement,
              ),
            ),
          );
      }
      if (property === "exec") {
        return (query: string) => measure(usage, () => target.exec(query));
      }
      const value = Reflect.get(target, property, target);
      return typeof value === "function" ? value.bind(target) : value;
    },
  });
}

/**
 * 応答の Server-Timing ヘッダーに、処理の合計時間（total）と、D1と往復した回数・待った時間（d1）を返す。
 * 遅い画面の内訳を、端末のログとサーバーの時間で切り分けるために使う。
 */
export function applyServerTiming<E extends D1Env>(api: Hono<E>) {
  api.use("*", timing({ totalDescription: "API" }));
  api.use("*", async (c: Context<E>, next) => {
    const binding = c.env?.DB;
    if (!binding) {
      await next();
      return;
    }
    const usage: D1Usage = { calls: 0, ms: 0 };
    // 要求ごとに包み直し、ほかの要求と数を混ぜない。共有のenvは書き換えない。
    c.env = { ...c.env, DB: timedD1(binding, usage) };
    await next();
    setMetric(c, "d1", usage.ms, `${usage.calls} calls`);
  });
  return api;
}
