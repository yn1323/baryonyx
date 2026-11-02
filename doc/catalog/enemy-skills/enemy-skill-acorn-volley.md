---
id: enemy-skill-acorn-volley
type: definition
status: 候補
updated: 2026-11-02
name: どんぐりつぶて
category: enemy-skills
---

# どんぐりつぶて

[敵の技の定義](README.md) / [関連仕様](../../features/combat-enemy-behavior.md)

どんぐり兵の固有技。槍で頭上の枝を叩き、仲間のどんぐりをばらばらと降らせる。どこに落ちるかは、どんぐり兵にも分からない。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-19、ユーザーの依頼（敵50体を試作し、文書カタログ・クライアント・サーバーにそろえる）で、[どんぐり兵](../enemies/enemy-acorn-soldier.md)の技として作った |
| 役割・説明 | 標的をランダムに選び直す、小さな物理攻撃の連続 |
| 関連する仕様・個別ID | 使う敵：[どんぐり兵](../enemies/enemy-acorn-soldier.md)。[敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
| 実装・ゲームマスタの場所 | [CombatEnemyBehaviors](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatEnemyBehaviors.cs) の、使う敵の `Moves`。演出は、名前から選ぶ種類ごとの形（[BattleSkillVfx.Enemies](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleSkillVfx.Enemies.cs) の `EnemyBlowOf`） |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 固有技（[どんぐり兵](../enemies/enemy-acorn-soldier.md)） | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 攻撃・味方からランダムに1人ずつ | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | 物理・打 | [記入する項目](README.md#記入する項目) | 候補 |
| 効果 | 物攻の40%のダメージを3回。1回ごとに、立っている味方からランダムに標的を選び直す。同じ味方に重なることもある | [記入する項目](README.md#記入する項目) | 仮 |
| 予告 | 全体攻撃（3回）。標的はランダムのため示さない | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [どんぐり兵](../enemies/enemy-acorn-soldier.md) | 各敵の定義の「技」の表 | 候補 |

[二段突き](enemy-skill-double-thrust.md)は同じ標的を続けて突くが、この技は1回ごとに標的が変わる。
[地響き](enemy-skill-tremor.md)のように全員へ確実に当たるのではなく、だれに何回当たるかが分からない。

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | ばらばら、こつん |
| 本体の動き | 槍を上へ突き上げるように、絵を少し上へ伸ばす |
| 溜め | 画面の上端の外で枝が揺れる音に合わせ、葉の粒が2〜3枚落ちる |
| 発生 | 茶色のどんぐりが上から1つずつ、標的の頭上へ落ちる。当たるたびに標的を小さく揺らし、ダメージの数字を出す |
| 余韻 | 当たったどんぐりが床で1回跳ねて転がり、消える |
| 色 | 茶色、こげ茶、緑 |
| 格 | 小 |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と回数は仮。[戦闘の未決事項](../../features/combat/implementation.md#未決事項)の数値が決まったら見直す |
| ランダムな標的 | 1回ごとに標的を選び直す仕組みは新しい。予告にランダムであることをどう見せるか。[挑発](../../features/combat/cards.md#カードの状態の仮設定)の味方がいるとき、全回をその味方が引き受けるか |
| 実装 | 戦闘で使う（[敵の行動の仮設定](../../features/combat-enemy-behavior.md)） |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-19 | 固有技として定義を作った | ユーザーの依頼 | 使う敵：[どんぐり兵](../enemies/enemy-acorn-soldier.md) |
| 2026-11-02 | スキルとdocの整理で、今のコードに合わせて「実装・ゲームマスタの場所」と「実装」を直した | 根拠：CombatEnemyBehaviors の `Moves` にこの技があり、戦闘で使う | [敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
