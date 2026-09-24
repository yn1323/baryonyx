---
name: unity-android-ci-distribution
description: Unity AndroidのAPK/AABビルド、IL2CPP・ARM64設定、署名、GitHub Actions artifact、Dev/Prod/Preview配布、Google Drive更新を実装・レビュー・検証するときに使う。秘密値や外部配布を含まない通常のUnityビルド説明には使わない。
---

# Unity Androidビルドと配布

Unityのビルド成功、artifactの保存、Google Driveへの配置、Google Drive for desktopの同期を別の事実として扱う。
信頼できない変更から長期保存された資格情報を使う経路を作らず、ビルド結果と配布結果を証跡で結び付ける。

## 発動したら最初に確認すること

1. ルートと`client/`の`AGENTS.md`、[Androidビルドと実機確認](../../../doc/rules/client-android-testing.md)、`ProjectSettings/ProjectVersion.txt`、`Packages/manifest.json`、`client/Assets/Baryonyx/Editor/CI/AndroidBuild.cs`を読む。
2. `.github/workflows/client-ci.yml`、`.github/workflows/client-distribute.yml`、`client/ci/`、`shortcuts/`の現在の起動条件とコメントアウト状態を確認する。
3. Androidの出力形式、アプリケーションID、署名方式、IL2CPP、ARM64、環境名（`dev`、`prod`、`preview`）を実設定から確認する。版数や環境を推測しない。
4. 既存の未コミット変更を保護する。ユーザーが求めない限り、ブランチ作成、commit、push、PR作成、秘密値の変更を行わない。

## ローカルビルド

- `ProjectVersion.txt`のUnity EditorとAndroid Build Supportを使う。
- Unityが開いている場合は、作業を保存してプロジェクトを閉じ、`Temp/UnityLockfile`がないことを確認する。
- [Androidビルドと実機確認](../../../doc/rules/client-android-testing.md)に記載されたショートカットまたは同じ`AndroidBuild.Build`を使う。
- 同文書に記載されたAPKとログの実在、成功マーカー、配置先を確認する。
- ビルド失敗時は、古いAPKを新しい成果物として配布しない。既存の配置先を更新しない。
- APK生成後の配置先、コピー条件、CI配布の扱いは[Androidビルドと実機確認](../../../doc/rules/client-android-testing.md)とルートの`AGENTS.md`に従う。配置先へアクセスできない、コピーに失敗する、対象フォルダーがない場合は作業を未完了として報告する。

Google Drive for desktopの同期完了は、ローカルコピー成功だけでは証明しない。同期状態は同アプリで確認する必要がある。

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
2. APK/AABの存在、サイズ、署名、SHA-256、artifact ID。
3. 個人DriveまたはCI配布先へのコピー・アップロード結果。
4. Google Drive for desktopの同期、端末インストール、OAuth、実機動作の未確認事項。

ビルド成功だけで端末動作やDrive同期を成功扱いにしない。

## 参照

- [Unityのビルド概要](https://docs.unity.com/en-us/engine/6000.6/manual/building-and-publishing/building-introduction)
- [Unity Build Automation](https://docs.unity.com/en-us/build-automation/basic-build-configuration/overview)
- リポジトリの`shortcuts/build-apk.bat`
- リポジトリの`shortcuts/build-apk-dev-to-drive.bat`・`shortcuts/build-apk-prod-to-drive.bat`
- リポジトリの`shortcuts/build-apk-dev-to-drive.command`・`shortcuts/build-apk-prod-to-drive.command`（macOS）
- リポジトリの`.github/workflows/client-ci.yml`
- リポジトリの`.github/workflows/client-distribute.yml`
