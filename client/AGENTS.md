# AGENTS.md

`client/` の作業には、[ルートのAGENTS.md](../AGENTS.md)と以下の指示を適用する。
C#編集はZed、Unityの操作・検証はUnity CLIを基本とする。
Unityのバージョンは [ProjectSettings/ProjectVersion.txt](ProjectSettings/ProjectVersion.txt) に従う。

## 配置と設計の参照先

ディレクトリ構成、コード・アセット・テストの配置、責務と依存方向は [クライアントの構成と依存関係](../doc/rules/frontend-design.md) に従う。
シーンの一覧と、シーン・画面を作り直すEditorのメニューは [シーンと生成メニュー](../doc/rules/frontend-design.md#シーンと生成メニュー)、各画面の表示と操作は [画面一覧と操作](../doc/features/screens.md) を正本とする。
実行方法は [UnityのテストとCI](../doc/rules/client-testing.md)、画面設計は [UI設計ルール](../doc/rules/ui-design.md) を参照する。

## サーバーの接続先

接続先のURLと選び方は [実行環境と接続先](../doc/features/startup-sync.md#実行環境と接続先) を正本とする。

- APIキー、CloudflareのAPIトークン、クライアントシークレットなどの秘密情報を、[HealthConnectionSettings.asset](Assets/Baryonyx/Features/Health/Data/HealthConnectionSettings.asset) やUnityプロジェクトへ追加しない。このAssetの値は公開される接続先である。
- 接続先を切り替えるためにAssetを書き換えない。EditorのPlayはメニュー `Baryonyx > Server`、APKはビルド時の環境変数で選ぶ。
- APKをビルドしたあとは、Assetの `BuildServerUrl` が空に戻っているかを差分で確かめる。ビルドが途中で強制終了すると値が残ることがあり、残っていれば空へ戻す。

## Unity CLI

Unity CLIの利用手順は、Unityプラグインの `unity:unity-cli` スキルに従う。
接続に使うUnity Pipelineは、[Packages/manifest.json](Packages/manifest.json)に含まれている。
接続先がこのリポジトリの `client/` であることを確認する。

## Unityの操作と検証

- WindowsのAndroidエミュレーターでAPKを確認するときは [専用の手順](../doc/rules/client-android-emulator.md) とルートの [手動実行用ショートカット](../AGENTS.md#手動実行用ショートカット) を使う。起動確認のためにビルド対象のABIを変更せず、同じ手順に記載したAPKとAVDを使う。
- 再コンパイル、テスト、PlayMode、シーンの切り替え、アセットの生成し直しなど、Editorの状態を変える操作は、Editorの札を取ってから行い、終えたら返す。複数のチャットが1台のEditorを共有するためである。手順は [複数のチャットで1台のEditorを使うとき](../doc/rules/client-testing.md#複数のチャットで1台のeditorを使うとき) に従う。
- C#変更後は、再コンパイルの完了とConsoleのエラーを確認する。
- C#変更後は、CIと同じCSharpierで整形・検査する。実行に必要な.NET SDKの導入と実行手順は [整形と静的解析](../doc/rules/client-code-quality.md) に従う。
- 変更した動作に対応するテストを実行する。テスト0件は合格として扱わない。
- PlayModeの開始やシーンの切り替えは、実行中の作業を確認してから行う。
- PlayMode中のEditorは、ユーザーに確認せずに停止してよい。
- PlayModeテストの実行、シーンの切り替え・作り直しの前に、開いているシーンに未保存の変更（`isDirty`）があるかを確認する。未保存の変更は、ユーザーに確認せずに保存してよい（`EditorSceneManager.SaveOpenScenes`）。テストを始めると、Unityが保存の確認ダイアログを出し、人が押すまで止まるためである。画面のPrefabを生成し直すと、そのPrefabを置いた開いているシーンが未保存になる（Prefab内の部品のIDが振り直され、シーン側の上書きが記録し直されるため）。スクリプトでシーンを開き直すと未保存の変更が黙って消えるため、保存せずに開き直さない。
- 接続中のEditorでテストを実行するときは、開始直後に件数を確かめ、0件なら待たずに止めてやり直す。手順は [実行と結果確認](../doc/rules/client-testing.md#実行と結果確認) に従う。
- アセットの移動・名前変更・削除はUnityの機能を使い、対応する `.meta` とGUIDの整合を保つ。
- 画面変更後はGameビューを撮影し、保存した画像を開いて確認する。Overlay UIを含める場合はPlayModeで `capture_game_view --source screen` を使う。保存先と記録する内容は [機種差を確認する条件](../doc/rules/ui-design.md#機種差を確認する条件) に従う。

## フォントアセットの差分

[DotGothic16.asset](Assets/Baryonyx/Shared/UI/Fonts/DotGothic16.asset) はTextMeshProの動的フォントアセットである。
Editorで新しい文字を表示すると、Unityが文字の一覧とアトラス画像をこのファイルへ書き足すため、作業内容と関係なく差分が出る。

- コミットするときは、この差分もコミットの対象に含める。作業内容とは別の `chore` コミットに分ける。
- `.gitignore` や `git update-index --skip-worktree` で除外しない。このアセットはTMPの既定フォント設定やシーン・PrefabからGUIDで参照されており、手元にない環境では文字を表示できなくなる。
- 追加された文字データはビルド時に消える（Clear Dynamic Data On Build）ため、コミットしても実機の表示や容量には影響しない。
