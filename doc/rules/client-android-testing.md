# Androidビルドと実機確認

[Client CI](../../.github/workflows/client-ci.yml) の `android-build` は、整形・解析・テストが成功した後にAPKを生成する。
ビルド入口は [AndroidBuild.Build](../../client/Assets/Baryonyx/Editor/CI/AndroidBuild.cs) である。

| 項目 | CIの設定 |
|---|---|
| Unity | `ProjectVersion.txt` の版。現在は `6000.6.0f1` |
| 成果物 | `Builds/Android/baryonyx.apk` |
| Application ID | `dev.baryonyx.ci` |
| backend・ABI | IL2CPP・ARM64のみ |
| 最小・対象SDK | API 26・36 |
| ビルド・署名 | Development APK・Android debug署名 |
| versionCode | 1。継続アップデートを保証しない検証用成果物 |

有効なシーンを設定順でビルドし、なし・欠落・重複を拒否する。
CI用に変更したPlayer Settingsは `finally` で元へ戻す。
以前のAPKを先に除去し、失敗したビルドで古い成果物を公開しない。
プラグイン追加でSDK要件が変わる場合は、Unityのビルド設定とworkflowのSDK指定を更新する。

## ビルドの成否とAPKの取得

Unityの `BuildResult` が成功であることを確認し、失敗した場合は例外でCIを失敗にする。
成功したAPKだけを `client-android-apk-<run attempt>` artifactに7日間保存する。
APKが生成されていない場合もアップロードを失敗にする。
APK内部や署名の追加検査、独自のJSON・ビルドレポート出力は行わない。

同一リポジトリのPRでは、成功したAPKの保存先をPRコメントへ掲載する。
再び成功したときは同じbotコメントを更新し、対象コミットも表示する。
mainへのpushと手動実行では、Actionsの実行画面からartifactを取得する。
fork PRにはコメントを投稿しない。

ダウンロードするZIPには `baryonyx.apk` が入っている。
リンクの利用にはGitHubへのログインとリポジトリの閲覧権限が必要であり、保存期間が過ぎると取得できなくなる。
URLは [upload-artifactの公式出力](https://github.com/actions/upload-artifact/tree/v4#outputs) を使用する。
Cloudflareや別の公開サーバーの設定は不要である。

ローカルではAndroid Build Support、SDK・NDK・OpenJDKを同じUnity版へ導入する。
対象Editorを閉じるか検証コピーを用意し、次を実行する。

```text
unity build <clientの絶対パス> --target Android --execute-method Baryonyx.Editor.CI.AndroidBuild.Build --allow-dirty-build --timeout 1800
```

## 手動で残す確認

Androidスモーク・Android E2EをCIへ導入しない。
debug鍵は実行環境ごとに変わり得るため、署名不一致時は検証用アプリを入れ直す。
アンインストールで保存データが消える点を確認してから行う。

- 実機へのインストール、起動、バックグラウンド移行からの復帰。
- 権限の許可・拒否・取り消しと、その後の画面表示。
- 主要操作、保存状態、再起動後の復元。
- 歩数取得と実際に歩いた場合の反映。

APK生成の成功だけでは、端末プラグインの実動作を確認したことにはならない。
