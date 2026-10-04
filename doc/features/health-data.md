---
id: feature-health-data
type: specification
status: 運用中
updated: 2026-10-05
---

# 健康データの読み取りと保存

[機能索引](README.md) / 関連：[起動時の連携と歩数の同期](startup-sync.md)・[運動データとルーン換算](exercise-rewards.md)

Android版はHealth Connectから当日を含む直近7暦日の歩数を読み、ゲームサーバーへ保存する。
いつ読み、どう案内するか（Topの起動処理、連携モーダル、HomeのACTパネル）は[起動時の連携と歩数の同期](startup-sync.md)を正本とする。
この文書は、取得・保存・再送の決まり、サーバーのAPI、Google認証の設定、プラグインとAndroidビルドを扱う。
Google接続は任意の独立した操作とし、健康データの読み取りには要求しない。
バックグラウンド同期、Health Connectへの書き込み、iOSの実装は対象外である。

利用時に歩数が表示されない場合は、[Health Connectで歩数が「データなし」になるときの対処法](../qa/health-connect-no-steps.md)を参照する。

## 現在の実装状況

2026-09-24に、健康データを表示していた `Main`・`Wireframe` シーン、歩数画面のUI（View・Prefab）、冒険の試作画面を削除した。
現在の起動経路では、TopとHomeがゲストのセッションで直近7日分の歩数をサーバーへ同期し、Homeに今日の歩数をACTで表示する。
日別一覧・JSON詳細・Google接続の画面はなく、Google認証と、削除した画面が使っていた運動報酬の請求の経路は呼ばれていない。
削除した画面の構成・文言・寸法と検証記録は[削除した歩数画面の記録](../archive/health-data-local-screen.md)に移した。
それらを新しい画面から使うときは、[HealthRuntime](../../client/Assets/Baryonyx/App/Runtime/HealthRuntime.cs) でProviderの選択とPresenterの生成を行い、画面側でPresenterの状態を描画する。

2026-09-27の企画見直しで、ゲームが扱う運動データを歩数だけにし、Health Connectの権限も歩数の読み取りだけとした（[運動データとルーン換算](exercise-rewards.md)）。
起動時の同期は歩数だけを要求して読むが、[Androidライブラリのマニフェスト](../../client/Assets/Plugins/Android/BaryonyxHealth.androidlib/AndroidManifest.xml)は16種類（歩数、体重、体脂肪率、身長、血圧、心拍、安静時心拍、酸素飽和度、呼吸数、体温、血糖値、睡眠、距離、活動時消費カロリー、総消費カロリー、運動記録）の読み取り権限を宣言したままである。
宣言と追加項目の読み取り処理を減らす作業は未着手である。

## Editor・PCでのプレビュー

EditorとAndroid以外の実行環境では、OAuth設定やAndroid端末の接続なしで、7日分の架空の歩数を返すプレビューのProvider（[HealthScreenPreviewProvider](../../client/Assets/Baryonyx/Features/Health/Runtime/Preview/HealthScreenPreviewProvider.cs)）を使う。
日付は日本時間の今日から過去7日分を生成し、通常の歩数、「データなし」、測定値の0を含める。
切り替えはUSB接続の有無ではなく、[GameServices](../../client/Assets/Baryonyx/App/Runtime/GameServices.cs) の `UNITY_ANDROID && !UNITY_EDITOR` で決める。
Androidをビルド対象にしたEditorやDevice Simulatorもプレビューになり、Androidプレイヤーだけが実Providerを使う。
プレビューのProviderはAndroidプレイヤーのコンパイルから除外する。
未連携の状態から始める設定は[実行環境と接続先](startup-sync.md#実行環境と接続先)を参照する。

## 実装の入口

| 場所 | 処理 |
|---|---|
| [HealthContracts.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthContracts.cs) | Provider、権限・取得結果の共通型 |
| [HealthServerSync.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/Sync/HealthServerSync.cs) | 歩数の保存とルーン請求の実行順と、ユーザーごとの取得元ID |
| [AccountSessionRunner.cs](../../client/Assets/Baryonyx/Features/Account/Runtime/AccountSessionRunner.cs) | ゲストのログイン、期限の5分前の取り直し、401のときの1回だけのやり直し。全機能で1つを共有する |
| [HealthApiClient.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/Sync/HealthApiClient.cs) | 歩数の保存要求 |
| [AccountApiClient.cs](../../client/Assets/Baryonyx/Features/Account/Runtime/AccountApiClient.cs)・[ExerciseRewardsApiClient.cs](../../client/Assets/Baryonyx/Features/ExerciseRewards/Runtime/ExerciseRewardsApiClient.cs) | サーバーへのログイン・ログアウト、ルーン請求・履歴・残高の要求 |
| [ServerApi.cs](../../client/Assets/Baryonyx/Shared/Networking/ServerApi.cs) | サーバーURLの検証とHTTP送信 |
| [HealthConnectProvider.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthConnectProvider.cs) | UnityからAndroidへの呼び出し |
| [Android連携コード](../../client/Assets/Plugins/Android/BaryonyxHealth.androidlib/src/main/kotlin/com/baryonyx/health/HealthBridge.kt) | Health Connectの権限確認と日別集計 |
| [サーバールート](../../server/src/features/health/routes.ts)・[入力検証](../../server/src/features/health/schema.ts) | HTTP要求の検証と応答、単日と週全体の制約 |
| [認証処理](../../server/src/features/accounts/auth.ts)・[ログインAPI](../../server/src/features/accounts/routes.ts)・[セッション確認](../../server/src/features/accounts/session.ts) | Googleの本人確認、セッション発行、トークンのハッシュ化と確認 |
| [DB操作](../../server/src/features/health/repository.ts) | 取得元の所有者確認、日別歩数の保存・取得 |
| [DBスキーマ](../../server/src/features/health/db-schema.ts)・[ユーザーとセッション](../../server/src/features/accounts/db-schema.ts)・[初期マイグレーション](../../server/migrations/0000_initial.sql) | ユーザー、セッション、取得元、日別歩数と過去値の取得日時 |

HTTPのパスと入出力はサーバールートと入力検証コードを正とする。
サーバーの入力検証にはZodを使い、ログイン・同期要求と日別歩数の制約をスキーマで定義する。
スキーマの配置と検証方法は [バックエンドの入力検証](../rules/backend-design.md#zodによる入力検証) に従う。
認証以外の機能で必要になるまでは、健康データ機能の外へ認証の共通基盤を広げない。

## 利用に必要な設定

サーバーには `GOOGLE_CLIENT_ID` を設定する。
GoogleのAndroidクライアント設定に登録するアプリID・署名証明書と、IDトークンの宛先となるWebクライアントIDを用意する。
Unityの `Initialize` に渡すGoogleクライアントIDと、サーバーの `GOOGLE_CLIENT_ID` は同じWebクライアントIDにする。
設定がないサーバーはログインを503で拒否する。
ユーザーから受け取っていない実際のID・URLをコードへ埋め込んでいない。

ローカルのサーバーでは `server/.dev.vars` を作成し、`GOOGLE_CLIENT_ID` にGoogle OAuthのWebクライアントIDを設定する。
D1マイグレーションを適用してから起動する。
具体的な起動・環境別の公開手順は [バックエンドの開発環境](../rules/backend-design.md) に従う。
この機能の作業でリモートDBや公開環境は変更していない。

Unityからの接続先はHTTPSを使用する。
開発時の例外はループバックHTTPのみとし、AndroidのUSB接続でポートを転送する場合もループバックを使う。
認証トークンを送る接続で証明書検証を無効にしない。
Androidのパッケージ名は、ローカル・CIともに `com.croissantlab.baryonyx` を使う。
Google OAuthのAndroidクライアントには、このパッケージ名と実機へインストールするAPKの署名証明書のSHA-1を登録する。
ローカルとCIで署名証明書が異なる場合は、それぞれの組み合わせを登録する。

## Google認証の設定

1. 同じGoogle Cloudプロジェクトで、Googleログイン用のWebクライアントと、実際のアプリID・署名証明書に対応するAndroidクライアントを用意する。
2. [HealthConnectionSettings](../../client/Assets/Baryonyx/Features/Health/Data/HealthConnectionSettings.asset) の `Google Web Client Id` に、末尾が `.apps.googleusercontent.com` のWebクライアントIDを設定する。クライアントシークレットはアプリに入れない。
3. 接続先のサーバーは環境の名前で選ぶ（[実行環境と接続先](startup-sync.md#実行環境と接続先)）。
4. Androidビルドを端末にインストールし、Google認証と、Google未接続でのHealth Connect接続・歩数の読み取りをそれぞれ確認する。

設定アセットにはWebクライアントIDが保存されている。
Google Cloud側の登録内容とAndroidのアプリID・署名との整合、実機での認証成功は未確認である。
WebクライアントIDが空のときは設定不足として扱い、認証に成功した扱いにはしない。
EditorではネイティブSDKを作らない。
UMoth 1.0.3はキャンセルと一部の失敗を同じエラーとして返す。

CIの `UNITY_LICENSE` などはUnityの実行用、`GOOGLE_DRIVE_CLIENT_ID` はAPKの配布用であり、今回のログイン設定とは用途が異なる。
サーバー公開設定の `GOOGLE_CLIENT_ID` は将来IDトークンを検証するときに、このWebクライアントIDと一致させる。
現在のAndroidビルド補助は `com.croissantlab.baryonyx` とデバッグ署名を使うため、実際にインストールするAPKのアプリID・署名をOAuth登録時に確認する。
WebクライアントIDやAndroidクライアントの作成は今回行っていない。[Googleの設定手順](https://developer.android.com/identity/sign-in/credential-manager-siwg)

## サーバー同期の経路

歩数の保存とルーンの請求は [HealthServerSync](../../client/Assets/Baryonyx/Features/Health/Runtime/Sync/HealthServerSync.cs) にまとめている。
ゲストのセッション（ログイン、期限の5分前の取り直し、401のときの1回だけのやり直し）は [AccountSessionRunner](../../client/Assets/Baryonyx/Features/Account/Runtime/AccountSessionRunner.cs) が受け持ち、歩数・パーティ・装備・ボーナス・冒険の全機能で1つを共有する。
[GameServices](../../client/Assets/Baryonyx/App/Runtime/GameServices.cs) が選んだ環境のURLからこれらを作り、Topの起動処理とHomeのACTパネルが [HealthStepLink](../../client/Assets/Baryonyx/Features/Health/Runtime/Link/HealthStepLink.cs) を通して使う。
ゲストのセッションで7日分を保存し、HomeのACTパネルでは続けてルーンを請求する（[起動時の連携と歩数の同期](startup-sync.md)）。
選んだ環境のURLが空なら、歩数を端末のメモリだけに保持する。

削除した歩数画面が使っていた `HealthRuntime`・`HealthScreenPresenter` の経路（Googleのサインイン後にサーバーへログインし、7日分を保存してからルーンを請求する）はコードに残っているが、どの画面からも呼ばない。
この経路の操作と同期は、`HealthScreenOperations` の世代管理で多重実行と遅延結果を防ぐ。
以前の `HealthClient` と `HealthSyncService` は起動シーンから使われていなかったため、2026-09-23に削除した。

EditorではOSの認証・健康データ取得を実行しない。
Editorの自動テストはテスト用Providerを明示的に使う。

## 権限と同期の動作

Androidは読み取り直前に歩数の権限を確認し、取得中に許可が取り消された場合も送信を止める。
拒否されたときに権限要求を自動で繰り返さない。
明示的な連携操作の後は、設定から戻った際に権限を確認できるため、取り消し・再許可後も再起動を必須にしない。

HealthKitへの拡張に備え、共通型には権限の `Unknown` を用意している。
iOSでは読み取り権限の拒否を判定できないため、将来のProviderは空の取得結果を拒否や0歩と断定しない。[Appleの認可状態API](https://developer.apple.com/documentation/healthkit/hkhealthstore/authorizationstatus%28for%3A%29)

アプリが前面にあり、ログインと明示的な健康データ連携が済んでいる場合に同期する。
通常の背面移行では読み取り・送信を中断し、終了後の定期処理や永続的な送信待ちキューは持たない。
OSの認証・権限画面の結果は待つが、前面へ戻るまで健康データ同期は続行しない。
すでにサーバーへ届いた要求を、端末のキャンセルで取り消せるとは扱わない。

Googleログインのセッションは1時間で期限切れになる。
トークンはクライアントのメモリだけに保持し、サーバーにはハッシュを保存する。
アプリ再起動・期限切れ後は再認証する。
ログアウトは端末の認証状態と未送信データを先に破棄し、通信できる場合はサーバーのセッションも失効する。
オフラインで失効できなかったセッションは有効期限まで残る。
許可の取り消しやログアウトで、サーバーの保存済み履歴は削除しない。

## 保存と再送

歩数はHealth Connectの集計APIを使い、取得元アプリの生レコードを単純に足し合わせない。[Androidの読み取りガイド](https://developer.android.com/health-and-fitness/health-connect/read-data)
端末のタイムゾーンにおける日付境界で取得し、サーバーへUTC区間・タイムゾーン・取得日時を送る。
サーバーはユーザー・Provider・端末側の取得元IDを区別する。
取得元IDはアプリインストール内でもユーザーごとに分ける。

同期開始時にサーバーが更新版を発行し、その版に対応する7日分をまとめて保存する。
同じ日付・タイムゾーンの値は置き換え、加算しない。
新しい同期の開始後に古い版が届いた場合や、保存済みの版を再送した場合は409になる。
通信失敗・競合後は次の前面同期で新しい版を取得し、7日分を読み直す。

値が得られない場合は `hasValue=false` とし、現在値を空にする。
以前の値があれば、最後に値を得た日時とともに `lastKnownSteps` として残す。
履歴がない場合と過去の0歩を区別するため、取得APIには `hasLastKnownValue` も含める。
複数端末やAndroid・iOSの値は取得元ごとに取得し、自動合算しない。
取得APIは指定した取得元の新しい日付から最大100件を返す。
7日より前の元データ修正や、長期間起動していなかった間の全履歴回収は対象外とする。

## プラグインとAndroidビルド

認証にはApache-2.0の [UMoth](https://github.com/Uralstech/UMoth) を採用した。
UMoth、Utils.Singleton、Utils.Loggers、EDM4Uは [manifest.json](../../client/Packages/manifest.json) のGitコミットで固定している。
UMothのiOS実装はApple ID向けであり、iOSでGoogleログインを続ける場合は別途認証方式を決める。

健康データ用の無料Unityプラグインは、権限処理・歩数集計・ビルド・ライセンスの条件を満たすものを確定できなかったため、読み取り専用のAndroidライブラリを実装した。
追加項目でも、Unity 6000.6・Android 9以降での動作、項目別の部分許可、記録元付きのページ取得を満たす無料Unity用プラグインを確認できていないため、既存ライブラリを拡張した。
Health Connect SDKは既存の安定版1.1.0を使用する。[Android公式の導入手順](https://developer.android.com/health-and-fitness/health-connect/get-started)
有料のHealthBridgeは採用していない。
AndroidライブラリはソースのままUnityの `.androidlib` として組み込み、Unity 6000.6のAGP 9によるKotlinビルドを使う。
依存バージョンは [build.gradle](../../client/Assets/Plugins/Android/BaryonyxHealth.androidlib/build.gradle) に記載している。
Android 8では未対応とし、Health Connectを使えるAndroid 9以降で利用可否を確認する。

UMothのAndroid依存関係とGradleテンプレートは、EDM4UのResolve操作で生成する。
生成済みテンプレートの依存欄を手編集せず、UMoth側の依存宣言とResolverを使って更新する。
ネイティブ部分だけを検証するGradleプロジェクトは [client/ci/health-native](../../client/ci/health-native/settings.gradle) に置く。
リポジトリ直下から、Unity同梱のGradleに `-p client/ci/health-native :health:assembleDebug` を指定してビルドする。
`-p client/ci/health-native :health:testDebugUnitTest` で、歩数と追加項目の部分許可・ページ取得・中断・日付境界・値のJSON変換を検証する。
歩数の取得元と端末情報の検証には、テスト専用の `androidx.health.connect:connect-testing:1.0.0-alpha03` と `populatedWithTestValues` を使う。製品の依存はHealth Connect SDK 1.1.0を維持する。[公式のテスト手順](https://developer.android.com/health-and-fitness/health-connect/test/unit-tests)
Androidアプリ全体は既存の [AndroidBuild](../../client/Assets/Baryonyx/Editor/CI/AndroidBuild.cs) とCIを使う。
必要に応じて最終APKへ [組み込み検査](../../client/ci/verify-health-apk.py) を手動で実行し、Health Connect・UMothのクラス、歩数を含む16種類の読み取り権限、書き込み・バックグラウンド・履歴拡張の権限がないことを確認する。
Android CIはビルドの成否確認とAPK保存に絞り、この追加検査は実行しない。

## 検証と残る確認

自動テストは [Unityの画面操作テスト](../../client/Assets/Baryonyx/Features/Health/Tests/EditMode/Presentation/HealthScreenPresenterTests.cs)、[署名検証テスト](../../server/src/features/accounts/auth.test.ts)、[入力検証テスト](../../server/src/features/health/schema.test.ts)、[APIシナリオ](../../server/tests/scenarios/) に置く。
Unity側は既存の `Baryonyx.EditModeTests` アセンブリへ含め、CIの実行対象を維持する。
サーバーの健康データ専用 [fixture](../../server/src/features/health/fixtures.ts) は機能内に置き、認証・同期・保存・取得を通すAPIシナリオからも参照する。

2026-09-10の検証結果は以下のとおり。

- Unityの同期テスト10件が成功した。拒否・再許可、判定不能、値なし、通信失敗、ユーザー切り替え、背面からの即時復帰を含む。
- サーバーの既存機能を含む13件が成功し、その後追加した夏時間の検証も成功した。現在の対象は計14件。整形・型検査・Workersのビルドも成功した。
- Androidライブラリ単体と、ARM64・IL2CPPのAndroid APKをビルドできた。APKのネイティブクラス、読み取り専用権限、SDK 26/36、デバッグ署名を検査した。
- 文書リンクとGit差分の空白検査を確認した。

実機のOAuthログイン、OS権限画面での拒否・取り消し・再許可、歩行後の値、実際のサーバーへの送信は未確認である。
公開前には実際のアプリ識別子・署名・接続先で検証し、権限用途の説明を実際の公開ポリシーと一致させる。
iOSはProvider差し替え用の共通型とテストまでで、HealthKit・iOS認証は未実装である。

## 変更と判断の記録

- 2026-10-05：文書の整理で、2026-09-24に削除した歩数画面の仕様と検証記録を[削除した歩数画面の記録](../archive/health-data-local-screen.md)へ移した。現行の部分（取得・保存・再送、Google認証の設定、プラグイン）は残し、サーバー同期の経路を、TopとHomeがゲストのセッションで同期する現在の作りに合わせて書き直した。
