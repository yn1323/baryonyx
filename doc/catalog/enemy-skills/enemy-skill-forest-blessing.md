---
id: enemy-skill-forest-blessing
type: definition
status: 候補
updated: 2026-11-02
name: 森の恵み
category: enemy-skills
---

# 森の恵み

[敵の技の定義](README.md) / [関連仕様](../../features/combat-enemy-behavior.md)

森の守り手の固有技。根を通して、森の命を仲間に分け与える。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-04、ユーザーの依頼（敵の技を増やす。その敵が持っていそうな技にする）で、[森の守り手](../enemies/enemy-forest-guardian.md)の技として作った |
| 役割・説明 | 自分以外の敵の回復 |
| 関連する仕様・個別ID | 使う敵：[森の守り手](../enemies/enemy-forest-guardian.md)。[敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
| 実装・ゲームマスタの場所 | [CombatEnemyBehaviors](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatEnemyBehaviors.cs) の、使う敵の `Moves`。攻撃しない技のため、演出は技の名前と効果の文字・数字だけ（[BattleInspectView.EnemyMoves](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.EnemyMoves.cs) の `Support`） |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 固有技（[森の守り手](../enemies/enemy-forest-guardian.md)） | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 回復・自分以外の敵全体 | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | ダメージなし | [記入する項目](README.md#記入する項目) | 候補 |
| 効果 | 属攻の100%のHPを回復。リジェネ2ターン（敵のターンの開始時に、使った敵の属攻の20%を回復） | [記入する項目](README.md#記入する項目) | 仮 |
| 予告 | 回復 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [森の守り手](../enemies/enemy-forest-guardian.md) | 各敵の定義の「技」の表 | 候補 |

自分を回復しないのは、[ボスの歯ごたえ](../../features/combat/rules.md#ボスの歯ごたえ)の「HPを増やすだけで戦闘を長くしない」に合わせたためである。仲間を先に倒すか、守り手を先に削るかを選ばせる。

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | 温かい、満ちる |
| 本体の動き | 動かず、胸の刻印だけが緑に灯る |
| 溜め | 刻印に緑の光がゆっくり集まる |
| 発生 | 守り手の足元から仲間の足元へ光る根の線が伸び、仲間の足元から緑の光の柱と葉の粒が昇る |
| 余韻 | 葉の粒がゆっくり漂って消える。揺れは使わない |
| 色 | 緑、金、白 |
| 格 | 中 |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat/implementation.md#未決事項)の数値が決まったら見直す |
| 実装 | 戦闘で使う（[敵の行動の仮設定](../../features/combat-enemy-behavior.md)） |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-04 | 固有技として定義を作った | ユーザーの依頼 | 使う敵：[森の守り手](../enemies/enemy-forest-guardian.md) |
| 2026-11-02 | スキルとdocの整理で、今のコードに合わせて「実装・ゲームマスタの場所」と「実装」を直した | 根拠：CombatEnemyBehaviors の `Moves` にこの技があり、戦闘で使う。攻撃しない技は `EnemyBlowOf` の形を使わない | [敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
