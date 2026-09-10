# AGENTS.md

`server/` の作業には、[ルートのAGENTS.md](../AGENTS.md)と以下の指示を適用する。
HonoアプリをCloudflare Workersで実行し、D1へ接続する。
開発・検証・公開の手順は [バックエンドの開発環境](../doc/rules/backend-design.md)、コマンドと依存バージョンは [package.json](package.json) を参照する。

## ディレクトリ構成と狙い

機能ごとにHTTPの入口・入力検証・業務処理・DB操作・テストをまとめ、変更対象を同じ機能内からたどれるようにする。
DB全体の履歴や複数機能をまたぐ検証は、機能の外で管理する。
以下は新規配置・移行時の基準であり、未作成の配置先を含む。
機能内のファイル名は例であり、処理の分割が必要になった時点で追加する。

```text
server/
├── src/
│   ├── index.ts                   Workerの入口
│   ├── app.ts                     共通middlewareと機能ルートの組み立て
│   ├── app.test.ts
│   ├── features/
│   │   └── <feature>/
│   │       ├── routes.ts          HTTPの入口
│   │       ├── routes.test.ts
│   │       ├── schema.ts          入出力のスキーマ
│   │       ├── sync.ts            業務処理の例
│   │       ├── sync.test.ts
│   │       └── repository.ts      機能専用のDB操作
│   └── shared/                    複数機能で使う処理
├── tests/
│   ├── integration/               Worker・D1などの結合確認
│   ├── scenarios/                 複数APIを通す業務シナリオ
│   └── support/                   共通のテスト補助・固定データ
├── migrations/                    DB全体で順序を管理するSQL
├── scripts/                       開発・公開処理とそのテスト
├── package.json                   コマンドと依存管理
├── wrangler.json                  Workerの設定とBindings
├── vitest.config.ts               テストの検出・実行設定
└── tsconfig.json                  TypeScriptの検査設定
```

### 実装と依存の配置

- `index.ts` はWorkerの入口、`app.ts` は共通middlewareと機能ルートの組み立てを担当する。機能ごとのHonoアプリを `app.route()` で接続する。
- HTTPのパスとhandlerは `routes.ts` にまとめ、複雑になった業務処理・DB操作を同じ機能内のファイルへ分ける。全機能共通の `controllers/`・`services/`・`repositories/` へ種類別に分散させない。
- `shared/` には複数機能で実際に使う共通の認証・通信などを置く。個別機能の業務判断やテスト専用の補助は含めない。
- 入出力のスキーマは所有する機能に置く。API資料やクライアントコードを生成する場合は生成元を一つにし、生成結果を手編集しない。
- マイグレーションは `migrations/` に集約する。機能をまたぐ適用順序と履歴を維持するため、機能別には分散させない。適用済みSQLへの変更は新しいマイグレーションで行う。
- 開発・公開処理は `scripts/`、CIの起動条件と実行順序はルートの `.github/workflows/` で管理する。設定は対応するツールの読み込み位置に置く。
- `dist/`、`.wrangler/`、依存パッケージ、生成型、テスト結果はソースと分け、生成手順とGit除外設定に従う。

### テストの配置

- 単体テストと機能内で完結するAPIテストは、対象の実装と同じ階層に `<名前>.test.ts` として置く。機能専用のfixtureもその近くに置く。
- WorkersランタイムやD1との結合確認は `tests/integration/`、複数APIを通す業務シナリオは `tests/scenarios/` に置く。共通の起動・初期化処理や固定データだけを `tests/support/` へ置く。
- `scripts/` のテストは対象スクリプトの隣に置き、アプリのテストと実行環境を分ける。
- シナリオの並列実行とDBの独立性は [tests/scenarios/AGENTS.md](tests/scenarios/AGENTS.md) に従う。
- ファイルを移動したら、import、[Vitest設定](vitest.config.ts)、[TypeScript設定](tsconfig.json)、実行コマンド、関連文書の参照を併せて確認する。
