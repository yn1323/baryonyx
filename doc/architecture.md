---
id: system-architecture
type: reference
status: 一部確定
updated: 2026-09-13
---

# システム全体の構成

[全体索引](README.md)

## 現在の実装

| 構成 | 現在の責務・確認元 |
|---|---|
| Unity App | [HealthScreenBootstrap](../client/Assets/Baryonyx/App/Runtime/HealthScreenBootstrap.cs)がAndroidの実ProviderとEditor等のプレビューを選ぶ |
| Unity Health | [健康データ仕様](features/health-data.md)に従い、認証・権限・取得・一覧・JSON詳細を扱う |
| Android連携 | [Androidライブラリ](../client/Assets/Plugins/Android/BaryonyxHealth.androidlib/)からHealth Connectへ接続する |
| Google認証 | 健康データ読み取りと独立した任意の操作。詳細は健康データ仕様を参照 |
| サーバー | [app.ts](../server/src/app.ts)が疎通確認と健康データAPIを組み立てる。Hono・Workers・D1を使用する |
| 同期 | 同期クライアントとサーバーAPIは存在するが、現在の起動経路からは呼ばない |

現在の画面は健康データをメモリで扱う。
ゲーム進行の保存方針は[アカウントとセーブ](features/accounts-save.md)で別途決める。
現在の実装入口と機能フォルダーを2026-09-13に確認したもので、実環境への公開・認証成功を確認した記録ではない。

## 企画で決まっている境界

Androidの運動データはHealth Connectへ集約する。
サービス連携の企画は[運動報酬](features/exercise-rewards.md)、iOS対応の範囲も同文書を参照する。

## 未決事項

ルーン・報酬・戦闘結果をクライアントとサーバーのどちらで確定するかは未決。
オフライン動作、ゲームマスタの実行形式、配信・版管理、複数端末間の整合も未決である。

実装の詳細は[クライアント構成](rules/frontend-design.md)・[バックエンド構成](rules/backend-design.md)、設定場所は[設定索引](settings/README.md)を参照する。
