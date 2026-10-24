import { and, eq, sql } from "drizzle-orm";
import {
  createDatabase,
  type Database,
  insertRowsWhen,
} from "../../shared/db.js";
import { MIN_MULTIPLIER_PERCENT } from "../../shared/quality.js";
import { discoveryStatements } from "../library/discoveries.js";
import { partyProfiles } from "../party/db-schema.js";
import { partyStarterStatements } from "../party/starter.js";
import {
  type EquipmentId,
  STARTER_EQUIPPED,
  STARTER_ITEMS,
  slotOf,
} from "./catalog.js";
import {
  equipmentCharacterSlots,
  equipmentItems,
  equipmentProfiles,
} from "./db-schema.js";

/**
 * 仮の装備と初めの4人の装備を付与する文。装備のIDは毎回作るため、同時に届いた要求で二重に付与しないよう、
 * 印を書けた要求（grant_id）だけが装備を入れる。付与の印がないときにだけ使う。
 * 行の多い表は1つの文にまとめる（D1とMiniflareは batch の文ごとに準備の手間がかかる）。
 */
function equipmentStarterStatements(db: Database, userId: string, now: string) {
  const grantId = crypto.randomUUID();
  const granted = sql`EXISTS (SELECT 1 FROM ${equipmentProfiles} WHERE ${and(
    eq(equipmentProfiles.userId, userId),
    eq(equipmentProfiles.grantId, grantId),
  )})`;
  const ids = STARTER_ITEMS.map(() => crypto.randomUUID());
  const itemOf = (equipmentId: EquipmentId) =>
    ids[STARTER_ITEMS.indexOf(equipmentId)];
  return [
    db
      .insert(equipmentProfiles)
      .values({ userId, grantId, createdAt: now })
      .onConflictDoNothing(),
    insertRowsWhen(
      db,
      equipmentItems,
      STARTER_ITEMS.map((equipmentId, index) => ({
        id: ids[index],
        userId,
        equipmentId,
        multiplierPercent: MIN_MULTIPLIER_PERCENT,
        acquiredAt: now,
      })),
      granted,
    ),
    // ライブラリの見つけた記録。二重に届いても初めの日時のまま残るため、付与の印を条件にしない。
    ...(["weapon", "armor"] as const).flatMap((slot) =>
      discoveryStatements(
        db,
        userId,
        slot,
        STARTER_ITEMS.filter((equipmentId) => slotOf(equipmentId) === slot),
        now,
      ),
    ),
    insertRowsWhen(
      db,
      equipmentCharacterSlots,
      STARTER_EQUIPPED.flatMap((entry) =>
        (["weapon", "armor"] as const).map((slot) => ({
          userId,
          characterId: entry.characterId,
          slot,
          itemId: itemOf(entry[slot]),
          updatedAt: now,
        })),
      ),
      granted,
    ),
  ];
}

/**
 * ログインしたユーザーに、編成と装備の初期データを1回だけ付与する（doc/rules/backend-design.md の認証とAPIの組み立て）。
 * 付与を済ませた印を1回の往復で読み、まだのものだけを1回の batch で付与する。キャラの装備は持っているキャラにしか
 * 付けられないため、編成を先に付与する。ログインのたびに呼ぶが、付与済みなら読むだけで終える。
 */
export async function grantStarters(
  binding: D1Database,
  userId: string,
  now: string,
) {
  const db = createDatabase(binding);
  const [party, equipment] = await db.batch([
    db
      .select({ userId: partyProfiles.userId })
      .from(partyProfiles)
      .where(eq(partyProfiles.userId, userId)),
    db
      .select({ userId: equipmentProfiles.userId })
      .from(equipmentProfiles)
      .where(eq(equipmentProfiles.userId, userId)),
  ]);
  const statements = [
    ...(party.length > 0 ? [] : partyStarterStatements(db, userId, now)),
    ...(equipment.length > 0
      ? []
      : equipmentStarterStatements(db, userId, now)),
  ];
  const [first, ...rest] = statements;
  if (first) await db.batch([first, ...rest]);
}
