---
id: enemy-skill-screech
type: definition
status: 候補
updated: 2026-10-19
name: 金切り声
category: enemy-skills
---

# 金切り声

[敵の技の定義](README.md) / [関連仕様](../../features/combat-enemy-behavior.md)

共通技。耳をつんざく声で、味方全体の手元を鈍らせる弱体の技である。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-19、ユーザーの依頼（敵を50体に増やし、文書カタログ・クライアント・サーバーにそろえる）で、既存の共通技では足りない攻撃の型として作った |
| 役割・説明 | 全体への麻痺 |
| 関連する仕様・個別ID | 使う敵：[大耳コウモリ](../enemies/enemy-big-eared-bat.md)、[鬼火](../enemies/enemy-will-o-wisp.md)、[炎尾の鳥](../enemies/enemy-flame-tail-bird.md)、[墓守りの大鴉](../enemies/enemy-grave-raven.md)、[霧の竜](../enemies/enemy-mist-dragon.md)。[敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
| 実装・ゲームマスタの場所 | 未実装 |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 共通技 | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 弱体・味方全体 | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | ダメージなし | 味方は物理の技を物防で、魔法の技を属防で受ける（[使い道の候補](../../features/party.md#ステータスの項目)） | 候補 |
| 効果 | 麻痺1ターン | 使う敵のステータスの割合（[威力の決め方](../../features/combat.md#威力の決め方)）。状態は[カードの状態の仮設定](../../features/combat.md#カードの状態の仮設定)の名前 | 仮 |
| 予告 | 弱体 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [大耳コウモリ](../enemies/enemy-big-eared-bat.md)、[鬼火](../enemies/enemy-will-o-wisp.md)、[炎尾の鳥](../enemies/enemy-flame-tail-bird.md)、[墓守りの大鴉](../enemies/enemy-grave-raven.md)、[霧の竜](../enemies/enemy-mist-dragon.md) | 各敵の定義の「技」の表 | 候補 |
| 似合う敵 | 鳴き声の大きい敵（コウモリ、鳥、亡霊） | 新しい敵へ付けるときの目安 | 候補 |

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | きいん、耳に刺さる |
| 本体の動き | 体を反らして口を開き、小刻みに震える |
| 溜め | 口の前に小さな輪が1つ出る |
| 発生 | 敵から味方全体へ、輪が3重に広がる |
| 余韻 | 輪が薄れながら画面の端で消える |
| 色 | 白と薄い紫 |
| 格 | 小 |

使う敵ごとの見た目の違いは、次のとおりである。

| 敵 | 色・材質・形の違い |
|---|---|
| [大耳コウモリ](../enemies/enemy-big-eared-bat.md) | 声の輪を紫にし、大きな耳の間から細かい波の線が何重にも広がる形にする |
| [鬼火](../enemies/enemy-will-o-wisp.md) | 炎の中の顔がすすり泣くように声を上げ、黄緑の音の輪が揺らめいて広がる。 |
| [炎尾の鳥](../enemies/enemy-flame-tail-bird.md) | 甲高い声で鳴き、橙の輪が3重に広がる |
| [墓守りの大鴉](../enemies/enemy-grave-raven.md) | しゃがれた鴉の鳴き声で、黒と濃紺の音の輪が味方全体へ広がる |
| [霧の竜](../enemies/enemy-mist-dragon.md) | 霧の山に響く竜の咆哮で、白い霧の輪が何重にも広がる |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat.md#未決事項)の数値が決まったら見直す |
| 実装 | 未実装。実装するときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-19 | 共通技として定義を作った | ユーザーの依頼 | 使う敵：[大耳コウモリ](../enemies/enemy-big-eared-bat.md)、[鬼火](../enemies/enemy-will-o-wisp.md)、[炎尾の鳥](../enemies/enemy-flame-tail-bird.md)、[墓守りの大鴉](../enemies/enemy-grave-raven.md)、[霧の竜](../enemies/enemy-mist-dragon.md) |
