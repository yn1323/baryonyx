---
id: enemy-skill-tackle
type: definition
status: 候補
updated: 2026-10-19
name: たいあたり
category: enemy-skills
---

# たいあたり

[敵の技の定義](README.md) / [関連仕様](../../features/combat-enemy-behavior.md)

共通技。手足や武器を持たない敵が、体ごとぶつかる攻撃である。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-04、ユーザーの依頼（敵の技を増やし、共通化できるものも入れる）で、[苔スライム](../enemies/enemy-moss-slime.md)の技を共通技として作った |
| 役割・説明 | 単体への基本の物理攻撃 |
| 関連する仕様・個別ID | 使う敵：[苔スライム](../enemies/enemy-moss-slime.md)、[朝露ぷるん](../enemies/enemy-dew-slime.md)、[毒胞子オオダケ](../enemies/enemy-spore-cap.md)、[切り株猪](../enemies/enemy-stump-boar.md)、[鉱石喰いモグラ](../enemies/enemy-ore-mole.md)、[沼のカエル王](../enemies/enemy-frog-king.md)、[泥人形](../enemies/enemy-mud-golem.md)、[雪玉ころん](../enemies/enemy-snowball.md)、[雪原のマンモス](../enemies/enemy-snow-mammoth.md)、[沖の大ダコ](../enemies/enemy-giant-octopus.md)、[かぼちゃ頭の案山子](../enemies/enemy-pumpkin-scarecrow.md)、[迷い道標](../enemies/enemy-lost-signpost.md)、[金貨袋](../enemies/enemy-coin-pouch.md)。[敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
| 実装・ゲームマスタの場所 | 戦闘画面のモックで、苔スライムの攻撃として名前と単体攻撃だけを使っている（[BattleInspectAssets](../../../client/Assets/Baryonyx/Features/Combat/Editor/BattleInspectAssets.cs)の `Skill`）。ほかの効果と演出は未実装 |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 共通技 | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 攻撃・味方単体 | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | 物理・打 | 味方は物理の技を物防で、魔法の技を属防で受ける（[使い道の候補](../../features/party.md#ステータスの項目)） | 候補 |
| 効果 | 物攻の100%のダメージ | 使う敵のステータスの割合（[威力の決め方](../../features/combat.md#威力の決め方)）。状態は[カードの状態の仮設定](../../features/combat.md#カードの状態の仮設定)の名前 | 仮 |
| 予告 | 攻撃 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [苔スライム](../enemies/enemy-moss-slime.md)、[朝露ぷるん](../enemies/enemy-dew-slime.md)、[毒胞子オオダケ](../enemies/enemy-spore-cap.md)、[切り株猪](../enemies/enemy-stump-boar.md)、[鉱石喰いモグラ](../enemies/enemy-ore-mole.md)、[沼のカエル王](../enemies/enemy-frog-king.md)、[泥人形](../enemies/enemy-mud-golem.md)、[雪玉ころん](../enemies/enemy-snowball.md)、[雪原のマンモス](../enemies/enemy-snow-mammoth.md)、[沖の大ダコ](../enemies/enemy-giant-octopus.md)、[かぼちゃ頭の案山子](../enemies/enemy-pumpkin-scarecrow.md)、[迷い道標](../enemies/enemy-lost-signpost.md)、[金貨袋](../enemies/enemy-coin-pouch.md) | 各敵の定義の「技」の表 | 候補 |
| 似合う敵 | 手足や武器を持たない敵（スライム、転がる岩、丸い獣） | 新しい敵へ付けるときの目安 | 候補 |

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | ぽよんと弾む、鈍い |
| 本体の動き | 縦に縮んでから横に伸びて跳ね、標的にぶつかって弾み返る |
| 溜め | 体を縦に縮める |
| 発生 | ぶつかった所で体が横につぶれ、使う敵の体の色の粒と小さな輪が広がる |
| 余韻 | 粒が床に落ちて消える |
| 色 | 使う敵の体の色と白い芯 |
| 格 | 小 |

使う敵ごとの見た目の違いは、次のとおりである。

| 敵 | 色・材質・形の違い |
|---|---|
| [苔スライム](../enemies/enemy-moss-slime.md) | 青緑の水しぶき。溜めで、足元に小さな水たまりの光を出す |
| [朝露ぷるん](../enemies/enemy-dew-slime.md) | 水色のしずくの体が弾んでぶつかり、当たった所に白い水しぶきが散る |
| [毒胞子オオダケ](../enemies/enemy-spore-cap.md) | 紫の傘ごと前へ倒れかかり、当たると黄緑の胞子がふわりと舞う |
| [切り株猪](../enemies/enemy-stump-boar.md) | 赤茶の巨体が頭から突っ込み、土ぼこりと若葉が舞う |
| [鉱石喰いモグラ](../enemies/enemy-ore-mole.md) | 衝撃の輪を岩の灰色にし、当たった所から小石のかけらを散らす |
| [沼のカエル王](../enemies/enemy-frog-king.md) | 白い腹を突き出して前へ倒れ込み、ぬめった黄緑の体で押しつぶす。 |
| [泥人形](../enemies/enemy-mud-golem.md) | 黄土色の泥の巨体ごとのしかかり、当たった所に泥のしぶきが飛ぶ。 |
| [雪玉ころん](../enemies/enemy-snowball.md) | 白い雪玉が転がってぶつかり、当たった所で雪の粒が散る |
| [雪原のマンモス](../enemies/enemy-snow-mammoth.md) | 赤茶の巨体が押し出し、雪けむりが散る |
| [沖の大ダコ](../enemies/enemy-giant-octopus.md) | 太い触手を1本、鞭のように振り下ろし、赤紫の弧と水しぶきが出る |
| [かぼちゃ頭の案山子](../enemies/enemy-pumpkin-scarecrow.md) | 重いかぼちゃの頭ごと倒れかかり、黄色い藁くずが散る |
| [迷い道標](../enemies/enemy-lost-signpost.md) | 矢印の板の腕を振り回して叩きつけ、乾いた木屑が散る |
| [金貨袋](../enemies/enemy-coin-pouch.md) | 金貨の詰まった重い袋ごとぶつかり、金貨が数枚ちゃりんと跳ねる |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat.md#未決事項)の数値が決まったら見直す |
| 実装 | 未実装。実装するときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-04 | 共通技として定義を作った | ユーザーの依頼 | 使う敵：[苔スライム](../enemies/enemy-moss-slime.md) |
| 2026-10-19 | 使う敵に、新しく作った敵を加えた | ユーザーの依頼（敵を50体に増やし、文書カタログ・クライアント・サーバーにそろえる） | 使う敵：[朝露ぷるん](../enemies/enemy-dew-slime.md)、[毒胞子オオダケ](../enemies/enemy-spore-cap.md)、[切り株猪](../enemies/enemy-stump-boar.md)、[鉱石喰いモグラ](../enemies/enemy-ore-mole.md)、[沼のカエル王](../enemies/enemy-frog-king.md)、[泥人形](../enemies/enemy-mud-golem.md)、[雪玉ころん](../enemies/enemy-snowball.md)、[雪原のマンモス](../enemies/enemy-snow-mammoth.md)、[沖の大ダコ](../enemies/enemy-giant-octopus.md)、[かぼちゃ頭の案山子](../enemies/enemy-pumpkin-scarecrow.md)、[迷い道標](../enemies/enemy-lost-signpost.md)、[金貨袋](../enemies/enemy-coin-pouch.md) |
