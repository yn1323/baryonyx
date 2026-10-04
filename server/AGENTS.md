# AGENTS.md

`server/` の作業には、[ルートのAGENTS.md](../AGENTS.md)と以下の指示を適用する。
HonoアプリをCloudflare Workersで実行し、D1へ接続する。
開発・検証・公開の手順とディレクトリ構成は [バックエンドの開発環境](../doc/rules/backend-design.md)、コマンドと依存バージョンは [package.json](package.json) を参照する。

## 配置とテスト

- 実装・テスト・マイグレーション・スクリプトの置き場所は [ディレクトリ構成と配置](../doc/rules/backend-design.md#ディレクトリ構成と配置) に従う。全機能共通の `controllers/`・`services/`・`repositories/` へ種類別に分散させない。
- 適用済みのマイグレーションSQLとスナップショットは書き換えず、変更は新しいマイグレーションで行う（[マイグレーション](../doc/rules/backend-design.md#マイグレーション)）。
- テストの並列実行とDBの独立性は [tests/scenarios/AGENTS.md](tests/scenarios/AGENTS.md) に従う。
- テーブルや初期付与を足したら、開発用データ（seed）も同じ変更で直す（[開発用データ（seed）](../doc/rules/backend-design.md#開発用データseed)）。
