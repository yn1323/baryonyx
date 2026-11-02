# 武器の定義

[データ索引](../README.md) / [関連仕様](../../features/equipment.md)

個別内容と決定状態は、下の索引から各定義を参照する。
画像対象テンプレートを使う。
IDの接頭辞は `weapon-`、メタデータの `category` は `weapons` とする。
今の武器の種類・☆・効果・倍率の上限の仮の値は [EquipmentCatalog](../../../client/Assets/Baryonyx/Features/Equipment/Runtime/EquipmentCatalog.cs) にある（[装備の実装との対応](../../features/equipment.md#実装との対応)）。

## 個別データの索引

<!-- entries:start -->
登録された個別データはまだない。
<!-- entries:end -->

## 合成のレシピ

[合成](../../features/synthesis.md#レシピ)で作るときの材料である。決まりは[レシピ](../../features/synthesis.md#レシピ)・[出来上がる品質](../../features/synthesis.md#出来上がる品質)・[費用](../../features/synthesis.md#費用)に従う。
「（特別）」は、アイテムではない材料（決まった装備・カスタムスキル）である。
組み合わせは2026-10-31にこちらで決めた仮の値である（[レシピの決め方](../../features/synthesis.md#レシピの決め方)）。
この表の材料はサーバーの [recipes.ts](../../../server/src/features/workshop/recipes.ts) とクライアントの [WorkshopRecipes](../../../client/Assets/Baryonyx/Features/Workshop/Runtime/WorkshopRecipes.cs)、名前と☆は [EquipmentCatalog](../../../client/Assets/Baryonyx/Features/Equipment/Runtime/EquipmentCatalog.cs) の写しで、正本はコードである（2026-11-02、ユーザーの判断）。直すときはコードを先に直し、同じ変更で表をそろえる。

| ID | 名前 | ☆ | 行き先 | 材料 |
|---|---|---|---|---|
| iron-sword | 鉄の剣 | ☆2 | 港町ポルトリア | 貝殻のつぶて×2・船乗りのロープ×1 |
| war-hammer | 戦鎚 | ☆2 | 港町ポルトリア | 干し魚×1・貝殻のつぶて×1・船乗りのロープ×1 |
| flame-dagger | 炎のダガー | ☆4 | 港町ポルトリア | 鉄の剣×1（特別）・海賊のラム酒×1・墨つぼ×1・船乗りのロープ×2 |
| thunder-spear | 雷鳴の槍 | ☆3 | グラシエラ氷窟 | 氷柱のかけら×2・ツチボタルの灯×1・地底湖の清水×1 |
| frost-wand | 氷晶のワンド | ☆3 | グラシエラ氷窟 | 霜の結晶×1・地底湖の清水×2・氷柱のかけら×1 |
| reed-bow | 葦の長弓 | ☆2 | しじまの沼 | 蓮の根×2・泥だんご×1 |
| wisp-staff | 鬼火の杖 | ☆3 | しじまの沼 | 鬼火の灯心×2・蓮の根×1・泥だんご×1 |
| scorpion-bow | 砂蠍の弓 | ☆2 | アルマジャ王墓 | 砂サソリの毒針×2・サボテンの実×1 |
| tomb-khopesh | 王墓の鎌剣 | ☆3 | アルマジャ王墓 | 砂サソリの毒針×2・砂時計の砂×1・サボテンの実×1 |
| bone-axe | 骨の大斧 | ☆2 | 月影の墓所 | 墓前のろうそく×1・月見草の蜜×1・大鴉の羽根×1 |
| moonshade-staff | 月影の杖 | ☆4 | 月影の墓所 | 樫の杖×1（特別）・聖水×1・月見草の蜜×2・大鴉の羽根×1 |
| firebird-bow | 炎尾の弓 | ☆2 | ヴォルガン火山 | 火とかげのしっぽ×2・硫黄の粉×1 |
| magma-hammer | 溶岩の大槌 | ☆3 | ヴォルガン火山 | 溶岩のつぶて×3・硫黄の粉×1 |
| hailstone-staff | 雹の杖 | ☆3 | 白嶺ネーヴェ | 水晶角のかけら×1・固い雪玉×3 |
| glacier-greatsword | 氷河の大剣 | ☆4 | 白嶺ネーヴェ | 鉄の剣×1（特別）・水晶角のかけら×2・固い雪玉×2 |

☆1の木の剣・短弓・樫の杖はレシピを持たない。

## 記入する項目

| 項目 | 初期の扱い |
|---|---|
| 武器種・装備枠 | 未決。基本的にどのキャラでも装備できる（[装備](../../features/equipment.md)） |
| 参照するステータスと割合 | 固定値は持たない。個別値は未決。[装備の効果](../../features/equipment.md#装備の効果)に従う |
| レア度（☆1〜☆5）・Affix候補・固有効果・数値幅・倍率の上限 | 未決。レア度は種類ごとに決め、1つずつ倍率と品質を持つ（[品質](../../features/inventory.md#品質)） |
| 強化定義ID・入手テーブルとの関係 | 強化は[品質強化](../../features/enhancement.md#品質強化)、入手は[冒険のドロップ](../../features/drops.md#行き先ごとの一覧)に従う |
| 全体の輪郭・刃や先端・柄・長さ・重量感 | 未決 |
| 素材・加工・配色・紋章・装飾・使用痕 | 未決 |
| 構え・持つ位置・発光・攻撃時の見え方 | 未決 |
| 同系列武器と区別する特徴 | 未決 |

## 追加方法

[個別データの作り方](../README.md#個別データの作り方)に従う。
