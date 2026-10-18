---
name: card-skill-authoring
description: >
  戦闘のカードスキル（CardSkills.cs のカード、UniqueSkillTrees.cs の固有スキルとその進化段階、試作のカード）を追加・変更・レビューするとき、
  カードの説明文・効果・威力の割合・キーワードを書く・直すときに必ず使う。
  カードの文言が長い・分かりにくい、説明文に何を書くか、という相談にも当てはめる。
  カードの挿絵は pixel-art-standards、演出は vfx-authoring の担当で、カードを扱わない戦闘画面のUIだけの作業には使わない。
---

# カードスキルを書く

カードの決まりは [戦闘システム](../../../doc/features/combat.md) を正本にする。
このスキルは、カードを足す・直すときの進め方と、確かめる場所だけを持つ。

## 説明文の決まり

説明文には、実際の戦闘に関わる情報だけを書く。
「炎の玉で」「氷の槍で貫き」「雷を落とし」のような、技の見た目や演出を表す言葉は書かない。
何度も当たる攻撃は、「ダメージを5回」ではなく「ダメージ×5」と書く。
書く項目と言い回しの正本は [カードの説明文に書くこと](../../../doc/features/combat.md#カードの説明文に書くこと) である。

威力は、使い手のステータスやACTを入れずに、ステータスの割合（「属攻の100%」）で出す（[カードの数値の見せ方](../../../doc/features/combat.md#カードの数値の見せ方)）。
定義の `Text` には数を書かず、威力のある効果の順に `{0}`・`{1}` を置く。割合は `CardRules.Describe` が入れる。
状態や条件の名前は、複数のカードで使うものだけ `[凍え]` のように角かっこで囲む（[カードのキーワード](../../../doc/features/combat.md#カードのキーワード)）。

## 進め方

1. [威力の決め方](../../../doc/features/combat.md#威力の決め方) と [スキルの仮設定](../../../doc/features/combat.md#スキルの仮設定)（重複を避ける基準）を読み、効果を決める。
2. [CardSkills](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CardSkills.cs)（固有スキルの進化の段階は [UniqueSkillTrees](../../../client/Assets/Baryonyx/Features/SkillEvolution/Runtime/UniqueSkillTrees.cs)）に定義を書き、上の決まりで説明文を書く。説明文の欄は4行に収める。
3. 1枚ごとの記録 [技・カードの定義](../../../doc/catalog/skills/README.md) の「対象の役割・説明」と「説明文」の行を同じ文にそろえる。
4. 説明文はPrefabに焼き込まれるため、メニューの `Baryonyx > Combat > Create Battle Inspect Assets`（戦闘画面のモック）と `Baryonyx > Tavern > Create Screen Assets`（酒場の付け替え・育成）で生成し直し、[展示室](../../../doc/features/showcase.md)で変わった文を確かめる。
5. `CardSkillsTests`（EditMode）と、カードを出す画面のPlayModeテストを実行する（[UnityのテストとCI](../../../doc/rules/client-testing.md)）。

## 見直すときの問い

- 演出の言葉を消しても、対象・量・回数・状態・条件が読み取れるか。
- 何度も当たる攻撃を「ダメージ×回数」で書いているか。
- 同じ効果のカードが、同じ言い回しになっているか。
- 1枚にしかない効果をキーワードにしていないか。
