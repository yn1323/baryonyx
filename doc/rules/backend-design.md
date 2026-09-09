# バックエンドの開発環境

バックエンドは `server/` のHonoアプリをNode.jsで実行する。
TypeScriptのESM構成とし、pnpmで依存関係を管理する。
Cloudflare、認証、DB、デプロイの設定は含めていない。

## バージョンと依存関係

Node.jsは [`.node-version`](../../server/.node-version)、pnpmと直接の依存パッケージは [package.json](../../server/package.json) に固定する。
Node.jsの対応系列はpackage.jsonの `engines.node` にも記す。
推移的な依存関係を含むインストール結果は [pnpm-lock.yaml](../../server/pnpm-lock.yaml) で固定する。

実行時に必要なパッケージは `hono` と `@hono/node-server` である。
TypeScript、Node.jsの型定義、tsx、Biome、Vitest、Vitestが使用するViteは開発時の依存パッケージとして管理する。

[pnpm-workspace.yaml](../../server/pnpm-workspace.yaml) は、tsxが使用するesbuildのインストール処理を許可するために置く。
このファイルには複数パッケージの定義を追加せず、`server/` 単独のプロジェクトとして扱う。
lockfileはpnpmで更新し、手で編集しない。

## 初回の準備

リポジトリ直下から `server/` に移動する。
以降のコマンドはWindows・macOSとも、このディレクトリから実行する。

```sh
cd server
```

`.node-version` に記載されたNode.jsを用意する。
既存のバージョン管理ツールを使う場合も、このファイルの値に合わせる。
インストーラーを使う場合は [Node.jsの公式配布一覧](https://nodejs.org/dist/) から指定バージョンを選ぶ。

pnpmをまだ導入していない場合は、Node.js 24に付属するCorepackを有効にする。
Corepackはpackage.jsonの `packageManager` を読み、指定されたpnpmを用意する。

```sh
corepack enable pnpm
node --version
pnpm --version
```

初回はpnpmのダウンロード確認が表示される場合がある。
Node.jsが `v24.21.0`、pnpmが `12.3.4` と表示されることを確認してから、依存パッケージをインストールする。
バージョンを変更した際は、この手順も更新する。

```sh
pnpm install --frozen-lockfile
```

インストールにはレジストリへのネットワーク接続が必要である。
設定値や秘密情報を用意せずに起動できる。

## 開発起動とビルド後の起動

開発中は、ファイル変更時に再起動するコマンドを使う。

```sh
pnpm dev
```

起動したら、ブラウザーで [ローカルの疎通確認](http://127.0.0.1:3000/health) を開く。
正常時のJSON応答を確認できる。
起動中のターミナルで `Ctrl+C` を押すと停止する。

生成したJavaScriptでの動作は、次の順で確認する。

```sh
pnpm build
pnpm start
```

`start` は事前に生成された `dist/index.js` を実行する。
ソースを変更した後は、再度buildしてから起動する。
開発起動とビルド後の起動は同じポートを使うため、同時には実行しない。

待ち受け先は `127.0.0.1:3000` とし、同じPCから利用する。
UnityのAndroid実機など、別の端末から接続するための待ち受け設定は未導入である。

## 整形と検査

開発者とCIは、同じpackage.jsonのscriptsを使う。

| コマンド | 用途 |
|---|---|
| `pnpm format` / `npm run format` | Biomeで対応ファイルを整形する |
| `pnpm lint` | Biomeでlint・整形・import整理を検査する。警告も失敗にする |
| `pnpm lint:fix` | 整形と安全なlint・import整理の修正を適用する |
| `pnpm typecheck` | ソース・テスト・Vitest設定を型チェックする |
| `pnpm test` | Vitestを1回実行して終了する |
| `pnpm test:watch` | 変更を監視してテストを再実行する |
| `pnpm build` | 実行用のJavaScriptを生成する |

CIと同じ検査を手元で実行するときは、次の順で行う。

```sh
pnpm lint
pnpm typecheck
pnpm test
pnpm build
```

リポジトリ直下から実行する場合は、たとえば `pnpm --dir server test` と書く。
依存の追加・更新にはpnpmを使い、npmは必要に応じたscriptの実行に使う。

Biomeの規則は [biome.json](../../server/biome.json) に集約する。
推奨のlint規則、2スペースのインデント、LF改行を使用する。
生成物、依存パッケージ、カバレッジ出力は検査対象から除き、Gitにも追加しない。
[.gitattributes](../../server/.gitattributes) でも `server/` 内のテキストをLFに揃える。

## ソースとテストの配置

| ファイル | 責務 |
|---|---|
| [src/app.ts](../../server/src/app.ts) | Honoアプリとルートの定義。HTTPサーバーを起動しない |
| [src/index.ts](../../server/src/index.ts) | Node.jsアダプターによる起動と終了シグナルの処理 |
| [src/app.test.ts](../../server/src/app.test.ts) | `app.request()` で正常応答と未定義パスの応答を確認する |
| [vitest.config.ts](../../server/vitest.config.ts) | Node環境で `src/**/*.test.ts` を実行する |
| [tsconfig.json](../../server/tsconfig.json) | ソース・テスト・Vitest設定のstrictな型チェック |
| [tsconfig.build.json](../../server/tsconfig.build.json) | 実行用ソースを `dist/` へ出力する。テストと設定は出力しない |

相対importは、Node.jsで生成後のファイルを解決できるよう `.js` 拡張子で書く。
buildは型エラーがあると失敗し、新しいJavaScriptを出力しない。
既存の `dist/` は残るため、buildが失敗した場合はstartへ進まない。

APIの応答テストは、ポートを開かない `app.request()` を基本とする。
起動処理やビルド設定を変えたときは、実際に起動してHTTPでの疎通も確認する。
テストが0件の状態を成功にする設定は使用しない。

## GitHub Actions

[server-ci.yml](../../.github/workflows/server-ci.yml) は、PRの作成・更新と `main` へのpushで実行する。
作業ブランチへのpushでは起動せず、PRとの二重実行を避ける。
変更パスによる絞り込みはなく、クライアントや文書だけの変更も対象になる。
Ubuntuの1ジョブで依存をインストールし、lint、型チェック、test、buildを順に実行する。
いずれかのstepが失敗すると、後続stepはスキップされる。

CIはリポジトリの設定からNode.js・pnpmのバージョンを読み、`--frozen-lockfile` でインストールする。
pnpmのstoreをlockfileに基づいてキャッシュする。
使用するActionはコミットSHAで固定し、リポジトリの権限は `contents: read`、ジョブの制限時間は10分とする。
デプロイや外部サービス用のSecretsは必要ない。

バージョンを更新したときは、設定とlockfileを更新し、上記のローカル検査と起動確認を行う。
GitHub上でのCI実行は、push後にリポジトリのActions画面で確認する。
