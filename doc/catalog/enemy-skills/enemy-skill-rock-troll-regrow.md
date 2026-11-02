---
id: enemy-skill-rock-troll-regrow
type: definition
status: 候補
updated: 2026-11-02
name: 岩肌の再生
category: enemy-skills
---

# 岩肌の再生

[敵の技の定義](README.md) / [関連仕様](../../features/combat-enemy-behavior.md)

岩肌トロルの固有技。傷口に洞窟の岩が育ち、すぐに塞がる。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-19、ユーザーの依頼（敵50体を試作し、文書カタログ・クライアント・サーバーにそろえる）で、[岩肌トロル](../enemies/enemy-rock-troll.md)の技として作った |
| 役割・説明 | 自分の回復 |
| 関連する仕様・個別ID | 使う敵：[岩肌トロル](../enemies/enemy-rock-troll.md)。[敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
| 実装・ゲームマスタの場所 | [CombatEnemyBehaviors](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatEnemyBehaviors.cs) の、使う敵の `Moves`。攻撃しない技のため、演出は技の名前と効果の文字・数字だけ（[BattleInspectView.EnemyMoves](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.EnemyMoves.cs) の `Support`） |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 固有技（[岩肌トロル](../enemies/enemy-rock-troll.md)） | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 回復・自分 | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | ダメージなし | [記入する項目](README.md#記入する項目) | 候補 |
| 効果 | 自分の失ったHPの20%を回復する | [記入する項目](README.md#記入する項目) | 仮 |
| 予告 | 回復 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [岩肌トロル](../enemies/enemy-rock-troll.md) | 各敵の定義の「技」の表 | 候補 |

共通技の傷ふさぎ（属攻の割合で回復し、リジェネを付ける）と違い、深く傷つくほど多く回復する。

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | みしみし、塞がる |
| 本体の動き | 体を丸めて縮め、ゆっくり元に戻す |
| 溜め | 岩肌のひびの縁が淡く光る |
| 発生 | ひびの上に小さな岩の欠片が寄り集まって塞がり、回復の数字を出す |
| 余韻 | 塞がった所から細かい砂がこぼれる。揺れは使わない |
| 色 | 灰青、灰茶、淡い緑 |
| 格 | 小 |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat/implementation.md#未決事項)の数値が決まったら見直す |
| 失ったHPの割合での回復 | 新しい仕組み。[戦闘の長さ](../../features/combat/rules.md#戦闘の長さ)の目安に収まるよう、使う回数の制限を決める |
| 実装 | 戦闘で使う。効果のうち戦闘にまだないもの（味方に付く状態など）は写していない（[敵の行動の仮設定](../../features/combat-enemy-behavior.md)）。写すときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-19 | 固有技として定義を作った | ユーザーの依頼（敵50体を試作し、文書カタログ・クライアント・サーバーにそろえる） | 使う敵：[岩肌トロル](../enemies/enemy-rock-troll.md) |
| 2026-11-02 | スキルとdocの整理で、今のコードに合わせて「実装・ゲームマスタの場所」を直した | 根拠：CombatEnemyBehaviors の `Moves` にこの技があり、戦闘で使う | [敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
