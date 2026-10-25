---
id: enemy-skill-double-thrust
type: definition
status: 候補
updated: 2026-10-19
name: 二段突き
category: enemy-skills
---

# 二段突き

[敵の技の定義](README.md) / [関連仕様](../../features/combat.md#敵の行動の仮設定)

共通技。槍・角・針で、味方1人を素早く2回突く物理の攻撃である。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-19、ユーザーの依頼（敵を50体に増やし、文書カタログ・クライアント・サーバーにそろえる）で、既存の共通技では足りない攻撃の型として作った |
| 役割・説明 | 単体への2回の物理・貫の攻撃。ブロックを2回に分けて削る |
| 関連する仕様・個別ID | 使う敵：[どんぐり兵](../enemies/enemy-acorn-soldier.md)、[水晶角うさぎ](../enemies/enemy-crystal-hare.md)、[地底湖の盲魚人](../enemies/enemy-blind-fishman.md)、[サボテン戦士](../enemies/enemy-cactus-warrior.md)、[砂鮫](../enemies/enemy-sand-shark.md)、[氷槍のペンギン兵](../enemies/enemy-ice-penguin.md)、[炎尾の鳥](../enemies/enemy-flame-tail-bird.md)、[墓守りの大鴉](../enemies/enemy-grave-raven.md)。[敵の行動の仮設定](../../features/combat.md#敵の行動の仮設定) |
| 実装・ゲームマスタの場所 | 未実装 |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 共通技 | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 攻撃・味方単体 | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | 物理・貫 | 味方は物理の技を物防で、魔法の技を属防で受ける（[使い道の候補](../../features/party.md#ステータスの項目)） | 候補 |
| 効果 | 物攻の60%のダメージを2回 | 使う敵のステータスの割合（[威力の決め方](../../features/combat.md#威力の決め方)）。状態は[カードの状態の仮設定](../../features/combat.md#カードの状態の仮設定)の名前 | 仮 |
| 予告 | 攻撃（2回） | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [どんぐり兵](../enemies/enemy-acorn-soldier.md)、[水晶角うさぎ](../enemies/enemy-crystal-hare.md)、[地底湖の盲魚人](../enemies/enemy-blind-fishman.md)、[サボテン戦士](../enemies/enemy-cactus-warrior.md)、[砂鮫](../enemies/enemy-sand-shark.md)、[氷槍のペンギン兵](../enemies/enemy-ice-penguin.md)、[炎尾の鳥](../enemies/enemy-flame-tail-bird.md)、[墓守りの大鴉](../enemies/enemy-grave-raven.md) | 各敵の定義の「技」の表 | 候補 |
| 似合う敵 | 槍・角・くちばし・針を持つ敵 | 新しい敵へ付けるときの目安 | 候補 |

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | 鋭い、小刻み |
| 本体の動き | 前へ2回、短く突き出る |
| 溜め | 体を後ろへ引き、先端が光る |
| 発生 | 標的へ細い光の線が2回刺さり、刺さるたびに小さな星形の光が出る |
| 余韻 | 光の線が細くなって消える |
| 色 | 白と、使う敵の槍・角の色 |
| 格 | 小 |

使う敵ごとの見た目の違いは、次のとおりである。

| 敵 | 色・材質・形の違い |
|---|---|
| [どんぐり兵](../enemies/enemy-acorn-soldier.md) | 小枝の槍の灰白の石の穂先で、短い突きの線を2本続けて出す |
| [水晶角うさぎ](../enemies/enemy-crystal-hare.md) | 跳ねながら額の水晶の角で突き、薄紫の突きの線2本と水晶の小さなかけらが散る |
| [地底湖の盲魚人](../enemies/enemy-blind-fishman.md) | 突きの線を骨の白にし、とげの並んだ穂先の形の光が二度走り、水しぶきを散らす |
| [サボテン戦士](../enemies/enemy-cactus-warrior.md) | とげだらけの左右の腕で続けて殴り、当たるたびに黄色いとげの粒が散る。 |
| [砂鮫](../enemies/enemy-sand-shark.md) | 一度離れてすぐもう一度食らいつき、2回とも砂しぶきが上がる。 |
| [氷槍のペンギン兵](../enemies/enemy-ice-penguin.md) | 氷の槍の穂先が水色に光り、細い光の線が2回刺さる |
| [炎尾の鳥](../enemies/enemy-flame-tail-bird.md) | 曲がったくちばしで2回つつき、刺さるたびに火の粉が散る |
| [墓守りの大鴉](../enemies/enemy-grave-raven.md) | 斜めに急降下して灰色のくちばしで2度つつき、黒い羽根の粒が舞う |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat.md#未決事項)の数値が決まったら見直す |
| 実装 | 未実装。実装するときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-19 | 共通技として定義を作った | ユーザーの依頼 | 使う敵：[どんぐり兵](../enemies/enemy-acorn-soldier.md)、[水晶角うさぎ](../enemies/enemy-crystal-hare.md)、[地底湖の盲魚人](../enemies/enemy-blind-fishman.md)、[サボテン戦士](../enemies/enemy-cactus-warrior.md)、[砂鮫](../enemies/enemy-sand-shark.md)、[氷槍のペンギン兵](../enemies/enemy-ice-penguin.md)、[炎尾の鳥](../enemies/enemy-flame-tail-bird.md)、[墓守りの大鴉](../enemies/enemy-grave-raven.md) |
