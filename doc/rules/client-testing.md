# UnityのテストとCI

[設計・開発ルールの索引](README.md)

[Client CI](../../.github/workflows/client-ci.yml) は整形、Analyzer、EditMode、PlayMode、CI補助スクリプトのテストを実行する。
Android APK生成とDrive配布は一時停止中である（[停止範囲と再開方法](client-android-testing.md)）。

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
ホーム画面の [シーンテスト](../../client/Assets/Baryonyx/Features/Home/Tests/PlayMode/HomeSceneTests.cs) は実シーンを使い、仮データと、保存済みの歩数を換算したACTの表示、タップ領域、ボタンの反応、歩数の同期を検査する。
ルーンの獲得は、[演出のテスト](../../client/Assets/Baryonyx/Features/Home/Tests/PlayMode/HomeRuneTapTests.cs) が仮想入力で押し、同期したACTと同じ量が付与されて代役のサーバーに残ることを確かめる。
Top・Home・案内人の画面のシーンテストは [TestGameServices](../../client/Assets/Baryonyx/Tests/PlayMode/Support/TestGameServices.cs) でHealth Connectとゲームサーバーを端末内の代役へ差し替え、設定アセットのサーバーURLへ接続しない。
`TestGameServices` は、Unityの乱数と冒険の代役の道を決まった種から始め、終わるとアプリ全体の状態（staticな `〜Session`・`RuneBalance`・`SaveQueue` など）を初めに戻し、乱数を元の続きへ戻す。
`TestGameServices` を使わない戦闘・デバッグルームのテストは、[FixedRandom](../../client/Assets/Baryonyx/Tests/PlayMode/Support/FixedRandom.cs) を準備で使い、片付けで乱数と `Time.timeScale` を戻す。
戦闘の規則（山札・相手をランダムに選ぶ・確率）は `BattleInspectView.Dice` の乱数で決まり、テストは種を決めた `BattleDice` を渡せる。
入力基盤の成功を、ゲームの主要操作の検証済みとは扱わない。

### シーンテストを軽く保つ

PlayModeの時間の大半は、シーンの読み込みと遷移演出（TopからHomeは閉じる・開くとも0.75秒、ほかは0.15秒）の待ち時間である。
全体の流れを1回は通しつつ、同じ待ち時間を繰り返さないよう、次のように書く。

- `TestGameServices` は遷移演出の時間を0.05倍にする。演出の途中の入力を確かめるテストだけ、`SceneTransitionController.DurationScale` を1に戻す。
- シーンの読み込み・条件の待機・後片付け・タップ領域の検査は [SceneTests](../../client/Assets/Baryonyx/Tests/PlayMode/Support/SceneTests.cs) を使う。固定秒数の `WaitForSeconds` で待たず、条件がそろうまでフレームを進める。
- 同じ画面を往復する流れは、ボタンごとにテストを分けず、1件で順に通す。
- ロジックの組み合わせ（計算・状態遷移）はEditModeで検査し、PlayModeでは画面とのつなぎ込みを1回確かめる。
- 1画面の静的な確認（表示・タップ領域）で、演出の完了を待たない。演出を確かめるテストは、演出を待つ1件にまとめる。
- デバッグルーム（ラボ）のテストは、読み込み、操作の押しやすさ、1つの操作のつなぎ込みだけを確かめる。そこで動かす規則は、EditModeと本番の画面のテストで確かめる。

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
2026-10-25にmacOSのEditor（背面で表示）で [run-editor-tests.py](../../client/ci/run-editor-tests.py) から全件を実行したときの所要時間（スクリプトの開始から終了まで）は、EditMode 661件で約45秒、PlayMode 170件で約7分（0件によるやり直し1回を含む）だった。テストの整理の前は、EditMode 702件で約55秒、PlayMode 190件で約9分40秒だった。PlayModeの2クラス8件は約15秒だった。
作業中は関連するPlayModeテストだけを実行し、全件はcommit前に実行する（[commitの手順](../../AGENTS.md#commitの手順)）。影響が広い変更でも、作業中にPlayModeの全件は実行しない。
EditModeは全件でも1分ほどで終わるため、作業中も `Baryonyx.EditModeTests` を全件実行する（`--mode editmode`）。

関連するPlayModeテストは、次の順に選ぶ。

1. 変更したファイルと同じ機能の `Tests/PlayMode/` にあるテスト。
2. 変更した型・シーン・Prefabを参照する、他の場所のPlayModeテスト。テストコードを型名・シーン名・Prefab名で検索して探す。シーンの中身を変えたときは、ビルド対象のシーンでも、そのシーンを読むテストだけを実行する。テストの実行や `eval` でついでに保存されただけのシーンの差分は、変更に含めない。
3. `Tests/PlayMode/Support/` や、入力・シーン遷移・画面の寸法など複数の画面が使う基盤を変えたときも、変えた型名・メソッド名でテストコードを検索し、それを使うクラスだけを実行する。使うクラスが多いときは、機能ごとに1クラスを選ぶ。
4. `Packages/`・`ProjectSettings/` の変更のように検索で絞れないときや、関連するテストを判断できないときは、起動からホームまでの遷移を通す `TopHomeSceneTests` を実行する。

接続中のEditorでは、[run-editor-tests.py](../../client/ci/run-editor-tests.py) にクラス名を並べて実行する（例：`python3 client/ci/run-editor-tests.py HomeSceneTests TavernTrainingTests --owner "<作業の名前>"`）。
並べたクラスは1回の実行にまとめ、再生の開始と停止も1回で済ませる。
EditModeは `--mode editmode` を付ける。クラス名を省くと、そのモードのアセンブリ（`Baryonyx.PlayModeTests`・`Baryonyx.EditModeTests`）を全件実行する（例：`python3 client/ci/run-editor-tests.py --mode editmode --owner "<作業の名前>"`）。
クラス名は名前空間を付けずに書く。スクリプトはテストの完全名のうちクラスの部分を前後の `.` で区切って照らすため、`HomeSceneTests` に `TopHomeSceneTests` は含まれない。
直したテストが1件や数件なら、クラス全体ではなく `クラス名.テスト名` で書く（例：`python3 client/ci/run-editor-tests.py AdventureLoopTests.TheCampfireTakesTapsOnItsThingsAndTags --owner "<作業の名前>"`）。テスト名は引数の前か名前の終わりで区切って照らすため、`Opens` に `OpensTwice` は含まれない。2026-10-25の計測では、24件のクラスを流すと約113秒、その中の1件だけなら約9秒だった。クラス名とテスト名は混ぜて並べてよい。
C#を変えたあとは `--recompile` を付ける。札を取ってから再コンパイルし、完了を待ってコンパイルエラーを確かめ、そのままテストへ進む。エラーがあればテストを始めず、エラーの行だけを出して止める。コンパイルだけを確かめるときは `--compile-only` を付ける（テストは実行しない）。
スクリプトは[実行と結果確認](#実行と結果確認)の手順を順に行い、クラスごとの成否と件数を1行ずつ出す。
札を自分で取らずにスクリプトに取らせる。すでに札を持っているときは `--token <token>` を渡すと、札を延長し、返さずに終わる。
クラス名・テスト名の誤りは、札を取る前に `list_tests` の一覧と照らして止める。`--recompile` のときは、新しいテストを読み込んでから照らすため、再コンパイルのあとに照らす。
出力は要約だけにする。成功なら `compiled (12s)` とクラスまたはテストごとの1行と合計の1行だけを出し、失敗したテストとコンパイルエラーの行だけを足す。Editorのログは出さない。
数分かかることがあるため、バックグラウンドで1回だけ実行し、終了の通知を待つ。終了コードは、すべて成功で0、失敗ありで1、クラス名・テスト名の誤りで2、札が取れないと3、Editorの異常や時間切れで5、コンパイルエラーで6である。
札の待ち（既定は10分。`--lock-wait` で変える）、コンパイルの完了待ち、テストの完了待ちは、すべてスクリプトの中で行う。待っている間に `editor_status` や `echo` を別に呼ばない。

接続中のEditorで `run_tests` を直接使うと、クラスを1つずつしか絞り込めない（`--filter` に `;` 区切りや「A|B」で複数のクラスを渡すと0件になる）。
保存の確認ダイアログも防げないため、スクリプトを使う。
Unity CLIの別起動では、`unity test` の `--filter` にクラス名を `;` 区切りで渡す（[Unity Test Frameworkのコマンドライン引数](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/reference-command-line.html)）。
絞り込んだ実行で0件になった場合は、指定を誤っているため成功に含めない。

1件だけの実行でも、PlayModeテストはEditorを再生状態にして実行し、終了後に停止する。
再生が始まったことは、全件を実行した証拠にならない。
実行した件数は、スクリプトが出す件数、またはEditorログの `[EditorTestRun] Run finished: <件数> total` で確かめる。

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
2. 札を取れずに `busy` で終わったら（終了コード3）、30秒以内に同じコマンドをもう一度実行して待ち続ける。
3. 再コンパイルの完了とConsoleの確認、テストの実行と結果確認のように、ひと続きの操作を終えたら `python3 client/ci/editor-lock.py release <token>` で返す。操作が失敗したときも返す。

`acquire` は既定で90秒まで待つ。
コマンドの実行時間に上限があるツールでは、`--wait` にその上限より短い秒数を渡す。
札は待ち始めた順に渡る。`busy` で終わっても30秒以内に `acquire` をやり直せば順番は保たれ、それより遅れると列の最後に並び直す。
札を持っているチャットと待っているチャットは `status` で確かめる。

札には期限があり、既定では取ってから5分で切れる。
期限が切れた札は次に待っているチャットへ移るため、途中で止まったチャットが他のチャットを止め続けることはない。
5分を超えそうな操作では、区切りごとに `renew <token>` を実行して期限を延ばす。
ただし、1つの札を持てるのは取ってから15分までで、`renew` してもそれより先へは延びない。
`renew` が `capped` と表示したら、区切りで札を返して取り直し、待っているチャットに順番を回す。
`renew` や `release` が `not held`（終了コード4）で終わったら、札は期限切れで他のチャットへ移っている。
その間の結果は他のチャットの操作の影響を受けた可能性があるため、札を取り直して確かめ直す。

札は1回のターン（ユーザーの依頼から応答を終えるまで）の中で使い終える。
Claude Codeのチャットは、応答を終えると [.claude/settings.json](../../.claude/settings.json) のhookが `release-session` を実行し、そのチャットの札と待ちの順番を返す。
そのため、ユーザーの返事を待つ間も札を持ち続けることはない。次のターンでEditorを使うときは札を取り直す。
`run-editor-tests.py` が動いている間は、そのチャットが応答を終えても札を返さない。
Claude Code以外のチャットにはこのhookがないため、応答を終える前に自分で札を返す。

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

接続中のEditorでは [run-editor-tests.py](../../client/ci/run-editor-tests.py) で実行する（[使い方](#作業中に実行するplaymodeテスト)）。
スクリプトは次の順に進める。

1. Editorの札を取る（[複数のチャットで1台のEditorを使うとき](#複数のチャットで1台のeditorを使うとき)）。
2. 再生中なら止め、コンパイルや再読み込みが終わるのを待つ。`--recompile`・`--compile-only` のときは、ここで再コンパイルを頼み、`recompile_status` が終わりを返し、Editorが空くまで待つ（上限5分）。そのあと、Editorのログの今回の分と `recompile_status` から `error CS` の行を集め、重複を除いて最大20行だけを出す。エラーがあれば終了コード6で止める。`--compile-only` はここで終わる。
3. `eval` の1回の呼び出しで [EditorTestRun](../../client/Assets/Baryonyx/Editor/CI/EditorTestRun.cs) の `Start` を呼ぶ。`Start` は開いているシーンを保存し（`EditorSceneManager.SaveOpenScenes`）、同じ呼び出しの中で `TestRunnerApi.Execute` を呼んで実行を始める。複数のクラスやテストは `Filter.groupNames` の正規表現で1回の実行にまとめる。
4. `EditorTestRun` が `client/Temp/editor-test-run.json` に書く今回の結果を待つ。開始の件数が0なら、テストを見つけられていない。
5. 0のときは、完了を待たずにすぐ止め、スクリプトの再読み込み（`EditorUtility.RequestScriptReload`。コンパイルは走らず2秒ほど）をしてから、1回だけやり直す。
6. 完了を待つ時間には上限を設ける（既定は10分。`--timeout` で変える）。上限を超えたら止めて、終了コード5で終わる。
7. 完了したら、クラスごとの件数を `list_tests` の件数と照らす。実行件数が0の結果は成功に含めない。
8. 札を返す。

Unity Test Frameworkは、テストを始めるたびに開いているシーンに未保存の変更がないかを確かめ、あれば保存の確認ダイアログを出す（`SaveModifiedSceneTask`）。
ダイアログは人が押すまでEditorを止め、Unity CLIからは押せない。
保存と実行の開始を別々の命令にすると、その間に他のチャットの操作やPrefabの生成し直しでシーンが未保存に戻ることがあるため、`Start` で1回にまとめている。
名前のない（Untitled）シーンに未保存の変更があると、保存先を選ぶダイアログになるため、`Start` は実行を始めずにエラーを返す。スクリプトは終了コード5で終わるので、ユーザーに保存か破棄を頼む。
スクリプトが終了コード5で止まったら、Editorの状態（確認ダイアログ、再生中か、コンパイル中か）を確かめてユーザーに報告する。
やり直しても0件なら、検証中だけEnter Play Mode Optionsの省略設定を無効にして再実行し、検証後に元へ戻す。

1回の実行の完了は、1つの待ち方で待つ。同じ完了を複数の監視やコマンドで待っても早くは終わらず、同じ通知が重なって確認の手間が増える。

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
接続中のEditorでテストを実行するスクリプトは、偽のEditorを相手に `python -B -m unittest discover -s client/ci -p 'test_run_editor_tests.py'` が検査する。
実行補助の検査にはBashが必要で、WindowsではGit for WindowsのBashを使う。
ライセンス設定は [コード品質の手順](client-code-quality.md) を参照する。

実環境ではキャッシュ有無を分けて所要時間を比較し、2回目のイメージ取得と初期インポートが再利用されることを確認する。

## 戦闘の自動対戦

戦闘の強さは、カードを自動で使わせて同じ戦闘を何回も戦わせ、勝率とターン数で測る。
自動対戦は本物の戦闘画面（`BattleInspectView`）をそのまま動かすため、カード・装備・ステータス・戦闘の計算の変更は、自動対戦にも同じように効く。
調べ方の進め方は手動のスキル [battle-balance](../../.agents/skills/battle-balance/SKILL.md) にあり、測った結果は[戦闘の試し](../features/stage-progression.md#戦闘の試し)に記録する。

### 回し方

`client/Temp/battle-balance/request.json` に依頼を書き、PlayModeの `BattleBalanceTests` を実行する。

```bash
python3 client/ci/run-editor-tests.py BattleBalanceTests --timeout 900
```

結果は同じフォルダーの `result.md`（組ごとの表）と `result.json`（依頼、組ごとの集計、1戦ごとの記録）に書かれる。
`RunsTheRequestedBattles` は、始めに依頼を `request.taken.json` へ移してから戦う。
依頼がなければ何もせずに合格するため、全件実行に含まれていても時間はかからない。
置いたままの依頼が、ほかの作業の全件実行で回り直すこともない。

1戦はLv10の森の強い魔物で約6秒、最奥の間で約9秒かかる（2026-10-25）。
Editorの札は1回15分までのため（[複数のチャットで1台のEditorを使うとき](#複数のチャットで1台のeditorを使うとき)）、1回の依頼はその中に収まる量にし、多いときは分けて回す。
強い魔物の20組を3回ずつ戦わせると約6分である。
同じフォルダーの `BattleBalanceTests` には、森の魔物の1組を1回戦って終わりまで数えられることを確かめるテストもあり、こちらは全件実行でも毎回動く。

### 依頼の項目

依頼はJSONで書き、書かなかった項目は既定の値になる。

| 項目 | 既定 | 内容 |
|---|---|---|
| `mode` | `single` | `single` は1つの部屋の戦闘を繰り返す。`through` は冒険の道を入口から最奥まで歩き、戦闘のマスで戦ってHPを持ち越す |
| `destination` | `forest-ruins` | 行き先のID（[行き先と敵](../features/stage-progression.md#行き先と敵)） |
| `room` | `elite` | `single` で戦う部屋。`pack`（魔物の気配）・`elite`（強い魔物の気配）・`boss`（最奥の間）・`mimic`（怪しい宝箱のミミック） |
| `depthPercent` | `100` | 道中の深さの倍率（%）。最奥の間には掛けない |
| `variants` | 空 | 敵の組み合わせの番号。空なら全組み合わせ。番号 v の小さな敵は v を小さな敵の数で割った余り、中くらいの敵はその商を中くらいの敵の数で割った余り、最奥の主はさらにその商で選ぶ。番号と敵は `result.md` の組の欄に出る |
| `repeats` | `3` | `single` で組み合わせごとに戦う回数 |
| `runs` | `5` | `through` で歩く冒険の数 |
| `seed` | `1` | 乱数の種。1戦（`through` では1回の冒険）ごとに1ずつ進める。同じ依頼と同じ種なら、同じ戦闘になる |
| `levels` | 空 | 編成の枠の順のLv。0や書かない枠は仮データのLv |
| `cardMultiplier` | `1` | カスタムスキルのカードすべての倍率（[品質](../features/inventory.md#品質)） |
| `awakenedBranch` | `0` | カスタムスキルのカードすべての覚醒（1か2）。その覚醒がないカードは覚醒しない |
| `equipment` | 空 | 付ける装備の一覧。1つごとに `character`（キャラのID。空なら `slot` の編成の枠）、`equipment`（[装備](../features/equipment.md)の種類のID）、`multiplier`（倍率）、アクセサリーなら `accessory`（枠の番号0〜2）を書く。空なら装備なし |
| `startHpShare` | `1` | 始めのHP（最大HPに対する割合）。`through` では最初の戦闘だけ |
| `knowWeaknesses` | `false` | 敵の弱点を最初から開示しておくか |
| `odds` | `random` | `random` は会心と状態異常を確率どおりに振る。`fixed` はテストと同じく、会心なし・状態異常は必ず付く |
| `policy` | `default` | カードの選び方。今は1つだけ |
| `openShadyChests` | `false` | `through` で怪しい宝箱を開けてミミックと戦うか |
| `maxTurns` | `40` | このターンを超えた戦闘は打ち切り（`Timeout`）にする |
| `verbose` | `false` | 1手ごとの記録（ターン・エネルギー・カード・対象・両方のHP）を `[balance] step` としてEditorのログに出す |
| `timeScale` | `20` | 戦闘の時間を進める速さ |

### 本物の戦闘との違い

- 戦闘ごとに戦闘のデバッグルーム（`BattleLab.unity`）を開き、冒険の戦闘と同じく、戦闘が始まる前に部屋の敵と編成を入れる。このシーンの戦闘画面は、冒険の戦闘（`Battle.unity`）と同じPrefabである。
- 演出を外し、時間を速く進める。演出がないときにカードの効果をすぐ当てる処理は、戦闘画面がもともと持っている。演出が要るため、戦闘中の固有スキルの覚醒は起きない。
- カードは、戦闘画面の操作（カードを押して対象を押す、全体のカードは2回押す）で使う。選び方は、倒れた味方がいれば蘇生、HPが半分を切った味方がいれば回復・防御、ほかはコストの高い攻撃から使い、開示した弱点を突ける敵、なければHPの最も少ない敵を狙う。
- 編成は仮データの編成で、サーバーに保存した編成ではない。Lv・カードの倍率・覚醒・装備は依頼で変える。
- 装備は、冒険の戦闘がプレイヤーの装備を渡すのと同じ所（`AdventureParty.Of`）へ渡すため、ステータスの上昇と攻撃スキルの2回発動が本物と同じに効く。`result.json` の `partyStats` に、戦闘が始まったときの味方のステータスと2回発動の確率が残る。
- 本物の初期のパーティは、サーバーの定義（[STARTER_EQUIPPED](../../server/src/features/equipment/catalog.ts)）の武器と防具を×1.00で付けている。初期のパーティを測るときは、この装備を `equipment` に書く。
- `through` では、休憩のマスで休む（[HPの持ち越し](../features/stage-progression.md#hpの持ち越し)と同じ回復）だけを入れ、戦闘後の強化、休憩の鍛える・研鑽、「？」の中身、宝箱と行商人は入れていない。道の分かれ道は種で決めた乱数で選ぶ。全滅したらその冒険を終える。
- 1戦ごとの種で、Unityの乱数（敵の行動・山札・狙い・2回発動）と、会心と状態異常の確率（`BattleOdds.Seed`）の両方を決める。テスト全体は確率を固定しているが（会心なし・状態異常は必ず付く）、自動対戦は `odds` が `random` なら種で振り、終わったら本物の戦闘と同じ共有の乱数へ戻す。テストの後片付けで固定へ戻る。
- 手札は隠しておく。味方が倒れたときに手札が崩れる演出は、実時間で進みながらUnityの乱数を使うため、隠さないと種でそろわない。手札を隠すと、使えるカードがなくなったときの自動のターン終了も止まるため、自動対戦が、使えるカードがないときと手札を使い切ったときにターンを終える。

### 実装の場所

| 対象 | 入口 |
|---|---|
| 依頼の項目 | [BattleBalanceRequest](../../client/Assets/Baryonyx/Features/Adventure/Runtime/Balance/BattleBalanceRequest.cs) |
| 戦闘を開いて戦わせ、数える | [BattleBalanceSession](../../client/Assets/Baryonyx/Features/Adventure/Runtime/Balance/BattleBalanceSession.cs) |
| 敵の組み合わせ | [BattleBalanceEncounters](../../client/Assets/Baryonyx/Features/Adventure/Runtime/Balance/BattleBalanceEncounters.cs) |
| パーティ（`AdventureParty.Of` を通す） | [BattleBalanceParty](../../client/Assets/Baryonyx/Features/Adventure/Runtime/Balance/BattleBalanceParty.cs) |
| カードの選び方（差し替えられる） | [BattleAutoPolicy](../../client/Assets/Baryonyx/Features/Adventure/Runtime/Balance/BattleAutoPolicy.cs) |
| 集計と表 | [BattleBalanceResult](../../client/Assets/Baryonyx/Features/Adventure/Runtime/Balance/BattleBalanceResult.cs) |
| 実行の入口と確認のテスト | [BattleBalanceTests](../../client/Assets/Baryonyx/Features/Adventure/Tests/PlayMode/BattleBalanceTests.cs)・[BattleBalanceResultTests](../../client/Assets/Baryonyx/Features/Adventure/Tests/EditMode/BattleBalanceResultTests.cs) |

## 関連手順

- [Androidビルドと実機確認](client-android-testing.md)：APKの取得、PRコメント、手動確認の範囲。
- [再構成計画](../plans/2026-09-10-client-ci-backlog.md)：受入条件と実環境での残件。

clientと無関係な変更ではworkflowのパスフィルターが働く。
必須チェック化する前に、対象外PRでも待機し続けない変更検知と集約チェックの運用を確定する。
