# UnityのテストとCI

[Client CI](../../.github/workflows/client-ci.yml) は整形、Analyzer、EditMode、PlayMode、CI補助スクリプトのテストを実行する。
Android APK生成とDrive配布は一時停止中である（[停止範囲と再開方法](client-android-testing.md)）。
Unity Webビルド、ブラウザでの起動確認、Web成果物のWorkers公開は廃止した。

## テストで検査するもの

テストは、ロジックとその出力結果を検査する。
入力を与えて、計算結果、生成されたオブジェクト、状態遷移、画面遷移が期待どおりになるかを確かめる。

見た目を調整するための値は検査しない。
Bloomのしきい値、ティルトシフトのピント幅、色、透明度、大きさ、位置、演出の時間、フォントサイズ、遷移の種類などが該当する。
「どのアセット・シーン・Rendererに、どの値や機能を設定したか」も同じ扱いにする。
これらはInspectorや生成スクリプトで調整する値であり、調整のたびにテストの期待値を直すことになるうえ、ロジックの誤りは見つからないためである。
値を変えたときの見た目は、[展示室](../features/showcase.md)とUnity Editorで確認する。

次のものは検査してよい。

- **ロジックへ明示的に渡す入力**：計算の入力として調整値を渡し、出力を確かめる（例：ティルトシフトのぼかし量の計算にピント幅を渡す）。
- **要件として決めた条件**：タップ領域の最小サイズ、装飾が入力を遮らないこと、全画面で開始操作を受け付けること、フォントの統一、[UI設計ルール](ui-design.md)の機種差の条件など、仕様書やルールで決めた条件。
- **ロジックの実行に必要な参照**：テスト対象を動かすために必要なPrefabやコンポーネントの参照がそろっていること。

## テストの配置

コードとテストは [クライアントの構成と依存関係](frontend-design.md) に従い、`Assets/Baryonyx/` に配置する。
4つのアセンブリ定義も同じ配下に置き、App・機能専用のEditor処理とテストは `.asmref` で対応するアセンブリへ所属させる。
Runtimeはルートの `Baryonyx.Runtime.asmdef` に所属する。
フォルダー名だけでプレイヤーからテストを除外したとは扱わない。

| アセンブリ | 用途 |
|---|---|
| `Baryonyx.Runtime` | 製品の実装 |
| `Baryonyx.Editor` | Editor専用処理、ビルド |
| `Baryonyx.EditModeTests` | ロジックとその出力の検査 |
| `Baryonyx.PlayModeTests` | シーン読み込み、入力、フレームをまたぐ状態遷移 |

[BuildSceneTests](../../client/Assets/Baryonyx/Tests/EditMode/BuildSceneTests.cs) はビルド対象シーンの選択を5件で検査する。
[TopHomeSceneTests](../../client/Assets/Baryonyx/App/Tests/PlayMode/TopHomeSceneTests.cs) はTopの読み込み、LOADINGと再試行、連携モーダル、Homeへの遷移を、[ShowcaseSceneTests](../../client/Assets/Baryonyx/App/Tests/PlayMode/ShowcaseSceneTests.cs) は展示室シーンの読み込みを確認する。

## PlayModeのシナリオ

[ScenarioInputFixture](../../client/Assets/Baryonyx/Tests/PlayMode/Support/ScenarioInputFixture.cs) はInput Systemの `InputTestFixture` を継承し、仮想Keyboard・Mouseを用意する。
終了時には元の入力デバイスと設定へ戻す。
`manifest.json` の `testables` とテストアセンブリの参照で、パッケージのテスト支援コードを有効にしている。
CIはプロジェクトのテストアセンブリだけを実行する。

現在の [入力基盤テスト](../../client/Assets/Baryonyx/Tests/PlayMode/Scenarios/ScenarioInputFixtureTests.cs) は押下・解放に伴うInputActionの変化を確認する。
案内人がいる画面の [シーンテスト](../../client/Assets/Baryonyx/App/Tests/PlayMode/GuideScenesTests.cs) は、Homeの4つのボタンから各画面へ移って戻る流れを1件で通し、メニュー・リスト・決定の通知、地図の印の選択を検査する。
ホーム画面の [シーンテスト](../../client/Assets/Baryonyx/Features/Home/Tests/PlayMode/HomeSceneTests.cs) は実シーンを使い、仮データと、保存済みの歩数を換算したUPTの表示、タップ領域、ボタンの反応、歩数の同期を検査する。
ルーンの獲得は、[演出のテスト](../../client/Assets/Baryonyx/Features/Home/Tests/PlayMode/HomeRuneTapTests.cs) が仮想入力で押し、同期したUPTと同じ量が付与されて代役のサーバーに残ることを確かめる。
Top・Home・案内人の画面のシーンテストは [TestGameServices](../../client/Assets/Baryonyx/Tests/PlayMode/Support/TestGameServices.cs) でHealth Connectとゲームサーバーを端末内の代役へ差し替え、設定アセットのサーバーURLへ接続しない。
入力基盤の成功を、ゲームの主要操作の検証済みとは扱わない。

### シーンテストを軽く保つ

PlayModeの時間の大半は、シーンの読み込みと遷移演出（閉じる・開くとも0.75秒）の待ち時間である。
全体の流れを1回は通しつつ、同じ待ち時間を繰り返さないよう、次のように書く。

- `TestGameServices` は遷移演出の時間を0.05倍にする。演出の途中の入力を確かめるテストだけ、`SceneTransitionController.DurationScale` を1に戻す。
- シーンの読み込み・条件の待機・後片付け・タップ領域の検査は [SceneTests](../../client/Assets/Baryonyx/Tests/PlayMode/Support/SceneTests.cs) を使う。固定秒数の `WaitForSeconds` で待たず、条件がそろうまでフレームを進める。
- 同じ画面を往復する流れは、ボタンごとにテストを分けず、1件で順に通す。
- ロジックの組み合わせ（計算・状態遷移）はEditModeで検査し、PlayModeでは画面とのつなぎ込みを1回確かめる。
- 1画面の静的な確認（表示・タップ領域）で、演出の完了を待たない。演出を確かめるテストは、演出を待つ1件にまとめる。

実画面のシナリオでは、ボタンのハンドラーを直接呼ぶ前に仮想入力から操作できるか確認する。
端末機能はEditorで固定応答を返す境界へ差し替える。
生成したオブジェクト、シーン、購読、保存状態は各テストの終了時に片付ける。

画面のレイアウトやフォントを変更したら、[UI設計ルールの検証条件](ui-design.md#機種差を確認する条件)を適用する。
PlayModeでは実Prefabへ代表寸法とSafeAreaを適用して配置を確認し、実際の入力から操作を確認する。
配置だけの検査では画面寸法を明示し、Unity Editor上の `Screen.SetResolution` だけでGameビューを変更できたとは扱わない。
自動テスト用のデータはテストアセンブリに置き、Editor向けのサンプルプレビューと区別する。
健康データのPresenterはEditModeで、固定応答を返すProviderを使って状態遷移を検査する。
Appの起動テストは `App/Tests/PlayMode/` に置き、起動シーンの読み込みと遷移を確認する。

## 作業中に実行するPlayModeテスト

PlayModeテストはシーンの読み込みとフレームの経過を待つため、1件あたりの時間がEditModeより長い。
2026-09-29にmacOSのEditorで全件を実行したときのテスト本体の実行時間は、EditMode 223件の合計が約23秒、PlayMode 29件が約12秒だった（遷移演出を短くする前は34件で約50秒）。
作業中は関連するPlayModeテストだけを実行し、全件はcommit前に実行する（[commitの手順](../../AGENTS.md#commitの手順)）。
EditModeは全件でも30秒ほどで終わるため、作業中も `Baryonyx.EditModeTests` を全件実行する。

関連するPlayModeテストは、次の順に選ぶ。

1. 変更したファイルと同じ機能の `Tests/PlayMode/` にあるテスト。
2. 変更した型・シーン・Prefabを参照する、他の場所のPlayModeテスト。テストコードを型名・シーン名・Prefab名で検索して探す。
3. 次の変更では全件を実行する。
   - `Tests/PlayMode/Support/` の変更
   - 入力、シーン遷移、画面の寸法など、複数の画面が使う基盤の変更
   - `Packages/`、`ProjectSettings/`、ビルド対象シーンの変更
   - 関連するテストを判断できない場合

PlayModeテストはすべて名前空間 `Baryonyx.Tests.PlayMode` に属するため、クラス名で絞る。
接続中のEditorでは `run_tests --mode playmode --filter PlayMode.<テストクラス名>. --async_tests true` をクラスごとに実行する（例：`--filter PlayMode.HomeSceneTests.`）。
`filter_type` の既定値 `testName` は、テストの完全名（`Baryonyx.Tests.PlayMode.<クラス名>.<メソッド名>`）を大文字小文字を区別せず部分一致で絞り込む。
クラス名だけを渡すと、名前にそのクラス名を含む別のクラスも実行される（例：`HomeSceneTests` は `TopHomeSceneTests` にも一致する）。
そのため、前に `PlayMode.`、後ろに `.` を付けてクラスの境界を示す。
Unity CLIの別起動では、`unity test` の `--filter` にクラス名を `;` 区切りで渡す（[Unity Test Frameworkのコマンドライン引数](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/reference-command-line.html)）。
絞り込んだ実行で0件になった場合は、指定を誤っているため成功に含めない。

1件だけの実行でも、PlayModeテストはEditorを再生状態にして実行し、終了後に停止する。
再生が始まったことは、全件を実行した証拠にならない。
実行した件数は、`test_status` の件数、またはEditorログの `[TestResultCollector] Run finished: <件数> total` で確かめる。
同じログの `Run started: <件数> test(s)` は、絞り込む前のPlayModeテスト全体の件数を表示するため、実行件数の確認に使わない。

## 複数のチャットで1台のEditorを使うとき

複数のチャットで並行して作業すると、`client/` を開いた1台のEditorを全員で使うことになる。
Editorは、再コンパイルとテストを一度に1つずつしか処理できない。
あるチャットのテスト中に別のチャットが再コンパイルやテストを始めると、テストが途中で止まったり0件になったりして、やり直しが増える。
これを防ぐため、Editorの状態を変える操作は、[editor-lock.py](../../client/ci/editor-lock.py) で**札**を取ったチャットだけが行う。
札を待つ間も、ファイルの編集など札の要らない作業は進めてよい。

| 札 | 操作 |
|---|---|
| 必要 | `recompile`、`run_tests`、`editor_play`、シーンを開く・保存する、Prefabやアセットの生成し直し、Gameビューの撮影、`eval`・`run_script` での変更 |
| 不要 | `unity status`・`editor_status`・`console`・`get_*`・`find_*` などの読み取り、ファイルの編集、CSharpierでの整形と検査 |

札は次の順に使う。

1. `python3 client/ci/editor-lock.py acquire --owner "<作業の名前>"` で札を取り、表示された `token=` の値を控える。
2. 札を取れずに `busy` で終わったら（終了コード3）、同じコマンドをもう一度実行して待ち続ける。
3. 再コンパイルの完了とConsoleの確認、テストの実行と結果確認のように、ひと続きの操作を終えたら `python3 client/ci/editor-lock.py release <token>` で返す。操作が失敗したときも返す。

`acquire` は既定で90秒まで待つ。
コマンドの実行時間に上限があるツールでは、`--wait` にその上限より短い秒数を渡す。
札を持っているチャットは `status` で確かめる。

札には期限があり、既定では取ってから10分で切れる。
期限が切れた札は次に待っているチャットへ移るため、途中で止まったチャットが他のチャットを止め続けることはない。
commit前の全件実行のように10分を超えそうな操作では、EditModeとPlayModeの間などの区切りで `renew <token>` を実行して期限を延ばす。
`renew` や `release` が `not held`（終了コード4）で終わったら、札は期限切れで他のチャットへ移っている。
その間の結果は他のチャットの操作の影響を受けた可能性があるため、札を取り直して確かめ直す。

札で順番にできるのはEditorの操作だけで、作業フォルダーは全チャットで共有したままである。
再コンパイルは保存済みのC#をすべて対象にするため、他のチャットが書きかけのC#もコンパイルされる。
自分が変更していないファイルでコンパイルエラーが出たら、他のチャットの作業途中とみなし、直さずに札を返して少し待ってからやり直す。
エラーが続く場合はユーザーに報告する。

札は `client/Temp/editor-lock.json` に置く。
Unityは終了時に `Temp/` を消すため、Editorを開き直すと札も消える。
ユーザーが手でEditorを操作するときは札を使わない。

## 実行と結果確認

Unity Test RunnerまたはUnity CLIで、対象アセンブリを指定して実行する。
対象プロジェクトをEditorで開いている場合、CLIの別起動には検証コピーを使う。

接続中のEditorでは `run_tests --mode editor` または `run_tests --mode playmode` に `--filter <アセンブリ名> --filter_type assembly --async_tests true` を付け、`test_status` で完了と件数を確認する。
接続中のEditorでテストを実行するときは、毎回次の順に進める。

1. Editorの札を取る（[複数のチャットで1台のEditorを使うとき](#複数のチャットで1台のeditorを使うとき)）。
2. 開いているシーンに未保存の変更があれば保存する（[client/AGENTS.md](../../client/AGENTS.md#unityの操作と検証)）。
3. `run_tests` で実行を始める。
4. 開始から数秒のうちに、Editorログの今回の `[TestResultCollector] Run started: <件数> test(s)` を確認する。この件数は絞り込む前の全体の件数で、PlayModeでは0にならない。0なら、テストを見つけられていない。
5. 0のときは、完了を待たずにすぐ `cancel_tests` と `editor_stop` で止め、スクリプトの再読み込み（`EditorUtility.RequestScriptReload`。コンパイルは走らず2秒ほど）をしてから、1回だけやり直す。
6. やり直しても0なら、検証中だけEnter Play Mode Optionsの省略設定を無効にして再実行し、検証後に元へ戻す。
7. 完了を待つ時間には上限を設ける（EditModeは2分、PlayModeは5分を目安）。上限を超えたら待つのをやめ、Editorの状態（保存の確認ダイアログ、再生中か、コンパイル中か）を確かめてユーザーに報告する。
8. 完了は、`test_status` が completed になり、かつ今回の開始以降のログに `Run finished: <件数> total` が出たことで判断する。前回の実行の結果と取り違えない。実行件数が0の結果は成功に含めない。
9. 結果を確かめたら札を返す。続けて別のテストを実行する場合は、返さずに手順2へ戻ってよい。

Unity 6000.6.0f1でDomain ReloadとScene Reloadを両方省略した状態では、PlayModeテストを再読み込みなしで続けて実行すると、2回目は見つかるテストが0件になることが多い。
毎回あらかじめ再読み込みするより、手順4で0件を見つけたときだけ再読み込みするほうが、普段の実行は軽く済む。

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
Editorの札の取得・待機・期限切れの引き継ぎ・返却は、同じjobで `python -B -m unittest discover -s client/ci -p 'test_editor_lock.py'` が検査する。
実行補助の検査にはBashが必要で、WindowsではGit for WindowsのBashを使う。
ライセンス設定は [コード品質の手順](client-code-quality.md) を参照する。

必須チェックで旧 `Client tests (editmode)`、`Client tests (playmode)` を指定している場合は、統合後の `Client tests` へ切り替える。
実環境ではキャッシュ有無を分けて所要時間を比較し、2回目のイメージ取得と初期インポートが再利用されることを確認する。

## 関連手順

- [Androidビルドと実機確認](client-android-testing.md)：APKの取得、PRコメント、手動確認の範囲。
- [再構成計画](../plans/2026-09-10-client-ci-backlog.md)：受入条件と実環境での残件。

clientと無関係な変更ではworkflowのパスフィルターが働く。
必須チェック化する前に、対象外PRでも待機し続けない変更検知と集約チェックの運用を確定する。
