---
id: enemy-skill-rend
type: definition
status: 候補
updated: 2026-10-19
name: 引き裂き
category: enemy-skills
---

# 引き裂き

[敵の技の定義](README.md) / [関連仕様](../../features/combat.md#敵の行動の仮設定)

共通技。爪で傷を負わせ、出血させる攻撃である。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-04、ユーザーの依頼（敵の技を増やし、共通化できるものも入れる）で、[苔むした狼](../enemies/enemy-moss-wolf.md)の技を共通技として作った |
| 役割・説明 | 単体へのダメージと出血。威力は低いが、カードを使うたびに削る |
| 関連する仕様・個別ID | 使う敵：[苔むした狼](../enemies/enemy-moss-wolf.md)、[いばら蛇](../enemies/enemy-thorn-serpent.md)、[石のガーゴイル](../enemies/enemy-stone-gargoyle.md)、[鉱石喰いモグラ](../enemies/enemy-ore-mole.md)、[骨背の大ワニ](../enemies/enemy-bone-croc.md)、[包帯の山犬](../enemies/enemy-wrapped-jackal.md)、[砂鮫](../enemies/enemy-sand-shark.md)、[霜の大猿人](../enemies/enemy-frost-ape.md)、[骸骨海賊](../enemies/enemy-skeleton-pirate.md)、[霧の竜](../enemies/enemy-mist-dragon.md)。[敵の行動の仮設定](../../features/combat.md#敵の行動の仮設定) |
| 実装・ゲームマスタの場所 | 未実装 |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 共通技 | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 攻撃・味方単体 | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | 物理・斬 | 味方は物理の技を物防で、魔法の技を属防で受ける（[使い道の候補](../../features/party.md#ステータスの項目)） | 候補 |
| 効果 | 物攻の60%のダメージ。出血2ターン | 使う敵のステータスの割合（[威力の決め方](../../features/combat.md#威力の決め方)）。状態は[カードの状態の仮設定](../../features/combat.md#カードの状態の仮設定)の名前 | 仮 |
| 予告 | 攻撃・弱体 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [苔むした狼](../enemies/enemy-moss-wolf.md)、[いばら蛇](../enemies/enemy-thorn-serpent.md)、[石のガーゴイル](../enemies/enemy-stone-gargoyle.md)、[鉱石喰いモグラ](../enemies/enemy-ore-mole.md)、[骨背の大ワニ](../enemies/enemy-bone-croc.md)、[包帯の山犬](../enemies/enemy-wrapped-jackal.md)、[砂鮫](../enemies/enemy-sand-shark.md)、[霜の大猿人](../enemies/enemy-frost-ape.md)、[骸骨海賊](../enemies/enemy-skeleton-pirate.md)、[霧の竜](../enemies/enemy-mist-dragon.md) | 各敵の定義の「技」の表 | 候補 |
| 似合う敵 | 爪を持つ獣（狼、熊、猫の仲間） | 新しい敵へ付けるときの目安 | 候補 |

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | ざっくり裂ける |
| 本体の動き | 絵を後ろへ傾けて前足を上げたように見せ、斜めに跳びかかって戻る |
| 溜め | 体を後ろへ傾ける |
| 発生 | 斜め上から3本の爪痕の線が速く走る |
| 余韻 | 爪痕から赤い粒が少しこぼれて消える（出血の合図） |
| 色 | 白、赤 |
| 格 | 小 |

使う敵ごとの見た目の違いは、次のとおりである。

| 敵 | 色・材質・形の違い |
|---|---|
| [いばら蛇](../enemies/enemy-thorn-serpent.md) | とげだらけの深緑の胴が巻きついて引きはがし、茶色のとげの引っかき傷と緑の葉くずが散る |
| [石のガーゴイル](../enemies/enemy-stone-gargoyle.md) | 石の鉤爪で裂き、灰色の爪の線と石の粉が散る |
| [鉱石喰いモグラ](../enemies/enemy-ore-mole.md) | 爪の線を鉄色の三本にし、ひっかいた跡に細かい火花を散らす |
| [骨背の大ワニ](../enemies/enemy-bone-croc.md) | 太い前脚の白い爪で裂き、苔の垂れたうろこの色の残像が走る。 |
| [包帯の山犬](../enemies/enemy-wrapped-jackal.md) | 包帯から突き出た黒い爪で裂き、青い目の光が残像を引く。 |
| [砂鮫](../enemies/enemy-sand-shark.md) | かみついたまま頭を振って裂き、砂色の残像と赤い粒が散る。 |
| [霜の大猿人](../enemies/enemy-frost-ape.md) | 水色の氷の爪で、太い3本の引っかき線を描く |
| [骸骨海賊](../enemies/enemy-skeleton-pirate.md) | 曲刀でわざと傷口を広げる斬りで、赤い筋と潮のしぶきが散る |
| [霧の竜](../enemies/enemy-mist-dragon.md) | 前脚の黒い鉤爪で3本の青灰の線を刻み、傷口に霧の粒がまとわりつく |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat.md#未決事項)の数値が決まったら見直す |
| 味方に付く出血の量 | [敵の行動の仮設定](../../features/combat.md#敵の行動の仮設定)の案（カードを使うたびに最大HPの4%）で決める |
| 実装 | 未実装。実装するときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-04 | 共通技として定義を作った | ユーザーの依頼 | 使う敵：[苔むした狼](../enemies/enemy-moss-wolf.md) |
| 2026-10-19 | 使う敵に、新しく作った敵を加えた | ユーザーの依頼（敵を50体に増やし、文書カタログ・クライアント・サーバーにそろえる） | 使う敵：[いばら蛇](../enemies/enemy-thorn-serpent.md)、[石のガーゴイル](../enemies/enemy-stone-gargoyle.md)、[鉱石喰いモグラ](../enemies/enemy-ore-mole.md)、[骨背の大ワニ](../enemies/enemy-bone-croc.md)、[包帯の山犬](../enemies/enemy-wrapped-jackal.md)、[砂鮫](../enemies/enemy-sand-shark.md)、[霜の大猿人](../enemies/enemy-frost-ape.md)、[骸骨海賊](../enemies/enemy-skeleton-pirate.md)、[霧の竜](../enemies/enemy-mist-dragon.md) |
