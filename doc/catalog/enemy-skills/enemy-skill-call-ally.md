---
id: enemy-skill-call-ally
type: definition
status: 候補
updated: 2026-10-19
name: 仲間を呼ぶ
category: enemy-skills
---

# 仲間を呼ぶ

[敵の技の定義](README.md) / [関連仕様](../../features/combat.md#敵の行動の仮設定)

共通技。空いた立ち位置に、仲間の敵を1体呼ぶ。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-04、ユーザーの依頼（敵の技を増やし、共通化できるものも入れる）で、[森の守り手](../enemies/enemy-forest-guardian.md)の技を共通技として作った |
| 役割・説明 | 戦闘の途中で敵を増やす |
| 関連する仕様・個別ID | 使う敵：[森の守り手](../enemies/enemy-forest-guardian.md)、[石のガーゴイル](../enemies/enemy-stone-gargoyle.md)、[真鍮の番人](../enemies/enemy-brass-warden.md)、[岩肌トロル](../enemies/enemy-rock-troll.md)、[沼のカエル王](../enemies/enemy-frog-king.md)、[骨背の大ワニ](../enemies/enemy-bone-croc.md)、[砂鮫](../enemies/enemy-sand-shark.md)、[霜の大猿人](../enemies/enemy-frost-ape.md)、[雪原のマンモス](../enemies/enemy-snow-mammoth.md)、[黒曜石の巨像](../enemies/enemy-obsidian-colossus.md)、[骸骨海賊](../enemies/enemy-skeleton-pirate.md)、[沖の大ダコ](../enemies/enemy-giant-octopus.md)、[墓守りの大鴉](../enemies/enemy-grave-raven.md)、[死霊の魔導師](../enemies/enemy-lich.md)。[敵の行動の仮設定](../../features/combat.md#敵の行動の仮設定) |
| 実装・ゲームマスタの場所 | 未実装 |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 共通技 | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 呼び出し | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | ダメージなし | 味方は物理の技を物防で、魔法の技を属防で受ける（[使い道の候補](../../features/party.md#ステータスの項目)） | 候補 |
| 効果 | 空いた立ち位置に、使う敵ごとに決めた敵を1体呼ぶ。呼んだ敵のLvは使った敵と同じにする | 使う敵のステータスの割合（[威力の決め方](../../features/combat.md#威力の決め方)）。状態は[カードの状態の仮設定](../../features/combat.md#カードの状態の仮設定)の名前 | 仮 |
| 予告 | 呼び出し | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [森の守り手](../enemies/enemy-forest-guardian.md)、[石のガーゴイル](../enemies/enemy-stone-gargoyle.md)、[真鍮の番人](../enemies/enemy-brass-warden.md)、[岩肌トロル](../enemies/enemy-rock-troll.md)、[沼のカエル王](../enemies/enemy-frog-king.md)、[骨背の大ワニ](../enemies/enemy-bone-croc.md)、[砂鮫](../enemies/enemy-sand-shark.md)、[霜の大猿人](../enemies/enemy-frost-ape.md)、[雪原のマンモス](../enemies/enemy-snow-mammoth.md)、[黒曜石の巨像](../enemies/enemy-obsidian-colossus.md)、[骸骨海賊](../enemies/enemy-skeleton-pirate.md)、[沖の大ダコ](../enemies/enemy-giant-octopus.md)、[墓守りの大鴉](../enemies/enemy-grave-raven.md)、[死霊の魔導師](../enemies/enemy-lich.md) | 各敵の定義の「技」の表 | 候補 |
| 似合う敵 | 群れの長、巣の主、ボス | 新しい敵へ付けるときの目安 | 候補 |

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | 呼び寄せる、現れる |
| 本体の動き | 体を前へ傾け、沈むように呼ぶ仕草をする |
| 溜め | 空いた立ち位置の床に、使う敵に合わせた輪（根、魔法陣、巣穴の縁など）が浮かぶ |
| 発生 | 輪の中から、呼ばれた敵の絵が下から上へ現れる |
| 余韻 | 粒が舞って消える |
| 色 | 使う敵と呼ぶ敵の色 |
| 格 | 中 |

使う敵ごとの見た目の違いは、次のとおりである。

| 敵 | 色・材質・形の違い |
|---|---|
| [森の守り手](../enemies/enemy-forest-guardian.md) | 苔スライムを呼ぶ。根で描いた輪の中の床から苔が盛り上がり、光る芽が伸びて苔スライムの絵になる（苔の黄緑、青緑、淡い金） |
| [石のガーゴイル](../enemies/enemy-stone-gargoyle.md) | 空いた立ち位置の床に陶器のかけらの輪が浮かび、割れ壺の番が跳ね出る |
| [真鍮の番人](../enemies/enemy-brass-warden.md) | 呼び出しの光を青い炉の色にし、床の継ぎ目から紫の霧が立ち上って鎧が組み上がる形にする |
| [岩肌トロル](../enemies/enemy-rock-troll.md) | 呼び出しの光は使わず、天井から砂と小石と一緒にコウモリが落ちてくる形にする |
| [沼のカエル王](../enemies/enemy-frog-king.md) | 床に蓮の葉の形の輪が浮かび、沼の水が盛り上がって吸血大ヒルが現れる（黄緑、深緑、赤黒）。 |
| [骨背の大ワニ](../enemies/enemy-bone-croc.md) | 立ち位置の床に沼の波紋の輪が広がり、泥の中から吸血大ヒルが這い出る（暗緑、泥色、赤黒）。 |
| [砂鮫](../enemies/enemy-sand-shark.md) | 砂の波紋の輪の中から、砂を払い落として砂サソリが現れる（砂色、琥珀、青）。 |
| [霜の大猿人](../enemies/enemy-frost-ape.md) | 足元の雪をすくって丸め、雪玉ころんが転がり出る |
| [雪原のマンモス](../enemies/enemy-snow-mammoth.md) | 鼻を高く上げて鳴くと、氷槍のペンギン兵が駆けつける |
| [黒曜石の巨像](../enemies/enemy-obsidian-colossus.md) | 体から垂れた溶岩が床で固まり、溶岩スライムになって起き上がる |
| [骸骨海賊](../enemies/enemy-skeleton-pirate.md) | 青い海面のような波紋の輪から、灯りを抱いたクラゲの亡霊が浮かび上がる（紺、水色、黄色） |
| [沖の大ダコ](../enemies/enemy-giant-octopus.md) | 岩の割れ目を描いた輪から、触手がフジツボ大ガニを掻き出し、海藻と泡が舞う（赤紫、赤、灰） |
| [墓守りの大鴉](../enemies/enemy-grave-raven.md) | ランタンの黄色い灯りが墓土の輪を照らし、土の中から骸骨兵の骨が組み上がる（黄色、土色、黄ばんだ白） |
| [死霊の魔導師](../enemies/enemy-lich.md) | 緑の魔法陣の輪が床に浮かび、古王国の兵の骨が組み上がって骸骨兵になる（緑、紫、黄ばんだ白） |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat.md#未決事項)の数値が決まったら見直す |
| 呼び出しの決まり | 立ち位置、行動順への入れ方、呼んだ敵を倒す必要があるか（[敵の行動の仮設定](../../features/combat.md#敵の行動の仮設定)） |
| 実装 | 未実装。実装するときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-04 | 共通技として定義を作った | ユーザーの依頼 | 使う敵：[森の守り手](../enemies/enemy-forest-guardian.md) |
| 2026-10-19 | 使う敵に、新しく作った敵を加えた | ユーザーの依頼（敵を50体に増やし、文書カタログ・クライアント・サーバーにそろえる） | 使う敵：[石のガーゴイル](../enemies/enemy-stone-gargoyle.md)、[真鍮の番人](../enemies/enemy-brass-warden.md)、[岩肌トロル](../enemies/enemy-rock-troll.md)、[沼のカエル王](../enemies/enemy-frog-king.md)、[骨背の大ワニ](../enemies/enemy-bone-croc.md)、[砂鮫](../enemies/enemy-sand-shark.md)、[霜の大猿人](../enemies/enemy-frost-ape.md)、[雪原のマンモス](../enemies/enemy-snow-mammoth.md)、[黒曜石の巨像](../enemies/enemy-obsidian-colossus.md)、[骸骨海賊](../enemies/enemy-skeleton-pirate.md)、[沖の大ダコ](../enemies/enemy-giant-octopus.md)、[墓守りの大鴉](../enemies/enemy-grave-raven.md)、[死霊の魔導師](../enemies/enemy-lich.md) |
