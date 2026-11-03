---
name: card-skill-authoring
description: >
  戦闘のカードスキル（CardSkills.cs のカード、CardSkillAwakenings.cs のカスタムスキルの覚醒版、UniqueSkillTrees.cs の固有スキルとその覚醒段階、試作のカード）を足す・変える・レビューするとき、
  カードの説明文・効果・威力の割合・キーワードを書く・直すときに必ず使う。
  「新しいカードを足したい」「カードの文言が長い・分かりにくい」「説明文に何を書くか」という相談にも当てはめる。
  カードの挿絵は pixel-art-standards、演出は vfx-authoring、強さの調べ直しは battle-balance の担当で、カードを扱わない戦闘画面のUIだけの作業には使わない。
---

# カードスキルを書く

カードの決まりは [戦闘システム](../../../doc/features/combat.md) を正本にする。
このスキルは、カードを足す・直すときの進め方と、確かめる場所だけを持つ。

## 説明文の決まり

説明文に書く項目と言い回し、欄に収める行数は [カードの説明文に書くこと](../../../doc/features/combat/cards.md#カードの説明文に書くこと) に従う。
威力の割合の書き方と、定義の `Text` に置く `{0}`・`{1}` は [カードの数値の見せ方](../../../doc/features/combat/cards.md#カードの数値の見せ方)、角かっこで囲む語は [カードのキーワード](../../../doc/features/combat/cards.md#カードのキーワード) に従う。

## 進め方

1. [威力の決め方](../../../doc/features/combat/rules.md#威力の決め方) と [スキルの仮設定](../../../doc/features/combat/cards.md#スキルの仮設定)（重複を避ける基準）を読み、効果を決める。
2. [CardSkills](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CardSkills.cs)（固有スキルの覚醒の段階は [UniqueSkillTrees](../../../client/Assets/Baryonyx/Features/SkillEvolution/Runtime/UniqueSkillTrees.cs)）に定義を書き、上の決まりで説明文を書く。カスタムスキルの覚醒版の2つの枝は [CardSkillAwakenings](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CardSkillAwakenings.cs) に書く（[カスタムスキルの覚醒](../../../doc/features/skill-awakening.md)）。カードを足す・消すときは、下の[一緒に直す所](#カードを足す消すときに一緒に直す所)の表も当てる。今の働き（`CardAction`）で書けない、そのカードだけの効果（「このカードで敵を倒したら1枚引く」など）は、新しい欄を足さずに `CardSkill.Effects` に効果の部品で書く。使っている1枚の中で起きたことにだけ働く（[戦闘の効果の部品](../../../doc/plans/2026-11-03-battle-effect-system.md#効果の部品)）。
3. 1枚ごとの記録 [スキルの定義](../../../doc/catalog/skills/README.md) の「説明文」の行に定義の説明文を、「対象の役割・説明」の行に種類（「攻撃のカード。」など）と、ステータスと割合（「属攻の90%」）を〇にした同じ文を書く（例：[ブリザード](../../../doc/catalog/skills/skill-blizzard.md)）。覚醒版の枝は、同じ文書の[覚醒版](../../../doc/catalog/skills/README.md#覚醒版)の節に書く。
4. 説明文はPrefabに焼き込まれるため、メニューの `Baryonyx > Combat > Create Battle Inspect Assets`（戦闘画面のモック）、`Baryonyx > Tavern > Create Screen Assets`（酒場の付け替え・育成）、`Baryonyx > Library > Create Screen Assets`（ライブラリ）で生成し直し、[展示室](../../../doc/features/showcase.md)で変わった文を確かめる。
5. `CardSkillsTests`・`CardSkillAwakeningsTests`（EditMode）と、カードを出す画面のPlayModeテストを実行する（[UnityのテストとCI](../../../doc/rules/client-testing.md)）。
6. 効果・コスト・割合を変えたら、[戦闘の自動対戦](../../../doc/rules/client-testing.md#戦闘の自動対戦)で勝率とターン数を測り直す（ルートの AGENTS.md の[検証と報告](../../../AGENTS.md#検証と報告)）。原因を調べて直し方の案を出すのは、手動の battle-balance である。

## カードを足す・消すときに一緒に直す所

カスタムスキルのカードを足す・消す・IDを変えるときは、次の表の行ごとに直す所があるかを確かめ、作業の一覧にする。当てはまらない行は、理由を一言添えて外す。
固有スキルの段階だけを変えるときは、定義・記録・テストの行だけを当てる。

| 直す所 | 中身 |
|---|---|
| 定義 | [CardSkills](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CardSkills.cs) の `All`（ID・名前・使い手・属性・種類・対象・コスト・倍率の上限・説明文・効果） |
| 覚醒版 | [CardSkillAwakenings](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CardSkillAwakenings.cs) の2つの枝（`<ID>.1`・`<ID>.2`）。`CardSkillAwakeningsTests` が `CardSkills.All` のすべてに求める |
| 挿絵 | `Features/Combat/UI/Art/` の `Card<ID>.aseprite` と、覚醒版の `Card<ID>1`・`Card<ID>2`（[pixel-art-standards](../pixel-art-standards/SKILL.md)）。[展示室](../../../doc/features/showcase.md)への登録 |
| 演出 | [BattleSkillVfx.Cards](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleSkillVfx.Cards.cs) のカードごとの演出と属性の色、覚醒版は [BattleSkillVfx.Awakened](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleSkillVfx.Awakened.cs)（[vfx-authoring](../vfx-authoring/SKILL.md)） |
| サーバーのIDと上限 | [party/catalog.ts](../../../server/src/features/party/catalog.ts) の `CARD_SKILL_IDS` と、`CardSkills` の倍率の上限と同じ値にする `CARD_SKILL_MAX_PERCENT` |
| ドロップと☆ | サーバーの [adventure/drops.ts](../../../server/src/features/adventure/drops.ts) とクライアントの [AdventureDrops](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/AdventureDrops.cs) の両方の行き先の一覧に、同じ `skill("<ID>", ☆)` を置く（[冒険のドロップ](../../../doc/features/drops.md)）。☆は商会の値段も決める。`AdventureDropsTests`・`WorkshopGoodsTests` とサーバーの workshop のテストが、すべてのカードに☆と値段があることを確かめる |
| 合成のレシピ | ☆2以上なら、サーバーの [workshop/recipes.ts](../../../server/src/features/workshop/recipes.ts) とクライアントの [WorkshopRecipes](../../../client/Assets/Baryonyx/Features/Workshop/Runtime/WorkshopRecipes.cs) の両方（[レシピ](../../../doc/features/synthesis.md#レシピ)） |
| 記録 | [スキルの定義](../../../doc/catalog/skills/README.md) に1枚1ファイル（覚醒版の節と合成の材料の行を含む）、[スキルの仮設定](../../../doc/features/combat/cards.md#スキルの仮設定)の枚数・属性・種類の数。`python -B doc/tools/check_docs.py --write-index` で索引を作り直す |
| テスト | `CardSkillsTests`・`CardSkillAwakeningsTests`・`AdventureDropsTests`・`WorkshopGoodsTests`・`WorkshopCraftTests`（EditMode）と、`server/` の `pnpm test` |

## 見直すときの問い

- 演出の言葉を消しても、対象・量・回数・状態・条件が読み取れるか。
- 何度も当たる攻撃を「ダメージ×回数」で書いているか。
- 同じ効果のカードが、同じ言い回しになっているか。
- 1枚にしかない効果をキーワードにしていないか。
