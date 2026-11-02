---
id: enemy-skill-rock-troll-boulder-toss
type: definition
status: 候補
updated: 2026-11-02
name: 肩石投げ
category: enemy-skills
---

# 肩石投げ

[敵の技の定義](README.md) / [関連仕様](../../features/combat-enemy-behavior.md)

岩肌トロルの固有技。肩に埋まった岩をもぎ取って投げつけ、砕けた破片を周りへ飛ばす。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-19、ユーザーの依頼（敵50体を試作し、文書カタログ・クライアント・サーバーにそろえる）で、[岩肌トロル](../enemies/enemy-rock-troll.md)の技として作った |
| 役割・説明 | 単体への攻撃と、ほかの味方への小さな飛び散り |
| 関連する仕様・個別ID | 使う敵：[岩肌トロル](../enemies/enemy-rock-troll.md)。[敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
| 実装・ゲームマスタの場所 | [CombatEnemyBehaviors](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatEnemyBehaviors.cs) の、使う敵の `Moves`。演出は、名前から選ぶ種類ごとの形（[BattleSkillVfx.Enemies](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleSkillVfx.Enemies.cs) の `EnemyBlowOf`） |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 固有技（[岩肌トロル](../enemies/enemy-rock-troll.md)） | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 攻撃・味方単体（ほかの味方にも当たる） | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | 物理・打 | [記入する項目](README.md#記入する項目) | 候補 |
| 効果 | 標的に物攻の80%、ほかの味方に物攻の30%のダメージ | [記入する項目](README.md#記入する項目) | 仮 |
| 予告 | 攻撃・全体攻撃 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [岩肌トロル](../enemies/enemy-rock-troll.md) | 各敵の定義の「技」の表 | 候補 |

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | ぶん、ばらっ |
| 本体の動き | 体を右へひねって肩の岩をもぎ取るように傾け、左へ大きく振る |
| 溜め | 肩の岩の一つが絵から浮き上がる |
| 発生 | 岩が弧を描いて標的に当たり、砕けて破片がほかの味方へ飛び散る。標的は大きく、ほかの味方は小さく揺らす |
| 余韻 | 砂ぼこりが床に残って晴れる |
| 色 | 灰茶、灰青、白 |
| 格 | 中 |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat/implementation.md#未決事項)の数値が決まったら見直す |
| 飛び散る攻撃 | 新しい仕組み。単体と全体に一度に当てる技の予告の出し方 |
| 実装 | 戦闘で使う。効果のうち戦闘にまだないもの（味方に付く状態など）は写していない（[敵の行動の仮設定](../../features/combat-enemy-behavior.md)）。写すときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-19 | 固有技として定義を作った | ユーザーの依頼（敵50体を試作し、文書カタログ・クライアント・サーバーにそろえる） | 使う敵：[岩肌トロル](../enemies/enemy-rock-troll.md) |
| 2026-11-02 | スキルとdocの整理で、今のコードに合わせて「実装・ゲームマスタの場所」を直した | 根拠：CombatEnemyBehaviors の `Moves` にこの技があり、戦闘で使う | [敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
