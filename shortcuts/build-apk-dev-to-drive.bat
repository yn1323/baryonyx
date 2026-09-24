@echo off
setlocal EnableExtensions DisableDelayedExpansion
chcp 65001 >nul
rem Build an APK for the dev game server, ignoring any server settings in the caller environment.
set "BARYONYX_ENVIRONMENT=dev"
set "BARYONYX_SERVER_URL="
set "BARYONYX_BUILD_SCRIPT=%~f0"
set "BARYONYX_EDITOR_ARGUMENT=%~1"
set "BARYONYX_EXTRA_ARGUMENT=%~2"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$s = [IO.File]::ReadAllText($env:BARYONYX_BUILD_SCRIPT, [Text.Encoding]::UTF8); & ([ScriptBlock]::Create(($s -split '(?m)^# POWERSHELL\r?$', 2)[1])); exit $LASTEXITCODE"
set "RESULT=%ERRORLEVEL%"
pause >nul
exit /b %RESULT%
# POWERSHELL
$ErrorActionPreference = 'Stop'
$result = 1
try {
    if ($env:BARYONYX_EXTRA_ARGUMENT) {
        throw '指定できる引数はUnity.exeのパス1つだけです。'
    }
    $destinationDirectory = 'G:\マイドライブ\71_プロジェクト\baryonyx'
    if (-not (Test-Path -LiteralPath $destinationDirectory -PathType Container)) {
        throw "配置先フォルダーが見つかりません。Google Drive for desktopの起動とフォルダーを確認してください: $destinationDirectory"
    }
    $destinationApk = Join-Path $destinationDirectory 'baryonyx-dev.apk'
    if (Test-Path -LiteralPath $destinationApk -PathType Container) {
        throw "APKの配置先に同名のフォルダーがあります: $destinationApk"
    }
    $shortcutDirectory = Split-Path -Parent $env:BARYONYX_BUILD_SCRIPT
    $projectDirectory = [IO.Path]::GetFullPath((Join-Path $shortcutDirectory '..\client'))
    $versionFile = Join-Path $projectDirectory 'ProjectSettings\ProjectVersion.txt'
    if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) {
        throw "Unityプロジェクトが見つかりません: $projectDirectory"
    }
    $versionMatch = [regex]::Match([IO.File]::ReadAllText($versionFile), '(?m)^m_EditorVersion:\s*(\S+)')
    if (-not $versionMatch.Success) {
        throw 'ProjectVersion.txtからUnityのバージョンを取得できませんでした。'
    }
    $editorVersion = $versionMatch.Groups[1].Value
    $unityExecutable = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$editorVersion\Editor\Unity.exe"
    if ($env:UNITY_EDITOR_PATH) { $unityExecutable = $env:UNITY_EDITOR_PATH }
    if ($env:BARYONYX_EDITOR_ARGUMENT) { $unityExecutable = $env:BARYONYX_EDITOR_ARGUMENT }
    $unityExecutable = [IO.Path]::GetFullPath($unityExecutable)
    if (-not (Test-Path -LiteralPath $unityExecutable -PathType Leaf)) {
        throw "Unity $editorVersion が見つかりません: $unityExecutable`nUnity Hubで対象バージョンとAndroid Build Supportをインストールしてください。`n別の配置では、同じバージョンのUnity.exeのパスを第1引数またはUNITY_EDITOR_PATHで指定してください。"
    }
    if (Test-Path -LiteralPath (Join-Path $projectDirectory 'Temp\UnityLockfile')) {
        throw 'このプロジェクトのUnityロックファイルが存在します。Unityで作業を保存してこのプロジェクトを閉じ、終了完了後にやり直してください。'
    }
    $logDirectory = Join-Path $projectDirectory 'Logs'
    $buildLog = Join-Path $logDirectory 'build-apk-dev-to-drive.log'
    $apkPath = Join-Path $projectDirectory 'Builds\Android\baryonyx.apk'
    [IO.Directory]::CreateDirectory($logDirectory) | Out-Null
    # 前回の成功ログを今回の成功と誤認しないよう、実行前に初期化する。
    [IO.File]::WriteAllText($buildLog, '')
    Write-Host "Unity $editorVersion でdev環境向けのAPKをビルドします。完了までこのウィンドウを開いたままにしてください。"
    Write-Host "ビルドログ: $buildLog"
    Write-Host "ビルド成功後の配置先: $destinationApk（同名ファイルは上書き）"
    Push-Location -LiteralPath $projectDirectory
    try {
        & $unityExecutable -batchmode -quit -nographics -projectPath $projectDirectory -buildTarget Android -executeMethod Baryonyx.Editor.CI.AndroidBuild.Build -logFile $buildLog | Out-Host
        $unityExitCode = $LASTEXITCODE
    } finally {
        Pop-Location
    }
    if ($unityExitCode -ne 0) {
        $result = $unityExitCode
        throw "APKのビルドに失敗しました。ビルドログを確認してください: $buildLog"
    }
    $buildCompleted = Select-String -LiteralPath $buildLog -SimpleMatch 'BARYONYX_ANDROID_BUILD_OK: Builds/Android/baryonyx.apk' -Quiet
    if (-not $buildCompleted) {
        throw "ビルド完了を確認できませんでした。ビルドログを確認してください: $buildLog"
    }
    $serverSelected = Select-String -LiteralPath $buildLog -SimpleMatch 'BARYONYX_ANDROID_SERVER: dev ' -Quiet
    if (-not $serverSelected) {
        throw "dev環境の接続先でビルドされたことを確認できませんでした。ビルドログを確認してください: $buildLog"
    }
    if (-not (Test-Path -LiteralPath $apkPath -PathType Leaf)) {
        throw "ビルド後のAPKが見つかりません: $apkPath"
    }
    if ((Get-Item -LiteralPath $apkPath).Length -eq 0) {
        throw "ビルド後のAPKが空です: $apkPath"
    }
    Write-Host "ビルドが完了しました: $apkPath"
    [IO.File]::Copy($apkPath, $destinationApk, $true)
    $result = 0
    Write-Host "APKを配置しました: $destinationApk"
    # 環境別のファイル名にする前の baryonyx.apk は、Dev向けのAPKに置き換わったため残さない。
    $legacyApk = Join-Path $destinationDirectory 'baryonyx.apk'
    if (Test-Path -LiteralPath $legacyApk -PathType Leaf) {
        try {
            Remove-Item -LiteralPath $legacyApk -Force
            Write-Host "以前のファイル名のAPKを削除しました: $legacyApk"
        } catch {
            Write-Host ("[エラー] 以前のファイル名のAPKを削除できませんでした。手で削除してください: " + $legacyApk) -ForegroundColor Red
        }
    }
    Write-Host 'Google Driveへの同期状況はGoogle Drive for desktopで確認してください。'
} catch {
    Write-Host ("[エラー] " + $_.Exception.Message) -ForegroundColor Red
} finally {
    Write-Host ''
    Write-Host 'キーを押すとこのウィンドウを閉じます。'
}
exit $result
