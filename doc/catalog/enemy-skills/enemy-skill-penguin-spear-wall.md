---
id: enemy-skill-penguin-spear-wall
type: definition
status: 候補
updated: 2026-11-02
name: 槍ぶすま
category: enemy-skills
---

# 槍ぶすま

[敵の技の定義](README.md) / [関連仕様](../../features/combat-enemy-behavior.md)

氷槍のペンギン兵の固有技。隊列を組んで槍を前へ並べ、仲間ごと守りを固める。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-19、ユーザーの依頼（敵50体を試作し、文書カタログ・クライアント・サーバーにそろえる）で、[氷槍のペンギン兵](../enemies/enemy-ice-penguin.md)の技として作った |
| 役割・説明 | 自分と仲間のブロック |
| 関連する仕様・個別ID | 使う敵：[氷槍のペンギン兵](../enemies/enemy-ice-penguin.md)。[敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
| 実装・ゲームマスタの場所 | [CombatEnemyBehaviors](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatEnemyBehaviors.cs) の、使う敵の `Moves`。攻撃しない技のため、演出は技の名前と効果の文字・数字だけ（[BattleInspectView.EnemyMoves](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.EnemyMoves.cs) の `Support`） |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 固有技（[氷槍のペンギン兵](../enemies/enemy-ice-penguin.md)） | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 防御・敵全体（自分と仲間） | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | ダメージなし | [記入する項目](README.md#記入する項目) | 候補 |
| 効果 | 自分と仲間それぞれに、使った敵の物防の60%のブロック | [記入する項目](README.md#記入する項目) | 仮 |
| 予告 | 防御 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [氷槍のペンギン兵](../enemies/enemy-ice-penguin.md) | 各敵の定義の「技」の表 | 候補 |

共通技の身を固める（自分だけのブロック）を仲間にも広げる技であり、隊列で持ち場を守るペンギン兵の習性を表すため固有技にした。

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | ずらり、構える |
| 本体の動き | 腰を落とし、槍を前へ倒して構える |
| 溜め | なし（すぐ構える） |
| 発生 | 敵それぞれの前に、槍の穂先の形の細い水色の光が扇状に並び、その奥に薄い膜（ブロック）が張る |
| 余韻 | 膜は縁だけ明るく残る |
| 色 | 水色、白 |
| 格 | 小 |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat/implementation.md#未決事項)の数値が決まったら見直す |
| 仲間に付くブロック | 今の戦闘では、敵のブロックは、ブロックを持つ敵の次の行動の前に消える（[敵の行動の仮設定](../../features/combat-enemy-behavior.md)）。仲間に張ったブロックもこれと同じに消すかを決める |
| 実装 | 戦闘で使う。効果のうち戦闘にまだないもの（味方に付く状態など）は写していない（[敵の行動の仮設定](../../features/combat-enemy-behavior.md)）。写すときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-19 | 固有技として定義を作った | ユーザーの依頼 | 使う敵：[氷槍のペンギン兵](../enemies/enemy-ice-penguin.md) |
| 2026-11-02 | スキルとdocの整理で、今のコードに合わせて「実装・ゲームマスタの場所」と、敵のブロックが消えるタイミングを未決とした記述を直した | 根拠：CombatEnemyBehaviors の `Moves` にこの技があり、戦闘で使う。敵のブロックは BattleInspectView.EnemyMoves の `EnemyAct` で、その敵の行動の始めに消す | [敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
