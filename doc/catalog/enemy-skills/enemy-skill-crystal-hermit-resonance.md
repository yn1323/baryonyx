---
id: enemy-skill-crystal-hermit-resonance
type: definition
status: 候補
updated: 2026-11-02
name: 水晶の共鳴
category: enemy-skills
---

# 水晶の共鳴

[敵の技の定義](README.md) / [関連仕様](../../features/combat-enemy-behavior.md)

水晶ヤドカリの固有技。背中の水晶を鳴らし、ルーンの光の膜で仲間を包む。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-19、ユーザーの依頼（敵50体を試作し、文書カタログ・クライアント・サーバーにそろえる）で、[水晶ヤドカリ](../enemies/enemy-crystal-hermit.md)の技として作った |
| 役割・説明 | 仲間へのブロック |
| 関連する仕様・個別ID | 使う敵：[水晶ヤドカリ](../enemies/enemy-crystal-hermit.md)。[敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
| 実装・ゲームマスタの場所 | [CombatEnemyBehaviors](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatEnemyBehaviors.cs) の、使う敵の `Moves`。攻撃しない技のため、演出は技の名前と効果の文字・数字だけ（[BattleInspectView.EnemyMoves](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.EnemyMoves.cs) の `Support`） |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 固有技（[水晶ヤドカリ](../enemies/enemy-crystal-hermit.md)） | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 防御・敵全体（自分以外） | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | ダメージなし | [記入する項目](README.md#記入する項目) | 候補 |
| 効果 | 自分以外の敵全体に、属攻の60%のブロック。自分のほかに敵がいないときは、自分に同じ量のブロック | [記入する項目](README.md#記入する項目) | 仮 |
| 予告 | 防御 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [水晶ヤドカリ](../enemies/enemy-crystal-hermit.md) | 各敵の定義の「技」の表 | 候補 |

共通技の身を固める（自分に物防の割合のブロック）とは、仲間に配る点と、属攻の割合で決める点で違う。

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | りいん、包む |
| 本体の動き | はさみを下ろして体を低く沈め、水晶を少し上へ向ける |
| 溜め | 背中の水晶のルーンの紋が白く灯り、細い光の輪が水晶から広がる |
| 発生 | 光の輪が仲間の敵へ届き、それぞれの体の縁に桃色の多角形の薄い膜（ブロック）が張る |
| 余韻 | 膜は縁だけ明るく残る。水晶の紋の光はゆっくり消える |
| 色 | 桃色、紫、白 |
| 格 | 小 |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat/implementation.md#未決事項)の数値が決まったら見直す |
| 敵が敵に配るブロック | 今は、配られたブロックも配られた敵の次の行動の前に消える（[敵の行動の仮設定](../../features/combat-enemy-behavior.md)）。配った敵の行動に合わせるかを決める |
| 実装 | 戦闘で使う（[敵の行動の仮設定](../../features/combat-enemy-behavior.md)） |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-19 | 固有技として定義を作った | ユーザーの依頼（敵50体を試作し、文書カタログ・クライアント・サーバーにそろえる） | 使う敵：[水晶ヤドカリ](../enemies/enemy-crystal-hermit.md) |
| 2026-11-02 | スキルとdocの整理で、今のコードに合わせて「実装・ゲームマスタの場所」と「実装」を直した。「敵が敵に配るブロック」に今の消え方を書いた | 根拠：CombatEnemyBehaviors の `Moves` にこの技があり、戦闘で使う。攻撃しない技は `EnemyBlowOf` の形を使わない | [敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
