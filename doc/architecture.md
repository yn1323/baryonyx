---
id: system-architecture
type: reference
status: 一部確定
updated: 2026-11-02
---

# システム全体の構成

[全体索引](README.md) → 全体構成

## 現在の実装

| 構成 | 現在の責務・確認元 |
|---|---|
| Unity App | [TopSceneController](../client/Assets/Baryonyx/App/Runtime/TopSceneController.cs)・[HomeBootstrap](../client/Assets/Baryonyx/App/Runtime/HomeBootstrap.cs)が起動画面を組み立てる。[GameServices](../client/Assets/Baryonyx/App/Runtime/GameServices.cs)がAndroidの実ProviderとEditor等のプレビューを選び、サーバーとの接続とゲストのセッションをアプリの終了まで全画面で共有する |
| Unity Health | [健康データ仕様](features/health-data.md)と[起動時の同期](features/startup-sync.md)に従い、権限・取得・サーバー保存を扱う。健康データの確認画面は削除済みで、日ごとの歩数はライブラリの[日々の歩数](features/library.md#日々の歩数)で見る |
| Unityの各機能 | サーバーに保存する機能（編成・装備・冒険・商会・神殿・ライブラリ・地図など）は、画面を開くたびにサーバーから読み、操作ごとに保存する（[保存先の決め方](rules/data-storage.md#サーバーに置くときの作り方)） |
| Android連携 | [Androidライブラリ](../client/Assets/Plugins/Android/BaryonyxHealth.androidlib/)からHealth Connectへ接続する |
| アカウント | 端末の秘密値によるゲストで始める。Google認証は健康データ読み取りと独立した任意の操作で、ゲストとの結び付けは未実装 |
| サーバー | [app.ts](../server/src/app.ts)が疎通確認と、アカウント・健康データ・運動報酬・パーティ・装備・冒険・戦闘の記録・ライブラリ・商会・強化・召喚・街道の世界地図・入れ子の地図のAPIと、Prod以外（ローカル・Dev・Preview）で使う確かめ用のAPI（`/v1/debug/*`、[デバッグルームの一覧](features/screens/top-home.md#デバッグルームの一覧)）を組み立てる。Hono・Workers・D1を使用する |
| 同期 | Topの起動時とHomeのACTパネルで、直近7日分の歩数をサーバーへ保存する。ルーンの請求はHomeのACTパネルを押したときだけ行う |

保存する項目と置き場所は[保存先の決め方の置き場所の一覧](rules/data-storage.md#置き場所の一覧)、ゲーム進行の保存要件と未決事項は[アカウントとセーブ](features/accounts-save.md)を参照する。
この表は2026-11-02に [app.ts](../server/src/app.ts) と機能フォルダーを確かめたもので、実環境への公開・認証成功を確認した記録ではない。

## リポジトリの配置

実行・ビルド・依存管理の単位を `client/` と `server/` で分け、機能仕様と開発ルールは `doc/` から横断して参照する。
配置の共通方針はこの節、各実装の具体的な配置はそれぞれの `AGENTS.md` またはそこから参照する設計文書（[クライアントの構成と依存関係](rules/frontend-design.md)・[バックエンドの開発環境](rules/backend-design.md)）で管理する。

```text
baryonyx/
├── client/                 Unityクライアント
│   ├── Assets/            Unityが読み込むコードとアセット
│   ├── Packages/          Unityパッケージの依存管理
│   ├── ProjectSettings/   Unityプロジェクトの設定
│   ├── ArtSource/         Unityに読み込ませない制作元（画像の生成指示の記録）
│   └── ci/                クライアントのビルド・検証補助
├── server/                 Honoバックエンド
│   ├── src/               実装と近接するテスト
│   ├── tests/             結合・横断シナリオのテスト
│   ├── migrations/        DB全体の変更履歴
│   └── scripts/           開発・公開処理とそのテスト
├── doc/                   仕様・設計ルール・計画・QAなどの文書（配置は文書管理方針）
├── shortcuts/            手動実行用の単体スクリプト
├── .github/workflows/     CIの起動条件と実行順序
└── .agents/skills/        特定作業の手順
```

### 配置の共通方針

- 機能単位で整理し、一緒に変更する実装・テスト・専用アセットを近くに置く。ファイルの種類だけでプロジェクト全体を分割しない。
- 専用のものは所有する機能内に置き、複数機能で実際に共有するものだけを `Shared/` または `shared/` へ置く。単に将来使いそうという理由で共通化しない。
- 単体テストと機能内のシナリオテストは対象機能の近くに置く。複数機能をまたぐシナリオは、その範囲をまとめるテスト置き場へ置く。
- 起動処理が機能を組み立て、共通部品は個別機能へ依存させない。物理的な近さと依存方向を分けて設計する。
- clientとserverで同じ業務を扱う機能は、各言語の命名規則に合わせて名前を対応させる。API契約は生成元を一つに定め、言語間の型共有だけを目的としたルートの `shared/` は設けない。
- アセンブリ、ルーティング、外部プラグイン、配信対象、DBマイグレーションなど、技術上の境界を優先する。設定・生成物・キャッシュは、それぞれのツールが管理できる場所へ置く。
- 必要なフォルダーだけを作る。下位の構成図は新規配置・移行時の基準であり、未作成の配置先も含む。既存構成を移行するときは、対象作業に必要な範囲で参照・設定・CI・文書を併せて更新する。

## 企画で決まっている境界

Androidの運動データはHealth Connectへ集約し、扱うのは歩数だけとする。
サービス連携の企画は[運動報酬](features/exercise-rewards.md)、iOS対応の範囲も同文書を参照する。

データを端末とサーバーのDBのどちらに保存するかは、[保存先の決め方](rules/data-storage.md)の基準で決める。
ルーンの増減と報酬は、[アカウントとセーブ](features/accounts-save.md)で決めたとおりサーバーで確定する。

## 未決事項

戦闘の勝敗をサーバーで検証するかは未決。今は端末が勝敗を判定し、サーバーはマスを終えた知らせを受けて報酬を確定する。
オフライン動作、ゲームマスタの実行形式、配信・版管理、複数端末間の整合も未決である。

実装の詳細は[クライアント構成](rules/frontend-design.md)・[バックエンド構成](rules/backend-design.md)、設定場所は[設定索引](settings/README.md)を参照する。

## 変更と判断の記録

経緯は[システム全体の構成の変更と判断の記録](architecture-history.md)に置く。新しい記録はそのファイルに追記する（2026-11-02に移した）。
