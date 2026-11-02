---
id: character-ritsu
type: visual-entity
status: 候補
updated: 2026-11-02
name: リツ
category: characters
silhouette: 白い短髪と淡い青のマフラー。紺のコートで片手を突き出し、氷と雷を放つ
palette: 紺 | 灰 | 白い髪 | 淡い青
motifs: 氷の結晶 | 雷 | マフラー
personality: 未決
art_status: 下書きのイラストを冒険者の画面で表示中
art_files: client/Assets/Baryonyx/Shared/Art/Characters/Illustrations/ritsu.png
---

# リツ

[キャラクターの定義](README.md) / [関連仕様](../../features/party.md)

仮データの9人の1人である。
正式な設定はまだなく、値はすべて仮設定である。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-24、ユーザーの依頼「個別のキャラの情報に書き込んでおいて」で、絆の段階を書くために作成した。ほかの値は既存の仮設定へリンクする |
| 役割・説明 | 役割の見立ては「氷と雷の見習い魔法使い」（[自キャラのLv100のステータス](../../features/progression.md#自キャラのlv100のステータス)）。正式な設定ではない |
| 関連する仕様・個別ID | [見よう見まね](../passives/passive-copycat.md)・[独り立ち](../passives/passive-standing-alone.md)・[キャラが被ったとき](../../features/monetization.md#キャラが被ったとき) |
| 実装・ゲームマスタの場所 | キャラID `ritsu`（[catalog.ts](../../../server/src/features/party/catalog.ts)）。名前・Lv・カードの仮データは [PartyAssets](../../../client/Assets/Baryonyx/Features/Party/Editor/PartyAssets.cs)。限界突破（`bond`）の段としるしは [TrainingBond](../../../client/Assets/Baryonyx/Features/Training/Runtime/TrainingBond.cs) とサーバーの [growth.ts](../../../server/src/features/party/growth.ts)、効果は全キャラ共通の仮の値（`TrainingBond.PercentPerStage`） |

## 確定した設定

なし（すべて仮設定）。

## 絆

同じキャラが被るたびに突破のしるしが1つ手に入り、使うと1段限界突破する。6段が上限である（[キャラが被ったとき](../../features/monetization.md#キャラが被ったとき)）。
効果は全キャラ共通の仮の値で、本番の効果はキャラごとに考える。

| 段 | 手に入れた数 | 効果（仮・合計） |
|---|---|---|
| 0 | 1体目 | なし |
| 1 | 2体目 | 全ステータス+2% |
| 2 | 3体目 | 全ステータス+4% |
| 3 | 4体目 | 全ステータス+6% |
| 4 | 5体目 | 全ステータス+8% |
| 5 | 6体目 | 全ステータス+10% |
| 6（上限） | 7体目 | 全ステータス+12% |

8体目からは、かけらに変える。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 役割・加入方法 | 役割の見立ては「氷と雷の見習い魔法使い」。加入方法は未決 | [自キャラのLv100のステータス](../../features/progression.md#自キャラのlv100のステータス) | 仮 |
| パッシブスキルと解放レベル | [見よう見まね](../passives/passive-copycat.md)（Lv 1）・[独り立ち](../passives/passive-standing-alone.md)（Lv 10） | [仮データの9人のパッシブ](../../features/passives.md#仮データの9人のパッシブ) | 仮 |
| 固有スキルと覚醒のツリー | [UniqueSkillTrees](../../../client/Assets/Baryonyx/Features/SkillEvolution/Runtime/UniqueSkillTrees.cs) の仮データ | [固有スキルの覚醒](../../features/skill-evolution.md) | 仮 |
| 装備できるカードの属性 | 未決 | | 未決 |
| 基礎ステータス・レベルによる成長 | Lv100の値は[表](../../features/progression.md#自キャラのlv100のステータス)のとおり。成長率は全キャラ共通 | [レベルとステータス](../../features/progression.md#レベルとステータス) | 仮 |
| 絆の段階と効果 | [絆](#絆)のとおり | [キャラが被ったとき](../../features/monetization.md#キャラが被ったとき) | 段数は確定、効果は仮 |
| 外見・性格・経歴 | 未決。今の絵は下書きである（[画像化の指示と記録](#画像化の指示と記録)） | | 未決 |

## 画像化の指示と記録

| 項目 | 内容 |
|---|---|
| 用途 | 冒険者の画面の縦長のイラスト（1024×1536の下書き。[パーティと冒険者のタブ](../../features/screens/adventurers.md)）。戦闘のドット絵はなく、4人の絵に色を掛けた仮の絵を使う（[PartyAssets](../../../client/Assets/Baryonyx/Features/Party/Editor/PartyAssets.cs)） |
| 出力画像のパス | `Illustrations/ritsu.png`（`client/Assets/Baryonyx/Shared/Art/Characters/`） |
| 採用・候補・不採用と理由 | 下書きを仮に使っている。外見の設定は未決で、メタデータの比較用の要約（silhouette・palette・motifs）は今の下書きのイラストから書いた |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 絆の効果 | 本番の効果を、このキャラの役割に合わせて決める。全ステータス+は仮の値 |
| 役割・加入方法・外見・性格 | 正式な設定 |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-11-02 | ユーザーの了承を得て、画像対象（visual-entity）にし、今の絵のパスと比較用の要約を書いた | キャラの絵が個別データと比較索引からたどれなかったため | [外見・性格の比較索引](../../art/visual-index.md) |
| 2026-11-02 | スキルとdocの整理で、今のコードに合わせて「実装・ゲームマスタの場所」の、限界突破（絆）を未実装とした記述と、[絆](#絆)の、被ると自動で1段上がるとした記述を直した | 根拠：段としるしと全キャラ共通の仮の効果が TrainingBond.cs とサーバーの party/growth.ts にある。被るとしるしが増える（server/src/features/summon/repository.ts） | [キャラが被ったとき](../../features/monetization.md#キャラが被ったとき) |
| 2026-10-24 | 絆の上限を6段に改めた | ユーザーの決定「いや、やっぱり6凸にしよう！」 | [キャラが被ったとき](../../features/monetization.md#キャラが被ったとき) |
| 2026-10-24 | 定義を作成し、[絆](#絆)の5段を仮の値で書いた | ユーザーの決定「5凸までにしましょう」「今は全ステータス＋でいいですが、ほんちゃんでは、じっくり考えたい」 | [キャラが被ったとき](../../features/monetization.md#キャラが被ったとき) |
