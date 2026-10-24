import { z } from "zod";
import {
  CRAFT_MATERIAL_COUNT,
  SELL_LIMIT,
  WORKSHOP_CATEGORIES,
} from "./catalog.js";

const requestId = z.string().regex(/^[a-f0-9-]{36}$/);
const entryId = z.string().min(1).max(64);

// 商会で、これまでに手に入れた物を1つ買う。値段はサーバーが決める。
export const buySchema = z.object({
  requestId,
  category: z.enum(WORKSHOP_CATEGORIES),
  id: entryId,
});

export type Buy = z.infer<typeof buySchema>;

// 商会で、☆が1つ低い同じ分類の物を合計3つ使い、1つ作る。費用と出来上がる物の倍率はサーバーが決める。
// スキル・装備は持ち物の行のIDを重ねずに3つ、アイテムは種類ごとの数を、種類を重ねずに合計3個で選ぶ。
export const craftSchema = z.discriminatedUnion("category", [
  z.object({
    requestId,
    category: z.enum(WORKSHOP_CATEGORIES),
    id: entryId,
    materialIds: z
      .array(z.string().min(1).max(64))
      .length(CRAFT_MATERIAL_COUNT)
      .refine((ids) => new Set(ids).size === ids.length),
  }),
  z.object({
    requestId,
    category: z.literal("item"),
    id: entryId,
    items: z
      .array(
        z.object({
          id: entryId,
          count: z.number().int().min(1).max(CRAFT_MATERIAL_COUNT),
        }),
      )
      .min(1)
      .max(CRAFT_MATERIAL_COUNT)
      .refine(
        (items) =>
          new Set(items.map((item) => item.id)).size === items.length &&
          items.reduce((sum, item) => sum + item.count, 0) ===
            CRAFT_MATERIAL_COUNT,
      ),
  }),
]);

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
