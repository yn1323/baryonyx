---
name: unity-android-device-validation
description: Unity AndroidのAPKを、ユーザーが起動した実機または指定AVDで検証するときに使う。adbでの端末の特定、インストール、起動とログ、Health Connectの歩数の権限と取得、サーバーへの同期、画面証跡を扱い、検証結果を4区分で報告する。エミュレーターの起動や単なるEditorテストには使わない。
---

# Unity Android実機・AVD検証

APKが作成できたことと、Android端末で機能が動くことを分けて確認する。
端末を自動起動せず、接続先を一意に特定し、再現可能なログと画面証跡を残す。
このスキルの「完了報告」の4区分は、[unity-android-health-connect](../unity-android-health-connect/SKILL.md) と [unity-android-ci-distribution](../unity-android-ci-distribution/SKILL.md) が端末で確かめたときの報告にも使う。

## 守ること

- エミュレーターはユーザーが起動する（ルートの AGENTS.md の[デバッグ環境](../../../AGENTS.md#デバッグ環境)）。AIは起動、再起動、スナップショット復元を行わない。ログの取得や端末の操作が必要なら、ユーザーに依頼する。
- 対象端末、AVD、ABIは[WindowsでのUnityとAndroidエミュレーター](../../../doc/rules/client-android-emulator.md)、インストール用ショートカットの動作は[手動実行用ショートカットの動作](../../../doc/rules/local-shortcuts.md)に従う。別の端末へ黙って切り替えない。
- 起動中の端末が0台、対象AVDが0台、対象AVDが複数台、または `sys.boot_completed` が未完了なら、インストールや検証を進めず理由を報告する。
- `adb install -r` を使い、ユーザーのアプリデータを消去しない。データ消去やアプリ設定の初期化が必要な場合は、実行前に目的と影響を明示する。ゲストの秘密値は端末にだけあり、データを消すとそのゲストのデータへ戻れない（[起動時の連携と歩数の同期](../../../doc/features/startup-sync.md)）。
- 健康データ、セッションやOAuthのトークン、ゲストの秘密値、個人情報をログやスクリーンショットへ残さない。必要な場合はマスクする。

## 発動したら最初に確認すること

1. ルートと `client/` の `AGENTS.md`、[Androidビルドと実機確認](../../../doc/rules/client-android-testing.md)、[エミュレーター手順](../../../doc/rules/client-android-emulator.md)、APKのパス、`ProjectVersion.txt`、アプリケーションID、対象ビルドのコミットまたは作成時刻、APKの接続先の環境（ビルドログの `BARYONYX_ANDROID_SERVER:` の行）を確認する。
2. [手動実行用ショートカットの動作](../../../doc/rules/local-shortcuts.md)と同じ順でAndroid SDKを探し、`platform-tools` の `adb` を使う。
3. `adb devices` で接続一覧を取得し、`device` 状態のエミュレーターだけを候補にする。
4. 各候補に次を実行してAVD名を確認する。

```text
adb -s <serial> shell getprop ro.boot.qemu.avd_name
adb -s <serial> shell getprop sys.boot_completed
```

5. 対象AVDが一台だけ確定した後に、APKの存在、拡張子、サイズ、ビルドログの成功マーカーを確認する。

## 検証の流れ

### 1. インストール前

- APKが今回のビルドで生成されたことを確認する。古いAPKを新しい結果として扱わない。
- `adb shell pm path <applicationId>` で既存アプリの有無を確認し、更新インストールか新規インストールかを記録する。
- 端末の日時、ネットワーク接続、Health Connectアプリと権限、歩数データの有無の前提を確認する。接続先がLocalのサーバーなら、`server/` の `pnpm dev` の起動をユーザーに依頼する（ルートの [AGENTS.md](../../../AGENTS.md)）。

### 2. インストール

既定のショートカットがある場合は、それを使う。手動で行う場合は次の形を守る。

```text
adb -s <serial> install -r <absolute-apk-path>
```

インストール結果の終了コードと表示を保存し、失敗した場合は再試行で隠さず、署名、ABI、SDK、端末状態、既存アプリとの関係を切り分ける。

### 3. 起動とログ

- ランチャーまたは `adb shell monkey` で対象アプリを起動する。パッケージ名はプロジェクト設定から確認し、推測しない。
- 必要な範囲だけログを取得し、健康データやトークンを含む行を保存・共有しない。
- クラッシュ、ANR、権限拒否、Activity再生成、Manifest Merger、JNI例外、ネットワーク失敗を別の事象として記録する。
- Unity Consoleのログだけでなく、Androidの `logcat`、Package Manager、Health Connectの権限画面を相互に照合する。

### 4. 機能確認

現在の起動時の流れと画面は[起動時の連携と歩数の同期](../../../doc/features/startup-sync.md)を正本とし、次を別々の確認項目にする。
一つの操作が成功しても、後続の状態を成功扱いにしない。

- ゲストのセッション確保とサーバーへの接続
- Health Connectの利用可否と、歩数の読み取り権限（連携モーダル、許可画面、「あとで」）
- 直近の歩数の取得と、サーバーへの同期
- 画面の表示（Topの案内の切り替え、HomeのACTとルーン）
- 失敗時の表示と再試行（通信の失敗、読み取りの失敗）

Google接続は任意の別機能であり、確かめる依頼があるときだけ、別の項目として確認する。

### 5. 証跡

- APKファイルの絶対パス、SHA-256、Unityバージョン、接続先の環境、端末シリアル、AVD名、実行日時を記録する。
- 画面変更やOverlay UIは、UnityのGameビューまたは端末画面を撮影して確認する。
- スクリーンショットには、認証情報、健康データ、端末識別情報が写り込まないようにする。
- 未確認の項目を「実機確認済み」と書かず、「権限設定待ち」「サーバー未起動」「端末未接続」など具体的な状態で報告する。

## 失敗の切り分け

- APK生成失敗：Unity、C#、Gradle、署名、パッケージ設定の問題として扱う。
- インストール失敗：APK署名、ABI、SDK、端末状態、既存アプリを確認する。
- 起動直後のクラッシュ：Manifest、Java/Kotlin bridge、ネイティブライブラリ、ABI、初期化順を確認する。
- サーバー接続の失敗：APKの接続先の環境、サーバーの起動状態、端末のネットワーク、HTTPを許す接続先（[ServerApi](../../../client/Assets/Baryonyx/Shared/Networking/ServerApi.cs)）を確認する。
- Health Connect失敗：アプリの存在、SDK/API対応、権限、データ期間、タイムゾーン、ユーザーのデータ有無を分けて確認する。利用者向けの対処は[QA一覧](../../../doc/qa/README.md)のHealth Connectの項目にある。
- Google接続を確かめる場合の失敗：OAuthクライアントID、SHA-1/SHA-256、パッケージ名、ネットワーク、アカウント状態を確認する。
- 実端末未確認：コードの失敗と断定せず、必要な端末操作や設定を未確認事項として報告する。

## 完了報告

次の4区分で短く報告する。

1. 自動検証で通った項目。
2. APKの静的検査で確認した項目。
3. 端末上で実際に操作して確認した項目。
4. OS権限、ネットワーク、サーバー、Health Connectのデータ、OAuthなど、残った外部条件。

端末操作の一般的な使い方は[Android公式adbドキュメント](https://developer.android.com/tools/adb?hl=ja)を参照する。
