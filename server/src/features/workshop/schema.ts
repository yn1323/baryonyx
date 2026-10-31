import { z } from "zod";
import { SELL_LIMIT, WORKSHOP_CATEGORIES } from "./catalog.js";

// 1回の合成で送れる特別な材料（装備・スキル）の数の上限。今のレシピは1つまで。
const CRAFT_HELD_LIMIT = 5;

const requestId = z.string().regex(/^[a-f0-9-]{36}$/);
const entryId = z.string().min(1).max(64);

// 商会で、これまでに手に入れた物を1つ買う。値段はサーバーが決める。
export const buySchema = z.object({
  requestId,
  category: z.enum(WORKSHOP_CATEGORIES),
  id: entryId,
});

export type Buy = z.infer<typeof buySchema>;

// 商会で、レシピの材料をすべて使って1つ作る。費用と出来上がる物の倍率はサーバーが決める。
// アイテムの材料はレシピで決まるため送らない。装備・スキルの材料（特別な材料）は、使う持ち物の行のIDを重ねずに送る。
// 同じ種類の装備・スキルはレシピに1つまでのため、行のIDの数はレシピの特別な材料の数と同じになる。
// クライアント（JsonUtility）は空の配列も送るため、省略と空を受け付ける。
export const craftSchema = z.object({
  requestId,
  category: z.enum(WORKSHOP_CATEGORIES),
  id: entryId,
  materialIds: z
    .array(z.string().min(1).max(64))
    .max(CRAFT_HELD_LIMIT)
    .refine((ids) => new Set(ids).size === ids.length)
    .default([]),
});

export type Craft = z.infer<typeof craftSchema>;

const rowId = z.string().min(1).max(64);
const distinct = (ids: readonly string[]) => new Set(ids).size === ids.length;

// 商会で、持ち物を売る。スキルは持ち物の行のIDの cardIds、装備は equipmentIds、アイテムは種類ごとの持っている
// 数の items で、重ねずに合計1〜SELL_LIMIT個を選ぶ。値段はサーバーが決める。
// クライアント（JsonUtility）は使わない配列も空で送るため、どれも省略と空を受け付ける。
export const sellSchema = z
  .object({
    requestId,
    cardIds: z.array(rowId).max(SELL_LIMIT).refine(distinct).default([]),
    equipmentIds: z.array(rowId).max(SELL_LIMIT).refine(distinct).default([]),
    items: z
      .array(
        z.object({
          id: entryId,
          count: z.number().int().min(1).max(9999),
        }),
      )
      .max(SELL_LIMIT)
      .refine((items) => distinct(items.map((item) => item.id)))
      .default([]),
  })
  .refine((sale) => {
    const total =
      sale.cardIds.length + sale.equipmentIds.length + sale.items.length;
    return total >= 1 && total <= SELL_LIMIT;
  });

export type Sell = z.infer<typeof sellSchema>;
