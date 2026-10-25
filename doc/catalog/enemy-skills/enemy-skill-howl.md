---
id: enemy-skill-howl
type: definition
status: 候補
updated: 2026-10-19
name: 遠吠え
category: enemy-skills
---

# 遠吠え

[敵の技の定義](README.md) / [関連仕様](../../features/combat-enemy-behavior.md)

共通技。群れの仲間を奮い立たせる合図である。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-04、ユーザーの依頼（敵の技を増やし、共通化できるものも入れる）で、[苔むした狼](../enemies/enemy-moss-wolf.md)の技を共通技として作った |
| 役割・説明 | 自分と仲間の攻撃を強める |
| 関連する仕様・個別ID | 使う敵：[苔むした狼](../enemies/enemy-moss-wolf.md)、[包帯の山犬](../enemies/enemy-wrapped-jackal.md)、[霜の大猿人](../enemies/enemy-frost-ape.md)、[氷槍のペンギン兵](../enemies/enemy-ice-penguin.md)、[骸骨海賊](../enemies/enemy-skeleton-pirate.md)。[敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
| 実装・ゲームマスタの場所 | 未実装 |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 共通技 | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 強化・敵全体（自分と仲間） | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | ダメージなし | 味方は物理の技を物防で、魔法の技を属防で受ける（[使い道の候補](../../features/party.md#ステータスの項目)） | 候補 |
| 効果 | 攻撃力アップ2ターン | 使う敵のステータスの割合（[威力の決め方](../../features/combat.md#威力の決め方)）。状態は[カードの状態の仮設定](../../features/combat.md#カードの状態の仮設定)の名前 | 仮 |
| 予告 | 強化 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [苔むした狼](../enemies/enemy-moss-wolf.md)、[包帯の山犬](../enemies/enemy-wrapped-jackal.md)、[霜の大猿人](../enemies/enemy-frost-ape.md)、[氷槍のペンギン兵](../enemies/enemy-ice-penguin.md)、[骸骨海賊](../enemies/enemy-skeleton-pirate.md) | 各敵の定義の「技」の表 | 候補 |
| 似合う敵 | 群れで暮らす獣（狼、野犬、鳥の群れ） | 新しい敵へ付けるときの目安 | 候補 |

仲間がいないときに使うかどうかは、使う敵の行動パターンで決める。

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | 響く、ざわつく |
| 本体の動き | 絵を後ろへ傾けて頭を上げたように見せ、小刻みに震わせる |
| 溜め | 体を沈める |
| 発生 | 口元から同心の音の輪が3つ広がり、仲間の体に赤橙の光が一瞬走る。背景の葉が揺れて粒が舞う |
| 余韻 | 仲間の足元に赤橙の光の輪が残り、薄れて消える |
| 色 | 赤橙、白 |
| 格 | 中 |

使う敵ごとの見た目の違いは、次のとおりである。

| 敵 | 色・材質・形の違い |
|---|---|
| [包帯の山犬](../enemies/enemy-wrapped-jackal.md) | 頭を上げて吠えると、金の首飾りが光り、包帯の端が風に舞い上がる。 |
| [霜の大猿人](../enemies/enemy-frost-ape.md) | 胸を叩いて吠え、白い息の輪と雪の粒が広がる |
| [氷槍のペンギン兵](../enemies/enemy-ice-penguin.md) | 遠吠えではなく、くちばしを上げて鳴き交わし、黄色の小さな音の輪が出る |
| [骸骨海賊](../enemies/enemy-skeleton-pirate.md) | 遠吠えの代わりに口笛を吹き曲刀を掲げる号令で、紺と赤の波が仲間へ広がる |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat.md#未決事項)の数値が決まったら見直す |
| 実装 | 未実装。実装するときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-04 | 共通技として定義を作った | ユーザーの依頼 | 使う敵：[苔むした狼](../enemies/enemy-moss-wolf.md) |
| 2026-10-19 | 使う敵に、新しく作った敵を加えた | ユーザーの依頼（敵を50体に増やし、文書カタログ・クライアント・サーバーにそろえる） | 使う敵：[包帯の山犬](../enemies/enemy-wrapped-jackal.md)、[霜の大猿人](../enemies/enemy-frost-ape.md)、[氷槍のペンギン兵](../enemies/enemy-ice-penguin.md)、[骸骨海賊](../enemies/enemy-skeleton-pirate.md) |
