# バックエンドの開発環境

`server/` のHonoアプリをCloudflare Workersで実行し、D1へ接続する。
Node.jsは開発ツールとCIで使い、ローカルのWorkerもWranglerとMiniflareで動かす。
Google認証・健康データの業務APIは、この基盤と別の機能として実装・検証する。
実装とテストの配置は [server/AGENTS.md](../../server/AGENTS.md) のコロケーション方針に従う。

## 初回の準備

Node.jsは [`.node-version`](../../server/.node-version)、pnpmと依存パッケージは [package.json](../../server/package.json) に固定する。
指定版のNode.jsとpnpmを用意し、以降のコマンドは `server/` から実行する。

```sh
cd server
node --version
pnpm --version
pnpm install --frozen-lockfile
pnpm db:migrate:local
pnpm dev
```

CIの指定版はNode.js `24.21.0`、pnpm `12.3.4` である。
インストールにはnpmレジストリへの接続が必要だが、ローカル開発と結合テストにCloudflareのアカウントやSecretsは不要である。
[pnpm-workspace.yaml](../../server/pnpm-workspace.yaml) でesbuildとworkerdのインストール処理を許可している。
lockfileはpnpmで更新し、手で編集しない。

## ローカル実行

`pnpm dev` はソース変更を反映しながら `127.0.0.1:3000` で待ち受ける。
[HTTPの疎通確認](http://127.0.0.1:3000/health) と [D1の疎通確認](http://127.0.0.1:3000/ready) を開いて確認する。
停止は `Ctrl+C` とする。

```sh
pnpm build
pnpm start
```

`build` はWranglerのdry runで `dist/index.js` を生成し、`start` はその成果物をローカルのWorkersランタイムで実行する。
型チェックは `pnpm typecheck` で別に行う。
開発起動とビルド後の起動は同じポートを使うため、同時には起動しない。

[wrangler.json](../../server/wrangler.json) はLocal専用で、DBの識別子もローカル用の固定値である。
`--local` を明示してリモートDBへの接続を防ぎ、データは `.wrangler/` 以下に保持する。
この設定で直接リモート公開せず、クラウド側は後述の公開workflowを使う。
Android実機から同じPCへの接続設定は含めていない。

## 環境とDBの保持

| 環境 | Worker・DB | データの扱い |
|---|---|---|
| Local | 手元のWranglerとD1 | 再起動しても保持する |
| Miniflareテスト | テスト実行専用の一時ディレクトリ | 毎回空のDBから開始し、終了時に削除する |
| Preview | PRごとの `baryonyx-server-pr-<番号>` | 同じPRでは保持し、PR終了時にWorkerとDBを削除する |
| Dev | `baryonyx-server-dev` | 保持する |
| Prod | `baryonyx-server-prod` | 保持する |

PreviewはPRごとに独立したWorkerとD1を持つ。
WorkersのバージョンPreview URLを共有する方式ではなく、各PR用Workerの `workers.dev` URLを使用する。
別PR、Dev、ProdのDBを共有しない。

通常のPreview更新でDBをリフレッシュする処理はない。
既存DBを再利用し、未適用のマイグレーションだけを追加する。
PRを閉じてから再び開いた場合は、新しいDBで開始する。

## マイグレーション

SQLは [migrations/](../../server/migrations/) に置き、`0001_baseline.sql` のように4桁の連番と英小文字の名前を付ける。
最初のSQLは `SELECT 1` のみで、Wranglerの適用履歴を開始するために置いている。
業務テーブルは各機能の実装時に新しいSQLとして追加する。

```sh
pnpm exec wrangler d1 migrations create DB add_example
pnpm db:migrate:local
```

適用済みSQLを書き換えず、新しいSQLを追加する。
Preview・Dev・Prodの公開処理も同じSQLを使い、適用履歴は `d1_migrations` に記録する。
公開時はマイグレーションが成功してからWorkerを更新し、失敗時は公開を止める。
DBの変更後にWorkerの公開が失敗した場合、DBの変更は残るため、旧Workerと互換性を保つSQLを用意する。
コードを古いコミットへ戻しても、DBの適用履歴やデータは巻き戻らない。

参考：[D1のマイグレーション](https://developers.cloudflare.com/d1/reference/migrations/)。

## 検査とMiniflareテスト

| コマンド | 確認内容 |
|---|---|
| `pnpm lint` | Biomeによるlint・整形・import整理 |
| `pnpm lint:fix` / `pnpm format` | ソースの修正・整形 |
| `pnpm types` | Wrangler設定からWorkerとD1の型を生成する |
| `pnpm typecheck` | 型生成後、ソース・テスト・Vitest設定を検査する |
| `pnpm test` | Workerをビルドし、APIテストとMiniflare結合テストを実行する |
| `pnpm test:watch` | 最初にWorkerをビルドし、Vitestを監視実行する |
| `pnpm test:ci` | 公開スクリプトの環境分離、DB保持、削除順序などを検査する |
| `pnpm build` | Cloudflareへ接続せずWorkerをバンドルする |
| `pnpm artifact` | CI公開用のSQLとビルド情報を `dist/` へまとめる |

アプリとD1の結合テストは [tests/integration/d1.test.ts](../../server/tests/integration/d1.test.ts) に置く。
一時DBにWranglerで全マイグレーションを2回適用して、適用履歴の重複がないことを確認する。
そのDBをMiniflareへ渡し、ビルド済みHonoからのD1接続、パラメーター付きSQLの作成・取得・更新・削除、失敗したバッチのロールバックを検証する。
テスト用テーブルは一時DBだけに作り、Previewや本番のDBへ投入しない。

`test:watch` 中にHonoの実装を変更した場合は、別のターミナルで `pnpm build` を実行してからテストを再実行する。
Miniflareはバンドル済みのWorkerを使うため、ソースだけを変更しても結合テストのWorkerには反映されない。
健康データAPIの業務シナリオは、[認証・入力エラー](../../server/tests/scenarios/health-auth.test.ts)、[歩数の同期](../../server/tests/scenarios/health-sync.test.ts)、[セッションの失効](../../server/tests/scenarios/health-session.test.ts) に分ける。
各ファイルは専用のMiniflareとD1を作成し、外部の本人確認をテスト用に差し替えてHonoとDBを検証する。
入力検証の単体テストは [schema.test.ts](../../server/src/features/health/schema.test.ts) に置き、実装と同じ機能内で管理する。

[Vitest設定](../../server/vitest.config.ts) でファイル間の並列実行を有効にし、単体・結合・シナリオテストを合わせて最大3並列に固定する。
ファイル内のテストは順番に実行する。
DBの独立性とシナリオ追加時のルールは [tests/scenarios/AGENTS.md](../../server/tests/scenarios/AGENTS.md) に従う。

Wrangler `4.130.0` と、その依存に合わせたMiniflare `5.20260908.0-alpha` を固定する。
Miniflareの設定は同パッケージが提供する `convertV4MiniflareOptions` を通している。
Vitest `5.0.0` を維持するため、Vitest `4.1` 系を要求する `@cloudflare/vitest-plugin` は導入していない。

[biome.json](../../server/biome.json) は2スペース・LFを使用する。
生成型 `worker-configuration.d.ts`、`.wrangler/`、`dist/`、依存パッケージ、SecretsファイルはGitへ追加しない。

## GitHub Actions

[Server CI](../../.github/workflows/server-ci.yml) はPRの作成・更新・再オープンと `main` へのpushで検査する。
変更パスによる絞り込みはなく、fork PRでも認証情報を使わない検査は実行する。
検査jobは依存を固定してインストールし、lint、型チェック、ビルド、Miniflareを含むテスト、公開スクリプトのテストを実行する。
成功したWorker・SQL・ビルド情報だけをartifactに保存する。

同一リポジトリのPRでは、検査成功後に `server-preview` environmentへ公開する。
認証情報を使うjobはPRのbaseブランチから公開ヘルパーと依存設定を取得し、PR側のビルドフックを実行しない。
PRが既に閉じられていれば再公開をスキップし、PR終了イベントでは専用WorkerとD1を削除する。
同じPRのworkflowは並列に動かさず、途中の自動キャンセルも行わない。
削除に失敗した場合は、終了イベントのworkflowを再実行する。

公開先では対象コミットの `X-Build-SHA` と `/health`・`/ready` を確認する。
業務シナリオはMiniflareで検証し、公開先の確認でデータの書き込みやリセットは行わない。
APIのURLはjob summary、GitHub environment、`preview-url` 出力から取得できる。
クライアントのPreviewへこのAPI URLを渡す処理は、クライアントの通信機能を実装するときに追加する。

[Server deploy](../../.github/workflows/server-deploy.yml) は手動実行で `dev` または `prod` を選ぶ。
指定したGit参照を検査してから公開し、DBがなければ作成し、あれば再利用する。
`main` へのpushだけではDev・Prodへ公開しない。

### 初回の公開準備

1. 対象Cloudflareアカウントで `workers.dev` のサブドメインを設定する。
2. GitHub Repository secretsへ `CLOUDFLARE_ACCOUNT_ID` と `CLOUDFLARE_API_TOKEN` を登録する。トークンには対象アカウントのWorkers ScriptsとD1の編集権限を付ける。
3. このサーバー基盤をPRのbaseブランチへ先に反映する。ヘルパーがないbaseを使うPreview jobは理由を表示して失敗する。
4. 反映後のbaseを使うPRで、Preview公開とD1疎通を確認する。Dev・ProdはGitHub Actionsの `Server deploy` から環境を選択する。

Worker名・DB名は [deploy.mjs](../../server/scripts/deploy.mjs) で環境ごとに固定し、取得したDB IDを使う設定を `.wrangler/deploy/` へ生成する。
実行時に環境を指定するため、リポジトリへ実アカウントのDB IDを記入する必要はない。
環境別の承認や公開元ブランチを制限する場合は、GitHubの `server-dev`・`server-prod` environmentで設定する。

参考：[GitHub Actionsの認証設定](https://developers.cloudflare.com/workers/ci-cd/external-cicd/github-actions/)、[D1の環境分離](https://developers.cloudflare.com/d1/configuration/environments/)。
