# ドキュメント

baryonyxの文書は、リポジトリ直下の `doc/` に集約する。
クライアントとバックエンドの機能を、同じ入口から確認できるようにする。

## 現在の文書

- [機能一覧](features/README.md)：実装済みの機能と詳細文書への入口。
- [バックエンドの開発環境](rules/backend-design.md)：導入、起動、整形、テスト、CIの手順と構成。
- [Unityクライアントの整形と静的解析](rules/client-code-quality.md)：CSharpier、Analyzer、GitHub CIとライセンス設定。
- [UnityのテストとWebプレビュー](rules/client-testing-and-preview.md)：EditMode・PlayModeテスト、Webビルド、Cloudflare WorkersのPR Preview設定と初回反映手順。
- [文書管理方針](rules/documentation-policy.md)：文書の配置、役割、更新方法。
- [エージェント共通指示](../AGENTS.md)：リポジトリ全体の作業ルール。
- [serverのHono開発環境とCIの導入計画](plans/2026-09-09-server-bootstrap.md)：導入の判断、実施結果、残る確認事項。
- [Unityクライアントの整形と静的解析の導入計画](plans/2026-09-10-client-code-quality.md)：CSharpier、Microsoft.Unity.AnalyzersとGitHub Actionsの導入案。
- [UnityテストとWebビルドの初期設定](plans/2026-09-10-client-tests-web-preview.md)：追加した初期設定と検証結果。
- [クライアントCIの積み残し](plans/2026-09-10-client-ci-backlog.md)：VRT、Web E2E、Androidビルド、Androidスモーク／E2Eと実機での歩数取得の未着手項目。

## 文書の配置

次の文書は、記載する内容が決まった時点で追加する。

| 配置 | 内容 |
|---|---|
| `architecture.md` | 全体構成とclient・server・データ保存先の責務 |
| `features/<機能名>.md` | 機能の動作、制約、実装の入口 |
| `rules/frontend-design.md` | クライアントの配置、責務、依存方向 |
| `rules/backend-design.md` | バックエンドの配置、責務、依存方向 |
| `rules/testing-policy.md` | テスト対象とテスト層の選び方 |
| `rules/ui-design.md` | 画面、操作、表示の判断基準 |
| `plans/YYYY-MM-DD-<変更名>.md` | 提案、変更理由、実装手順、受入条件 |

現時点では `guides/` と `product.md` は設けない。
機能仕様と開発ルールの詳細は、対応する文書へ集約する。
