# パッシブの定義

[データ索引](../README.md) / [関連仕様](../../features/passives.md)

個別内容と決定状態は、下の索引から各定義を参照する。
パッシブを考えるときは[パッシブスキルの設計方針](../../features/passives.md#設計方針)に従う。
画像化の要否は個別に決め、画像が必要なら画像対象テンプレートを使う。
IDの接頭辞は `passive-`、メタデータの `category` は `passives` とする。

## 個別データの索引

<!-- entries:start -->
| ID | 名称 | 状態 | 詳細 |
|---|---|---|---|
| passive-chained-arrows | 連ね矢 | 候補 | [定義](passive-chained-arrows.md) |
| passive-charged-shield | 帯電の盾 | 候補 | [定義](passive-charged-shield.md) |
| passive-copycat | 見よう見まね | 候補 | [定義](passive-copycat.md) |
| passive-cover | かばう | 候補 | [定義](passive-cover.md) |
| passive-head-start | 早駆け | 候補 | [定義](passive-head-start.md) |
| passive-holy-flame-hammer | 聖火の鉄槌 | 候補 | [定義](passive-holy-flame-hammer.md) |
| passive-iai-form | 居合の型 | 候補 | [定義](passive-iai-form.md) |
| passive-mana-shield | 魔力の盾 | 候補 | [定義](passive-mana-shield.md) |
| passive-mark-the-weak | 急所狙い | 候補 | [定義](passive-mark-the-weak.md) |
| passive-open-wounds | 傷口を開く | 候補 | [定義](passive-open-wounds.md) |
| passive-overflowing-prayer | あふれる祈り | 候補 | [定義](passive-overflowing-prayer.md) |
| passive-purifying-vow | 浄めの誓い | 候補 | [定義](passive-purifying-vow.md) |
| passive-riposte | 切り返し | 候補 | [定義](passive-riposte.md) |
| passive-silent-kill | 影の一刺し | 候補 | [定義](passive-silent-kill.md) |
| passive-standing-alone | 独り立ち | 候補 | [定義](passive-standing-alone.md) |
| passive-star-chart | 星図の計算 | 候補 | [定義](passive-star-chart.md) |
| passive-thermal-shock | 寒暖の理 | 候補 | [定義](passive-thermal-shock.md) |
| passive-walkers-blessing | 歩み手の加護 | 候補 | [定義](passive-walkers-blessing.md) |
<!-- entries:end -->

## 記入する項目

| 項目 | 初期の扱い |
|---|---|
| 所持者・解放するレベル | キャラ1人に2つ（[キャラクターの構成](../../features/party.md#キャラクターの構成)）。解放するレベルは[仮設定](../../features/passives.md#解放するレベル) |
| 発動条件・効果ID・効果量・対象 | きっかけと働きの組で書く。量はステータスの割合で書く（[設計方針](../../features/passives.md#設計方針)） |
| 常時・条件付き・回数制限 | 働く回数を、戦闘の長さと敵の数で見積もって書く（[値打ちを見積もる](../../features/passives.md#値打ちを見積もる)） |
| 重複・相互作用・対象外 | ほかのパッシブ・カード・固有スキルの進化の枝とのつながり（[パッシブ同士のつながり](../../features/passives.md#パッシブ同士のつながり)） |
| 表示文・アイコンの要否 | 説明文はカードと同じ決まりで書く。アイコンは24×24ドットで1個ずつ描き、[画像対象テンプレート](../templates/visual-entity.md)で記録する |

## 追加方法

[画像対象テンプレート](../templates/visual-entity.md)または[定義テンプレート](../templates/definition.md)をこの分類にコピーし、1対象1ファイルで記入する。
既存名・ID・画像の有無を確認し、未決欄を推測で埋めない。
追加後は[索引を再生成](../../tools/README.md)する。
