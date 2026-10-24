import { createDatabase } from "../../shared/db.js";
import { createFormationOperations } from "./formation.js";
import { createGrowthOperations } from "./growth.js";
import { readState } from "./state.js";

// パーティの操作。編成（formation.ts）と成長（growth.ts）に分け、読み込みは state.ts の1回のbatchにまとめる。
// 初期データはログインしたときに付与する（equipment/starter.ts の grantStarters）。
export function createPartyRepository(binding: D1Database) {
  const db = createDatabase(binding);

  // 持っているキャラ・編成・カード・所持ルーン。
  async function getState(userId: string) {
    return readState(db, userId);
  }

  return {
    getState,
    ...createFormationOperations(db),
    ...createGrowthOperations(db),
  };
}
