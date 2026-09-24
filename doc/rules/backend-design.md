---
id: rule-backend-design
type: reference
status: 運用中
updated: 2026-09-24
---

# バックエンドの開発環境

`server/` のHonoアプリをCloudflare Workersで実行し、Drizzle ORM経由でD1へ接続する。
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

`pnpm dev` はソース変更を反映しながら `127.0.0.1:4000` で待ち受ける。
[HTTPの疎通確認](http://127.0.0.1:4000/health) と [D1の疎通確認](http://127.0.0.1:4000/ready) を開いて確認する。
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

## Zodによる入力検証

バリデーションライブラリには **Zod** を使用する。
HTTP要求のスキーマは所有する機能の `schema.ts` に置き、Honoのhandlerで `safeParse()` を使って検証する。
業務処理とDB操作には検証済みの `data` を渡し、TypeScriptの型は `z.infer` でスキーマから導出する。
依存バージョンは [package.json](../../server/package.json) に固定する。

健康データAPIの [入力スキーマ](../../server/src/features/health/schema.ts) は同期開始、取得元ID、歩数保存の要求を、アカウントの [入力スキーマ](../../server/src/features/accounts/schema.ts) はログイン要求を検証する。
日別データの型・件数・数値範囲に加え、`refine()` で期間の連続性、タイムゾーンと日付境界、欠損と歩数の整合を確認する。
単日では欠損と歩数、取得時刻の範囲、指定タイムゾーンの日付境界を検証し、週全体では同一タイムゾーン、区間の連続性、日付の重複を検証する。
区間の連続性はUTC日時を数値へ変換して比較し、日時文字列の小数秒の表記差や夏時間による1日の長さの変化を受け入れる。
現在時刻に依存するスキーマはリクエストごとに生成する。
不正なJSONとスキーマ違反には、既存の `400 {"error":"invalid_request"}` を返す。
Zodのエラー詳細や入力値はHTTP応答へ含めない。

参考：[Zodの基本的な使い方](https://zod.dev/basics)、[Honoの入力検証](https://hono.dev/docs/guides/validation)。

## 認証とAPIの組み立て

認証とユーザー・セッションは、健康データと運動報酬が共通で使うため `features/accounts/` に置く。
[routes.ts](../../server/src/features/accounts/routes.ts) はGoogleログイン、ゲストの開始（`POST /v1/auth/guest`）、ログアウト、[auth.ts](../../server/src/features/accounts/auth.ts) はGoogle IDトークンの検証、セッションの発行、トークンのハッシュ化、Bearerトークンからのセッション確認を担当する。
DBの検索・書き込みは、認証処理から同じ機能のrepositoryへ委譲する。
セッションが必要な機能は [session.ts](../../server/src/features/accounts/session.ts) の `requireSession` を、自分のパス（`/health/*` など）にだけ付ける。

[app.ts](../../server/src/app.ts) の `createApi` は、`Cache-Control: no-store`、16KBの本文上限、503応答の共通設定を [shared/http.ts](../../server/src/shared/http.ts) で一度だけ付け、各機能のHonoアプリを接続する。
Honoでは同じ接続先へ接続した機能の `use("*")` が他機能のパスにも適用されるため、機能内で `use("*")` を使わない。
署名検証テストはルート定義を読み込まずに認証処理を検証し、HTTPの認証失敗・期限切れ・ログアウトはAPIシナリオで確認する。

## DrizzleによるDB操作

DB操作には `drizzle-orm/d1` を使い、リクエストのD1 bindingから [createDatabase](../../server/src/shared/db.ts) で接続を作る。
テーブル定義は所有する機能の `db-schema.ts`、DB操作は同じ機能の `repository.ts` に置く。
健康データでは [DBスキーマ](../../server/src/features/health/db-schema.ts) と [repository](../../server/src/features/health/repository.ts)、ユーザーとセッションでは [DBスキーマ](../../server/src/features/accounts/db-schema.ts) と [repository](../../server/src/features/accounts/repository.ts) が対応する。
HTTPの入力検証は `schema.ts` に残し、DBスキーマと分ける。

取得・更新にはDrizzleのクエリービルダーを使い、値をSQLへ埋め込む場合はパラメーター化する `sql` タグを使う。
複数の書き込みを一括で確定する処理はD1対応の `db.batch()` を使う。
健康データの保存では、所有者と同期の版を確認する条件をINSERT SELECTに含め、確認と書き込みの間に別の同期が割り込むことを防ぐ。
`has_value` はSQLiteの整数とTypeScriptのbooleanをDrizzleで相互変換する。

## マイグレーション

テーブル定義を変更したら、Drizzle Kitで差分SQLを生成する。
[drizzle.config.ts](../../server/drizzle.config.ts) は各機能の `db-schema.ts` を読み、[migrations/](../../server/migrations/) に4桁の連番付きSQLと `meta/` のスナップショットを出力する。
SQLとスナップショットを一緒にGitへ追加し、生成結果の制約、データ移行、既存Workerとの互換性を確認する。

未デプロイの段階でDrizzleを導入したため、初期状態は [0000_initial.sql](../../server/migrations/0000_initial.sql) に統合した。
このSQLがユーザー、セッション、取得元、日別歩数の4テーブルを作成する。
[0002_guest_accounts.sql](../../server/migrations/0002_guest_accounts.sql) は、ゲストを識別する秘密値のハッシュ列を追加し、`google_sub` を任意にする。
SQLiteは列の制約を変更できないため表を作り直す。D1はトランザクション内で `foreign_keys` を切り替えられないため、Drizzle Kitが生成した `PRAGMA foreign_keys` を `PRAGMA defer_foreign_keys` に書き換えた（[D1の外部キー](https://developers.cloudflare.com/d1/sql-api/foreign-keys/)）。

```sh
pnpm db:generate --name add_example
pnpm db:check
pnpm db:migrate:local
```

生成はDB接続や認証情報を必要としない。
`pnpm db:export` は現在のスキーマ全体のSQLを表示する確認用コマンドで、差分の生成・適用には使わない。
データ補正などの手書きSQLは `pnpm db:generate --custom --name backfill_example` で履歴を追加する。
適用済みSQLとスナップショットを書き換えず、以後の変更は新しいマイグレーションにする。

DBへの適用はWranglerに統一し、`drizzle-kit migrate` や `push` は使用しない。
Preview・Dev・Prodの公開処理も同じSQLを使い、適用履歴は `d1_migrations` に記録する。
Drizzleの `meta/` は差分生成に使い、公開artifactにはSQLだけを含める。
公開時はマイグレーションが成功してからWorkerを更新し、失敗時は公開を止める。
DBの変更後にWorkerの公開が失敗した場合、DBの変更は残るため、旧Workerと互換性を保つSQLを用意する。
コードを古いコミットへ戻しても、DBの適用履歴やデータは巻き戻らない。

参考：[DrizzleとD1の接続](https://orm.drizzle.team/docs/sqlite/connect-cloudflare-d1)、[Drizzle KitのSQL生成](https://orm.drizzle.team/docs/drizzle-kit-generate)、[D1のマイグレーション](https://developers.cloudflare.com/d1/reference/migrations/)。

## 検査とMiniflareテスト

| コマンド | 確認内容 |
|---|---|
| `pnpm lint` | Biomeによるlint・整形・import整理 |
| `pnpm lint:fix` / `pnpm format` | ソースの修正・整形 |
| `pnpm types` | Wrangler設定からWorkerとD1の型を生成する |
| `pnpm typecheck` | 型生成後、ソース・テスト・Vitest設定・Drizzle設定を検査する |
| `pnpm db:generate --name <名前>` | DBスキーマから差分SQLとスナップショットを生成する |
| `pnpm db:check` | Drizzleのマイグレーション履歴の整合を検査する |
| `pnpm test` | Workerをビルドし、APIテストとMiniflare結合テストを実行する |
| `pnpm test:watch` | 最初にWorkerをビルドし、Vitestを監視実行する |
| `pnpm test:ci` | 公開スクリプト、SQLだけのartifact生成、DBスキーマの未生成差分を検査する |
| `pnpm build` | Cloudflareへ接続せずWorkerをバンドルする |
| `pnpm artifact` | CI公開用のSQLとビルド情報を `dist/` へまとめる |

アプリとD1の結合テストは [tests/integration/d1.test.ts](../../server/tests/integration/d1.test.ts) に置く。
一時DBにWranglerで全マイグレーションを2回適用して、適用履歴の重複がないことを確認する。
そのDBをMiniflareへ渡し、ビルド済みHonoからのD1接続、パラメーター付きSQLの作成・取得・更新・削除、失敗したバッチのロールバックを検証する。
Drizzle経由の取得・更新と、セッション作成に失敗した場合に期限切れセッションの削除も戻ることを確認する。
テスト用テーブルは一時DBだけに作り、Previewや本番のDBへ投入しない。

`test:watch` 中にHonoの実装を変更した場合は、別のターミナルで `pnpm build` を実行してからテストを再実行する。
Miniflareはバンドル済みのWorkerを使うため、ソースだけを変更しても結合テストのWorkerには反映されない。
ゲストの開始・保存・取得は[ゲストのシナリオ](../../server/tests/scenarios/guest-auth.test.ts)で確認する。
健康データAPIの業務シナリオは、[認証・入力エラー](../../server/tests/scenarios/health-auth.test.ts)、[歩数の同期](../../server/tests/scenarios/health-sync.test.ts)、[セッションの失効](../../server/tests/scenarios/health-session.test.ts) に分ける。
各ファイルは専用のMiniflareとD1を作成し、外部の本人確認をテスト用に差し替えてHonoとDBを検証する。
入力検証の単体テストは [健康データ](../../server/src/features/health/schema.test.ts) と [アカウント](../../server/src/features/accounts/schema.test.ts) の `schema.test.ts` に置き、実装と同じ機能内で管理する。
運動報酬APIの認証・入力エラー・所有者確認は、機能内の [routes.test.ts](../../server/src/features/exercise-rewards/routes.test.ts) で確認する。

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

[Server deploy](../../.github/workflows/server-deploy.yml) は、`develop` へマージされたPRをDevへ、`main` へマージされたPRをProdへ自動公開する。
マージコミットを検査してから公開し、DBがなければ作成し、あれば再利用する。
PRをマージせずに閉じた場合は公開しない。
手動実行では `dev` または `prod` を選んで任意のGit参照を公開できる。
ブランチへの直接pushは自動公開の契機にしない。

### 初回の公開準備

1. 対象Cloudflareアカウントで `workers.dev` のサブドメインを設定する。
2. GitHub Repository secretsへ `CLOUDFLARE_ACCOUNT_ID` と `CLOUDFLARE_API_TOKEN` を登録する。トークンには対象アカウントのWorkers ScriptsとD1の編集権限を付ける。
3. `server-preview`・`server-dev`・`server-prod` の各GitHub environmentに、Unityと同じGoogle OAuthのWebクライアントIDを変数 `GOOGLE_CLIENT_ID` として登録する。全環境で共通ならRepository variablesへ登録してもよい。公開処理はこの値をWorkerのbindingへ渡し、未設定ならDB更新・Worker公開前に停止する。
4. このサーバー基盤をPRのbaseブランチへ先に反映する。ヘルパーがないbaseを使うPreview jobは理由を表示して失敗する。
5. 反映後のbaseを使うPRで、Preview公開とD1疎通を確認する。`develop`・`main` へのマージで、それぞれDev・Prodへ自動公開される。任意のGit参照を公開する場合はGitHub Actionsの `Server deploy` から環境を選択する。

Worker名・DB名は [deploy.mjs](../../server/scripts/deploy.mjs) で環境ごとに固定し、取得したDB IDを使う設定を `.wrangler/deploy/` へ生成する。
実行時に環境を指定するため、リポジトリへ実アカウントのDB IDを記入する必要はない。
環境別の承認や公開元ブランチを制限する場合は、GitHubの `server-dev`・`server-prod` environmentで設定する。

参考：[GitHub Actionsの認証設定](https://developers.cloudflare.com/workers/ci-cd/external-cicd/github-actions/)、[D1の環境分離](https://developers.cloudflare.com/d1/configuration/environments/)。
