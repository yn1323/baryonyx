# 健康データの読み取りと保存

AndroidのHealth Connectから当日を含む直近7暦日の歩数を取得し、Googleログインしたユーザーのデータとしてサーバーへ保存する。
独自画面、バックグラウンド同期、Health Connectへの書き込み、iOSの実装は含めていない。

## 実装の入口

| 場所 | 処理 |
|---|---|
| [HealthClient.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthClient.cs) | 画面から呼ぶ認証・権限・同期の入口とアプリの前面状態 |
| [HealthContracts.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthContracts.cs) | iOSでも使うProvider、権限・取得結果の共通型 |
| [HealthSyncService.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthSyncService.cs) | 同期の多重起動防止、中断、ユーザー切り替え時の結果破棄 |
| [HealthApiClient.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthApiClient.cs) | サーバーへの認証・保存・取得要求 |
| [HealthConnectProvider.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthConnectProvider.cs) | UnityからAndroidへの呼び出し |
| [Android連携コード](../../client/Assets/Plugins/Android/BaryonyxHealth.androidlib/src/main/kotlin/com/baryonyx/health/HealthBridge.kt) | Health Connectの権限確認と日別集計 |
| [サーバールート](../../server/src/features/health/routes.ts)・[入力検証](../../server/src/features/health/schema.ts) | Googleの本人確認、セッション、取得元の所有者確認、D1保存・取得 |
| [DB定義](../../server/migrations/0002_health.sql)・[過去値の取得日時](../../server/migrations/0003_health_observation.sql) | ユーザー、セッション、取得元、日別歩数 |

HTTPのパスと入出力はサーバールートと入力検証コードを正とする。
認証以外の機能で必要になるまでは、健康データ機能の外へ認証の共通基盤を広げない。

## 利用に必要な設定

サーバーには `GOOGLE_CLIENT_ID` を設定する。
GoogleのAndroidクライアント設定に登録するアプリID・署名証明書と、IDトークンの宛先となるWebクライアントIDを用意する。
Unityの `Initialize` に渡すGoogleクライアントIDと、サーバーの `GOOGLE_CLIENT_ID` は同じWebクライアントIDにする。
設定がないサーバーはログインを503で拒否する。
ユーザーから受け取っていない実際のID・URLをコードへ埋め込んでいない。

ローカルのサーバーでは、[設定例](../../server/.env.example)を `server/.dev.vars` へコピーして値を設定する。
D1マイグレーションを適用してから起動する。
具体的な起動・環境別の公開手順は [バックエンドの開発環境](../rules/backend-design.md) に従う。
この機能の作業でリモートDBや公開環境は変更していない。

Unityからの接続先はHTTPSを使用する。
開発時の例外はループバックHTTPのみとし、AndroidのUSB接続でポートを転送する場合もループバックを使う。
認証トークンを送る接続で証明書検証を無効にしない。
既存CIの `dev.baryonyx.ci` はビルド検査用であり、実際のOAuth設定へ無条件に流用しない。

## 画面からの呼び出し

画面ができたら、存続させるGameObjectへ `HealthClient` を追加して初期化する。
以下は呼び出し順の例であり、現在のシーンへ自動実行処理を追加していない。

```csharp
var health = gameObject.AddComponent<Baryonyx.Health.HealthClient>();
health.Initialize(serverUrl, googleWebClientId);

// ユーザーのログイン操作から呼ぶ。
bool signedIn = await health.SignInAsync();
if (!signedIn) return;

// ユーザーの健康データ連携操作から呼ぶ。OSの権限画面が表示される。
var permission = await health.ConnectHealthAsync();
if (permission == Baryonyx.Health.HealthPermission.NotGranted) return;
var result = await health.SyncNowAsync();

// 保存済みデータは取得日時と状態を含む。
var saved = await health.ReadSavedAsync();

// ログアウト操作から呼ぶ。
await health.SignOutAsync();
```

接続処理や取得に失敗した場合は、呼び出し側で例外と `Status` を扱う。
`GetHealthAvailabilityAsync` で未対応・更新が必要な端末を区別し、`OpenHealthSettings` で設定を開ける。
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

健康データ用の無料Unityプラグインは、権限処理・集計・ビルド・ライセンスの条件を満たすものを確定できなかったため、歩数読み取り専用のAndroidライブラリを実装した。
有料のHealthBridgeは採用していない。
AndroidライブラリはソースのままUnityの `.androidlib` として組み込み、Unity 6000.6のAGP 9によるKotlinビルドを使う。
依存バージョンは [build.gradle](../../client/Assets/Plugins/Android/BaryonyxHealth.androidlib/build.gradle) に記載している。
Android 8では未対応とし、Health Connectを使えるAndroid 9以降で利用可否を確認する。

UMothのAndroid依存関係とGradleテンプレートは、EDM4UのResolve操作で生成する。
生成済みテンプレートの依存欄を手編集せず、UMoth側の依存宣言とResolverを使って更新する。
ネイティブ部分だけを検証するGradleプロジェクトは [client/ci/health-native](../../client/ci/health-native/settings.gradle) に置く。
リポジトリ直下から、Unity同梱のGradleに `-p client/ci/health-native :health:assembleDebug` を指定してビルドする。
Androidアプリ全体は既存の [AndroidBuild](../../client/Assets/Baryonyx/Editor/CI/AndroidBuild.cs) とCIを使う。
最終APKには [組み込み検査](../../client/ci/verify-health-apk.py) を実行し、Health Connect・UMothのクラス、歩数読み取り権限、バックグラウンド権限がないことを確認する。

## 検証と残る確認

自動テストは [Unityの同期テスト](../../client/Assets/Baryonyx/Features/Health/Tests/EditMode/HealthSyncTests.cs)、[署名検証テスト](../../server/src/features/health/auth.test.ts)、[入力検証テスト](../../server/src/features/health/schema.test.ts)、[APIシナリオ](../../server/tests/scenarios/) に置く。
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
