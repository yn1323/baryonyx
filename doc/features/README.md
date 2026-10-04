# 機能仕様の索引

[全体索引](../README.md)

ゲームのルールは各仕様、個別の値・見た目は[データ索引](../catalog/README.md)で管理する。
企画仕様はユーザーの[初期企画](../game/brief.md)と、その後の仕様決定を各文書へ反映している。
2026-09-27の[企画見直し](../game/redesign-2026-09-27.md)で、戦闘・運動連動・ダンジョン・課金を大きく変更した。
候補や未決欄の存在は機能の採用・実装を意味しない。

## 仕様の確度

探索・進行・戦闘やACTボーナスなどの企画仕様では、次の区分を保つ。
各文書のメタデータは文書全体の状態であり、個々の数値や候補の確定を意味しない。

| 区分 | 意味 |
|---|---|
| **確定** | ユーザーが明示的に採用したルール・方向性 |
| **設計方針** | プレイヤー体験として目指すこと。実装方法や細部は調整する |
| **仮設定** | 試作用の数値・具体例。最終的なバランス値ではない |
| **未確定** | 採用候補や実装前に詰める事項。決定済みとして扱わない |

「候補」「未決事項」は未確定として読む。
各機能の決定は対応する正本へ反映する。
ACTボーナスの段階と区切りは[ACTボーナス](step-bonus.md)、ACTの定義とルーン換算は[運動報酬](exercise-rewards.md)、ルーンの用途は[経済](economy.md)で区別して管理する。
既存の仕様と今回の内容のどちらを採るか判断が必要な事項は、各仕様の「確認待ち」に記録する。

## ゲームの企画仕様

| 仕様 | 読む文書 |
|---|---|
| 歩数とACTの換算、ルーン換算、Health Connectの権限 | [仕様と未決事項](exercise-rewards.md) |
| 日次のACTボーナス、朝4時の区切り、週次ボーナスの案 | [ACTボーナス](step-bonus.md) |
| ルーン経済と資源 | [仕様と未決事項](economy.md) |
| キャラの構成・デッキ・パーティ編成 | [仕様と未決事項](party.md) |
| ターン制のカードバトル・エネルギー・属性と弱点・ルーン復活 | [戦闘システム](combat.md) |
| 装備生成と厳選 | [仕様と未決事項](equipment.md) |
| ルーンによるレベルアップと強化 | [仕様と未決事項](progression.md) |
| 分岐ルートの冒険・冒険終了と獲得物の保持・部屋単位の探索 | [探索とダンジョンの進行](stage-progression.md) |
| 日付の区切りとデイリー変異 | [仕様と未決事項](daily-rules.md) |
| アカウントとセーブ | [仕様と未決事項](accounts-save.md) |
| 画面一覧と操作 | [仕様と未決事項](screens.md) |
| 主要画面を操作するUnityワイヤー（実装削除済み） | [試作の記録と評価](game-wireframe.md) |
| クライアントアセットの一覧とプレビュー | [クライアントアセット展示室](showcase.md) |
| 初回体験とチュートリアル | [仕様と未決事項](onboarding.md) |
| ユーザー設定とアクセシビリティ | [仕様と未決事項](player-settings.md) |
| 召喚と課金、ACTのチケット、法務メモ | [仕様と未決事項](monetization.md) |
| 実績とミッション | [仕様と未決事項](achievements.md) |

ゲーム全体の体験・コアループは[ゲーム概要](../game/overview.md)、最初の戦闘試作とプレイヤー目線の評価は[MVPと企画の判断基準](../game/mvp.md)を参照する。

## 現在の実装

シーンは、Top・Home・展示室と、2026-09-27に追加した戦闘画面のモック（BattleInspect）、2026-10-03に追加した戦闘の背景を選べる戦闘シーン（Battle）、2026-10-04に追加した敵の挙動デバッグルーム（EnemyLab）、Homeから開く案内人の画面（編成・商会・神殿・旅の案内所。2026-10-05に酒場を編成、装備を商会へ改めた）、同日に追加した冒険の探索（Exploration）である。
2026-10-04から、Battleは冒険の戦闘を兼ね、Homeから旅の案内所で出発し、探索・戦闘・報酬・復活・結果を通ってHomeへ戻る1周を遊べる（[冒険の画面](screens.md#冒険の画面)）。
冒険・獲得・装備・戦闘を試せた操作試作と、健康データを表示した画面は削除した。
TopとHomeは、Health Connectの歩数をゲストのセッションでサーバーへ同期する。
画面に依存しない戦闘計算と、Google認証・運動報酬の請求のロジックは残しているが、現在はどの画面からも呼ばない。
残っている戦闘計算は旧仕様のリアルタイム戦闘の試作であり、現行のカードバトルとは一致しない。

| 機能 | 現行仕様 | 実装の入口 |
|---|---|---|
| 健康データの読み取りと保存 | [機能詳細](health-data.md) | [Providerの組み立て](../../client/Assets/Baryonyx/App/Runtime/HealthRuntime.cs)・[サーバー同期](../../client/Assets/Baryonyx/Features/Health/Runtime/Sync/HealthServerSync.cs)・[API](../../server/src/features/health/routes.ts) |
| 起動時の連携と歩数の同期 | [機能詳細](startup-sync.md) | [Topの起動処理](../../client/Assets/Baryonyx/Features/Health/Runtime/Link/HealthStartupFlow.cs)・[共有する接続](../../client/Assets/Baryonyx/App/Runtime/GameServices.cs)・[ゲストAPI](../../server/src/features/accounts/routes.ts) |
| サーバー疎通確認 | [機能詳細](server-health.md) | [app.ts](../../server/src/app.ts) |
| 画面の操作ワイヤー | [試作の記録](game-wireframe.md) | 2026-09-24に削除。[戦闘計算](../../client/Assets/Baryonyx/Features/Combat/Runtime)だけを残す |
| ホーム画面のモック | [画面一覧](screens.md#ホーム画面の見た目モック) | [Homeシーン](../../client/Assets/Baryonyx/App/Scenes/Home.unity)・[Prefab生成](../../client/Assets/Baryonyx/Features/Home/Editor/HomeScreenAssets.cs) |
| 編成・商会・神殿・旅の案内所（案内人がいる画面）のモック | [画面一覧](screens.md#編成商会神殿旅の案内所の画面) | [共通部品](../../client/Assets/Baryonyx/Shared/UI/GuideMenu/GuideMenuView.cs)・[シーン生成](../../client/Assets/Baryonyx/App/Editor/GuideSceneSetup.cs)・[酒場の仮データ](../../client/Assets/Baryonyx/Features/Tavern/Editor/TavernScreenAssets.cs) |
| ACTボーナスの持ち物と枠の設定 | [ACTボーナス](step-bonus.md#実装との対応)・[ボーナス設定](screens.md#ボーナス設定) | [枠の計算](../../client/Assets/Baryonyx/Features/StepBonus/Runtime/StepBonusLoadout.cs)・[設定の表示と操作](../../client/Assets/Baryonyx/Features/StepBonus/Runtime/StepBonusSettingsView.cs)・[サーバーとの読み書き](../../client/Assets/Baryonyx/Features/StepBonus/Runtime/StepBonusSource.cs)・[パネルと仮データの生成](../../client/Assets/Baryonyx/Features/StepBonus/Editor/StepBonusAssets.cs)・[API](../../server/src/features/step-bonus/routes.ts) |
| 編成のパーティ（パーティの入れ替え） | [パーティ](screens.md#パーティ)・[パーティ](party.md) | [入れ替えの決まり](../../client/Assets/Baryonyx/Features/Party/Runtime/PartyFormation.cs)・[編成の表示と操作](../../client/Assets/Baryonyx/Features/Party/Runtime/PartyFormationView.cs)・[パネルと仮データの生成](../../client/Assets/Baryonyx/Features/Party/Editor/PartyAssets.cs) |
| 編成の装備（武器・防具の付け替え）とサーバー保存 | [装備](screens.md#装備)・[装備の保存](equipment.md#持っている装備と付け替えの保存) | [表示と付け替え](../../client/Assets/Baryonyx/Features/Equipment/Runtime/EquipmentPresenter.cs)・[サーバーとの読み書き](../../client/Assets/Baryonyx/Features/Equipment/Runtime/EquipmentSource.cs)・[パネルの生成](../../client/Assets/Baryonyx/Features/Equipment/Editor/EquipmentAssets.cs)・[API](../../server/src/features/equipment/routes.ts)・[DB操作](../../server/src/features/equipment/repository.ts) |
| キャラ・Lv・カード・編成のサーバー保存 | [キャラと編成の保存](party.md#キャラと編成の保存) | [サーバーとの読み書き](../../client/Assets/Baryonyx/Features/Party/Runtime/PartySource.cs)・[アプリ内での保持](../../client/Assets/Baryonyx/Features/Party/Runtime/PartySession.cs)・[API](../../server/src/features/party/routes.ts)・[DB操作](../../server/src/features/party/repository.ts) |
| 編成の育成（1人のレベルアップ） | [育成](screens.md#育成)・[育成と強化](progression.md) | [レベルアップの流れ](../../client/Assets/Baryonyx/Features/Training/Runtime/TrainingPresenter.cs)・[表示と操作](../../client/Assets/Baryonyx/Features/Training/Runtime/TrainingView.cs)・[画面と仮データの生成](../../client/Assets/Baryonyx/Features/Training/Editor/TrainingAssets.cs) |
| 編成のスキル（仲間ごとの4枚の付け替え） | [スキル](screens.md#スキル)・[パーティ](party.md) | [付け替えの決まり](../../client/Assets/Baryonyx/Features/CardLoadout/Runtime/CardLoadoutRules.cs)・[表示と操作](../../client/Assets/Baryonyx/Features/CardLoadout/Runtime/CardLoadoutView.cs)・[パネルの生成](../../client/Assets/Baryonyx/Features/CardLoadout/Editor/CardLoadoutAssets.cs) |
| 冒険の1周（出発・探索・戦闘・報酬・復活・結果・中断と再開） | [冒険の画面](screens.md#冒険の画面)・[冒険の中断と再開](stage-progression.md#冒険の中断と再開)・[実装計画](../plans/2026-10-04-adventure-loop.md) | [冒険の状態の読み書き](../../client/Assets/Baryonyx/Features/Adventure/Runtime/AdventureSource.cs)・[探索の流れ](../../client/Assets/Baryonyx/Features/Adventure/Runtime/ExplorationFlow.cs)・[戦闘との受け渡し](../../client/Assets/Baryonyx/Features/Adventure/Runtime/BattleAdventureFlow.cs)・[旅の案内所の出発](../../client/Assets/Baryonyx/Features/Adventure/Runtime/TravelDeparture.cs)・[画面の生成](../../client/Assets/Baryonyx/Features/Adventure/Editor/AdventureAssets.cs)・[探索シーンの生成](../../client/Assets/Baryonyx/App/Editor/ExplorationSceneSetup.cs)・[API](../../server/src/features/adventure/routes.ts)・[DB操作](../../server/src/features/adventure/repository.ts) |
| 確認のダイアログ（冒険の確認・メニュー・報酬・復活・結果） | [冒険の画面](screens.md#冒険の画面) | [表示と操作](../../client/Assets/Baryonyx/Shared/UI/Dialog/GameDialog.cs)・[生成](../../client/Assets/Baryonyx/Shared/UI/Dialog/Editor/GameDialogAssets.cs) |
| 戦闘画面のモック（ロジックなし） | [画面一覧](screens.md#戦闘画面の見た目モック) | [BattleInspectシーン](../../client/Assets/Baryonyx/App/Scenes/Debug/BattleInspect.unity)・[Prefab生成](../../client/Assets/Baryonyx/Features/Combat/Editor/BattleInspectAssets.cs)・[表示と操作](../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.cs) |
| 戦闘の背景の選択 | [画面一覧](screens.md#戦闘の背景を選べる戦闘シーン)・[戦闘の背景](../art/hd2d-stage.md#戦闘の背景) | [Battleシーン](../../client/Assets/Baryonyx/App/Scenes/Battle.unity)・[シーン生成](../../client/Assets/Baryonyx/App/Editor/BattleSceneSetup.cs)・[背景の生成](../../client/Assets/Baryonyx/Shared/Art/Stages/Editor/BattleStageSets.cs)・[背景の選択](../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleStageSelector.cs) |
| スキルのデバッグルーム | [画面一覧](screens.md#スキルのデバッグルーム) | [CardSkillLabシーン](../../client/Assets/Baryonyx/App/Scenes/Debug/CardSkillLab.unity)・[シーン生成](../../client/Assets/Baryonyx/App/Editor/CardSkillLabSceneSetup.cs)・[Prefab生成](../../client/Assets/Baryonyx/Features/Combat/Editor/CardSkillLabAssets.cs)・[表示と操作](../../client/Assets/Baryonyx/Features/Combat/Presentation/CardSkillLab.cs) |
| 敵の挙動デバッグルーム | [画面一覧](screens.md#敵の挙動デバッグルーム) | [EnemyLabシーン](../../client/Assets/Baryonyx/App/Scenes/Debug/EnemyLab.unity)・[シーン生成](../../client/Assets/Baryonyx/App/Editor/EnemyLabSceneSetup.cs)・[Prefab生成](../../client/Assets/Baryonyx/Features/Combat/Editor/EnemyLabAssets.cs)・[操作盤](../../client/Assets/Baryonyx/Features/Combat/Presentation/EnemyLab.cs)・[敵の行動の呼び出し](../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.EnemyLab.cs) |
| 旧仕様の戦闘計算（現行の企画と不一致） | [残っているコードの動作](combat.md#実装との対応) | [CombatEncounter](../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatEncounter.cs) |
| クライアントアセット展示室 | [展示室仕様](showcase.md) | [展示室シーン](../../client/Assets/Baryonyx/App/Scenes/Showcase.unity)・[カタログ生成](../../client/Assets/Baryonyx/Features/Showcase/Editor/ShowcaseCatalogBuilder.cs) |

現在の起動画面はHealth Connectと任意のGoogle接続を独立して扱い、サーバー同期は呼ばない。
詳細な制約・検証済み範囲・実環境で残る確認は各機能詳細を参照する。

## 機能追加時の更新

仕様に動作・制約・未決事項・実装入口を記し、この索引へリンクする。
実装した際は「現在の実装」へ反映し、企画で決めたことと実装したことの差を対応する仕様に記す。
