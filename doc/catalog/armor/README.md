# 防具の定義

[データ索引](../README.md) / [関連仕様](../../features/equipment.md)

個別内容と決定状態は、下の索引から各定義を参照する。
画像対象テンプレートを使う。
IDの接頭辞は `armor-`、メタデータの `category` は `armor` とする。
今の防具の種類・☆・効果・倍率の上限の仮の値は [EquipmentCatalog](../../../client/Assets/Baryonyx/Features/Equipment/Runtime/EquipmentCatalog.cs) にある（[装備の実装との対応](../../features/equipment.md#実装との対応)）。

## 個別データの索引

<!-- entries:start -->
登録された個別データはまだない。
<!-- entries:end -->

## 属性

種類ごとの属性である。決まりは[装備の属性](../../features/equipment.md#装備の属性)に従い、2026-11-03に名前と手に入る行き先から決めた。
防具は、3分の2ほどに属性を2つ付けた。魔法のローブは、魔法を使う誰にでも合うよう炎・氷・雷の3つにした。☆1は誰もが最初に使う装備として属性なしにした。

| ID | 名前 | ☆ | 属性 |
|---|---|---|---|
| traveler-cloak | 旅人のマント | ☆3 | 貫・斬 |
| chainmail | 鎖かたびら | ☆2 | 斬・打 |
| magic-robe | 魔法のローブ | ☆2 | 炎・氷・雷 |
| iron-helm | 鉄の兜 | ☆2 | 属性なし |
| leather-armor | 革の鎧 | ☆1 | 属性なし |
| wooden-shield | 木の丸盾 | ☆1 | 属性なし |
| miner-helm | 鉱夫の兜 | ☆2 | 打・貫 |
| crystal-shield | 水晶の大盾 | ☆4 | 打・氷 |
| mudcloth-robe | 泥染めのローブ | ☆2 | 氷・雷 |
| clay-armor | 土くれの鎧 | ☆3 | 属性なし |
| burial-wraps | 王墓の包帯 | ☆2 | 属性なし |
| ancient-plate | 古王国の鎧 | ☆4 | 斬・打 |
| hollow-helm | 空鎧の兜 | ☆2 | 属性なし |
| grave-cloak | 墓守りの外套 | ☆3 | 貫・氷 |
| lizardscale-mail | 火とかげの鱗鎧 | ☆2 | 炎・貫 |
| obsidian-shield | 黒曜石の盾 | ☆4 | 炎・打 |
| snow-cloak | 雪原の外套 | ☆2 | 氷・貫 |
| mammoth-fur | マンモスの毛皮 | ☆3 | 打・氷 |

## 合成のレシピ

[合成](../../features/synthesis.md#レシピ)で作るときの材料である。決まりは[レシピ](../../features/synthesis.md#レシピ)・[出来上がる品質](../../features/synthesis.md#出来上がる品質)・[費用](../../features/synthesis.md#費用)に従う。
「（特別）」は、アイテムではない材料（決まった装備・カスタムスキル）である。
組み合わせは2026-10-31にこちらで決めた仮の値である（[レシピの決め方](../../features/synthesis.md#レシピの決め方)）。
この表の材料はサーバーの [recipes.ts](../../../server/src/features/workshop/recipes.ts) とクライアントの [WorkshopRecipes](../../../client/Assets/Baryonyx/Features/Workshop/Runtime/WorkshopRecipes.cs)、名前と☆は [EquipmentCatalog](../../../client/Assets/Baryonyx/Features/Equipment/Runtime/EquipmentCatalog.cs) の写しで、正本はコードである（2026-11-02、ユーザーの判断）。直すときはコードを先に直し、同じ変更で表をそろえる。

| ID | 名前 | ☆ | 行き先 | 材料 |
|---|---|---|---|---|
| iron-helm | 鉄の兜 | ☆2 | ミストラ遺跡 | どんぐり×2・薬草×1 |
| chainmail | 鎖かたびら | ☆2 | 港町ポルトリア | 船乗りのロープ×3 |
| magic-robe | 魔法のローブ | ☆2 | 港町ポルトリア | 清めの海塩×2・船乗りのロープ×1 |
| traveler-cloak | 旅人のマント | ☆3 | 港町ポルトリア | 船乗りのロープ×2・干し魚×1・清めの海塩×1 |
| miner-helm | 鉱夫の兜 | ☆2 | グラシエラ氷窟 | 氷柱のかけら×2・ツチボタルの灯×1 |
| crystal-shield | 水晶の大盾 | ☆4 | グラシエラ氷窟 | 木の丸盾×1（特別）・霜の結晶×2・トロルの石皮×1・地底湖の清水×1 |
| mudcloth-robe | 泥染めのローブ | ☆2 | しじまの沼 | 泥だんご×3 |
| clay-armor | 土くれの鎧 | ☆3 | しじまの沼 | 泥だんご×2・鬼火の灯心×1・蓮の根×1 |
| burial-wraps | 王墓の包帯 | ☆2 | アルマジャ王墓 | サボテンの実×3 |
| ancient-plate | 古王国の鎧 | ☆4 | アルマジャ王墓 | 王墓の包帯×1（特別）・魔導書の切れ端×1・砂時計の砂×2・砂サソリの毒針×1 |
| hollow-helm | 空鎧の兜 | ☆2 | 月影の墓所 | 墓前のろうそく×3 |
| grave-cloak | 墓守りの外套 | ☆3 | 月影の墓所 | 大鴉の羽根×2・墓前のろうそく×2 |
| lizardscale-mail | 火とかげの鱗鎧 | ☆2 | ヴォルガン火山 | 火とかげのしっぽ×3 |
| obsidian-shield | 黒曜石の盾 | ☆4 | ヴォルガン火山 | 炎尾の尾羽×1・硫黄の粉×2・溶岩のつぶて×2 |
| snow-cloak | 雪原の外套 | ☆2 | 白嶺ネーヴェ | 固い雪玉×2・雪割り草×1 |
| mammoth-fur | マンモスの毛皮 | ☆3 | 白嶺ネーヴェ | 猪の干し肉×2・固い雪玉×2 |

☆1の革の鎧・木の丸盾はレシピを持たない。

## 記入する項目

| 項目 | 初期の扱い |
|---|---|
| 防具種・装備枠 | 防具種は未決。どのキャラでも装備できる（[装備](../../features/equipment.md)） |
| 属性 | [属性](#属性)の表に書く。決まりは[装備の属性](../../features/equipment.md#装備の属性)に従う。追加効果の中身と値は未決 |
| 参照するステータスと割合・レア度（☆1〜☆5）・Affix候補・倍率の上限 | 固定値は持たない。レア度は種類ごとに決め、1つずつ倍率と品質を持つ（[品質](../../features/inventory.md#品質)）。個別値は未決。[装備の効果](../../features/equipment.md#装備の効果)に従う。属性による耐性は設けない |
| 強化定義ID・セット関係の有無 | 強化は[品質強化](../../features/enhancement.md#品質強化)に従う。セット関係は未決 |
| 覆う部位・形状・厚み・材質・重量感 | 未決 |
| 配色・模様・紋章・装飾・使用痕 | 未決 |
| キャラの装備外見へ反映するか | 未決 |
| 同系列防具と区別する特徴 | 未決 |

## 追加方法

[個別データの作り方](../README.md#個別データの作り方)に従う。
