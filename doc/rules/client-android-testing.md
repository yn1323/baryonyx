# Androidビルドと実機確認

[Client CI](../../.github/workflows/client-ci.yml) の `android-build` は、整形・解析・テストが成功した後にAPKを生成する。
ビルド入口は [AndroidBuild.Build](../../client/Assets/Baryonyx/Editor/CI/AndroidBuild.cs) である。

| 項目 | CIの設定 |
|---|---|
| Unity | `ProjectVersion.txt` の版。現在は `6000.6.0f1` |
| 成果物 | `Builds/Android/baryonyx.apk` |
| Application ID | `com.croissantlab.baryonyx` |
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
Client CI全体が成功した後、[Client Android distribution](../../.github/workflows/client-distribute.yml) の `distribute` jobがAPKをGoogle Driveへ配布する。
CI内の受け渡しとビルドごとの保存には、`client-android-apk-<dev|prod|preview>-<run attempt>` artifactを7日間残す。
APKが生成されていない場合もアップロードを失敗にする。
APK内部や署名の追加検査、独自のJSON・ビルドレポート出力は行わない。

同一リポジトリのPRでは、成功したAPKの保存先をPRコメントへ掲載する。
再び成功したときは同じbotコメントを更新し、対象コミットも表示する。
mainへのpushと手動実行では、配布workflowの実行サマリーからDriveのリンクを開く。
fork PRではDriveへの配布とPRコメントを実行しない。

配布workflowは `workflow_run` で起動し、既定ブランチのコミットに固定したスクリプトだけを実行する。
PRから受け取るのはAPK artifactであり、PR内のスクリプトへDriveの秘密値を渡さない。
GitHub APIで実行元、成功状態、最新のPR head、artifactの区分と再実行番号を確認し、終了済み・更新済みのPRや不一致の成果物を配布しない。
初回は配布workflowと補助スクリプトを既定ブランチ `main` へ反映する必要があり、`develop` への反映だけでは起動しない。[workflow_runの起動条件](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#workflow_run)

保存先は個人のマイドライブの [Android配布フォルダー](https://drive.google.com/drive/folders/1iNgsGNtCDyMhyJsAjS2HmzxKVEuY4y8k) とする。
[upload-drive-apk.py](../../client/ci/upload-drive-apk.py) がAPKを環境別の名前でそのまま保存するため、Android側でZIPを展開する必要はない。
フォルダーを閲覧できるGoogleアカウントでログインし、APKをダウンロードして開く。
CIは共有権限を変更しない。

| CIの起動条件 | 配布区分 | Driveのファイル名 |
|---|---|---|
| mainへのpush | Dev | `baryonyx-dev.apk` |
| 同一リポジトリのPR | Preview | `baryonyx-preview.apk` |
| 既定ブランチの手動実行で `dev` を選択（既定値） | Dev | `baryonyx-dev.apk` |
| 既定ブランチの手動実行で `prod` を選択 | Prod | `baryonyx-prod.apk` |
| 既定ブランチの手動実行で `preview` を選択 | Preview | `baryonyx-preview.apk` |

この区分は配布ファイル名だけに適用する。
各APKのAPI接続先、Application ID、Developmentビルド設定、デバッグ署名の扱いは共通であり、Prodという名前だけでは本番用ビルドにならない。
Unityが出力するローカルのファイル名は従来どおり `baryonyx.apk` とする。

同名ファイルが一つあれば、そのファイルIDと共有設定を保って内容を更新する。
存在しなければ新規作成し、同名ファイルが複数ある場合や同名のフォルダー・ショートカットがある場合は失敗にする。
この場合は保存先で残すファイルを一つに整理してから再実行する。
配布jobはリポジトリ全体で直列化し、アップロード後のサイズとチェックサムを照合する。
`queue: max` で最大100件を待機させ、待機開始順に処理する。
上限を超えた実行はキャンセルされるため、該当するClient CIの再実行が必要になる。[GitHubのconcurrency仕様](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/control-workflow-concurrency)
認証・転送・照合の失敗はjobの失敗として扱い、成功リンクを出さない。

Driveの各ファイルは、その配布区分で最後に配布が完了したAPKになる。
Dev・Prod・Previewは互いに上書きしない。
Previewは全PRで一つを共有する。
CI開始順は保証せず、PRコメントのリンク先も次の配布で内容が変わる。
現在のAPKに対応するコミットとCI実行URLは、Driveのファイル説明で確認する。
過去の特定ビルドが必要な場合は、保存期間内のGitHub artifactを使う。

## Google Driveの初回認証

個人のマイドライブへの書き込みには、所有者のGoogleアカウントでOAuth認証を行う。
個人フォルダーの共有設定を変えても、CIの認証は省略できない。
サービスアカウントには個人用の保存容量がないため、今回は使用しない。[Google公式の制約](https://developers.google.com/workspace/drive/api/guides/about-shareddrives)

1. このCI用のGoogle Cloudプロジェクトを選び、Google Drive APIを有効にする。
2. Google Auth PlatformでOAuth同意画面を設定する。継続運用では公開ステータスを「本番環境」にする。「テスト」のままDrive権限を認可すると、更新トークンは7日で期限切れになる。[更新トークンの有効期間](https://developers.google.com/identity/protocols/oauth2#expiration)
3. OAuthクライアントを「ウェブアプリケーション」として作成し、承認済みリダイレクトURIに `https://developers.google.com/oauthplayground` を登録する。
4. [OAuth 2.0 Playground](https://developers.google.com/oauthplayground/) の設定で `Use your own OAuth credentials` を有効にし、作成したクライアントIDとシークレットを入力する。`Access type` は `Offline`、`Force prompt` は `Consent Screen` を選ぶ。
5. スコープ `https://www.googleapis.com/auth/drive` を指定し、保存先フォルダーを所有するアカウントで認可する。`Exchange authorization code for tokens` で更新トークンを取得する。
6. [リポジトリのEnvironments](https://github.com/yn1323/baryonyx/settings/environments) で `client-distribution` を作成する。Deployment branches and tagsを `Selected branches and tags` にし、許可するbranchを `main` だけに設定する。PRのmerge ref、他のbranch、tagは許可しない。
7. `client-distribution` のEnvironment secretsに以下の3項目を登録する。Repository secretsや、このリポジトリから使えるOrganization secretsに同名の値を残さない。既に登録済みなら所有者が環境へ登録し直し、広い範囲の登録を削除する。秘密値はチャットやリポジトリ内のファイルへ貼り付けない。

| Secret名 | 値 |
|---|---|
| `GOOGLE_DRIVE_CLIENT_ID` | 自分で作成したOAuthクライアントID |
| `GOOGLE_DRIVE_CLIENT_SECRET` | そのクライアントのシークレット |
| `GOOGLE_DRIVE_REFRESH_TOKEN` | 保存先アカウントで発行した更新トークン |

Environment secretsとブランチ制限を組み合わせ、PRから変更できるworkflowがDriveの秘密値を取得できないようにする。
workflowに `environment: client-distribution` を書くだけでは、Repository secretsの利用範囲は制限されない。[Environmentの保護とSecrets](https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments)

指定済みのフォルダーと既存の同名ファイルを検索・更新するため、この手順ではDrive全体への権限を認可する。
スクリプトの書き込み先は指定フォルダー内の上記3種類のAPKに限るが、トークン自体の権限はそのフォルダーだけに限定されない。
`drive.file` はアプリにアクセスを許可したファイルに限られるため、スコープの置き換えだけでは既存フォルダーを扱えない。[スコープの違い](https://developers.google.com/workspace/drive/api/guides/api-specific-auth)
Playground標準のクライアントでは更新トークンが24時間後に失効するため、手順4で自分のクライアントを指定する。

Environmentの保護、Secretsの移設、既定ブランチへの反映後にClient CIを実行し、初回作成と、次の実行で同じファイルIDへの更新を確認する。
認証が失効した場合は同じアカウントで再認可し、`GOOGLE_DRIVE_REFRESH_TOKEN` を更新する。
実装と模擬APIによる検証は済んでいるが、保護されたEnvironmentへの設定と実際のDriveへの配布確認は未完了である。

## ローカルビルドと配布処理の検証

ローカルではAndroid Build Support、SDK・NDK・OpenJDKを同じUnity版へ導入する。
対象Editorを閉じるか検証コピーを用意し、次を実行する。

```text
unity build <clientの絶対パス> --target Android --execute-method Baryonyx.Editor.CI.AndroidBuild.Build --allow-dirty-build --timeout 1800
python -B -m unittest discover -s client/ci -p "test_upload_drive_apk.py"
node --test client/ci/resolve-drive-distribution.test.cjs
```

## 手動で残す確認

画面の変更時は [UI設計ルール](ui-design.md#機種差を確認する条件) に従い、縦画面、SafeArea、文字の読みやすさ、タップ領域、OS画面からの復帰を実機で確認する。
「1週間の歩数」は、認証、接続、更新、一覧末尾の選択、JSON詳細、閉じる操作、サインアウトまでを確認する。
実機がない場合は、GameビューやSimulatorでの確認結果と実機の未確認項目を分けて記録する。

Androidスモーク・Android E2EをCIへ導入しない。
debug鍵は実行環境ごとに変わり得るため、署名不一致時は検証用アプリを入れ直す。
アンインストールで保存データが消える点を確認してから行う。

- 実機へのインストール、起動、バックグラウンド移行からの復帰。
- 分割画面で別アプリへ操作を移したときの一覧・JSON詳細の消去と、一時停止が解除されてフォーカスが戻った後の再読み取り。
- 権限の許可・拒否・取り消しと、その後の画面表示。
- 主要操作、保存状態、再起動後の復元。
- 歩数取得と実際に歩いた場合の反映。

APK生成の成功だけでは、端末プラグインの実動作を確認したことにはならない。
