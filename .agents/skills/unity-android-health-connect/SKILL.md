---
name: unity-android-health-connect
description: Unity Androidプロジェクトで、Health Connectの権限と歩数の取得、Android Java/Kotlin bridge、AndroidManifest、Gradle、端末側の状態とC#の境界を実装・レビュー・検証するときに使う。歩数のサーバー同期の流れやGoogle接続（任意）に触れる変更も含む。Unityの一般的なUI実装や単なるAPKビルドには使わない。
---

# Unity AndroidとHealth Connectの連携

AndroidのOS機能とUnityのC#コードの境界を、実端末で検証できる形に保つ。
Health Connectの権限、歩数の取得、サーバーへの同期、アカウント（ゲストのセッション、任意のGoogle接続）、Unity画面の状態を別々の責務として扱い、どれか一つが成功しただけで全体が完了したと判断しない。

## 発動したら最初に確認すること

1. ルートと `client/` の `AGENTS.md`、[健康データの機能文書](../../../doc/features/health-data.md)、[起動時の連携と歩数の同期](../../../doc/features/startup-sync.md)、[運動データとルーン換算](../../../doc/features/exercise-rewards.md)、`client/ProjectSettings/ProjectVersion.txt`、`client/Packages/manifest.json`、`client/Packages/packages-lock.json` を読む。今の実装状況（使っている画面、要求する権限、宣言している権限、同期の流れ）は機能文書の現在の実装状況で確かめる。
2. `client/Assets/Plugins/Android`、対象Feature、既存のProvider、Manifest、Gradleテンプレート、関連テストを調べる。
3. Unityの実バージョン、対象Android API、minSdk・targetSdk、IL2CPP/ARM64、アプリケーションID、現在の権限宣言を実ファイルから確認する。記憶や古いサンプルから版数を推測しない。
4. 新しい端末機能は、ルートの AGENTS.md の[作業の基本](../../../AGENTS.md#作業の基本)に従い、無料の既存プラグインで満たせるかを先に確かめる。自作のJava/Kotlinを追加する場合は、既存プラグインで満たせない要件と、その理由を変更説明に残す。
5. 秘密値（OAuthのクライアントシークレット、APIキー、トークン、ゲストの秘密値）は、Asset、Manifest、ログ、コミットへ入れない（ルートの [AGENTS.md](../../../AGENTS.md#作業の基本)）。

## 状態と責務の境界

- ゲストのセッション、Health Connectの接続と権限、歩数の取得、サーバーへの同期を別の状態として設計する。Google接続は任意の別機能であり、Health Connectの状態を変えない。
- C#側は画面状態、キャンセル、再試行、古い非同期応答の破棄を管理し、Android bridgeはOS APIの呼び出しと結果の変換に限定する。
- Health Connectの値を表示・送信する場合は、取得できない状態、値が0の状態、権限がない状態、ユーザーがキャンセルした状態を区別する。
- 取得したデータをどこに保存するか（サーバー、端末、保存しない）は [data-storage](../data-storage/SKILL.md) で決め、仕様にない保存先を足さない。
- Android固有コードからUnityのViewやPresenterを直接操作せず、既存のProviderインターフェースまたは明示的な結果型を経由する。

## 実装時の確認点

### Android bridge

- Java/Kotlinの公開メソッド、JNIの引数型、スレッド、ActivityまたはContextの取得元、nullと例外の扱いを確認する。
- AndroidManifestの権限、provider、intent-filter、`tools:node`、Gradle依存関係がUnityのManifest Merger後に残ることを確認する。
- Android APIレベルやHealth Connectアプリの対応状況を公式資料で再確認し、存在しないAPIや古いクラス名を生成しない。
- 非同期結果をUnityのメインスレッドへ戻す方法と、Activityが再生成された場合の再接続を確認する。
- センサー値や健康データをログへ出す場合は、個人データを省略またはマスクする。

### Health Connect

- 読み取り権限は、ゲームが使う運動データ（機能文書で決めた種類）だけを要求し、許可要求の前に現在の権限を確認する。宣言している権限と要求する権限がずれている場合は、機能文書の記載と照らして報告する。
- 歩数の対象期間、タイムゾーン、集計方法、値が存在しない場合の意味は、機能文書の現行仕様に合わせる。
- Health Connect権限の成功、実データの取得の成功、サーバーへの同期の成功を別々に記録する。
- Android端末で未確認の値を、EditModeテストやAPK生成だけで実証済みと報告しない。

## 検証

1. EditModeで、日付境界、権限結果の変換、キャンセル、再試行、古い応答の破棄、同期の失敗の扱いをFake Providerで検証する。
2. PlayModeで、起動画面の連携モーダル、許可から同期までの状態遷移、エラー表示と再試行、再入場を検証する。Health Connectとゲームサーバーは端末内の代役へ差し替える（[UnityのテストとCI](../../../doc/rules/client-testing.md)）。
3. Unityの対象バージョンでIL2CPP・ARM64 APKを生成し、Manifestと依存ライブラリを読み取り専用で確認する。
4. 実端末またはユーザーが起動した指定AVDで、[unity-android-device-validation](../unity-android-device-validation/SKILL.md) の手順に従って確認する。
5. 検証報告は、unity-android-device-validation の「完了報告」の4区分に分ける。

## 参照

- [Android Health Connectの歩数](https://developer.android.com/health-and-fitness/health-connect/features/steps?hl=ja)
- [Health Connectの対応環境](https://developer.android.com/health-and-fitness/health-connect/availability?hl=ja)
- [Android権限の概要](https://developer.android.com/guide/topics/permissions/overview?hl=ja)
- [Unity Androidプラットフォーム](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/android)

実装前後で公式資料の対象Unity・Android APIバージョンを確認し、古いブログ記事や検索結果だけで仕様を断定しない。
