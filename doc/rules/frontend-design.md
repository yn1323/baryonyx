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
│   │   │   ├── Scenes/Main.unity         起動シーン
│   │   │   ├── Editor/                  シーンへの機能の配置
│   │   │   └── Tests/PlayMode/          起動シーンとプレビュー起動の検査
│   │   ├── Features/Health/
│   │   │   ├── Runtime/
│   │   │   │   ├── HealthContracts.cs   取得結果とProviderの契約
│   │   │   │   ├── HealthConnectProvider.cs
│   │   │   │   ├── HealthConnectionSettings.cs
│   │   │   │   ├── Authentication/      Google認証の契約とUMoth接続
│   │   │   │   ├── Presentation/        表示状態、View、配置、日別JSON
│   │   │   │   ├── Requirements/        利用条件の確認契約、判定、案内文
│   │   │   │   ├── Preview/             サンプルの認証結果と健康データ
│   │   │   │   └── Sync/                サーバー同期、API接続、同期用の契約
│   │   │   ├── UI/                     画面Prefabとフォント
│   │   │   ├── Data/                   接続設定アセット
│   │   │   ├── Editor/                 専用アセット生成とビルド前検査
│   │   │   └── Tests/
│   │   │       ├── EditMode/
│   │   │       │   ├── Presentation/
│   │   │       │   ├── Requirements/
│   │   │       │   ├── Preview/
│   │   │       │   └── Sync/
│   │   │       └── PlayMode/Presentation/
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
│   ├── TutorialInfo/                   Unityテンプレートの説明用アセット
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
| [Health/Runtime/Sync/](../../client/Assets/Baryonyx/Features/Health/Runtime/Sync) | サーバーのセッション、API要求、同期。現在の起動シーンからは呼ばない |
| [Health/Editor/HealthScreenAssets](../../client/Assets/Baryonyx/Features/Health/Editor/HealthScreenAssets.cs) | Health専用のPrefab・フォント・設定アセットを生成する |

AppのRuntimeはHealth機能を組み立て、HealthのRuntimeはAppへ依存しない。
AppのEditor処理はAppの起動処理とHealthのアセット定義を参照し、HealthのEditor処理はAppのオブジェクトを生成しない。
`Baryonyx/App/Attach Health Screen To Current Scene` メニューで、保存済みの現在のシーンへ画面を配置する。
専用アセットの生成メニューは `Baryonyx/Health/Create Screen Assets` である。

プレビューでは `HealthScreenView.Bind(presenter, preview: true)` を使う。
通常表示とプレビュー表示の文言はViewの同じ描画処理で決まり、Appからの表示上書きやイベント購読順に依存しない。
認証・健康データのサンプル応答はProviderが返し、自動テストの固定データはテスト側に置く。
機能の詳しい動作は [健康データの機能文書](../features/health-data.md) を参照する。

## アセンブリとテスト

フォルダーは変更する用途で分け、アセンブリはコンパイル対象と参照先の違いで分ける。
アセンブリ名とGUIDは移行前から維持している。

| アセンブリ | 定義ファイル | 所属するコード |
|---|---|---|
| `Baryonyx.Runtime` | [Baryonyx/Baryonyx.Runtime.asmdef](../../client/Assets/Baryonyx/Baryonyx.Runtime.asmdef) | Appと機能の製品コード |
| `Baryonyx.Editor` | [Baryonyx/Editor/Baryonyx.Editor.asmdef](../../client/Assets/Baryonyx/Editor/Baryonyx.Editor.asmdef) | 共通・App・機能のEditor専用処理 |
| `Baryonyx.EditModeTests` | [Baryonyx/Tests/EditMode/Baryonyx.EditModeTests.asmdef](../../client/Assets/Baryonyx/Tests/EditMode/Baryonyx.EditModeTests.asmdef) | ロジックとビルド設定の検査 |
| `Baryonyx.PlayModeTests` | [Baryonyx/Tests/PlayMode/Baryonyx.PlayModeTests.asmdef](../../client/Assets/Baryonyx/Tests/PlayMode/Baryonyx.PlayModeTests.asmdef) | Appの起動、機能の画面操作、共通入力fixture |

AppとHealthのRuntimeは、上位の `Baryonyx.Runtime.asmdef` に所属する。
App・HealthのEditorとテストには `.asmref` を置き、対応するアセンブリへ所属させる。
`App/Editor/` や機能内の `Tests/` にC#を置くときは、上位のRuntimeへ混入しないようアセンブリ境界を確認する。
AppとHealthの依存方向は設計上の規則であり、現在の単一Runtimeアセンブリではコンパイラーによる分離は行っていない。

CIのテスト対象は従来どおり `Baryonyx.EditModeTests` と `Baryonyx.PlayModeTests` である。
実行と0件の検出、入力シナリオ、テスト用Resourcesの後始末は [UnityのテストとCI](client-testing.md) に従う。

## 配置を増やすときの基準

必要になったフォルダーだけを作る。
複数機能で共有するコードやUIができた時点で `Baryonyx/Shared/`、アプリ全体の自作設定ができた時点で `Baryonyx/Settings/` を設ける。
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

Unity内のビルド処理は `Baryonyx/Editor/CI/`、Unityの実行・結果検査・公開を補助する処理は `client/ci/` に置く。
Windows用の手動実行ショートカットは、[ルートの作業ルール](../../AGENTS.md#手動実行用ショートカット) に従って `shortcuts/` で管理する。
