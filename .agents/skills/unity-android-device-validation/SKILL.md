---
name: unity-android-device-validation
description: Unity AndroidのAPKを、ユーザーが起動した実機または指定AVDで検証するときに使う。adb、インストール、ログ、権限、Google認証、Health Connect、画面証跡を扱う。エミュレーターの起動や単なるEditorテストには使わない。
---

# Unity Android実機・AVD検証

APKが作成できたことと、Android端末で機能が動くことを分けて確認する。
端末を自動起動せず、接続先を一意に特定し、再現可能なログと画面証跡を残す。

## 絶対条件

- Android Emulatorはユーザーが手動で起動する。AIは起動、再起動、スナップショット復元を行わない。
- 対象AVDは`Pixel_8a_API_36`（Android 16、Google APIs、x86_64）とする。別のAVDへ黙って切り替えない。
- 起動中の端末が0台、対象AVDが0台、対象AVDが複数台、または`sys.boot_completed`が未完了なら、インストールや検証を進めず理由を報告する。
- `adb install -r`を使い、ユーザーのアプリデータを消去しない。データ消去やアプリ設定の初期化が必要な場合は、実行前に目的と影響を明示する。
- 健康データ、OAuthトークン、個人情報をログやスクリーンショットへ残さない。必要な場合はマスクする。

## 発動したら最初に確認すること

1. ルートと`client/`の`AGENTS.md`、APKのパス、`ProjectVersion.txt`、アプリケーションID、対象ビルドのコミットまたは作成時刻を確認する。
2. `ANDROID_HOME`、`ANDROID_SDK_ROOT`、`%LOCALAPPDATA%\Android\Sdk`の順で`platform-tools\adb.exe`を探す。
3. `adb devices`で接続一覧を取得し、`device`状態のエミュレーターだけを候補にする。
4. 各候補に次を実行してAVD名を確認する。

```text
adb -s <serial> shell getprop ro.boot.qemu.avd_name
adb -s <serial> shell getprop sys.boot_completed
```

5. 対象AVDが一台だけ確定した後に、APKの存在、拡張子、サイズ、ビルドログの成功マーカーを確認する。

## 検証の流れ

### 1. インストール前

- APKが今回のビルドで生成されたことを確認する。古いAPKを新しい結果として扱わない。
- `adb shell pm path <applicationId>`で既存アプリの有無を確認し、更新インストールか新規インストールかを記録する。
- 端末の日時、ネットワーク接続、Googleアカウント、Health Connectアプリと権限の前提を確認する。

### 2. インストール

既定のショートカットがある場合は、それを使う。手動で行う場合は次の形を守る。

```text
adb -s <serial> install -r <absolute-apk-path>
```

インストール結果の終了コードと表示を保存し、失敗した場合は再試行で隠さず、署名、ABI、SDK、端末状態、既存アプリとの関係を切り分ける。

### 3. 起動とログ

- ランチャーまたは`adb shell monkey`で対象アプリを起動する。パッケージ名はプロジェクト設定から確認し、推測しない。
- 必要な範囲だけログを取得し、健康データやトークンを含む行を保存・共有しない。
- クラッシュ、ANR、権限拒否、Activity再生成、Manifest Merger、JNI例外、ネットワーク失敗を別の事象として記録する。
- Unity Consoleのログだけでなく、Androidの`logcat`、Package Manager、Health Connectの権限画面を相互に照合する。

### 4. 機能確認

Google認証、Health Connect接続、権限許可、実データ取得、一覧表示、原文JSON詳細を別々の確認項目にする。
一つの操作が成功しても、後続の状態を成功扱いにしない。

### 5. 証跡

- APKファイルの絶対パス、SHA-256、Unityバージョン、端末シリアル、AVD名、実行日時を記録する。
- 画面変更やOverlay UIは、UnityのGameビューまたは端末画面を撮影して確認する。
- スクリーンショットには、認証情報、健康データ、端末識別情報が写り込まないようにする。
- 未確認の項目を「実機確認済み」と書かず、「権限設定待ち」「OAuth設定待ち」「端末未接続」など具体的な状態で報告する。

## 失敗の切り分け

- APK生成失敗：Unity、C#、Gradle、署名、パッケージ設定の問題として扱う。
- インストール失敗：APK署名、ABI、SDK、端末状態、既存アプリを確認する。
- 起動直後のクラッシュ：Manifest、Java/Kotlin bridge、ネイティブライブラリ、ABI、初期化順を確認する。
- Google認証失敗：OAuthクライアントID、SHA-1/SHA-256、パッケージ名、ネットワーク、アカウント状態を確認する。
- Health Connect失敗：アプリの存在、SDK/API対応、権限、データ期間、タイムゾーン、ユーザーのデータ有無を分けて確認する。
- 実端末未確認：コードの失敗と断定せず、必要な端末操作や設定を未確認事項として報告する。

## 完了報告

次の4区分で短く報告する。

1. 自動検証で通った項目。
2. APKの静的検査で確認した項目。
3. 端末上で実際に操作して確認した項目。
4. OAuth、OS権限、ネットワーク、Health Connectデータなど残った外部条件。

参照する端末操作は[Android公式adbドキュメント](https://developer.android.com/tools/adb?hl=ja)と、リポジトリの`shortcuts/install-apk-pixel-8a.bat`および`doc/rules/client-android-emulator.md`に合わせる。
