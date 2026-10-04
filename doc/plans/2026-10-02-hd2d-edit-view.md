---
id: plan-2026-10-02-hd2d-edit-view
type: reference
status: 記録
updated: 2026-10-02
---

# 停止中とSceneタブの見た目をPlay Mode中とそろえる

[計画の索引](README.md) / 仕様の正本：[3Dの舞台](../art/hd2d-stage.md#停止中の表示) / 前の計画：[Topと戦闘画面の屋外の舞台](2026-10-02-hd2d-outdoor-stages.md)

状態：完了（Editorでの実装と検証）

## 依頼

2026-10-02、ユーザーから次の指摘と依頼を受けた。

- Top・Home・BattleInspectが、Play Modeの前後で見た目が違いそうである。Play Mode中の見た目を、停止中にも見られるようにする。
- Sceneタブで見ると、3つのシーンが何も見えない。Sceneタブでも同じ見た目で見えるようにする。

## 原因

| 症状 | 原因 |
|---|---|
| 停止中にHomeの2Dの霧と光が見える | 2Dのときだけの部品を `CanvasRenderer.cull` で止めていたが、霧の層のマスクが毎フレーム戻し、光と火の粉の後から作られる絵も止まっていなかった |
| 停止中に火の粉・光の粒・蛍・砂ぼこりがない | Unityは選んだ粒子しか停止中に動かさない |
| 停止中は戦闘のHPバーと弱点が手前のUIとして描かれる | 戦闘の舞台のCanvasは、Play Modeの開始時にカメラを探して設定していた |
| Sceneタブで何も見えない | 手前のUI（Screen Space - Overlay）がSceneタブの原点に幅1920単位の板として描かれ、56mほどの舞台は板の隅の点にしか見えなかった |

## 実施した内容

1. 2Dのときだけの部品を、停止中は保存されない `CanvasGroup`（透明度0）で消すようにした。シーンは変更扱いにならず、Prefabの上書きにもならない。
2. 舞台のPrefabに、停止中も粒子を動かす `Hd2dParticlePreview` を付けた。
3. BattleInspectのシーンを作るときに、シーンのカメラを戦闘の舞台のCanvasに設定するようにした。
4. シーンを開いたときにSceneタブを舞台のカメラにそろえる `Hd2dSceneViewSync` を作った。霧・ポストプロセス・粒子・Always Refreshを有効にし、グリッドを消し、手前のUIをSceneタブでだけ隠す。メニュー `Baryonyx > HD-2D > Scene View Through Stage Camera` で戻せる。

## 完了条件と結果

| 完了条件 | 結果 |
|---|---|
| 停止中のGameビューがPlay Mode中と同じ見た目になる | 3シーンとも確認済み。手前のUIを一時的にカメラで描かせて撮り、Play Mode中の画面と比べた |
| Sceneタブで同じ見た目で見える | 3シーンとも確認済み（手前のUIはGameタブで見る） |
| EditModeとPlayModeのテスト | EditMode 264件、PlayMode 66件がすべて成功 |

## 残る違い

- カメラの揺れとすべり込み、点光源の揺らぎは、Play Mode中だけ動く。
- 戦闘の手札は、停止中はPrefabに焼き込んだ最初の手札、Play Mode中は配り直した手札になる。
- 手前のUIはSceneタブでは隠す。Hierarchyの目のアイコンで再表示できる。
