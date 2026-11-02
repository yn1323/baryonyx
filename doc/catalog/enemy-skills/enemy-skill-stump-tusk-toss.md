---
id: enemy-skill-stump-tusk-toss
type: definition
status: 候補
updated: 2026-11-02
name: 牙のかち上げ
category: enemy-skills
---

# 牙のかち上げ

[敵の技の定義](README.md) / [関連仕様](../../features/combat-enemy-behavior.md)

切り株猪の固有技。反り返った牙を構えの下へ差し入れ、守りごとはね上げてから突き刺す。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-19、ユーザーの依頼（敵50体を試作し、文書カタログ・クライアント・サーバーにそろえる）で、[切り株猪](../enemies/enemy-stump-boar.md)の技として作った |
| 役割・説明 | 味方のブロックを消す単体攻撃 |
| 関連する仕様・個別ID | 使う敵：[切り株猪](../enemies/enemy-stump-boar.md)。[敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
| 実装・ゲームマスタの場所 | [CombatEnemyBehaviors](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatEnemyBehaviors.cs) の、使う敵の `Moves`。演出は、名前から選ぶ種類ごとの形（[BattleSkillVfx.Enemies](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleSkillVfx.Enemies.cs) の `EnemyBlowOf`） |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 固有技（[切り株猪](../enemies/enemy-stump-boar.md)） | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 攻撃・味方単体 | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | 物理・貫 | [記入する項目](README.md#記入する項目) | 候補 |
| 効果 | 標的のブロックをすべて消してから、物攻の100%のダメージ | [記入する項目](README.md#記入する項目) | 仮 |
| 予告 | 攻撃・弱体 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [切り株猪](../enemies/enemy-stump-boar.md) | 各敵の定義の「技」の表 | 候補 |

[かみつき](enemy-skill-bite.md)と威力は同じだが、ブロックで受けられない。
既存の共通技・固有技に、味方のブロックを消す技はない。
プレイヤーは、ブロックではなくプロテクトや回復で備えるか、標的の味方に挑発の味方を立てるかを選ぶ。

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | ずどん、はね上げる |
| 本体の動き | 頭をさらに低く沈め、標的の方へ短く突進してから、頭を下から上へ振り上げるように絵を傾ける |
| 溜め | 前足の下から土の粒が後ろへ跳ねる |
| 発生 | 標的のブロックの膜が下から割れて砕け、白い牙の弧の線が下から上へ走る。標的を一瞬上へ浮かせ、ダメージの数字を出す |
| 余韻 | 砕けた膜の破片と土の粒が落ちて消える |
| 色 | 白、土の茶色、赤茶 |
| 格 | 中 |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合は仮。[戦闘の未決事項](../../features/combat/implementation.md#未決事項)の数値が決まったら見直す |
| 味方のブロックを消す | 敵の技で味方のブロックを消す仕組みは新しい。プロテクトや無敵も消すか（案：消さない。ブロックだけ） |
| 実装 | 戦闘で使う。効果のうち戦闘にまだないもの（味方に付く状態など）は写していない（[敵の行動の仮設定](../../features/combat-enemy-behavior.md)）。写すときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-19 | 固有技として定義を作った | ユーザーの依頼 | 使う敵：[切り株猪](../enemies/enemy-stump-boar.md) |
| 2026-11-02 | スキルとdocの整理で、今のコードに合わせて「実装・ゲームマスタの場所」を直した | 根拠：CombatEnemyBehaviors の `Moves` にこの技があり、戦闘で使う | [敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
