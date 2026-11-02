---
id: enemy-skill-barnacle-crab-crusher
type: definition
status: 候補
updated: 2026-11-02
name: 大ばさみ砕き
category: enemy-skills
---

# 大ばさみ砕き

[敵の技の定義](README.md) / [関連仕様](../../features/combat-enemy-behavior.md)

フジツボ大ガニの固有技。大きなはさみで、構えた守りごと叩き砕く。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-19、ユーザーの依頼（敵50体を試作し、文書カタログ・クライアント・サーバーにそろえる）で、[フジツボ大ガニ](../enemies/enemy-barnacle-crab.md)の技として作った |
| 役割・説明 | 味方のブロックを大きく削る単体攻撃 |
| 関連する仕様・個別ID | 使う敵：[フジツボ大ガニ](../enemies/enemy-barnacle-crab.md)。[敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
| 実装・ゲームマスタの場所 | [CombatEnemyBehaviors](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatEnemyBehaviors.cs) の、使う敵の `Moves`。演出は、名前から選ぶ種類ごとの形（[BattleSkillVfx.Enemies](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleSkillVfx.Enemies.cs) の `EnemyBlowOf`） |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 固有技（[フジツボ大ガニ](../enemies/enemy-barnacle-crab.md)） | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 攻撃・味方単体（ブロックの最も多い味方を狙う） | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | 物理・打 | [記入する項目](README.md#記入する項目) | 候補 |
| 効果 | 物攻の100%のダメージ。標的のブロックには2倍のダメージを与え、ブロックが尽きた残りは通常のダメージとして通す | [記入する項目](README.md#記入する項目) | 仮 |
| 予告 | 攻撃 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [フジツボ大ガニ](../enemies/enemy-barnacle-crab.md) | 各敵の定義の「技」の表 | 候補 |

ブロックを消す技（[牙のかち上げ](enemy-skill-stump-tusk-toss.md)など）と違い、ブロックを2倍の速さで削る。厚いブロックなら残ることもある。

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | がしゃん、砕ける |
| 本体の動き | はさみを高く振り上げ、標的へ振り下ろす |
| 溜め | はさみが閉じて、縁が赤く光る |
| 発生 | 標的の前のブロックの膜に大きなひびが走って砕け、続けて標的に重い衝撃の輪が出る |
| 余韻 | 砕けたブロックの破片とフジツボの殻のかけらが床に落ちて消える |
| 色 | 赤、白、灰 |
| 格 | 中 |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat/implementation.md#未決事項)の数値が決まったら見直す |
| ブロックへの2倍のダメージ | 新しい仕組み。ブロックへのダメージと残りの通し方の計算順（防御で減らす前か後か）と、予告での見せ方を決める |
| 実装 | 戦闘で使う。ブロックへの2倍のダメージは入れず、ブロックの最も多い味方への通常の攻撃として当てる（[敵の行動の仮設定](../../features/combat-enemy-behavior.md)）。写すときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-19 | 固有技として定義を作った | ユーザーの依頼 | 使う敵：[フジツボ大ガニ](../enemies/enemy-barnacle-crab.md) |
| 2026-11-02 | スキルとdocの整理で、今のコードに合わせて「実装・ゲームマスタの場所」と「実装」を直した | 根拠：CombatEnemyBehaviors の `Moves` にこの技があり、戦闘で使う | [敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
