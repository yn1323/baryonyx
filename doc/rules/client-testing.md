# UnityのテストとCI

[Client CI](../../.github/workflows/client-ci.yml) は整形、Analyzer、EditMode、PlayMode、Android APK生成を実行する。
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
製品画面の遷移や保存操作を検証するシナリオは、対象画面の実装後に追加する。
入力基盤の成功を、ゲームの主要操作の検証済みとは扱わない。

実画面のシナリオでは、ボタンのハンドラーを直接呼ぶ前に仮想入力から操作できるか確認する。
端末機能はEditorで固定応答を返す境界へ差し替える。
生成したオブジェクト、シーン、購読、保存状態は各テストの終了時に片付ける。

## 実行と結果確認

Unity Test RunnerまたはUnity CLIで、対象アセンブリを指定して実行する。
対象プロジェクトをEditorで開いている場合、CLIの別起動には検証コピーを使う。

```text
unity test <clientの絶対パス> --mode EditMode --output <結果XMLの絶対パス> --timeout 600 -- -nographics -assemblyNames Baryonyx.EditModeTests
unity test <clientの絶対パス> --mode PlayMode --output <結果XMLの絶対パス> --timeout 600 -- -nographics -assemblyNames Baryonyx.PlayModeTests
python client/ci/verify-test-results.py <結果ディレクトリ> Baryonyx.EditModeTests
python client/ci/verify-test-results.py <結果ディレクトリ> Baryonyx.PlayModeTests
```

CIでは [run-unity-tests.sh](../../client/ci/run-unity-tests.sh) の固定版GameCI CLIを使う。
[verify-test-results.py](../../client/ci/verify-test-results.py) が結果なし、0件、全Skip、テスト・suiteの失敗を拒否する。
Unityを使うjobはAnalyzer、テスト、Androidの順に直列化する。
ライセンス設定は [コード品質の手順](client-code-quality.md) を参照する。

## 関連手順

- [Androidビルドと実機確認](client-android-testing.md)：APKの取得、PRコメント、手動確認の範囲。
- [再構成計画](../plans/2026-09-10-client-ci-backlog.md)：受入条件と実環境での残件。

clientと無関係な変更ではworkflowのパスフィルターが働く。
必須チェック化する前に、対象外PRでも待機し続けない変更検知と集約チェックの運用を確定する。
