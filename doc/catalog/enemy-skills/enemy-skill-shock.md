---
id: enemy-skill-shock
type: definition
status: 候補
updated: 2026-11-02
name: 電撃
category: enemy-skills
---

# 電撃

[敵の技の定義](README.md) / [関連仕様](../../features/combat-enemy-behavior.md)

共通技。体から稲妻を放ち、味方1人をしびれさせる魔法の攻撃である。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-04、ユーザーの依頼（共通化できる技も入れる）で、今の3体では使わない共通技として作った |
| 役割・説明 | 単体への魔法攻撃と麻痺 |
| 関連する仕様・個別ID | 使う敵：[ツチボタル](../enemies/enemy-glow-worm.md)、[砂時計の精](../enemies/enemy-hourglass-sprite.md)。[敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
| 実装・ゲームマスタの場所 | [CombatEnemyBehaviors](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatEnemyBehaviors.cs) の、使う敵の `Moves`。演出は、名前から選ぶ種類ごとの形（[BattleSkillVfx.Enemies](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleSkillVfx.Enemies.cs) の `EnemyBlowOf`） |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 共通技 | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 攻撃・味方単体 | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | 魔法・雷 | [記入する項目](README.md#記入する項目) | 候補 |
| 効果 | 属攻の100%のダメージ。麻痺1ターン | [記入する項目](README.md#記入する項目) | 仮 |
| 予告 | 攻撃・弱体 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [ツチボタル](../enemies/enemy-glow-worm.md)、[砂時計の精](../enemies/enemy-hourglass-sprite.md) | 各敵の定義の「技」の表 | 候補 |
| 似合う敵 | 雷をまとう獣、魔法を使う敵 | 新しい敵へ付けるときの目安 | 候補 |

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | 鋭い、しびれる |
| 本体の動き | 体を震わせ、縁に青白い光を走らせる |
| 溜め | 体の上に小さな火花が集まる（短い） |
| 発生 | 敵から標的へ、折れ曲がった稲妻の線が1〜2コマで走り、標的が白く光る |
| 余韻 | 標的の周りで、小さな火花が数回はぜる |
| 色 | 白、青白、黄 |
| 格 | 小 |

使う敵ごとの見た目の違いは、次のとおりである。

| 敵 | 色・材質・形の違い |
|---|---|
| [ツチボタル](../enemies/enemy-glow-worm.md) | 稲妻を青緑にし、背中の玉が順に灯ってから先頭の玉から放たれる形にする |
| [砂時計の精](../enemies/enemy-hourglass-sprite.md) | ガラスの中の青い砂が光り、金の枠の青い宝石から青白い雷が走る。 |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat/implementation.md#未決事項)の数値が決まったら見直す |
| 実装 | 戦闘で使う。効果のうち戦闘にまだないもの（味方に付く状態など）は写していない（[敵の行動の仮設定](../../features/combat-enemy-behavior.md)）。写すときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-04 | 共通技として定義を作った | ユーザーの依頼 | 使う敵：なし（今の敵は使わない） |
| 2026-10-19 | 使う敵に、新しく作った敵を加えた | ユーザーの依頼（敵を50体に増やし、文書カタログ・クライアント・サーバーにそろえる） | 使う敵：[ツチボタル](../enemies/enemy-glow-worm.md)、[砂時計の精](../enemies/enemy-hourglass-sprite.md) |
| 2026-11-02 | スキルとdocの整理で、今のコードに合わせて「実装・ゲームマスタの場所」を直した | 根拠：CombatEnemyBehaviors の `Moves` にこの技があり、戦闘で使う | [敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
