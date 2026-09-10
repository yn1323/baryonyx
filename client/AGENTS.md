# AGENTS.md

`client/` の作業には、[ルートのAGENTS.md](../AGENTS.md)と以下の指示を適用する。
C#編集はZed、Unityの操作・検証はUnity CLIを基本とする。
Unityのバージョンは [ProjectSettings/ProjectVersion.txt](ProjectSettings/ProjectVersion.txt) に従う。

## ディレクトリ構成と狙い

自作のコードとアセットは `Assets/Baryonyx/` にまとめ、機能の変更に必要な実装・画面・データ・テストを同じ機能からたどれるようにする。
以下は新規配置・移行時の基準であり、未作成の配置先を含む。
既存の `Assets/Scripts/`、`Assets/Tests/`、`Assets/Editor/` などは、参照とCIの実行対象を保ちながら段階的に移行する。

```text
client/
├── Assets/
│   ├── Baryonyx/
│   │   ├── App/
│   │   │   ├── Runtime/           起動と機能の組み立て
│   │   │   └── Scenes/            起動・アプリ全体のシーン
│   │   ├── Features/
│   │   │   └── <Feature>/
│   │   │       ├── Runtime/       機能の実装
│   │   │       ├── UI/            専用のPrefab・画像・表示用アセット
│   │   │       ├── Data/          専用の設定・データアセット
│   │   │       ├── Editor/        専用のEditor拡張
│   │   │       └── Tests/
│   │   │           ├── EditMode/
│   │   │           └── PlayMode/
│   │   ├── Platform/             OS・端末・外部SDKとの接続処理
│   │   ├── Shared/
│   │   │   ├── Runtime/          複数機能で使うコード
│   │   │   └── UI/               共通UI・フォント・テーマ
│   │   ├── Settings/             アプリ全体の設定アセット
│   │   ├── Editor/               共通のEditor拡張・ビルド処理
│   │   └── Tests/
│   │       ├── EditMode/
│   │       ├── PlayMode/
│   │       │   └── Scenarios/    複数機能をまたぐ操作シナリオ
│   │       └── Support/          共通のテスト補助
│   ├── Analyzers/                解析ツール
│   ├── <外部アセットの配置先>/    配布元が指定する構成を尊重
│   └── DevCaptures/              一時キャプチャ
├── Packages/                     Unityパッケージの依存管理
├── ProjectSettings/              Unityプロジェクトの設定
├── ci/                           Unityの実行・検証・公開の補助
└── ArtSource/                    必要な場合のみ、Unityが使わない制作元
```

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
- EditMode／PlayModeは実行環境、単体／シナリオは検証範囲として区別する。機能内のシナリオはその機能の `Tests/`、横断シナリオは `Baryonyx/Tests/PlayMode/Scenarios/` を基本とする。
- テスト専用のシーン・Prefab・固定データは利用するテストの近くに置き、複数テストで共有する補助だけを `Support/` に置く。補助コードも対応するテスト用アセンブリに所属させる。
- `Tests/` という名前だけで本番ビルドから除外されるとは扱わない。テスト用アセンブリの設定に加え、本番シーンからの参照、ビルド対象シーン、アセットの配信設定を確認する。
- 配置やアセンブリを変更したら、テスト検出とCIのアセンブリ指定を併せて確認する。

## Unity CLI

Unity CLIの利用手順は、Unityプラグインの `unity:unity-cli` スキルに従う。
接続に使うUnity Pipelineは、[Packages/manifest.json](Packages/manifest.json)に含まれている。
接続先がこのリポジトリの `client/` であることを確認する。

## Unityの操作と検証

- C#変更後は、再コンパイルの完了とConsoleのエラーを確認する。
- 変更した動作に対応するテストを実行する。テスト0件は合格として扱わない。
- PlayModeの開始・停止やシーンの切り替えは、実行中の作業を確認してから行う。
- アセットの移動・名前変更・削除はUnityの機能を使い、対応する `.meta` とGUIDの整合を保つ。
- 画面変更後はGameビューを撮影し、保存した画像を開いて確認する。Overlay UIを含める場合はPlayModeで `capture_game_view --source screen` を使う。
- 検証画像は `Assets/DevCaptures/` に保存する。このフォルダと対応する `.meta` はGit除外済み。
