#!/bin/bash
# macOS用。Finderでダブルクリックするとターミナルで実行される。
# Dev環境のゲームサーバーへ接続するAPKをビルドする。
set -u

# 手元の環境変数に関係なく、devの接続先でビルドする。
export BARYONYX_ENVIRONMENT=dev
unset BARYONYX_SERVER_URL

error() {
    printf '\033[31m[エラー] %s\033[0m\n' "$1"
}

run() {
    if [ $# -gt 1 ]; then
        error '指定できる引数はUnityのパス1つだけです。'
        return 1
    fi
    local destination_directory="$HOME/Google Drive/マイドライブ/71_プロジェクト/baryonyx"
    if [ ! -d "$destination_directory" ]; then
        error "配置先フォルダーが見つかりません。Google Drive for desktopの起動とフォルダーを確認してください: $destination_directory"
        return 1
    fi
    local destination_apk="$destination_directory/baryonyx-dev.apk"
    if [ -d "$destination_apk" ]; then
        error "APKの配置先に同名のフォルダーがあります: $destination_apk"
        return 1
    fi
    local shortcut_directory project_directory
    shortcut_directory="$(cd "$(dirname "$0")" && pwd)"
    project_directory="$(cd "$shortcut_directory/../client" 2>/dev/null && pwd)"
    local version_file="$project_directory/ProjectSettings/ProjectVersion.txt"
    if [ -z "$project_directory" ] || [ ! -f "$version_file" ]; then
        error "Unityプロジェクトが見つかりません: $shortcut_directory/../client"
        return 1
    fi
    local editor_version
    editor_version="$(sed -n 's/^m_EditorVersion:[[:space:]]*\([^[:space:]]*\).*/\1/p' "$version_file" | head -n 1)"
    if [ -z "$editor_version" ]; then
        error 'ProjectVersion.txtからUnityのバージョンを取得できませんでした。'
        return 1
    fi
    local unity_executable="/Applications/Unity/Hub/Editor/$editor_version/Unity.app/Contents/MacOS/Unity"
    if [ -n "${UNITY_EDITOR_PATH:-}" ]; then unity_executable="$UNITY_EDITOR_PATH"; fi
    if [ $# -eq 1 ]; then unity_executable="$1"; fi
    if [ ! -x "$unity_executable" ] || [ -d "$unity_executable" ]; then
        error "Unity $editor_version が見つかりません: $unity_executable"$'\n''Unity Hubで対象バージョンとAndroid Build Supportをインストールしてください。'$'\n''別の配置では、同じバージョンのUnity.app/Contents/MacOS/Unityのパスを第1引数またはUNITY_EDITOR_PATHで指定してください。'
        return 1
    fi
    if [ -e "$project_directory/Temp/UnityLockfile" ]; then
        error 'このプロジェクトのUnityロックファイルが存在します。Unityで作業を保存してこのプロジェクトを閉じ、終了完了後にやり直してください。'
        return 1
    fi
    local log_directory="$project_directory/Logs"
    local build_log="$log_directory/build-apk-dev-to-drive.log"
    local apk_path="$project_directory/Builds/Android/baryonyx.apk"
    mkdir -p "$log_directory" || return 1
    # 前回の成功ログを今回の成功と誤認しないよう、実行前に初期化する。
    : > "$build_log" || return 1
    echo "Unity $editor_version でdev環境向けのAPKをビルドします。完了までこのウィンドウを開いたままにしてください。"
    echo "ビルドログ: $build_log"
    echo "ビルド成功後の配置先: $destination_apk（同名ファイルは上書き）"
    (cd "$project_directory" && "$unity_executable" -batchmode -quit -nographics -projectPath "$project_directory" -buildTarget Android -executeMethod Baryonyx.Editor.CI.AndroidBuild.Build -logFile "$build_log")
    local unity_exit_code=$?
    if [ $unity_exit_code -ne 0 ]; then
        error "APKのビルドに失敗しました。ビルドログを確認してください: $build_log"
        return $unity_exit_code
    fi
    if ! grep -qF 'BARYONYX_ANDROID_BUILD_OK: Builds/Android/baryonyx.apk' "$build_log"; then
        error "ビルド完了を確認できませんでした。ビルドログを確認してください: $build_log"
        return 1
    fi
    if ! grep -qF 'BARYONYX_ANDROID_SERVER: dev ' "$build_log"; then
        error "dev環境の接続先でビルドされたことを確認できませんでした。ビルドログを確認してください: $build_log"
        return 1
    fi
    if [ ! -f "$apk_path" ]; then
        error "ビルド後のAPKが見つかりません: $apk_path"
        return 1
    fi
    if [ ! -s "$apk_path" ]; then
        error "ビルド後のAPKが空です: $apk_path"
        return 1
    fi
    echo "ビルドが完了しました: $apk_path"
    if ! cp -f "$apk_path" "$destination_apk"; then
        error "APKのコピーに失敗しました: $destination_apk"
        return 1
    fi
    echo "APKを配置しました: $destination_apk"
    # 環境別のファイル名にする前の baryonyx.apk は、Dev向けのAPKに置き換わったため残さない。
    local legacy_apk="$destination_directory/baryonyx.apk"
    if [ -f "$legacy_apk" ]; then
        if rm -f "$legacy_apk"; then
            echo "以前のファイル名のAPKを削除しました: $legacy_apk"
        else
            error "以前のファイル名のAPKを削除できませんでした。手で削除してください: $legacy_apk"
        fi
    fi
    echo 'Google Driveへの同期状況はGoogle Drive for desktopで確認してください。'
    return 0
}

run "$@"
result=$?
echo ''
read -r -n 1 -s -p 'キーを押すとこのウィンドウを閉じます。'
echo ''
exit $result
