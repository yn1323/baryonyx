---
id: enemy-skill-shatter
type: definition
status: 候補
updated: 2026-10-19
name: 打ち砕き
category: enemy-skills
---

# 打ち砕き

[敵の技の定義](README.md) / [関連仕様](../../features/combat-enemy-behavior.md)

共通技。重い一撃で、味方1人の守りを崩す物理の攻撃である。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-19、ユーザーの依頼（敵を50体に増やし、文書カタログ・クライアント・サーバーにそろえる）で、既存の共通技では足りない攻撃の型として作った |
| 役割・説明 | 単体への物理・打の攻撃と防御ダウン |
| 関連する仕様・個別ID | 使う敵：[古王国の空鎧](../enemies/enemy-hollow-armor.md)、[人食い宝箱](../enemies/enemy-chest-mimic.md)、[真鍮の番人](../enemies/enemy-brass-warden.md)、[水晶ヤドカリ](../enemies/enemy-crystal-hermit.md)、[岩肌トロル](../enemies/enemy-rock-troll.md)、[泥人形](../enemies/enemy-mud-golem.md)、[霜の大猿人](../enemies/enemy-frost-ape.md)、[雪原のマンモス](../enemies/enemy-snow-mammoth.md)、[黒曜石の巨像](../enemies/enemy-obsidian-colossus.md)、[骸骨兵](../enemies/enemy-skeleton-soldier.md)。[敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
| 実装・ゲームマスタの場所 | 未実装 |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 共通技 | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 攻撃・味方単体 | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | 物理・打 | 味方は物理の技を物防で、魔法の技を属防で受ける（[使い道の候補](../../features/party.md#ステータスの項目)） | 候補 |
| 効果 | 物攻の60%のダメージ。防御ダウン2ターン | 使う敵のステータスの割合（[威力の決め方](../../features/combat.md#威力の決め方)）。状態は[カードの状態の仮設定](../../features/combat.md#カードの状態の仮設定)の名前 | 仮 |
| 予告 | 攻撃・弱体 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [古王国の空鎧](../enemies/enemy-hollow-armor.md)、[人食い宝箱](../enemies/enemy-chest-mimic.md)、[真鍮の番人](../enemies/enemy-brass-warden.md)、[水晶ヤドカリ](../enemies/enemy-crystal-hermit.md)、[岩肌トロル](../enemies/enemy-rock-troll.md)、[泥人形](../enemies/enemy-mud-golem.md)、[霜の大猿人](../enemies/enemy-frost-ape.md)、[雪原のマンモス](../enemies/enemy-snow-mammoth.md)、[黒曜石の巨像](../enemies/enemy-obsidian-colossus.md)、[骸骨兵](../enemies/enemy-skeleton-soldier.md) | 各敵の定義の「技」の表 | 候補 |
| 似合う敵 | 重い拳・はさみ・甲羅・石の体を持つ敵 | 新しい敵へ付けるときの目安 | 候補 |

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | ごつん、重い |
| 本体の動き | 体を高く持ち上げてから、標的へ落とすように打ちつける |
| 溜め | 体を後ろへ大きく反らす |
| 発生 | 標的に放射状のひびの形が出て、ひびの間から破片が飛ぶ |
| 余韻 | ひびの形が薄れ、標的の周りに小さな砂ぼこりが残る |
| 色 | 茶、灰、白 |
| 格 | 小 |

使う敵ごとの見た目の違いは、次のとおりである。

| 敵 | 色・材質・形の違い |
|---|---|
| [古王国の空鎧](../enemies/enemy-hollow-armor.md) | 盾の縁で打ち、鉄色の衝撃の輪と錆の粉が散る |
| [人食い宝箱](../enemies/enemy-chest-mimic.md) | 衝撃の輪を金の縁取りの色にし、ふたが勢いよく閉じる音に合わせて金貨の粒を散らす |
| [真鍮の番人](../enemies/enemy-brass-warden.md) | 衝撃の輪を真鍮の金色にし、四角い拳の形の光が上から落ちる形にする |
| [水晶ヤドカリ](../enemies/enemy-crystal-hermit.md) | 衝撃の輪を橙色にし、ぎざぎざのはさみの形の光が左右から締まる形にする |
| [岩肌トロル](../enemies/enemy-rock-troll.md) | 衝撃の輪を灰青の岩の色にし、当たった所から岩のかけらを散らす |
| [泥人形](../enemies/enemy-mud-golem.md) | 泥を固めた焦げ茶の拳を振り下ろし、ひびの線と泥のかけらが散る。 |
| [霜の大猿人](../enemies/enemy-frost-ape.md) | 青い拳を振り下ろし、ひびの形に霜の粒が混じる |
| [雪原のマンモス](../enemies/enemy-snow-mammoth.md) | 赤い布を巻いた象牙色の牙を振り下ろして打ち据える |
| [黒曜石の巨像](../enemies/enemy-obsidian-colossus.md) | 黒曜石の拳で叩きつけ、ひびの形の間から溶岩の光がのぞく |
| [骸骨兵](../enemies/enemy-skeleton-soldier.md) | 欠けた丸盾の縁で殴りつけ、褪せた赤と鉄色の衝撃の輪が出る |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat.md#未決事項)の数値が決まったら見直す |
| 実装 | 未実装。実装するときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-19 | 共通技として定義を作った | ユーザーの依頼 | 使う敵：[古王国の空鎧](../enemies/enemy-hollow-armor.md)、[人食い宝箱](../enemies/enemy-chest-mimic.md)、[真鍮の番人](../enemies/enemy-brass-warden.md)、[水晶ヤドカリ](../enemies/enemy-crystal-hermit.md)、[岩肌トロル](../enemies/enemy-rock-troll.md)、[泥人形](../enemies/enemy-mud-golem.md)、[霜の大猿人](../enemies/enemy-frost-ape.md)、[雪原のマンモス](../enemies/enemy-snow-mammoth.md)、[黒曜石の巨像](../enemies/enemy-obsidian-colossus.md)、[骸骨兵](../enemies/enemy-skeleton-soldier.md) |
