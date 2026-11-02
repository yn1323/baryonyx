---
id: enemy-skill-mend
type: definition
status: 候補
updated: 2026-11-02
name: 傷ふさぎ
category: enemy-skills
---

# 傷ふさぎ

[敵の技の定義](README.md) / [関連仕様](../../features/combat-enemy-behavior.md)

共通技。体の傷をふさぎ、しばらく少しずつ治り続ける回復の技である。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-19、ユーザーの依頼（敵を50体に増やし、文書カタログ・クライアント・サーバーにそろえる）で、既存の共通技では足りない攻撃の型として作った |
| 役割・説明 | 自分の回復と、続けて回復するリジェネ |
| 関連する仕様・個別ID | 使う敵：[毒胞子オオダケ](../enemies/enemy-spore-cap.md)、[蜜吸い花](../enemies/enemy-nectar-bloom.md)、[水晶ヤドカリ](../enemies/enemy-crystal-hermit.md)、[ツチボタル](../enemies/enemy-glow-worm.md)、[地底湖の盲魚人](../enemies/enemy-blind-fishman.md)、[泥人形](../enemies/enemy-mud-golem.md)、[サボテン戦士](../enemies/enemy-cactus-warrior.md)、[火とかげ](../enemies/enemy-fire-lizard.md)、[沖の大ダコ](../enemies/enemy-giant-octopus.md)。[敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
| 実装・ゲームマスタの場所 | [CombatEnemyBehaviors](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatEnemyBehaviors.cs) の、使う敵の `Moves`。攻撃しない技のため、演出は技の名前と効果の文字・数字だけ（[BattleInspectView.EnemyMoves](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.EnemyMoves.cs) の `Support`） |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 共通技 | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 回復・自分 | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | ダメージなし | [記入する項目](README.md#記入する項目) | 候補 |
| 効果 | 属攻の60%のHPを回復。リジェネ2ターン（敵のターンの開始時に、使った敵の属攻の20%を回復） | [記入する項目](README.md#記入する項目) | 仮 |
| 予告 | 回復 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [毒胞子オオダケ](../enemies/enemy-spore-cap.md)、[蜜吸い花](../enemies/enemy-nectar-bloom.md)、[水晶ヤドカリ](../enemies/enemy-crystal-hermit.md)、[ツチボタル](../enemies/enemy-glow-worm.md)、[地底湖の盲魚人](../enemies/enemy-blind-fishman.md)、[泥人形](../enemies/enemy-mud-golem.md)、[サボテン戦士](../enemies/enemy-cactus-warrior.md)、[火とかげ](../enemies/enemy-fire-lizard.md)、[沖の大ダコ](../enemies/enemy-giant-octopus.md) | 各敵の定義の「技」の表 | 候補 |
| 似合う敵 | 体が再生する敵（植物、スライム、泥、アンデッド）、自分を治す魔法を使う敵 | 新しい敵へ付けるときの目安 | 候補 |

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | じんわり、やわらかい |
| 本体の動き | 体が小さくふくらんで、ゆっくり戻る |
| 溜め | 体の中心に、使う敵の体の色の光が集まる |
| 発生 | 光が体の縁まで広がり、傷の位置に小さな光の粒が吸い込まれる |
| 余韻 | 体の周りを、小さな光の粒がしばらく漂う |
| 色 | 使う敵の体の色を明るくした色と黄緑 |
| 格 | 小 |

使う敵ごとの見た目の違いは、次のとおりである。

| 敵 | 色・材質・形の違い |
|---|---|
| [毒胞子オオダケ](../enemies/enemy-spore-cap.md) | クリーム色の柄の筋に沿って細い菌糸が伸び、傷をつなぐ |
| [蜜吸い花](../enemies/enemy-nectar-bloom.md) | 茎から金色の蜜がにじみ、ちぎれた桃色の花弁をつなぐ |
| [水晶ヤドカリ](../enemies/enemy-crystal-hermit.md) | 回復の光を水晶の桃色と白にし、背中の水晶の紋が灯ってから光が体へ降りる形にする |
| [ツチボタル](../enemies/enemy-glow-worm.md) | 回復の光を淡い黄緑にし、しずくの付いた細い糸が体に巻きつく形にする |
| [地底湖の盲魚人](../enemies/enemy-blind-fishman.md) | 回復の光を水色にし、頭の上から水が流れ落ちて体を洗う形にする |
| [泥人形](../enemies/enemy-mud-golem.md) | 足元の泥が這い上がって傷を埋め、胸の金の指輪が淡く光る。 |
| [サボテン戦士](../enemies/enemy-cactus-warrior.md) | 体にためた水が縦のうねに沿って昇り、頭の赤い花が一度開き直す。 |
| [火とかげ](../enemies/enemy-fire-lizard.md) | 溶岩の橙の光が体に集まり、うろこの継ぎ目が光ってふさがる |
| [沖の大ダコ](../enemies/enemy-giant-octopus.md) | ちぎれた触手の先が伸び直し、赤紫と白の粒が触手の先へ集まる |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat/implementation.md#未決事項)の数値が決まったら見直す |
| 実装 | 戦闘で使う。効果のうち戦闘にまだないもの（味方に付く状態など）は写していない（[敵の行動の仮設定](../../features/combat-enemy-behavior.md)）。写すときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-19 | 共通技として定義を作った | ユーザーの依頼 | 使う敵：[毒胞子オオダケ](../enemies/enemy-spore-cap.md)、[蜜吸い花](../enemies/enemy-nectar-bloom.md)、[水晶ヤドカリ](../enemies/enemy-crystal-hermit.md)、[ツチボタル](../enemies/enemy-glow-worm.md)、[地底湖の盲魚人](../enemies/enemy-blind-fishman.md)、[泥人形](../enemies/enemy-mud-golem.md)、[サボテン戦士](../enemies/enemy-cactus-warrior.md)、[火とかげ](../enemies/enemy-fire-lizard.md)、[沖の大ダコ](../enemies/enemy-giant-octopus.md) |
| 2026-11-02 | スキルとdocの整理で、今のコードに合わせて「実装・ゲームマスタの場所」を直した | 根拠：CombatEnemyBehaviors の `Moves` にこの技があり、戦闘で使う | [敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
