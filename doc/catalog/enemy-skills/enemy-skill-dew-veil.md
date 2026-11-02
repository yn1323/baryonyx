---
id: enemy-skill-dew-veil
type: definition
status: 候補
updated: 2026-11-02
name: 露の膜
category: enemy-skills
---

# 露の膜

[敵の技の定義](README.md) / [関連仕様](../../features/combat-enemy-behavior.md)

朝露ぷるんの固有技。体の水を薄く伸ばし、自分と仲間を朝露の膜で包んで、受けるダメージを和らげる。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-19、ユーザーの依頼（敵50体を試作し、文書カタログ・クライアント・サーバーにそろえる）で、[朝露ぷるん](../enemies/enemy-dew-slime.md)の技として作った |
| 役割・説明 | 自分と仲間全体の守りの強化 |
| 関連する仕様・個別ID | 使う敵：[朝露ぷるん](../enemies/enemy-dew-slime.md)。[敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
| 実装・ゲームマスタの場所 | [CombatEnemyBehaviors](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatEnemyBehaviors.cs) の、使う敵の `Moves`。攻撃しない技のため、演出は技の名前と効果の文字・数字だけ（[BattleInspectView.EnemyMoves](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.EnemyMoves.cs) の `Support`） |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 固有技（[朝露ぷるん](../enemies/enemy-dew-slime.md)） | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 強化・自分と仲間全体（敵全体） | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | ダメージなし | [記入する項目](README.md#記入する項目) | 候補 |
| 効果 | プロテクト2ターン（受けるダメージ-25%） | [記入する項目](README.md#記入する項目) | 仮 |
| 予告 | 強化 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [朝露ぷるん](../enemies/enemy-dew-slime.md) | 各敵の定義の「技」の表 | 候補 |

ブロック（[身を固める](enemy-skill-brace.md)）は決まった量を先に引くが、この技は受けるダメージの割合を下げ、仲間全体に掛かる。
敵にプロテクトを付ける技は、ほかに自分だけに付ける[霜の鎧毛](enemy-skill-frost-ape-rime-coat.md)がある。

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | ぷるん、ひんやり |
| 本体の動き | 縦に伸びてから横へつぶれ、元の形へ戻る |
| 溜め | 双葉の先に小さな水の玉がふくらむ |
| 発生 | 水の玉が割れ、細い水の筋が敵それぞれへ弧を描いて飛び、体の縁に沿って水色の薄い膜を張る |
| 余韻 | 膜の上を白い光の点がひとつ流れて消える。膜は縁だけ薄く残り、HPと状態の表示を隠さない |
| 色 | 水色、白 |
| 格 | 小 |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 持続ターンは仮。[戦闘の未決事項](../../features/combat/implementation.md#未決事項)の数値が決まったら見直す |
| 敵に付くプロテクト | 今は、カードの状態と同じく味方ターンの開始ごとに持続ターンが減る（[カードの状態の仮設定](../../features/combat/cards.md#カードの状態の仮設定)）。敵のターンで数えるかを決める |
| 実装 | 戦闘で使う（[敵の行動の仮設定](../../features/combat-enemy-behavior.md)） |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-19 | 固有技として定義を作った | ユーザーの依頼 | 使う敵：[朝露ぷるん](../enemies/enemy-dew-slime.md) |
| 2026-11-02 | スキルとdocの整理で、今のコードに合わせて「実装・ゲームマスタの場所」と「実装」を直した。「敵に付くプロテクト」に今の数え方を書き、敵にプロテクトを付けるほかの技に霜の鎧毛を挙げた | 根拠：CombatEnemyBehaviors の `Moves` にこの技があり、戦闘で使う。攻撃しない技は `EnemyBlowOf` の形を使わない | [敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
