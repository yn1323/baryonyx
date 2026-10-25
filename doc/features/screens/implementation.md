---
id: feature-screens-implementation
type: specification
status: 一部確定
updated: 2026-10-25
---

# 画面一覧：画面と実装の対応

[画面一覧](../screens.md) / 根拠：[初期企画](../../game/brief.md)

## 実装との対応

シーンの一覧と、シーン・画面を作り直すメニューは[シーンと生成メニュー](../../rules/frontend-design.md#シーンと生成メニュー)にある。
画面ごとの実装の入口は次のとおりである。
編成の各パネルの入口は、[冒険者の一覧・スキル](../party.md#実装との対応)・[装備](../equipment.md#実装との対応)・[育成](../progression.md#実装との対応)、冒険の入口は[探索と進行](../stage-progression.md#実装との対応)に記す。

| 画面 | 入口 |
|---|---|
| Top | [起動時の連携と歩数の同期の実装の入口](../startup-sync.md#実装の入口)・[画面遷移の演出](../../../client/Assets/Baryonyx/Shared/UI/SceneTransition/SceneTransitionController.cs) |
| ホーム画面 | [Homeシーン](../../../client/Assets/Baryonyx/App/Scenes/Home.unity)・[Prefab生成](../../../client/Assets/Baryonyx/Features/Home/Editor/HomeScreenAssets.cs) |
| 編成・商会・神殿・旅の案内所 | [共通部品](../../../client/Assets/Baryonyx/Shared/UI/GuideMenu/GuideMenuView.cs)・[停止中の表示の切り替え](../../../client/Assets/Baryonyx/Shared/UI/EditModePreview/Editor/EditModePreviewer.cs)・[シーン生成](../../../client/Assets/Baryonyx/App/Editor/GuideSceneSetup.cs)・[編成の画面と仮データの生成](../../../client/Assets/Baryonyx/Features/Tavern/Editor/TavernScreenAssets.cs)・[神殿の召喚の生成](../../../client/Assets/Baryonyx/Features/Temple/Editor/TempleSummonAssets.cs) |
| 商会の画面いっぱいの形 | [札・行・2面の共通の生成](../../../client/Assets/Baryonyx/Features/Workshop/Editor/WorkshopPanelAssets.cs)・[持ち物の一覧](../../../client/Assets/Baryonyx/Features/Workshop/Runtime/WorkshopStock.cs) |
| 商会の売る | [パネル](../../../client/Assets/Baryonyx/Features/Workshop/Runtime/WorkshopSellView.cs)・[売却と保存](../../../client/Assets/Baryonyx/Features/Workshop/Runtime/WorkshopSession.cs)・[パネルの生成](../../../client/Assets/Baryonyx/Features/Workshop/Editor/WorkshopSellAssets.cs)・[API](../../../server/src/features/workshop/routes.ts)・[値段の定義](../../../server/src/features/workshop/catalog.ts)・[保存](../../../server/src/features/workshop/repository.ts) |
| 商会の合成 | [決まり](../../../client/Assets/Baryonyx/Features/Workshop/Runtime/WorkshopCraft.cs)・[パネル](../../../client/Assets/Baryonyx/Features/Workshop/Runtime/WorkshopCraftView.cs)・[パネルの生成](../../../client/Assets/Baryonyx/Features/Workshop/Editor/WorkshopCraftAssets.cs)・[API](../../../server/src/features/workshop/routes.ts)・[保存](../../../server/src/features/workshop/repository.ts) |
| 商会の買う | [品物と値段](../../../client/Assets/Baryonyx/Features/Workshop/Runtime/WorkshopGoods.cs)・[パネル](../../../client/Assets/Baryonyx/Features/Workshop/Runtime/WorkshopBuyView.cs)・[購入と保存](../../../client/Assets/Baryonyx/Features/Workshop/Runtime/WorkshopSession.cs)・[パネルの生成](../../../client/Assets/Baryonyx/Features/Workshop/Editor/WorkshopBuyAssets.cs)・[API](../../../server/src/features/workshop/routes.ts)・[値段の定義](../../../server/src/features/workshop/catalog.ts) |
| 旅の案内所のお宝 | [お宝の一覧と見つけた数](../../../client/Assets/Baryonyx/Features/TravelOffice/Runtime/TravelTreasures.cs)・[ボタンと窓](../../../client/Assets/Baryonyx/Features/TravelOffice/Runtime/TravelTreasureView.cs)・[生成](../../../client/Assets/Baryonyx/Features/TravelOffice/Editor/TravelOfficeScreenAssets.cs) |
| 枠つきの表示・すりガラスのカード | [枠の生成](../../../client/Assets/Baryonyx/Shared/UI/GuideMenu/Editor/GuideFrameAssets.cs)・[枠の切り替え](../../../client/Assets/Baryonyx/Shared/UI/GuideMenu/GuideMenuView.cs)・[カードの操作](../../../client/Assets/Baryonyx/Shared/UI/GlassCard/GlassCard.cs)・[カードの生成](../../../client/Assets/Baryonyx/Shared/UI/GlassCard/Editor/GlassCardAssets.cs) |
| 確認のダイアログ（冒険の確認・メニュー・報酬・復活・結果） | [表示と操作](../../../client/Assets/Baryonyx/Shared/UI/Dialog/GameDialog.cs)・[生成](../../../client/Assets/Baryonyx/Shared/UI/Dialog/Editor/GameDialogAssets.cs) |
| 戦闘画面の見た目モック（ロジックなし） | [BattleLabシーン](../../../client/Assets/Baryonyx/App/Scenes/Debug/BattleLab.unity)・[Prefab生成](../../../client/Assets/Baryonyx/Features/Combat/Editor/BattleInspectAssets.cs)・[表示と操作](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.cs) |
| 戦闘の背景を選べる戦闘シーン | [Battleシーン](../../../client/Assets/Baryonyx/App/Scenes/Battle.unity)・[シーン生成](../../../client/Assets/Baryonyx/App/Editor/BattleSceneSetup.cs)・[背景の生成](../../../client/Assets/Baryonyx/Shared/Art/Stages/Editor/BattleStageSets.cs)・[背景の選択](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleStageSelector.cs) |
| 品質のデバッグルーム | [QualityLabシーン](../../../client/Assets/Baryonyx/App/Scenes/Debug/QualityLab.unity)・[シーン生成](../../../client/Assets/Baryonyx/App/Editor/QualityLabSceneSetup.cs)・[Prefab生成](../../../client/Assets/Baryonyx/Features/Inventory/Editor/QualityLabAssets.cs)・[表示と操作](../../../client/Assets/Baryonyx/Features/Inventory/Presentation/QualityLab.cs)・[品質の計算](../../../client/Assets/Baryonyx/Features/Inventory/Runtime/ItemQuality.cs) |
| スキルのデバッグルーム | [CardSkillLabシーン](../../../client/Assets/Baryonyx/App/Scenes/Debug/CardSkillLab.unity)・[シーン生成](../../../client/Assets/Baryonyx/App/Editor/CardSkillLabSceneSetup.cs)・[Prefab生成](../../../client/Assets/Baryonyx/Features/Combat/Editor/CardSkillLabAssets.cs)・[表示と操作](../../../client/Assets/Baryonyx/Features/Combat/Presentation/CardSkillLab.cs) |
| 敵の挙動デバッグルーム | [EnemyLabシーン](../../../client/Assets/Baryonyx/App/Scenes/Debug/EnemyLab.unity)・[シーン生成](../../../client/Assets/Baryonyx/App/Editor/EnemyLabSceneSetup.cs)・[Prefab生成](../../../client/Assets/Baryonyx/Features/Combat/Editor/EnemyLabAssets.cs)・[操作盤](../../../client/Assets/Baryonyx/Features/Combat/Presentation/EnemyLab.cs)・[敵の行動の呼び出し](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.EnemyLab.cs) |
| 戦闘の演出デバッグルーム | [BattleMomentLabシーン](../../../client/Assets/Baryonyx/App/Scenes/Debug/BattleMomentLab.unity)・[シーン生成](../../../client/Assets/Baryonyx/App/Editor/BattleMomentLabSceneSetup.cs)・[Prefab生成](../../../client/Assets/Baryonyx/Features/Combat/Editor/BattleMomentLabAssets.cs)・[操作盤](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleMomentLab.cs)・[演出の流れ](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.Moments.cs)・[エフェクト](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleSkillVfx.Moments.cs)・[ドロップの札](../../../client/Assets/Baryonyx/Shared/UI/Reward/RewardReveal.cs) |
| 探索の演出デバッグルーム | [ExplorationMomentLabシーン](../../../client/Assets/Baryonyx/App/Scenes/Debug/ExplorationMomentLab.unity)・[シーン生成](../../../client/Assets/Baryonyx/App/Editor/ExplorationMomentLabSceneSetup.cs)・[Prefab生成](../../../client/Assets/Baryonyx/Features/Adventure/Editor/ExplorationMomentLabAssets.cs)・[操作盤](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/ExplorationMomentLab.cs)・[演出](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/ExplorationMoments.cs)・[光のシェーダー](../../../client/Assets/Baryonyx/Features/Adventure/Vfx/ExplorationLight.shader) |
| デバッグルームの一覧 | [一覧と開き方](../../../client/Assets/Baryonyx/App/Runtime/HomeBootstrap.cs)・[ルームの並び](../../../client/Assets/Baryonyx/App/Runtime/SceneNames.cs)・[重ねる「ホームへ」](../../../client/Assets/Baryonyx/Features/Home/Runtime/DebugRoomReturn.cs)・[ボタン・ダイアログ・「ホームへ」の生成](../../../client/Assets/Baryonyx/Features/Home/Editor/HomeScreenAssets.cs) |
