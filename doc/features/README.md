# 機能仕様の索引

[全体索引](../README.md)

ゲームのルールは各仕様、個別の値・見た目は[データ索引](../catalog/README.md)で管理する。
追加した企画仕様はユーザーの[初期企画](../game/brief.md)を基にしており、候補や未決欄の存在は機能の採用・実装を意味しない。

## ゲームの企画仕様

| 仕様 | 読む文書 |
|---|---|
| 運動データとルーン換算 | [仕様と未決事項](exercise-rewards.md) |
| 運動目標と達成報酬 | [仕様と未決事項](goals.md) |
| ルーン経済と資源 | [仕様と未決事項](economy.md) |
| キャラクターとパーティ編成 | [仕様と未決事項](party.md) |
| 戦闘と効果の計算 | [仕様と未決事項](combat.md) |
| 装備生成と厳選 | [仕様と未決事項](equipment.md) |
| 育成と強化 | [仕様と未決事項](progression.md) |
| ステージ進行と危険度 | [仕様と未決事項](stage-progression.md) |
| 日付の区切りとデイリー変異 | [仕様と未決事項](daily-rules.md) |
| アカウントとセーブ | [仕様と未決事項](accounts-save.md) |
| 画面一覧と操作 | [仕様と未決事項](screens.md) |
| 初回体験とチュートリアル | [仕様と未決事項](onboarding.md) |
| ユーザー設定とアクセシビリティ | [仕様と未決事項](player-settings.md) |
| ガチャと課金の検討項目 | [仕様と未決事項](monetization.md) |
| 実績とミッション | [仕様と未決事項](achievements.md) |

## 現在の実装

2026-09-13時点で、client・serverの機能フォルダーと起動処理を確認した。
現在の機能実装は健康データ関連であり、上記のRPG機能は企画の準備段階である。

| 機能 | 現行仕様 | 実装の入口 |
|---|---|---|
| 健康データの読み取りと保存 | [機能詳細](health-data.md) | [画面起動](../../client/Assets/Baryonyx/App/Runtime/HealthScreenBootstrap.cs)・[同期クライアント](../../client/Assets/Baryonyx/Features/Health/Runtime/Sync/HealthClient.cs)・[API](../../server/src/features/health/routes.ts) |
| サーバー疎通確認 | [機能詳細](server-health.md) | [app.ts](../../server/src/app.ts) |

現在の起動画面はHealth Connectと任意のGoogle接続を独立して扱い、サーバー同期は呼ばない。
詳細な制約・検証済み範囲・実環境で残る確認は各機能詳細を参照する。

## 機能追加時の更新

仕様に動作・制約・未決事項・実装入口を記し、この索引へリンクする。
実装した際は「現在の実装」へ反映し、企画で決めたことと実装したことの差を対応する仕様に記す。
