# 健康データの読み取りと保存

Android版の起動シーンでは、Googleで認証してからHealth Connectへ接続し、当日を含む直近7暦日の歩数と日別のJSON詳細を表示する。
EditorとAndroid以外の実行環境では、7日分のサンプルデータを使うプレビューを自動で表示する。
この画面はバックエンドへ接続せず、認証状態と歩数をメモリにだけ保持する。
既存のサーバー同期処理は残しているが、今回の起動経路からは呼ばない。
バックグラウンド同期、Health Connectへの書き込み、iOSの実装は対象外である。

## ローカル表示画面

Android版で [SampleScene](../../client/Assets/Scenes/SampleScene.unity) を起動すると、Google認証からHealth Connectへ接続する画面を表示する。
認証成功後に接続ボタンを表示し、ユーザーが接続を選ぶと利用可否と歩数の読み取り権限を確認する。
権限を拒否してもGoogleの認証状態は維持し、再接続とHealth Connect設定への導線を表示する。
更新・復帰時は権限を再確認し、OSの権限要求を自動で繰り返さない。

日別一覧は新しい日付から7行を表示する。
`hasValue=false` は「データなし」、測定値の0は「0歩」と表示し、今日は取得時点までの集計と明示する。
日付を選ぶと、画面内の大きなモーダルにその日のJSONを等幅フォントで表示する。
JSONは上下・左右へスクロールでき、「閉じる」またはAndroidの戻る操作で一覧へ戻る。
詳細表示中は背景のボタンを操作できない。

表示するJSONは、Androidブリッジが集計して返した1日分のオブジェクトである。
Health Connectの個々の生レコードや、Googleの資格情報を表示するものではない。
元の応答を保持し、未知の項目、null、整数、日付文字列の型を保って整形する。
画面表示のために健康データを再取得したり、ファイルやPlayerPrefsへ保存したりしない。
通常の背面移行、サインアウト、画面破棄で一覧と詳細を消去する。
OSの認証・権限画面の完了を待ち、前面へ戻ってから続行する。

GoogleサインインとHealth Connectの許可は別の状態であり、Googleアカウントを変えても端末内の健康データの取得元が切り替わるわけではない。
今回の認証成功はUMothがGoogleの資格情報を返したことを指し、サーバーのセッション発行・本人確認は行わない。
Google認証では外部通信が発生するが、取得した歩数を外部へ送信する処理はこの画面にない。
GoogleアカウントやHealth Connectの許可はOS側に残り得るため、アプリのメモリ保持とは区別する。

### Editor・PCでのプレビュー

UnityのPlayでは、OAuth設定やAndroid端末の接続なしで7日分の架空の歩数を表示する。
画面に「サンプルデータ / プレビュー」、注記に架空のデータであることを表示し、詳細にも「サンプルJSON」と `sample: true` を付ける。
日付は日本時間の今日から過去7日分を生成し、通常の歩数、「データなし」、測定値の0を含める。
更新すると実行時点の日付で生成し直す。

日別の行、JSON詳細、更新は実際の画面と同じ操作で確認できる。
「プレビューを終了」で表示を消去し、「プレビューを開始」から「サンプルデータを表示」で再開できる。
Google認証、Health Connect、バックエンドへの通信や資格情報の生成は行わない。

切り替えはUSB接続の有無ではなく、[HealthScreenBootstrap](../../client/Assets/Baryonyx/App/Runtime/HealthScreenBootstrap.cs) の `UNITY_ANDROID && !UNITY_EDITOR` で決める。
Androidをビルド対象にしたEditorやDevice Simulatorもプレビューになり、Androidプレイヤーだけが実Providerを使う。
Android以外のビルドもプレビュー対象とし、iOSの実データ取得に対応したものとは扱わない。
プレビューのProviderと表示用コードはAndroidプレイヤーのコンパイルから除外する。

### スマートフォン向けの画面構成

「1週間の歩数」は縦画面を基本とし、見出し、接続状態と操作、対象期間と更新、7日分の一覧、注記とサインアウトを縦一列に配置する。
画面全体を一つのScrollRectでスクロールし、通常の説明文や長い数値は必要な高さへ伸ばす。
認証後は認証ボタンを状態表示へ置き換え、取得後は更新を表示する。
サインアウトは認証済みの場合に末尾へ表示する。

日別の行は日付・曜日、歩数または「データなし」、矢印を別の部品として配置する。
行全体をタップするとJSON詳細が開く。
詳細の背景は画面全体を覆い、対象日、JSONの表示領域、閉じるボタンはSafeArea内に配置する。
詳細表示中はメイン画面のスクロールを止め、閉じた後に閲覧位置を保つ。

[HealthScreenLayout](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthScreenLayout.cs) が画面サイズ・SafeArea・Canvasの論理サイズを比較し、初期表示、再有効化、表示条件の変更時に配置を更新する。
サイズ変更だけでは認証・取得処理を呼び出さない。
一般の配置・操作・検証ルールは [UI設計ルール](../rules/ui-design.md) に従う。

この画面はCanvasScalerの基準を800×1100、Scale With Screen Size、Expandとする。
以下はUnityの基準単位であり、端末のpxやAndroidのdpとは区別する。

| 対象 | 設定値 |
|---|---|
| 左右余白 | 40ずつ |
| メイン・詳細パネルの最大幅 | 860・980 |
| 通常ボタンの最小幅・最小高 | 120 |
| 日別の行の最小高 | 140 |
| 操作領域間の余白 | 20以上 |
| 見出し・本文・補足の文字サイズ | 60・40・35 |
| ブランド表記・JSONの文字サイズ | 30・36 |

JSONはインデントと構造を読むため、等幅フォント・折り返しなし・上下左右のスクロールを維持する。
閉じるボタンはJSON本文のスクロール領域に含めない。
OSの文字拡大への追従や入力欄のキーボード回避は、現在の画面には実装していない。

### Google認証の設定

1. 同じGoogle Cloudプロジェクトで、Googleログイン用のWebクライアントと、実際のアプリID・署名証明書に対応するAndroidクライアントを用意する。
2. [HealthConnectionSettings](../../client/Assets/Baryonyx/Features/Health/Data/HealthConnectionSettings.asset) の `Google Web Client Id` に、末尾が `.apps.googleusercontent.com` のWebクライアントIDを設定する。クライアントシークレットはアプリに入れない。
3. Androidビルドを端末にインストールし、Google認証、Health Connect接続、歩数の読み取りを順に確認する。

設定アセットにはWebクライアントIDが保存されている。
Google Cloud側の登録内容とAndroidのアプリID・署名との整合、実機での認証成功は未確認である。
空のまま認証ボタンを押すと設定不足を表示し、成功した扱いにはしない。
EditorではネイティブSDKを作らず、前述のプレビューを表示する。
UMoth 1.0.3はキャンセルと一部の失敗を同じエラーとして返すため、画面では「認証を完了できませんでした」と案内して再試行を受け付ける。

CIの `UNITY_LICENSE` などはUnityの実行用、`GOOGLE_DRIVE_CLIENT_ID` はAPKの配布用であり、今回のログイン設定とは用途が異なる。
サーバー公開設定の `GOOGLE_CLIENT_ID` は将来IDトークンを検証するときに、このWebクライアントIDと一致させる。
現在のAndroidビルド補助は `com.croissantlab.baryonyx` とデバッグ署名を使うため、実際にインストールするAPKのアプリID・署名をOAuth登録時に確認する。
WebクライアントIDやAndroidクライアントの作成は今回行っていない。[Googleの設定手順](https://developer.android.com/identity/sign-in/credential-manager-siwg)

### 画面の構成とテスト

| 実装 | 責務 |
|---|---|
| [HealthScreenBootstrap](../../client/Assets/Baryonyx/App/Runtime/HealthScreenBootstrap.cs) | 実行環境に応じたProviderと画面の組み立て、前面・背面・終了通知 |
| [HealthScreenPreviewProvider](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthScreenPreviewProvider.cs) | Android以外で使う仮の認証結果と、日本時間の直近7日分のサンプルデータ |
| [UmothGoogleSignInProvider](../../client/Assets/Baryonyx/Features/Health/Runtime/Authentication/UmothGoogleSignInProvider.cs) | Googleサインインとサインアウト。資格情報を画面に渡さない |
| [HealthScreenPresenter](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthScreenPresenter.cs) | 操作順、取得、権限、状態遷移、多重実行防止、遅延結果の破棄 |
| [HealthDaySnapshot](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthDaySnapshot.cs) | 7日分の検査と、一覧値・元JSONの対応 |
| [HealthScreenView](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthScreenView.cs) | uGUIの入力、状態別の操作、日別一覧、JSONモーダル、閲覧位置の復帰 |
| [HealthScreenLayout](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthScreenLayout.cs) | SafeAreaと表示領域の変更への追従、パネルの最大幅 |
| [HealthScreenAssets](../../client/Assets/Baryonyx/Features/Health/Editor/HealthScreenAssets.cs) | Unity APIで専用Prefabとフォントを生成するEditorコマンド |

読み取りの操作はこの画面だけが所有するため、計画時の `HealthReadService` とPresenterは一つの通常のC#クラスにまとめた。
外部SDKの境界はインターフェースで保ち、一覧と詳細の小さなViewも一つのMonoBehaviourで管理する。
アセンブリとCIのテスト対象は既存の境界を維持している。

EditModeの [状態遷移・JSONテスト](../../client/Assets/Baryonyx/Features/Health/Tests/EditMode/HealthScreenPresenterTests.cs) と、PlayModeの [画面操作テスト](../../client/Assets/Baryonyx/Features/Health/Tests/PlayMode/HealthScreenScenarioTests.cs) を追加した。
PlayModeは実Prefabと仮想入力を使い、外部Providerだけをテスト用に差し替える。
テストPlayer向けのPrefabはテスト準備時だけResourcesへコピーし、終了時にUnity APIで削除する。
途中終了で `Tests/PlayMode/Resources/BaryonyxHealthScreenTest.prefab` が残っている場合は、HealthScreenBuildCheckが通常ビルドを停止して混入を防ぐ。テストの後始末を完了してから再ビルドする。
通常の起動シーンは実行環境に応じて実Providerとプレビューを自動で選び、Android版をサンプル表示へ切り替える操作は設けない。
[プレビューのデータ検査](../../client/Assets/Baryonyx/Features/Health/Tests/EditMode/HealthScreenPreviewProviderTests.cs) は日付境界、欠測と0の区別、JSON、更新・再開を確認する。
[Editor起動の検査](../../client/Assets/Baryonyx/Features/Health/Tests/PlayMode/HealthScreenPreviewStartupTests.cs) はOAuth設定なしでPlayするとサンプル一覧が表示されることを確認する。

日本語にはNoto Sans CJK JP、JSONにはNoto Sans Monoを同梱し、ライセンスは [Fonts](../../client/Assets/Baryonyx/Features/Health/UI/Fonts/) に置く。
いずれもSIL Open Font Licenseで再配布できる。[Notoの利用条件](https://notofonts.github.io/noto-docs/website/use/)
JSONにはUPMの `com.unity.nuget.newtonsoft-json@3.2.2` を直接依存として使う。

### 2026-09-13のローカル表示画面の検証

- 既存分を含むEditMode 40件・PlayMode 7件が成功した。認証前の制限、拒否後の再試行、背面移行と復帰、遅延応答の破棄、JSON構造の保持、実Prefabのボタン操作と詳細スクロールを含む。
- CSharpierでC# 26ファイルの整形検査が成功し、再コンパイルと最終ビルド後のConsoleに新たなエラーがないことを確認した。
- ARM64・IL2CPPの開発用APKを `client/Builds/Android/baryonyx-health-preview.apk` に生成した。パッケージ名は `com.croissantlab.baryonyx`、最小SDKは26、対象SDKは36である。
- APKの署名検証と [組み込み検査](../../client/ci/verify-health-apk.py) が成功した。Health Connect・UMothのクラス、歩数の読み取り権限を確認し、書き込み・バックグラウンド・履歴拡張の権限がないことを確認した。
- ビルドはエラー0件、警告8件で完了した。警告はUnity Pipelineの実行時設定、診断用シンボル、TMPシェーダーの非推奨指定、Pipeline・TMPなどの大きなメソッドに対するIL2CPP出力の分割に関するものである。
- PlayModeのGameビューで初期画面、7日一覧、JSONモーダルを撮影し、日本語表示と配置を確認した。画像はGit対象外の `client/Assets/DevCaptures/health-initial.png`、`health-week.png`、`health-json.png` に置く。一覧と詳細の画像はテストデータである。

このローカル表示画面の検証時点では、OAuthのWebクライアントIDは未設定で、Android実機は接続されていなかった。
実際のGoogle認証、OS権限画面での拒否・取り消し・再許可、端末内の歩数との照合は未確認であり、EditorのテストやAPKのビルド成功では代替しない。
この画面からバックエンドへの送信とアプリ側の永続化は行わない。

### 2026-09-13の縦画面改修の検証

縦画面の設定、全体スクロール、日付と歩数の個別表示、SafeAreaに追従する詳細画面を実装した。
PrefabのGUIDと起動シーンからの参照を維持し、同時に追加されたEditor・PCのサンプルプレビューにも同じ配置を適用している。

- EditMode 52件、PlayMode 12件が成功した。PlayModeは640×1136の固定解像度でも12件成功し、認証、接続、末尾までのスクロール、日別選択、長いJSON、一覧への復帰、サインアウトを確認した。
- 実Prefabに6種類の画面寸法と非対称なSafeAreaを適用し、操作領域、本文の高さ、Viewportと閉じるボタンの配置を検査した。長い数値、300文字の状態説明、詳細表示中のSafeArea変更も含む。
- Gameビューで640×1136、720×1280、1080×2400、2048×1536を確認した。Device Simulatorでは `Punch Hole Left (1080x2340)` と `Tablet Small (1536x2048)` を使用し、サンプル一覧と詳細の配置を撮影して確認した。
- 今回変更したC# 5ファイルのCSharpier検査が成功した。検証画像、実行時の寸法、テスト結果の保存先は [縦画面の実装計画](../plans/2026-09-13-portrait-ui.md#実施結果) に記録する。
- 縦画面改修後のARM64・IL2CPP開発用APKを `client/Builds/Android/baryonyx.apk` に生成し、署名検査と健康データ連携の組み込み検査が成功した。Manifestの起動Activityは `screenOrientation=1`（Portrait）で、最小SDK 26、対象SDK 36を維持している。

640px幅の条件は論理密度2倍を仮定した幅320dp相当の配置検査であり、実機のdpやタップのしやすさを測定した結果ではない。
Android実機は接続されていないため、端末回転、システムバー、Google認証・権限画面からの復帰、実際の歩数との照合は未確認である。

以下は、既存のサーバー同期処理の仕様である。

## 実装の入口

| 場所 | 処理 |
|---|---|
| [HealthClient.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthClient.cs) | 画面から呼ぶ認証・権限・同期の入口とアプリの前面状態 |
| [HealthContracts.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthContracts.cs) | iOSでも使うProvider、権限・取得結果の共通型 |
| [HealthSyncService.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthSyncService.cs) | 同期の多重起動防止、中断、ユーザー切り替え時の結果破棄 |
| [HealthApiClient.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthApiClient.cs) | サーバーへの認証・保存・取得要求 |
| [HealthConnectProvider.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthConnectProvider.cs) | UnityからAndroidへの呼び出し |
| [Android連携コード](../../client/Assets/Plugins/Android/BaryonyxHealth.androidlib/src/main/kotlin/com/baryonyx/health/HealthBridge.kt) | Health Connectの権限確認と日別集計 |
| [サーバールート](../../server/src/features/health/routes.ts)・[入力検証](../../server/src/features/health/schema.ts) | Googleの本人確認、セッション、HTTP要求の検証 |
| [DB操作](../../server/src/features/health/repository.ts) | Drizzleによるセッション管理、取得元の所有者確認、日別歩数の保存・取得 |
| [DBスキーマ](../../server/src/features/health/db-schema.ts)・[初期マイグレーション](../../server/migrations/0000_initial.sql) | ユーザー、セッション、取得元、日別歩数と過去値の取得日時 |

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

## サーバー同期を利用する場合の呼び出し

サーバー同期を組み込む場合は、存続させるGameObjectへ `HealthClient` を追加して初期化する。
以下は将来の接続用の例であり、現在のシーンはこの経路を呼ばない。

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
必要に応じて最終APKへ [組み込み検査](../../client/ci/verify-health-apk.py) を手動で実行し、Health Connect・UMothのクラス、歩数読み取り権限、バックグラウンド権限がないことを確認する。
Android CIはビルドの成否確認とAPK保存に絞り、この追加検査は実行しない。

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
