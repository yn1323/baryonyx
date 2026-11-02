# アクセサリーの定義

[データ索引](../README.md) / [関連仕様](../../features/equipment.md#アクセサリー)

個別内容と決定状態は、下の索引から各定義を参照する。
画像対象テンプレートを使う。
IDの接頭辞は `accessory-`、メタデータの `category` は `accessories` とする。
今のアクセサリーの種類・☆・効果・倍率の上限の仮の値は [EquipmentCatalog](../../../client/Assets/Baryonyx/Features/Equipment/Runtime/EquipmentCatalog.cs) にある（[装備の実装との対応](../../features/equipment.md#実装との対応)）。

## 個別データの索引

<!-- entries:start -->
登録された個別データはまだない。
<!-- entries:end -->

## 属性

種類ごとの属性である。決まりは[装備の属性](../../features/equipment.md#装備の属性)に従い、2026-11-03に名前と手に入る行き先から決めた。
アクセサリーは誰にでも付ける底上げとして、7割ほどを属性なしにした。

| ID | 名前 | ☆ | 属性 |
|---|---|---|---|
| moss-charm | 苔玉のお守り | ☆1 | 属性なし |
| acorn-brooch | どんぐりのブローチ | ☆2 | 属性なし |
| dew-pendant | 朝露のペンダント | ☆3 | 属性なし |
| guardian-circlet | 守り手の葉冠 | ☆5 | 属性なし |
| shell-necklace | 貝殻の首飾り | ☆1 | 属性なし |
| anchor-bangle | 錨の腕輪 | ☆2 | 打・斬 |
| gold-earring | 金貨の耳飾り | ☆3 | 属性なし |
| kraken-pearl | 大ダコの黒真珠 | ☆5 | 属性なし |
| penguin-feather | ペンギンの羽飾り | ☆1 | 属性なし |
| crystal-ring | 水晶の指輪 | ☆3 | 氷・雷 |
| troll-heartstone | トロルの心臓石 | ☆5 | 属性なし |
| leech-bell | ヒルよけの鈴 | ☆2 | 属性なし |
| frog-crown | カエル王の冠 | ☆3 | 雷・氷 |
| croc-fang | 大ワニの牙飾り | ☆5 | 貫・斬 |
| scarab-amulet | スカラベの護符 | ☆1 | 属性なし |
| gargoyle-eye | ガーゴイルの石眼 | ☆3 | 属性なし |
| golden-mask | 王の黄金仮面 | ☆5 | 属性なし |
| bone-ring | 骨の指輪 | ☆2 | 属性なし |
| moonstone-pendant | 月長石のペンダント | ☆3 | 属性なし |
| lich-signet | 死霊の印章 | ☆5 | 属性なし |
| ember-charm | 熾火のお守り | ☆1 | 炎・雷 |
| magma-ring | 溶岩の指輪 | ☆3 | 炎・打 |
| obsidian-crown | 黒曜石の冠 | ☆5 | 属性なし |
| snowflake-brooch | 雪の結晶のブローチ | ☆2 | 属性なし |
| foxtail-charm | つらら尾の房飾り | ☆3 | 属性なし |
| mistdragon-eye | 霧竜の瞳 | ☆5 | 氷・炎 |

## 合成のレシピ

[合成](../../features/synthesis.md#レシピ)で作るときの材料である。決まりは[レシピ](../../features/synthesis.md#レシピ)・[出来上がる品質](../../features/synthesis.md#出来上がる品質)・[費用](../../features/synthesis.md#費用)に従う。
「（特別）」は、アイテムではない材料（決まった装備・カスタムスキル）である。
組み合わせは2026-10-31にこちらで決めた仮の値である（[レシピの決め方](../../features/synthesis.md#レシピの決め方)）。
この表の材料はサーバーの [recipes.ts](../../../server/src/features/workshop/recipes.ts) とクライアントの [WorkshopRecipes](../../../client/Assets/Baryonyx/Features/Workshop/Runtime/WorkshopRecipes.cs)、名前と☆は [EquipmentCatalog](../../../client/Assets/Baryonyx/Features/Equipment/Runtime/EquipmentCatalog.cs) の写しで、正本はコードである（2026-11-02、ユーザーの判断）。直すときはコードを先に直し、同じ変更で表をそろえる。

| ID | 名前 | ☆ | 行き先 | 材料 |
|---|---|---|---|---|
| acorn-brooch | どんぐりのブローチ | ☆2 | ミストラ遺跡 | どんぐり×3 |
| dew-pendant | 朝露のペンダント | ☆3 | ミストラ遺跡 | 朝露の小瓶×2・苔の綿×1・薬草×1 |
| anchor-bangle | 錨の腕輪 | ☆2 | 港町ポルトリア | 船乗りのロープ×2・貝殻のつぶて×1 |
| gold-earring | 金貨の耳飾り | ☆3 | 港町ポルトリア | 墨つぼ×1・貝殻のつぶて×2・清めの海塩×1 |
| crystal-ring | 水晶の指輪 | ☆3 | グラシエラ氷窟 | 霜の結晶×1・氷柱のかけら×3 |
| leech-bell | ヒルよけの鈴 | ☆2 | しじまの沼 | 蓮の根×2・鬼火の灯心×1 |
| frog-crown | カエル王の冠 | ☆3 | しじまの沼 | 沼の毒水×1・蓮の根×2・泥だんご×1 |
| gargoyle-eye | ガーゴイルの石眼 | ☆3 | アルマジャ王墓 | 魔導書の切れ端×1・砂時計の砂×1・サボテンの実×2 |
| bone-ring | 骨の指輪 | ☆2 | 月影の墓所 | 墓前のろうそく×2・月見草の蜜×1 |
| moonstone-pendant | 月長石のペンダント | ☆3 | 月影の墓所 | 聖水×1・月見草の蜜×3 |
| magma-ring | 溶岩の指輪 | ☆3 | ヴォルガン火山 | 炎尾の尾羽×1・溶岩のつぶて×2・火とかげのしっぽ×1 |
| snowflake-brooch | 雪の結晶のブローチ | ☆2 | 白嶺ネーヴェ | 雪割り草×3 |
| foxtail-charm | つらら尾の房飾り | ☆3 | 白嶺ネーヴェ | 水晶角のかけら×1・雪割り草×2・固い雪玉×1 |

☆1の苔玉のお守り・貝殻の首飾り・ペンギンの羽飾り・スカラベの護符・熾火のお守りと、主の物（守り手の葉冠・大ダコの黒真珠・トロルの心臓石・大ワニの牙飾り・王の黄金仮面・死霊の印章・黒曜石の冠・霧竜の瞳）はレシピを持たない。

## 記入する項目

| 項目 | 初期の扱い |
|---|---|
| 効果（底上げするステータスと量、ちょっとした効果） | 個別値は未決。パッシブスキルのように、付けたキャラのステータスの底上げや、ちょっとした効果を与える（[アクセサリー](../../features/equipment.md#アクセサリー)） |
| レア度・倍率の上限 | 個別値は未決。レア度は☆1〜☆5で種類ごとに決め（[レア度](../../features/inventory.md#レア度)）、1つずつ倍率と品質を持つ（[品質](../../features/inventory.md#品質)） |
| 属性 | [属性](#属性)の表に書く。決まりは[装備の属性](../../features/equipment.md#装備の属性)に従う。追加効果の中身と値は未決 |
| 入手先・合成での用途 | 入手先は[冒険のドロップ](../../features/drops.md#行き先ごとの一覧)、合成の材料は[合成のレシピ](#合成のレシピ)にある |
| 重ね付け・パッシブスキルとの関係 | 未決 |
| 形状・材質・配色・装飾・発光 | 未決 |
| 同系列のアクセサリーと区別する特徴 | 未決 |

## 追加方法

[個別データの作り方](../README.md#個別データの作り方)に従う。
