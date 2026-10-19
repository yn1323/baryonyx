---
id: enemy-skill-flame-bolt
type: definition
status: 候補
updated: 2026-10-19
name: 火の玉
category: enemy-skills
---

# 火の玉

[敵の技の定義](README.md) / [関連仕様](../../features/combat.md#敵の行動の仮設定)

共通技。炎の玉を味方1人にぶつける魔法の攻撃である。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-19、ユーザーの依頼（敵を50体に増やし、文書カタログ・クライアント・サーバーにそろえる）で、既存の共通技では足りない攻撃の型として作った |
| 役割・説明 | 単体への魔法・炎の攻撃とやけど |
| 関連する仕様・個別ID | 使う敵：[呪いの燭台](../enemies/enemy-cursed-candelabra.md)、[鬼火](../enemies/enemy-will-o-wisp.md)、[火とかげ](../enemies/enemy-fire-lizard.md)、[溶岩スライム](../enemies/enemy-lava-slime.md)、[クラゲの亡霊](../enemies/enemy-ghost-jelly.md)、[墓守りの大鴉](../enemies/enemy-grave-raven.md)。[敵の行動の仮設定](../../features/combat.md#敵の行動の仮設定) |
| 実装・ゲームマスタの場所 | 未実装 |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 共通技 | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 攻撃・味方単体 | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | 魔法・炎 | 味方は物理の技を物防で、魔法の技を属防で受ける（[使い道の候補](../../features/party.md#ステータスの項目)） | 候補 |
| 効果 | 属攻の100%のダメージ。やけど1ターン | 使う敵のステータスの割合（[威力の決め方](../../features/combat.md#威力の決め方)）。状態は[カードの状態の仮設定](../../features/combat.md#カードの状態の仮設定)の名前 | 仮 |
| 予告 | 攻撃・弱体 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [呪いの燭台](../enemies/enemy-cursed-candelabra.md)、[鬼火](../enemies/enemy-will-o-wisp.md)、[火とかげ](../enemies/enemy-fire-lizard.md)、[溶岩スライム](../enemies/enemy-lava-slime.md)、[クラゲの亡霊](../enemies/enemy-ghost-jelly.md)、[墓守りの大鴉](../enemies/enemy-grave-raven.md) | 各敵の定義の「技」の表 | 候補 |
| 似合う敵 | 炎を宿す敵、魔法を使う敵 | 新しい敵へ付けるときの目安 | 候補 |

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | ぼうっと燃える、熱い |
| 本体の動き | 体の前に炎の玉を作り、標的へ放つ |
| 溜め | 体の前に小さな炎が渦を巻いて丸くなる |
| 発生 | 炎の玉が弧を描いて飛び、標的で弾けて火の粉が広がる |
| 余韻 | 火の粉が上へ昇って消える |
| 色 | 橙、黄、白の芯 |
| 格 | 小 |

使う敵ごとの見た目の違いは、次のとおりである。

| 敵 | 色・材質・形の違い |
|---|---|
| [呪いの燭台](../enemies/enemy-cursed-candelabra.md) | 火の玉を青白い炎にし、尾に溶けた蝋のしずくを引く形にする |
| [鬼火](../enemies/enemy-will-o-wisp.md) | 火の玉は赤ではなく黄緑の炎で、白い芯の光の筋が渦を巻く。 |
| [火とかげ](../enemies/enemy-fire-lizard.md) | 口から、尾の炎と同じ黄と白の芯の火の玉を吐く |
| [溶岩スライム](../enemies/enemy-lava-slime.md) | 炎の玉ではなく、黒い殻のかけらが付いた溶岩の塊を吐き飛ばす |
| [クラゲの亡霊](../enemies/enemy-ghost-jelly.md) | 傘の中の黄色い灯りが強く明滅し、沈んだ船のランタンの灯のような黄色い火の玉が傘の縁から放たれる |
| [墓守りの大鴉](../enemies/enemy-grave-raven.md) | 足に下げた真鍮のランタンを振り、黄色い灯りを火の玉にして落とす |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat.md#未決事項)の数値が決まったら見直す |
| 実装 | 未実装。実装するときは[敵の挙動デバッグルーム](../../features/screens.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-19 | 共通技として定義を作った | ユーザーの依頼 | 使う敵：[呪いの燭台](../enemies/enemy-cursed-candelabra.md)、[鬼火](../enemies/enemy-will-o-wisp.md)、[火とかげ](../enemies/enemy-fire-lizard.md)、[溶岩スライム](../enemies/enemy-lava-slime.md)、[クラゲの亡霊](../enemies/enemy-ghost-jelly.md)、[墓守りの大鴉](../enemies/enemy-grave-raven.md) |
