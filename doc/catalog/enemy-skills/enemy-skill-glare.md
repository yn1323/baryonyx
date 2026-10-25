---
id: enemy-skill-glare
type: definition
status: 候補
updated: 2026-10-19
name: にらみつけ
category: enemy-skills
---

# にらみつけ

[敵の技の定義](README.md) / [関連仕様](../../features/combat.md#敵の行動の仮設定)

共通技。鋭い目でにらみ、味方1人の手元を鈍らせる弱体の技である。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-19、ユーザーの依頼（敵を50体に増やし、文書カタログ・クライアント・サーバーにそろえる）で、既存の共通技では足りない攻撃の型として作った |
| 役割・説明 | 単体への麻痺 |
| 関連する仕様・個別ID | 使う敵：[いばら蛇](../enemies/enemy-thorn-serpent.md)、[石のガーゴイル](../enemies/enemy-stone-gargoyle.md)、[浮遊する魔導書](../enemies/enemy-floating-grimoire.md)、[沼のカエル王](../enemies/enemy-frog-king.md)、[包帯の山犬](../enemies/enemy-wrapped-jackal.md)、[つらら尾の狐](../enemies/enemy-icicle-fox.md)、[死霊の魔導師](../enemies/enemy-lich.md)、[かぼちゃ頭の案山子](../enemies/enemy-pumpkin-scarecrow.md)。[敵の行動の仮設定](../../features/combat.md#敵の行動の仮設定) |
| 実装・ゲームマスタの場所 | 未実装 |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 共通技 | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 弱体・味方単体 | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | ダメージなし | 味方は物理の技を物防で、魔法の技を属防で受ける（[使い道の候補](../../features/party.md#ステータスの項目)） | 候補 |
| 効果 | 麻痺2ターン | 使う敵のステータスの割合（[威力の決め方](../../features/combat.md#威力の決め方)）。状態は[カードの状態の仮設定](../../features/combat.md#カードの状態の仮設定)の名前 | 仮 |
| 予告 | 弱体 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [いばら蛇](../enemies/enemy-thorn-serpent.md)、[石のガーゴイル](../enemies/enemy-stone-gargoyle.md)、[浮遊する魔導書](../enemies/enemy-floating-grimoire.md)、[沼のカエル王](../enemies/enemy-frog-king.md)、[包帯の山犬](../enemies/enemy-wrapped-jackal.md)、[つらら尾の狐](../enemies/enemy-icicle-fox.md)、[死霊の魔導師](../enemies/enemy-lich.md)、[かぼちゃ頭の案山子](../enemies/enemy-pumpkin-scarecrow.md) | 各敵の定義の「技」の表 | 候補 |
| 似合う敵 | 目の大きな敵、光る目を持つ敵、威圧する大型の敵 | 新しい敵へ付けるときの目安 | 候補 |

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | ぎろり、冷える |
| 本体の動き | 体を標的のほうへ少し傾け、目が強く光る |
| 溜め | 目の位置に小さな光がともる |
| 発生 | 目から標的へ細い光の線が伸び、標的の周りに縦の線が数本出る |
| 余韻 | 縦の線がゆっくり消える |
| 色 | 使う敵の目の色と白 |
| 格 | 小 |

使う敵ごとの見た目の違いは、次のとおりである。

| 敵 | 色・材質・形の違い |
|---|---|
| [いばら蛇](../enemies/enemy-thorn-serpent.md) | 赤い実の目が赤く光り、標的に赤い光の輪が重なる |
| [石のガーゴイル](../enemies/enemy-stone-gargoyle.md) | 黄色く光る目で見据え、標的に黄色の光が重なる |
| [浮遊する魔導書](../enemies/enemy-floating-grimoire.md) | にらむ光を一つ目の水色にし、金の飾り枠ごと目が見開く形にする |
| [沼のカエル王](../enemies/enemy-frog-king.md) | 半ば閉じた金色の目がじろりと光り、王冠の赤い宝石が一瞬きらめく。 |
| [包帯の山犬](../enemies/enemy-wrapped-jackal.md) | 包帯の隙間から青く光る目で射すくめ、青い光の筋が標的へ伸びる。 |
| [つらら尾の狐](../enemies/enemy-icicle-fox.md) | 青い吊り目から、細い水色の光の線が標的へ伸びる |
| [死霊の魔導師](../enemies/enemy-lich.md) | 王冠の下の目のくぼみが緑に光り、緑の細い視線の線が標的へ伸びる |
| [かぼちゃ頭の案山子](../enemies/enemy-pumpkin-scarecrow.md) | くり抜いた三角の目の奥が橙にぼうっと灯り、相手をにらむ |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat.md#未決事項)の数値が決まったら見直す |
| 実装 | 未実装。実装するときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-19 | 共通技として定義を作った | ユーザーの依頼 | 使う敵：[いばら蛇](../enemies/enemy-thorn-serpent.md)、[石のガーゴイル](../enemies/enemy-stone-gargoyle.md)、[浮遊する魔導書](../enemies/enemy-floating-grimoire.md)、[沼のカエル王](../enemies/enemy-frog-king.md)、[包帯の山犬](../enemies/enemy-wrapped-jackal.md)、[つらら尾の狐](../enemies/enemy-icicle-fox.md)、[死霊の魔導師](../enemies/enemy-lich.md)、[かぼちゃ頭の案山子](../enemies/enemy-pumpkin-scarecrow.md) |
