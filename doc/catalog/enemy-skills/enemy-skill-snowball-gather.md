---
id: enemy-skill-snowball-gather
type: definition
status: 候補
updated: 2026-11-02
name: ころがり太り
category: enemy-skills
---

# ころがり太り

[敵の技の定義](README.md) / [関連仕様](../../features/combat-enemy-behavior.md)

雪玉ころんの固有技。雪原を転がって雪を集め、欠けた体を戻しながらひと回り大きくなる。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-19、ユーザーの依頼（敵50体を試作し、文書カタログ・クライアント・サーバーにそろえる）で、[雪玉ころん](../enemies/enemy-snowball.md)の技として作った |
| 役割・説明 | 自分の回復と、次の攻撃の強化 |
| 関連する仕様・個別ID | 使う敵：[雪玉ころん](../enemies/enemy-snowball.md)。[敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
| 実装・ゲームマスタの場所 | [CombatEnemyBehaviors](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatEnemyBehaviors.cs) の、使う敵の `Moves`。攻撃しない技のため、演出は技の名前と効果の文字・数字だけ（[BattleInspectView.EnemyMoves](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.EnemyMoves.cs) の `Support`） |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 固有技（[雪玉ころん](../enemies/enemy-snowball.md)） | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 回復・自分（強化を兼ねる） | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | ダメージなし | [記入する項目](README.md#記入する項目) | 候補 |
| 効果 | 属攻の80%のHPを回復。次に使う攻撃の技の威力を1.5倍にする | [記入する項目](README.md#記入する項目) | 仮 |
| 予告 | 回復・強化 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [雪玉ころん](../enemies/enemy-snowball.md) | 各敵の定義の「技」の表 | 候補 |

共通技の傷ふさぎ（回復）と力を溜める（次の攻撃の強化）を1回で行う技であり、転がって大きくなる雪玉の習性を表すため固有技にした。回復にリジェネは付かない。

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | ころころ、ふくらむ |
| 本体の動き | 左右に小さく2回傾いて転がるように揺れ、ひと回り大きくなって止まる |
| 溜め | 足元の雪面から白い雪の粒が体へ吸い寄せられる |
| 発生 | 体の縁に雪の粒が付いて輪郭が一段太り、回復の数字を出す |
| 余韻 | 体の上に細かな雪の粒が舞い落ちる。揺れは使わない |
| 色 | 白、水色 |
| 格 | 小 |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat/implementation.md#未決事項)の数値が決まったら見直す |
| 大きくなった見た目 | 次の攻撃まで絵を大きいまま残すか、演出の間だけにするか |
| 実装 | 戦闘で使う。効果のうち戦闘にまだないもの（味方に付く状態など）は写していない（[敵の行動の仮設定](../../features/combat-enemy-behavior.md)）。写すときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-19 | 固有技として定義を作った | ユーザーの依頼 | 使う敵：[雪玉ころん](../enemies/enemy-snowball.md) |
| 2026-11-02 | スキルとdocの整理で、今のコードに合わせて「実装・ゲームマスタの場所」を直した | 根拠：CombatEnemyBehaviors の `Moves` にこの技があり、戦闘で使う | [敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
