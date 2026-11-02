---
id: art-hd2d-stage
type: specification
status: 一部確定
updated: 2026-11-02
---

# 3Dの舞台（HD-2D）

[アート索引](README.md) / 根拠：[アート方針](direction.md)

Top・Home・戦闘画面は、3Dの地面と背景にドット絵のキャラを立たせ、3Dの光・影・霧とポストプロセスで見せるHD-2Dの構成で描く（経緯は[変更と判断の記録](hd2d-stage-history.md)）。
この文書は、その構成・カメラ・光・ポストプロセス・テクスチャ・描く順番の値の正本である。
作業の進め方とつまずいた点は、[hd2d-stage-set](../../.agents/skills/hd2d-stage-set/SKILL.md)スキル（カメラ・光・レンズ・停止中の表示は[camera-light.md](../../.agents/skills/hd2d-stage-set/references/camera-light.md)、地面・遠景・小物・草・汚しは[stage-building.md](../../.agents/skills/hd2d-stage-set/references/stage-building.md)・[set-dressing.md](../../.agents/skills/hd2d-stage-set/references/set-dressing.md)）に記す。

節ごとの仕様は、次のファイルに分けて置く（2026-11-02に分けた。読むときは、必要な節のファイルだけを開く）。

| 話題 | ファイル | 主な内容 |
|---|---|---|
| 方針・画面の構成・カメラとレンズ | [stage-view.md](hd2d-stage/stage-view.md) | 確定している方針、画面ごとの構成、キャラの立たせ方、停止中とSceneタブの表示、カメラの動き、レンズと色（ポストプロセス）、描く順番 |
| 小物とテクスチャ | [textures.md](hd2d-stage/textures.md) | 小物と汚し、テクスチャ（作り方・寸法・ドットの細かさ・ファイル一覧） |
| 戦闘の背景 | [battle-backgrounds.md](hd2d-stage/battle-backgrounds.md) | 戦闘の背景の10種類、置き方、光芒と光だまり、霧と煙 |
| 光と炎、実装との対応 | [light-fire.md](hd2d-stage/light-fire.md) | 光と炎（主光源・環境光・木漏れ日・光の筋・炎・火の粉・煙・草と葉）、実装との対応、未決事項 |

## 変更と判断の記録

経緯は[3Dの舞台（HD-2D）の変更と判断の記録](hd2d-stage-history.md)に置く。新しい記録はそのファイルに追記する（2026-10-25に移した）。
