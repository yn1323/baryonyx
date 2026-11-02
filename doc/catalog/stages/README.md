# 探索する場所・ステージの定義

[データ索引](../README.md) / [関連仕様](../../features/stage-progression.md)

個別内容と決定状態は、下の索引から各定義を参照する。
画像対象テンプレートを使う。
IDの接頭辞は `stage-`、メタデータの `category` は `stages` とする。

## 個別データの索引

<!-- entries:start -->
登録された個別データはまだない。
<!-- entries:end -->

## 記入する項目

| 項目 | 初期の扱い |
|---|---|
| 場所の名前・地域ID・解放条件・推奨の強さ | 個別設定は未決 |
| 分岐ルート・マス・入口・深奥のボスとのつながり | 個別構造は未決。[分岐ルートの冒険](../../features/stage-progression/run.md#分岐ルートを選んで進む冒険)と[マス単位の探索](../../features/stage-progression/run.md#マス単位の探索と操作)に従う |
| 敵の傾向・弱点・狙える報酬・未探索情報 | 開示範囲と個別設定は未決。[選ぶための情報](../../features/stage-progression/run.md#行き先を選ぶ情報)に従う |
| 敵ID・ボスID・戦闘構成 | 未決 |
| 野営地・中継地点・再出発先 | 旧仕様の候補。1階からやり直す冒険との関係は[確認待ち](../../features/stage-progression/status.md#確認待ち) |
| 勝敗・撤退・時間制限の有無 | 未決 |
| 報酬テーブルID・採用する場合の変異条件 | 未決 |
| 地形・建築・植生・天候・時刻・背景の奥行き | 未決 |
| 主色・光源・材質・ランドマーク・空気感 | 未決 |
| カメラ・足場・前景・敵味方の視認性 | 未決 |
| 同じ地域のステージと区別する特徴 | 未決 |

今の行き先8つと、出る敵・Lvは[行き先と敵](../../features/stage-progression/destinations.md#行き先と敵)にあり、割り振りの正本は [AdventureCatalog](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/AdventureCatalog.cs) の `Areas` である。
IDと配置は維持し、プレイヤーに示す場所の名前・選択情報は[探索仕様](../../features/stage-progression.md)に従う。
共通背景と扉・階段・宝箱などの差分は[アセット構成の方針](../../features/stage-progression/destinations.md#アセットを増やしすぎない構成)を参照する。

## 追加方法

[個別データの作り方](../README.md#個別データの作り方)に従う。
