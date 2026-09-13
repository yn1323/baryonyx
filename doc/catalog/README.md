# 個別データの索引

[全体索引](../README.md) → データ分類 → 個別定義

ここには個別のキャラ・装備・敵などの設定を置く。
個別データの登録状況と確度は、分類内の生成索引で確認する。
分類を用意したことは、未決のシステムの採用を意味しない。

| 分類 | 内容 | 個別IDの接頭辞 |
|---|---|---|
| [キャラクター](characters/README.md) | 役割・固定職業ID・加入方法 | `character-` |
| [敵](enemies/README.md) | 分類・役割・出現ステージID | `enemy-` |
| [アイテム](items/README.md) | 分類・用途・使用条件・対象 | `item-` |
| [武器](weapons/README.md) | 武器種・装備可能職業・装備枠 | `weapon-` |
| [防具](armor/README.md) | 防具種・装備枠・装備可能職業 | `armor-` |
| [技・魔法](skills/README.md) | 通常攻撃・アクティブなどの分類・使用者 | `skill-` |
| [職業](classes/README.md) | 役割・得意不得意・キャラクターID | `class-` |
| [属性・ダメージ種別](attributes/README.md) | 属性名・物理魔法などとの分類関係 | `attribute-` |
| [状態異常・強化効果](effects/README.md) | 効果種別・対象・発動条件 | `effect-` |
| [パッシブ](passives/README.md) | 所持者・装備者・解放条件 | `passive-` |
| [装備Affix](affixes/README.md) | 付与対象の武器種・防具種・レア度 | `affix-` |
| [レア度](rarities/README.md) | キャラ・武器・防具等の適用対象 | `rarity-` |
| [素材・通貨](resources/README.md) | 用途・入手先・消費先 | `resource-` |
| [強化・合成](upgrades/README.md) | 対象・前提条件・段階・上限 | `upgrade-` |
| [報酬・ドロップテーブル](loot-tables/README.md) | 適用する敵・ステージ・危険度・目標ID | `loot-` |
| [運動目標](goals/README.md) | 対象運動・指標・単位 | `goal-` |
| [危険度・デイリー変異の条件](stage-modifiers/README.md) | 危険度または変異としての分類 | `modifier-` |
| [ステージ](stages/README.md) | 地域ID・解放条件・推奨の強さ | `stage-` |
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
