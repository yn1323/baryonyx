---
name: unity-android-ci-distribution
description: Unity AndroidのAPK・AABを作る・配るときに使う。ショートカットや開いているEditorでのAPKのビルド、接続先の環境（Dev・Prod）、IL2CPP・ARM64・署名の設定、GitHub Actionsのartifact、Dev・Prod・Previewの配布、いつものGoogle Driveのフォルダーへのコピーを実装・レビュー・検証する。「APK作って」「build、いつものディレクトリに入れて」「Prod向けも作って」という依頼に当てはめる。端末へ入れて確かめるのは unity-android-device-validation、APKが大きい・重いときの画像の圧縮は image-compression の担当とし、ユーザーが頼んでいないAPKのビルドや、ビルドを伴わないUnityの説明には使わない。
---

# Unity Androidビルドと配布

Unityのビルド成功、artifactの保存、Google Driveへの配置、Google Drive for desktopの同期を別の事実として扱う。
信頼できない変更から長期保存された資格情報を使う経路を作らず、ビルド結果と配布結果を証跡で結び付ける。

## 発動したら最初に確認すること

1. ルートと`client/`の`AGENTS.md`、[Androidビルドと実機確認](../../../doc/rules/client-android-testing.md)、`ProjectSettings/ProjectVersion.txt`、`Packages/manifest.json`、`client/Assets/Baryonyx/Editor/CI/AndroidBuild.cs`を読む。
2. `.github/workflows/client-ci.yml`、`.github/workflows/client-distribute.yml`、`client/ci/`、`shortcuts/`の現在の起動条件とコメントアウト状態を確認する。
3. Androidの出力形式、アプリケーションID、署名方式、IL2CPP、ARM64、環境名（`dev`、`prod`、`preview`）を実設定から確認する。版数や環境を推測しない。
4. 未コミット変更の保護とGit操作はルートの [AGENTS.md](../../../AGENTS.md) に従う。ユーザーが求めない限り、秘密値を変更しない。

## ローカルビルド

ショートカットの一覧・動作・前提・ログ・接続先の環境・失敗時の扱いと、ビルド後にAPKをコピーする配置先は[手動実行用ショートカットの動作](../../../doc/rules/local-shortcuts.md)、コピーまでをビルド作業とする制約はルートの [AGENTS.md](../../../AGENTS.md#手動実行用ショートカット)、CIの成果物は[Androidビルドと実機確認](../../../doc/rules/client-android-testing.md)を正本とする。
作業では次を確かめる。

- APKは、ユーザーが頼んだときだけ作る（[ローカルビルドと配布処理の検証](../../../doc/rules/client-android-testing.md#ローカルビルドと配布処理の検証)）。
- ショートカットや `unity build` で作るときは、Unityを閉じて `Temp/UnityLockfile` がないことを確かめる。開いているEditorで作るときは、同節の `eval` の手順に従う。
- ショートカットまたは同じ`AndroidBuild.Build`を使い、APKとログの実在、成功マーカー、ビルドログの接続先の環境（`BARYONYX_ANDROID_SERVER:`）を確認する。
- 古いAPKを新しい成果物として扱わない。
- ローカルへのコピーの成功は、Google Drive for desktopの同期の完了を証明しない。同期状態は同アプリで確認する必要がある。
- ビルドのあとは、ビルドが書き換えたファイルを[ローカルビルドと配布処理の検証](../../../doc/rules/client-android-testing.md#ローカルビルドと配布処理の検証)に従って戻す。

## artifactと環境名

- Dev、Prod、Previewのファイル名と接続先を混同しない。
- artifact名、コミットSHA、Unityバージョン、環境名、CI実行URL、APKのSHA-256を記録する。
- Previewは別のPRの配布で上書きされ得るため、配布先のファイル説明やCI実行URLで出所を確認する。
- artifactをダウンロードする場合は、選択した成功ビルドのID、環境名、コミット、ファイル名が一致することを確認する。
- APKのサイズ0、拡張子違い、期待する成功マーカーの欠落、署名不一致を配布前に止める。

## GitHub Actionsの信頼境界

- PRや未信頼ブランチのコードからGoogle Driveの長期資格情報を扱わない。
- Drive配布は、厳格なartifact選択、イベントとベースブランチの確認、適切なGitHub Environment、最小権限の秘密値を備えた信頼済み経路で実行する。
- `GOOGLE_DRIVE_CLIENT_ID`、`GOOGLE_DRIVE_CLIENT_SECRET`、`GOOGLE_DRIVE_REFRESH_TOKEN`の値をログ、artifact、コメント、エラー出力へ出さない。
- GitHub Environmentの秘密値や保護ルールが不足している場合、コードを変えて解決しようとせず、必要な設定名とブロッカーを報告する。
- `workflow_run`など別ワークフローへ成果物を渡す場合は、対象リポジトリ、コミット、ワークフロー、成功状態、artifact ID、環境名をすべて検証する。
- ローカルのDriveコピーとCIのDriveアップロードを同じ成功条件として扱わない。

## AAB・署名・リリース検証

- APKとAABの用途を分け、Google Play提出用のAABをAPKの代替として扱わない。
- keystore、alias、証明書、署名済みartifactのフィンガープリントを秘密値と公開検証値に分ける。
- リリースビルドではDevelopment設定やデバッグログが残っていないことを確認する。開発用APKは環境名と用途を明記する。
- Android SDK、NDK、Gradle、JDKの組み合わせは`ProjectVersion.txt`とプロジェクトのCI設定に合わせ、機械的に最新版へ変更しない。
- ビルド設定を変更した場合は、Manifest、ABI、アプリケーションID、署名、依存ライブラリを再確認する。

## 完了報告

次を分けて報告する。

1. Unityビルドの成功または失敗とログ。
2. APK/AABの存在、サイズ、署名、SHA-256、artifact ID、接続先の環境。
3. 個人DriveまたはCI配布先へのコピー・アップロード結果。
4. Google Drive for desktopの同期、端末インストール、実機動作の未確認事項。

ビルド成功だけで端末動作やDrive同期を成功扱いにしない。
端末へ入れて確かめた場合は、その結果を [unity-android-device-validation](../unity-android-device-validation/SKILL.md) の「完了報告」の4区分で添える。

## 参照

- [Unityのビルド概要](https://docs.unity.com/en-us/engine/6000.6/manual/building-and-publishing/building-introduction)
- [Unity Build Automation](https://docs.unity.com/en-us/build-automation/basic-build-configuration/overview)
- リポジトリの`shortcuts/`（一覧は[手動実行用ショートカットの動作](../../../doc/rules/local-shortcuts.md#ファイルの一覧)）
- リポジトリの`.github/workflows/client-ci.yml`
- リポジトリの`.github/workflows/client-distribute.yml`
