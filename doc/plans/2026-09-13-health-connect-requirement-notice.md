# Health Connectの利用条件を確認して案内する実装計画

状態：実装完了（自動テスト・画面確認・APKビルド・Drive配置済み。Android実環境の動作は未確認）
作成日：2026-09-13
更新日：2026-09-13

アプリ起動時と、アプリから開いた設定画面から戻ったときに、Health Connectで歩数を利用するための状態を確認する。
不足している条件に応じて、ユーザーが次に行う操作を日本語で案内する。
判定とメッセージの生成を通常のC#クラスに置き、当面は既存画面の説明領域に表示する。
通知専用のUIデザインは後で検討する。

## 着手時の実装

- [HealthScreenBootstrap.cs](../../client/Assets/Baryonyx/App/Runtime/HealthScreenBootstrap.cs) はProviderと画面を組み立て、前面・背面への移行をPresenterへ伝えている。Android版には起動時の要件チェックがない。
- [HealthScreenPresenter.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/Presentation/HealthScreenPresenter.cs) は接続操作で利用可否と権限を調べ、接続を試した後の復帰では健康データを読み直す。
- [HealthConnectProvider.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthConnectProvider.cs) と[既存Androidブリッジ](../../client/Assets/Plugins/Android/BaryonyxHealth.androidlib/src/main/kotlin/com/baryonyx/health/HealthBridge.kt) は利用可否、権限、7日分の読み取り、設定画面の起動に対応している。
- 現在の権限確認は対象項目のいずれかの許可を表す。歩数単独の未許可は、読み取り結果の `stepsStatus` で区別している。
- AndroidのSDK拡張バージョンと、端末の歩数センサーの有無は取得していない。

既存の前面復帰時のデータ再取得と、今回追加する要件チェックは目的が異なる。
データ取得前の権限確認・取り消しへの対処は維持し、要件チェックをすべての前面復帰に追加しない。

## チェックするタイミング

| 契機 | 要件チェックの扱い |
|---|---|
| アプリ起動 | 起動処理から1回実行する。Google接続を前提にしない。 |
| アプリから開いたHealth Connect・端末設定・インストール先からの復帰 | 前面へ戻ってから1回再実行し、案内を更新する。 |
| 通常のアプリ切り替え、画面ロックからの復帰 | 今回の要件チェックは追加実行しない。既存の読み取り処理は維持する。 |
| Googleの認証画面からの復帰 | 要件チェックを開始しない。 |
| 権限ダイアログの完了、接続・歩数更新 | 既存処理で得た権限・歩数の結果を案内へ反映する。要件一式を重ねて取得しない。 |
| アプリを終了して再起動 | 新しい起動として再確認する。 |

設定画面を開く操作の直前に「設定からの復帰待ち」を記録し、起動失敗時は解除する。
その後の背面移行と前面復帰を確認してからフラグを消費する。
`OnApplicationPause` と `OnApplicationFocus` が両方届いても、復帰1回につきチェックは1回にまとめる。
通常のフォーカス取得だけでは、設定から戻ったと判定しない。

実行中に再確認が必要になった場合は、必要な1回だけを予約する。
画面破棄・キャンセル後の結果や、古いチェックの結果で最新の案内を上書きしない。

## 確認する情報と取得方法

| 情報 | 取得方法 | 判定上の注意 |
|---|---|---|
| Health Connectの利用可否 | 既存の `HealthConnectClient.getSdkStatus()` | 利用不可、インストール・更新が必要、利用可能を区別する。 |
| Androidバージョン | C#から `Build.VERSION.SDK_INT` を読む | 自動計測の条件はAPI 34以上。アプリ全体の最低OSとは分ける。 |
| SDK拡張バージョン | API 34以上で `SdkExtensions.getExtensionVersion(34)` | 20以上かを判定する。古いOSではこの呼び出しを行わない。更新日付で代用しない。 |
| 歩数センサーの有無 | C#から `PackageManager.hasSystemFeature(FEATURE_SENSOR_STEP_COUNTER)` | センサーを使って計測する処理は追加しない。取得失敗を「センサーなし」と扱わない。 |
| このアプリの歩数読み取り権限 | `getGrantedPermissions()` 内の `READ_STEPS` | ほかの健康データの許可や、他アプリの許可で代用しない。 |
| 利用できる歩数データの有無 | 許可済みの場合に限り、直近7暦日の歩数を集計 | 取得元を限定しない。値が0でも記録があれば「データあり」。取得失敗と空の結果を分ける。 |

自動計測のプラットフォーム条件はAndroid 14以上とSDK拡張20以上である。
Health Connectは端末の歩数センサーを使い、歩数の読み取りを許可したアプリがあると記録を開始する。[Androidの歩数記録仕様](https://developer.android.com/health-and-fitness/health-connect/features/steps?hl=ja)

Android APIの呼び出しはUnityの `AndroidJavaClass` / `AndroidJavaObject` で行える。
センサーの搭載確認にはAndroidの端末機能APIを使う。[UnityのAndroid呼び出し](https://docs.unity3d.com/6000.0/Documentation/Manual/android-plugins-java-code-from-c-sharp.html)・[歩数センサーの機能定数](https://developer.android.com/reference/android/content/pm/PackageManager#FEATURE_SENSOR_STEP_COUNTER)

判定に必要な値には「不明」「未確認」を残す。
Health Connect側の端末計測スイッチや、外部サービス側の同期設定を、OSバージョン・権限・アプリのインストール有無から推測しない。
直近7日分の記録が見つかっても、現在も同期が続いていることまでは保証しない。

## 判定と案内メッセージ

Health Connectが利用でき、このアプリに歩数の読み取り権限があることを共通条件とする。
歩数の取得元は、端末の自動計測と、Health Connectへ歩数を書き込む外部アプリ・機器の両方を認める。
自動計測の条件を満たさないことだけを理由に、アプリの利用を禁止しない。

上から順に、最初に該当したものを主な案内とする。
権限不足と自動計測の条件不足が重なった場合は、先に歩数の許可を案内し、許可結果を受けて次の案内へ更新する。
判定コードはユーザー向けの本文には表示しない。

| 判定コード | 条件 | メッセージ |
|---|---|---|
| `HealthUnavailable` | Health Connectを利用できない | 「この端末ではHealth Connectを利用できません。端末の対応状況と、Health Connectが有効になっているか確認してください。」 |
| `HealthUpdateRequired` | Health Connectのインストール・更新が必要 | 「Health Connectのインストールまたは更新が必要です。設定を開いて確認してください。」 |
| `CheckFailed` | 利用可否または権限を確認できない | 「歩数の利用条件を確認できませんでした。時間を置いて、アプリを起動し直してください。」 |
| `StepsPermissionRequired` | このアプリの歩数読み取りが未許可 | 「歩数の読み取りが許可されていません。Health Connectの設定で、このアプリの『歩数』へのアクセスを許可してください。」 |
| `Ready` | 歩数の集計値を取得できた | 要件不足の案内は表示しない。端末の自動計測が未対応でも、取得できた歩数を利用できる。 |
| `CheckFailed` | 許可済みだが歩数の取得に失敗、または必要な端末情報が不明 | 「歩数の利用条件を確認できませんでした。時間を置いて、アプリを起動し直してください。」 |
| `StepSensorUnavailable` | 歩数データがなく、端末に歩数センサーがない | 「この端末では歩数の自動計測を利用できません。Health Connectへ歩数を送れるアプリや機器を連携してください。」 |
| `AndroidVersionUnsupportedForCounting` | 歩数データがなく、Android 14未満 | 「このAndroidバージョンではHealth Connectの自動計測を利用できません。対応する記録アプリから歩数を連携してください。端末が対応している場合はAndroidの更新も利用できます。」 |
| `SystemUpdateRequiredForCounting` | 歩数データがなく、Android 14以上・SDK拡張20未満 | 「端末の自動計測を利用するには、設定から『Google Playシステムアップデート』を確認してください。対応する別アプリから歩数を連携する方法も利用できます。」 |
| `StepsDataPending` | 自動計測の対応条件を満たすが、歩数データがない | 「歩数データがまだありません。歩いたあとに時間を置いて更新してください。記録されない場合は、Health Connectの歩数記録や連携元の設定を確認してください。」 |

`StepsDataPending` は要件違反のエラーにしない。
記録開始直後・同期待ち・未歩行・設定オフなどを、空の結果だけで区別できないためである。
手動更新や接続処理で歩数を取得できた場合は、保持している案内を解除する。
Google Playシステム更新を案内するときも、更新が必ず提供される、更新だけで必ず解決するとは表示しない。[Googleの更新・歩数設定手順](https://support.google.com/android/answer/16786157?hl=ja)

## C#の責務と表示への受け渡し

機能内の通常のC#クラスとして実装し、画面デザインと判定条件を分離した。

| 対象 | 変更内容 |
|---|---|
| [HealthRequirementCheck.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/Requirements/HealthRequirementCheck.cs) | 確認した事実から判定コードと案内を決定する。UnityやAndroidを直接呼ばず、EditModeで条件を差し替えてテストする。 |
| [HealthRequirementMessage.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/Requirements/HealthRequirementMessage.cs) | 判定コード、日本語本文、案内先を保持する。正常時は案内なしとする。 |
| 既存の `HealthConnectProvider.cs` / `HealthContracts.cs` | 端末情報とHealth Connectの状態を取得し、判定用の値として返す。取得の失敗・未許可・未確認を区別する。 |
| 既存の `HealthScreenPresenter.cs` | チェックの実行、設定からの復帰待ち、重複抑制、キャンセル、案内の保持を担当する。接続や歩数取得で得た結果も案内に反映する。 |
| 既存の `HealthScreenBootstrap.cs` | 起動時のチェックを1回開始する。既存の前面・背面通知を利用する。 |
| 既存の `HealthScreenView.cs` | 判定済みの本文を既存の説明領域へ表示する。本文を画面側で組み立てない。 |

要件の案内は通常の接続・読み取りメッセージと別の値としてPresenterに保持する。
暫定UIでは既存の `Status` を利用し、案内がある場合は接続済みでも表示する。
既存の取得エラーや歩数の未許可が隠れないよう、表示の優先順を定める。
新しいモーダル、通知履歴、OSのプッシュ通知、通知権限の要求は追加しない。

要件照会と設定起動の完了結果は、任意の `IHealthRequirementProvider` として既存のProviderへ追加した。
設定起動に失敗した場合は復帰待ちを解除し、通常のアプリ切り替えを設定からの復帰と誤認しない。
Android・システム更新の案内では「端末の設定を開く」、それ以外では「Health Connectの設定を開く」を表示する。
端末設定は一般の設定画面を開く。メーカー固有の更新画面へは直接遷移せず、本文の更新名から探せるようにする。

端末のOS・SDK拡張・センサー情報はC#から取得する。
Health Connectの非同期処理は既存ブリッジを再利用し、歩数単独の許可と歩数の集計結果だけを返す最小の照会を追加する。
現在の公開インターフェースでは、歩数単独の許可を得るために追加15種類を含む7日分の読み取りまで必要になるため、要件確認用の応答を設ける。
これは既存SDK呼び出しの受け渡しを補う変更であり、ネイティブの歩数計処理は自作しない。
照会処理は [HealthRequirements.kt](../../client/Assets/Plugins/Android/BaryonyxHealth.androidlib/src/main/kotlin/com/baryonyx/health/HealthRequirements.kt) に配置した。
新しいプラグインは追加していない。既存ブリッジの採用経緯は[機能文書](../features/health-data.md#プラグインとandroidビルド)を参照する。

Health Connectが利用不可、または歩数が未許可の場合は、歩数を読み取らない。
読み取りは直近7暦日の歩数集計に限定し、血圧・体重などの追加項目を要件確認のために取得しない。
既存の読み取りと重なる場合は直列化し、利用できる結果を共有する。
判定結果・健康データの永続化やサーバー送信は追加しない。

## 実装順と受入条件

1. 判定用の値と案内のC#クラスを作り、条件・優先順をEditModeで確認する。
2. Providerから端末情報、Health Connectの利用可否、歩数単独の許可、必要時の歩数集計を取得する。
3. 起動処理と設定画面への移動・復帰を接続する。チェック自体から権限ダイアログは開かず、ユーザーの接続操作で要求する。
4. 既存の説明領域へ案内を表示し、通常の読み取り成功や権限変更でも案内が更新されるようにする。
5. 関連テストとUnityの画面確認を行い、実装で確定した動作を[機能文書](../features/health-data.md)と[QA](../qa/health-connect-requirements.md)へ反映する。

| 検証 | 受入条件 |
|---|---|
| 条件の境界 | Android 13/14、SDK拡張19/20、センサーあり・なし・不明、Health Connectの各状態で適切な案内になる。 |
| 権限 | 歩数だけ未許可、全項目未許可、確認途中の取り消しを区別する。自動の権限要求や無許可のデータ取得を行わない。 |
| 歩数の取得元 | 自動計測に未対応でも歩数を取得できれば利用できる。アプリのインストールだけでは連携済みとしない。 |
| 記録の状態 | 0歩、欠測、取得失敗、取得未実行を混同しない。過去の記録だけで現在の同期継続を保証しない。 |
| 起動・復帰 | 起動1回、設定からの復帰1回ごとに1回実行する。Pause/Focusの重複、設定を開く失敗、確認中の復帰・破棄でも古い結果が残らない。 |
| 対象外の復帰 | 通常のアプリ切り替え・ロック解除・Google認証からの復帰で要件チェックを追加実行しない。 |
| 案内の更新 | 設定後の再確認、許可後の接続、既存の歩数更新で条件が解消した場合は案内が消える。 |
| 表示 | PlayModeで案内が読め、長文が切れず、既存のボタンとJSON詳細を操作できる。Gameビューを撮影して確認する。 |
| Android | ユーザーが手動起動したエミュレーターでOS情報・権限・設定からの復帰を確認する。センサー搭載や実際の記録継続は、Editorのテスト成功で確認済みとしない。 |

実装時のC#整形、再コンパイル、Console確認、EditMode・PlayModeはリポジトリの既存手順に従う。
APKをビルドする段階では、既定のDrive配置先へのコピーまでを完了条件に含める。

## 実装後の検証記録

2026-09-13に以下を確認した。

- 変更したC# 10ファイルの整形チェックとUnityの再コンパイルが成功し、Consoleのエラーは0件だった。
- EditModeの `Baryonyx.EditModeTests` は84件成功した。判定の境界・優先順に加え、起動時の重複抑制、設定起動の失敗と再試行後のエラー解除、設定起動の完了前に復帰する場合、キャンセル・破棄後の結果の破棄を確認した。
- PlayModeの `HealthScreenScenarioTests` は12件成功した。既存の画面操作に加え、起動時の案内、設定先の切り替え、復帰後の案内更新、接続済みで歩数が空の場合の長文表示とJSON操作を確認した。
- AndroidブリッジのJVMテストは13件成功した。今回追加した4件では、歩数の未許可時に読まないこと、0歩と空の区別、確認途中の許可取り消し、取得失敗とキャンセルを確認した。
- UnityのGameビューを1080×2400で撮影し、システム更新の案内とAndroid 13相当の長文案内が切れず、設定ボタンと一覧を操作できることを確認した。
- Unity 6000.6.0f1の `AndroidBuild.Build` で、IL2CPP・ARM64のAPKをビルドした。出力は `client/Builds/Android/baryonyx.apk`、サイズは115,144,425バイトだった。
- 最終APKを `G:\マイドライブ\71_プロジェクト\baryonyx\baryonyx.apk` へ上書きコピーし、コピー元・配置先のSHA-256が一致した。Google Drive for desktopによるクラウドへの同期完了は未確認である。

Androidの実環境でのOS・SDK拡張・センサー情報取得、権限の操作、設定画面への遷移と復帰、実際の歩数記録は未確認である。
Editorのテスト成功とAPKビルド成功だけでは、これらを確認済みとしない。
