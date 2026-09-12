# Renovateによる依存関係の更新

依存関係の更新PRはRenovateで作成する。
設定はリポジトリ直下の [renovate.json](../../renovate.json) に集約し、Mendが提供するRenovate GitHub Appで実行する。
更新先は `main` とし、PRの確認とマージは手動で行う。

## 更新タイミング

[yps-crispy-carnivalの設定](https://github.com/yn1323/yps-crispy-carnival/blob/develop/renovate.json) を参考に、次の値を設定する。

| 項目 | 設定 |
|---|---|
| タイムゾーン | `Asia/Tokyo` |
| 曜日・時間帯 | 制限なし（`* * * * *`） |
| リリース後の待機 | 2日 |
| 待機中の更新 | `internalChecksFilter: strict` でブランチ・PR作成を保留 |
| PR作成 | ブランチのチェックが実行中でなくなってから（`not-pending`） |
| 同時作成数 | PR・ブランチともに10件まで |
| 1時間の作成数 | PR・コミットともに10件まで |
| 既存ブランチのrebase | 競合したとき |

スケジュールは更新を許可する時間帯を表し、毎分の実行を予約するものではない。
実際の巡回間隔はRenovate GitHub App側の実行に従う。
リリース日時が得られない更新は、Renovateの既定動作に従って保留される。
セキュリティ更新など、通常の更新制限の対象外となる処理もある。[Renovateのスケジュール](https://docs.renovatebot.com/key-concepts/scheduling/)、[リリース後の待機](https://docs.renovatebot.com/key-concepts/minimum-release-age/)

このリポジトリのCIは `main` へのpushとPRを起点とし、Renovateブランチへのpushだけでは実行されない。
PR作成前の待機が止まらないように `internalChecksAsSuccess: true` を設定する。
PR作成後は既存のServer CI・Client CIで変更内容を検証する。

## 更新対象

生成ファイルや外部アセットを直接変更しないよう、更新元のファイルを `includePaths` で指定する。

| 対象 | 更新元 |
|---|---|
| Hono・Zodなどのサーバー依存とpnpm | [server/package.json](../../server/package.json) |
| Node.js | [server/.node-version](../../server/.node-version) |
| Biome設定のスキーマURL | [server/biome.json](../../server/biome.json) |
| GitHub Actions | [.github/workflows/](../../.github/workflows/) |
| .NET SDK・CSharpier | [client/global.json](../../client/global.json)・[dotnet-tools.json](../../client/.config/dotnet-tools.json) |
| Unity Editor | [ProjectVersion.txt](../../client/ProjectSettings/ProjectVersion.txt) |
| Android検証用Gradleプラグインと健康データライブラリ | [検証プロジェクト](../../client/ci/health-native/)・[build.gradle](../../client/Assets/Plugins/Android/BaryonyxHealth.androidlib/build.gradle) |

Biome本体と設定内のスキーマURLは、同じ `Biome` グループのPRで更新する。
サーバーの依存変更時は、pnpmで生成する [ロックファイル](../../server/pnpm-lock.yaml) も更新する。
Unity EditorやAndroid Gradle Pluginの更新は、Unity・Androidビルドとの互換性を確認してからマージする。

UnityのUPMパッケージ・Git参照、配置済みのAnalyzer DLL、スクリプト内のツールバージョンは今回の自動更新対象に含めない。
UPMとGradleテンプレートはUnity Package Manager・EDM4Uによる既存の生成手順で更新する。

## 有効化と設定の検証

`renovate.json` を `main` に反映し、[Renovate GitHub App](https://github.com/apps/renovate) の対象リポジトリに `yn1323/baryonyx` を含める。
GitHub App側の対象設定が必要なため、設定ファイルの追加だけでは稼働確認は完了しない。
初回の巡回後にDependency Dashboardと更新PRを確認する。

設定を変更したら、リポジトリ直下で公式validatorを実行する。
初回実行にはnpmレジストリへの接続が必要である。

```sh
npx --yes --package renovate -- renovate-config-validator --strict --no-global renovate.json
```

この検証は設定の妥当性を確認するもので、更新PRの作成やマージは行わない。[公式の設定検証手順](https://docs.renovatebot.com/config-validation/)
