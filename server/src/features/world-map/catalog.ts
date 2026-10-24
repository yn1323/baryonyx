// 街道の世界地図（doc/features/stage-progression.md#街道とダンジョン）。
// マスと道の形はクライアントの WorldMapCatalog が持ち、サーバーは進み具合と宝箱の中身だけを預かる。

import type { Quality } from "../../shared/quality.js";
import type { MaterialId } from "../enhancement/catalog.js";
import type { EquipmentId } from "../equipment/catalog.js";
import type { CardSkillId } from "../party/catalog.js";

// 最初の街。初めて開くユーザーは、ここをクリアした状態でここに立つ。
export const START_SQUARE = "porta";

// マスのID：街は英小文字の名前（porta など）、街道のマスは方眼の位置（n12-4 など）。
export const SQUARE_ID_PATTERN = /^(?:n\d{1,2}-\d{1,2}|[a-z]{2,20})$/;

// 宝箱の1つの中身。装備とスキルは品質を決めて入れ、倍率はその品質の幅の下端にする。
// 強化素材は1つずつ並べる（2つなら2行）。
export type ChestEntry =
  | {
      category: "weapon" | "armor" | "accessory";
      id: EquipmentId;
      quality: Quality;
    }
  | { category: "skill"; id: CardSkillId; quality: Quality }
  | { category: "material"; id: MaterialId };

// 街道の宝箱のマスごとの中身（仮。doc/features/stage-progression.md の街道の宝箱）。
// 抽選せず、マスを初めてクリアしたときに1回だけもらう。その先の区間で役立つ物を置く。
// 中身はクライアントの WorldMapChests と同じにする。
export const CHESTS: Record<string, readonly ChestEntry[]> = {
  // ピーノの西の寄り道。北の氷室村フレッダ（推奨Lv18）へ向かう前に。
  "n8-5": [
    { category: "weapon", id: "frost-wand", quality: "A" },
    { category: "material", id: "polish-blue" },
    { category: "material", id: "polish-blue" },
  ],
  // 内の輪の東の寄り道。鐘の町カンパネラ（推奨Lv30）へ向かう前に。
  "n17-7": [
    { category: "accessory", id: "crystal-ring", quality: "A" },
    { category: "material", id: "polish-purple" },
  ],
  // サッビアの北の寄り道。南の砂の町へ向かう前に。
  "n15-15": [
    { category: "armor", id: "clay-armor", quality: "A" },
    { category: "material", id: "polish-blue" },
    { category: "material", id: "polish-blue" },
  ],
  // ドゥーナの北の寄り道。
  "n20-14": [
    { category: "weapon", id: "tomb-khopesh", quality: "A" },
    { category: "material", id: "polish-purple" },
  ],
  // サッビアの南の寄り道。
  "n16-18": [
    { category: "skill", id: "HolyLight", quality: "A" },
    { category: "material", id: "awakening-shard" },
  ],
};

export function chestOf(squareId: string): readonly ChestEntry[] | undefined {
  return CHESTS[squareId];
}
