---
id: feature-stage-progression
type: specification
status: 一部確定
updated: 2026-11-02
---

# 探索とダンジョンの進行

[機能索引](README.md) / [ゲーム概要](../game/overview.md) / [戦闘システム](combat.md) / 根拠：[2026-09-27の企画見直し](../game/redesign-2026-09-27.md)

2026-09-15にユーザーが提示した「てくてくダンジョン｜探索・進行・戦闘仕様」の探索と進行を基に、2026-09-27の企画見直しでSlay the Spireのような分岐ルートの冒険へ変更した。
負けたら1階からやり直す構成になり、進捗を持ち越す旧方針は[変更と判断の記録](stage-progression-history.md)へ移した。
[節の区分](../rules/documentation-policy.md#mdの形式と状態)を区別し、[初期企画](../game/brief.md)にあった危険度選択中心の進行は現行ルールへ混ぜない。

仕様は、次のファイルに分けて置く（2026-11-02に分けた。読むときは、必要なファイルだけを開く）。

| ファイル | 主な内容 |
|---|---|
| [冒険の進み方](stage-progression/run.md) | 分岐ルートの冒険、冒険の終了と獲得物の保持、中断と再開、行き先を選ぶ情報、探索の地図の見た目、マス単位の操作、道とマスの数、HPの持ち越し、冒険中の状態の保存先、成長の記録、行き先で敵の情報を見せる候補 |
| [道とマスの種類・中身](stage-progression/rooms.md) | マスの種類、道の作り方と決まり、マスの中身、休憩の鍛える、歩数の扉の報酬 |
| [戦闘後の強化](stage-progression/boons.md) | 戦闘に勝ったあとの強化の決まりと、段階・系統・候補の並べ方・敵の強さで変える割合・重ね方・強さの見込み |
| [行き先と敵・解放・クリア回数の報酬](stage-progression/destinations.md) | 街とダンジョン、ダンジョンの解放、クリア回数の報酬とその例、行き先と敵、戦闘の編成、段階的な解放、アセットを増やしすぎない構成 |
| [行き先の戦闘の試し](stage-progression/battle-trials.md) | 強い魔物・最奥の間などの倍率を決めた試しと、自動対戦での測り直しの記録 |
| [未決事項と実装との対応](stage-progression/status.md) | 未決事項、確認待ち、実装との対応 |
| [前の街道の世界地図](stage-progression-world-map.md) | 前の街道（`WorldMap`。2026-10-29から入口なし）の構想・マス・宝箱・戦闘・試作 |

## 関連する仕様とデータ

[戦闘](combat.md)・[育成](progression.md)・[装備](equipment.md)・[画面](screens.md)・[セーブ](accounts-save.md)・[アート方針](../art/direction.md)

[ステージ定義](../catalog/stages/README.md)・[ドロップ](../catalog/loot-tables/README.md)・[デイリー変異の候補](daily-rules.md)

## 変更と判断の記録

経緯は[探索とダンジョンの進行の変更と判断の記録](stage-progression-history.md)に置く。新しい記録はそのファイルに追記する（2026-10-25に移した）。
