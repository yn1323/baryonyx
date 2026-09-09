# serverのHono開発環境とCIの導入計画

状態：完了（実装・ローカル検証・文書更新）
作成日：2026-09-09
更新日：2026-09-10

`server/` にHonoの開発環境とGitHub ActionsのCIを導入した。
ローカル検証と関連文書の更新は完了している。
GitHub上での初回CI実行は、push後の確認事項として残る。

## 必須要件と実装範囲

- Hono、pnpm、Biome、Vitestを使用する。
- npm scriptsでbuild・lint・testを実行でき、`pnpm format` と `npm run format` でBiomeの整形を実行できる。
- ソース・テスト・設定ファイルの型チェックを行う。
- Node.js・pnpm・依存パッケージのバージョンとlockfileを固定する。
- 疎通確認用の `GET /health` と、導入・開発起動・build後の起動手順を用意する。
- GitHub Actionsを全ブランチのpushとPRで実行し、lint・型チェック・test・buildを検査する。
- 実装に合わせて構成・機能・手順の文書を更新し、検証結果と未確認事項を残す。

Cloudflare、認証、DB・ORM、歩数同期、ルーン計算、デプロイは対象外とした。
環境構築は当初のブランチと作業ディレクトリで行い、構築時点ではcommit・push・PR作成を行わなかった。

## 採用した構成と判断

着手時の `server/` には `.gitkeep` だけがあり、JavaScriptの設定やCIはなかった。
Node.jsで実行する単独のTypeScriptプロジェクトを追加した。

| 項目 | 採用内容 | 判断理由 |
|---|---|---|
| 実行環境 | Node.js 24.21.0 LTS | ローカルとCIの実行環境を固定する |
| HTTP | Hono、`@hono/node-server` | Hono公式のNode.jsアダプターで起動する |
| 言語とビルド | TypeScript、ESM、strict、`tsc` | 型チェックとNode.js用のJavaScript生成を行う |
| パッケージ管理 | pnpm 12.3.4、単独パッケージ | 現時点では複数パッケージの管理が不要である |
| 整形と静的検査 | Biome | lint・整形・import整理を共通の設定で検査する |
| テスト | VitestのNode環境と `app.request()` | 応答テストのためにHTTPサーバーを起動せずに済む |
| 開発起動 | `tsx watch` | ソースの変更を開発サーバーへ反映する |

Honoのアプリ定義とNode.jsでの起動処理を分離した。
テストはアプリ定義だけを読み込み、実行用ビルドにはテストとVitestの設定を含めない。
型チェックはそれらを含めて実行する。

VitestはHono公式にも使用例があり、別のテストランナーは追加しなかった。
公式ガイドのCloudflare Workers用の実行環境は導入していない。参照：[HonoのNode.jsガイド](https://hono.dev/docs/getting-started/nodejs)、[Honoのテストガイド](https://hono.dev/docs/guides/testing)。

実装時には、Vitestが必要とするViteも開発依存として固定した。
pnpm 12ではesbuildのインストール処理に明示的な許可が必要だったため、`pnpm-workspace.yaml` にesbuildだけの許可を記した。
このファイルはpnpmの設定用であり、複数パッケージの構成は追加していない。
Biomeの設定は、採用版の推奨形式である `preset: "recommended"` に揃えた。

具体的な依存バージョンは [package.json](../../server/package.json)、生成された依存関係は [pnpm-lock.yaml](../../server/pnpm-lock.yaml) に記録している。
継続して使う配置・コマンド・更新手順は [バックエンドの開発環境](../rules/backend-design.md) に集約した。

## 実施結果

Windowsの作業環境で、固定したNode.js 24.21.0とpnpm 12.3.4を使用して検証した。
検証用の実行環境は一時フォルダーに置き、PC全体のNode.js・pnpm設定は変更していない。

| 確認項目 | 結果 |
|---|---|
| pnpmの初回セットアップ | Node.js 24付属のCorepackで固定版を取得し、scriptの実行と失敗時の終了コードの伝播を確認した |
| `pnpm install --frozen-lockfile` | 成功。lockfileに変更がないことも確認した |
| `pnpm format` と `npm run format` | 両方成功。同じBiomeの整形を実行した |
| `pnpm lint` | 成功。未整形コードを一時的に加えた場合は失敗した |
| `pnpm typecheck` | 成功。テストとVitest設定に加えた型エラーを両方検出した |
| `pnpm test` | 正常応答と404の2件が成功。一時的な失敗テストでは終了コードが失敗になった |
| `pnpm build` | 成功。`dist/` には実行用の2ファイルを出力した。ソースの型エラーでは失敗し、新しいJavaScriptを出力しなかった |
| `pnpm dev` | 実HTTPで `/health` の200・JSON応答と、未定義パスの404を確認した |
| build後の `pnpm start` | 開発起動と同じ実HTTPの応答を確認した |
| GitHub Actionsの設定 | actionlint 1.7.12で構文を検査した。Actionの入力と `server/` 配下の参照パスも確認した |

検査の失敗を確認するための一時ファイルは削除し、変更した設定は元に戻した。
その後、lint・型チェック・test・buildがすべて成功することを確認した。
疎通確認用に起動したプロセスも停止した。

## 文書の反映先

- [バックエンドの開発環境](../rules/backend-design.md)：導入、起動、整形、検査、配置、CIの運用。
- [サーバーの疎通確認](../features/server-health.md)：目的、動作、制約、実装の入口。
- [機能一覧](../features/README.md)：疎通確認の実装状況。
- [文書一覧](../README.md)：上記資料への入口。

## 未確認事項と後続の候補

GitHub上でのCI実行は未確認である。
最初のpush後にActions画面で、依存のインストールと全検査の成功を確認する。
macOS・Linuxでの実行は未確認であり、Windowsでの検証結果とは区別する。
実環境へのデプロイは今回の範囲に含めていない。

| 後続の候補 | 導入する時点 |
|---|---|
| 環境変数の検証と `.env.example` | 外部接続や設定値が必要になったとき |
| 入力検証、OpenAPI | ゲームAPIとUnity側の通信仕様を決めるとき |
| 共通エラー応答とログ方針 | ゲームAPIのエラーをクライアントで扱い始めるとき |
| カバレッジ計測 | ルーン計算などのロジックを追加したとき |
| 依存関係の更新支援 | CIの初回成功後、更新運用を決めるとき |
