# UnityのテストとCI

[Client CI](../../.github/workflows/client-ci.yml) は整形、Analyzer、EditMode、PlayMode、CI補助スクリプトのテストを実行する。
Android APK生成とDrive配布は一時停止中である（[停止範囲と再開方法](client-android-testing.md)）。
Unity Webビルド、ブラウザでの起動確認、Web成果物のWorkers公開は廃止した。

## テストの配置

新規コードは [client/AGENTS.md](../../client/AGENTS.md) の `Assets/Baryonyx/` 配置に従う。
既存のアセンブリ名を維持し、新しいフォルダーからは `.asmref` で参照する。
フォルダー名だけでプレイヤーからテストを除外したとは扱わない。

| アセンブリ | 用途 |
|---|---|
| `Baryonyx.Runtime` | 製品の実装 |
| `Baryonyx.Editor` | Editor専用処理、ビルド |
| `Baryonyx.EditModeTests` | ロジック・設定の検査 |
| `Baryonyx.PlayModeTests` | シーン読み込み、入力、フレームをまたぐ状態遷移 |

[BuildSceneTests](../../client/Assets/Baryonyx/Tests/EditMode/BuildSceneTests.cs) はビルド対象シーンの選択を5件で検査する。
[StartupSceneTests](../../client/Assets/Tests/PlayMode/StartupSceneTests.cs) はSampleSceneの読み込みと有効なカメラを確認する。

## PlayModeのシナリオ

[ScenarioInputFixture](../../client/Assets/Baryonyx/Tests/PlayMode/Support/ScenarioInputFixture.cs) はInput Systemの `InputTestFixture` を継承し、仮想Keyboard・Mouseを用意する。
終了時には元の入力デバイスと設定へ戻す。
`manifest.json` の `testables` とテストアセンブリの参照で、パッケージのテスト支援コードを有効にしている。
CIはプロジェクトのテストアセンブリだけを実行する。

現在の [入力基盤テスト](../../client/Assets/Baryonyx/Tests/PlayMode/Scenarios/ScenarioInputFixtureTests.cs) は押下・解放に伴うInputActionの変化を確認する。
「1週間の歩数」の [画面シナリオ](../../client/Assets/Baryonyx/Features/Health/Tests/PlayMode/HealthScreenScenarioTests.cs) は実Prefabを使い、認証・接続・一覧・JSON詳細とスクロールを検査する。
入力基盤の成功を、ゲームの主要操作の検証済みとは扱わない。

実画面のシナリオでは、ボタンのハンドラーを直接呼ぶ前に仮想入力から操作できるか確認する。
端末機能はEditorで固定応答を返す境界へ差し替える。
生成したオブジェクト、シーン、購読、保存状態は各テストの終了時に片付ける。

画面のレイアウトやフォントを変更したら、[UI設計ルールの検証条件](ui-design.md#機種差を確認する条件)を適用する。
[HealthScreenLayoutTests](../../client/Assets/Baryonyx/Features/Health/Tests/EditMode/HealthScreenLayoutTests.cs) は非対称なSafeArea、最大幅、サイズ変更、0サイズからの復帰を検査する。
PlayModeでは実Prefabへ代表寸法とSafeAreaを適用して配置を確認し、実際の入力からスクロール、詳細の開閉、再有効化を確認する。
配置だけの検査では画面寸法を明示し、Unity Editor上の `Screen.SetResolution` だけでGameビューを変更できたとは扱わない。
自動テスト用のデータはテストアセンブリに置き、Editor向けのサンプルプレビューと区別する。

## 実行と結果確認

Unity Test RunnerまたはUnity CLIで、対象アセンブリを指定して実行する。
対象プロジェクトをEditorで開いている場合、CLIの別起動には検証コピーを使う。

接続中のEditorでは `run_tests --mode editor` または `run_tests --mode playmode` に `--filter <アセンブリ名> --filter_type assembly --async_tests true` を付け、`test_status` で完了と件数を確認する。
Unity 6000.6.0f1でDomain ReloadとScene Reloadを両方省略した状態では、検出済みのPlayModeテストが0件で終了することがあった。
この場合は再生を停止し、検証中だけEnter Play Mode Optionsの省略設定を無効にして再実行する。
検証後は元のEditor設定へ戻し、0件の結果は成功に含めない。

```text
unity test <clientの絶対パス> --mode EditMode --output <結果XMLの絶対パス> --timeout 600 -- -nographics -assemblyNames Baryonyx.EditModeTests
unity test <clientの絶対パス> --mode PlayMode --output <結果XMLの絶対パス> --timeout 600 -- -nographics -assemblyNames Baryonyx.PlayModeTests
python client/ci/verify-test-results.py <結果ディレクトリ> Baryonyx.EditModeTests
python client/ci/verify-test-results.py <結果ディレクトリ> Baryonyx.PlayModeTests
```

CIでは [run-unity-tests.sh](../../client/ci/run-unity-tests.sh) の固定版GameCI CLIを使う。
[verify-test-results.py](../../client/ci/verify-test-results.py) が結果なし、0件、全Skip、テスト・suiteの失敗を拒否する。
Unityを使うjobはAnalyzer、テスト、Androidの順に直列化する。
テストは一つの `Client tests` jobでEditMode、PlayModeの順に実行し、同じrunnerのDockerイメージ、Library、GameCI CLIを再利用する。
Unityはモードごとに起動し、対象アセンブリと結果ディレクトリを分ける。
各モードの開始前に、そのモードの古い結果と前のコンテナーのプロセスID記録だけを除去する。

共通の前提確認とキャッシュ復元が成功し、キャンセルされていなければ、EditModeが失敗してもPlayModeを実行する。
実行または結果検証が失敗した場合はjob全体も失敗し、Androidビルドへ進まない。
XMLとログはモード別の `client-tests-editmode`、`client-tests-playmode` artifactへ7日間保存する。
各モードの実行上限は30分、準備と結果保存を含むjob全体の上限は65分とする。

Libraryはテスト2モード共通の `client-tests-linux-il2cpp-combined-` キーで復元・保存する。
Unity版、Packages、Analyzer、rulesetの組み合わせを区別し、同じ組み合わせなら前のコミットのキャッシュも復元する。
共通キーが見つからない場合は、同じ組み合わせの旧EditMode、旧PlayModeキーを順に探す。
両モードの実行と結果検証を含むjob全体が成功した場合だけ、新しいLibraryキャッシュを保存する。
AnalyzerとAndroidのLibraryは、従来どおり別のキーで管理する。

`Client CI scripts` はUnityを起動せず、実行補助の後始末と結果検証の異常系を検査する。
ローカルでは `python -B -m unittest discover -s client/ci -p 'test_unity_ci.py'` で実行する。
実行補助の検査にはBashが必要で、WindowsではGit for WindowsのBashを使う。
ライセンス設定は [コード品質の手順](client-code-quality.md) を参照する。

必須チェックで旧 `Client tests (editmode)`、`Client tests (playmode)` を指定している場合は、統合後の `Client tests` へ切り替える。
実環境ではキャッシュ有無を分けて所要時間を比較し、2回目のイメージ取得と初期インポートが再利用されることを確認する。

## 関連手順

- [Androidビルドと実機確認](client-android-testing.md)：APKの取得、PRコメント、手動確認の範囲。
- [再構成計画](../plans/2026-09-10-client-ci-backlog.md)：受入条件と実環境での残件。

clientと無関係な変更ではworkflowのパスフィルターが働く。
必須チェック化する前に、対象外PRでも待機し続けない変更検知と集約チェックの運用を確定する。
