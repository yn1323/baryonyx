# Unityクライアントの整形と静的解析の導入計画

状態：進行中
作成日：2026-09-10
更新日：2026-09-10

`client/` にCSharpierとMicrosoft.Unity.Analyzersを導入し、ローカルとGitHub Actionsで同じバージョンとルールを使う。
未整形のC#と、Errorに指定したUnity Analyzerの違反をCIで検出できる状態を目指す。
利用するUnityライセンスはPersonal（無料）である。
以下の計画に沿って実装・検証を進めている。GitHub上の初回実行結果は、ローカル検証と区別して記録する。

## 対象範囲と導入前の状態

対象はC#の整形、Unity固有の静的解析、その実行に必要なUnityコンパイル、開発手順の文書化とする。
EditMode・PlayModeの機能テスト、VRT、Web Preview、E2E、Androidアプリのビルドは今回に含めない。

計画作成時に確認した導入前の状態は次のとおり。

- Unityは `client/ProjectSettings/ProjectVersion.txt` に記録された `6000.6.0f1`。
- `Assets/` にあるC#は、テンプレート由来の `TutorialInfo/Readme.cs` と `TutorialInfo/Editor/ReadmeEditor.cs` の2ファイル。
- プロジェクトで固定したCSharpier、Analyzer DLL、`.editorconfig`、`.ruleset` はまだない。
- 生成された `Assembly-CSharp.csproj` はVS Code拡張内のMicrosoft.Unity.Analyzersを参照している。一方、Unity本体の現在のコンパイル引数には、そのAnalyzerの指定がない。
- `.github/workflows/server-ci.yml` があり、クライアント用workflowはまだない。
- 作業環境の.NET SDKは `9.0.303`。下記のSDKは追加導入が必要。

## 採用する構成

| 項目 | 提案 | 管理方法 |
|---|---|---|
| Unity Editor | `6000.6.0f1` を継続 | `ProjectVersion.txt` をCIでも参照 |
| .NET SDK | `10.0.401` | `client/global.json` に固定 |
| Formatter | CSharpier `1.3.0` | `client/.config/dotnet-tools.json` のローカルツール |
| Analyzer | Microsoft.Unity.Analyzers `1.27.0` | NuGet配布のDLLとUnityの `.meta` を管理 |
| 整形ルール | UTF-8、LF、スペース4、行幅100 | `client/.editorconfig` |
| Gitの改行 | 対象C#と追加設定のLFを維持 | `client/.gitattributes` |
| 診断の重大度 | Unityコンパイル時のrule setに集約 | `client/Assets/Default.ruleset` |

2026-09-10時点の配布情報で [CSharpier 1.3.0](https://www.nuget.org/packages/CSharpier/1.3.0)、[Microsoft.Unity.Analyzers 1.27.0](https://www.nuget.org/packages/Microsoft.Unity.Analyzers/1.27.0) を確認した。
.NET 10はLTSで、[Microsoftのリリース情報](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json) にSDK 10.0.401が掲載されている。
実装時に、この組み合わせで復元・実行できることを確認して固定する。

## CSharpierの導入

`client/` にローカルツールのmanifestを作り、全員が同じバージョンを復元する。
CSharpierの対象ランタイムがない環境では、ツール導入時の `--allow-roll-forward` を使って.NET 10で動作することを確認する。
SDKの固定とツールのランタイム選択は別の設定として扱う。
参照：[CSharpierの導入](https://csharpier.com/docs/Installation)、[.NETツールのランタイム選択](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-install#options)。

整形設定は `.editorconfig` のC#セクションへ集約し、`.csharpierrc` に重複させない。
CSharpierは `.editorconfig` に対応している。参照：[CSharpierの設定](https://csharpier.com/docs/Configuration)。
対象は `Assets/` 配下で管理するC#とし、`.csharpierignore` でC#以外、テンプレートの `Assets/TutorialInfo/`、第三者ライブラリ、生成コードを除外する。
新しいゲームコードとCI用のEditorコードは対象に含める。
Library、Temp、Package Cache、生成される `.csproj` と `.slnx` は整形対象にしない。

導入後の共通コマンドは、Windows・macOSとも次の形に揃える。

```text
cd client
dotnet tool restore
dotnet csharpier format Assets
dotnet csharpier check Assets
```

CIでは `check` だけを実行し、未整形を成功扱いにするオプションは使わない。
参照：[CSharpierのCLI](https://csharpier.com/docs/CLI)。
既存の `.vscode/settings.json` と `extensions.json` を保ち、C#のFormatter・保存時整形・推奨拡張だけを追記する。
IDE拡張がなくても上記コマンドで確認できるようにする。

## Microsoft.Unity.Analyzersの導入

NuGetの固定版から必要なAnalyzer DLLを取り出し、`Assets/Analyzers/Microsoft.Unity.Analyzers/` に配置する。
出所、バージョン、SHA-256、取得手順を記録し、配布ライセンスを同梱する。
不要なCode Fix用DLLやRoslyn本体は一括で持ち込まない。

UnityのPlugin Inspectorで通常の実行時プラグインの対象を外し、`RoslynAnalyzer` ラベルを付ける。
設定を保存した `.meta` もGitで管理し、Unityが生成する `.csproj` を手書きで変更しない。
参照：[Unity 6.6の既存Analyzer導入手順](https://docs.unity3d.com/6000.6/Documentation/Manual/install-existing-analyzer.html)。

初期設定では、次の診断を `Default.ruleset` でErrorにし、残りは配布元の既定値から始める。
Unityや第三者パッケージの警告を一括でErrorにする設定は追加しない。

| 診断ID | Errorにする内容 |
|---|---|
| UNT0006 | Unityメッセージのシグネチャの誤り |
| UNT0007 | Unity Objectへのnull合体演算子の使用 |
| UNT0008 | Unity Objectへのnull条件演算子の使用 |
| UNT0010 | Componentを `new` するコード |
| UNT0011 | ScriptableObjectを `new` するコード |

診断の意味は [Microsoftの診断一覧](https://github.com/microsoft/Microsoft.Unity.Analyzers/blob/main/doc/index.md) を確認する。
Unityの重大度設定はrule setを正本とし、`.editorconfig` に同じ診断設定を複製しない。
参照：[UnityのAnalyzerとrule setの適用範囲](https://docs.unity3d.com/6000.6/Documentation/Manual/analyzer-scope-and-diagnostics.html)。

IDEのプロジェクトファイルを再生成し、固定版DLLとrule setの参照を確認する。
実装中に追加されたZed連携では、生成されたプロジェクトにAnalyzerとrule setが含まれないことを確認した。
Unityの生成フックで不足する参照を補い、IDE由来の同名Analyzerと重複する場合は固定版だけを残す。
Microsoft.Unity.Analyzers以外のAnalyzerやSource Generatorは保持する。
参照：[Microsoftの重複診断に関する説明](https://github.com/microsoft/Microsoft.Unity.Analyzers#handling-duplicate-diagnostics)。

## CIの構成

`.github/workflows/client-ci.yml` に次の2種類のチェックを追加する。

| job | 内容 | 実行環境 |
|---|---|---|
| `Client format` | 固定.NET SDK、ツール復元、CSharpier check | GitHub-hosted Windows・macOSのmatrix |
| `Client analyzers` | Unityのimport、C#コンパイル、Analyzer実行 | GitHub-hosted UbuntuとGameCIを第一候補とする |

初期導入ではWindows・macOSの整形結果と改行を両方確認する。
Unityコンパイルのjobは1環境で実行し、Editorバージョンを `ProjectVersion.txt` に合わせる。

GameCIのカスタム `buildMethod` に、コンパイル確認用のEditorメソッドを指定する。
このメソッドではアプリのビルドや機能テストを開始せず、コンパイル後の確認結果を返す。
コンパイルエラー、Analyzerの読み込み失敗、Error指定の診断がjobの失敗になることを確認する。
成功には終了コードだけでなく、確認メソッドへの到達とAnalyzerの設定・読み込みの確認を必要とする。
設定確認後にコンパイラのキャッシュをクリアして再コンパイルし、完了コールバックで診断レポートを保存して終了する。
Unityは再コンパイル後のDLLが同じ場合にも `assemblyCompilationNotRequired` を通知するため、この通知も完了確認に含める。
Analyzerの読み込み・実行失敗を表すRoslyn診断をrulesetでErrorにし、メソッドへ到達する前の失敗も検出する。
参照：[GameCIのカスタムbuildMethod](https://game.ci/docs/github/builder/#buildmethod)、[Unity Editorのバッチ実行](https://docs.unity3d.com/6000.6/Documentation/Manual/EditorCommandLineArguments.html)。

今回はCIのEditorターゲットで有効なC#を解析する。
`UNITY_ANDROID` など別ターゲットでだけ有効なコードやネイティブコードの確認は、後続のAndroidビルドで扱う。

- `push`、`pull_request`、手動実行に対応し、`client/**` と当該workflowの変更で起動する。
- 補助スクリプトをclient外へ置く場合は、そのパスも起動対象に含める。
- PRの必須チェックにする場合は、パス除外されたworkflowが待機状態を残さない構成を確認する。
- ActionはコミットSHAで固定し、権限は `contents: read` を基本とする。
- 実行時間の上限、同一PRの古い実行のキャンセル、失敗時のUnityログ保存を設定する。
- LibraryキャッシュはUnity版・OS・Packages・Analyzer・rule setの変更を識別できるキーを使い、キャッシュなしでも成功することを確認する。

## Unity PersonalでのCI実行

Unityを使うjobにはライセンス認証が必要である。
Hubの「Unity CLIを使用」をONにしただけでは、CI側の認証は完了しない。
実装の最初に6000.6.0f1のCI用Editor環境の入手性と、Personalライセンスでバッチ起動できることを確認する。
参照：[GameCIのPersonalライセンス設定](https://game.ci/docs/github/activation/#personal-license)。

GameCIのPersonal向け手順を第一候補とし、対応するライセンスファイルとGitHub Secretsの設定可否を確認する。
必要な認証情報の値は文書・コード・ログに残さない。
ライセンス方式やEditor版の組み合わせが成立しない場合は、認証済みのrunnerなどの代案を比較する。
runnerの常駐や課金を伴う変更は、利用者の判断事項として提示する。
ライセンスなしで実行できるFormatterと、ローカルUnityへのAnalyzer導入は独立して進められる。

Secretsを利用できないfork PRでは、Analyzerを実行できたことにしない。
`pull_request_target` で外部PRのコードへSecretsを渡す構成にはしない。

## 追加・変更するファイル

以下は配置予定である。
`Assets/` 以下の追加ファイルとフォルダには、対応する `.meta` も含める。

| 配置予定 | 役割 |
|---|---|
| `client/global.json` | .NET SDKの固定 |
| `client/.config/dotnet-tools.json` | CSharpierの固定 |
| `client/.editorconfig`、`.gitattributes`、`.csharpierignore` | 整形・改行・対象範囲 |
| `client/.vscode/settings.json`、`extensions.json` | 既存設定へCSharpier連携を追記 |
| `client/Assets/Analyzers/Microsoft.Unity.Analyzers/` | DLL、取得情報、ライセンス |
| `client/Assets/Default.ruleset` | Analyzer診断の重大度 |
| `client/Assets/Editor/CI/CompileCheck.cs` | コンパイル確認の入口 |
| `client/Assets/Editor/AnalyzerProjectSettings.cs` | IDEのAnalyzer参照を補い、同名Analyzerの重複を除く生成フック |
| `.github/workflows/client-ci.yml` | 整形・静的解析のチェック |
| `doc/rules/client-code-quality.md` | 実装で確定した導入・確認・更新手順 |
| `doc/README.md` | 計画と開発ルールへのリンク |

## 実装順序と受入条件

| 順序 | 作業 | 受入条件 |
|---|---|---|
| 1 | CI実行環境とSDKの前提確認 | Personal認証とEditor版の可否、必要な利用者操作が分かる |
| 2 | CSharpier・改行・IDE設定の導入 | 固定版を復元でき、format後のcheckが成功する |
| 3 | Analyzerとrule setの導入 | Unityコンパイル引数に固定版Analyzerとrule setが現れる |
| 4 | コンパイル確認メソッドとworkflowの追加 | バッチ実行で完了到達と失敗伝播を確認できる |
| 5 | 正常・異常の確認 | 未整形とUNT0008違反がそれぞれ失敗し、修正後に成功する |
| 6 | 開発ルールの文書化 | Windows・macOSの共通手順と前提条件を参照できる |
| 7 | 許可されたpush後のGitHub確認 | 2種類のチェックが起動し、成功・失敗を正しく表示する |

導入確認では一時コピーに整形違反とUnity Objectへの `?.` を入れ、異常を見逃さないことを確認する。
DLLの読み込みに失敗した場合にもCIが成功しないことを確認する。
検証用の違反コードは通常のプロジェクトへ残さず、そのためだけにゲーム機能のテスト基盤やアセンブリ構成を追加しない。

Unityが開いている作業ディレクトリへ別のバッチ実行を重ねない。
ローカル確認はEditor内で行うか、一時コピーのプロジェクトで実行する。
OSごとの確認結果とGitHub上で実行した範囲は分けて記録する。

## 実装・検証結果

状態：ローカル実装・検証済み。GitHubの初回実行はCI確認待ち。
確定した開発手順は [クライアントの整形と静的解析](../rules/client-code-quality.md) を参照する。

| 確認項目 | 結果 |
|---|---|
| CSharpier 1.3.0の復元と実行 | Windows・.NET SDK 10.0.401で成功 |
| 整形違反と除外 | 未整形は終了1、整形後は終了0。テンプレート・XMLの除外とLFを確認 |
| Unityへの適用 | コンパイル引数に固定DLLと `Assets/Default.ruleset` を確認 |
| キャッシュを持つUnityプロジェクトの確認 | 79アセンブリ、完了記録あり、終了0 |
| UNT0008違反 | 一時コピーで `error UNT0008`、終了1 |
| RoslynAnalyzerラベル欠落 | 設定検証で失敗、終了1 |
| Analyzer DLL破損 | SHA-256不一致で失敗、終了1 |
| 元に戻した後のコンパイル | 完了記録あり、終了0 |
| Libraryなしの最終構成 | 79アセンブリ、完了記録あり、終了0 |
| Zed向けのプロジェクト再生成 | 生成されたC#・Editor両プロジェクトに固定版Analyzerとrulesetの参照を確認 |
| IDE由来の重複参照 | 一時コピーで同名Analyzerだけを重複排除し、別のSource Generatorを保持することを確認 |
| GitHub workflow構文 | actionlint 1.7.12で成功 |
| macOS上の整形とGitHub上のUnity | 初回CI実行待ち |

.NET SDKは公式配布のSHA-512を照合し、一時領域へ展開してローカル検証に使用した。
通常の開発環境では、開発手順に従って指定版SDKを導入する。
Unityの検証は6000.6.0f1とローカルのPersonalライセンスを使い、開いているプロジェクトへバッチ処理を重ねずに実施した。
Unity CLIの `unity` はこの検証シェルで見つからなかったため、Editorのバッチ起動で確認した。

GitHubのRepository secretsは確認時点で未登録である。
`UNITY_LICENSE`・`UNITY_EMAIL`・`UNITY_PASSWORD` の登録と、許可されたcommit・push後の初回実行が残る。
GitHubでの初回成功が未確認のため、この計画は完了扱いにしない。
