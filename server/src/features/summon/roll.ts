import {
  pickQuality,
  type Quality,
  rollMultiplierPercent,
} from "../../shared/quality.js";
import { maxPercentOf, pickWeighted } from "../adventure/drops.js";
import {
  isEquipmentCategory,
  SUMMON_CATEGORIES,
  SUMMON_CATEGORY_PERMILLE,
  SUMMON_GUARANTEED_STARS,
  SUMMON_QUALITY_PERMILLE,
  SUMMON_STAR_PERMILLE,
  type SummonEntry,
} from "./catalog.js";

export type Random = () => number;

// 1回で出た物。装備だけが倍率（百分率）を持つ。
export type SummonDraw = SummonEntry & { multiplierPercent: number | null };

const guaranteed = (draw: SummonDraw) =>
  draw.category === "character" || draw.stars >= SUMMON_GUARANTEED_STARS;

// 分類→☆→物→品質の順に選ぶ。☆を先に割合で決め、その☆の物から均等に選ぶため、
// 画面に出す割合は一覧の中身によらない。その分類にない☆は除く。
// minStars が1より大きいときは、キャラを除き、その☆以上だけから選ぶ（10回の確定）。
function draw(
  pool: readonly SummonEntry[],
  random: Random,
  minStars: number,
): SummonDraw | undefined {
  const categories = SUMMON_CATEGORIES.filter((category) =>
    pool.some(
      (entry) =>
        entry.category === category &&
        (category === "character" ? minStars <= 1 : entry.stars >= minStars),
    ),
  );
  if (categories.length === 0) return undefined;
  const category = pickWeighted(
    categories,
    (choice) => SUMMON_CATEGORY_PERMILLE[choice],
    random,
  );
  let entries = pool.filter((entry) => entry.category === category);
  if (category !== "character") {
    const stars = [
      ...new Set(
        entries
          .map((entry) => entry.stars)
          .filter(
            (star) =>
              star >= minStars &&
              star >= 1 &&
              star <= SUMMON_STAR_PERMILLE.length,
          ),
      ),
    ];
    const star = pickWeighted(
      stars,
      (choice) => SUMMON_STAR_PERMILLE[choice - 1],
      random,
    );
    entries = entries.filter((entry) => entry.stars === star);
  }
  const chosen =
    entries[
      Math.min(Math.floor(random() * entries.length), entries.length - 1)
    ];
  let multiplierPercent: number | null = null;
  if (isEquipmentCategory(category)) {
    const max =
      maxPercentOf(category as "weapon" | "armor" | "accessory", chosen.id) ??
      100;
    const quality: Quality = pickQuality(SUMMON_QUALITY_PERMILLE, random);
    multiplierPercent = rollMultiplierPercent(quality, random, max);
  }
  return { ...chosen, multiplierPercent };
}

/** count 回引く。10回以上で☆3以上もキャラも出なければ、最後の1つを☆3以上から引き直す。 */
export function rollSummon(
  pool: readonly SummonEntry[],
  count: number,
  random: Random,
): SummonDraw[] {
  const draws: SummonDraw[] = [];
  for (let i = 0; i < count; i++) {
    const next = draw(pool, random, 1);
    if (next) draws.push(next);
  }
  if (count >= 10 && draws.length > 0 && !draws.some(guaranteed)) {
    const last = draw(pool, random, SUMMON_GUARANTEED_STARS);
    if (last) draws[draws.length - 1] = last;
  }
  return draws;
}
