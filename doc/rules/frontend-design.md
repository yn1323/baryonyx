---
id: rule-frontend-design
type: reference
status: 運用中
updated: 2026-10-04
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
│   │   │   ├── Runtime/                 起動、Providerの選択、前面・背面通知、シーン名（SceneNames）
│   │   │   ├── Scenes/                   Top.unity、Home.unity、Showcase.unity、Battle.unity
│   │   │   │   ├── Guide/                案内人の画面（Pub・Shop・Temple・TravelOffice）
│   │   │   │   └── Debug/                デバッグ用のシーン（BattleInspect・CardSkillLab・EnemyLab）
│   │   │   ├── Editor/                  シーンへの機能の配置、HD-2Dの演出と3Dの舞台のシーンへの配置
│   │   │   └── Tests/                   EditMode（サーバーの接続先）とPlayMode（起動・案内人・展示室のシーン）
│   │   ├── Features/Account/
│   │   │   ├── Runtime/                Google認証の契約とUMoth接続、ゲストの秘密値、サーバーのセッションとログインAPI
│   │   │   └── Tests/EditMode/
│   │   ├── Features/ExerciseRewards/
│   │   │   └── Runtime/                ルーン請求・履歴・残高のAPI
│   │   ├── Features/Health/
│   │   │   ├── Runtime/
│   │   │   │   ├── HealthContracts.cs   取得結果とProviderの契約
│   │   │   │   ├── HealthConnectProvider.cs
│   │   │   │   ├── HealthConnectionSettings.cs
│   │   │   │   ├── Link/                連携の確認・許可の要求・同期、Topの起動処理、連携モーダル
│   │   │   │   ├── Presentation/        接続・取得の状態遷移と操作の管理、日別JSON
│   │   │   │   ├── Requirements/        利用条件の確認契約、判定、案内文
│   │   │   │   ├── Preview/             サンプルの認証結果と健康データ
│   │   │   │   └── Sync/                歩数の保存・取得APIと、ゲストのセッション・保存・ルーン請求の実行順
│   │   │   ├── UI/                     連携モーダルのPrefab
│   │   │   ├── Data/                   接続設定アセット
│   │   │   ├── Editor/                 接続設定アセットの生成
│   │   │   └── Tests/EditMode/
│   │   │       ├── Presentation/
│   │   │       ├── Requirements/
│   │   │       └── Preview/
│   │   ├── Features/Home/               ホーム画面（野営地）のモック
│   │   │   ├── Runtime/                表示状態の計算、View、Presenter、仮データ
│   │   │   ├── UI/                      専用Prefabとコードで生成するドット絵
│   │   │   ├── Data/                    仮データのアセット
│   │   │   ├── Editor/                  Prefabと画像の生成
│   │   │   └── Tests/                   EditModeとPlayMode
│   │   ├── Features/Combat/             画面に依存しない戦闘計算と試作カタログ、戦闘画面のモック
│   │   │   ├── Runtime/                共通時計、行動、HP、ダウン、勝敗。Baryonyx.Combat.asmdef
│   │   │   ├── Presentation/           戦闘画面のモックの表示と操作（Baryonyx.Runtime）
│   │   │   ├── UI/                      モックのPrefabと、Codexで生成して縮小した画像
│   │   │   ├── Editor/                  モックのPrefabの生成
│   │   │   └── Tests/                   EditMode（計算・時間・再開）とPlayMode（モックの操作）
│   │   ├── Features/Tavern/             酒場（パーティの編成・育成）の画面。工房 Workshop・神殿 Temple・旅の案内所 TravelOffice も同じ構成
│   │   │   ├── UI/                      画面のPrefab、案内人・背景・メニューのアイコンの画像
│   │   │   ├── Data/                    画面の中身（案内人・メニュー・仮データ）の定義アセット
│   │   │   └── Editor/                  仮データと画面の生成
│   │   ├── Features/Training/           酒場の育成（1人の詳細と、重ねて開くレベルアップ）のモック。Runtime・Editor・Data・UI・Tests
│   │   ├── Features/Wireframe/          削除した操作試作の画像だけを保管
│   │   │   └── UI/Art/                  出発地点・坑道の背景とボタン・パネルの枠
│   │   ├── Shared/
│   │   │   ├── Art/                     ゲーム内の対象の画像。カタログの分類ごとに分ける
│   │   │   │   ├── Characters/          キャラクター
│   │   │   │   ├── Stages/              探索する場所の背景（森、TopとHomeのダンジョンの広間）と3Dの舞台
│   │   │   │   │   ├── DungeonHall/     3Dのダンジョンの広間（今は画面で使わない）
│   │   │   │   │   ├── DuskHighland/    戦闘の3Dの夕暮れの高原のテクスチャ・マテリアル・Prefab
│   │   │   │   │   ├── ForestGlade/     Homeの3Dの昼の森の野営地のテクスチャ・マテリアル・Prefab
│   │   │   │   │   ├── ForestRuins/     3Dの森の遺跡の石舞台（今は画面で使わない）
│   │   │   │   │   ├── StarlitGate/     Topの3Dの星空の夜の山の門のテクスチャ・マテリアル・Prefab
│   │   │   │   │   └── Editor/          3Dの舞台の組み立て（StageSetAssets）
│   │   │   │   └── GameResources/       素材・通貨（ルーンのアイコン）
│   │   │   ├── Networking/              ゲームサーバーへのHTTP送信
│   │   │   ├── UI/                      複数画面で使う共通UI（下の表）
│   │   │   └── VFX/HD2D/                HD-2Dの演出と3Dの舞台の共通部品（カメラ、キャラの板、2Dのときだけの部品、舞台のレンズ、光の筋）。Editor/に共通アセットと舞台の部品の生成とInspector、Tests/EditMode/
│   │   ├── Editor/
│   │   │   ├── Baryonyx.Editor.asmdef
│   │   │   ├── AnalyzerProjectSettings.cs
│   │   │   ├── ScreenScenes.cs         画面のシーンの作り直し（カメラ・入力・Build Settings）
│   │   │   ├── Art/                    .asepriteの読み込みの補正（キャンバスの大きさの画像を追加）、Driveとの受け渡し、画像の読み込みと保存（ArtAssets）
│   │   │   ├── UI/                     画面のPrefabをコードで組み立てる部品（UiBuild）と、共通の形・影・アイコンの画像の生成（UiArt）
│   │   │   └── CI/                     コンパイル検査、シーン選択、APKビルド
│   │   └── Tests/
│   │       ├── EditMode/               アセンブリ定義と共通ビルド処理の検査
│   │       └── PlayMode/
│   │           ├── Baryonyx.PlayModeTests.asmdef
│   │           ├── Scenarios/          共通入力fixtureの検査、横断シナリオ
│   │           └── Support/            共通入力fixture、シーンテストの読み込み・待機・後片付け、端末とサーバーの代役
│   ├── Analyzers/                      固定版の解析ツール
│   ├── Plugins/Android/                GradleテンプレートとAndroidライブラリ
│   ├── Settings/                       既存テンプレートのURP設定
│   ├── TextMesh Pro/                   外部パッケージのアセット
│   └── DevCaptures/                    Git対象外の検証画像
├── ArtSource/                          Unityに読み込ませない制作元。Shared/Art/と同じ分類で分ける
│   └── UI/                             UI試作画像の生成指示の記録
├── Packages/                           Unityパッケージの依存管理
├── ProjectSettings/                    Unityプロジェクトの設定
├── ci/
│   └── health-native/                  同じAndroidソースの単体検証用Gradle設定
├── Builds/                             Git対象外のビルド成果物
├── Logs/                               Git対象外のログ
└── TestResults/                        Git対象外のテスト結果
```

`Shared/UI/` には、複数の画面が使うUIを部品ごとのフォルダーで置く。

| フォルダー | 所有するもの |
|---|---|
| `Art/` | 角丸・円・カプセルの形、画面の端の影、足元の影、文字の下地、設定の歯車のアイコン。白で描き、使う側で色を付ける |
| `Fonts/` | DotGothic16と、影つきの文字のマテリアル `TextShadow.mat` |
| `Buttons/` | 押すとアイコンと文字も暗くなるボタン |
| `ResponsiveLayout/` | 背景の比率の維持、Safe Areaへの追従、狭い画面での中央の層の縮小（`WorldLayerFit`）、ドット絵を整数倍に保つ `PixelPerfectRawImage` |
| `SceneTransition/` | 画面を覆う遷移演出と、覆ってからシーンを読み込む `SceneLoader` |
| `Toast/` | 知らせをしばらく表示して消す `FadingMessage` |
| `TranslucentTextPanel/` | 半透明の文字パネル。`Editor/` にPrefabの生成 |
| `Cards/` | カードスキルの書き方 `CardText`（種類の行と、説明の数字を何をするかで色分けしたリッチテキスト）。戦闘のカードと酒場のカードスキルが使う |
| `GuideMenu/` | 案内人がいる画面の共通部品。項目のパネルは `IGuideBackHandler` を付けると、戻る操作を先に受け取れる |
| `Tests/` | 上記の部品のEditMode・PlayModeテスト（`GuideMenu/` は自分の `Tests/` を持つ） |

`Assets/Scripts/`、`Assets/Editor/`、`Assets/Tests/`、`Assets/Scenes/` にあった自作コード・アセンブリ定義・起動シーンは、上記の配置へ移行した。
旧 `SampleScene.unity` はGUIDを保って `App/Scenes/Main.unity` へ移したが、2026-09-24に操作試作とともに削除した。
`Assets/Resources/` など外部パッケージが利用する配置や、Unityテンプレートから引き継いだ設定は、参照元と用途を確認して扱う。

## 起動と機能の責務

| 配置・入口 | 所有する処理 |
|---|---|
| [App/Runtime/GameServices](../../client/Assets/Baryonyx/App/Runtime/GameServices.cs) | 実行環境に応じたProviderと保存先の選択。アプリの終了までTopとHomeで同じ接続を共有する。テストでは差し替える |
| [Health/Runtime/Link/](../../client/Assets/Baryonyx/Features/Health/Runtime/Link) | 連携の確認・許可の要求・同期（`HealthStepLink`）、Topの起動処理の状態（`HealthStartupFlow`）、連携モーダルの描画 |
| [App/Runtime/HealthRuntime](../../client/Assets/Baryonyx/App/Runtime/HealthRuntime.cs) | 実行環境に応じたProviderの選択、Presenterの生成、初回の接続開始、破棄。現在はどのシーンからも使っていない |
| [Health/Runtime/Presentation/](../../client/Assets/Baryonyx/Features/Health/Runtime/Presentation) | Presenterの状態遷移、操作の多重起動防止と前面復帰待ち、日別JSONの解析 |
| [Health/Runtime/Requirements/](../../client/Assets/Baryonyx/Features/Health/Runtime/Requirements) | 利用条件の確認インターフェース、判定結果、表示する案内 |
| [Health/Runtime/Preview/](../../client/Assets/Baryonyx/Features/Health/Runtime/Preview) | EditorとAndroid以外の環境に返すサンプルデータ |
| [Health/Runtime/Sync/](../../client/Assets/Baryonyx/Features/Health/Runtime/Sync) | 歩数の保存APIと、ログイン・保存・ルーン請求を順に行う `HealthServerSync`。Android実機でサーバーURLが設定されている場合だけ、Presenterから呼ぶ |
| [Account/Runtime/](../../client/Assets/Baryonyx/Features/Account/Runtime) | Google認証の契約とUMoth接続、サーバーのセッション、ログイン・ログアウトAPI |
| [ExerciseRewards/Runtime/](../../client/Assets/Baryonyx/Features/ExerciseRewards/Runtime) | ルーン請求・履歴・残高のAPIと応答の型 |
| [Shared/Networking/](../../client/Assets/Baryonyx/Shared/Networking) | ゲームサーバーのURL検証、HTTP送信、失敗時の例外。パスと入出力の型は各機能が持つ |
| [Health/Editor/HealthScreenAssets](../../client/Assets/Baryonyx/Features/Health/Editor/HealthScreenAssets.cs) | 接続設定アセットと共有フォントを生成する |

HealthのPresenterは、認証・権限・取得結果に応じた画面状態と、次に実行する操作を決める。
[HealthScreenOperations](../../client/Assets/Baryonyx/Features/Health/Runtime/Presentation/HealthScreenOperations.cs) は多重起動の防止、キャンセル、OS画面からの前面復帰待ち、操作世代と寿命を管理する。
通常の背面移行では取得を中断し、OSの認証・権限画面は結果と前面復帰を待つ。
設定画面への移動は、Presenterが未移動・移動待ち・復帰待ちの状態で管理する。

健康データの画面（View・Prefab）は2026-09-24に削除した。
新しい画面から使うときは、HealthRuntimeでPresenterを生成し、画面側でPresenterの状態を描画する。

AppのRuntimeはHealth機能を組み立て、HealthのRuntimeはAppへ依存しない。
依存方向はApp → Health → ExerciseRewards → Account → Shared、App → Home → Sharedとし、逆向きに参照しない。
HomeはHealth・ExerciseRewardsを参照せず、Appの[HomeBootstrap](../../client/Assets/Baryonyx/App/Runtime/HomeBootstrap.cs)が仮データからPresenterを組み立てる。
今日の歩数はHomeが定義する `IHomeStepSource` を通して受け取り、Appの [HomeStepSource](../../client/Assets/Baryonyx/App/Runtime/HomeStepSource.cs) がHealthの `HealthStepLink` へつなぐ。
キャラクターや素材など、ゲーム内の対象の画像は[ゲーム内の対象の画像](#ゲーム内の対象の画像)に従って `Shared/Art/<分類>/` に置く。
ルーンの残高と請求結果は現在HealthScreenPresenterが保持しており、報酬画面を独立させる時点でExerciseRewards側の表示状態へ移す。
HealthのEditor処理はAppのオブジェクトを生成しない。
接続設定アセットの生成メニューは `Baryonyx/Health/Create Screen Assets` である。

EditorとAndroid以外では、HealthRuntimeがプレビュー用Providerを選び、`Preview` を `true` にする。
認証・健康データのサンプル応答はProviderが返し、自動テストの固定データはテスト側に置く。
機能の詳しい動作は [健康データの機能文書](../features/health-data.md) を参照する。

Combatの戦闘計算（`Runtime/`）はApp・Unityの画面へ依存しない。
戦闘画面のモック（`Presentation/`・`Editor/`・`UI/`）は `Baryonyx.Runtime` に属し、戦闘計算を参照しない。
モックは、ドット絵を整数倍に保つ `PixelPerfectRawImage`、形の画像、文字の影、画面の組み立て部品をHome・案内人の画面と共有する（[配置を増やすときの基準](#配置を増やすときの基準)）。
冒険・戦闘・歩数の操作試作（Wireframe）は2026-09-24に削除した。経緯と評価は[操作試作の記録](../features/game-wireframe.md)を参照する。

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
- 2つ以上の画面・機能が使うコードや画像は、2つ目が使い始める時点で `Shared/` へ移す。ほかの機能のフォルダーにある部品を借りたままにしない。移すときはUnityの移動機能でGUIDを保ち、名前空間とクラス名から元の機能名を外す。
- 共有部品のPrefab・画像を生成するEditor処理は、その部品のフォルダーの `Editor/` に置く（例：`Shared/VFX/HD2D/Editor/`）。部品を特定のシーンへ置く処理は、そのシーンを持つ側（Topなら `App/Editor/`）に置く。
- 画面のPrefabとシーンをコードで生成するときは、`Baryonyx/Editor/UI/` の組み立て部品（`UiBuild`・`UiArt`）、`Baryonyx/Editor/Art/ArtAssets`、`Baryonyx/Editor/ScreenScenes` を使い、同じ補助メソッドを機能ごとに書かない。
- シーンの名前は [SceneNames](../../client/Assets/Baryonyx/App/Runtime/SceneNames.cs)、遷移演出を挟むシーンの切り替えは [SceneLoader](../../client/Assets/Baryonyx/Shared/UI/SceneTransition/SceneLoader.cs) を使う。
- 画面専用のPrefab・画像・アニメーションは機能内の `UI/` に置く。少数なら同階層に並べ、増えた場合だけ種類や画面部品で分ける。
- モデル・音声・マテリアル・機能専用シーンも所有する機能内に置く。キャラクターなど、一緒に編集する対象単位でまとめてよい。
- ScriptableObjectのクラスは `Runtime/`、そのインスタンスは `Data/` に置く。アプリ全体の設定アセットは `Baryonyx/Settings/`、Unity自身の設定は `ProjectSettings/` で管理する。
- OS・端末・外部SDKを呼び出す自作の接続処理は `Platform/` を基本とし、機能専用で小さいものは機能内に置いてよい。外部プラグイン本体はUPMまたは配布元が指定する場所に置く。
- `Resources`・`StreamingAssets`・`Plugins` などの特殊フォルダーは、Unityやプラグインが要求する用途・位置でのみ使う。
- Unityに読み込ませる必要のない制作元ファイルは、必要に応じて `ArtSource/` へ置く。生成コードは生成元と手順を明確にし、ビルド・テストの出力やキャッシュは実装と分ける。
- 採用前の候補、確認用の拡大画像やGIF、下描きに使ったスクリプトは、リポジトリのルートの `output/`（Git対象外）に置く。

### ゲーム内の対象の画像

キャラ・敵・アイテム・武器・素材など、[データ索引](../catalog/README.md)に定義がある対象の画像は、使う画面が1つでも `Baryonyx/Shared/Art/<分類>/` に置く。
画像の持ち主は表示する画面ではなく対象そのものであり、同じ対象を複数の画面が表示するためである。
枠・影・ボタンのアイコンなど、画面専用のUI部品は従来どおり機能内の `UI/` に置く。

フォルダー名は、データ索引の分類名をPascalCaseにしたものとする。
`resources`（素材・通貨）だけは、Unityが特別に扱う `Resources` フォルダーと同名になるため `GameResources` とする。

| データ索引の分類 | 画像のフォルダー |
|---|---|
| `characters`・`enemies`・`items`・`weapons`・`armor` | `Characters`・`Enemies`・`Items`・`Weapons`・`Armor` |
| `skills`・`effects`・`stages` | `Skills`・`Effects`・`Stages` |
| `resources` | `GameResources` |
| その他の分類 | 分類名をPascalCaseにする |

- ドット絵の正本は、Unityで使う場所に `.aseprite` のまま置き、Gitで管理する（ルーンのアイコンは `Shared/Art/GameResources/IconRune.aseprite`）。インデックスカラーで保存し、色違いはパレットの差し替えで作る。PNGへは書き出さない。
- `.aseprite` は、Unity公式のAseprite Importer（`com.unity.2d.aseprite`）が読み込む。Asepriteで保存してUnity Editorへ戻ると読み込み直され、Aseprite本体がない環境（CIなど）でも同じ画像になる。
  - Importerは絵の周りの透明な部分を切り取り、余白をつけて1枚のテクスチャへ詰める。そのままでは大きさと位置がキャンバスと変わるため、[AsepriteCanvasImport](../../client/Assets/Baryonyx/Editor/Art/AsepriteCanvasImport.cs) が読み込みの最後に、切り取られた絵をキャンバスの位置へ戻したテクスチャと、フレームごとのSpriteを追加する。Importerが作るSpriteは、一覧に出ないよう非表示にする。
  - 複数フレームのファイルは、フレームを左上から右へ並べた1枚のテクスチャになる。Spriteの名前は、1フレームならファイル名、複数ならファイル名に `_` とフレーム番号（0から）を付けたものになる。
  - `Assets/Baryonyx/` の `.aseprite` は、読み込むたびに、Read/Writeを有効にし、SpriteRenderer向けのPrefabとAnimation Clipを作らない設定に揃える。フィルターとミップマップはInspectorまたは生成スクリプトで指定し、追加するテクスチャにも同じ設定を使う。
  - Prefab・データ・展示室は、追加したSpriteとテクスチャを参照する。生成スクリプトからは `AsepriteCanvasImport.LoadSprite`・`LoadTexture` で読み込む。`AssetDatabase.LoadAssetAtPath` では、Importerが作った余白つきのテクスチャや、切り取った絵のSpriteが返ることがある。
  - Importerが作るAnimation ClipはSpriteRendererの絵を切り替えるもので、uGUIのImageには使えない。UIでアニメーションを再生する方法は、最初のアニメーション素材を作るときに決める。
  - [AsepriteCanvasImportTests](../../client/Assets/Baryonyx/Tests/EditMode/AsepriteCanvasImportTests.cs) は、全ての `.aseprite` に、キャンバスと同じ大きさのSpriteがフレームの数だけあることを検査する。
- 1つの対象の画像が複数になったら、分類の下に対象名のフォルダーを作る（例：`Characters/Toma/`）。
- 画像を置いたら、対象の個別文書の `art_files` に画像のパス（`.aseprite` またはPNG）を書き、[比較索引](../art/visual-index.md)を再生成する。比較索引が、どの対象の画像がどこにあるかの目次になる。
- 探索する場所の背景は `Shared/Art/Stages/` に置く。2026-09-29に `Shared/Art/Dungeons/` から移し、TopとHomeが使うダンジョンの広間（旧 `App/Art/Top/TopDungeonBackground.png`）も `DungeonHall.png` として同じ場所へ移した。
- 3Dの舞台（[3Dの舞台](../art/hd2d-stage.md)）は、舞台ごとのフォルダー（`Stages/StarlitGate/`・`Stages/ForestGlade/`・`Stages/DuskHighland/` など）に、テクスチャの `.aseprite`、マテリアル、メッシュをまとめたアセット、Prefab、舞台のポストプロセス（`<舞台>Look.asset`）を置く。組み立てるEditor処理は `Stages/Editor/`、複数の舞台が使う部品（カメラ、キャラの板、舞台の部品の生成）は `Shared/VFX/HD2D/` に置き、舞台をシーンへ置く処理は `App/Editor/Hd2dStageSceneSetup` が持つ。

#### タブレットとの受け渡し

正本はGitで管理し、タブレットではGoogle Driveに置いた写しを編集する。
Driveには、`client/Assets/Baryonyx/` からの相対パスで置く（`Features/Home/UI/Art/IconCompass.aseprite` など）。
受け渡しは、Unityのメニューからユーザーが手動で行う（[AsepriteDriveSync](../../client/Assets/Baryonyx/Editor/Art/AsepriteDriveSync.cs)）。
AIは、検証時も含めてこのメニューを実行しない。

| メニュー | 動作 |
|---|---|
| `Baryonyx > Art > Choose Aseprite Drive Folder...` | Driveの中で `.aseprite` を置くフォルダーを選び、このPCの設定として保存する |
| `Baryonyx > Art > Copy Aseprite Sources to Drive` | `client/Assets/Baryonyx/` の `.aseprite` をDriveのフォルダーへコピーする |
| `Baryonyx > Art > Copy Aseprite Sources from Drive` | Driveのフォルダーの `.aseprite` を `client/Assets/Baryonyx/` へコピーし、Unityに読み込み直させる |

Driveのフォルダーのパスは、WindowsとmacOSで異なるため、PCごとに `client/UserSettings/BaryonyxAsepriteDrive.json`（Gitの対象外）へ保存する。
未設定のままコピーを実行すると、先にフォルダーを選ぶ画面を出す。
どのPCでも、Drive上の同じフォルダーを選ぶ。
受け渡しの記録と退避先は選んだフォルダーの隣に作るため、Driveの中に `.aseprite` 専用のフォルダー（例：`71_プロジェクト/baryonyx/Aseprite`）を作って選ぶ。

2026-09-29に、正本の置き場所を `client/ArtSource/` から `client/Assets/Baryonyx/` へ移した。
それより前にDriveへコピーした `.aseprite` は古い構成のままのため、Driveのフォルダーの中身と `<フォルダー名>-sync.tsv` を消してから、PCからコピーし直す。

- 実行すると、コピーの向きとフォルダーを示す確認のダイアログを出す。
- `.aseprite` 以外のファイルはコピーしない。片側にしかないファイルは、消したのか追加したのかを判断できないため削除せず、結果に示す。
- 受け渡したときの各ファイルのSHA-256を、Driveのフォルダーの隣の `<フォルダー名>-sync.tsv` に記録する。WindowsとmacOSで同じ記録を使う。コピー先がこの記録から変わっていないときだけ上書きする。
- コピー先も記録から変わっている、または記録がない状態で内容が異なるファイルは、両方で編集された可能性がある。上書きするかをダイアログで確認し、上書きする場合はコピー先の今のファイルを、Driveのフォルダーの隣の `<フォルダー名>-backup/<日時>/<PCまたはDrive>/` へ退避する。
- Driveからの取り込みのあとは、`git status` で差分を確認してコミットする。新しいファイルは、Unityが作る `.meta` も同じコミットに含める。

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
