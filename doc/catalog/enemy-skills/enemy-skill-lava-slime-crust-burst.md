---
id: enemy-skill-lava-slime-crust-burst
type: definition
status: 候補
updated: 2026-11-02
name: 殻はじけ
category: enemy-skills
---

# 殻はじけ

[敵の技の定義](README.md) / [関連仕様](../../features/combat-enemy-behavior.md)

溶岩スライムの固有技。溜めた熱で黒い殻をはじけさせ、溶岩を味方全体へ浴びせる。殻が開いた体は無防備になる。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-19、ユーザーの依頼（敵50体を試作し、文書カタログ・クライアント・サーバーにそろえる）で、[溶岩スライム](../enemies/enemy-lava-slime.md)の技として作った |
| 役割・説明 | 全体への魔法・炎の攻撃と、自分の防御ダウン |
| 関連する仕様・個別ID | 使う敵：[溶岩スライム](../enemies/enemy-lava-slime.md)。[敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
| 実装・ゲームマスタの場所 | [CombatEnemyBehaviors](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatEnemyBehaviors.cs) の、使う敵の `Moves`。演出は、名前から選ぶ種類ごとの形（[BattleSkillVfx.Enemies](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleSkillVfx.Enemies.cs) の `EnemyBlowOf`） |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 固有技（[溶岩スライム](../enemies/enemy-lava-slime.md)） | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 攻撃・味方全体（自分に弱体） | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | 魔法・炎 | [記入する項目](README.md#記入する項目) | 候補 |
| 効果 | 属攻の80%のダメージ。そのあと、自分に防御ダウン2ターン | [記入する項目](README.md#記入する項目) | 仮 |
| 予告 | 全体攻撃 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [溶岩スライム](../enemies/enemy-lava-slime.md) | 各敵の定義の「技」の表 | 候補 |

火の息（全体に50%とやけど）より威力が高い代わりに、自分の守りが下がる技である。プレイヤーは、殻が開いた2ターンに攻撃を集められる。

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | ばきっ、噴き出す |
| 本体の動き | 体をふくらませてから、一気に平たく潰れる |
| 溜め | 殻のひびが一斉に明るくなり、体がふくらむ |
| 発生 | 殻の板が外へ弾け、溶岩のしぶきが弧を描いて味方全体に降る |
| 余韻 | 防御ダウンのあいだ、殻の板が少し浮いたまま残り、ひびが大きく開いて光る。殻を閉じ直すと元に戻る |
| 色 | 橙、黄、黒 |
| 格 | 中 |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat/implementation.md#未決事項)の数値が決まったら見直す |
| 敵に付く防御ダウン | 敵が自分に弱体を付ける仕組み。カードの防御ダウン（受けるダメージ+25%）と同じ効果にする案（[敵の行動の仮設定](../../features/combat-enemy-behavior.md)） |
| 実装 | 戦闘で使う。自分への防御ダウンは入れず、ダメージだけを当てる（[敵の行動の仮設定](../../features/combat-enemy-behavior.md)）。写すときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-19 | 固有技として定義を作った | ユーザーの依頼 | 使う敵：[溶岩スライム](../enemies/enemy-lava-slime.md) |
| 2026-11-02 | スキルとdocの整理で、今のコードに合わせて「実装・ゲームマスタの場所」と「実装」を直した | 根拠：CombatEnemyBehaviors の `Moves` にこの技があり、戦闘で使う | [敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
