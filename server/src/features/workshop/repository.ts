import { createDatabase } from "../../shared/db.js";
import { createBuyOperation } from "./buy.js";
import { createCraftOperation } from "./craft.js";
import { createSellOperation } from "./sell.js";

// 商会の操作。買う（buy.ts）・合成する（craft.ts）・売る（sell.ts）は互いに独立している。
export function createWorkshopRepository(binding: D1Database) {
  const db = createDatabase(binding);
  return {
    ...createBuyOperation(db),
    ...createCraftOperation(db),
    ...createSellOperation(db),
  };
}
