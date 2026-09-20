# AGENTS.md

`client/` の作業には、[ルートのAGENTS.md](../AGENTS.md)と以下の指示を適用する。
C#編集はZed、Unityの操作・検証はUnity CLIを基本とする。
Unityのバージョンは [ProjectSettings/ProjectVersion.txt](ProjectSettings/ProjectVersion.txt) に従う。

## 配置と設計の参照先

ディレクトリ構成、コード・アセット・テストの配置、責務と依存方向は [クライアントの構成と依存関係](../doc/rules/frontend-design.md) に従う。
現在の起動シーンは `Assets/Baryonyx/App/Scenes/Main.unity` である。
実行方法は [UnityのテストとCI](../doc/rules/client-testing.md)、画面設計は [UI設計ルール](../doc/rules/ui-design.md) を参照する。

## サーバーのBaseURL

Unityクライアントが利用するBaseURLは、[HealthConnectionSettings.asset](Assets/Baryonyx/Features/Health/Data/HealthConnectionSettings.asset) の `ServerBaseUrl` に保持する。
この値は公開される接続先であり、APIキー、CloudflareのAPIトークン、クライアントシークレットなどの秘密情報をこのAssetやUnityプロジェクトへ追加しない。

環境ごとの値は次のとおりとする。

- Unity EditorでLocalサーバーを使うときは `http://127.0.0.1:3000`。
- Androidエミュレーターから同じPCのLocalサーバーを使うときは `http://10.0.2.2:3000`。
- Dev向けAPKを作るときは `https://baryonyx-server-dev.croissant-lab.workers.dev`。

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

`ServerBaseUrl` だけを修正する場合は、リポジトリのルートで次のPowerShellを実行する。
`$baseUrl` にはLocalまたはDevのURLだけを指定し、実行後に差分を確認する。

```powershell
$settingsPath = 'client/Assets/Baryonyx/Features/Health/Data/HealthConnectionSettings.asset'
$baseUrl = 'https://baryonyx-server-dev.croissant-lab.workers.dev'
if (-not (Test-Path $settingsPath)) { throw "設定Assetが見つかりません: $settingsPath" }
$content = [System.IO.File]::ReadAllText((Resolve-Path $settingsPath))
if ($content -notmatch '(?m)^  ServerBaseUrl:') { throw 'ServerBaseUrlフィールドが見つかりません。' }
$content = [regex]::Replace($content, '(?m)^  ServerBaseUrl:.*$', "  ServerBaseUrl: $baseUrl", 1)
[System.IO.File]::WriteAllText(
    (Resolve-Path $settingsPath),
    $content,
    [System.Text.UTF8Encoding]::new($false)
)
```

Localで作業するときは `$baseUrl` を `http://127.0.0.1:3000` に変更する。
Dev向けAPKをビルドするときはDev URLへ変更してから [build-apk.bat](../shortcuts/build-apk.bat) または [build-apk-to-drive.bat](../shortcuts/build-apk-to-drive.bat) を実行する。
ビルド後にLocalへ戻す場合も同じ修正コマンドを再実行する。
この手順では設定Assetをビルド中に自動生成・書き換えないため、ビルド後に作業ツリーへ意図しない変更を残さないよう、変更前後の差分を確認する。

## Unity CLI

Unity CLIの利用手順は、Unityプラグインの `unity:unity-cli` スキルに従う。
接続に使うUnity Pipelineは、[Packages/manifest.json](Packages/manifest.json)に含まれている。
接続先がこのリポジトリの `client/` であることを確認する。

## Unityの操作と検証

- WindowsのAndroidエミュレーターでAPKを確認するときは [専用の手順](../doc/rules/client-android-emulator.md) とルートの [手動実行用ショートカット](../AGENTS.md#手動実行用ショートカット) を使う。
- 起動確認のためにビルド対象をARMv7やx86_64へ変更せず、現行のIL2CPP・ARM64 APKと、手順に記載したAndroid 16のAVDを使う。
- C#変更後は、再コンパイルの完了とConsoleのエラーを確認する。
- 変更した動作に対応するテストを実行する。テスト0件は合格として扱わない。
- PlayModeの開始・停止やシーンの切り替えは、実行中の作業を確認してから行う。
- アセットの移動・名前変更・削除はUnityの機能を使い、対応する `.meta` とGUIDの整合を保つ。
- 画面変更後はGameビューを撮影し、保存した画像を開いて確認する。Overlay UIを含める場合はPlayModeで `capture_game_view --source screen` を使う。
- 検証画像は `Assets/DevCaptures/` に保存する。このフォルダと対応する `.meta` はGit除外済み。
