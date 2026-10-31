import { createDatabase } from "../../shared/db.js";
import { createAwakenOperation } from "./awaken.js";
import { createCombineOperation } from "./combine.js";
import { createPolishOperation } from "./polish.js";
import { createRecords } from "./records.js";

// 商会の強化。品質強化（polish.ts）・覚醒（awaken.ts）・磨き石の合成（combine.ts）に分け、記録・素材の読み書きは
// records.ts にまとめる。
export function createEnhancementRepository(binding: D1Database) {
  const db = createDatabase(binding);
  const records = createRecords(db);

  // 持っている強化素材。
  async function getState(userId: string) {
    return {
      materials: records.materialsOf(await records.materialsRead(userId)),
    };
  }

  return {
    getState,
    ...createPolishOperation(db, records),
    ...createAwakenOperation(db, records),
    ...createCombineOperation(db, records),
  };
}
