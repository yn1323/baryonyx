---
id: feature-health-data
type: specification
status: 運用中
updated: 2026-09-21
---

# 健康データの読み取りと保存

Android版の起動シーンではホームを表示したままHealth Connectへ接続し、当日を含む直近7暦日の歩数取得を開始する。
歩数一覧と、血圧・体重などの健康データを含む日別のJSON詳細はホームから開く歩数画面に表示する。
Google接続は任意の独立した操作とし、健康データの読み取りには要求しない。
EditorとAndroid以外の実行環境では、7日分のサンプルデータを使うプレビューを自動で表示する。
サーバーURLを設定しない場合、この画面はバックエンドへ接続せず、認証状態と健康データをメモリに保持する。
サーバーURLを設定したAndroid版では、Google認証後にHealth Connectの直近7日分をサーバーへ保存し、歩数報酬の請求と履歴取得を行う。
ユーザーがJSONをコピーした場合は、OSのクリップボードにも残る。
サーバー報酬の同期は起動時のHealth Connect取得後と、画面上の明示操作後に実行する。
バックグラウンド同期、Health Connectへの書き込み、iOSの実装は対象外である。

利用時に歩数が表示されない場合は、[Health Connectで歩数が「データなし」になるときの対処法](../qa/health-connect-no-steps.md)を参照する。

## ローカル表示画面

[冒険の試作画面](game-wireframe.md)にも同じHealth Connect接続を組み込んだ。
ホームに今日の歩数、専用画面に直近7日間の数値と棒グラフを古い日から順に表示する。
取得済みの0歩と記録なし・権限不足・失敗を区別し、日付列から元のJSONを開く。
Providerの選択と寿命はAppのHealthRuntimeで共通化し、集計はHealthWeekSummaryが担当する。
サーバーURLが未設定の起動経路はサーバー・DBへ保存せず、Androidではサンプル値を使わない。
以下の一覧・認証ボタン・コピー操作の説明は、ホームから開く歩数画面に対応する。

画面上部には「1週間の歩数」と、[正式なアプリ名](../game/overview.md)を表示する。
画面アセットの生成時に、ブランド表記へPlayer Settingsの `productName` を設定する。

ディレクトリと依存方向は [クライアントの構成と依存関係](../rules/frontend-design.md) に従う。
表示処理はHealthの `Runtime/Presentation/`、利用条件の判定は `Runtime/Requirements/`、サンプルProviderは `Runtime/Preview/`、既存のサーバー同期は `Runtime/Sync/` に分ける。
AppはProviderを選択し、`HealthScreenView.Bind` の `preview` 引数で表示モードを渡す。
プレビューの文言はViewの描画内で決まり、App側での上書きやイベントの購読順に依存しない。

起動時に `Application.targetFrameRate = 60` を設定し、目標フレームレートを60 FPSにする。
実際のFPSは端末の画面更新頻度と処理負荷によって下がる場合がある。

Android版で [Main](../../client/Assets/Baryonyx/App/Scenes/Main.unity) を起動すると、冒険と運動データの入口を持つホームを表示する。
起動直後からHealth Connectの利用条件確認と接続・歩数取得を開始し、ホームの今日の歩数と歩数画面へ結果を反映する。
歩数画面では「運動データに接続」と「Googleに接続」を別のパネルに表示する。
運動データの取得元はHealth Connectとし、「歩数」の読み取りを許可すると7日分の日別歩数を表示する。
接続ボタン、運動データの一覧、「歩数を更新」を同じパネルにまとめる。
Google未接続でもHealth Connectの接続ボタンを使用でき、利用可否と健康データの読み取り権限を確認する。
Google接続の成功・失敗・解除は、Health Connectの接続状態や取得済みの一覧・JSON詳細を変更しない。
Google接続が成功してもHealth Connectの状態は変えない。歩数の読み取りは起動時のHealth Connect接続処理または歩数画面の操作で開始する。
権限を拒否してもGoogleの認証状態は維持し、再接続とHealth Connect設定への導線を表示する。
更新・復帰時は権限を再確認し、OSの権限要求を自動で繰り返さない。
「運動データに接続」を押したときは、既に歩数を許可済みでも追加項目の権限を要求する。
少なくとも1種類を許可すれば取得を開始し、歩数が未許可でもほかの項目のJSONを開ける。
歩数が未許可の場合は理由と「歩数の読み取りを許可」ボタンを表示し、追加で許可して歩数を取得できる。
接続済みでも「Health Connectの設定を開く」を表示し、項目ごとの権限を変更できる。

日別一覧は新しい日付から7行を表示する。
測定値の0は「0歩」と表示し、今日は取得時点までの集計と明示する。
歩数の欠測は「データなし」、ほかの記録だけがある日は「歩数なし・JSONあり」、歩数の未許可・取得失敗はそれぞれ別の文言で表示する。
日付を選ぶと、画面内の大きなモーダルにその日のJSONを等幅フォントで表示する。
JSONは上下・左右へスクロールでき、「閉じる」またはAndroidの戻る操作で一覧へ戻る。
「JSONをコピー」を押すと、表示中の日付の整形済みJSON全文をクリップボードへコピーし、ボタンを「コピーしました」に切り替える。
詳細を開き直すとボタンの表示を戻す。
詳細表示中は背景のボタンを操作できない。

表示するJSONは、Androidブリッジが歩数の集計結果、集計前の歩数レコード、追加項目の記録をまとめた1日分のオブジェクトである。
元レコードはHealth Connectから取得した測定値・単位・時刻・記録元などをJSONへ変換する。SDKの全フィールドをそのまま直列化したものではない。
元の応答を保持し、未知の項目、null、整数、日付文字列の型を保って整形する。
画面表示のために健康データを再取得したり、ファイルやPlayerPrefsへ保存したりしない。
通常の背面移行と画面破棄で一覧と詳細を消去する。
Androidの分割画面などで操作フォーカスを失った場合も一覧と詳細を消去し、一時停止が解除され、かつフォーカスが戻ったときに読み取りを再開する。
OSの認証・権限画面の完了を待ち、前面へ戻ってから続行する。
Googleの認証画面への移動で一覧を消去した場合も、既にHealth Connectを利用していれば復帰後に権限を再確認して読み直す。

GoogleサインインとHealth Connectの許可は別の状態であり、Googleアカウントを変えても端末内の健康データの取得元が切り替わるわけではない。
サーバーURLを設定した場合だけ、Googleの資格情報をサーバーのセッション発行へ使い、Health Connectから取得した7日分の歩数を報酬計算の入力として送信する。
サーバーURLを設定しない場合は、Google認証では外部通信が発生しても健康データをゲームサーバーへ送信しない。
GoogleアカウントやHealth Connectの許可はOS側に残り得るため、アプリのメモリ保持とは区別する。

### 起動時と設定画面からの復帰時の案内

Android版は起動時に1回、Health Connectの利用可否、このアプリの歩数読み取り権限、端末の自動計測への対応、歩数データの有無を確認する。
確認後は起動時の接続処理で読み取り権限を要求し、未許可の場合はAndroidの許可画面を表示する。
ホームは表示したまま接続処理を進め、許可を拒否した場合は歩数画面から再接続または設定へ進める。

アプリから開いた設定画面から戻ると、条件を再確認して案内を更新する。
通常のアプリ切り替え・ロック解除・Google認証からの復帰では、要件チェックを追加実行しない。
既存のデータ再取得前の権限確認は維持し、接続・歩数更新で得た結果も案内へ反映する。
初回の確認が背面移行で中断された場合は、前面へ戻ってから未完了の確認をやり直す。

[HealthRequirementCheck.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/Requirements/HealthRequirementCheck.cs) が確認結果から判定コードを決め、[HealthRequirementMessage.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/Requirements/HealthRequirementMessage.cs) が日本語の本文と案内先を返す。
AndroidのAPIレベル、SDK拡張バージョン、歩数センサーの搭載有無はC#から取得する。
Health Connectへの照会は [HealthRequirements.kt](../../client/Assets/Plugins/Android/BaryonyxHealth.androidlib/src/main/kotlin/com/baryonyx/health/HealthRequirements.kt) で行い、歩数単独の許可がある場合だけ、今日を含む7暦日の歩数を集計する。
要件確認のために血圧・体重などの追加項目は取得しない。

端末の自動計測はAndroid 14以上、SDK拡張20以上、歩数センサーありを対応条件として確認する。
これらを満たさなくても、Health Connectから歩数を取得できれば要件不足の案内を表示しない。
集計値が0の場合も記録ありとし、データの欠測、取得失敗、未許可、確認不能を区別する。
対応条件を満たしても、Health Connect側の記録スイッチや外部アプリの同期状態まで保証しない。[Androidの歩数記録仕様](https://developer.android.com/health-and-fitness/health-connect/features/steps?hl=ja)

暫定UIは運動データのパネルにある説明領域を使い、接続済みでも対応が必要な場合は案内を表示する。
Google Playシステム更新・Android更新の案内では「端末の設定を開く」、それ以外では「Health Connectの設定を開く」を表示する。
Google Playシステム更新の画面への機種固有の直接遷移は使わず、端末の設定を開いて対象項目の確認を促す。
案内は接続・取得エラーを隠さず、条件が解消されたら解除する。
新しいモーダルやOSのプッシュ通知は追加していない。
詳しい対処手順は[歩数の利用条件と起動時の案内](../qa/health-connect-requirements.md)、判定表と検証記録は[実装計画](../plans/2026-09-13-health-connect-requirement-notice.md)を参照する。

### 歩数の元レコードと追加項目のJSON詳細

歩数の元レコードと追加項目は [HealthRecords.kt](../../client/Assets/Plugins/Android/BaryonyxHealth.androidlib/src/main/kotlin/com/baryonyx/health/HealthRecords.kt) で読み取る。
対応する型、時刻、JSONへの値の変換は [HealthRecordCatalog.kt](../../client/Assets/Plugins/Android/BaryonyxHealth.androidlib/src/main/kotlin/com/baryonyx/health/HealthRecordCatalog.kt) の型別定義で管理し、読み取り権限もそこから導出する。
対象は歩数、体重、体脂肪率、身長、血圧、心拍、安静時心拍、酸素飽和度、呼吸数、体温、血糖値、睡眠、距離、活動時消費カロリー、総消費カロリー、運動記録の16種類である。
栄養・水分・月経関連・医療記録や運動ルートは今回の対象に含めない。

日付を選ぶと、JSONの `records` 内で各種類の `status` と `records` 配列を確認できる。
各記録に `sourceApp`（書き込み元のパッケージ名）、測定時刻、最終更新時刻、記録方法を付ける。
数値の単位は `kilograms`、`systolicMmHg`、`beatsPerMinute` などのキーで明示する。
睡眠にはステージ、運動には種別を含める。ステージ・運動種別・測定姿勢などの分類値はSDKの整数値を保持する。
特定サービスには限定せず、同じ端末のHealth Connectに保存された記録を読む。サービス側にだけあるデータは取得できない。

| `status` | 意味 |
|---|---|
| `success` | 対象日に記録がある |
| `empty` | 読み取りは成功したが、対象日の記録がない |
| `permission_required` | その種類の読み取りが未許可、または取得中に権限エラーになった |
| `failed` | その種類の取得が失敗した |
| `truncated` | 7日分の取得上限に達し、未取得の続きがある |

歩数は従来どおり日別の集計APIを使い、`stepsStatus` で成功・欠測・未許可・失敗を区別する。
`records.steps.records` には、集計前の `StepsRecord` を取得元ごとに保持する。
各記録の `count` は元の区間全体の歩数であり、`sourceApp`、区間の開始・終了時刻とオフセット、最終更新時刻、記録方法、レコードID、書き込み元が付けたレコードIDと版、端末の種類・メーカー・機種名を表示する。
提供されていない端末情報、書き込み元が付けたレコードID、時差は `null` とする。
Google Fitと端末で同じ時間帯を記録していても、元レコードは両方を表示する。
日別の集計値はHealth Connectの優先順位と重複処理に従うため、元レコードの `count` の単純合計とは一致しない場合がある。[歩数の集計仕様](https://developer.android.com/health-and-fitness/health-connect/aggregate-data)
歩数の元レコードの取得状態は `records.steps.status` に入り、集計結果の `stepsStatus` と独立する。
片方の取得に失敗した場合も、もう片方の結果を表示する。

端末の自動計測の `sourceApp` は、過去の記録では `android`、2026年6月以降のHealth Connect更新では端末固有の識別子になる。
識別子を固定値で置換せず、取得した値と `device` の情報を表示する。[端末の歩数記録仕様](https://developer.android.com/health-and-fitness/health-connect/features/steps)
ここでいう元データは同じ端末のHealth Connectが返したレコードであり、Google Fit内にだけあるデータや、集計で採用された記録の明細を表すものではない。

元レコードは合算・重複除去を行わず、記録元ごとの値として表示する。
一部の種類で取得に失敗してもほかの結果は表示し、画面に一部取得失敗の案内を出す。
取得完了前に読み取り許可の取り消しを検出した場合は、取得済みの応答全体を破棄して権限の確認を案内する。

歩数を含む元レコードは7日分を種類ごとに新しい順でページ取得し、最大200件まで保持する。
上限に達して続きがある場合、該当種類には全日で `truncated: true` を付ける。配列が空でも「記録なし」とは判断できない。
心拍サンプルと睡眠ステージは1レコードにつき先頭240件までを表示し、総件数と `samplesTruncated` / `stagesTruncated` を併記する。
点の測定は端末タイムゾーンの日付に振り分ける。歩数や睡眠などの区間記録は重なる日ごとに元の区間全体を表示し、日別に分割・按分しない。
上限と日付をまたぐ記録の扱いも、元レコードの合計と日別集計が異なる理由になる。

既存APKから更新した場合は、アプリを開き直すと起動時の接続処理が始まるため、表示されたHealth Connectの許可画面で確認する項目を許可する。
許可を後回しにした場合は、ホームから歩数画面を開いて「Health Connectに接続」を押す。
接続中の権限変更はHealth Connect設定で行い、アプリへ戻ると再取得する。
Health Connect自体に記録がない種類は、許可後も `empty` になる。

### Editor・PCでのプレビュー

UnityのPlayでは、OAuth設定やAndroid端末の接続なしで7日分の架空の歩数を表示する。
画面に「サンプルデータ / プレビュー」、注記に架空のデータであることを表示し、詳細にも「サンプルJSON」と `sample: true` を付ける。
日付は日本時間の今日から過去7日分を生成し、通常の歩数、「データなし」、測定値の0を含める。
更新すると実行時点の日付で生成し直す。
今日と昨日のJSONには架空の血圧と書き込み元、体重には未許可の状態を含め、歩数が欠測でも追加項目を確認できる。

日別の行、JSON詳細、更新は実際の画面と同じ操作で確認できる。
Google未接続のサンプル状態で一覧を表示し、「Google接続を試す（サンプル）」と「Google接続を解除（サンプル）」でGoogle側の状態だけを切り替えられる。
Google認証、Health Connect、バックエンドへの通信や資格情報の生成は行わない。

切り替えはUSB接続の有無ではなく、[HealthScreenBootstrap](../../client/Assets/Baryonyx/App/Runtime/HealthScreenBootstrap.cs) の `UNITY_ANDROID && !UNITY_EDITOR` で決める。
Androidをビルド対象にしたEditorやDevice Simulatorもプレビューになり、Androidプレイヤーだけが実Providerを使う。
Android以外のビルドもプレビュー対象とし、iOSの実データ取得に対応したものとは扱わない。
プレビューのProviderと表示用コードはAndroidプレイヤーのコンパイルから除外する。

### スマートフォン向けの画面構成

「1週間の歩数」は縦画面を基本とし、見出し、運動データのパネル、Googleの接続パネルを縦一列に配置する。
運動データのパネル内にHealth Connectの接続操作、「運動データ / 歩数」の見出し、対象期間と更新、7日分の一覧、注記を置く。
画面全体を一つのScrollRectでスクロールし、通常の説明文や長い数値は必要な高さへ伸ばす。
認証後は認証ボタンを状態表示へ置き換え、取得後は更新を表示する。
「Google接続を解除」は認証済みの場合にGoogleのパネル内へ表示する。

日別の行は日付・曜日、歩数または「データなし」、矢印を別の部品として配置する。
行全体をタップするとJSON詳細が開く。
詳細の背景は画面全体を覆い、対象日、JSONの表示領域、コピー・閉じるボタンはSafeArea内に配置する。
詳細表示中はメイン画面のスクロールを止め、閉じた後に閲覧位置を保つ。

[HealthScreenLayout](../../client/Assets/Baryonyx/Features/Health/Runtime/Presentation/HealthScreenLayout.cs) が画面サイズ・SafeArea・Canvasの論理サイズを比較し、初期表示、再有効化、表示条件の変更時に配置を更新する。
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
| 見出し・本文・補足の文字サイズ | 44・30・26 |
| ブランド表記・JSONの文字サイズ | 22・22 |

JSONはインデントと構造を読むため、等幅フォント・折り返しなし・上下左右のスクロールを維持する。
コピー・閉じるボタンはJSON本文のスクロール領域の外で横並びにし、常に操作できるようにする。
OSの文字拡大への追従や入力欄のキーボード回避は、現在の画面には実装していない。

### Google認証の設定

1. 同じGoogle Cloudプロジェクトで、Googleログイン用のWebクライアントと、実際のアプリID・署名証明書に対応するAndroidクライアントを用意する。
2. [HealthConnectionSettings](../../client/Assets/Baryonyx/Features/Health/Data/HealthConnectionSettings.asset) の `Google Web Client Id` に、末尾が `.apps.googleusercontent.com` のWebクライアントIDを設定する。クライアントシークレットはアプリに入れない。
3. 同じ設定アセットの `Server Base Url` に、認証APIを公開するサーバーのHTTPSベースURLを設定する。空欄ならローカル表示だけを使う。
4. Androidビルドを端末にインストールし、Google認証と、Google未接続でのHealth Connect接続・歩数の読み取りをそれぞれ確認する。

設定アセットにはWebクライアントIDが保存されている。
運動報酬APIのURLは未設定であるため、公開環境へ接続する場合は端末へ配布する設定アセットへ別途設定する。
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
| [HealthScreenPreviewProvider](../../client/Assets/Baryonyx/Features/Health/Runtime/Preview/HealthScreenPreviewProvider.cs) | Android以外で使う仮の認証結果と、日本時間の直近7日分のサンプルデータ |
| [UmothGoogleSignInProvider](../../client/Assets/Baryonyx/Features/Health/Runtime/Authentication/UmothGoogleSignInProvider.cs) | Googleサインインとサインアウト。サーバー連携用の資格情報は報酬サービスへだけ渡す |
| [ExerciseRewardService](../../client/Assets/Baryonyx/Features/Health/Runtime/Sync/ExerciseRewardService.cs) | Googleセッション、Health Connectの7日分保存、ルーン請求、残高と履歴の取得 |
| [HealthScreenPresenter](../../client/Assets/Baryonyx/Features/Health/Runtime/Presentation/HealthScreenPresenter.cs) | 操作順、取得、権限、報酬請求、画面の状態遷移、設定画面から戻った後の再確認 |
| [HealthScreenOperations](../../client/Assets/Baryonyx/Features/Health/Runtime/Presentation/HealthScreenOperations.cs) | 多重実行防止、中断、OS画面の前面復帰待ち、遅延結果の世代判定 |
| [HealthDaySnapshot](../../client/Assets/Baryonyx/Features/Health/Runtime/Presentation/HealthDaySnapshot.cs) | 7日分の検査と、一覧値・元JSONの対応 |
| [HealthScreenView](../../client/Assets/Baryonyx/Features/Health/Runtime/Presentation/HealthScreenView.cs) | uGUIの入力、通常・プレビューの文言、状態別の操作、日別一覧、閲覧位置の調整 |
| [HealthJsonDetails](../../client/Assets/Baryonyx/Features/Health/Runtime/Presentation/HealthJsonDetails.cs) | JSONモーダルの開閉、コピー、スクロールの初期化、一覧の選択復元 |
| [HealthScreenLayout](../../client/Assets/Baryonyx/Features/Health/Runtime/Presentation/HealthScreenLayout.cs) | SafeAreaと表示領域の変更への追従、パネルの最大幅 |
| [HealthScreenAssets](../../client/Assets/Baryonyx/Features/Health/Editor/HealthScreenAssets.cs) | Unity APIで専用Prefabとフォントを生成するEditorコマンド |
| [HealthAppSceneSetup](../../client/Assets/Baryonyx/App/Editor/HealthAppSceneSetup.cs) | HealthのPrefab・設定とAppの起動処理を現在のシーンへ配置するEditorコマンド |

読み取りの操作はこの画面だけが所有するため、計画時の `HealthReadService` とPresenterは一つの通常のC#クラスにまとめた。
外部SDKの境界はインターフェースで保ち、一覧と詳細の小さなViewも一つのMonoBehaviourで管理する。
アセンブリとCIのテスト対象は既存の境界を維持している。

EditModeの [状態遷移・JSONテスト](../../client/Assets/Baryonyx/Features/Health/Tests/EditMode/Presentation/HealthScreenPresenterTests.cs) と、PlayModeの [画面操作テスト](../../client/Assets/Baryonyx/Features/Health/Tests/PlayMode/Presentation/HealthScreenScenarioTests.cs) を追加した。
PlayModeは実Prefabと仮想入力を使い、外部Providerだけをテスト用に差し替える。
テストPlayer向けのPrefabはテスト準備時だけResourcesへコピーし、終了時にUnity APIで削除する。
途中終了で `Tests/PlayMode/Resources/BaryonyxHealthScreenTest.prefab` が残っている場合は、HealthScreenBuildCheckが通常ビルドを停止して混入を防ぐ。テストの後始末を完了してから再ビルドする。
通常の起動シーンは実行環境に応じて実Providerとプレビューを自動で選び、Android版をサンプル表示へ切り替える操作は設けない。
[プレビューのデータ検査](../../client/Assets/Baryonyx/Features/Health/Tests/EditMode/Preview/HealthScreenPreviewProviderTests.cs) は日付境界、欠測と0の区別、JSON、更新・再開を確認する。
[Editor起動の検査](../../client/Assets/Baryonyx/App/Tests/PlayMode/HealthScreenPreviewStartupTests.cs) はOAuth設定なしでPlayするとサンプル一覧が表示されることを確認する。

画面とJSONにはDotGothic16を同梱し、ライセンスは [Fonts](../../client/Assets/Baryonyx/Features/Health/UI/Fonts/) に置く。
SIL Open Font Licenseで再配布できる。[DotGothic16の配布元](https://github.com/fontworks-fonts/DotGothic16)
JSONにはUPMの `com.unity.nuget.newtonsoft-json@3.2.2` を直接依存として使う。

### 2026-09-13の歩数の元レコード表示の検証

- AndroidライブラリのJUnit 17件が成功した。歩数の取得元・端末情報の保持、同じ時間帯の複数記録、日付境界、取得上限、権限取り消しと既存項目の検査を含む。
- AndroidライブラリのDebugビルドが成功した。
- Editorの実画面に架空のGoogle Fit 120歩・端末110歩の元レコードと集計120歩を渡し、日付ボタンから開いたJSONで各値が保持されることを確認した。1080×2400のGameビューを `client/Assets/DevCaptures/health-raw-steps-json.png` に保存し、表示を目視確認した。
- CIと同じ `AndroidBuild.Build` でAPKを生成し、署名と組み込みクラス・読み取り権限の検査が成功した。`G:\マイドライブ\71_プロジェクト\baryonyx\baryonyx.apk` へ上書きコピーし、コピー元と配置先のSHA-256が一致した。

端末のHealth Connectから取得した実際の歩数レコードと、Android上のJSON表示との照合、Google Driveのクラウド側の同期完了は未確認である。

### 2026-09-13の文字サイズとJSONコピーの検証

画面全体の文字を約25〜30％、JSON本文を約39％小さくし、詳細の下端にコピー・閉じるボタンを並べた。

- PlayMode 14件が成功した。既存の画面操作、長いJSONのスクロール、6種類の画面寸法と非対称なSafeAreaでの配置を含む。
- EditorのサンプルJSONで、ボタン位置へのクリック、スクロール後の全文コピー、完了表示、別の日付を開いたときの表示リセットとコピー内容の切り替えを確認した。検証後は元のクリップボードを復元した。
- 1080×2400のGameビューで一覧、JSON詳細、コピー完了表示を撮影して確認した。画像は `client/Assets/DevCaptures/health-small-text-week.png`、`health-small-text-json.png`、`health-small-text-copied.png` に保存した。
- 変更したC# 2ファイルのCSharpier 1.3.0検査とUnityの再コンパイルが成功し、Consoleのエラーは0件だった。
- CIと同じ `AndroidBuild.Build` でARM64・IL2CPPのAPKを生成し、署名と組み込みクラス・権限の検査が成功した。`G:\マイドライブ\71_プロジェクト\baryonyx\baryonyx.apk` へ上書きコピーし、コピー元と配置先のSHA-256が一致した。Google Driveのクラウド側の同期完了は未確認である。

Androidエミュレーター上での文字の読みやすさと、ほかのアプリへのJSON貼り付けは未確認である。

### 2026-09-13の運動データ一覧の検証

接続ボタンと7日分の歩数を同じパネルへまとめ、歩数が未許可の場合の追加許可ボタンを表示した。
既存のHealth Connect Providerと日別集計を再利用している。

- EditMode 64件、PlayMode 14件が成功した。歩数が未許可の状態から追加許可して一覧を取得する操作、0歩・欠測の区別、JSON詳細、画面サイズ変更とスクロールを含む。
- 1080×2400のGameビューで接続前と歩数一覧を撮影し、ボタン、日付、歩数を目視確認した。画像は `client/Assets/DevCaptures/exercise-initial.png`、`exercise-week.png` に保存した。確認用のProviderと架空のデータを使用している。
- 変更したC# 6ファイルはCSharpier 1.3.0で整形・検査し、Unityの再コンパイルとConsoleのエラー0件を確認した。
- CIと同じ `AndroidBuild.Build` でARM64・IL2CPPのAPKを生成し、組み込みクラス、歩数を含む読み取り権限と署名を検査した。`G:\マイドライブ\71_プロジェクト\baryonyx\baryonyx.apk` へ上書きコピーし、コピー元と配置先のSHA-256が一致した。Google Driveのクラウド側の同期完了は確認していない。

Androidの権限画面と実際の歩数との照合は未確認であり、Androidエミュレーターでの手動確認が必要である。

### 2026-09-13の接続分離の検証

Google接続とHealth Connect接続を別のパネルに分け、Google未接続でも歩数の取得・更新・JSON詳細を利用できるようにした。
Google接続の失敗や解除でHealth Connectの状態を変更せず、認証画面から戻った場合も必要な権限確認と再取得を行う。

- EditMode 61件、PlayMode 13件が成功した。Google未接続での取得、認証失敗後の継続、Google接続解除後の一覧保持、復帰時の権限取り消し、実Prefabのボタン操作とJSON詳細を含む。
- 変更したC# 8ファイルのCSharpier検査が成功し、再コンパイル・画面確認後のUnity Consoleにエラーがないことを確認した。
- 1080×2400のGameビューで初期画面とGoogle未接続の歩数一覧を撮影し、両パネルの配置と日本語表示を確認した。画像はGit対象外の `client/Assets/DevCaptures/health-independent-initial.png`、`health-independent-week.png` に置く。一覧の画像はサンプルデータである。
- ローカルの `shortcuts/build-apk-to-drive.bat` でAPKをビルドし、Google Drive for desktopの配置先フォルダーへコピーした。APKの署名検証と [組み込み検査](../../client/ci/verify-health-apk.py) が成功し、コピー元と配置先のSHA-256が一致した。Google Driveのクラウド側への同期完了は確認していない。

今回の自動検証ではGoogleとHealth Connectの応答をテスト用Providerに置き換えている。
変更後のAPKでのGoogle認証とOSの権限画面は、Androidエミュレーターで別途確認する。

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
| [HealthClient.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/Sync/HealthClient.cs) | 画面から呼ぶ認証・権限・同期の入口とアプリの前面状態 |
| [HealthContracts.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthContracts.cs) | Provider、権限・取得結果の共通型。同期用のセッションとAPI契約は `Runtime/Sync/HealthSyncContracts.cs` に置く |
| [HealthSyncService.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/Sync/HealthSyncService.cs) | 同期の多重起動防止、中断、ユーザー切り替え時の結果破棄 |
| [HealthApiClient.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/Sync/HealthApiClient.cs) | サーバーへの認証・保存・取得要求 |
| [HealthConnectProvider.cs](../../client/Assets/Baryonyx/Features/Health/Runtime/HealthConnectProvider.cs) | UnityからAndroidへの呼び出し |
| [Android連携コード](../../client/Assets/Plugins/Android/BaryonyxHealth.androidlib/src/main/kotlin/com/baryonyx/health/HealthBridge.kt) | Health Connectの権限確認と日別集計 |
| [サーバールート](../../server/src/features/health/routes.ts)・[入力検証](../../server/src/features/health/schema.ts) | HTTP要求の検証、認証middlewareと応答、単日と週全体の制約 |
| [認証処理](../../server/src/features/health/auth.ts) | Googleの本人確認、セッション発行、トークンのハッシュ化と確認 |
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

## 既存のサーバー同期APIを直接利用する場合

既存の `HealthClient` は、健康データの保存APIを直接呼ぶ低レベルの入口として残している。
運動報酬を使う現在の起動経路は `HealthRuntime`、`HealthScreenPresenter`、`ExerciseRewardService` が担当し、Health Connectの7日分を保存してからルーンを請求する。
`HealthClient` を直接利用する場合は、存続させるGameObjectへ追加して初期化する。

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

2026-09-13の追加項目のJSON表示では、以下を確認した。

- UnityのEditMode 64件、PlayMode 16件が成功した。外部パッケージで無効化されているPlayMode 2件はスキップされた。
- 歩数が未許可でも体重のJSONを開けること、単位と記録元が残ること、明示的な接続だけが追加権限を要求すること、一部取得失敗から更新で回復できることをテストした。
- AndroidライブラリのJVMテスト9件が成功した。部分許可、ページ取得、上限表示、権限取り消し、中断、日付境界、JSONへの変換を含む。
- Unityの1080×2400のGameビューで、歩数が欠測した日の血圧サンプルJSON、スクロール領域、閉じるボタンを目視確認した。
- ARM64・IL2CPPのAPKを生成し、追加クラス、16種類の読み取り権限、SDK 26/36、APK署名を検査した。書き込み・バックグラウンド・履歴拡張の権限がないことも確認した。

実機での追加項目の権限許可と、Fit・OMRONなどが書き込んだ実データの読み取りは未確認である。

自動テストは [Unityの同期テスト](../../client/Assets/Baryonyx/Features/Health/Tests/EditMode/Sync/HealthSyncTests.cs)、[署名検証テスト](../../server/src/features/health/auth.test.ts)、[入力検証テスト](../../server/src/features/health/schema.test.ts)、[APIシナリオ](../../server/tests/scenarios/) に置く。
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
