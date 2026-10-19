---
id: enemy-skill-sweep
type: definition
status: 候補
updated: 2026-10-19
name: なぎ払い
category: enemy-skills
---

# なぎ払い

[敵の技の定義](README.md) / [関連仕様](../../features/combat.md#敵の行動の仮設定)

共通技。長い腕・尾・武器で、味方全体をまとめてなぎ払う物理の攻撃である。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-19、ユーザーの依頼（敵を50体に増やし、文書カタログ・クライアント・サーバーにそろえる）で、既存の共通技では足りない攻撃の型として作った |
| 役割・説明 | 全体への物理・斬の攻撃 |
| 関連する仕様・個別ID | 使う敵：[いばら蛇](../enemies/enemy-thorn-serpent.md)、[蜜吸い花](../enemies/enemy-nectar-bloom.md)、[骨背の大ワニ](../enemies/enemy-bone-croc.md)、[砂鮫](../enemies/enemy-sand-shark.md)、[フジツボ大ガニ](../enemies/enemy-barnacle-crab.md)、[霧の竜](../enemies/enemy-mist-dragon.md)。[敵の行動の仮設定](../../features/combat.md#敵の行動の仮設定) |
| 実装・ゲームマスタの場所 | 未実装 |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 共通技 | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 攻撃・味方全体 | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | 物理・斬 | 味方は物理の技を物防で、魔法の技を属防で受ける（[使い道の候補](../../features/party.md#ステータスの項目)） | 候補 |
| 効果 | 物攻の50%のダメージ | 使う敵のステータスの割合（[威力の決め方](../../features/combat.md#威力の決め方)）。状態は[カードの状態の仮設定](../../features/combat.md#カードの状態の仮設定)の名前 | 仮 |
| 予告 | 全体攻撃 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [いばら蛇](../enemies/enemy-thorn-serpent.md)、[蜜吸い花](../enemies/enemy-nectar-bloom.md)、[骨背の大ワニ](../enemies/enemy-bone-croc.md)、[砂鮫](../enemies/enemy-sand-shark.md)、[フジツボ大ガニ](../enemies/enemy-barnacle-crab.md)、[霧の竜](../enemies/enemy-mist-dragon.md) | 各敵の定義の「技」の表 | 候補 |
| 似合う敵 | 長い尾・腕・武器を持つ敵（ワニ、竜、大型の獣、剣を持つ敵） | 新しい敵へ付けるときの目安 | 候補 |

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | ぶん、横に広い |
| 本体の動き | 体を片側へひねってから、反対側へ大きく振る |
| 溜め | 体をひねり、振る側の縁が光る |
| 発生 | 味方全体の上を、横長の白い弧の線が左から右へ1本走る |
| 余韻 | 弧の線の跡に、細かな破片が散って消える |
| 色 | 白と、使う敵の尾・武器の色 |
| 格 | 小 |

使う敵ごとの見た目の違いは、次のとおりである。

| 敵 | 色・材質・形の違い |
|---|---|
| [いばら蛇](../enemies/enemy-thorn-serpent.md) | いばらの尾が横一文字に振られ、深緑の弧に茶色のとげの粒が混じる |
| [蜜吸い花](../enemies/enemy-nectar-bloom.md) | 葉の付いた蔓を振り回し、緑の弧に葉が舞う |
| [骨背の大ワニ](../enemies/enemy-bone-croc.md) | 骨のとげが並んだ尾を横に振り、白いとげの軌跡が弧を描く。 |
| [砂鮫](../enemies/enemy-sand-shark.md) | 砂から跳ね出て胸びれと尾びれで横に薙ぎ、砂の帯が弧を描く。 |
| [フジツボ大ガニ](../enemies/enemy-barnacle-crab.md) | 大きなはさみを横に振り回し、海藻のしずくが散る |
| [霧の竜](../enemies/enemy-mist-dragon.md) | 翼の先の爪と長い尾で宙から薙ぎ、白い霧の弧が味方全体を横切る |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat.md#未決事項)の数値が決まったら見直す |
| 実装 | 未実装。実装するときは[敵の挙動デバッグルーム](../../features/screens.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-19 | 共通技として定義を作った | ユーザーの依頼 | 使う敵：[いばら蛇](../enemies/enemy-thorn-serpent.md)、[蜜吸い花](../enemies/enemy-nectar-bloom.md)、[骨背の大ワニ](../enemies/enemy-bone-croc.md)、[砂鮫](../enemies/enemy-sand-shark.md)、[フジツボ大ガニ](../enemies/enemy-barnacle-crab.md)、[霧の竜](../enemies/enemy-mist-dragon.md) |
