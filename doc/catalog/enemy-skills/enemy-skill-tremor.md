---
id: enemy-skill-tremor
type: definition
status: 候補
updated: 2026-10-19
name: 地響き
category: enemy-skills
---

# 地響き

[敵の技の定義](README.md) / [関連仕様](../../features/combat-enemy-behavior.md)

共通技。大きな体で床を踏み鳴らし、味方全体を揺さぶる攻撃である。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-04、ユーザーの依頼（敵の技を増やし、共通化できるものも入れる）で、[森の守り手](../enemies/enemy-forest-guardian.md)の技を共通技として作った |
| 役割・説明 | 味方全体への物理攻撃 |
| 関連する仕様・個別ID | 使う敵：[森の守り手](../enemies/enemy-forest-guardian.md)、[切り株猪](../enemies/enemy-stump-boar.md)、[真鍮の番人](../enemies/enemy-brass-warden.md)、[岩肌トロル](../enemies/enemy-rock-troll.md)、[雪原のマンモス](../enemies/enemy-snow-mammoth.md)、[黒曜石の巨像](../enemies/enemy-obsidian-colossus.md)、[沖の大ダコ](../enemies/enemy-giant-octopus.md)。[敵の行動の仮設定](../../features/combat-enemy-behavior.md) |
| 実装・ゲームマスタの場所 | 未実装 |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 共通技 | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 攻撃・味方全体 | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | 物理・打 | 味方は物理の技を物防で、魔法の技を属防で受ける（[使い道の候補](../../features/party.md#ステータスの項目)） | 候補 |
| 効果 | 物攻の50%のダメージ | 使う敵のステータスの割合（[威力の決め方](../../features/combat.md#威力の決め方)）。状態は[カードの状態の仮設定](../../features/combat.md#カードの状態の仮設定)の名前 | 仮 |
| 予告 | 全体攻撃 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [森の守り手](../enemies/enemy-forest-guardian.md)、[切り株猪](../enemies/enemy-stump-boar.md)、[真鍮の番人](../enemies/enemy-brass-warden.md)、[岩肌トロル](../enemies/enemy-rock-troll.md)、[雪原のマンモス](../enemies/enemy-snow-mammoth.md)、[黒曜石の巨像](../enemies/enemy-obsidian-colossus.md)、[沖の大ダコ](../enemies/enemy-giant-octopus.md) | 各敵の定義の「技」の表 | 候補 |
| 似合う敵 | 大型の重い敵（ゴーレム、巨人、大型の獣） | 新しい敵へ付けるときの目安 | 候補 |

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | 揺れる、広がる |
| 本体の動き | 絵を斜めに持ち上げて一瞬止め、どしんと沈める |
| 溜め | 体を持ち上げて止める |
| 発生 | 足元から味方の列へ地割れの光の線が走り、味方の足元で土の塊が跳ね上がる。画面を横に揺らす |
| 余韻 | 小石の粒が降り、地割れの光が細って消える |
| 色 | 土の茶、橙がかった光 |
| 格 | 中 |

使う敵ごとの見た目の違いは、次のとおりである。

| 敵 | 色・材質・形の違い |
|---|---|
| [森の守り手](../enemies/enemy-forest-guardian.md) | 地割れから、土の塊と一緒に苔の欠片も跳ねる |
| [切り株猪](../enemies/enemy-stump-boar.md) | 重い前足で踏み鳴らし、床から土くれと木くずが跳ねる |
| [真鍮の番人](../enemies/enemy-brass-warden.md) | 床を走る揺れの線を真鍮の金色にし、関節の継ぎ目から細い蒸気が噴く |
| [岩肌トロル](../enemies/enemy-rock-troll.md) | 揺れの線を灰青にし、天井から細かい砂と小石を落とす |
| [雪原のマンモス](../enemies/enemy-snow-mammoth.md) | 前脚を踏み鳴らし、雪けむりが床を走る |
| [黒曜石の巨像](../enemies/enemy-obsidian-colossus.md) | 重い足で踏み鳴らし、床のひびから溶岩の光が走る |
| [沖の大ダコ](../enemies/enemy-giant-octopus.md) | 何本もの触手で岩場を叩き、味方全体の足元から水しぶきと岩の欠片が跳ねる |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat.md#未決事項)の数値が決まったら見直す |
| 実装 | 未実装。実装するときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-04 | 共通技として定義を作った | ユーザーの依頼 | 使う敵：[森の守り手](../enemies/enemy-forest-guardian.md) |
| 2026-10-19 | 使う敵に、新しく作った敵を加えた | ユーザーの依頼（敵を50体に増やし、文書カタログ・クライアント・サーバーにそろえる） | 使う敵：[切り株猪](../enemies/enemy-stump-boar.md)、[真鍮の番人](../enemies/enemy-brass-warden.md)、[岩肌トロル](../enemies/enemy-rock-troll.md)、[雪原のマンモス](../enemies/enemy-snow-mammoth.md)、[黒曜石の巨像](../enemies/enemy-obsidian-colossus.md)、[沖の大ダコ](../enemies/enemy-giant-octopus.md) |
