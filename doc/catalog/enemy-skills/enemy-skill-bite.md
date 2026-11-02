---
id: enemy-skill-bite
type: definition
status: 候補
updated: 2026-11-02
name: かみつき
category: enemy-skills
---

# かみつき

[敵の技の定義](README.md) / [関連仕様](../../features/combat-enemy-behavior.md)

共通技。牙を持つ獣の基本の攻撃である。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-04、ユーザーの依頼（敵の技を増やし、共通化できるものも入れる）で、[苔むした狼](../enemies/enemy-moss-wolf.md)の技を共通技として作った |
| 役割・説明 | 単体への基本の物理攻撃 |
| 関連する仕様・個別ID | 使う敵：[苔むした狼](../enemies/enemy-moss-wolf.md)、[いばら蛇](../enemies/enemy-thorn-serpent.md)、[割れ壺の番](../enemies/enemy-cracked-urn.md)、[人食い宝箱](../enemies/enemy-chest-mimic.md)、[骨背の大ワニ](../enemies/enemy-bone-croc.md)、[包帯の山犬](../enemies/enemy-wrapped-jackal.md)、[砂鮫](../enemies/enemy-sand-shark.md)、[つらら尾の狐](../enemies/enemy-icicle-fox.md)、[火とかげ](../enemies/enemy-fire-lizard.md)、[霧の竜](../enemies/enemy-mist-dragon.md)。[敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
| 実装・ゲームマスタの場所 | [CombatEnemyBehaviors](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatEnemyBehaviors.cs) の、使う敵の `Moves`。演出は、名前から選ぶ種類ごとの形（[BattleSkillVfx.Enemies](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleSkillVfx.Enemies.cs) の `EnemyBlowOf`）。単体で開く戦闘画面のモックでは、苔むした狼の攻撃の名前と単体攻撃に使う（[BattleInspectAssets](../../../client/Assets/Baryonyx/Features/Combat/Editor/BattleInspectAssets.cs) の `Skill`） |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 共通技 | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 攻撃・味方単体 | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | 物理・貫 | [記入する項目](README.md#記入する項目) | 候補 |
| 効果 | 物攻の100%のダメージ | [記入する項目](README.md#記入する項目) | 仮 |
| 予告 | 攻撃 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [苔むした狼](../enemies/enemy-moss-wolf.md)、[いばら蛇](../enemies/enemy-thorn-serpent.md)、[割れ壺の番](../enemies/enemy-cracked-urn.md)、[人食い宝箱](../enemies/enemy-chest-mimic.md)、[骨背の大ワニ](../enemies/enemy-bone-croc.md)、[包帯の山犬](../enemies/enemy-wrapped-jackal.md)、[砂鮫](../enemies/enemy-sand-shark.md)、[つらら尾の狐](../enemies/enemy-icicle-fox.md)、[火とかげ](../enemies/enemy-fire-lizard.md)、[霧の竜](../enemies/enemy-mist-dragon.md) | 各敵の定義の「技」の表 | 候補 |
| 似合う敵 | 牙を持つ獣（狼、蛇、トカゲ） | 新しい敵へ付けるときの目安 | 候補 |

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | 鋭い、荒々しい |
| 本体の動き | 体を少し後ろへ引いて沈め、前へ跳んで戻る |
| 溜め | 体を沈め、目に光が点る |
| 発生 | 標的の上下から、閉じる2本の牙の弧（白い芯、赤い縁）を出す。短いヒットストップを入れる |
| 余韻 | 牙の弧が赤い残光になり、細りながら消える |
| 色 | 白、赤。目の光は使う敵の目の色 |
| 格 | 小 |

使う敵ごとの見た目の違いは、次のとおりである。

| 敵 | 色・材質・形の違い |
|---|---|
| [苔むした狼](../enemies/enemy-moss-wolf.md) | 目の光は黄緑 |
| [いばら蛇](../enemies/enemy-thorn-serpent.md) | 蔓の束の頭が鞭のように伸び、茶色のとげの牙の線でかみつく |
| [割れ壺の番](../enemies/enemy-cracked-urn.md) | 口のふちの白い牙でかみつき、黒紫の闇の粒がこぼれる |
| [人食い宝箱](../enemies/enemy-chest-mimic.md) | 牙の光は白と金にし、ふたと箱のふちの二列の牙が上下から閉じる形にする |
| [骨背の大ワニ](../enemies/enemy-bone-croc.md) | 上下から閉じる大きな顎の白い牙の線が出て、泥水のしぶきが散る。 |
| [包帯の山犬](../enemies/enemy-wrapped-jackal.md) | 包帯の垂れた口でかみつき、黄ばんだ包帯の切れ端と砂の粒が散る。 |
| [砂鮫](../enemies/enemy-sand-shark.md) | 砂から跳ね出て、何列も並んだ白い牙の顎が閉じ、砂のかたまりがこぼれる。 |
| [つらら尾の狐](../enemies/enemy-icicle-fox.md) | 細い鼻先で噛みつき、当たった所に水色の霜の粒が散る |
| [火とかげ](../enemies/enemy-fire-lizard.md) | 細長い口で噛みつき、火の粉が少し散る |
| [霧の竜](../enemies/enemy-mist-dragon.md) | 長い首を伸ばして牙で噛みつき、首の後ろに白い霧の尾を引く |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat/implementation.md#未決事項)の数値が決まったら見直す |
| 実装 | 戦闘で使う（[敵の行動の仮設定](../../features/combat-enemy-behavior.md)） |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-04 | 共通技として定義を作った | ユーザーの依頼 | 使う敵：[苔むした狼](../enemies/enemy-moss-wolf.md) |
| 2026-10-19 | 使う敵に、新しく作った敵を加えた | ユーザーの依頼（敵を50体に増やし、文書カタログ・クライアント・サーバーにそろえる） | 使う敵：[いばら蛇](../enemies/enemy-thorn-serpent.md)、[割れ壺の番](../enemies/enemy-cracked-urn.md)、[人食い宝箱](../enemies/enemy-chest-mimic.md)、[骨背の大ワニ](../enemies/enemy-bone-croc.md)、[包帯の山犬](../enemies/enemy-wrapped-jackal.md)、[砂鮫](../enemies/enemy-sand-shark.md)、[つらら尾の狐](../enemies/enemy-icicle-fox.md)、[火とかげ](../enemies/enemy-fire-lizard.md)、[霧の竜](../enemies/enemy-mist-dragon.md) |
| 2026-11-02 | スキルとdocの整理で、今のコードに合わせて「実装・ゲームマスタの場所」と「実装」を直した | 根拠：CombatEnemyBehaviors の `Moves` にこの技があり、戦闘で使う | [敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
