# AGENTS.md

このファイルは、baryonyx全体に適用する作業上の制約と参照先を定める。

## 作業の基本

- ユーザーへの説明、文書、PR本文、レビューは日本語で書く。コードの識別子や既存の技術用語は維持する。
- 作業対象のコード、設定、関連文書を確認してから変更する。下位に `AGENTS.md` があれば併せて読む。
- 依頼の目的を満たす範囲で変更し、無関係な整理や共通基盤の追加へ広げない。
- ユーザーや並行作業の変更を保護し、依頼外の変更を上書き、復元、削除、ステージしない。
- 秘密値や個人情報を文書、ログ、コミットへ含めない。
- 未リリースのため、Migration、既存データとの互換性、旧バージョンとの互換性は考慮しない。DB不整合が発生した場合は、毎回データベースをリセットしてよい。
- サブエージェントは常に利用禁止とする。
- Unityの端末機能・外部サービス連携は、無料で利用できる既存のUnity用プラグインの利用を基本とする。採用前に必要な機能、Unity・対象OSへの対応、保守状況、ライセンス・費用を確認する。Kotlin・Java・Swiftなどのネイティブコードを自作する場合は、既存プラグインで満たせない要件と理由を明示する。

## ゲーム開発の進め方と提案

- ユーザーはゲーム開発が初めてであることを前提に、作法、工程、用語を必要に応じて平易に説明する。依頼された作業の順序や前提に問題があれば、理由と適切な進め方を伝える。
- ゲーム開発の作業を提案する際は、都度Web検索を行い、関連する開発手順や推奨事項を確認する。公式ドキュメントや開発元の資料などの一次情報を優先し、対象のUnityバージョン・OS・現在の実装に適用できるかを確認する。
- 提案の前に、現在の開発段階、今取り組むべきこと、その理由、先に決める・検証する事項を短く説明し、参照した情報源へのリンクを示す。そのうえで、今回の作業範囲、進める順序、完了条件を提案する。
- 一般的な開発手順と、このプロジェクトの状況から判断した提案を区別する。依頼の目的と合意済みの方針を尊重し、工程の助言を理由に無関係な実装へ広げない。
- Web検索できない場合はその旨を伝え、確認済みの情報と未確認の判断を分ける。

## クライアントアセット展示室

- `client/` には[クライアントアセット展示室](doc/features/showcase.md)を必ず設け、ゲームを進行させずにアセットを一覧・プレビューで確認できる状態を維持する。
- クライアントの画像、キャラクター、UI、Prefab、アニメーション、VFX、音声、シーンなどを追加・変更・移動・削除した場合は、同じ変更で展示室の登録内容とプレビューも更新する。表示・操作・再生結果に影響する実装変更も展示室へ反映する。
- カタログ更新と個別登録は展示室の仕様に従う。自動検出だけで完了とせず、対象が一覧に反映され、変更後の内容をプレビューで確認できることを検証する。検証できない場合は未確認事項として報告する。

## 構成と参照先

| 場所 | 役割 |
|---|---|
| [client/AGENTS.md](client/AGENTS.md) | Unityクライアントの配置と作業ルール |
| [server/AGENTS.md](server/AGENTS.md) | Honoバックエンドの配置と作業ルール |
| [doc/README.md](doc/README.md) | 機能、設計ルール、計画、QAの入口 |
| [文書管理方針](doc/rules/documentation-policy.md) | 文書の配置と更新方法 |
| `shortcuts/` | ユーザーが手動実行する、1ファイルで完結するコマンド |
| `.agents/skills/` | 特定作業の手順 |

設計ルールは `doc/rules/`、機能仕様は `doc/features/`、変更計画は `doc/plans/`、ユーザーから想定される質問と回答は `doc/qa/` に置く。
技術固有の指示と実行コマンドは、採用技術や実際の設定を確認してから追加する。

## ディレクトリ構成と狙い

実行・ビルド・依存管理の単位を `client/` と `server/` で分け、機能仕様と開発ルールは `doc/` から横断して参照する。
配置の共通方針はこのファイル、各実装の具体的な配置はそれぞれの `AGENTS.md` またはそこから参照する設計文書で管理する。

```text
baryonyx/
├── client/                 Unityクライアント
│   ├── Assets/            Unityが読み込むコードとアセット
│   ├── Packages/          Unityパッケージの依存管理
│   ├── ProjectSettings/   Unityプロジェクトの設定
│   └── ci/                クライアントのビルド・検証補助
├── server/                 Honoバックエンド
│   ├── src/               実装と近接するテスト
│   ├── tests/             結合・横断シナリオのテスト
│   ├── migrations/        DB全体の変更履歴
│   └── scripts/           開発・公開処理とそのテスト
├── doc/
│   ├── features/          client・serverを横断する機能仕様
│   ├── rules/             継続的な設計・開発ルール
│   ├── plans/             個別の変更計画
│   └── qa/                ユーザーから想定される質問と回答
├── shortcuts/            手動実行用の単体スクリプト
├── .github/workflows/     CIの起動条件と実行順序
└── .agents/skills/        特定作業の手順
```

### 配置の共通方針

- 機能単位で整理し、一緒に変更する実装・テスト・専用アセットを近くに置く。ファイルの種類だけでプロジェクト全体を分割しない。
- 専用のものは所有する機能内に置き、複数機能で実際に共有するものだけを `Shared/` または `shared/` へ置く。単に将来使いそうという理由で共通化しない。
- 単体テストと機能内のシナリオテストは対象機能の近くに置く。複数機能をまたぐシナリオは、その範囲をまとめるテスト置き場へ置く。
- 起動処理が機能を組み立て、共通部品は個別機能へ依存させない。物理的な近さと依存方向を分けて設計する。
- clientとserverで同じ業務を扱う機能は、各言語の命名規則に合わせて名前を対応させる。API契約は生成元を一つに定め、言語間の型共有だけを目的としたルートの `shared/` は設けない。
- アセンブリ、ルーティング、外部プラグイン、配信対象、DBマイグレーションなど、技術上の境界を優先する。設定・生成物・キャッシュは、それぞれのツールが管理できる場所へ置く。
- 必要なフォルダーだけを作る。下位の構成図は新規配置・移行時の基準であり、未作成の配置先も含む。既存構成を移行するときは、対象作業に必要な範囲で参照・設定・CI・文書を併せて更新する。

## Skill

- 作業に適用するSkillの発動条件と本文を確認し、その範囲で使う。
- 日本語の技術文書やPR本文を作成・推敲するときは [japanese-tech-writing](.agents/skills/japanese-tech-writing/SKILL.md) を使う。
- 読み物としての長い文章を扱う場合は、必要に応じて [cognitive-rhythm-writing](.agents/skills/cognitive-rhythm-writing/SKILL.md) を使う。
- Skillの手順や文章規範を、このファイルへ複製しない。

## Gitと環境

- 現在のブランチと作業ディレクトリで作業する。ユーザーの明示指示がなければ、ブランチの作成・切り替えやworktreeの作成を行わない。
- commit、push、PR作成は、ユーザーが依頼した場合に行う。必要な操作が既に許可されていれば、同じ確認を繰り返さない。
- 環境へ接続するときは対象を確認する。参照元の別プロジェクトのURLや権限設定を流用しない。
- 自動生成ファイルは生成元または生成手順を更新する。依存関係のロックファイルは対応する管理ツールで更新する。

## デバッグ環境

- デバッグにはUnityエディターを利用する。
- 端末環境での確認が必要なデバッグは、Androidエミュレーターで行う。
- エミュレーターの起動は、必ずユーザーが手動で行う。AIは、検証時も含めてエミュレーターを起動しない。
- 調査・実装は、これらのデバッグ環境を前提に進める。
- ログの取得・確認や操作が必要な場合は、ユーザーに依頼する。

## 手動実行用ショートカット

ルートの `shortcuts/` に、1ファイルで処理が完結するWindows用 `.bat` ファイルと、macOS用 `.command` ファイルを置く。
共通スクリプトへの依存を作らず、実行時の作業ディレクトリに依存しないパスを使う。
`shortcuts/` にファイルを追加するときは、同じ変更でこの `AGENTS.md` の一覧にファイルへのリンク・用途・実行方法を記載し、必要な引数や前提条件も追記する。
エミュレーター起動とAPKインストールでは、`ANDROID_HOME`、`ANDROID_SDK_ROOT`、`%LOCALAPPDATA%\Android\Sdk` の順に必要なAndroid SDKのツールを探す。
対象の仮想端末（AVD）は、Android 16（API 36）・Google APIs・x86_64のGoogle Pixel 8a `Pixel_8a_API_36` とする。
初回準備、Unity側の設定、起動できない場合の確認は [WindowsでのUnityとAndroidエミュレーター](doc/rules/client-android-emulator.md) に従う。
起動確認済みのAndroid Emulatorは37.1.11で、起動引数は `-gpu host -feature -Vulkan -no-snapshot` とする。
エミュレーター本体の更新とAndroidシステムイメージの追加は別の操作であり、API 34の既存AVDを標準の確認先として使わない。

| ファイル | ユーザーの操作と動作 |
|---|---|
| [build-apk.bat](shortcuts/build-apk.bat) | このプロジェクトをUnityで閉じてからダブルクリックし、`client/Builds/Android/baryonyx.apk` をビルドする。 |
| [build-apk-to-drive.bat](shortcuts/build-apk-to-drive.bat) | このプロジェクトをUnityで閉じ、Google Drive for desktopを起動してからダブルクリックする。APKをビルドし、成功後に `G:\マイドライブ\71_プロジェクト\baryonyx\baryonyx.apk` へ上書きコピーする。 |
| [build-apk-to-drive.command](shortcuts/build-apk-to-drive.command) | macOS用。このプロジェクトをUnityで閉じ、Google Drive for desktopを起動してからFinderでダブルクリックする。APKをビルドし、成功後に `~/Google Drive/マイドライブ/71_プロジェクト/baryonyx/baryonyx.apk` へ上書きコピーする。 |
| [start-pixel-8a.bat](shortcuts/start-pixel-8a.bat) | 初回準備後にダブルクリックして `Pixel_8a_API_36` をPCのGPU・Vulkan無効・スナップショット無効で起動する。AIによる実行は禁止する。 |
| [install-apk-pixel-8a.bat](shortcuts/install-apk-pixel-8a.bat) | `Pixel_8a_API_36` の起動完了後にダブルクリックし、`client/Builds/Android/baryonyx.apk` を送信・インストールする。別のAPKは、このファイルへ1つドラッグ＆ドロップするか、第1引数にパスを指定する。 |

APKビルドは [ProjectVersion.txt](client/ProjectSettings/ProjectVersion.txt) のUnityを使い、CIと同じ [AndroidBuild.Build](client/Assets/Baryonyx/Editor/CI/AndroidBuild.cs) を呼び出す。
APKビルドが成功したら、実行方法にかかわらず、生成したAPKを必ず次の配置先へ上書きコピーする。

| OS | 配置先 | 一致させる定義 |
|---|---|---|
| Windows | `G:\マイドライブ\71_プロジェクト\baryonyx\baryonyx.apk` | [build-apk-to-drive.bat](shortcuts/build-apk-to-drive.bat) の `$destinationDirectory` |
| macOS | `~/Google Drive/マイドライブ/71_プロジェクト/baryonyx/baryonyx.apk` | [build-apk-to-drive.command](shortcuts/build-apk-to-drive.command) の `destination_directory` |

通常はWindowsで `build-apk-to-drive.bat`、macOSで `build-apk-to-drive.command` を使い、`build-apk.bat` やUnity CLI・Editorでビルドした場合も、成功後に同じコピーを行う。
コピー完了までをビルド作業に含め、配置先へアクセスできない場合やコピーに失敗した場合は未完了として報告する。

`build-apk.bat` と `build-apk-to-drive.bat` はWindows標準のPowerShellで処理し、それぞれ必要なコードを同じファイル内に持つ。
Unity Hubの標準配置 `%ProgramFiles%\Unity\Hub\Editor\<バージョン>\Editor\Unity.exe` を探し、別の配置では同じバージョンの `Unity.exe` のパスを第1引数または環境変数 `UNITY_EDITOR_PATH` で指定する（第1引数を優先する）。
対象バージョンのAndroid Build Supportと有効なUnityライセンスが必要となる。
ログはそれぞれ `client/Logs/build-apk.log`、`client/Logs/build-apk-to-drive.log` に実行ごとに上書きする。
`build-apk.bat` はAPK生成のみを行い、テスト・配布・エミュレーター起動・インストールは行わない。

`build-apk-to-drive.command` は `build-apk-to-drive.bat` と同じ処理をbashで行い、ログも `client/Logs/build-apk-to-drive.log` に上書きする。
Unity Hubの標準配置 `/Applications/Unity/Hub/Editor/<バージョン>/Unity.app/Contents/MacOS/Unity` を探し、別の配置では同じバージョンの実行ファイルのパスを第1引数または環境変数 `UNITY_EDITOR_PATH` で指定する（第1引数を優先する）。

`build-apk-to-drive.bat` と `build-apk-to-drive.command` は配置先フォルダーが存在し、アクセスできることを前提とする。
ビルド失敗時は配置先のAPKを更新せず、コピー失敗時もエラーで終了する。
コピー後も `client/Builds/Android/baryonyx.apk` を残す。
Google Driveへの同期はGoogle Drive for desktopが行うため、同期完了は同アプリで確認する。

APKのインストール先は、起動中のエミュレーターに `adb shell getprop ro.boot.qemu.avd_name` を実行してAVD名で特定する。
対象AVDが未起動・起動途中・同名で複数起動の場合はエラーで終了し、自動起動しない。
既存アプリのデータを保持して更新する `adb install -r` を使う（[Android公式ドキュメント](https://developer.android.com/tools/adb?hl=ja#move)）。
結果を確認できるよう、各ファイルは終了時にキー入力を待つ。

## 検証と報告

- 変更した動作を直接確認できるテストや検査を選ぶ。実行方法はリポジトリ内の設定から確認する。
- 文書だけの変更では、リンク、参照パス、記述の整合を確認する。
- 同じ変更後に成功した検証は再利用し、追加変更や未解決の懸念がある場合に再実行する。
- 実装の失敗と、権限・接続・ツール不足による検証不能を区別する。
- 完了時に差分を見直し、変更内容、検証結果、残っている未確認事項を報告する。
