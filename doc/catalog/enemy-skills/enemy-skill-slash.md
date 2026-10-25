---
id: enemy-skill-slash
type: definition
status: 候補
updated: 2026-10-19
name: 斬りつけ
category: enemy-skills
---

# 斬りつけ

[敵の技の定義](README.md) / [関連仕様](../../features/combat.md#敵の行動の仮設定)

共通技。爪・刃・とげで、味方1人を切りつける物理の攻撃である。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-19、ユーザーの依頼（敵を50体に増やし、文書カタログ・クライアント・サーバーにそろえる）で、既存の共通技では足りない攻撃の型として作った |
| 役割・説明 | 単体への基本の物理・斬の攻撃 |
| 関連する仕様・個別ID | 使う敵：[古王国の空鎧](../enemies/enemy-hollow-armor.md)、[水晶ヤドカリ](../enemies/enemy-crystal-hermit.md)、[砂サソリ](../enemies/enemy-sand-scorpion.md)、[黒曜石の巨像](../enemies/enemy-obsidian-colossus.md)、[フジツボ大ガニ](../enemies/enemy-barnacle-crab.md)、[骸骨海賊](../enemies/enemy-skeleton-pirate.md)、[骸骨兵](../enemies/enemy-skeleton-soldier.md)。[敵の行動の仮設定](../../features/combat.md#敵の行動の仮設定) |
| 実装・ゲームマスタの場所 | 未実装 |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 共通技 | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 攻撃・味方単体 | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | 物理・斬 | 味方は物理の技を物防で、魔法の技を属防で受ける（[使い道の候補](../../features/party.md#ステータスの項目)） | 候補 |
| 効果 | 物攻の100%のダメージ | 使う敵のステータスの割合（[威力の決め方](../../features/combat.md#威力の決め方)）。状態は[カードの状態の仮設定](../../features/combat.md#カードの状態の仮設定)の名前 | 仮 |
| 予告 | 攻撃 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [古王国の空鎧](../enemies/enemy-hollow-armor.md)、[水晶ヤドカリ](../enemies/enemy-crystal-hermit.md)、[砂サソリ](../enemies/enemy-sand-scorpion.md)、[黒曜石の巨像](../enemies/enemy-obsidian-colossus.md)、[フジツボ大ガニ](../enemies/enemy-barnacle-crab.md)、[骸骨海賊](../enemies/enemy-skeleton-pirate.md)、[骸骨兵](../enemies/enemy-skeleton-soldier.md) | 各敵の定義の「技」の表 | 候補 |
| 似合う敵 | 爪・刃・とげを持つ敵（獣、剣を持つ骸骨、とげの植物） | 新しい敵へ付けるときの目安 | 候補 |

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | 鋭い、速い |
| 本体の動き | 標的の側へ少し身を乗り出し、すぐ戻る |
| 溜め | 体を後ろへ小さく引く |
| 発生 | 標的の上を、斜めの白い弧の線が1コマで走る |
| 余韻 | 弧の線の端から細かな破片が散って消える |
| 色 | 白と、使う敵の刃・爪の色 |
| 格 | 小 |

使う敵ごとの見た目の違いは、次のとおりである。

| 敵 | 色・材質・形の違い |
|---|---|
| [古王国の空鎧](../enemies/enemy-hollow-armor.md) | 錆びた長剣の、くすんだ茶色の斬撃の線に紫の霧の尾が引く |
| [水晶ヤドカリ](../enemies/enemy-crystal-hermit.md) | 斬撃の線を橙色の殻の色にし、はさみが閉じる形の二本の弧にする |
| [砂サソリ](../enemies/enemy-sand-scorpion.md) | 振り上げた琥珀色のはさみで挟み切り、ぎざぎざの刃の線が2本交わる。 |
| [黒曜石の巨像](../enemies/enemy-obsidian-colossus.md) | 腕に並ぶ結晶の縁で切り、紫の弧の線が走る |
| [フジツボ大ガニ](../enemies/enemy-barnacle-crab.md) | ぎざぎざのはさみで挟み切り、赤い弧の線が走る |
| [骸骨海賊](../enemies/enemy-skeleton-pirate.md) | 刃こぼれした曲刀の鈍い銀の弧で斬り、金の護拳が一瞬きらめく |
| [骸骨兵](../enemies/enemy-skeleton-soldier.md) | 錆びた幅広の剣で赤茶の弧を描き、錆の粉が散る |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat.md#未決事項)の数値が決まったら見直す |
| 実装 | 未実装。実装するときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-19 | 共通技として定義を作った | ユーザーの依頼 | 使う敵：[古王国の空鎧](../enemies/enemy-hollow-armor.md)、[水晶ヤドカリ](../enemies/enemy-crystal-hermit.md)、[砂サソリ](../enemies/enemy-sand-scorpion.md)、[黒曜石の巨像](../enemies/enemy-obsidian-colossus.md)、[フジツボ大ガニ](../enemies/enemy-barnacle-crab.md)、[骸骨海賊](../enemies/enemy-skeleton-pirate.md)、[骸骨兵](../enemies/enemy-skeleton-soldier.md) |
