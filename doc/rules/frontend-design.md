---
id: rule-frontend-design
type: reference
status: 運用中
updated: 2026-09-22
---

# クライアントの構成と依存関係

Unityクライアントの自作コードと専用アセットは `client/Assets/Baryonyx/` にまとめる。
機能の変更に必要な実装・画面・設定・テストを、その機能からたどれるように配置する。
作業時には [ルートのAGENTS.md](../../AGENTS.md) と [clientのAGENTS.md](../../client/AGENTS.md) を適用する。

## 現在のディレクトリ構成

以下は構成整理後の配置である。
`.meta`、外部アセットの詳細、生成物の内部構成は省略している。

```text
client/
├── Assets/
│   ├── Baryonyx/
│   │   ├── Baryonyx.Runtime.asmdef
│   │   ├── AssemblyInfo.cs
│   │   ├── App/
│   │   │   ├── Runtime/                 起動、Providerの選択、前面・背面通知
│   │   │   ├── Scenes/                   Top.unity、Home.unity、Main.unity、Wireframe.unity
│   │   │   ├── Editor/                  シーンへの機能の配置
│   │   │   └── Tests/PlayMode/          起動シーンとプレビュー起動の検査
│   │   ├── Features/Account/
│   │   │   └── Runtime/                Google認証の契約とUMoth接続、サーバーのセッションとログインAPI
│   │   ├── Features/ExerciseRewards/
│   │   │   └── Runtime/                ルーン請求・履歴・残高のAPI
│   │   ├── Features/Health/
│   │   │   ├── Runtime/
│   │   │   │   ├── HealthContracts.cs   取得結果とProviderの契約
│   │   │   │   ├── HealthConnectProvider.cs
│   │   │   │   ├── HealthConnectionSettings.cs
│   │   │   │   ├── Presentation/        表示状態、View、配置、日別JSON
│   │   │   │   ├── Requirements/        利用条件の確認契約、判定、案内文
│   │   │   │   ├── Preview/             サンプルの認証結果と健康データ
│   │   │   │   └── Sync/                歩数の保存APIと、ログイン・保存・ルーン請求の実行順
│   │   │   ├── UI/                     画面Prefab
│   │   │   ├── Data/                   接続設定アセット
│   │   │   ├── Editor/                 専用アセット生成とビルド前検査
│   │   │   └── Tests/
│   │   │       ├── EditMode/
│   │   │       │   ├── Presentation/
│   │   │       │   ├── Requirements/
│   │   │       │   └── Preview/
│   │   │       └── PlayMode/Presentation/
│   │   ├── Features/Combat/             画面に依存しない戦闘計算と試作カタログ
│   │   │   ├── Runtime/                共通時計、行動、HP、ダウン、勝敗。Baryonyx.Combat.asmdef
│   │   │   └── Tests/EditMode/          計算・時間・再開の検査
│   │   ├── Features/Wireframe/          冒険・戦闘・歩数の操作試作
│   │   │   ├── Runtime/
│   │   │   │   ├── Flow/               画面遷移、所持状態、戦闘結果の反映
│   │   │   │   └── Presentation/       入力、各画面の表示、演出、SafeArea
│   │   │   ├── UI/                      専用Prefabと画像
│   │   │   ├── Data/                    仮のキャラ・武器
│   │   │   ├── Editor/                  専用アセットの生成
│   │   │   └── Tests/                   EditModeとPlayMode
│   │   ├── Shared/
│   │   │   ├── Networking/              ゲームサーバーへのHTTP送信
│   │   │   └── UI/                      複数画面で使う共通UIプレハブとフォント
│   │   ├── Editor/
│   │   │   ├── Baryonyx.Editor.asmdef
│   │   │   ├── AnalyzerProjectSettings.cs
│   │   │   └── CI/                     コンパイル検査、シーン選択、APKビルド
│   │   └── Tests/
│   │       ├── EditMode/               アセンブリ定義と共通ビルド処理の検査
│   │       └── PlayMode/
│   │           ├── Baryonyx.PlayModeTests.asmdef
│   │           ├── Scenarios/          共通入力fixtureの検査、横断シナリオ
│   │           └── Support/            PlayModeの共通入力fixture
│   ├── Analyzers/                      固定版の解析ツール
│   ├── Plugins/Android/                GradleテンプレートとAndroidライブラリ
│   ├── Settings/                       既存テンプレートのURP設定
│   ├── TextMesh Pro/                   外部パッケージのアセット
│   └── DevCaptures/                    Git対象外の検証画像
├── Packages/                           Unityパッケージの依存管理
├── ProjectSettings/                    Unityプロジェクトの設定
├── ci/
│   └── health-native/                  同じAndroidソースの単体検証用Gradle設定
├── Builds/                             Git対象外のビルド成果物
├── Logs/                               Git対象外のログ
└── TestResults/                        Git対象外のテスト結果
```

`Assets/Scripts/`、`Assets/Editor/`、`Assets/Tests/`、`Assets/Scenes/` にあった自作コード・アセンブリ定義・起動シーンは、上記の配置へ移行した。
旧 `SampleScene.unity` はGUIDを保って `App/Scenes/Main.unity` へ移し、ビルド対象とテストの参照も更新した。
`Assets/Resources/` など外部パッケージが利用する配置や、Unityテンプレートから引き継いだ設定は、参照元と用途を確認して扱う。

## 起動と機能の責務

| 配置・入口 | 所有する処理 |
|---|---|
| [App/Runtime/HealthScreenBootstrap](../../client/Assets/Baryonyx/App/Runtime/HealthScreenBootstrap.cs) | Providerの選択、PresenterとViewの組み立て、初回処理、前面・背面・終了通知 |
| [App/Editor/HealthAppSceneSetup](../../client/Assets/Baryonyx/App/Editor/HealthAppSceneSetup.cs) | HealthのPrefab・設定とAppの起動オブジェクト、EventSystemをシーンへ配置する |
| [Health/Runtime/Presentation/](../../client/Assets/Baryonyx/Features/Health/Runtime/Presentation) | Presenterの状態遷移、Viewの入力と文言、SafeArea、一覧とJSON詳細 |
| [Health/Runtime/Requirements/](../../client/Assets/Baryonyx/Features/Health/Runtime/Requirements) | 利用条件の確認インターフェース、判定結果、表示する案内 |
| [Health/Runtime/Preview/](../../client/Assets/Baryonyx/Features/Health/Runtime/Preview) | EditorとAndroid以外の環境に返すサンプルデータ |
| [Health/Runtime/Sync/](../../client/Assets/Baryonyx/Features/Health/Runtime/Sync) | 歩数の保存APIと、ログイン・保存・ルーン請求を順に行う `HealthServerSync`。Android実機でサーバーURLが設定されている場合だけ、Presenterから呼ぶ |
| [Account/Runtime/](../../client/Assets/Baryonyx/Features/Account/Runtime) | Google認証の契約とUMoth接続、サーバーのセッション、ログイン・ログアウトAPI |
| [ExerciseRewards/Runtime/](../../client/Assets/Baryonyx/Features/ExerciseRewards/Runtime) | ルーン請求・履歴・残高のAPIと応答の型 |
| [Shared/Networking/](../../client/Assets/Baryonyx/Shared/Networking) | ゲームサーバーのURL検証、HTTP送信、失敗時の例外。パスと入出力の型は各機能が持つ |
| [Health/Editor/HealthScreenAssets](../../client/Assets/Baryonyx/Features/Health/Editor/HealthScreenAssets.cs) | Health専用のPrefab・設定アセットと、共有フォントを生成する |

HealthのPresenterは、認証・権限・取得結果に応じた画面状態と、次に実行する操作を決める。
[HealthScreenOperations](../../client/Assets/Baryonyx/Features/Health/Runtime/Presentation/HealthScreenOperations.cs) は多重起動の防止、キャンセル、OS画面からの前面復帰待ち、操作世代と寿命を管理する。
通常の背面移行では取得を中断し、OSの認証・権限画面は結果と前面復帰を待つ。
設定画面への移動は、Presenterが未移動・移動待ち・復帰待ちの状態で管理する。

Viewの描画は操作ボタン、状態文言、日別一覧、閲覧位置に分ける。
[HealthJsonDetails](../../client/Assets/Baryonyx/Features/Health/Runtime/Presentation/HealthJsonDetails.cs) がJSONモーダルの開閉、コピー、スクロールの初期化、選択の復元を担当する。
これらはHealth専用の通常のC#クラスとし、Prefabの参照はViewから渡す。

AppのRuntimeはHealth機能を組み立て、HealthのRuntimeはAppへ依存しない。
依存方向はApp → Wireframe → Health → ExerciseRewards → Account → Shared、Wireframe → Combatとし、逆向きに参照しない。
ルーンの残高と請求結果は現在HealthScreenPresenterが保持しており、報酬画面を独立させる時点でExerciseRewards側の表示状態へ移す。
AppのEditor処理はAppの起動処理とHealthのアセット定義を参照し、HealthのEditor処理はAppのオブジェクトを生成しない。
`Baryonyx/App/Attach Health Screen To Current Scene` メニューで、保存済みの現在のシーンへ画面を配置する。
専用アセットの生成メニューは `Baryonyx/Health/Create Screen Assets` である。

プレビューでは `HealthScreenView.Bind(presenter, preview: true)` を使う。
通常表示とプレビュー表示の文言はViewの同じ描画処理で決まり、Appからの表示上書きやイベント購読順に依存しない。
認証・健康データのサンプル応答はProviderが返し、自動テストの固定データはテスト側に置く。
機能の詳しい動作は [健康データの機能文書](../features/health-data.md) を参照する。

WireframeもAppがSession・View・試作データを組み立てる。
[HealthRuntime](../../client/Assets/Baryonyx/App/Runtime/HealthRuntime.cs)はMainとWireframeに共通するProvider選択とPresenterの寿命を管理する。
[WireframeBootstrap](../../client/Assets/Baryonyx/App/Runtime/WireframeBootstrap.cs)は健康データのPresenterを歩数画面へ渡し、前面・背面を通知する。
サーバー同期はHealthRuntimeを通じてMainと同じ条件で行う。
[WireframeSession](../../client/Assets/Baryonyx/Features/Wireframe/Runtime/Flow/WireframeSession.cs)は画面遷移と実行中の所持状態を管理する。
戦闘計算はCombatへ委譲し、Presentationは入力、通常ページ、戦闘、ダイアログ、歩数、演出と配置を分担する。
CombatはWireframe・App・Unityの画面へ依存しない。
健康データの並べ替え・集計はHealth内のHealthWeekSummaryへ置き、表示用の仮数値をゲームのSessionへ持ち込まない。
Editorの共通uGUI生成部品はWireframeScreenAssetsに置き、ページ・戦闘・歩数の組み立ては用途別のファイルへ分ける。
起動・対象画面・操作方法は[画面ワイヤー](../features/game-wireframe.md)を参照する。

## アセンブリとテスト

フォルダーは変更する用途で分け、アセンブリはコンパイル対象と参照先の違いで分ける。
アセンブリ名とGUIDは移行前から維持している。

| アセンブリ | 定義ファイル | 所属するコード |
|---|---|---|
| `Baryonyx.Combat` | [Features/Combat/Runtime/Baryonyx.Combat.asmdef](../../client/Assets/Baryonyx/Features/Combat/Runtime/Baryonyx.Combat.asmdef) | Unityに依存しない戦闘計算。`noEngineReferences` でUnityEngineを参照させない |
| `Baryonyx.Runtime` | [Baryonyx/Baryonyx.Runtime.asmdef](../../client/Assets/Baryonyx/Baryonyx.Runtime.asmdef) | Combat以外のAppと機能の製品コード |
| `Baryonyx.Editor` | [Baryonyx/Editor/Baryonyx.Editor.asmdef](../../client/Assets/Baryonyx/Editor/Baryonyx.Editor.asmdef) | 共通・App・機能のEditor専用処理 |
| `Baryonyx.EditModeTests` | [Baryonyx/Tests/EditMode/Baryonyx.EditModeTests.asmdef](../../client/Assets/Baryonyx/Tests/EditMode/Baryonyx.EditModeTests.asmdef) | ロジックとビルド設定の検査 |
| `Baryonyx.PlayModeTests` | [Baryonyx/Tests/PlayMode/Baryonyx.PlayModeTests.asmdef](../../client/Assets/Baryonyx/Tests/PlayMode/Baryonyx.PlayModeTests.asmdef) | Appの起動、機能の画面操作、共通入力fixture |

Combat以外のRuntimeは、上位の `Baryonyx.Runtime.asmdef` に所属する。
Combatは参照先がUnityを含まない点で他と異なるため、独立したアセンブリにした。
App・機能のEditorとテストには `.asmref` を置き、対応するアセンブリへ所属させる。
`App/Editor/` や機能内の `Tests/` にC#を置くときは、上位のRuntimeへ混入しないようアセンブリ境界を確認する。
Combat以外の依存方向は設計上の規則であり、単一のRuntimeアセンブリではコンパイラーによる分離は行っていない。
機能の境界が固まり、参照先の違いが生じた時点で、同じ基準でアセンブリを分ける。

CIのテスト対象は従来どおり `Baryonyx.EditModeTests` と `Baryonyx.PlayModeTests` である。
実行と0件の検出、入力シナリオ、テスト用Resourcesの後始末は [UnityのテストとCI](client-testing.md) に従う。

## 配置を増やすときの基準

必要になったフォルダーだけを作る。
複数機能で共有するコードやUIは `Baryonyx/Shared/`、アプリ全体の自作設定は `Baryonyx/Settings/` に置く。
機能専用の小さな端末接続処理は機能内に置き、複数機能で共有する場合や対応OSが増える場合に `Baryonyx/Platform/` への分離を判断する。
Unityが使わない制作元が必要になった場合だけ `client/ArtSource/` を設ける。

### コードとアセットの配置

- `Runtime/` と `Editor/` は、それぞれ製品の実装とEditor専用処理を所有する。機能専用のEditor拡張はその機能内、共通のビルド処理は `Baryonyx/Editor/` に置く。
- 画面専用のPrefab・画像・アニメーションは機能内の `UI/` に置く。少数なら同階層に並べ、増えた場合だけ種類や画面部品で分ける。
- モデル・音声・マテリアル・機能専用シーンも所有する機能内に置く。キャラクターなど、一緒に編集する対象単位でまとめてよい。
- ScriptableObjectのクラスは `Runtime/`、そのインスタンスは `Data/` に置く。アプリ全体の設定アセットは `Baryonyx/Settings/`、Unity自身の設定は `ProjectSettings/` で管理する。
- OS・端末・外部SDKを呼び出す自作の接続処理は `Platform/` を基本とし、機能専用で小さいものは機能内に置いてよい。外部プラグイン本体はUPMまたは配布元が指定する場所に置く。
- `Resources`・`StreamingAssets`・`Plugins` などの特殊フォルダーは、Unityやプラグインが要求する用途・位置でのみ使う。
- Unityに読み込ませる必要のない制作元ファイルは、必要に応じて `ArtSource/` へ置く。生成コードは生成元と手順を明確にし、ビルド・テストの出力やキャッシュは実装と分ける。

### テストとアセンブリの境界

- 実装とテストは機能フォルダー内で近接させ、`Foo.cs` と `FooTests.cs` は別フォルダーに置く。実装・Editor専用コード・テストを `.asmdef` などで分離する。
- 機能ごとの配置とアセンブリ分割は別に判断する。各機能のフォルダーを作るだけでアセンブリを細分化せず、参照関係やコンパイル対象の違いに応じて境界を設ける。
- EditMode／PlayModeは実行環境、単体／シナリオは検証範囲として区別する。機能内のシナリオはその機能の `Tests/`、Appの起動検査は `App/Tests/PlayMode/`、横断シナリオは `Baryonyx/Tests/PlayMode/Scenarios/` を基本とする。
- テスト専用のシーン・Prefab・固定データは利用するテストの近くに置き、複数テストで共有する補助だけを `Support/` に置く。補助コードも対応するテスト用アセンブリに所属させる。
- `Tests/` という名前だけで本番ビルドから除外されるとは扱わない。テスト用アセンブリの設定に加え、本番シーンからの参照、ビルド対象シーン、アセットの配信設定を確認する。
- 配置やアセンブリを変更したら、テスト検出とCIのアセンブリ指定を併せて確認する。

## AndroidとUnity外の検証処理

自作のAndroid連携ソースは [BaryonyxHealth.androidlib](../../client/Assets/Plugins/Android/BaryonyxHealth.androidlib) に置き、UnityのAndroidビルドへ組み込む。
[ci/health-native/settings.gradle](../../client/ci/health-native/settings.gradle) は、その同じソースを検証用Gradleプロジェクトから参照する。
製品ソースをCI側へ複製しない。
外部プラグイン本体はUPMまたは配布元が指定する配置で管理する。

Androidの [HealthRecordCatalog](../../client/Assets/Plugins/Android/BaryonyxHealth.androidlib/src/main/kotlin/com/baryonyx/health/HealthRecordCatalog.kt) は、健康データの型ごとにJSON項目名、SDKの型、時刻の取り出し方、値の変換を一つの定義へまとめる。
読み取り権限はその定義から導出する。
[HealthRecords](../../client/Assets/Plugins/Android/BaryonyxHealth.androidlib/src/main/kotlin/com/baryonyx/health/HealthRecords.kt) は部分許可、ページ取得、取得件数の上限、日別の振り分けを担当する。
型を追加するときは、その値・単位・日付境界と権限のテストも追加する。

Unity内のビルド処理は `Baryonyx/Editor/CI/`、Unityの実行・結果検査・公開を補助する処理は `client/ci/` に置く。
Windows用の手動実行ショートカットは、[ルートの作業ルール](../../AGENTS.md#手動実行用ショートカット) に従って `shortcuts/` で管理する。
