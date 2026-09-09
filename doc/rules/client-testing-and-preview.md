# UnityのテストとWebプレビュー

EditMode・PlayModeテストとWebビルドを [Client CI](../../.github/workflows/client-ci.yml) で実行する。
Cloudflare PagesへのPR Preview公開jobは、利用者の指定によりコメントアウトしている。
現在はWebビルドを `client-web` artifactとして保存するところまでが有効である。
VRT、Web E2E、Android向け検証の着手条件と完了条件は [クライアントCIの積み残し](../plans/2026-09-10-client-ci-backlog.md) で管理する。

## コードとテストの配置

| 配置 | 用途 |
|---|---|
| `client/Assets/Scripts/Runtime/` | ゲームの実行コード。アセンブリ名は `Baryonyx.Runtime` |
| `client/Assets/Editor/` | Editor専用コードとCI入口。アセンブリ名は `Baryonyx.Editor` |
| `client/Assets/Tests/EditMode/` | 画面を動かさず確認できるロジック。アセンブリ名は `Baryonyx.EditModeTests` |
| `client/Assets/Tests/PlayMode/` | シーン、コンポーネント、UI操作、フレームをまたぐ状態遷移。アセンブリ名は `Baryonyx.PlayModeTests` |

ダメージ計算やルーンの表示文字列への変換は、Runtime側にロジックを置き、EditMode側から呼び出す。
シーンの読み込みやUI操作はPlayMode側で確認し、生成したオブジェクトや読み込んだシーンを終了時に片付ける。
Runtimeの `internal` な実装も、指定した2つのテストアセンブリから参照できる。
テスト用asmdefを設定しているため、通常のWebプレイヤーにテストコードを含めない。

ゲーム固有のロジックはまだ実装していない。
初期状態では、次の実際の設定・動作を確認する。

| テスト | 内容 |
|---|---|
| [WebBuildSceneTests](../../client/Assets/Tests/EditMode/WebBuildSceneTests.cs) | 無効なシーンの除外、有効シーンなし・存在しないシーン・重複の拒否、現在のビルド対象シーンの確認 |
| [StartupSceneTests](../../client/Assets/Tests/PlayMode/StartupSceneTests.cs) | SampleSceneを読み込み、フレームが進んだ後に有効なカメラが存在することを確認 |

開始シーンを変更するときは、Build ProfilesのScene ListとPlayModeテストの対象も更新する。
ダメージ計算やUI操作の実装を追加した段階で、その機能のテストを同じ場所へ追加する。

## ローカル実行

Unityの `Window > General > Test Runner` から、EditMode・PlayModeをそれぞれ実行できる。
CLIで実行するときは、指定版Unityに次の引数を渡す。

```text
-batchmode -nographics -projectPath <clientの絶対パス>
-runTests -testPlatform EditMode -assemblyNames Baryonyx.EditModeTests
-testResults <結果ディレクトリ>/EditMode.xml -logFile <ログの絶対パス>
```

PlayModeは `-testPlatform PlayMode -assemblyNames Baryonyx.PlayModeTests` に変える。
引数は1回のUnity起動で渡し、`-quit` は付けない。
PowerShellでは `Start-Process -Wait -PassThru` で終了を待つ。
対象プロジェクトをEditorで開いている場合は、閉じてから実行するか、一時コピーを指定する。

CIでは [verify-test-results.py](../../client/ci/verify-test-results.py) でも結果XMLを検査する。
同じ確認をローカルで行う場合は、Python 3を使ってリポジトリ直下で実行する。

```text
python client/ci/verify-test-results.py <結果ディレクトリ> Baryonyx.EditModeTests
python client/ci/verify-test-results.py <結果ディレクトリ> Baryonyx.PlayModeTests
```

結果ファイルなし、対象テスト0件、すべてSkip、テストまたはsuiteの失敗を成功扱いにしない。
テストの一部を意図的にSkipする場合も、1件以上の成功が必要である。

## Webビルド

ローカルではUnity Hubから、使用しているEditor版の **Web Build Support** を追加する。
CIは同じEditor版のGameCI WebGLイメージを使用する。
ビルドの入口は [WebBuild.Build](../../client/Assets/Editor/CI/WebBuild.cs) である。

```text
<Unity実行ファイル> -batchmode -nographics -quit -buildTarget WebGL -projectPath <clientの絶対パス> -executeMethod Baryonyx.Editor.CI.WebBuild.Build -customBuildPath <出力先の絶対パス> -logFile <ログの絶対パス>
```

Build Profilesで有効なシーンを、設定された順序でビルドする。
出力先を省略した場合は `client/Builds/WebGL/` を使う。
GitHub CIでは `client/Builds/WebGL/Web/` に出力し、その内容を `client-web` artifactへ保存する。
ビルド失敗時はActionsのログを確認する。
ビルド処理が最後まで進んだ場合は `client-web-build-report` に結果とサイズも保存する。

Webビルド時だけGzip圧縮とJavaScriptの展開fallbackを有効にし、スレッドを無効にする。
HTTPの圧縮用ヘッダーやcross-origin isolationを追加しなくても、通常の静的サーバーで起動できる構成とする。
ビルド処理の終了時には元のPlayer Settingsへ戻す。
この設定を変更するときは [UnityのWeb配信ドキュメント](https://docs.unity3d.com/6000.6/Documentation/Manual/webgl-deploying.html) を確認する。

artifactを展開したディレクトリで `python -m http.server 8000` を実行し、ブラウザで `http://localhost:8000` を開くとローカル確認できる。
`index.html` の直接ダブルクリックでは起動しない。
ファイルの構成は次のコマンドで検査できる。

```text
python client/ci/verify-web-build.py <出力先>
```

## CIの実行順序

整形とAnalyzer検査を行い、Analyzer成功後にEditMode・PlayModeを実行する。
Unityの認証を伴うテストjobは1つずつ実行し、すべてのチェックが成功してからWebビルドへ進む。
テスト結果・Web成果物・診断レポートの保持期間は7日である。
Editorのテスト用LibraryとWebGL用Libraryは、別のキーでキャッシュする。

Unity PersonalのSecretsとfork PRの制約は [整形と静的解析の手順](client-code-quality.md#unity-personalの初回設定) を参照する。
Secretsが未登録の場合は、最初のUnity jobが不足している名前を表示して失敗する。
まだGitHubへ反映していない設定も、リポジトリに置いたworkflowでは有効なjobとして記述している。
GitHub上の実行確認とローカル検証の結果は [導入計画](../plans/2026-09-10-client-tests-web-preview.md) に分けて記録する。

テストの実行には [run-unity-tests.sh](../../client/ci/run-unity-tests.sh) から固定版のGameCI CLIを使う。
取得時とキャッシュ復元後に配布バイナリのSHA-256を検査し、`--coverageEnabled=false` を明示する。
Personalの認証はCLIの自動選択に従い、メールアドレスとパスワードで行う。
端末にひも付いたULFを別のrunnerで直接読み込む方式は指定しない。
Unity Test Runner Actionが生成する `--no-coverageEnabled` はこのCLI版では受理されないため、CLIを直接呼び出す。
テスト用イメージにはLinuxのプレイヤー用モジュールを含む `linux-il2cpp-3` を使う。

## キャッシュと所要時間

Unityの `client/Library` を、Analyzer・EditMode・PlayMode・Webビルドごとに分けて保存する。
Unity版と依存パッケージ・Analyzer設定が同じなら、前回のLibraryを復元し、変更に応じてUnityが必要な部分を更新する。
Unity版やこれらの依存設定を変えた場合は、別のキャッシュを作成する。
GameCI CLIのバイナリもキャッシュする。

Library復元後は、Unityを起動する前に `burst.pid` と `ilpp.pid` を削除する。
これらは前回のプロセス番号であり、別のrunnerやコンテナへ引き継ぐ情報ではない。
Unity 6000.6.0f1同梱のBurstには、保存されたPIDのプロセスを名前の確認なしに終了する処理がある。
プロセス番号が再利用されたときの誤終了を避け、import済みアセットやコンパイルのキャッシュは維持する。

初回とは、対象jobで復元できるキャッシュがない実行を指す。
同じPR内では前回分を再利用でき、新しいPRでは `main` など共有範囲内に一致するキャッシュがあれば利用できる。
別のPRだけに保存されたキャッシュは利用できない。
共有範囲は [GitHubのキャッシュ仕様](https://docs.github.com/en/actions/reference/workflows-and-actions/dependency-caching#restrictions-for-accessing-a-cache) に従う。

キャッシュはjob成功時に保存されるが、テストやビルドの実行を省略するものではない。
Analyzerは毎回C#を再コンパイルして解析する。
Unityコンテナの取得、Editorの起動、キャッシュの転送、変更部分の取り込み、テスト、Webビルドには毎回時間がかかる。
Dockerイメージ自体をjob間で永続化する設定は追加していない。

## Cloudflare Pagesの公開を有効にするとき

現時点では公開・Cloudflareリソースの作成を行わない。
workflow末尾の `preview` job全体をコメントアウトしてあり、Secretsの未登録だけで公開を止める構成にはしていない。

後日、次の設定を行ってから有効にする。

1. Cloudflare Pagesで、ビルド済みファイルをアップロードするDirect Uploadのプロジェクトを作成する。production branchは `main` とし、`pr-数字` を指定しない。
2. GitHub Repository secretsへ `CLOUDFLARE_API_TOKEN` と `CLOUDFLARE_ACCOUNT_ID` を登録する。API Tokenの権限は対象アカウントのCloudflare Pages編集に限定する。
3. Repository variablesへ `CLOUDFLARE_PAGES_PROJECT_NAME` を登録する。
4. この設定と検証スクリプトがPRのbaseブランチに存在する状態にしてから、`preview` jobのコメントを外す。

有効化後は、同一リポジトリからのPRで成功した `client-web` artifactをダウンロードし、`pr-<PR番号>` ブランチ名で配信する。
デプロイはWebビルドと別jobで行い、CloudflareのTokenをUnityの実行へ渡さない。
公開先はGitHubの `client-preview` environmentに表示する。
参考：[CloudflareのCIからのDirect Upload手順](https://developers.cloudflare.com/pages/how-to/use-direct-upload-with-continuous-integration/)。

Cloudflare Pagesには1ファイル25 MiBの上限があるため、有効化したjobではアップロード前にサイズも検査する。
上限を超えた場合はアセットの削減・分割や配信先の構成を検討し、検査だけを外して成功扱いにしない。
参考：[Cloudflare Pagesの制限](https://developers.cloudflare.com/pages/platform/limits/#file-size)。
