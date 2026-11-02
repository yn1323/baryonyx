# キャラクターの定義

[データ索引](../README.md) / [関連仕様](../../features/party.md)

個別内容と決定状態は、下の索引から各定義を参照する。
画像対象テンプレートを使う。
IDの接頭辞は `character-`、メタデータの `category` は `characters` とする。

## 個別データの索引

<!-- entries:start -->
| ID | 名称 | 状態 | 詳細 |
|---|---|---|---|
| character-anselm | アンセルム | 候補 | [定義](character-anselm.md) |
| character-aria | アリア | 候補 | [定義](character-aria.md) |
| character-greta | グレタ | 候補 | [定義](character-greta.md) |
| character-luka | ルカ | 候補 | [定義](character-luka.md) |
| character-lutz | ルッツ | 候補 | [定義](character-lutz.md) |
| character-mina | ミナ | 候補 | [定義](character-mina.md) |
| character-rita | リタ | 候補 | [定義](character-rita.md) |
| character-ritsu | リツ | 候補 | [定義](character-ritsu.md) |
| character-toma | トーマ | 候補 | [定義](character-toma.md) |
<!-- entries:end -->

## 記入する項目

| 項目 | 初期の扱い |
|---|---|
| 役割・加入方法 | 未決。職業を持たせるかは[パーティ仕様の確認待ち](../../features/party.md#確認待ち) |
| 固有スキルID（3つ）・パッシブスキルID（2つ）と解放レベル | 個別設定は未決。構成は[キャラクターの構成](../../features/party.md#キャラクターの構成)に従う |
| 固有スキルの覚醒のツリー（固有スキルごとに7つの段階の効果） | 個別設定は未決。構成は[覚醒の段階とツリー](../../features/skill-evolution/rules.md#覚醒の段階とツリー)に従う |
| 装備できるカードの属性 | 個別設定は未決 |
| 基礎ステータス・レベルによる成長 | 未決 |
| 限界突破（コードでは `bond`）の段階と効果（6段） | 段数は[キャラが被ったとき](../../features/monetization.md#キャラが被ったとき)に従う。効果はキャラごとに決め、決まるまでは同じ節の全キャラ共通の仮の値を使う |
| 年齢感・体格・頭身・輪郭・顔・髪・目 | 未決 |
| 衣装・素材・装飾・装備・主色・差し色 | 未決 |
| 価値観・欲求・弱点・葛藤・得意不得意 | 未決 |
| 行動の癖・口調・対人距離・仲間との関係 | 未決 |
| 経歴・所属・物語上の役割 | 未決 |
| 戦闘ドット絵とイラストで維持する特徴 | 未決 |

外見・性格・経歴・維持する特徴の欄は、ユーザーが採用したキャラ設定（[rpg-character-profile](../../../.agents/skills/rpg-character-profile/SKILL.md) の出力）から写す。
設定の作業用の写し（`output/characters/<キャラID>/profile.md`）はGitの対象外で、正本にしない。
採用していない設定で、未決の欄を埋めない。

## 追加方法

[個別データの作り方](../README.md#個別データの作り方)に従う。
