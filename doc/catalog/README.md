# 個別データの索引

[全体索引](../README.md) → データ分類 → 個別定義

ここには個別のキャラ・装備・敵などの設定を置く。
個別データの登録状況と確度は、分類内の生成索引で確認する。
分類を用意したことは、未決のシステムの採用を意味しない。

| 分類 | 内容 | 個別IDの接頭辞 |
|---|---|---|
| [キャラクター](characters/README.md) | 役割・固有スキル・パッシブスキル・装備できる属性 | `character-` |
| [敵](enemies/README.md) | 分類・役割・出現ステージID | `enemy-` |
| [敵の技](enemy-skills/README.md) | 共通技と固有技・種類と対象・使う敵 | `enemy-skill-` |
| [アイテム](items/README.md) | 道具・素材の分類・効果・レア度・入手先 | `item-` |
| [武器](weapons/README.md) | 武器種・装備枠・参照するステータス | `weapon-` |
| [防具](armor/README.md) | 防具種・装備枠・参照するステータス | `armor-` |
| [アクセサリー](accessories/README.md) | 効果（ステータスの底上げなど）・レア度 | `accessory-` |
| [スキル](skills/README.md) | スキル・固有スキルの分類・属性・コスト | `skill-` |
| [職業](classes/README.md) | 役割・得意不得意・キャラクターID | `class-` |
| [属性](attributes/README.md) | 魔法（炎・氷・雷）・物理（斬・打・貫）の分類 | `attribute-` |
| [状態異常・強化効果](effects/README.md) | 効果種別・対象・発動条件 | `effect-` |
| [パッシブ](passives/README.md) | 所持者・装備者・解放条件 | `passive-` |
| [装備Affix](affixes/README.md) | 付与対象の武器種・防具種・レア度 | `affix-` |
| [レア度](rarities/README.md) | ☆1〜☆5の段階と、分類ごとの扱い | `rarity-` |
| [通貨](resources/README.md) | 用途・入手先・消費先。素材はアイテムで定義する | `resource-` |
| [強化・合成](upgrades/README.md) | 対象・前提条件・段階・上限 | `upgrade-` |
| [報酬・ドロップテーブル](loot-tables/README.md) | 適用する敵・場所 | `loot-` |
| [ACTボーナス](step-bonuses/README.md) | カテゴリ・効果・枠ごとの効果量 | `step-bonus-` |
| [デイリー変異などの条件](stage-modifiers/README.md) | 採用する場合の変異の分類 | `modifier-` |
| [探索する場所・ステージ](stages/README.md) | 地域・部屋・分岐・解放条件 | `stage-` |
| [実績・ミッション・称号](achievements/README.md) | 種別・解放条件・達成条件 | `achievement-` |
| [世界の人物・地域・組織](world/README.md) | 人物・地域・組織などの種別 | `world-` |

## 個別データの作り方

1. 対象分類の記入項目と関連仕様を確認する。
2. [画像対象](templates/visual-entity.md)または[定義](templates/definition.md)のテンプレートを分類内へコピーする。
3. IDと同じ名前のMDファイルにし、メタデータのID・type・category・name・status・updatedを更新する。
4. 根拠・確定事項・候補・未決事項を分けて記す。画像化する対象は[比較索引](../art/visual-index.md)と類似対象の詳細を読む。
5. [索引生成・検査](../tools/README.md)を行う。

ルール・計算式は[機能仕様](../features/README.md)が所有し、個別データからリンクする。
データの実行形式と生成元は未決であり、このMD群をそのままゲームが読み込むとは定めていない。
数値を実装へ移す際は、[文書管理方針](../rules/documentation-policy.md)に従い正本を一つにする。
