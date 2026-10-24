---
name: server-api-authoring
description: >
  server/ のHonoのAPI・repository・DB操作（Drizzle・D1）を足す・直す・レビュー・リファクタするときに使う。
  APIが遅い、D1との往復を減らしたい、書き込みのAPIを足す、持ち物やルーンを増減する、ほかの機能の表を読む、
  初期データやセッションの扱いを変える、エラーとHTTPステータスを決める、といった作業に当てはめる。
  保存先（端末かサーバーか）を決める判断は data-storage が担当する。Unityのクライアントだけの作業、
  マイグレーションの書き方だけの作業、文書だけの作業には使わない。
---

# サーバーのAPIを書く

`server/` のAPIを、D1との往復が少なく、再送や同時の要求で壊れない形で書き、テストと文書まで仕上げる進め方を扱う。
規則・数値・配置の正本は[バックエンドの開発環境](../../../doc/rules/backend-design.md)であり、このスキルには写さない。
作業には[ルートのAGENTS.md](../../../AGENTS.md)と[serverのAGENTS.md](../../../server/AGENTS.md)を適用する。

## 使う場面

- APIを足す・直す、repositoryを分ける・まとめる
- 書き込みのAPIで、ルーン・持ち物・進行を増減する
- 「APIが遅い」「往復を減らしたい」と言われたとき
- サーバーの差分をレビューするとき

保存先をまだ決めていないデータは、先に [data-storage](../data-storage/SKILL.md) で決める。

## 手順

1. **読む**：[D1との往復と文の数](../../../doc/rules/backend-design.md#d1との往復と文の数)、[実装と依存の配置](../../../doc/rules/backend-design.md#実装と依存の配置)、[認証とAPIの組み立て](../../../doc/rules/backend-design.md#認証とapiの組み立て)、対象機能の仕様を読む。対象機能に `state.ts` があれば、状態の読み方をそこから使う。
2. **往復を設計する**：書く前に、1回の要求で何を読み、何を書き、何を返すかを並べる。読む文を1つ目のbatch、書く文と書いたあとに読む文を2つ目のbatchに振り分け、往復が多くても2回に収まることを確かめる。収まらないときは、どの読み込みが前の結果に依存しているかを書き出し、条件を書き込みの文へ移せないかを考える。
3. **共通の部品を探す**：書く前に `server/src/shared/` と [inventory/](../../../server/src/features/inventory/) と [wallet.ts](../../../server/src/features/exercise-rewards/wallet.ts) を検索する。持ち物の付与、ルーンの記録、条件付きの多数行の挿入、倍率と品質の応答、重み付きの抽選、エラーのステータスは、すでに部品がある。同じ処理を機能内に書き直さない。
4. **書く**：確かめた条件は書き込みと同じ文に入れ、要求IDで再送を見分ける。ほかの機能の状態は、その機能のrepositoryを呼ばずに読む文を受け取る。新しいエラーは [http.ts](../../../server/src/shared/http.ts) の `errorStatus` の表に合わせる。
5. **確かめる**：下の「確かめること」を実行する。
6. **記録する**：APIの仕様は対象機能の文書、規則を変えたら [backend-design.md](../../../doc/rules/backend-design.md) と、それぞれの「変更と判断の記録」を同じ変更で直す。
7. **報告する**：変えたAPIと往復の回数（変更前後）、テストの結果、確かめていないこと（DevやProdでの実時間など）を分けて書く。

## 判断の観点

作るときとレビューのときに、次を確かめる。各項目の根拠はリンク先にある。

- [ ] 1回の要求のD1との往復が、一覧のGETで1回、書き込みのAPIで2回までに収まっている（[D1との往復と文の数](../../../doc/rules/backend-design.md#d1との往復と文の数)）。
- [ ] `await` を重ねて順に読んでいない。書いたあとに読み直すためだけの往復を足していない。
- [ ] 書けなかった理由を見分ける読み込みを、書き込みと同じbatchに並べている。
- [ ] 確認と書き込みの間に割り込まれても壊れないよう、確かめた条件を書き込みの文に入れている（[DrizzleによるDB操作](../../../doc/rules/backend-design.md#drizzleによるdb操作)）。
- [ ] ルーンや持ち物を増減するAPIは要求IDを受け取り、`runeRecord` と `written` の条件で再送を二重に処理しない。
- [ ] 持ち物は `inventoryGrants` で足し、機能ごとに付与のSQLを書いていない。
- [ ] ほかの機能のrepositoryを呼んで状態を丸ごと読んでいない。ほかの機能の `repository.ts` から定数や部品を読み込んでいない（[実装と依存の配置](../../../doc/rules/backend-design.md#実装と依存の配置)）。
- [ ] 条件付きで多くの行を入れる文を、行ごとの文に分けていない（`insertRowsWhen`）。
- [ ] 読み込みのAPIで初期データを付与していない。`requireSession` を機能のパスに1つだけ付けている（[認証とAPIの組み立て](../../../doc/rules/backend-design.md#認証とapiの組み立て)）。
- [ ] 同じエラーを、ほかの機能と違うステータスで返していない（`errorStatus`）。
- [ ] 応答の数値に null を入れていない。倍率を持たない物は項目を省いている（`qualityFields`。UnityのJsonUtilityが数値の null を読めないため）。
- [ ] repositoryが大きくなったら、操作のまとまりごとにファイルを分けている。

## 確かめること

- **往復の回数**：足した・変えたAPIの回数を [read-round-trips.test.ts](../../../server/tests/scenarios/read-round-trips.test.ts) に足すか、応答の `Server-Timing` の `d1` の回数をテストで確かめる（[処理時間の計測](../../../doc/rules/backend-design.md#処理時間の計測server-timing)）。
- **テスト**：作業中は変えた機能の `routes.test.ts` とシナリオに絞って実行し、commit前は `server/` で `pnpm test` を全件実行する（[commitの手順](../../../AGENTS.md#commitの手順)）。再送・同時の要求・ほかのユーザーの物を扱うテストを含める。
- **検査**：`server/` で `pnpm lint` と `pnpm typecheck` を通す。
- **つないだ確認**：ローカルのサーバーで確かめるときは、`pnpm dev` の起動をユーザーに頼む（[ルートのAGENTS.md](../../../AGENTS.md)）。
