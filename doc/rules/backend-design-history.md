---
id: rule-backend-design-history
type: reference
status: 記録
updated: 2026-11-01
---

# バックエンドの開発環境の変更と判断の記録

[バックエンドの開発環境](backend-design.md)


- 2026-11-01：ユーザーの「タップでルーン獲得が重い」を受け、Homeの歩数のパネルを押したときの4回の要求（`POST /v1/health/syncs`・`PUT /v1/health/sources/:id/days`・`POST /v1/exercise/rewards/claim`・`GET /v1/health/sources/:id/days`）を、保存と請求を1回で行う `POST /v1/exercise/rewards/sync` にまとめた。D1との往復は、セッションを覚えたあとで版の発行・保存・請求の3回。あわせて請求（`claim`）の、要求IDの記録を先に読む往復と、書いたあとに読み直す2回の往復をやめ、記録と日ごとの変換を書き込みと同じbatchで読むようにした（[D1との往復と文の数](backend-design.md#d1との往復と文の数)）。再送は記録を書けずに前回の記録を読んで返すため、結果は変わらない。
- 2026-10-25：ユーザーの「サーバーのリファクタ（マイグレーション以外）と、API読み込みのbatch化、セッション確認の1往復を減らす」を受け、次のように変えた。参考：[D1のbatch](https://developers.cloudflare.com/d1/worker-api/d1-database/#batch)、[D1の上限](https://developers.cloudflare.com/d1/platform/limits/)。
  - 往復：書き込みのAPIは書き込みと書いたあとの読み込みを1回のbatchにし、[D1との往復と文の数](backend-design.md#d1との往復と文の数)を定めた。テストで数えたD1との往復（セッションの確認を含む平均）は、`POST /v1/adventure/buy` が7.7回から1.7回、`/adventure/end` が8.0回から2.0回、`/adventure/clear` が6.0回から1.9回、`/adventure/move` が5.0回から2.0回、`POST /v1/enhancement/polish`・`awaken` が約6回から1.8回、`POST /v1/workshop/craft`・`sell` が約5回から1.8回、`POST /v1/summon` が5.0回から1.5回、`GET /v1/library` が5.1回から1.6回になった。`POST /v1/exercise/rewards/claim`（5.8回）は今回変えていない。
  - セッション：確かめたセッションをWorkerのメモリに最長60秒覚えるようにした（ユーザーが3案から選んだ）。2回目からの一覧を読むGETは、D1との往復が1回になった。
  - 初期データ：読み込みのたびに付与を確かめていたのをやめ、ログインしたときに付与するようにした。付与の行は表ごとに1つの文（`insertRowsWhen`）にまとめ、Miniflareでの初回ログインは約5秒から約1.5秒になった。
  - 共通化：持ち物の付与（[inventory/grants.ts](../../server/src/features/inventory/grants.ts)）、ルーンの記録（`runeRecord`）、エラーのステータス（`errorStatus`）、重み付きの抽選、倍率と品質の応答をまとめた。冒険・編成・商会・強化のrepositoryを操作ごとのファイルに分けた。
  - エラー：同じエラーで機能ごとにステータスが違ったため、`goods_not_found` を冒険でも404にし、編成の「冒険の時間外」は `no_adventure`（404）と分けて `not_in_adventure`（409）にした。クライアントはどちらもステータスで分岐していない。
  - 見送り：API契約の生成元とマスターデータの同期は、方針を決めてから別に進める（ユーザーの判断）。seedの表と列の手書きは、Node.jsがseed.tsを直接読み込みsrcのimportを解決できないため残した。
- 2026-10-24：ユーザーの依頼で、街道の世界地図の進み具合を保存する `GET /v1/world-map`・`POST /v1/world-map/move` と2テーブル（[0025_world_map.sql](../../server/migrations/0025_world_map.sql)）を足し、seedの3人に進み具合を入れた。マスと道の形はクライアントだけが持つため、サーバーはマスのIDの形だけを確かめる。
- 2026-10-24：ユーザーの指示でACTボーナスを廃止し（[記録](../archive/step-bonus.md)）、APIと3テーブル（[0024_remove_step_bonus.sql](../../server/migrations/0024_remove_step_bonus.sql)）とseedのボーナスを外した。開発中の確かめ用に、ルーンを10,000足す `POST /v1/debug/runes` を足した（ローカルとDevだけで使え、Prodでは404）。
- 2026-10-22：ユーザーの「段階3（往復の削減）だけ進めて」を受け、一覧を読むGETのD1との往復を減らした。セッションの確認の二重登録を外し（party・equipment・adventure・library・step-bonus）、partyとequipmentは初回の付与の印を読み込みのbatchに入れた。2回目からの往復は、`GET /v1/party` が4回から2回、`GET /v1/equipment` が6回から2回、`GET /v1/library` が7回から3回、`GET /v1/adventure` が3回から2回、`GET /v1/step-bonus` が6回から5回になった。書き込みのAPIも同じ読み方にし、partyは1往復、equipmentは3往復ずつ減った。
- 2026-10-22：ユーザーの「キャラのスキル表示に1秒ちょっとかかる。何が原因なのか」を受け、[処理時間の計測](backend-design.md#処理時間の計測server-timing)を加えた。Miniflareで数えると、2回目以降の読み込みのD1の往復は `GET /v1/party` が4回、`GET /v1/equipment`・`GET /v1/step-bonus` が6回、`GET /v1/library` が7回、`GET /v1/adventure` が3回だった。各機能が `use("/party")` と `use("/party/*")` の両方へ `requireSession` を付けており、Honoでは `/party/*` が `/party` にも一致するため、一覧を読むGETでセッションの確認が2回走っている。往復を減らす変更は、計測の結果を見てから別に行う。参考：[Hono Timing Middleware](https://hono.dev/middleware/builtin/timing)、[Workerの配置](https://developers.cloudflare.com/workers/configuration/placement/)。
- 2026-10-22：ユーザーの「東京を指定したい。DBもHonoのserverも」を受け、[deploy.mjs](../../server/scripts/deploy.mjs) でD1の作成時に `primary_location_hint: "apac"` を、Workerに `placement.region: "aws:ap-northeast-1"` を指定した。それまでは場所を指定せず、作成を実行したGitHub Actionsの近く（北米と推定）にDBができていた。参考：[D1のデータの場所](https://developers.cloudflare.com/d1/configuration/data-location/)、[D1作成API](https://developers.cloudflare.com/api/resources/d1/subresources/database/methods/create/)、[Workerの配置](https://developers.cloudflare.com/workers/configuration/placement/)。
- 2026-10-19：[品質](../features/inventory.md#品質)の実装で、[0012_item_quality.sql](../../server/migrations/0012_item_quality.sql) を[マイグレーション](backend-design.md#マイグレーション)に加え、seedの装備とボーナスに倍率を持たせた。Drizzle Kitが列の新設か改名かを対話で尋ねて止まるため、疑似端末から既定の答え（新設）を選んで生成した。
- 2026-10-05：ルートの [AGENTS.md](../../AGENTS.md#作業の基本) の「未リリースのため、Migration、既存データとの互換性、旧バージョンとの互換性は考慮しない」に合わせ、[マイグレーション](backend-design.md#マイグレーション)の「既存Workerとの互換性を確認」「旧Workerと互換性を保つSQLを用意する」を、未リリースの間は考慮せずDBを作り直す扱いに改めた。リリース前に見直す。あわせて、[開発用データ（seed）](backend-design.md#開発用データseed)を加え、serverのAGENTS.mdにあったディレクトリ構成と配置をこの文書へ移した。
