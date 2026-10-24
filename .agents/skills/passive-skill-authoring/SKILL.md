---
name: passive-skill-authoring
description: >
  キャラのパッシブスキルを考える・足す・直す・レビューするときに必ず使う。
  新しいキャラにパッシブを付ける、パッシブの効果・数値・説明文・解放するレベルを決める、
  「パッシブがつまらない・どれも同じ・強すぎる」という相談、育成の画面の仮データのパッシブを差し替える作業にも当てはめる。
  カードスキル・固有スキルの効果と説明文は card-skill-authoring、アクセサリーの効果だけの作業には使わない。
---

# パッシブスキルを考える

パッシブの決まりと方針は [パッシブスキル](../../../doc/features/passives.md) を正本にする。
このスキルは、パッシブを考えるときの進め方と、見直すときの問いだけを持つ。
戦闘の決まりは [戦闘システム](../../../doc/features/combat.md) に従う。

## 進め方

1. 材料を読む。[設計方針](../../../doc/features/passives.md#設計方針)、[仮データの9人のパッシブ](../../../doc/features/passives.md#仮データの9人のパッシブ)の「仕掛け」の列、[戦闘の長さ](../../../doc/features/combat.md#戦闘の長さ)、[カードの状態の仮設定](../../../doc/features/combat.md#カードの状態の仮設定)、そのキャラの固有スキルの段階（[UniqueSkillTrees](../../../client/Assets/Baryonyx/Features/SkillEvolution/Runtime/UniqueSkillTrees.cs)）と付けているカードを読む。
2. キャラの見立てを3行で書く（[キャラの見立てから作る](../../../doc/features/passives.md#キャラの見立てから作る)）。設定がないキャラは、ステータスの役割の見立てから書き、候補として扱う。
3. 1つ目と2つ目の役割を分けて案を出す（[2つのパッシブの分担](../../../doc/features/passives.md#2つのパッシブの分担)）。1つにつき2〜3案を出し、仕掛け（きっかけと働きの組）が既存の18個と重ならないものを残す。
4. 値打ちを見積もる（[値打ちを見積もる](../../../doc/features/passives.md#値打ちを見積もる)）。通常の戦闘・強敵・最奥のボスで何回働くかを数え、表に書く。働く回数が0になる場面と、遊び進めると働かなくなる条件がないかを確かめる。
5. 説明文を書く。カードと同じ決まりで、[card-skill-authoring](../card-skill-authoring/SKILL.md) の説明文の決まりに従う。読んで「初めての人の得」と「慣れた人の工夫」を1行ずつ言えるかを確かめる。
6. 記録する。[パッシブの定義](../../../doc/catalog/passives/README.md)に1個1ファイルで書き、[仮データの9人のパッシブ](../../../doc/features/passives.md#仮データの9人のパッシブ)の表と、つながりがあれば[パッシブ同士のつながり](../../../doc/features/passives.md#パッシブ同士のつながり)に足す。`python -B doc/tools/check_docs.py --write-index` で索引を作り直し、検査を通す（[文書の索引生成と検査](../../../doc/tools/README.md)）。
7. 提案するときは、ユーザーに案を表で見せる。キャラ・解放するレベル・説明文・仕掛け・強い場面・初めての人と慣れた人の受け取り方を並べ、採用を確かめてから実装へ進む。

ゲーム開発の作業を提案するときの決まり（Web検索と一次情報）は [AGENTS.md](../../../AGENTS.md#ゲーム開発の進め方と提案) に従う。
パッシブの設計で参考にした資料は、[設計方針](../../../doc/features/passives.md#設計方針)の各節にリンクがある。

## 実装するとき

定義・戦闘での働き・育成の画面・テストの場所は [実装との対応](../../../doc/features/passives.md#実装との対応) にある。説明文だけで決まらない点は [実装での仮の扱い](../../../doc/features/passives.md#実装での仮の扱い) に足す。

1. 定義と数値を足す。新しい種類は、きっかけごとの呼ぶ所（手札・ターン・カード・敵の攻撃）のどこで働くかを先に決める。
2. アイコンを描く。[pixel-art-standards](../pixel-art-standards/SKILL.md) に従い、[prompts.json](../../../client/ArtSource/UI/Passives/prompts.json) の共通の指示文に図案を足して描き、24×24ドットの `.aseprite` にして指示文を記録する。
3. 育成の画面の仮データとPrefabを、メニューの `Baryonyx > Tavern > Create Screen Assets` で作り直す。パッシブの名前はライブラリにも出るため `Baryonyx > Library > Create Screen Assets` も作り直し、[展示室](../../../doc/features/showcase.md)に載ったことを確かめる。
4. `CombatPassivesTests`（EditMode）と、`BattlePassivesSceneTests` に働き方のテストを足して実行する（[UnityのテストとCI](../../../doc/rules/client-testing.md)）。

- C#は [unity-csharp-differences](../unity-csharp-differences/SKILL.md)。
- 働いたときの名前の出し方や演出は [ui-advisor](../ui-advisor/SKILL.md)・[vfx-authoring](../vfx-authoring/SKILL.md)。

## 見直すときの問い

- ステータスをそのまま上げるだけの効果になっていないか。それはアクセサリーの役目ではないか。
- 名前を隠して効果だけを見たとき、どのキャラのものか分かるか。
- 既存のパッシブと、きっかけと働きの組が同じになっていないか。
- 2つのうち少なくとも1つが、ほかの味方のカード・状態・HP、または共有のエネルギー・手札に触れているか。
- 通常の戦闘・強敵・最奥のボスのどこで強く、どこで弱いかを言えるか。どこでも最強になっていないか。
- カードに使う条件を付けたり、手札を詰まらせたりしていないか。
- 倒れたとき・復活したとき・全滅からの再開のときの扱いを書いたか。
- 働いたことが画面で分かるか。
- 説明文は1つ目なら1行、2つ目でも2行に収まっているか。
