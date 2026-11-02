# 職業の定義

[データ索引](../README.md) / [関連仕様](../../features/party.md)

個別内容と決定状態は、下の索引から各定義を参照する。
画像化の要否は個別に決め、画像が必要なら画像対象テンプレートを使う。
IDの接頭辞は `class-`、メタデータの `category` は `classes` とする。

2026-09-27の企画見直しでキャラの構成に職業がなくなり、職業を残すかは[パーティ仕様の確認待ち](../../features/party.md#確認待ち)である。
今のコードにある4つの職は、仮の設定として[今の職](#今の職仮設定)に書く（2026-11-02、ユーザーの判断）。
回答が出るまで、この分類に個別データを追加しない。

## 個別データの索引

<!-- entries:start -->
登録された個別データはまだない。
<!-- entries:end -->

## 今の職（仮設定）

今のコードは、9人を4つの職に分け、同じ職のキャラが固有スキル3つと、その倍率の上限を共有する。
正本はサーバーの [party/catalog.ts](../../../server/src/features/party/catalog.ts) の `CHARACTER_CLASSES`・`UNIQUE_MAX_PERCENT_BY_CLASS` と、クライアントの [UniqueSkillTrees](../../../client/Assets/Baryonyx/Features/SkillEvolution/Runtime/UniqueSkillTrees.cs) で、下の表はその写しである。

| 職 | コードのID | キャラ | 固有スキル（Lv1・Lv10・Lv30） |
|---|---|---|---|
| 魔法使い | `mage` | [トーマ](../characters/character-toma.md)・[ルッツ](../characters/character-lutz.md) | マナバースト・星降り・魔眼 |
| 弓使い | `archer` | [ルカ](../characters/character-luka.md)・[グレタ](../characters/character-greta.md)・[リツ](../characters/character-ritsu.md) | 雷撃の矢・矢の雨・速射 |
| 騎士 | `knight` | [アリア](../characters/character-aria.md)・[アンセルム](../characters/character-anselm.md) | 盾の構え・一閃・鼓舞 |
| 癒し手 | `healer` | [ミナ](../characters/character-mina.md)・[リタ](../characters/character-rita.md) | 癒しの風・蘇生・裁きの光 |

固有スキルの中身は[固有スキルの覚醒のツリー](../../features/skill-evolution/trees.md)にある。
職による装備の制限はなく、職ごとの役割・得意不得意は未決である。[装備の属性](../../features/equipment.md#装備の属性)で付けられる装備も、職ではなくカードの属性で決める（2026-11-03）。

## 記入する項目

| 項目 | 初期の扱い |
|---|---|
| 役割・得意不得意・キャラクターID | 役割・得意不得意は未決。今の職とキャラの組み合わせ（仮）は[今の職](#今の職仮設定)にある |
| 装備可能武器種・防具種 | 装備は基本的にどのキャラでも付けられる（[装備](../../features/equipment.md)）。職業による制限は置き換え済み |
| ステータス傾向・戦闘上の責務 | 未決 |
| 固有意匠・モチーフ・紋章の要否 | 未決 |

## 追加方法

[個別データの作り方](../README.md#個別データの作り方)に従う。
