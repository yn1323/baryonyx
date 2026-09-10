# AGENTS.md

`client/` の作業には、[ルートのAGENTS.md](../AGENTS.md)と以下の指示を適用する。
C#編集はZed、Unityの操作・検証はUnity CLIを基本とする。
Unityのバージョンは [ProjectSettings/ProjectVersion.txt](ProjectSettings/ProjectVersion.txt) に従う。

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
