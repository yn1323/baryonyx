# AGENTS.md

`client/` の作業には、[ルートのAGENTS.md](../AGENTS.md)と以下の指示を適用する。
C#編集はZed、Unityの操作・検証はUnity CLIを基本とする。
Unityのバージョンは [ProjectSettings/ProjectVersion.txt](ProjectSettings/ProjectVersion.txt) に従う。

## Unity CLI

Unity Editorの操作・状態確認・検証には、Unity CLI（`unity`）を利用できる。
接続に使うUnity Pipelineは、[Packages/manifest.json](Packages/manifest.json)に含まれている。

- 利用前に `unity --version` でCLIの実行可否を確認する。
- 接続先がこのリポジトリの `client/` であることを確認する。
- 利用可能な操作と引数は、接続先の `unity command --project-path <clientのパス>` が返すコマンド一覧や、導入済みパッケージの説明で確認する。

## Unityの操作と検証

- C#変更後は、再コンパイルの完了とConsoleのエラーを確認する。
- 変更した動作に対応するテストを実行する。テスト0件は合格として扱わない。
- PlayModeの開始・停止やシーンの切り替えは、実行中の作業を確認してから行う。
- アセットの移動・名前変更・削除はUnityの機能を使い、対応する `.meta` とGUIDの整合を保つ。
- 画面変更後はGameビューを撮影し、保存した画像を開いて確認する。Overlay UIを含める場合はPlayModeで `capture_game_view --source screen` を使う。
- 検証画像は `Assets/DevCaptures/` に保存する。このフォルダと対応する `.meta` はGit除外済み。
- CLI未導入や接続失敗で検証できない場合は、実装の失敗と区別し、未確認の内容と理由を報告する。
