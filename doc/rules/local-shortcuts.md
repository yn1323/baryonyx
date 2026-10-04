---
id: rule-local-shortcuts
type: reference
status: 運用中
updated: 2026-10-05
---

# 手動実行用ショートカットの動作

[設計・開発ルールの索引](README.md) / [ショートカットの一覧](../../AGENTS.md#手動実行用ショートカット)

`shortcuts/` に置いたファイルの動作・前提・ログを定める。
ファイルの一覧、追加するときの決まり、ビルド後にAPKをGoogle Driveへコピーする配置先は、ルートの[AGENTS.md](../../AGENTS.md#手動実行用ショートカット)を正本とする。
エミュレーターの構成・初回準備・起動できない場合の確認は[WindowsでのUnityとAndroidエミュレーター](client-android-emulator.md)に従う。

## 共通の作り

- ビルド用の `.bat` はWindows標準のPowerShell、エミュレーター用の `.bat` はコマンドプロンプトで処理する。macOS用の `.command` は、同じ環境の `.bat` と同じ処理をbashで行う。どれも必要なコードを同じファイル内に持つ。
- 結果を確認できるよう、各ファイルは終了時にキー入力を待つ。
- ログはファイルごとに `client/Logs/` へ書き、実行ごとに上書きする。

## APKのビルド

`build-apk.bat`・`build-apk-dev-to-drive.*`・`build-apk-prod-to-drive.*` は、[ProjectVersion.txt](../../client/ProjectSettings/ProjectVersion.txt) の版のUnityで、CIと同じ [AndroidBuild.Build](../../client/Assets/Baryonyx/Editor/CI/AndroidBuild.cs) を呼び出す。
出力先は `client/Builds/Android/baryonyx.apk` である。
このプロジェクトをUnityで閉じてから実行する。
対象バージョンのAndroid Build Supportと、有効なUnityライセンスが必要になる。

| OS | Unityの探し方 |
|---|---|
| Windows | Unity Hubの標準配置 `%ProgramFiles%\Unity\Hub\Editor\<バージョン>\Editor\Unity.exe` |
| macOS | Unity Hubの標準配置 `/Applications/Unity/Hub/Editor/<バージョン>/Unity.app/Contents/MacOS/Unity` |

別の場所に入れた場合は、同じバージョンの実行ファイルのパスを第1引数または環境変数 `UNITY_EDITOR_PATH` で指定する（第1引数を優先する）。

| ファイル | ログ |
|---|---|
| `build-apk.bat` | `client/Logs/build-apk.log` |
| `build-apk-dev-to-drive.bat`・`.command` | `client/Logs/build-apk-dev-to-drive.log` |
| `build-apk-prod-to-drive.bat`・`.command` | `client/Logs/build-apk-prod-to-drive.log` |

`build-apk.bat` はAPKの生成だけを行い、テスト・配布・エミュレーターの起動・インストールは行わない。

## 接続先の環境

APKの接続先はビルド時の環境変数 `BARYONYX_ENVIRONMENT` で選び、未指定ならDevになる（[実行環境と接続先](../features/startup-sync.md#実行環境と接続先)）。
Drive配置用のショートカットは、ファイル名の環境（`dev`・`prod`）をビルド前に設定し、手元の `BARYONYX_SERVER_URL` を無視する。
ビルドログの `BARYONYX_ANDROID_SERVER:` の行で指定した環境になっていることを確かめ、違えばコピーせずにエラーで終了する。
Prod向けは、ProdのURLが未設定の間はビルドを止める。

## Google Driveへの配置

Drive配置用のショートカットは、ビルドが成功したら、APKを[配置先](../../AGENTS.md#手動実行用ショートカット)へ環境別のファイル名で上書きコピーする。
Dev向けとProd向けのファイルは、設定する環境名・配置先のファイル名・ログ名だけが異なる。

- Google Drive for desktopを起動し、配置先フォルダーが存在してアクセスできることを前提とする。
- ビルドに失敗したときは、配置先のAPKを更新しない。コピーに失敗したときもエラーで終了する。
- コピー後も `client/Builds/Android/baryonyx.apk` を残す。
- Dev向けは、コピーに成功したあと、環境別のファイル名にする前の `baryonyx.apk` が配置先に残っていれば削除する。
- Google Driveへの同期はGoogle Drive for desktopが行うため、同期の完了は同アプリで確認する。

## エミュレーターの起動とAPKのインストール

`start-pixel-8a.bat`・`install-apk-pixel-8a.bat` は、`ANDROID_HOME`、`ANDROID_SDK_ROOT`、`%LOCALAPPDATA%\Android\Sdk` の順に必要なAndroid SDKのツールを探す。
`start-pixel-8a.bat` は、[確認環境](client-android-emulator.md)の仮想端末 `Pixel_8a_API_36` を同文書の起動引数で起動する。
AIは、検証時も含めてこのファイルを実行しない（[デバッグ環境](../../AGENTS.md#デバッグ環境)）。

`install-apk-pixel-8a.bat` は、起動中のエミュレーターに `adb shell getprop ro.boot.qemu.avd_name` を実行し、AVD名でインストール先を特定する。
実機や別名のAVDにはインストールしない。
対象のAVDが未起動・起動途中・同名で複数起動の場合はエラーで終了し、エミュレーターを自動で起動しない。
既存アプリのデータを保持して更新する `adb install -r` を使う（[Android公式ドキュメント](https://developer.android.com/tools/adb?hl=ja#move)）。
既定では `client/Builds/Android/baryonyx.apk` を入れる。
別のAPKは、ファイルへ1つドラッグ＆ドロップするか、第1引数にパスを指定する。
