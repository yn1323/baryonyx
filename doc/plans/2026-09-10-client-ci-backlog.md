# クライアントCIの再構成計画

状態：進行中（実装済み。GitHubでの実行・APKコメントの確認は未実施）
作成日：2026-09-10
更新日：2026-09-11

Android向け開発に合わせ、WebビルドとクライアントのCloudflare Workers公開CIを削除する。
2026-09-11に方針を見直し、VRTの実装・依存関係・Docker拡張・R2公開・差分承認もすべて削除することにした。
Androidはビルドの成否確認とAPK保存に絞り、生成後の詳細検査をCIへ追加しない。
サーバー側のWorkersはこの変更の対象外とする。

## 確定した構成

| 対象 | 動作 |
|---|---|
| 整形・Analyzer | 既存のC#整形・静的解析を維持する |
| EditMode | ロジック・設定・ビルド対象シーンを検査する |
| PlayMode | シーン起動と仮想入力を検査する。実画面のシナリオは画面実装後に追加する |
| Androidビルド | UnityでAPKを生成し、ビルド失敗ならCIを失敗にする |
| APK保存 | 成功したAPKをGitHub Actions artifactへ7日間保存する |
| PRコメント | 同一リポジトリのPRでビルドに成功したら、APKのダウンロードリンクを掲載する |

Unityを使う処理はAnalyzer、テスト、Androidの順に実行する。
APKのSDK・署名・CPU種別・内部ファイルをPythonで追加検査する処理は削除した。
Health Connectの組み込み検査もAndroid CIでは実行せず、既存の手動確認用スクリプトを残す。
Androidエミュレーターによるスモーク・E2Eは導入しない。

## 実装

- [Client CI](../../.github/workflows/client-ci.yml)：既存チェック、Androidビルド、APK保存、PRコメント。
- [AndroidBuild](../../client/Assets/Baryonyx/Editor/CI/AndroidBuild.cs)：検証用Development APKの生成とビルド成否判定。
- [BuildScenes](../../client/Assets/Baryonyx/Editor/CI/BuildScenes.cs)：既存Webビルドから引き継いだ有効シーンの選択。
- [BuildSceneTests](../../client/Assets/Baryonyx/Tests/EditMode/BuildSceneTests.cs)：シーン選択の5件の検査。
- [ScenarioInputFixture](../../client/Assets/Baryonyx/Tests/PlayMode/Support/ScenarioInputFixture.cs)：仮想Keyboard・Mouseを使うPlayModeシナリオの土台。

継続的な手順は [UnityのテストとCI](../rules/client-testing.md) と [Androidビルドと実機確認](../rules/client-android-testing.md) にまとめる。
APKリンクにはGitHub Actionsのartifact URLを使用するため、Cloudflareのバケット・URL・認証情報は不要である。

## 検証と残件

- 変更前の同じビルド処理で約64.5 MBのAndroid APK生成に成功した。今回の簡素化では追加検査と独自レポート出力を削除した。
- 既存の検証でEditMode 14件、PlayModeの起動・仮想入力2件が成功した。
- 簡素化後にUnityのコンパイルとシーン選択5件、C#整形、workflowの構文検査が成功した。PRコメントは通信を伴わない検証で新規作成・更新・古いコミット・closed PR・URL欠落を確認し、文書リンク68件にも欠落はなかった。
- GitHub上でのAndroidビルド、APK保存、PRコメントの実反映は未確認。
- 実画面の操作シナリオ、実機でのインストール・起動・復帰・権限・保存状態・歩数取得の確認は残る。
