---
name: unity-android-health-connect
description: Unity Androidプロジェクトで、Android Java/Kotlin bridge、AndroidManifest、Gradle、Google認証、Health Connectの権限とデータ取得を実装・レビュー・検証するときに使う。Unityの一般的なUI実装や単なるAPKビルドには使わない。
---

# Unity AndroidとHealth Connectの連携

AndroidのOS機能とUnityのC#コードの境界を、実端末で検証できる形に保つ。
Health Connectの仕様、Google認証、OS権限、Unity画面の状態を別々の責務として扱い、どれか一つが成功しただけで全体が完了したと判断しない。

## 発動したら最初に確認すること

1. ルートと`client/`の`AGENTS.md`、`client/ProjectSettings/ProjectVersion.txt`、`client/Packages/manifest.json`、`client/Packages/packages-lock.json`を読む。
2. `client/Assets/Plugins/Android`、対象Feature、既存のProvider、Manifest、Gradleテンプレート、関連テストを調べる。
3. Unityの実バージョン、対象Android API、minSdk・targetSdk、IL2CPP/ARM64、アプリケーションID、現在の権限宣言を実ファイルから確認する。記憶や古いサンプルから版数を推測しない。
4. 既存の無料Unityパッケージで要件を満たせるか確認する。自作のJava/Kotlinを追加する場合は、既存プラグインで満たせない要件と、その理由を変更説明に残す。
5. OAuthのクライアントID、APIキー、リフレッシュトークン、CloudflareやGoogleの秘密値をAsset、Manifest、ログ、コミットへ入れない。

## 状態と責務の境界

- Google認証の完了とHealth Connectの接続・権限許可を別の状態として設計する。
- C#側は画面状態、キャンセル、再試行、古い非同期応答の破棄を管理し、Android bridgeはOS APIの呼び出しと結果の変換に限定する。
- Health Connectの値を表示する場合は、取得できない状態、値が0の状態、権限がない状態、ユーザーがキャンセルした状態を区別する。
- JSONを受け取る機能では、表示用に変換した値だけでなく、要求された原文JSONを詳細画面へ渡せるようにする。
- 既存の画面仕様がローカル表示だけを対象にしている場合、サーバー同期、PlayerPrefs、データベース永続化を追加しない。
- Android固有コードからUnityのViewやPresenterを直接操作せず、既存のProviderインターフェースまたは明示的な結果型を経由する。

## 実装時の確認点

### Android bridge

- Java/Kotlinの公開メソッド、JNIの引数型、スレッド、ActivityまたはContextの取得元、nullと例外の扱いを確認する。
- AndroidManifestの権限、provider、intent-filter、`tools:node`、Gradle依存関係がUnityのManifest Merger後に残ることを確認する。
- Android APIレベルやHealth Connectアプリの対応状況を公式資料で再確認し、存在しないAPIや古いクラス名を生成しない。
- 非同期結果をUnityのメインスレッドへ戻す方法と、Activityが再生成された場合の再接続を確認する。
- センサー値や健康データをログへ出す場合は、個人データを省略またはマスクする。

### Health Connect

- 読み取り・書き込み権限を最小限にし、許可要求の前に現在の権限を確認する。
- 歩数集計では、対象期間、タイムゾーン、集計方法、値が存在しない場合の意味を明記する。
- 既存のbaryonyx実装では、七日分の同一タイムゾーンの日付、`hasValue`、整数値、`StepsRecord.COUNT_TOTAL`の扱いを壊さない。
- OAuthの成功、Health Connect権限の成功、実データ取得の成功を別々に記録する。
- Android端末で未確認の値を、EditModeテストやAPK生成だけで実証済みと報告しない。

## 検証

1. EditModeでJSON解析、日付境界、権限結果の変換、キャンセル、再試行、古い応答の破棄をFake Providerで検証する。
2. PlayModeで起動画面、認証から権限要求までの状態遷移、エラー表示、再入場を検証する。
3. Unityの対象バージョンでIL2CPP・ARM64 APKを生成し、Manifestと依存ライブラリを読み取り専用で確認する。
4. 実端末またはユーザーが起動した指定AVDで、Google認証、Health Connect権限ダイアログ、実データ、原文JSONを個別に確認する。エミュレーターはAIが起動しない。
5. 検証報告を「自動テスト」「APK静的検査」「実端末確認」「OAuth・外部設定未確認」に分ける。

## 参照

- [Android Health Connectの歩数](https://developer.android.com/health-and-fitness/health-connect/features/steps?hl=ja)
- [Health Connectの対応環境](https://developer.android.com/health-and-fitness/health-connect/availability?hl=ja)
- [Android権限の概要](https://developer.android.com/guide/topics/permissions/overview?hl=ja)
- [Unity Androidプラットフォーム](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/android)

実装前後で公式資料の対象Unity・Android APIバージョンを確認し、古いブログ記事や検索結果だけで仕様を断定しない。
