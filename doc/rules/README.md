# 設計・開発ルールの索引

[全体索引](../README.md) → 設計と開発ルール

継続して使う設計・開発上の判断基準と手順を置く。
変更ごとの経緯は[計画索引](../plans/README.md)、設定値の所在は[設定索引](../settings/README.md)にある。

## 構成と設計

| 文書 | 内容 |
|---|---|
| [クライアントの構成と依存関係](frontend-design.md) | Unityのディレクトリ構成、シーンと生成メニュー、責務と依存方向、アセンブリ、ゲーム内の画像の置き場所 |
| [バックエンドの開発環境](backend-design.md) | serverの構成と配置、ローカル実行、開発用データ（seed）、Workers・D1・Zod・Drizzle、マイグレーション、公開 |
| [保存先の決め方](data-storage.md) | データを端末とサーバーのDBのどちらに置くかの基準と、今の置き場所の一覧 |
| [UnityのUI設計ルール](ui-design.md) | 画面方向と拡縮、Safe Area、視覚階層、通知の帯、機種差の確認 |

## テストと品質

| 文書 | 内容 |
|---|---|
| [UnityのテストとCI](client-testing.md) | EditMode・PlayModeの実行と結果確認、作業中に実行するテスト、Editorの共有、CI |
| [Unityクライアントの整形と静的解析](client-code-quality.md) | CSharpier・Analyzer・Unityライセンス |
| [serverの検査とテスト](backend-design.md#検査とminiflareテスト) | lint・型検査・Miniflareテスト。並列実行とDBの独立性は[serverのテストの決まり](../../server/tests/scenarios/AGENTS.md)に従う |

## ビルド・配布・端末

| 文書 | 内容 |
|---|---|
| [Androidビルドと実機確認](client-android-testing.md) | APKの設定、CIでの配布とDriveの認証、手動で残す確認 |
| [手動実行用ショートカットの動作](local-shortcuts.md) | APKのビルドとDriveへの配置、エミュレーターの起動、APKのインストールの各ファイルの動作とログ |
| [WindowsでのUnityとAndroidエミュレーター](client-android-emulator.md) | 確認環境の仮想端末と起動引数、初回準備、起動できないときの確認 |

## 文書と依存関係

| 文書 | 内容 |
|---|---|
| [文書管理方針](documentation-policy.md) | 文書の配置と形式、節の区分、正本と変更の記録、仕様追加時の矛盾の扱い、docとスキルの使い分け、保管庫 |
| [Renovateによる依存関係の更新](dependency-updates.md) | 依存関係を更新する条件と対象 |
