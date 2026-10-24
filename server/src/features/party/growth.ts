import { and, eq, gte, inArray, isNull, or, sql } from "drizzle-orm";
import type { Database } from "../../shared/db.js";
import { adventureRuns } from "../adventure/db-schema.js";
import { runeRecord } from "../exercise-rewards/wallet.js";
import { BOND_MAX } from "../summon/catalog.js";
import { characterBonds } from "../summon/db-schema.js";
import {
  bitOf,
  type CharacterId,
  EVOLUTION_GRACE_MINUTES,
  isAwakenableSkill,
  levelUpCost,
  UNIQUE_UNLOCK_LEVELS,
  uniqueParent,
} from "./catalog.js";
import {
  partyCharacters,
  partyLevelUps,
  partyUniqueSkills,
  skillCards,
} from "./db-schema.js";
import type { LevelUp } from "./schema.js";
import { batchWithState, type PartyState, readState } from "./state.js";

type PartyUniqueChange = "set" | "none";
type PartyEvolveChange = "evolve" | "none";
type PartyAwakenChange = "awaken" | "none";
type PartyBondChange = "deepen" | "none";

type LevelUpRow = typeof partyLevelUps.$inferSelect;

// キャラの成長（レベル・固有スキルの段階・持っているスキルの覚醒・絆）を変える。
// 確かめる読み込みと、書き込みと書いたあとの読み込みの2回の往復で済ませる。
export function createGrowthOperations(db: Database) {
  const findLevelUp = (userId: string, requestId: string) =>
    db
      .select()
      .from(partyLevelUps)
      .where(
        and(
          eq(partyLevelUps.userId, userId),
          eq(partyLevelUps.requestId, requestId),
        ),
      );

  // 記録したレベルアップを返す。同じ要求IDで中身の違う要求は受け付けない。
  function recorded(
    characterId: CharacterId,
    request: LevelUp,
    row: LevelUpRow,
    state: PartyState,
  ) {
    if (
      row.characterId !== characterId ||
      row.fromLevel !== request.fromLevel ||
      row.toLevel !== request.toLevel
    ) {
      return { error: "request_conflict" as const };
    }
    const { characterId: id, fromLevel, toLevel, runes, balanceAfter } = row;
    return {
      levelUp: { characterId: id, fromLevel, toLevel, runes, balanceAfter },
      ...state,
    };
  }

  // ルーンを使ってキャラのレベルを上げる。費用はサーバーで計算し、残高と今のレベルを
  // 条件にした1回のbatchで、記録・ルーンの減算・レベルの更新をまとめて行う。
  async function levelUp(
    userId: string,
    characterId: CharacterId,
    request: LevelUp,
    now: string,
  ) {
    const first = await batchWithState(db, userId, [
      findLevelUp(userId, request.requestId),
    ]);
    const replay = first.results[0][0];
    if (replay) return recorded(characterId, request, replay, first.state);
    if (
      !first.state.characters.some((character) => character.id === characterId)
    ) {
      return { error: "character_not_owned" as const };
    }

    const runes = levelUpCost(request.fromLevel, request.toLevel);
    const pay = runeRecord(db, userId, partyLevelUps, now);
    const after = await batchWithState(db, userId, [
      db
        .insert(partyLevelUps)
        .select(
          db
            .select({
              id: sql<string>`${pay.id}`.as("id"),
              userId: partyCharacters.userId,
              requestId: sql<string>`${request.requestId}`.as("request_id"),
              characterId: partyCharacters.characterId,
              fromLevel: partyCharacters.level,
              toLevel: sql<number>`${request.toLevel}`.as("to_level"),
              runes: sql<number>`${runes}`.as("runes"),
              balanceAfter: sql<number>`0`.as("balance_after"),
              createdAt: sql<string>`${now}`.as("created_at"),
            })
            .from(partyCharacters)
            .where(
              and(
                eq(partyCharacters.userId, userId),
                eq(partyCharacters.characterId, characterId),
                eq(partyCharacters.level, request.fromLevel),
                pay.affords(runes),
              ),
            ),
        )
        .onConflictDoNothing({
          target: [partyLevelUps.userId, partyLevelUps.requestId],
        }),
      pay.debit(runes),
      db
        .update(partyCharacters)
        .set({ level: request.toLevel, updatedAt: now })
        .where(
          and(
            eq(partyCharacters.userId, userId),
            eq(partyCharacters.characterId, characterId),
            pay.written,
          ),
        ),
      pay.settle,
      findLevelUp(userId, request.requestId),
    ]);
    const row = (after.results.at(-1) as LevelUpRow[])[0];
    if (row) return recorded(characterId, request, row, after.state);
    // 書き込めなかった理由を、今のレベルで見分ける。
    const level = after.state.characters.find(
      (character) => character.id === characterId,
    )?.level;
    return {
      error:
        level !== request.fromLevel
          ? ("level_changed" as const)
          : ("insufficient_runes" as const),
    };
  }

  // 固有スキルを確かめる。持っていないキャラと、解放するLvに届いていない固有スキルは扱わない。
  function findUnique(
    state: PartyState,
    characterId: CharacterId,
    index: number,
  ) {
    const character = state.characters.find(
      (entry) => entry.id === characterId,
    );
    if (!character) return { error: "character_not_owned" as const };
    if (character.level < UNIQUE_UNLOCK_LEVELS[index]) {
      return { error: "unique_locked" as const };
    }
    return { unique: character.uniques[index] };
  }

  const uniqueOf = (state: PartyState, characterId: string, index: number) =>
    state.characters.find((entry) => entry.id === characterId)?.uniques[index];

  // 編成で、固有スキルに取得した段階のどれか1つをセットする。手前の段階に戻すのもこの操作で行う。
  async function setUnique(
    userId: string,
    characterId: CharacterId,
    index: number,
    stage: number,
    now: string,
  ) {
    const state = await readState(db, userId);
    const found = findUnique(state, characterId, index);
    if ("error" in found) return found;
    if (!found.unique.acquired.includes(stage)) {
      return { error: "stage_not_acquired" as const };
    }
    if (found.unique.set === stage) {
      return { change: "none" as const, ...state };
    }
    // 最初の段階以外を取得していれば行がある。取得の確認を更新と同じ文に入れる。
    const { state: after } = await batchWithState(db, userId, [
      db
        .update(partyUniqueSkills)
        .set({ setStage: stage, updatedAt: now })
        .where(
          and(
            eq(partyUniqueSkills.userId, userId),
            eq(partyUniqueSkills.characterId, characterId),
            eq(partyUniqueSkills.uniqueIndex, index),
            sql`(${partyUniqueSkills.acquiredMask} & ${bitOf(stage)}) <> 0`,
          ),
        ),
    ]);
    if (uniqueOf(after, characterId, index)?.set !== stage) {
      return { error: "stage_not_acquired" as const };
    }
    const change: PartyUniqueChange = "set";
    return { change, ...after };
  }

  // 冒険の途中か、踏破・敗北で終えた直後かを読む文。冒険の戦闘で起きた進化と覚醒は、このあいだだけ受け付ける
  // （そのマスの戦闘の進化が、冒険を終えた知らせと前後して届く）。途中でやめた冒険の戦闘の進化は残さない。
  const inAdventure = (userId: string, now: string) =>
    db
      .select({ id: adventureRuns.id })
      .from(adventureRuns)
      .where(
        and(
          eq(adventureRuns.userId, userId),
          or(
            eq(adventureRuns.status, "active"),
            and(
              inArray(adventureRuns.status, ["cleared", "defeated"]),
              gte(
                adventureRuns.endedAt,
                new Date(
                  Date.parse(now) - EVOLUTION_GRACE_MINUTES * 60_000,
                ).toISOString(),
              ),
            ),
          ),
        ),
      )
      .limit(1);

  // 冒険の戦闘で起きた1回の進化を保存する。進む先は、セットしている段階の次の段階だけで、
  // 進化した段階を取得し、そのままセットする。取得済みの段階へ進むのも進化として扱う。
  // 同じ進化の再送（すでにその段階をセットしている）は、何も変えずに成功として返す。
  async function evolveUnique(
    userId: string,
    characterId: CharacterId,
    index: number,
    stage: number,
    now: string,
  ) {
    const first = await batchWithState(db, userId, [inAdventure(userId, now)]);
    const found = findUnique(first.state, characterId, index);
    if ("error" in found) return found;
    if (found.unique.set === stage) {
      return { change: "none" as const, ...first.state };
    }
    const parent = uniqueParent(stage);
    if (parent !== found.unique.set) {
      return { error: "not_next_stage" as const };
    }
    if (first.results[0].length === 0) {
      return { error: "not_in_adventure" as const };
    }
    const target = and(
      eq(partyUniqueSkills.userId, userId),
      eq(partyUniqueSkills.characterId, characterId),
      eq(partyUniqueSkills.uniqueIndex, index),
    );
    // 確認と書き込みの間に別の進化やセットが割り込んでも、手前の段階をセットしていることと、
    // 解放するLvに届いていることを更新と同じ文に入れる。
    const { state: after } = await batchWithState(db, userId, [
      db
        .insert(partyUniqueSkills)
        .values({
          userId,
          characterId,
          uniqueIndex: index,
          setStage: 0,
          acquiredMask: bitOf(0),
          updatedAt: now,
        })
        .onConflictDoNothing(),
      db
        .update(partyUniqueSkills)
        .set({
          setStage: stage,
          acquiredMask: sql`${partyUniqueSkills.acquiredMask} | ${bitOf(stage)}`,
          updatedAt: now,
        })
        .where(
          and(
            target,
            eq(partyUniqueSkills.setStage, parent),
            sql`EXISTS (SELECT 1 FROM ${partyCharacters} WHERE ${partyCharacters.userId} = ${userId} AND ${partyCharacters.characterId} = ${characterId} AND ${partyCharacters.level} >= ${UNIQUE_UNLOCK_LEVELS[index]})`,
          ),
        ),
    ]);
    if (uniqueOf(after, characterId, index)?.set !== stage) {
      return { error: "not_next_stage" as const };
    }
    const change: PartyEvolveChange = "evolve";
    return { change, ...after };
  }

  // 冒険の戦闘で起きた、持っているスキル1枚の覚醒を保存する（doc/features/skill-awakening.md の保存）。
  // 覚醒は1枚に1回だけで、元に戻らない。同じ枝の再送は、何も変えずに成功として返す。別の枝へは覚醒しない。
  // 固有スキルの進化と同じく、冒険の途中か、踏破・敗北で終えた直後だけ受け付ける。
  async function awakenSkillCard(
    userId: string,
    cardId: string,
    branch: number,
    now: string,
  ) {
    const first = await batchWithState(db, userId, [inAdventure(userId, now)]);
    const card = first.state.skillCards.find((entry) => entry.id === cardId);
    if (!card) return { error: "card_not_owned" as const };
    if (card.awakenedBranch === branch) {
      return { change: "none" as const, ...first.state };
    }
    if (card.awakenedBranch !== null) {
      return { error: "already_awakened" as const };
    }
    if (!isAwakenableSkill(card.skillId)) {
      return { error: "not_awakenable" as const };
    }
    if (first.results[0].length === 0) {
      return { error: "not_in_adventure" as const };
    }
    // 確認と書き込みの間に別の覚醒が割り込んでも、まだ覚醒していないことを更新と同じ文に入れる。
    const { state: after } = await batchWithState(db, userId, [
      db
        .update(skillCards)
        .set({ awakenedBranch: branch })
        .where(
          and(
            eq(skillCards.userId, userId),
            eq(skillCards.id, cardId),
            isNull(skillCards.awakenedBranch),
          ),
        ),
    ]);
    const awakened = after.skillCards.find((entry) => entry.id === cardId);
    if (!awakened) return { error: "card_not_owned" as const };
    if (awakened.awakenedBranch !== branch) {
      return { error: "already_awakened" as const };
    }
    const change: PartyAwakenChange = "awaken";
    return { change, ...after };
  }

  // 絆のしるしを1つ使って、絆を1段深める（doc/features/monetization.md のキャラが被ったとき）。
  // bond は深めたあとの段で、今の段の次だけを受け付ける。同じ要求の再送（すでにその段）は、何も変えずに成功として返す。
  async function deepenBond(
    userId: string,
    characterId: CharacterId,
    bond: number,
    now: string,
  ) {
    const state = await readState(db, userId);
    const character = state.characters.find(
      (entry) => entry.id === characterId,
    );
    if (!character) return { error: "character_not_owned" as const };
    if (character.bond === bond) return { change: "none" as const, ...state };
    if (character.bond !== bond - 1 || bond > BOND_MAX) {
      return { error: "not_next_bond" as const };
    }
    if (character.tokens < 1) return { error: "no_bond_token" as const };
    // 確認と書き込みの間に別の深める操作が割り込んでも、今の段としるしの数を更新と同じ文に入れる。
    const { state: after } = await batchWithState(db, userId, [
      db
        .update(characterBonds)
        .set({
          bond: sql`${characterBonds.bond} + 1`,
          tokens: sql`${characterBonds.tokens} - 1`,
          updatedAt: now,
        })
        .where(
          and(
            eq(characterBonds.userId, userId),
            eq(characterBonds.characterId, characterId),
            eq(characterBonds.bond, bond - 1),
            gte(characterBonds.tokens, 1),
          ),
        ),
    ]);
    if (
      after.characters.find((entry) => entry.id === characterId)?.bond !== bond
    ) {
      return { error: "not_next_bond" as const };
    }
    const change: PartyBondChange = "deepen";
    return { change, ...after };
  }

  return { levelUp, setUnique, evolveUnique, awakenSkillCard, deepenBond };
}
