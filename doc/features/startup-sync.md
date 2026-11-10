---
id: feature-startup-sync
type: specification
status: 運用中
updated: 2026-11-02
---

# 起動時の連携と歩数の同期

[機能索引](README.md) / 関連：[健康データ](health-data.md)・[アカウントとセーブ](accounts-save.md)・[画面一覧](screens.md)

タイトル画面（Top）で、ゲームサーバーへの接続、Health Connectの連携確認、直近7日分の歩数の同期を行う。
Homeの左上には、サーバーに保存した今日の歩数をACT（1歩＝1ACT）に換算して表示し、押すと同じ同期をやり直してルーンへ変換する。
Topでの起動時の同期はルーンを請求しない。ルーンはHomeのACTパネルを押したときだけ増える。

## アカウント

Google接続なしのゲストで始める。
端末が32バイトの乱数（秘密値）を作って保存し、`POST /v1/auth/guest` へ送る。
サーバーは秘密値のハッシュだけを保存し、同じ秘密値には同じユーザーを返す。
応答が届かずに再送した場合も、ユーザーを重複して作らない。
発行するセッションは既存のGoogleログインと同じ1時間の期限で、クライアントは期限の5分前か、401を受けたときにゲストで取り直す。

Google接続は任意とし、端末の引き継ぎなどで使う想定である。
ゲストとGoogleアカウントを結び付ける処理は未実装で、現在の `POST /v1/auth/google` は別のユーザーを作る。
アプリのデータを消すと秘密値も消え、そのゲストのデータへは戻れない。

## Top画面の流れ

Topは開く演出と並行して起動処理を始め、完了するまで開始の案内に「LOADING...」を表示する。

1. ゲストのセッションを確保する。
2. Health Connectの利用可否と、このアプリの歩数の読み取り権限を確認する。
3. 許可済みなら、Health Connectから直近7日分の歩数を日別に集計し、サーバーへ保存する。起動時の同期では歩数以外の健康記録を読まない。
4. 完了したら「TAP TO START」を表示する。

失敗したときの扱いは、ゲストのセッションを確保できたかどうかで分ける（2026-11-10。それまでは、どの失敗でも「TAP TO RETRY」だけを出し、直るまでHomeへ進めなかった）。

| 失敗した所 | 案内 | 押したとき |
|---|---|---|
| セッションの確保（手順1。サーバーへつながらない） | 「通信に失敗しました  TAP TO RETRY」 | 起動処理をやり直す。成功するまでHomeへ進まない（サーバーがないと、編成もルーンも読めないため） |
| 歩数の読み取り（手順3。Health Connectが歩数を返さない） | 「歩数を読み取れませんでした  TAP TO START」 | 歩数を同期しないままHomeへ進む |
| 権限の確認・歩数の保存（手順2・3。端末の例外、保存の通信の失敗） | 「歩数を同期できませんでした  TAP TO START」 | 同上 |

歩数なしで始めたときは、開始の操作で同期し直さない。HomeのACTパネルを押すと、もう一度同期する（[HomeのACTパネル](#homeのactパネル)）。
運動しないと遊べないゲームにはしない方針（[ゲーム概要](../game/overview.md)）のため、歩数の失敗では開始を止めない。
7日のうち1日でも歩数の集計に失敗した場合は読み取りの失敗として扱い、サーバーへ保存しない（失敗した日を欠測として上書きしないため）。
エラーの詳細は画面へ出さない。

未連携の場合は、開始の案内の代わりに連携モーダルを表示する。
モーダルは[知らせる窓](../rules/ui-design.md#窓)で作り、下に「あとで」と、金の縁で光る主ボタン（[窓のボタン](../rules/ui-design.md#決めるボタン)）を並べる（2026-11-01に、青緑の窓とボタンからそろえた）。
連携済みとみなすのは歩数の読み取りを許可している場合だけで、体重などほかの記録だけを許可した状態は未連携として扱う。
許可画面で歩数を許可しなかった場合も、ほかの記録を許可したかどうかにかかわらず、閉じた回数に数える。
モーダルの主ボタンはOSの許可画面を開き、許可されるとその場で同期する。
許可画面では歩数の読み取りだけを要求する（体重・血圧などは要求しない）。
「あとで」を選ぶと同期せずに「TAP TO START」を表示し、運動データがなくてもゲームを始められる。

開始の案内を押したときは、手元の権限をもう一度確認してからHomeへ進む。
許可が取り消されていればモーダルを表示し、連携するか「あとで」を選ぶと、押した開始操作の続きとしてHomeへ進む。
その起動中に「あとで」を選んでいれば、開始時にモーダルを出し直さない。
起動処理のあとで設定から許可した場合は、開始時に同期してから進む。

右上の設定ボタンはHomeと同じ見た目で、押しても何も起きない（設定画面は未実装）。
全面の開始ボタンより手前にあるため、押しても開始操作にはならない。

## 連携モーダルの文言

| 状態 | 主ボタン | 押したときの動作 |
|---|---|---|
| 未許可 | Health Connectと連携 | OSのHealth Connect許可画面を開く |
| 許可画面を2回続けて閉じた | Health Connectの設定を開く | Health Connectの設定を開き、アプリへ戻ったときに確認し直す |
| Health Connectを利用できない | Health Connectを入手 | 設定を開けない場合はGoogle Playの配布ページを開く |
| Health Connectの更新が必要 | Health Connectを更新 | 同上 |

Health Connectは、許可画面でキャンセルが2回続くと、以降は許可画面を表示しない（[Android公式](https://developer.android.com/health-and-fitness/health-connect/ui/permissions)）。
アプリは許可画面を閉じた回数を端末に保存し、2回に達したら設定を開く案内へ切り替える。
許可を確認できたら回数を0に戻す。

## HomeのACTパネル

Homeの表示時に、サーバーに保存した今日の歩数と所持ルーン（`GET /v1/runes/balance`）を取得する。
今日の歩数は1歩＝1ACTでACTへ換算して表示する（[ゲーム共通の単位](exercise-rewards.md#ゲーム共通の単位act)）。
今日の日付は端末の時刻から朝4時区切りで決め（4時より前は前日）、その日の記録がなければ0ACTと表示する。
取得が終わるまでと、最初の取得に失敗したときは、ACTと所持ルーンを「--」と表示し、ゲージを出さない（モックの値を実際の値として見せないため）。
歩数を取得するたびに、画面の日付を歩数を集計した日に合わせる。日付をまたいでHomeを開いたままでも、日付とACTの組み合わせがずれない。
未連携ならACTを隠して「タップして歩数を連携」と表示する。

パネルを押すと「Loading...」を表示し、Health Connectの直近7日分の保存と、未変換の分のACTへの換算とルーンへの変換を、1回の要求（`POST /v1/exercise/rewards/sync`）で行う。
今日の歩数は保存した値（端末で読んだ値）を使い、サーバーから読み直さない。
変換は1ACT＝1ルーンで、付与済みの分は二重に付与しない（[運動報酬](exercise-rewards.md#actのルーン変換)）。
請求の応答に含まれる付与量と付与後の残高を使い、所持数を付与前の値から付与後の値まで増やす[獲得の演出](screens/top-home.md#ルーン獲得の演出)を出す。付与量が0なら「獲得ルーンはありません」と[通知の帯](../rules/ui-design.md#操作を求めない通知)で知らせる。
未許可なら先に許可画面を開き、許可画面を出せない状態なら設定を開く。
失敗や未連携は[通知の帯](../rules/ui-design.md#操作を求めない通知)で「歩数を取得できませんでした」などと知らせ、直前のACTと所持ルーンを残す。
同期中の連打は1回の同期として扱う。
付与したルーンと日ごとの変換済みの歩数はサーバーのDBに保存するため、アプリを閉じても、Homeを開き直しても残る。
ゲージの最大（8,000ACT）は固定値で、仮データやサーバーからは取得しない。

## 実行環境と接続先

Android版だけがHealth Connectを読む。
EditorなどAndroid以外では、健康データのサンプルを返すプレビューを使う。
[接続設定](../../client/Assets/Baryonyx/Features/Health/Data/HealthConnectionSettings.asset)の `PreviewStartsUnlinked` を有効にすると、プレビューを未連携の状態から始め、モーダルを確認できる。
プレビューでは許可の要求と設定を開く操作が、どちらも許可済みとして返る。

接続先は環境（Local・Dev・Prod）の名前で選び、接続先を切り替えるために設定アセットを書き換えない。
環境ごとのURLは[接続設定](../../client/Assets/Baryonyx/Features/Health/Data/HealthConnectionSettings.asset)に持つ。
この値は公開される接続先であり、秘密値を入れない。

| 項目 | 値 |
|---|---|
| `DevServerUrl` | `https://baryonyx-server-dev.croissant-lab.workers.dev` |
| `ProdServerUrl` | 未設定（Prod環境の公開後に設定する） |
| `BuildServerUrl` | リポジトリでは常に空。APKのビルド中だけ、選んだ環境のURLが入る |

EditorのPlayで接続する先は、メニューの `Baryonyx > Server` で `Local (127.0.0.1:4000)`・`Dev`・`Prod` から選ぶ。
選択は開発者ごとの `client/UserSettings/BaryonyxServer.json`（Git対象外）に保存し、未選択ならLocalに接続する。
この選択はAPKに影響しない。
開発用のプレイヤー（seed）としてLocalに接続する方法は、[開発用データ（seed）](../rules/backend-design.md#開発用データseed)に記す。

APKの接続先は、[AndroidBuild.Build](../../client/Assets/Baryonyx/Editor/CI/AndroidBuild.cs) を実行するときの環境変数で決める。

| 環境変数 | 接続先 |
|---|---|
| どちらも未指定 | Dev |
| `BARYONYX_ENVIRONMENT=dev` または `preview` | Dev（PreviewのAPKは当面Devへ接続する） |
| `BARYONYX_ENVIRONMENT=prod` | `ProdServerUrl`。未設定ならビルドを止める |
| `BARYONYX_SERVER_URL=<URL>` | 指定したURL。環境名より優先する。Androidエミュレーターから同じPCのLocalサーバーを使うときは `http://10.0.2.2:4000`（エミュレーター内の `127.0.0.1` はエミュレーター自身を指すため） |
| `BARYONYX_ENVIRONMENT=local` やほかの名前 | ビルドを止める。Localのサーバーへつなぐときは `BARYONYX_SERVER_URL` で指定する |

ビルドログの `BARYONYX_ANDROID_SERVER:` の行で、APKに入れた環境名とURLを確認できる。
Google Driveへ置くときは、環境名をあらかじめ設定した[手動実行用ショートカット](../rules/local-shortcuts.md#接続先の環境)を使う。
ビルドの最後に `BuildServerUrl` を空へ戻す。
ビルドが途中で強制終了した場合は値が残ることがあるため、Assetの差分を確認して空へ戻す。
[AndroidBuild.Build](../../client/Assets/Baryonyx/Editor/CI/AndroidBuild.cs) 以外の方法でビルドしたAPKはDevへ接続する。

選んだ環境のURLが空なら、歩数を端末のメモリだけに保持する。
URLはHTTPSを基本とし、HTTPは開発PC（`127.0.0.1`・`localhost`）とAndroidエミュレーターから開発PCへ届く `10.0.2.2` だけで受け付ける（[ServerApi](../../client/Assets/Baryonyx/Shared/Networking/ServerApi.cs)）。
APKはDevelopment Buildのため、Player Settingsの「Allow downloads over HTTP」を「Development builds only」にしている。

### 接続設定アセットがないとき

`HealthConnectionSettings.asset` が存在しない場合は、Unity Editorで `Baryonyx > Health > Create Screen Assets` を実行して生成する。
Unityを閉じた状態でリポジトリのルートから同じ処理を実行する場合は、次のPowerShellのコマンドを使う。

```powershell
$projectPath = (Resolve-Path 'client').Path
$unityPath = $env:UNITY_EDITOR_PATH
if ([string]::IsNullOrWhiteSpace($unityPath)) {
    $unityPath = (Get-ChildItem "$env:ProgramFiles\Unity\Hub\Editor" -Directory |
        Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName 'Editor\Unity.exe' } |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1)
}
if ([string]::IsNullOrWhiteSpace($unityPath)) {
    throw 'Unity.exeが見つかりません。UNITY_EDITOR_PATHに指定してください。'
}
& $unityPath -batchmode -quit -projectPath $projectPath `
    -executeMethod Baryonyx.Health.Editor.HealthScreenAssets.CreateAssets `
    -logFile (Join-Path $projectPath 'Logs/create-health-assets.log')
if ($LASTEXITCODE -ne 0) { throw "UnityでのAsset生成に失敗しました。終了コード: $LASTEXITCODE" }
```

サーバーは日付の古い順に連続した7日分と、UTC（末尾 `Z`）の時刻を要求する。
クライアントは取得元の並び順や時差の表記に依存しないよう、送信前に並べ替えてUTCへそろえる。

## 実装の入口

| 対象 | 入口 |
|---|---|
| ゲストの秘密値 | [GuestCredential](../../client/Assets/Baryonyx/Features/Account/Runtime/GuestCredential.cs) |
| ゲストのセッション（全機能で共有） | [AccountSessionRunner](../../client/Assets/Baryonyx/Features/Account/Runtime/AccountSessionRunner.cs) |
| 歩数の保存・取得、ルーンの請求 | [HealthServerSync](../../client/Assets/Baryonyx/Features/Health/Runtime/Sync/HealthServerSync.cs) |
| 連携の確認、許可の要求、同期 | [HealthStepLink](../../client/Assets/Baryonyx/Features/Health/Runtime/Link/HealthStepLink.cs) |
| Topの起動処理の状態 | [HealthStartupFlow](../../client/Assets/Baryonyx/Features/Health/Runtime/Link/HealthStartupFlow.cs) |
| 連携モーダル | [HealthLinkModalView](../../client/Assets/Baryonyx/Features/Health/Runtime/Link/HealthLinkModalView.cs)・[Prefab生成](../../client/Assets/Baryonyx/Features/Health/Editor/HealthLinkModalAssets.cs) |
| シーンをまたいで共有する接続 | [GameServices](../../client/Assets/Baryonyx/App/Runtime/GameServices.cs)。Domain Reloadを省略したPlay開始でも作り直す |
| 接続先の選択 | [ServerEndpoint](../../client/Assets/Baryonyx/App/Runtime/ServerEndpoint.cs)・[Editorのメニュー](../../client/Assets/Baryonyx/App/Editor/ServerEnvironmentMenu.cs)・[ビルド時の選択](../../client/Assets/Baryonyx/Editor/CI/ServerBuildEndpoint.cs) |
| Top・Homeへの接続 | [TopSceneController](../../client/Assets/Baryonyx/App/Runtime/TopSceneController.cs)・[HomeStepSource](../../client/Assets/Baryonyx/App/Runtime/HomeStepSource.cs)・[シーンへの配置](../../client/Assets/Baryonyx/App/Editor/TopStartupSyncSetup.cs) |
| ゲストAPI | [routes.ts](../../server/src/features/accounts/routes.ts)・[マイグレーション](../../server/migrations/0002_guest_accounts.sql) |

シーンへボタンとモーダルを置き直すときは、Unity Editorの `Baryonyx > App > Connect Startup Sync` を実行する。

## 確認状況

Miniflareのシナリオでゲストの開始・保存・取得・ログアウトを、EditModeで起動処理の状態遷移を、PlayModeでTop・Homeの表示と操作を確認した。
EditorからローカルのHonoへ接続し、ゲストログイン、7日分の保存、Homeでの取得と再同期まで通ることを確認した。
AndroidエミュレーターでのHealth Connect許可画面、設定からの復帰、APKからDev環境への接続は未確認である（EditorからDevへの接続は2026-10-22の計測で通っている）。

## 変更と判断の記録

経緯は[起動時の連携と歩数の同期の変更と判断の記録](startup-sync-history.md)に置く。新しい記録はそのファイルに追記する（2026-11-02に移した）。
