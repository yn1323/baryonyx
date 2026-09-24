# サーバーの疎通確認

HonoがHTTPに応答し、接続先のD1へクエリーを実行できることを確認する。
Workers対応とMiniflareでのローカル検証は実装済みで、Cloudflareへの実公開は未確認である。

## 動作と制約

`GET /health` はHTTPの生存確認で、DBの状態に関係なく正常応答を返す。
`GET /ready` はD1の `SELECT 1` を実行し、接続できれば正常応答、binding不足やクエリー失敗なら503を返す。
DBの状態を古いキャッシュで返さないよう、readyの応答には `Cache-Control: no-store` を付ける。
エラーの詳細、DB識別子、認証情報を応答へ含めない。
readyはテーブルの存在、書き込み権限、マイグレーションの適用完了までは判定しない。

応答には `X-Build-SHA` を付け、公開確認では今回ビルドしたコミットと一致することも検査する。
ローカル実行時の値は `local` である。
実際のステータスと応答内容は [ルート定義](../../server/src/app.ts) と [APIテスト](../../server/src/app.test.ts) で管理する。
未定義のパスにはHono標準の404を返す。

これらのAPIは認証を要求せず、DBへの書き込みも行わない。
Google認証・健康データの保存と取得は含まない。
ローカルは `127.0.0.1:4000` で待ち受け、クラウドの公開先は環境ごとのWorkers URLを使う。

## 実装と確認の入口

- サーバー：[app.ts](../../server/src/app.ts)、Workersの入口：[index.ts](../../server/src/index.ts)。
- D1の結合テスト：[d1.test.ts](../../server/tests/integration/d1.test.ts)。
- ローカル起動・環境分離・CI：[バックエンドの開発環境](../rules/backend-design.md)。
- Node.js版を導入した時点の記録：[初期導入計画](../plans/2026-09-09-server-bootstrap.md)。
