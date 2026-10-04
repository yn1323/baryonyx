# ドキュメント索引

分野の索引か、作業別の表から入り、対象の文書だけを読む。
文書の置き場所・書き方・更新方法は[文書管理方針](rules/documentation-policy.md)、AIの作業の入口は[文書作業の入口](AGENTS.md)、リポジトリ全体の制約は[共通指示](../AGENTS.md)に従う。

## 分野の索引

| 分野 | 索引 | 含むもの |
|---|---|---|
| ゲーム企画 | [企画索引](game/README.md) | ゲーム概要、世界設定、用語集、MVP、初期企画と企画見直しの記録 |
| 機能仕様 | [機能索引](features/README.md) | 運動とACT、冒険・戦闘・育成、経済と課金、画面と体験、アカウントと基盤 |
| 個別データ | [データ索引](catalog/README.md) | キャラ・敵・スキル・装備・ステージなどの個別定義と記入テンプレート |
| アート | [アート索引](art/README.md) | 画風とドット絵の規格、3Dの舞台、外見・性格の比較索引 |
| 設計と開発ルール | [開発ルール索引](rules/README.md) | clientとserverの構成、保存先、UI、テスト、ビルドと配布、エミュレーター、ショートカット、文書管理 |
| 設定 | [設定索引](settings/README.md) | 設定値の所在と変更手順 |
| QA | [QA索引](qa/README.md) | ユーザーが困ったときの確認手順 |
| 計画と記録 | [計画索引](plans/README.md) | 変更ごとの計画・判断・検証の記録と、その状態 |
| 保管庫 | [資料の保管庫](archive/README.md) | 削除した画面や廃止した仕様の記録 |
| 全体構成 | [システム全体の構成](architecture.md) | clientとserverと保存先の境界、未決事項 |
| 文書の検査 | [文書検査](tools/README.md) | 生成索引の更新と、リンク・ID・メタデータの検査 |
| スキル（doc外） | [スキル索引](../.agents/skills/README.md) | AIが特定の作業を行うときの進め方と呼び出し方 |

## 作業別に最初に読む文書

| 作業 | 最初に読む | 次に読む |
|---|---|---|
| 仕様を足す・変える | [仕様追加時の矛盾の扱い](rules/documentation-policy.md#仕様追加時の矛盾の扱い) | [機能索引](features/README.md)から対象の仕様、[用語集](game/glossary.md) |
| 画面・シーンを作る・直す | [画面一覧と操作](features/screens.md) | [シーンと生成メニュー](rules/frontend-design.md#シーンと生成メニュー)・[UI設計ルール](rules/ui-design.md)・[クライアントアセット展示室](features/showcase.md) |
| APIとDBを足す・開発用データを入れる | [バックエンドの開発環境](rules/backend-design.md) | [開発用データ（seed）](rules/backend-design.md#開発用データseed)・[保存先の決め方](rules/data-storage.md)・[アカウントとセーブ](features/accounts-save.md) |
| テストを実行・追加する | [UnityのテストとCI](rules/client-testing.md)・[serverの検査とテスト](rules/backend-design.md#検査とminiflareテスト) | [serverのテストの並列と独立性](../server/tests/scenarios/AGENTS.md)・[整形と静的解析](rules/client-code-quality.md) |
| APKをビルド・配布する | [手動実行用ショートカットの動作](rules/local-shortcuts.md) | [Androidビルドと実機確認](rules/client-android-testing.md)・[実行環境と接続先](features/startup-sync.md#実行環境と接続先)・[エミュレーター](rules/client-android-emulator.md) |
| キャラ・敵・スキルを足す | [データ索引](catalog/README.md) | [外見・性格の比較索引](art/visual-index.md)・[戦闘システム](features/combat.md)・[キャラクターとパーティ編成](features/party.md) |
| 画像・3Dの舞台を作る | [アート方針](art/direction.md) | [3Dの舞台（HD-2D）](art/hd2d-stage.md)・[ゲーム内の対象の画像](rules/frontend-design.md#ゲーム内の対象の画像) |
| 設定値を探す | [設定索引](settings/README.md) | 各行の正本・確認先 |
| ユーザーの困りごとに答える | [QA索引](qa/README.md) | [起動時の連携と歩数の同期](features/startup-sync.md) |
| 経緯を調べる | [計画索引](plans/README.md) | 各文書の「変更と判断の記録」・[企画索引](game/README.md)の記録・[資料の保管庫](archive/README.md) |
| 文書を足す・移す | [文書管理方針](rules/documentation-policy.md) | [文書検査](tools/README.md) |
