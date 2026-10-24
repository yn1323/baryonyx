import { createDatabase } from "../../shared/db.js";
import type { Random } from "../../shared/random.js";
import { createPurchaseOperations } from "./purchases.js";
import { createRoomOperations } from "./rooms.js";
import { createRunOperations } from "./run.js";
import { batchWithState } from "./state.js";

// 冒険の操作。始める・進む・終える（run.ts）、マスの出来事（rooms.ts）、ルーンを使う操作（purchases.ts）に分け、
// 読み込みは state.ts の1回のbatchにまとめる。
export function createAdventureRepository(
  binding: D1Database,
  random: Random = Math.random,
) {
  const db = createDatabase(binding);

  // 進行中の冒険・行き先ごとの記録・所持ルーン。
  async function getState(userId: string) {
    return (await batchWithState(db, userId, [])).state;
  }

  return {
    getState,
    ...createRunOperations(db, random),
    ...createRoomOperations(db, random),
    ...createPurchaseOperations(db),
  };
}
