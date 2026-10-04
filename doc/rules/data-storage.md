---
id: rule-data-storage
type: reference
status: 運用中
updated: 2026-10-05
---

# 保存先の決め方

[設計・開発ルールの索引](README.md) / [アカウントとセーブ](../features/accounts-save.md) / [全体構成](../architecture.md)

ゲームで扱うデータを、端末（Unity）とサーバーのDB（D1）のどちらに保存するかを決める基準と、それぞれに置くときの作り方を定める。
保存に求める体験上の要求は[アカウントとセーブ](../features/accounts-save.md)、各項目の保存の作りは各機能の仕様を正本とする。
この文書は、判断の基準と、今の置き場所の一覧を正本として持つ。

## 判断の基準

保存するデータに、次の3つの問いを順に当てはめる。

| 問い | 当てはまるときの置き場所 | 理由 |
|---|---|---|
| 消えたら困るか | サーバー | 端末の保存は、アプリのデータ削除やアンインストールで消える。サーバーに置けば、別の端末へ引き継ぐこともできる |
| 書き換えられたら困るか | サーバー | 端末の保存は暗号化されず、利用者が書き換えられる（[PlayerPrefs](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/playerprefs)）。ルーンは歩数からサーバーが計算して付与するため、ルーンで得たものを端末に置くと、ファイルの書き換えで増やせてしまう |
| 端末ごとに違ってよいか、消えても作り直せるか | 端末 | 端末の性能や環境に合わせる値や、失っても困らない値は、サーバーへ送る必要がない |

最初の2つのどちらかに当てはまれば、3つ目より優先してサーバーに置く。
例外は、サーバーのデータへ戻るための鍵（ゲストの秘密値）である。
鍵は、端末がサーバーに自分が誰かを示すために使うので、端末に置く。

ユーザーごとに変わらない定義（キャラの名前、スキルの効果、Lvの上限など）は、保存するデータとして扱わない。
このうちサーバーの判定に使う値（Lvの上限、費用、報酬）はサーバーを正本にして、応答で端末へ返す。
画面に出すだけの値（名前、効果の説明、ステータスの伸び）はクライアントに置く。
定義をまとめて管理する形式（ゲームマスタ）は[未決](#未決事項)である。

## 置き場所の一覧

「案」は、仕様で保存先が未決のまま、上の基準に当てはめた置き場所である。
作るときにユーザーに確かめ、決まったら仕様へ記録して、この一覧の状態を直す。

### サーバーに置くもの

| データ | 状態 | 正本 |
|---|---|---|
| 持っているキャラ・Lv・スキル・編成 | 実装済み | [キャラと編成の保存](../features/party.md#キャラと編成の保存) |
| ルーンの残高と、増えた・使った記録 | 実装済み | [ルーン経済と資源](../features/economy.md) |
| ACTボーナスの持ち物と枠の設定 | 実装済み | [持ち物と枠の保存](../features/step-bonus.md#持ち物と枠の保存) |
| 進行中の冒険（道を作る乱数の種、今いる部屋の階と種類、通った部屋）と、行き先ごとの記録 | 実装済み | [冒険の中断と再開](../features/stage-progression.md#冒険の中断と再開)。分岐ルートの道そのものは保存せず、種から端末が作る（[冒険の道の作り方](../features/stage-progression.md#冒険の道の作り方)） |
| 歩数 | 実装済み | [起動時の連携と歩数の同期](../features/startup-sync.md) |
| 持っている装備と、キャラごとの武器・防具 | 実装済み | [持っている装備と付け替えの保存](../features/equipment.md#持っている装備と付け替えの保存) |
| 獲得したスキル・素材 | 案 | [装備生成と厳選](../features/equipment.md)・[パーティ](../features/party.md) |
| 開示した敵の弱点 | 案 | [弱点の開示](../features/combat.md#弱点の開示) |
| 実績 | 案 | [実績とミッション](../features/achievements.md) |
| 課金で得たもの | 案 | [ガチャと課金の検討項目](../features/monetization.md) |

### 端末に置くもの

| データ | 状態 | 保存している場所 |
|---|---|---|
| ゲストの秘密値 | 実装済み | [GuestCredential](../../client/Assets/Baryonyx/Features/Account/Runtime/GuestCredential.cs)（`Account.GuestSecret`） |
| Health Connectの許可画面を閉じた回数 | 実装済み | [HealthStepLink](../../client/Assets/Baryonyx/Features/Health/Runtime/Link/HealthStepLink.cs)（`Health.PermissionDenials`） |
| 歩数の送信元ID（ユーザーごと） | 実装済み | [HealthServerSync](../../client/Assets/Baryonyx/Features/Health/Runtime/Sync/HealthServerSync.cs)（`Health.RewardSourceId.<ユーザーID>`） |
| 開発用のデバッグルームの表示設定 | 実装済み | [CardSkillLab](../../client/Assets/Baryonyx/Features/Combat/Presentation/CardSkillLab.cs)・[EnemyLab](../../client/Assets/Baryonyx/Features/Combat/Presentation/EnemyLab.cs)（`Baryonyx.<デバッグルーム>.<項目>`） |
| 音量・画質・振動などの設定 | 案 | [ユーザー設定とアクセシビリティ](../features/player-settings.md)。端末ごとにするかアカウント共通にするかは未決 |
| 最後に開いたタブなど、表示上の記憶 | 案 | 必要になった機能の仕様 |

### 保存しないもの

| データ | 理由 |
|---|---|
| 戦闘中の状態（ターン・手札・HP） | 戦闘の途中で中断・終了したときは、その部屋の戦闘を始めからやり直すと決めた（[アカウントとセーブ](../features/accounts-save.md#未決事項)） |
| 画面を開いている間だけの状態（選んでいる項目など） | 画面を開き直せば作り直せる |

## サーバーに置くときの作り方

ACTボーナス・編成（キャラ・装備）・冒険は次の形で作っている。
新しくサーバーに保存する機能も、これに合わせる。

- 費用・報酬・残高の変化はサーバーで計算して確定する。端末は操作の内容だけを送り、計算結果を送らない。たとえばレベルアップでは、今のLv・上げたあとのLv・要求IDだけを送り、使うルーンはサーバーが計算する（[編成の保存の計画の方針](../plans/2026-10-04-tavern-party-save.md#方針)）。
- ルーンや持ち物を増減する操作には、端末が作った要求ID（`requestId`）を付ける。サーバーは同じIDの再送に記録済みの結果を返し、通信が切れて送り直しても二重に処理しない。
- 複数の書き込みは `db.batch()` でまとめて確定する（[DrizzleによるDB操作](backend-design.md#drizzleによるdb操作)）。テーブルは所有する機能の `db-schema.ts` に定義し、変更は[マイグレーション](backend-design.md#マイグレーション)で行う。
- APIはログイン中のセッション（ゲストを含む）で利用者を特定し、その利用者のデータだけを読み書きする。
- クライアントは、画面を開くたびにサーバーから読み、1操作ごとに保存してから表示を変える。保存に失敗したときは表示を変えずに、通知の帯で知らせる。
- 接続先がないとき（展示室など）は仮データで動かし、変更はアプリを動かしている間だけ残す。例：[StepBonusSource](../../client/Assets/Baryonyx/Features/StepBonus/Runtime/StepBonusSource.cs)・[PartySource](../../client/Assets/Baryonyx/Features/Party/Runtime/PartySource.cs)

## 端末に置くときの作り方

- 小さな値は `PlayerPrefs`、まとまったデータは `Application.persistentDataPath` の下のファイルに保存する（[persistentDataPath](https://docs.unity3d.com/ScriptReference/Application-persistentDataPath)）。今は `PlayerPrefs` だけを使っている。
- キーは機能名から始め、ほかの機能と重ならない名前にする（例：`Account.GuestSecret`）。ユーザーごとに分ける値は、キーにユーザーIDを含める（例：`Health.RewardSourceId.<ユーザーID>`）。
- 消えると困る値は、書いた直後に `PlayerPrefs.Save()` を呼ぶ。Unityは既定ではアプリの終了時にディスクへ書き、Androidは裏に回ったアプリを終了の通知なしに止めることがあるためである（[PlayerPrefs.Save](https://docs.unity3d.com/ScriptReference/PlayerPrefs.Save.html)・[Activity state changes](https://developer.android.com/guide/components/activities/state-changes)）。ディスクへの書き込みで一瞬止まることがあるため、戦闘中などの操作の途中では呼ばない。
- Androidの `PlayerPrefs` は、アプリ専用の領域の `shared_prefs` にXMLとして置かれる（[PlayerPrefs](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/playerprefs)）。Unityは機密の値を `PlayerPrefs` に置かないよう勧めている。ゲストの秘密値の扱いは[未決](#未決事項)である。
- Editorだけで使う設定は、`PlayerPrefs` ではなくEditorの保存先（`EditorUserSettings` など）に置く。例：[Hd2dParticlePreview](../../client/Assets/Baryonyx/Shared/VFX/HD2D/Runtime/Hd2dParticlePreview.cs)

## Androidの自動バックアップ

APKのマニフェストは `android:allowBackup` を指定しておらず、targetSdkが36のため、Androidの自動バックアップが有効である。
2026-10-04に、手元の最後のビルド（2026-10-02）で生成されたマニフェストを読んで確認した。端末での復元は確認していない。
自動バックアップは `shared_prefs` を含むため、利用者がバックアップを有効にしていれば、再インストールや機種変更のあとにゲストの秘密値が戻る可能性がある（[Auto Backup](https://developer.android.com/identity/data/autobackup)）。
引き継ぎの方法を決めるときに、このバックアップを使うか除外するかを併せて決める。

## 未決事項

| 項目 | 状態 |
|---|---|
| 通信できないときに保存が必要な操作の扱い | 未決。今は保存に失敗した操作を画面に反映せず、通知の帯で知らせる |
| 引き継ぎ・再インストール・機種変更 | 未決（[アカウントとセーブ](../features/accounts-save.md#未決事項)）。Androidの自動バックアップの扱いを含む |
| ゲストの秘密値の保存方法 | 未決。今は `PlayerPrefs` に暗号化せずに置いている |
| 戦闘の勝敗をサーバーで検証するか | 未決。今は端末が勝敗を判定し、サーバーは部屋を終えた知らせを受けて報酬を確定する（[全体構成](../architecture.md#未決事項)） |
| 冒険の道の部屋の種類を端末から受け取ること | 確認待ち。道は端末が種から作り、部屋の階と種類を送る。サーバーは1階ずつ進むことと最奥の階のボスだけを確かめる（[探索の確認待ち](../features/stage-progression.md#確認待ち)） |
| 設定を端末ごとにするか、アカウント共通にするか | 未決（[ユーザー設定とアクセシビリティ](../features/player-settings.md)） |
| ゲームマスタの形式 | 未決（[正本と変更の記録](documentation-policy.md#正本と変更の記録)） |

## 変更と判断の記録

- 2026-10-05：ユーザーの指示で、編成で付け替える装備を最初からサーバーに保存することにし、持っている装備とキャラごとの武器・防具を「実装済み」にした。付け替えは装備やルーンを増減させないため、要求IDは付けていない。獲得したスキルと、商会で売り買いする素材は「案」のまま残した。
- 2026-10-04：探索の分岐ルートを冒険ごとに作る形にし、サーバーには道を作る乱数の種と、今いる部屋の階と種類を保存することにした。ユーザーは「途中の進捗はUnity内に保存する形でOK」としたが、進み具合は再開や別の端末で失わないよう、これまでどおりサーバーに置き、道そのものは種から端末が作る形にした。部屋の種類を端末から受け取ることは、未決事項に加えた。
- 2026-10-04：ユーザーの依頼で、保存先を決める基準と、今の置き場所の一覧を定めた。消えたら困るもの・書き換えられたら困るものをサーバーへ、端末ごとに違ってよいもの・消えても作り直せるものを端末へ置く。保存先が未決の項目は「案」として載せ、仕様の決定は変えていない。この基準で作業を進める [data-storage](../../.agents/skills/data-storage/SKILL.md) スキルを作った。
