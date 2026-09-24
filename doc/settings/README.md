# 設定の索引

[全体索引](../README.md)

設定値は各ツールが読むファイルを正本とし、この索引には用途・所在・変更手順を記す。
秘密値は転記しない。
以下は主要設定の入口であり、Unityの全既定値や外部管理画面の設定を確認済みとする一覧ではない。

## 開発・公開の設定

| 設定 | 正本・確認先 | 用途・変更手順 |
|---|---|---|
| Unityの版 | [ProjectVersion.txt](../../client/ProjectSettings/ProjectVersion.txt) | [ビルド手順](../rules/client-android-testing.md) |
| アプリ表示名 | [ProjectSettings.asset](../../client/ProjectSettings/ProjectSettings.asset) の `productName` | 正式名は[ゲーム概要](../game/overview.md)。Player Settingsの `Product Name` を変更する |
| Player Settings・描画・画面方向 | [ProjectSettings.asset](../../client/ProjectSettings/ProjectSettings.asset) | [UIルール](../rules/ui-design.md)・[エミュレーター](../rules/client-android-emulator.md) |
| ビルド対象シーン | [EditorBuildSettings.asset](../../client/ProjectSettings/EditorBuildSettings.asset) | [クライアント構成](../rules/frontend-design.md) |
| APKのアプリID・SDK・ABI・署名・出力先 | [AndroidBuild.cs](../../client/Assets/Baryonyx/Editor/CI/AndroidBuild.cs) | [Androidビルド](../rules/client-android-testing.md)。ビルド時の上書き設定も確認する |
| 目標FPS | [TopSceneController.cs](../../client/Assets/Baryonyx/App/Runtime/TopSceneController.cs) | 起動時に60 FPSを設定する |
| ゲーム内Google認証 | [設定型](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthConnectionSettings.cs)・[設定アセット](../../client/Assets/Baryonyx/Features/Health/Data/HealthConnectionSettings.asset) | [認証設定](../features/health-data.md#google認証の設定)。外部のOAuth登録との整合は別途確認する |
| ゲームサーバーのURL | [設定アセット](../../client/Assets/Baryonyx/Features/Health/Data/HealthConnectionSettings.asset) の `DevServerUrl`・`ProdServerUrl` | Editorはメニュー `Baryonyx > Server`（個人の選択は `client/UserSettings/BaryonyxServer.json`）、APKは環境別のショートカットまたはビルド時の `BARYONYX_ENVIRONMENT`・`BARYONYX_SERVER_URL` で選ぶ。[接続先の選び方](../../client/AGENTS.md#サーバーのbaseurl) |
| Editorの未連携プレビュー | 同じ設定アセットの `PreviewStartsUnlinked` | [起動時の同期](../features/startup-sync.md#実行環境と接続先) |
| HTTP通信の許可 | [ProjectSettings.asset](../../client/ProjectSettings/ProjectSettings.asset) の `insecureHttpOption` | Development Buildだけ許可する。受け付ける接続先は[起動時の同期](../features/startup-sync.md#実行環境と接続先)を参照 |
| UPMとAndroid依存 | [manifest.json](../../client/Packages/manifest.json)・[build.gradle](../../client/Assets/Plugins/Android/BaryonyxHealth.androidlib/build.gradle) | [プラグイン設定](../features/health-data.md#プラグインとandroidビルド) |
| .NET・整形・解析 | [global.json](../../client/global.json)・[ツール設定](../../client/.config/dotnet-tools.json)・[.editorconfig](../../client/.editorconfig) | [コード品質](../rules/client-code-quality.md) |
| Node.js・pnpm・サーバー依存 | [.node-version](../../server/.node-version)・[package.json](../../server/package.json) | [バックエンド](../rules/backend-design.md) |
| Local Worker・D1 | [wrangler.json](../../server/wrangler.json) | [環境とDB](../rules/backend-design.md#環境とdbの保持) |
| DBスキーマ・マイグレーション | [db-schema.ts](../../server/src/features/health/db-schema.ts)・[migrations](../../server/migrations/) | [マイグレーション](../rules/backend-design.md#マイグレーション) |
| クラウド公開・GOOGLE_CLIENT_ID | [サーバーCI](../../.github/workflows/server-ci.yml)・[公開workflow](../../.github/workflows/server-deploy.yml) | [初回の公開準備](../rules/backend-design.md#初回の公開準備)。実環境の登録値は転記しない |
| Unity CIのSecrets | [クライアントCI](../../.github/workflows/client-ci.yml) | [Unityライセンス設定](../rules/client-code-quality.md) |
| APKのCI配布・Drive認証 | [配布workflow](../../.github/workflows/client-distribute.yml) | [配布手順と停止状態](../rules/client-android-testing.md) |
| ローカルAPKのDrive配置 | Windowsは[Dev](../../shortcuts/build-apk-dev-to-drive.bat)・[Prod](../../shortcuts/build-apk-prod-to-drive.bat)、macOSは[Dev](../../shortcuts/build-apk-dev-to-drive.command)・[Prod](../../shortcuts/build-apk-prod-to-drive.command) | [手動実行ルール](../../AGENTS.md#手動実行用ショートカット) |
| Android SDK・AVD・起動引数 | [エミュレーター手順](../rules/client-android-emulator.md) | 同文書で確認する。AIはエミュレーターを起動しない |
| 依存更新 | [renovate.json](../../renovate.json) | [更新ルール](../rules/dependency-updates.md) |
| 文書の索引・リンク・ID検査 | [文書CI](../../.github/workflows/docs-ci.yml) | [検査手順](../tools/README.md) |

## ゲームとして未決の設定

| 項目 | 決定・記録先 |
|---|---|
| ルーン換算・報酬・経済の数値 | [機能仕様](../features/README.md) |
| キャラ・装備・敵などの数値と設定 | [個別データ](../catalog/README.md) |
| ユーザーが変更できる設定・既定値 | [ユーザー設定](../features/player-settings.md) |
| ゲームマスタの実行形式・配信・版管理 | [全体構成](../architecture.md) |
| 公開地域・言語 | [ゲーム概要](../game/overview.md) |

設定を追加するときは、用途、設定元、適用先、既定値の確認先、変更時の検証方法を記す。
外部管理画面の状態は、確認日と確認範囲を添えて記録する。
