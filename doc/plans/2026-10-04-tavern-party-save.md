---
id: plan-tavern-party-save-20261004
type: reference
status: 記録
updated: 2026-10-04
---

# 酒場の編成・カードスキル・レベルのサーバー保存

計画の状態：完了（実装と自動テストは完了。Unity EditorでローカルのサーバーにつないだPlayでの確認と、Androidエミュレーターでの確認は未実施。[検証結果](#検証結果)）

[計画索引](README.md) / [パーティ](../features/party.md) / [育成と強化](../features/progression.md) / [UPTボーナスの保存](../features/step-bonus.md#持ち物と枠の保存)

## 目的

酒場の[編成](../features/screens.md#編成)・[カードスキル](../features/screens.md#スキル)・[育成](../features/screens.md#育成)で変えた内容を、ゲストを含むユーザーごとにサーバーのDBへ保存し、アプリを起動し直しても残るようにする。
レベルアップに使うルーンもサーバーの残高から引き、Homeの所持ルーンと酒場の所持ルーンを一致させる。

## 現状と今取り組む理由

3つの画面はモックで、変えた内容はクライアントの `PartySession`（編成・Lv・カード）と `TrainingSession`（使ったルーン）がアプリを動かしている間だけ持つ。
起動し直すと仮データ（`PartyMockData`）に戻る。
使ったルーンはHomeで取得した残高から引いて見せるだけなので、Homeへ戻るとサーバーの残高に戻り、表示が食い違う。

直前の作業で、UPTボーナスの持ち物と枠をサーバーに保存した（`server/src/features/step-bonus/`）。
酒場の画面を開くたびにサーバーから読み、1操作ごとに保存してから画面に反映し、接続先がないときだけ仮データを使う形である。
同じ形を3つの画面へ広げられるため、いま保存の仕組みを作る。

キャラの加入方法、カードの所持、Lvの上限と必要ルーンは[未決](../features/party.md#未決事項)のままである。
今回はこれらを決めず、仮の値のまま保存の仕組みだけを作る。

## 参照した一般的な手順

次は一般的な手順であり、このプロジェクトへの当てはめは[方針](#方針)に書く。

- 通貨の消費や成長のように不正の影響が大きい処理は、サーバーで検証して確定させ、端末に残高や結果を直接書かせない（[Unity Cloud Code：Server authority](https://docs.unity.com/en-us/cloud-code/server-authority)、[Cheat prevention](https://docs.unity.com/ugs/en-us/manual/cloud-code/manual/cheat-prevention)）。
- D1の `batch()` は渡した文を1つのトランザクションとして順に実行し、途中の文が失敗すると全体を戻す（[D1 Database API](https://developers.cloudflare.com/d1/worker-api/d1-database/)）。
- 再送で同じ処理を二重に行わないよう、端末が作った一意のキーで同じ要求を見分ける（[Idempotency-Key ヘッダーのIETFドラフト](https://datatracker.ietf.org/doc/draft-ietf-httpapi-idempotency-key-header/)。ドラフトは失効しているため考え方だけを参照し、既存のルーン請求と同じく本文の `requestId` で受け取る）。

## 方針

このプロジェクトの状況から判断した方針である。

1. サーバーの機能は `server/src/features/party/` の1つにまとめる。キャラ・編成・カード・Lvはどれもクライアントの `PartySession` が持つデータで、テーブルが外部キーでつながるためである。
2. UPTボーナスと同じく、画面を開くたびに `GET` で読み、1操作ごとに保存してから表示を変える。保存に失敗したら画面を変えずに通知の帯で知らせる。
3. レベルアップの費用はサーバーで計算する。端末は「今のLv」「上げたあとのLv」「要求ID」だけを送り、サーバーが残高の確認・ルーンの減算・Lvの更新を1回の `batch()` で行う。
4. Lvの上限と1レベルあたりの費用はサーバーを正本にし、`GET` の応答で返す。クライアントは接続先がないときだけ仮データ（`TrainingMockData`）の値を使う。費用の式（Lv n から n+1 へ n × 1レベルあたりの費用）は両方に置く。
5. 初めて読むユーザーには、仮データの9人・Lv・カード・4人の編成を1回だけ付与する（UPTボーナスの初期付与と同じ扱い。[決めた事項](#決めた事項)の1）。
6. カードは、知っているカードIDか、持っているキャラか、そのキャラの4枠で重複しないかをサーバーで判定する。属性の決まりはクライアントだけで判定する（[決めた事項](#決めた事項)の2）。
7. 接続先がないとき（展示室のプレビューなど）は、今と同じく仮データを使い、変更はアプリを動かしている間だけ残す。

ステータスの伸び、スキルの名前・効果・解放するLvは、画面に出すだけでサーバーの判定に使わないため、クライアントの仮データに残す。

## API案

どれもログイン中のセッション（ゲストを含む）が必要で、ないときは401を返す。
変更するAPIは、結果と最新の状態をまとめて返す。

| API | 内容 |
|---|---|
| `GET /v1/party` | 持っているキャラ（ID・Lv・4枠のカード）、4つの枠のキャラ（空きはnull）、所持ルーン、Lvの規則（上限・1レベルあたりの費用）を返す。初めて読むユーザーには初期データを付与する |
| `PUT /v1/party/slots/{0〜3}` | 本文 `{"characterId": "..."}` で枠に入れる。編成の画面と同じ決まりで、パーティにいるキャラを別の枠へ入れる操作は受け付けない（409 `already_in_party`） |
| `DELETE /v1/party/slots/{0〜3}` | 枠のキャラを外して空きにする。最後の1人は外せない（409 `keep_one`） |
| `PUT /v1/party/characters/{id}/cards/{0〜3}` | 本文 `{"skillId": "..."}` でカードを入れる。そのキャラのほかの枠にあれば2つの枠を入れ替える |
| `POST /v1/party/characters/{id}/level-up` | 本文 `{"requestId": "...", "fromLevel": 12, "toLevel": 15}`。費用を計算し、ルーンを引いてLvを上げる。今のLvが `fromLevel` と違えば409 `level_changed`、ルーンが足りなければ409 `insufficient_runes` で、どちらもDBを変えない。同じ `requestId` の再送には、記録済みの結果を返す |

`GET` の応答の形は次を想定する。

```json
{
  "characters": [{ "id": "toma", "level": 12, "cards": ["Fire", "Meteor", "Ice", "Blizzard"] }],
  "slots": [{ "slot": 0, "characterId": "toma" }],
  "runes": 8450,
  "rules": { "maxLevel": 30, "costPerLevel": 100 }
}
```

知らないIDや範囲外の枠・Lvは400 `invalid_request`、持っていないキャラは404 `character_not_owned` とする。
外す操作を `PUT` の `null` にしないのは、クライアントが本文を作る `JsonUtility` が `null` の文字列を空文字にして送るためである。
クライアントの通信部品（`ServerApiException`）は応答の `error` を読まずにステータスだけを持つ。
そこで、レベルアップが409で失敗したときは状態を読み直して描き直し、「レベルアップできませんでした」と知らせる。
画面は足りないルーンでは押せないため、409になるのは別の端末で操作したなど、手元の表示がずれた場合に限られる。

## テーブル案

| テーブル | 内容 |
|---|---|
| `party_profiles` | 初期データを付与済みのユーザー |
| `party_characters` | ユーザーとキャラごとに1行。Lv（1以上）と入手・更新日時 |
| `party_slots` | ユーザーと枠（0〜3）ごとに1行。空きの枠は行を持たない。同じキャラを2つの枠に入れられない一意制約と、持っているキャラだけを入れられる外部キーを持つ |
| `party_character_cards` | ユーザー・キャラ・枠（0〜3）ごとに1行。1人のキャラの4枠で同じカードを持てない一意制約と、`party_characters` への外部キーを持つ |
| `party_level_ups` | レベルアップ1回ごとに1行。要求ID（一意）・キャラ・上げる前と後のLv・使ったルーン・使ったあとの残高。ルーンの消費の記録を兼ねる |

既存のルーン台帳（`rune_ledger`）は、歩数の日と請求に必ず結び付き、増える量だけを記録する作りで、消費を書けない。
台帳を作り直すと運動報酬の処理まで変わるため、消費は `party_level_ups` に記録し、残高（`rune_wallets`）だけを共有する。
残高は「台帳の合計 − レベルアップで使った合計」に一致する。
復活や召喚でもルーンを使うようになった時点で、消費の記録を1つの台帳にまとめるかを改めて決める。

## レベルアップの書き込み

レベルアップは、次の文を1回の `batch()` で実行する。
既存のルーン請求（`exercise-rewards/repository.ts`）と同じく、最初の文が行を作れたときだけ後の文が効くよう、各文に「この要求の記録が存在する」条件を付ける。

1. `party_level_ups` へ、`party_characters` と `rune_wallets` を結んだ `INSERT ... SELECT` で記録を入れる。条件は、そのキャラのLvが `fromLevel` であることと、残高が費用以上であることである。`requestId` が既にあれば何もしない。
2. 記録があれば、`rune_wallets` の残高から費用を引く。
3. 記録があれば、`party_characters` のLvを `toLevel` にする。
4. 記録に、使ったあとの残高を書き込む。

実行後に `requestId` の記録を読み、あれば成功（再送ならそのときの結果）として返す。
なければ、Lvのずれか残高の不足かを判定して409を返す。
同じ `requestId` で中身の違う要求が届いた場合は、記録と比べて409を返す。

## 影響範囲

### サーバー

| 場所 | 変更 |
|---|---|
| `server/src/features/party/`（新規） | `catalog.ts`（キャラ9人とカード50枚のID、初期データ、Lvの規則）、`db-schema.ts`、`schema.ts`、`repository.ts`、`routes.ts`、`routes.test.ts` |
| [app.ts](../../server/src/app.ts) | `createPartyApi()` を接続する |
| `server/migrations/0004_party.sql` と `meta/`（新規） | `pnpm db:generate --name party` で生成する |
| [exercise-rewards/db-schema.ts](../../server/src/features/exercise-rewards/db-schema.ts) | 変更しない。`party` のrepositoryが `rune_wallets` を読み書きする |
| `server/tests/scenarios/party-level-up.test.ts`（新規） | 歩数の保存 → ルーンの請求 → レベルアップ → 残高の確認を、複数のAPIを通して確かめる |

### クライアント

| 場所 | 変更 |
|---|---|
| `Features/Party/Runtime/PartyApiClient.cs`（新規） | APIの呼び出しとJSONの型 |
| `Features/Party/Runtime/PartySource.cs`（新規） | `IPartySource`・`PartyServerSource`・`PartyState`。UPTボーナスの [StepBonusSource](../../client/Assets/Baryonyx/Features/StepBonus/Runtime/StepBonusSource.cs) と同じ形にする |
| [PartySession](../../client/Assets/Baryonyx/Features/Party/Runtime/PartySession.cs) | `Source` を持ち、サーバーから読んだ状態（編成・Lv・カード・所持ルーン・Lvの規則）に置き換える。接続先がないときは今の辞書のまま |
| [PartyFormation](../../client/Assets/Baryonyx/Features/Party/Runtime/PartyFormation.cs) | 持っているキャラをサーバーの状態から決める。名前と絵は仮データから引く |
| 編成の [Presenter](../../client/Assets/Baryonyx/Features/Party/Runtime/PartyFormationPresenter.cs)・[View](../../client/Assets/Baryonyx/Features/Party/Runtime/PartyFormationView.cs) | 開くたびに読み込み（読み込むまで「読み込み中…」）、入れ替えを保存してから表示、失敗の通知 |
| カードスキルの [Presenter](../../client/Assets/Baryonyx/Features/CardLoadout/Runtime/CardLoadoutPresenter.cs)・[View](../../client/Assets/Baryonyx/Features/CardLoadout/Runtime/CardLoadoutView.cs)・[SessionStore](../../client/Assets/Baryonyx/Features/CardLoadout/Runtime/CardLoadoutSessionStore.cs) | 4枚をまとめて書く `SetCards` を、1枠ずつ保存する非同期の処理に替える |
| 育成の [Presenter](../../client/Assets/Baryonyx/Features/Training/Runtime/TrainingPresenter.cs)・[View](../../client/Assets/Baryonyx/Features/Training/Runtime/TrainingView.cs)・[Session](../../client/Assets/Baryonyx/Features/Training/Runtime/TrainingSession.cs) | 確定を非同期にし、保存中は押せなくする。接続先があるときは、所持ルーンはサーバーの残高、上限と費用はサーバーの規則を使う。端末だけでルーンを引く `Spend` は、接続先がないときだけに使う |
| [HomeBootstrap](../../client/Assets/Baryonyx/App/Runtime/HomeBootstrap.cs)・[HomePresenter](../../client/Assets/Baryonyx/Features/Home/Runtime/HomePresenter.cs) | 所持ルーンを酒場へ渡す処理（`TrainingSession.HomeRunes` と、そのためだけにあった `HomePresenter.Runes`）を削除する。Homeが所持ルーンを持つのは接続先があるときだけで、そのときは酒場が `GET` で残高を読むためである |
| [GameServices](../../client/Assets/Baryonyx/App/Runtime/GameServices.cs) | `PartyServerSource` を作り、`PartySession.Source` に渡す |
| EditModeテスト | 編成・育成・カードスキルのPresenterのテストを非同期の保存に合わせ、`PartyServerSource` の変換のテストを加える |
| PlayModeテスト | `TavernFormationTests`・`TavernTrainingTests`・`TavernCardLoadoutTests` に、偽のサーバー（`Tests/PlayMode/Support/FakePartySource.cs`）での読み込み・保存・失敗を加える |

読み込み中の表示は、既存の文字（「所持 〇人」、所持ルーン、枚数）に出す。
この場合Prefabの作り直しは要らない見込みで、要るときは `Baryonyx > Tavern > Create Screen Assets` で作り直す。
展示室は接続先がないため、表示は今の仮データのままである。
登録内容の変更は要らない見込みだが、3つのパネルがプレビューで今までどおり出ることを確かめる。

### 文書

- [パーティ](../features/party.md)・[育成と強化](../features/progression.md)・[ルーン経済](../features/economy.md)（レベルアップでの消費の実装）・[アカウントとセーブ](../features/accounts-save.md)（保存項目）
- [画面一覧](../features/screens.md)の編成・育成・カードスキル（「アプリを動かしている間だけ残る」の記述）
- [機能索引の現在の実装](../features/README.md#現在の実装)、[バックエンドの開発環境](../rules/backend-design.md)のマイグレーションとテストの一覧

### 今回変えないもの

- Homeに立つ4人と戦闘画面のモックへの、編成の反映
- キャラの加入、カードの所持、ステータス・スキルのサーバー保存
- ゲストとGoogleアカウントの結び付け（アプリのデータを消すと、ゲストの保存内容へは戻れないまま）
- 冒険中に編成やカードを変えられない決まり（冒険の実装とあわせて作る）

## 進める順序

| 順 | 作業 | 完了の条件 |
|---|---|---|
| 1 | サーバー：テーブル・マイグレーション・初期付与・`GET` | `routes.test.ts` で初期付与が1回だけ・ユーザーごとに分かれることを確認。`pnpm db:check` が通る |
| 2 | サーバー：編成とカードの `PUT` | 入れ替え・外す・最後の1人・持っていないキャラ・カードの入れ替えをテストで確認 |
| 3 | サーバー：レベルアップ | 残高の減算、Lvのずれ・残高不足でDBを変えないこと、再送で二重に引かないことをテストとシナリオで確認。`pnpm test`・`typecheck`・`lint` が通る |
| 4 | クライアント：通信部品・`PartySession`・`GameServices` | 応答の変換をEditModeで確認 |
| 5 | クライアント：編成 → カードスキル → 育成の順に、画面を保存つきへ替える | 各画面のPresenterのEditModeテストと、対応する酒場のPlayModeテストクラスが通る |
| 6 | 展示室の確認と文書の更新 | 展示室で3つのパネルが出る。文書のリンクと記述が実装と合う |
| 7 | ローカルのサーバーとの結合確認 | 下の受入条件を、Editorの接続先Localで確かめる |

画面は、ルーンが絡まない編成から始め、ルーンの減算がある育成を最後にする。
手順7の `pnpm dev` はユーザーが起動する。
Androidエミュレーターでの確認は任意とし、行う場合は起動と操作をユーザーに依頼する。

## 受入条件

- 編成・カード・Lvを変えてからPlayを止めて再開しても、変えた内容が残る。
- レベルアップでサーバーの残高が減り、Homeへ戻っても同じ所持ルーンを表示する。
- 同じ要求の再送でルーンを二重に引かない。残高不足とLvのずれは拒否し、DBを変えない。別のユーザーの内容が混ざらない（サーバーのテスト）。
- 保存に失敗したときは画面を変えず、通知の帯で知らせる。
- 接続先がないとき（展示室）は、今までどおり仮データで動く。

## 検証結果

2026-10-04に次を確かめた。

| 対象 | 結果 |
|---|---|
| サーバー（`pnpm lint`・`typecheck`・`db:check`・`test`・`test:ci`） | すべて成功。`pnpm test` は14ファイル68件、`test:ci` は12件 |
| パーティAPIのテスト（[routes.test.ts](../../server/src/features/party/routes.test.ts)） | 認証・入力エラー・初期付与・編成とカードの変更・最後の1人・レベルアップの再送と拒否・ユーザーごとの分離の8件が成功 |
| レベルアップのシナリオ（[party-level-up.test.ts](../../server/tests/scenarios/party-level-up.test.ts)） | 歩数から請求した7,000ルーンで2,500ルーンを使い、同時に届いた2つの要求のうち1つだけが通って残高が3,400になることを確認 |
| ローカルのサーバー（`pnpm dev`、ローカルのD1に `0004_party.sql` を適用） | ゲストで初期付与・枠の入れ替えと外す・カードの付け替え・ルーン不足のレベルアップの拒否（409）・読み直しで変更が残ることを確認 |
| Unity EditMode（`Baryonyx.EditModeTests`） | 360件すべて成功 |
| Unity PlayMode（`Baryonyx.PlayModeTests`） | 85件すべて成功。酒場の3画面のサーバー版の3件を含む |
| CSharpier | 変更した22ファイルの整形検査が成功 |

実装中に、初期付与のカード36行（5列で180個の値）を1つの文で入れると、D1の上限（1つの文に100個まで）を超えて失敗した。
キャラごとの文に分けて直し、上限を[バックエンドの開発環境](../rules/backend-design.md#drizzleによるdb操作)に記した。

次は確かめていない。

- Unity Editorで接続先をLocalにし、TopからHomeを経て酒場で変更し、Playを止めて再開しても残ること
- Androidエミュレーターでの動作と、Dev環境への公開後の動作
- 展示室は接続先がないため、表示は今までどおり仮データのままである。展示室のシーンのテストは成功したが、読み込み中の表示は展示室では見られない

## 決めた事項

2026-10-04、提案した2つの案をユーザーが採った。

1. **初期データ**：初めて読むユーザーに、仮データの9人（Lv 12〜1）・各4枚のカード・4人の編成を付与する。UPTボーナスの初期付与と同じ扱いで、今の画面の見た目がそのまま残る。
2. **カードの属性の判定**：サーバーでは属性の決まりを判定しない。属性の割り当ても所持の仕組みも仮で、50枚の属性とキャラごとの属性をサーバーにも置くと、仮の決まりを変えるたびに2か所を直す必要があるためである。

## 変更と判断の記録

- 2026-10-04：ユーザーの依頼で、酒場の編成・カードスキル・レベルをサーバーに保存する計画を作った。
- 2026-10-04：ユーザーが初期データ（仮データの付与）とカードの属性の判定（サーバーでは判定しない）の2案を採り、実装を始めた。クライアントが `null` を送れないため、枠から外すAPIを `DELETE` に分けた。
