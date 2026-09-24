---
id: feature-startup-sync
type: specification
status: 運用中
updated: 2026-09-24
---

# 起動時の連携と歩数の同期

[機能索引](README.md) / 関連：[健康データ](health-data.md)・[アカウントとセーブ](accounts-save.md)・[画面一覧](screens.md)

タイトル画面（Top）で、ゲームサーバーへの接続、Health Connectの連携確認、直近7日分の歩数の同期を行う。
Homeの左上には、サーバーに保存した今日の歩数を表示し、押すと同じ同期をやり直す。
歩数からルーンへの変換は手動で行う仕様のため、この機能では請求しない。

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
3. 許可済みなら、Health Connectから直近7日分を読み、サーバーへ保存する。
4. 完了したら「TAP TO START」を表示する。

通信や読み取りに失敗した場合は、案内を「通信に失敗しました  TAP TO RETRY」（読み取りの失敗は「歩数を読み取れませんでした」）に替える。
7日のうち1日でも歩数の集計に失敗した場合は読み取りの失敗として扱い、サーバーへ保存しない（失敗した日を欠測として上書きしないため）。
画面を押すと起動処理をやり直し、成功するまでHomeへ進まない。
エラーの詳細は画面へ出さない。

未連携の場合は、開始の案内の代わりに連携モーダルを表示する。
連携済みとみなすのは歩数の読み取りを許可している場合だけで、体重などほかの記録だけを許可した状態は未連携として扱う。
許可画面で歩数を許可しなかった場合も、ほかの記録を許可したかどうかにかかわらず、閉じた回数に数える。
モーダルの主ボタンはOSの許可画面を開き、許可されるとその場で同期する。
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

## Homeの歩数パネル

Homeの表示時に、サーバーに保存した今日の歩数を取得する。
今日の日付は端末の日付で決め、その日の記録がなければ0歩と表示する。
未連携なら歩数を隠して「タップして歩数を連携」と表示する。

パネルを押すと「同期中…」を表示し、Health Connectの直近7日分をサーバーへ保存してから、今日の歩数を取得し直す。
未許可なら先に許可画面を開き、許可画面を出せない状態なら設定を開く。
結果はトーストで「歩数を同期しました」「歩数を取得できませんでした」などと知らせ、失敗しても直前の歩数を残す。
同期中の連打は1回の同期として扱う。
目標・週次の達成・ルーン残高は、引き続き仮データを表示する。

## 実行環境と接続先

Android版だけがHealth Connectを読む。
EditorなどAndroid以外では、健康データのサンプルを返すプレビューを使う。
[接続設定](../../client/Assets/Baryonyx/Features/Health/Data/HealthConnectionSettings.asset)の `PreviewStartsUnlinked` を有効にすると、プレビューを未連携の状態から始め、モーダルを確認できる。
プレビューでは許可の要求と設定を開く操作が、どちらも許可済みとして返る。

接続先は環境（Local・Dev・Prod）の名前で選ぶ。
EditorのPlayはメニュー `Baryonyx > Server` の選択（既定はLocal）、APKはビルド時の環境変数（既定はDev、PreviewもDev）で決まり、設定アセットは書き換えない。
選び方の詳細は[クライアントの作業ルール](../../client/AGENTS.md#サーバーのbaseurl)を正本とする。
選んだ環境のURLが空なら、歩数を端末のメモリだけに保持する。
URLはHTTPSを基本とし、HTTPは開発PC（`127.0.0.1`・`localhost`）とAndroidエミュレーターから開発PCへ届く `10.0.2.2` だけで受け付ける。
APKはDevelopment Buildのため、Player Settingsの「Allow downloads over HTTP」を「Development builds only」にしている。

サーバーは日付の古い順に連続した7日分と、UTC（末尾 `Z`）の時刻を要求する。
クライアントは取得元の並び順や時差の表記に依存しないよう、送信前に並べ替えてUTCへそろえる。

## 実装の入口

| 対象 | 入口 |
|---|---|
| ゲストの秘密値 | [GuestCredential](../../client/Assets/Baryonyx/Features/Account/Runtime/GuestCredential.cs) |
| ゲストのセッション、保存、取得 | [HealthServerSync](../../client/Assets/Baryonyx/Features/Health/Runtime/Sync/HealthServerSync.cs) |
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
AndroidエミュレーターでのHealth Connect許可画面、設定からの復帰、Dev環境への公開後の動作は未確認である。

## 変更と判断の記録

- 2026-09-24：PR #9 の自動レビューの指摘を受け、連携の判定を「いずれかの記録を許可」から「歩数を許可」に改め、日ごとの歩数の集計失敗を同期の失敗として扱うようにした。ほかの記録だけを許可した状態で0歩を保存し、歩数の許可を案内しなくなるのを防ぐ。
- 2026-09-24：設定アセットのURLを手で切り替える運用をやめ、環境の名前で接続先を選ぶ形にした（ユーザー承認）。PreviewのAPKは当面Devへ接続する（ユーザー決定）。Prod環境は未公開のため `ProdServerUrl` は空とし、Prod向けのビルドはURLの設定まで止める。
- 2026-09-24：ユーザーの依頼により、Topでの接続・連携確認・同期、連携モーダル、LOADING表示、通信失敗時の再試行、右上の設定ボタン、Homeの歩数パネルの同期を追加した。Googleログインは任意とし、ゲストで始める方針をユーザーが決めた。サーバーにデータがあるかどうかではモーダルを出さず、未連携の場合だけ出す。
