---
id: feature-world-atlas-status
type: specification
status: 一部確定
updated: 2026-11-02
---

# 入れ子の地図：未決事項と実装との対応

[入れ子の地図](../world-atlas.md)

## 未決事項

| 項目 | 決めること |
|---|---|
| 前の街道との関係 | 前の街道の宝箱・関所のボス・サーバーへの保存を、入れ子の地図へ移すか（Homeの行き先カードは2026-10-28に入れ子の地図へ替えた。戦闘は2026-11-01に入れ子の地図からも開くようにした） |
| 戦闘のあと | 負けたマスで戦い直す手段、勝ったときにもらう物、マスの設定の項目（[地図エディター](../screens/debug-rooms.md#地図エディター)の「これから設定できるようにするもの」） |
| マスの中身 | 出来事の割合と置き方を試しの配置から決め直すか。日週月の復活を、どの地図のマスに置くか（[計画](../../plans/2026-10-27-world-map-rune-squares.md#仕様案)）。何もないマスで隠し宝箱が出る形にするか（2026-10-29、ユーザーの案。割合と合わせて後で相談する） |
| マスの値段 | 地図ごとの推奨Lv（仮）と、全部で約66万ルーンになる値段を、1日に地図へ回すルーンと合わせて決め直すか |
| 地域の境 | オーロ砂漠以外の6地域の境を、地形に合わせて引き直すか |
| 町と名所の絵 | 空き地に置くドット絵の一覧と大きさ |
| 進み具合の保存 | 今いる地図・マスと、出来事を見たかを、サーバーに置くか（開けたマスは2026-10-29からサーバーに置く） |
| 背景の容量 | 背景を全部入れたAPKの大きさと、端末での読み込みの速さ |

## 実装との対応

- 地図の配置：[WorldAtlasLayout](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/WorldAtlasLayout.cs)
- 地図の切り替えと演出：[WorldAtlasNavigator](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/WorldAtlasNavigator.cs)
- 地図の画面（前の街道と共通）：[WorldMapView](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/WorldMapView.cs)（`Layout`・`StartZoom`・`StartAt`・`FrameAt`・`Arrived`）
- マスの出来事の窓：[WorldMapEventWindow](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/WorldMapEventWindow.cs)・[WorldMapEventWindowAssets](../../../client/Assets/Baryonyx/Features/WorldMap/Editor/WorldMapEventWindowAssets.cs)
- マスに置いた出来事：[WorldAtlasLayout](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/WorldAtlasLayout.cs) の `EventOf`・`FormOf`・[WorldMapView](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/WorldMapView.cs) の `NodeEvents`・`NodeEventForms`・`EventSprites`・`TryEventAt`・`EventFormAt`
- 出来事の見本：[Events/](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/Events/WorldEvents.cs)（種類ごとの中身と文面。賭けは [WorldEventDice](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/Events/WorldEventDice.cs)・[WorldEventChestRun](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/Events/WorldEventChestRun.cs)、仲間の助けは [WorldMapEventWindow](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/WorldMapEventWindow.cs) の `Helped`）・[WorldEventPanelAssets](../../../client/Assets/Baryonyx/Features/WorldMap/Editor/WorldEventPanelAssets.cs)（配置）
- 戦闘の設定：[WorldAtlasEncounters](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/WorldAtlasEncounters.cs)（言葉を読む `Read`、敵を引く `Draw`、地図エディターの説明 `Describe`）・[WorldAtlasBattle](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/WorldAtlasBattle.cs)（戦闘の画面へ渡す敵）・[WorldAtlasNavigator](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/WorldAtlasNavigator.cs) の `BattleAt`・[WorldBootstrap](../../../client/Assets/Baryonyx/App/Runtime/WorldBootstrap.cs)・[BattleBootstrap](../../../client/Assets/Baryonyx/App/Runtime/BattleBootstrap.cs)・[WorldMapBattleFlow](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/WorldMapBattleFlow.cs)（街道と共通の戦闘の流れ）
- 地図エディター：[WorldEventLab](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/WorldEventLab.cs)（付け替えは `Assign`、設定は `SetEnemies`・`SetLevel`・`NextStrength`、地図は [WorldMapView](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/WorldMapView.cs) の `Picking`・`SetEvent`・`LookAt`）・[WorldEventLabSave](../../../client/Assets/Baryonyx/Features/WorldMap/Editor/WorldEventLabSave.cs)（Editorでの保存）・[WorldEventLabAssets](../../../client/Assets/Baryonyx/Features/WorldMap/Editor/WorldEventLabAssets.cs)・[WorldEventLabSceneSetup](../../../client/Assets/Baryonyx/App/Editor/WorldEventLabSceneSetup.cs)
- マスを開くルーン：[WorldAtlasSession](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/WorldAtlasSession.cs)（読み込み・1マスの支払い（`OptimisticState` と `SaveQueue` による楽観的更新）・受けなかった支払いの知らせ `Refused`・オンオフ・確認を省く選択）・[WorldAtlasToll](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/WorldAtlasToll.cs)（残高・値札・確認の窓・止めるボタン）・[WorldAtlasTollAssets](../../../client/Assets/Baryonyx/Features/WorldMap/Editor/WorldAtlasTollAssets.cs)（配置）・[WorldMapGraph](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/WorldMapGraph.cs) の `CheapestPathTo`・[WorldMapView](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/WorldMapView.cs) の `Pay`・`Go`・`Stop`・`Resume`・`Unpaid`（受けなかった支払いを戻す）、[WorldAtlasNavigator](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/WorldAtlasNavigator.cs) の `TakeBack`、サーバーとの読み書きは [WorldAtlasSource](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/WorldAtlasSource.cs)・[WorldAtlasApiClient](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/WorldAtlasApiClient.cs)、サーバーは [world-atlas/](../../../server/src/features/world-atlas/routes.ts)
- 画面とシーンの生成：[WorldAtlasAssets](../../../client/Assets/Baryonyx/Features/WorldMap/Editor/WorldAtlasAssets.cs)・[WorldMapScreenAssets](../../../client/Assets/Baryonyx/Features/WorldMap/Editor/WorldMapScreenAssets.cs)
- 入口：[SceneNames](../../../client/Assets/Baryonyx/App/Runtime/SceneNames.cs) の `WorldAtlas`・`WorldEventLab` と `DebugRooms`
- テスト：配置は [WorldAtlasLayoutTests](../../../client/Assets/Baryonyx/Features/WorldMap/Tests/EditMode/WorldAtlasLayoutTests.cs)、道と支払いは [WorldAtlasTollTests](../../../client/Assets/Baryonyx/Features/WorldMap/Tests/EditMode/WorldAtlasTollTests.cs)、切り替えと払って歩くこと・出来事の窓は [WorldAtlasTests](../../../client/Assets/Baryonyx/Features/WorldMap/Tests/PlayMode/WorldAtlasTests.cs)、地図エディターは [WorldEventLabSceneTests](../../../client/Assets/Baryonyx/Features/WorldMap/Tests/PlayMode/WorldEventLabSceneTests.cs)、戦闘の言葉は [WorldAtlasEncountersTests](../../../client/Assets/Baryonyx/Features/WorldMap/Tests/EditMode/WorldAtlasEncountersTests.cs)、戦闘の画面へ移ることは [WorldAtlasBattleTests](../../../client/Assets/Baryonyx/Features/WorldMap/Tests/PlayMode/WorldAtlasBattleTests.cs)、サーバーは [routes.test.ts](../../../server/src/features/world-atlas/routes.test.ts)
