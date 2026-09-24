# AGENTS.md

`client/` の作業には、[ルートのAGENTS.md](../AGENTS.md)と以下の指示を適用する。
C#編集はZed、Unityの操作・検証はUnity CLIを基本とする。
Unityのバージョンは [ProjectSettings/ProjectVersion.txt](ProjectSettings/ProjectVersion.txt) に従う。

## 配置と設計の参照先

ディレクトリ構成、コード・アセット・テストの配置、責務と依存方向は [クライアントの構成と依存関係](../doc/rules/frontend-design.md) に従う。
現在の起動シーンは `Assets/Baryonyx/App/Scenes/Top.unity` であり、全面押下で `Home.unity` へ上下から閉じるShutter演出（閉じる・開くとも0.75秒）で遷移する。
Topは起動時にゲームサーバーへの接続とHealth Connectの歩数の同期を行い、終わるまで「LOADING...」を表示する（[起動時の連携と歩数の同期](../doc/features/startup-sync.md)）。
`Home.unity` はホーム画面のモックで、左上の今日の歩数だけサーバーの値を表示する。行き先カード（再開）の遷移先は未設定のため遷移しない。
シーンはTop・Home・展示室の `Showcase.unity` だけである。
実行方法は [UnityのテストとCI](../doc/rules/client-testing.md)、画面設計は [UI設計ルール](../doc/rules/ui-design.md) を参照する。

## サーバーのBaseURL

Unityクライアントの接続先は、[HealthConnectionSettings.asset](Assets/Baryonyx/Features/Health/Data/HealthConnectionSettings.asset) に環境ごとに保持する。
この値は公開される接続先であり、APIキー、CloudflareのAPIトークン、クライアントシークレットなどの秘密情報をこのAssetやUnityプロジェクトへ追加しない。
接続先を切り替えるためにAssetを書き換えない。

| 項目 | 値 |
|---|---|
| `DevServerUrl` | `https://baryonyx-server-dev.croissant-lab.workers.dev` |
| `ProdServerUrl` | 未設定（Prod環境の公開後に設定する） |
| `BuildServerUrl` | リポジトリでは常に空。APKのビルド中だけ、選んだ環境のURLが入る |

EditorのPlayで接続する先は、メニューの `Baryonyx > Server` で `Local (127.0.0.1:4000)`・`Dev`・`Prod` から選ぶ。
選択は開発者ごとの `client/UserSettings/BaryonyxServer.json`（Git対象外）に保存し、未選択ならLocalに接続する。
この選択はAPKに影響しない。

APKの接続先は、[AndroidBuild.Build](Assets/Baryonyx/Editor/CI/AndroidBuild.cs) を実行するときの環境変数で決める。

| 環境変数 | 接続先 |
|---|---|
| どちらも未指定 | Dev |
| `BARYONYX_ENVIRONMENT=dev` または `preview` | Dev（PreviewのAPKは当面Devへ接続する） |
| `BARYONYX_ENVIRONMENT=prod` | `ProdServerUrl`。未設定ならビルドを止める |
| `BARYONYX_SERVER_URL=<URL>` | 指定したURL。環境名より優先する。Androidエミュレーターから同じPCのLocalサーバーを使うときは `http://10.0.2.2:4000`（エミュレーター内の `127.0.0.1` はエミュレーター自身を指すため） |

Google Driveへ配置するときは、環境名をあらかじめ設定した[環境別のショートカット](../AGENTS.md#手動実行用ショートカット)を使う。
ビルドログの `BARYONYX_ANDROID_SERVER:` の行で、APKに入れた環境名とURLを確認できる。
ビルド後は `BuildServerUrl` を空へ戻す。ビルドが途中で強制終了した場合は値が残ることがあるため、Assetの差分を確認して空へ戻す。
[AndroidBuild.Build](Assets/Baryonyx/Editor/CI/AndroidBuild.cs) 以外の方法でビルドしたAPKはDevへ接続する。

HTTPは [ServerApi](Assets/Baryonyx/Shared/Networking/ServerApi.cs) が `127.0.0.1`・`localhost`・`10.0.2.2` だけで受け付け、Player Settingsの「Allow downloads over HTTP」はDevelopment Buildだけで許可している。
Editorで未連携のモーダルを確認するときは、同じAssetの `PreviewStartsUnlinked` を有効にする。

`HealthConnectionSettings.asset` が存在しない場合は、Unity Editorで `Baryonyx > Health > Create Screen Assets` を実行して生成する。
Unityを閉じた状態でリポジトリのルートから同じ処理を実行する場合は、次のコマンドを使う。

```powershell
$projectPath = (Resolve-Path 'client').Path
$unityPath = $env:UNITY_EDITOR_PATH
if ([string]::IsNullOrWhiteSpace($unityPath)) {
    $unityPath = (Get-ChildItem "$env:ProgramFiles\Unity\Hub\Editor" -Directory |
        Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName 'Editor\Unity.exe' } |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1)
}
if ([string]::IsNullOrWhiteSpace($unityPath)) {
    throw 'Unity.exeが見つかりません。UNITY_EDITOR_PATHに指定してください。'
}
& $unityPath -batchmode -quit -projectPath $projectPath `
    -executeMethod Baryonyx.Health.Editor.HealthScreenAssets.CreateAssets `
    -logFile (Join-Path $projectPath 'Logs/create-health-assets.log')
if ($LASTEXITCODE -ne 0) { throw "UnityでのAsset生成に失敗しました。終了コード: $LASTEXITCODE" }
```

## Unity CLI

Unity CLIの利用手順は、Unityプラグインの `unity:unity-cli` スキルに従う。
接続に使うUnity Pipelineは、[Packages/manifest.json](Packages/manifest.json)に含まれている。
接続先がこのリポジトリの `client/` であることを確認する。

## Unityの操作と検証

- WindowsのAndroidエミュレーターでAPKを確認するときは [専用の手順](../doc/rules/client-android-emulator.md) とルートの [手動実行用ショートカット](../AGENTS.md#手動実行用ショートカット) を使う。
- 起動確認のためにビルド対象をARMv7やx86_64へ変更せず、現行のIL2CPP・ARM64 APKと、手順に記載したAndroid 16のAVDを使う。
- C#変更後は、再コンパイルの完了とConsoleのエラーを確認する。
- C#変更後は、CIと同じCSharpierで整形・検査する。実行に必要な.NET SDKの導入と実行手順は [整形と静的解析](../doc/rules/client-code-quality.md) に従う。
- 変更した動作に対応するテストを実行する。テスト0件は合格として扱わない。
- PlayModeの開始・停止やシーンの切り替えは、実行中の作業を確認してから行う。
- アセットの移動・名前変更・削除はUnityの機能を使い、対応する `.meta` とGUIDの整合を保つ。
- 画面変更後はGameビューを撮影し、保存した画像を開いて確認する。Overlay UIを含める場合はPlayModeで `capture_game_view --source screen` を使う。
- 検証画像は `Assets/DevCaptures/` に保存する。このフォルダと対応する `.meta` はGit除外済み。

## フォントアセットの差分

[DotGothic16.asset](Assets/Baryonyx/Shared/UI/Fonts/DotGothic16.asset) はTextMeshProの動的フォントアセットである。
Editorで新しい文字を表示すると、Unityが文字の一覧とアトラス画像をこのファイルへ書き足すため、作業内容と関係なく差分が出る。

- コミットするときは、この差分もコミットの対象に含める。作業内容とは別の `chore` コミットに分ける。
- `.gitignore` や `git update-index --skip-worktree` で除外しない。このアセットはTMPの既定フォント設定やシーン・PrefabからGUIDで参照されており、手元にない環境では文字を表示できなくなる。
- 追加された文字データはビルド時に消える（Clear Dynamic Data On Build）ため、コミットしても実機の表示や容量には影響しない。
