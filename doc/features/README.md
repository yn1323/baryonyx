# 機能仕様の索引

[全体索引](../README.md)

ゲームのルールは各仕様、個別の値・見た目は[データ索引](../catalog/README.md)で管理する。
企画仕様はユーザーの[初期企画](../game/brief.md)と、その後の仕様決定を各文書へ反映している。
候補や未決欄の存在は機能の採用・実装を意味しない。

## 仕様の確度

探索・進行・戦闘や運動目標などの企画仕様では、次の区分を保つ。
各文書のメタデータは文書全体の状態であり、個々の数値や候補の確定を意味しない。

| 区分 | 意味 |
|---|---|
| **確定** | ユーザーが明示的に採用したルール・方向性 |
| **設計方針** | プレイヤー体験として目指すこと。実装方法や細部は調整する |
| **仮設定** | 試作用の数値・具体例。最終的なバランス値ではない |
| **未確定** | 採用候補や実装前に詰める事項。決定済みとして扱わない |

「候補」「未決事項」は未確定として読む。
各機能の決定は対応する正本へ反映する。
運動目標の条件と継続ルールは[目標仕様](goals.md)、ルーン換算や報酬の具体値は[運動報酬](exercise-rewards.md)・[経済](economy.md)で区別して管理する。

## ゲームの企画仕様

| 仕様 | 読む文書 |
|---|---|
| 運動データとルーン換算 | [仕様と未決事項](exercise-rewards.md) |
| 日次・週次目標の設定・集計・継続倍率・編集・履歴 | [運動目標と達成報酬](goals.md) |
| ルーン経済と資源 | [仕様と未決事項](economy.md) |
| キャラクターとパーティ編成 | [仕様と未決事項](party.md) |
| 戦闘操作・ダウン・弱点・UI・再戦・復活・仮の数値 | [戦闘システム](combat.md) |
| 装備生成と厳選 | [仕様と未決事項](equipment.md) |
| 育成と強化 | [仕様と未決事項](progression.md) |
| 部屋単位の探索・進捗・中断・再出発 | [探索とダンジョンの進行](stage-progression.md) |
| 日付の区切りとデイリー変異 | [仕様と未決事項](daily-rules.md) |
| アカウントとセーブ | [仕様と未決事項](accounts-save.md) |
| 画面一覧と操作 | [仕様と未決事項](screens.md) |
| 主要画面を操作するUnityワイヤー（実装削除済み） | [試作の記録と評価](game-wireframe.md) |
| クライアントアセットの一覧とプレビュー | [クライアントアセット展示室](showcase.md) |
| 初回体験とチュートリアル | [仕様と未決事項](onboarding.md) |
| ユーザー設定とアクセシビリティ | [仕様と未決事項](player-settings.md) |
| ガチャと課金の検討項目 | [仕様と未決事項](monetization.md) |
| 実績とミッション | [仕様と未決事項](achievements.md) |

ゲーム全体の体験・コアループは[ゲーム概要](../game/overview.md)、最初の戦闘試作とプレイヤー目線の評価は[MVPと企画の判断基準](../game/mvp.md)を参照する。

## 現在の実装

2026-09-24時点のシーンは、Top・Home・展示室だけである。
冒険・獲得・装備・戦闘を試せた操作試作と、健康データを表示した画面は削除した。
TopとHomeは、Health Connectの歩数をゲストのセッションでサーバーへ同期する。
画面に依存しない戦闘計算と、Google認証・運動報酬の請求のロジックは残しているが、現在はどの画面からも呼ばない。

| 機能 | 現行仕様 | 実装の入口 |
|---|---|---|
| 健康データの読み取りと保存 | [機能詳細](health-data.md) | [Providerの組み立て](../../client/Assets/Baryonyx/App/Runtime/HealthRuntime.cs)・[サーバー同期](../../client/Assets/Baryonyx/Features/Health/Runtime/Sync/HealthServerSync.cs)・[API](../../server/src/features/health/routes.ts) |
| 起動時の連携と歩数の同期 | [機能詳細](startup-sync.md) | [Topの起動処理](../../client/Assets/Baryonyx/Features/Health/Runtime/Link/HealthStartupFlow.cs)・[共有する接続](../../client/Assets/Baryonyx/App/Runtime/GameServices.cs)・[ゲストAPI](../../server/src/features/accounts/routes.ts) |
| サーバー疎通確認 | [機能詳細](server-health.md) | [app.ts](../../server/src/app.ts) |
| 画面の操作ワイヤー | [試作の記録](game-wireframe.md) | 2026-09-24に削除。[戦闘計算](../../client/Assets/Baryonyx/Features/Combat/Runtime)だけを残す |
| ホーム画面のモック | [画面一覧](screens.md#ホーム画面の見た目モック) | [Homeシーン](../../client/Assets/Baryonyx/App/Scenes/Home.unity)・[Prefab生成](../../client/Assets/Baryonyx/Features/Home/Editor/HomeScreenAssets.cs) |
| 戦闘MVP | [試作値と実装範囲](combat.md#実装との対応) | [CombatEncounter](../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatEncounter.cs) |
| クライアントアセット展示室 | [展示室仕様](showcase.md) | [展示室シーン](../../client/Assets/Baryonyx/App/Scenes/Showcase.unity)・[カタログ生成](../../client/Assets/Baryonyx/Features/Showcase/Editor/ShowcaseCatalogBuilder.cs) |

現在の起動画面はHealth Connectと任意のGoogle接続を独立して扱い、サーバー同期は呼ばない。
詳細な制約・検証済み範囲・実環境で残る確認は各機能詳細を参照する。

## 機能追加時の更新

仕様に動作・制約・未決事項・実装入口を記し、この索引へリンクする。
実装した際は「現在の実装」へ反映し、企画で決めたことと実装したことの差を対応する仕様に記す。
