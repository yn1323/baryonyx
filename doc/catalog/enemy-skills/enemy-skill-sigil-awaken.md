---
id: enemy-skill-sigil-awaken
type: definition
status: 候補
updated: 2026-11-02
name: 刻印の目覚め
category: enemy-skills
---

# 刻印の目覚め

[敵の技の定義](README.md) / [関連仕様](../../features/combat-enemy-behavior.md)

森の守り手の固有技。胸の刻印（守り手の核）が本来の力を取り戻す。森の守り手の激化の合図である。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-04、ユーザーの依頼（敵の技を増やす。その敵が持っていそうな技にする）で、[森の守り手](../enemies/enemy-forest-guardian.md)の技として作った |
| 役割・説明 | 激化の合図。攻撃を強め、行動パターンを後半へ切り替える |
| 関連する仕様・個別ID | 使う敵：[森の守り手](../enemies/enemy-forest-guardian.md)。[敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
| 実装・ゲームマスタの場所 | [CombatEnemyBehaviors](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatEnemyBehaviors.cs) の、使う敵の `Moves`。攻撃しない技のため、演出は技の名前と効果の文字・数字だけ（[BattleInspectView.EnemyMoves](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.EnemyMoves.cs) の `Support`） |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 固有技（[森の守り手](../enemies/enemy-forest-guardian.md)） | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 強化・自分 | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | ダメージなし | [記入する項目](README.md#記入する項目) | 候補 |
| 効果 | 戦闘の終わりまで攻撃力アップ。使った敵の行動パターンを後半へ切り替える | [記入する項目](README.md#記入する項目) | 仮 |
| 予告 | 強化 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [森の守り手](../enemies/enemy-forest-guardian.md) | 各敵の定義の「技」の表 | 候補 |

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | 目覚める、畏れ |
| 本体の動き | 体をゆっくり起こし、細かく震わせる |
| 溜め | 画面を少し暗くし（真っ黒にせず森の色を残す）、胸の刻印に光が集まる |
| 発生 | 刻印から放射状の光の筋と輪が広がり、体の岩の継ぎ目から淡い金の光が漏れる。画面を短く揺らし、葉が舞い上がる |
| 余韻 | 以後、刻印が鼓動のように明滅し続ける。明るさは予告・標的・HPバーを隠さない程度に抑える |
| 色 | 淡い金、白 |
| 格 | 大 |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat/implementation.md#未決事項)の数値が決まったら見直す |
| 実装 | 戦闘で使う。効果のうち戦闘にまだないもの（味方に付く状態など）は写していない（[敵の行動の仮設定](../../features/combat-enemy-behavior.md)）。写すときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-04 | 固有技として定義を作った | ユーザーの依頼 | 使う敵：[森の守り手](../enemies/enemy-forest-guardian.md) |
| 2026-11-02 | スキルとdocの整理で、今のコードに合わせて「実装・ゲームマスタの場所」を直した | 根拠：CombatEnemyBehaviors の `Moves` にこの技があり、戦闘で使う | [敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
