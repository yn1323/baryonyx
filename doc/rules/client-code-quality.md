# Unityクライアントの整形と静的解析

`client/` のC#はCSharpierで整形し、Unityのコンパイル時にMicrosoft.Unity.Analyzersで検査する。
GitHub Actionsの設定は [.github/workflows/client-ci.yml](../../.github/workflows/client-ci.yml) に置く。
GitHub上の初回実行状況は [導入計画](../plans/2026-09-10-client-code-quality.md) に記録する。

## 必要な環境

| ツール | バージョンの管理場所 |
|---|---|
| Unity Editor | [ProjectVersion.txt](../../client/ProjectSettings/ProjectVersion.txt) |
| .NET SDK | [global.json](../../client/global.json) |
| CSharpier | [.config/dotnet-tools.json](../../client/.config/dotnet-tools.json) |
| Microsoft.Unity.Analyzers | [provenance.json](../../client/Assets/Analyzers/Microsoft.Unity.Analyzers/provenance.json) |

UnityはHubから、.NET SDKは [Microsoftの配布ページ](https://dotnet.microsoft.com/download/dotnet/10.0) から指定版を導入する。
Unity同梱のSDKと、整形に使うSDKは別に管理する。
CSharpierのmanifestではランタイムのroll forwardを有効にしており、指定した.NET SDKのランタイムで実行できる。

## 日常の整形

Windows・macOSとも、リポジトリ直下から次の順に実行する。

```text
cd client
dotnet tool restore
dotnet csharpier format Assets
dotnet csharpier check Assets
```

`format` はファイルを修正し、`check` は未整形のファイルがあると失敗する。
整形ルールは [.editorconfig](../../client/.editorconfig) のUTF-8、LF、スペース4、行幅100に揃える。
[.gitattributes](../../client/.gitattributes) でC#と関連設定のLFを維持する。

対象は `Assets/` のC#である。
[.csharpierignore](../../client/.csharpierignore) は、C#以外、テンプレートの `TutorialInfo/`、第三者コード用の `Plugins/`・`ThirdParty/`、生成コードを除外する。
自作のC#は除外されたフォルダへ置かない。
新しい第三者コードを導入するときは、配置先に応じて除外を追加する。

VS Codeで `client/` を開くと、推奨拡張のCSharpierで保存時に整形できる。
他のエディタでも上記コマンドが共通の確認手順になる。

## Unityの静的解析

Analyzer DLLは `Assets/Analyzers/Microsoft.Unity.Analyzers/` に固定版を同梱する。
DLLの `.meta` に `RoslynAnalyzer` ラベルを設定し、通常のEditor・プレイヤー用プラグインとしての読み込みを無効にする。
DLLと `.meta` は両方Gitで管理する。

診断の重大度は [Assets/Default.ruleset](../../client/Assets/Default.ruleset) に集約する。
次の誤用をコンパイルエラーにする。

| 診断ID | 検出する誤用 |
|---|---|
| UNT0006 | Unityメッセージのシグネチャの誤り |
| UNT0007 | Unity Objectへのnull合体演算子 |
| UNT0008 | Unity Objectへのnull条件演算子 |
| UNT0010 | Componentを `new` するコード |
| UNT0011 | ScriptableObjectを `new` するコード |

Analyzer・Source Generatorの読み込みや実行失敗を表す診断もErrorにする。
それ以外の診断は配布元の既定値を使う。
エラーを修正するときは [Microsoftの診断一覧](https://github.com/microsoft/Microsoft.Unity.Analyzers/blob/main/doc/index.md) を参照する。

`Assets/` にアセンブリ別のrulesetを追加するときは、共通の診断を継承する構成とCIの検証を一緒に更新する。
現時点のCIは、プロジェクトの各C#アセンブリが `Default.ruleset` を使うことを確認する。
Unityが生成する `.csproj` は手書きで変更せず、External Toolsから再生成する。
[生成フック](../../client/Assets/Editor/AnalyzerProjectSettings.cs) が、Zedなどで不足する固定版Analyzerとrulesetの参照を補い、IDE側の同名Analyzerとの重複を除く。
他のAnalyzerやSource Generatorの既存参照は保持する。

## GitHub Actions

| チェック | 実行内容 |
|---|---|
| Client format (windows-2025) | WindowsでCSharpierを復元し、整形を検査 |
| Client format (macos-15) | macOSで同じ整形を検査 |
| Client analyzers | UbuntuでUnityを起動し、コンパイルとAnalyzerを実行 |

`client/**` またはworkflowの変更を含むpush・PRで起動し、手動実行にも対応する。
Unity版は `ProjectVersion.txt` から読み、対応するGameCIのbaseイメージを使う。
Editor版を更新するときは、対応するイメージの公開状況も確認する。

Unityの入口は [CompileCheck.Run](../../client/Assets/Editor/CI/CompileCheck.cs) である。
解析jobは `-nographics` を渡し、描画機能を初期化せずに起動する。
失敗時はrunnerのメモリ・ディスク状況とカーネル警告もログに残す。
DLLのハッシュ、Plugin設定、ラベル、各アセンブリのAnalyzer・ruleset適用を確認してから、スクリプトを再コンパイルする。
コンパイル完了を確認したメソッドがUnityを終了するため、GameCIでは `manualExit: true` を指定する。
Libraryを復元した場合もコンパイラのキャッシュをクリアして検査する。

コンパイルエラーやAnalyzerの実行失敗はjobの失敗になる。
成功にはUnityの正常終了と `Logs/client-compile.txt` の完了記録の両方を必要とする。
診断レポートは7日間artifactとして保存する。
初回importなど、この入口へ到達する前のエラーはActionsのUnity実行ログを確認する。

このjobはLinux Editorターゲットで有効なC#を検査する。
Android専用の条件付きコードは、このAnalyzer jobの対象に含まない。
追加したEditMode・PlayModeテスト、Webビルド、コメントアウトしたCloudflare Pages公開設定は [UnityのテストとWebプレビュー](client-testing-and-preview.md) を参照する。

パス指定で起動を絞っているため、これらのjobをそのままブランチ保護の必須チェックに指定しない。
必須化するときは、clientに変更のないPRでも結果を返すjobを設ける。

## Unity Personalの初回設定

[GitHubのActions secrets設定](https://github.com/yn1323/baryonyx/settings/secrets/actions) に、次のRepository secretsを登録する。

| 名前 | 値 |
|---|---|
| `UNITY_LICENSE` | GameCIのPersonal手順で用意した `.ulf` ファイルの内容 |
| `UNITY_EMAIL` | Unityアカウントのメールアドレス |
| `UNITY_PASSWORD` | Unityアカウントのパスワード |

ライセンスの準備は [GameCIのPersonal向け認証手順](https://game.ci/docs/github/activation/#personal-license) に従う。
Hubで「Unity CLIを使用」をONにしていても、このCIの認証設定は別途必要である。
値はGitHubの設定画面で直接入力し、チャット・リポジトリ・実行ログへ貼り付けない。
Windowsのローカルライセンスファイルの存在だけでは、LinuxのCIで認証できることまでは確認できない。

Secrets未登録時は、Analyzer jobが不足している名前を表示して失敗する。
整形jobはUnityの認証なしで実行できる。
fork PRにはSecretsが渡らないため、Analyzer jobは同様に失敗する。
外部PRのコードへSecretsを渡す `pull_request_target` は使用しない。

## ローカルでCIのコンパイルを確認する

Unityで対象プロジェクトを閉じてから、次の引数で指定版Editorを起動する。
開いたまま確認するときは `Assets/`・`Packages/`・`ProjectSettings/` を一時ディレクトリへコピーし、そのコピーを指定する。

```text
<Unity実行ファイル> -batchmode -nographics -projectPath <clientの絶対パス> -executeMethod Baryonyx.Editor.CI.CompileCheck.Run -logFile <ログの絶対パス>
```

このコマンドには `-quit` を付けない。
PowerShellから起動するときは `Start-Process -Wait -PassThru` で終了を待ち、`ExitCode` と `Logs/client-compile.txt` の結果を確認する。
ローカルで有効なUnityライセンスが必要になる。

## バージョン更新

CSharpierは `client/` で `dotnet tool update csharpier --version <採用版> --allow-roll-forward` を実行してmanifestを更新する。
SDK変更時は `global.json` も更新し、両OSのCIで確認する。

Analyzerを更新するときは、[provenance.json](../../client/Assets/Analyzers/Microsoft.Unity.Analyzers/provenance.json) に記載したNuGetのURLを採用版に置き換え、`.nupkg` をZIPとして展開する。
`analyzers/dotnet/cs/Microsoft.Unity.Analyzers.dll` だけを同じ配置先へコピーし、既存の `.meta` とGUIDを保持する。
配布元のライセンスを確認して `LICENSE.md` を更新する。
パッケージとDLLのSHA-256、NuGetのnuspecに記録されたソースコミットをprovenanceへ反映する。
SHA-256はWindowsの `Get-FileHash -Algorithm SHA256`、macOSの `shasum -a 256` で取得できる。

更新後は、一時コピーへ未整形のC#とUNT0008違反を入れ、それぞれ失敗することを確認する。
Analyzerのラベル欠落やDLL破損でも失敗することを確認し、違反を取り除いて再実行する。
導入計画に、実行したOS・Editor版と確認結果を記録する。
