---
id: enemy-skill-brace
type: definition
status: 候補
updated: 2026-10-19
name: 身を固める
category: enemy-skills
---

# 身を固める

[敵の技の定義](README.md) / [関連仕様](../../features/combat-enemy-behavior.md)

共通技。体を固めてブロックを張り、次の攻撃に備える。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-04、ユーザーの依頼（敵の技を増やし、共通化できるものも入れる）で、[森の守り手](../enemies/enemy-forest-guardian.md)の技を共通技として作った |
| 役割・説明 | 自分のブロック |
| 関連する仕様・個別ID | 使う敵：[森の守り手](../enemies/enemy-forest-guardian.md)、[どんぐり兵](../enemies/enemy-acorn-soldier.md)、[毒胞子オオダケ](../enemies/enemy-spore-cap.md)、[切り株猪](../enemies/enemy-stump-boar.md)、[石のガーゴイル](../enemies/enemy-stone-gargoyle.md)、[割れ壺の番](../enemies/enemy-cracked-urn.md)、[人食い宝箱](../enemies/enemy-chest-mimic.md)、[真鍮の番人](../enemies/enemy-brass-warden.md)、[水晶ヤドカリ](../enemies/enemy-crystal-hermit.md)、[鉱石喰いモグラ](../enemies/enemy-ore-mole.md)、[岩肌トロル](../enemies/enemy-rock-troll.md)、[地底湖の盲魚人](../enemies/enemy-blind-fishman.md)、[泥人形](../enemies/enemy-mud-golem.md)、[吸血大ヒル](../enemies/enemy-blood-leech.md)、[骨背の大ワニ](../enemies/enemy-bone-croc.md)、[砂サソリ](../enemies/enemy-sand-scorpion.md)、[砂時計の精](../enemies/enemy-hourglass-sprite.md)、[雪玉ころん](../enemies/enemy-snowball.md)、[雪原のマンモス](../enemies/enemy-snow-mammoth.md)、[溶岩スライム](../enemies/enemy-lava-slime.md)、[黒曜石の巨像](../enemies/enemy-obsidian-colossus.md)、[フジツボ大ガニ](../enemies/enemy-barnacle-crab.md)、[沖の大ダコ](../enemies/enemy-giant-octopus.md)、[骸骨兵](../enemies/enemy-skeleton-soldier.md)、[死霊の魔導師](../enemies/enemy-lich.md)、[迷い道標](../enemies/enemy-lost-signpost.md)、[金貨袋](../enemies/enemy-coin-pouch.md)。[敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
| 実装・ゲームマスタの場所 | 未実装 |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 共通技 | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 防御・自分 | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | ダメージなし | 味方は物理の技を物防で、魔法の技を属防で受ける（[使い道の候補](../../features/party.md#ステータスの項目)） | 候補 |
| 効果 | 物防の120%のブロック | 使う敵のステータスの割合（[威力の決め方](../../features/combat.md#威力の決め方)）。状態は[カードの状態の仮設定](../../features/combat.md#カードの状態の仮設定)の名前 | 仮 |
| 予告 | 防御 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [森の守り手](../enemies/enemy-forest-guardian.md)、[どんぐり兵](../enemies/enemy-acorn-soldier.md)、[毒胞子オオダケ](../enemies/enemy-spore-cap.md)、[切り株猪](../enemies/enemy-stump-boar.md)、[石のガーゴイル](../enemies/enemy-stone-gargoyle.md)、[割れ壺の番](../enemies/enemy-cracked-urn.md)、[人食い宝箱](../enemies/enemy-chest-mimic.md)、[真鍮の番人](../enemies/enemy-brass-warden.md)、[水晶ヤドカリ](../enemies/enemy-crystal-hermit.md)、[鉱石喰いモグラ](../enemies/enemy-ore-mole.md)、[岩肌トロル](../enemies/enemy-rock-troll.md)、[地底湖の盲魚人](../enemies/enemy-blind-fishman.md)、[泥人形](../enemies/enemy-mud-golem.md)、[吸血大ヒル](../enemies/enemy-blood-leech.md)、[骨背の大ワニ](../enemies/enemy-bone-croc.md)、[砂サソリ](../enemies/enemy-sand-scorpion.md)、[砂時計の精](../enemies/enemy-hourglass-sprite.md)、[雪玉ころん](../enemies/enemy-snowball.md)、[雪原のマンモス](../enemies/enemy-snow-mammoth.md)、[溶岩スライム](../enemies/enemy-lava-slime.md)、[黒曜石の巨像](../enemies/enemy-obsidian-colossus.md)、[フジツボ大ガニ](../enemies/enemy-barnacle-crab.md)、[沖の大ダコ](../enemies/enemy-giant-octopus.md)、[骸骨兵](../enemies/enemy-skeleton-soldier.md)、[死霊の魔導師](../enemies/enemy-lich.md)、[迷い道標](../enemies/enemy-lost-signpost.md)、[金貨袋](../enemies/enemy-coin-pouch.md) | 各敵の定義の「技」の表 | 候補 |
| 似合う敵 | 硬い体や殻を持つ敵（ゴーレム、甲虫、亀） | 新しい敵へ付けるときの目安 | 候補 |

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | 固い、どっしり |
| 本体の動き | 縦に少し縮み、身を固める |
| 溜め | 短く、体の表面がざわつく |
| 発生 | 体の輪郭に沿って、使う敵の材質の板の光の面が重なりながら閉じ、縁が明るく光る |
| 余韻 | 面は縁から薄れ、表面の欠片が落ちる |
| 色 | 使う敵の材質の色、白い縁 |
| 格 | 小 |

使う敵ごとの見た目の違いは、次のとおりである。

| 敵 | 色・材質・形の違い |
|---|---|
| [森の守り手](../enemies/enemy-forest-guardian.md) | ふぞろいな苔むした岩の板（灰、淡い緑）。余韻で苔の粒が落ちる |
| [どんぐり兵](../enemies/enemy-acorn-soldier.md) | 大きな緑の葉の盾を前へ出し、葉脈の模様が入った緑の膜を張る |
| [毒胞子オオダケ](../enemies/enemy-spore-cap.md) | 紫の傘を深く下ろし、黄色の斑点の模様が入った紫の膜を張る |
| [切り株猪](../enemies/enemy-stump-boar.md) | 背中の切り株を前へ向け、年輪の模様が入った木肌の茶色の膜を張る |
| [石のガーゴイル](../enemies/enemy-stone-gargoyle.md) | 翼をたたんで石像に戻り、ひびの光が消えて灰色の石の膜が張る |
| [割れ壺の番](../enemies/enemy-cracked-urn.md) | 口を閉じて転がり、青い唐草の模様が入った白い陶器の膜を張る |
| [人食い宝箱](../enemies/enemy-chest-mimic.md) | ブロックの膜を金の縁取りの色にし、ふたが閉じる動きに合わせて張る形にする |
| [真鍮の番人](../enemies/enemy-brass-warden.md) | ブロックの膜を鋲の並んだ真鍮の板の形にする |
| [水晶ヤドカリ](../enemies/enemy-crystal-hermit.md) | ブロックの膜を桃色と紫の水晶の多角形の板の形にする |
| [鉱石喰いモグラ](../enemies/enemy-ore-mole.md) | ブロックの膜を灰色の岩の板を重ねた形にし、頭の兜から広がるようにする |
| [岩肌トロル](../enemies/enemy-rock-troll.md) | ブロックの膜を灰青の岩を継いだ板の形にする |
| [地底湖の盲魚人](../enemies/enemy-blind-fishman.md) | ブロックの膜を半透明の薄紫のひれの形にする |
| [泥人形](../enemies/enemy-mud-golem.md) | 体の泥が厚く盛り上がり、しずくを垂らす黄土色の泥の膜がブロックになる。 |
| [吸血大ヒル](../enemies/enemy-blood-leech.md) | 赤黒い体を丸く縮め、照り返しの強いぬめりの膜がブロックになる。 |
| [骨背の大ワニ](../enemies/enemy-bone-croc.md) | 頭を下げて骨のとげの並んだ背を向け、暗緑のうろこと白いとげの板がブロックになる。 |
| [砂サソリ](../enemies/enemy-sand-scorpion.md) | はさみを前に寄せ、琥珀色の殻の板が重なってブロックになる。 |
| [砂時計の精](../enemies/enemy-hourglass-sprite.md) | 金の枠を前に向け、青い宝石を中心に金の格子の膜がブロックになる。 |
| [雪玉ころん](../enemies/enemy-snowball.md) | 表面の雪を押し固めた、白と水色の膜にする |
| [雪原のマンモス](../enemies/enemy-snow-mammoth.md) | 背と毛の雪が厚く固まった、白い膜にする |
| [溶岩スライム](../enemies/enemy-lava-slime.md) | ひびを閉じ、黒い殻を厚くした黒っぽい膜にする |
| [黒曜石の巨像](../enemies/enemy-obsidian-colossus.md) | ひびを閉じ、黒曜石の板を寄せ合わせた黒と紫の膜にする |
| [フジツボ大ガニ](../enemies/enemy-barnacle-crab.md) | 甲羅に身を縮め、フジツボの殻が盾のように並ぶ白い膜にする |
| [沖の大ダコ](../enemies/enemy-giant-octopus.md) | 触手を体に巻きつけ、白い吸盤の並ぶ赤紫の肉の膜をブロックにする |
| [骸骨兵](../enemies/enemy-skeleton-soldier.md) | 欠けた丸盾を前に構え、鉄の縁の色の膜がブロックとして張る |
| [死霊の魔導師](../enemies/enemy-lich.md) | 杖の前に緑の魔力の障壁を張り、ローブの裾が揺れる |
| [迷い道標](../enemies/enemy-lost-signpost.md) | 裂けた脚を地面に突き立て、乾いた木の黄土色の膜がブロックとして張る |
| [金貨袋](../enemies/enemy-coin-pouch.md) | 赤い口紐をきゅっと締めて革を張り、こげ茶の革の膜がブロックになる |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat.md#未決事項)の数値が決まったら見直す |
| 敵のブロックが消えるタイミング | [敵の行動の仮設定](../../features/combat-enemy-behavior.md)で決める |
| 実装 | 未実装。実装するときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-04 | 共通技として定義を作った | ユーザーの依頼 | 使う敵：[森の守り手](../enemies/enemy-forest-guardian.md) |
| 2026-10-19 | 使う敵に、新しく作った敵を加えた | ユーザーの依頼（敵を50体に増やし、文書カタログ・クライアント・サーバーにそろえる） | 使う敵：[どんぐり兵](../enemies/enemy-acorn-soldier.md)、[毒胞子オオダケ](../enemies/enemy-spore-cap.md)、[切り株猪](../enemies/enemy-stump-boar.md)、[石のガーゴイル](../enemies/enemy-stone-gargoyle.md)、[割れ壺の番](../enemies/enemy-cracked-urn.md)、[人食い宝箱](../enemies/enemy-chest-mimic.md)、[真鍮の番人](../enemies/enemy-brass-warden.md)、[水晶ヤドカリ](../enemies/enemy-crystal-hermit.md)、[鉱石喰いモグラ](../enemies/enemy-ore-mole.md)、[岩肌トロル](../enemies/enemy-rock-troll.md)、[地底湖の盲魚人](../enemies/enemy-blind-fishman.md)、[泥人形](../enemies/enemy-mud-golem.md)、[吸血大ヒル](../enemies/enemy-blood-leech.md)、[骨背の大ワニ](../enemies/enemy-bone-croc.md)、[砂サソリ](../enemies/enemy-sand-scorpion.md)、[砂時計の精](../enemies/enemy-hourglass-sprite.md)、[雪玉ころん](../enemies/enemy-snowball.md)、[雪原のマンモス](../enemies/enemy-snow-mammoth.md)、[溶岩スライム](../enemies/enemy-lava-slime.md)、[黒曜石の巨像](../enemies/enemy-obsidian-colossus.md)、[フジツボ大ガニ](../enemies/enemy-barnacle-crab.md)、[沖の大ダコ](../enemies/enemy-giant-octopus.md)、[骸骨兵](../enemies/enemy-skeleton-soldier.md)、[死霊の魔導師](../enemies/enemy-lich.md)、[迷い道標](../enemies/enemy-lost-signpost.md)、[金貨袋](../enemies/enemy-coin-pouch.md) |
