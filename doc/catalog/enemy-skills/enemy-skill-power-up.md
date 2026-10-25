---
id: enemy-skill-power-up
type: definition
status: 候補
updated: 2026-10-19
name: 力を溜める
category: enemy-skills
---

# 力を溜める

[敵の技の定義](README.md) / [関連仕様](../../features/combat.md#敵の行動の仮設定)

共通技。力を溜め、次の攻撃を強める。

## 識別情報と根拠

| 項目 | 内容 |
|---|---|
| 定義の根拠・決定日・参照元 | 2026-10-04、ユーザーの依頼（共通化できる技も入れる）で、今の3体では使わない共通技として作った |
| 役割・説明 | 次の攻撃の予告を重くし、プレイヤーに防御を促す |
| 関連する仕様・個別ID | 使う敵：[水晶角うさぎ](../enemies/enemy-crystal-hare.md)、[切り株猪](../enemies/enemy-stump-boar.md)、[古王国の空鎧](../enemies/enemy-hollow-armor.md)、[人食い宝箱](../enemies/enemy-chest-mimic.md)、[真鍮の番人](../enemies/enemy-brass-warden.md)、[鉱石喰いモグラ](../enemies/enemy-ore-mole.md)、[砂サソリ](../enemies/enemy-sand-scorpion.md)、[砂鮫](../enemies/enemy-sand-shark.md)、[霜の大猿人](../enemies/enemy-frost-ape.md)、[フジツボ大ガニ](../enemies/enemy-barnacle-crab.md)、[骸骨兵](../enemies/enemy-skeleton-soldier.md)、[かぼちゃ頭の案山子](../enemies/enemy-pumpkin-scarecrow.md)。[敵の行動の仮設定](../../features/combat.md#敵の行動の仮設定) |
| 実装・ゲームマスタの場所 | 未実装 |

## 確定した設定

なし（すべて候補）。

## 分類固有のデータ

| 項目 | 値 | 単位・条件・参照元 | 確度 |
|---|---|---|---|
| 区分 | 共通技 | [共通技と固有技](README.md#共通技と固有技) | 候補 |
| 種類・対象 | 強化・自分 | [記入する項目](README.md#記入する項目) | 候補 |
| 物理・魔法と属性 | ダメージなし | 味方は物理の技を物防で、魔法の技を属防で受ける（[使い道の候補](../../features/party.md#ステータスの項目)） | 候補 |
| 効果 | 次に使う攻撃の技の威力を1.5倍にする | 使う敵のステータスの割合（[威力の決め方](../../features/combat.md#威力の決め方)）。状態は[カードの状態の仮設定](../../features/combat.md#カードの状態の仮設定)の名前 | 仮 |
| 予告 | 強化 | [記入する項目](README.md#記入する項目) | 候補 |
| 使う敵 | [水晶角うさぎ](../enemies/enemy-crystal-hare.md)、[切り株猪](../enemies/enemy-stump-boar.md)、[古王国の空鎧](../enemies/enemy-hollow-armor.md)、[人食い宝箱](../enemies/enemy-chest-mimic.md)、[真鍮の番人](../enemies/enemy-brass-warden.md)、[鉱石喰いモグラ](../enemies/enemy-ore-mole.md)、[砂サソリ](../enemies/enemy-sand-scorpion.md)、[砂鮫](../enemies/enemy-sand-shark.md)、[霜の大猿人](../enemies/enemy-frost-ape.md)、[フジツボ大ガニ](../enemies/enemy-barnacle-crab.md)、[骸骨兵](../enemies/enemy-skeleton-soldier.md)、[かぼちゃ頭の案山子](../enemies/enemy-pumpkin-scarecrow.md) | 各敵の定義の「技」の表 | 候補 |
| 似合う敵 | 力自慢の獣、戦士の姿の敵 | 新しい敵へ付けるときの目安 | 候補 |

## 演出

| 項目 | 内容 |
|---|---|
| 感覚の言葉 | 張りつめる |
| 本体の動き | 体を沈め、小刻みに震わせる |
| 溜め | 体の周りに赤橙の光の粒が吸い寄せられる |
| 発生 | 体の縁が一瞬赤く光り、足元に輪が広がる |
| 余韻 | 次の攻撃まで、体の縁に薄い赤の揺らぎが残る。揺らぎは予告とHPバーを隠さない |
| 色 | 赤橙、白 |
| 格 | 小 |

使う敵ごとの見た目の違いは、次のとおりである。

| 敵 | 色・材質・形の違い |
|---|---|
| [水晶角うさぎ](../enemies/enemy-crystal-hare.md) | 後ろ足を縮めて身を沈め、額の角が薄紫に明るむ |
| [切り株猪](../enemies/enemy-stump-boar.md) | 前足で地面をかき、鼻先から白い息を荒く吹く |
| [古王国の空鎧](../enemies/enemy-hollow-armor.md) | 剣を掲げ、継ぎ目からもれる紫の霧が刃にまとわりつく |
| [人食い宝箱](../enemies/enemy-chest-mimic.md) | 溜めの光を口の中の暗い赤にし、ふたが大きく開いて牙がきらりと光る形にする |
| [真鍮の番人](../enemies/enemy-brass-warden.md) | 溜めの光を胸の炉の青にし、関節の歯車に回る光の輪を描く |
| [鉱石喰いモグラ](../enemies/enemy-ore-mole.md) | 溜めの光を鉄色と白にし、爪の先に研いだ光の筋が走る形にする |
| [砂サソリ](../enemies/enemy-sand-scorpion.md) | はさみを高く振り上げ、毒針と目の青い光が強まる。 |
| [砂鮫](../enemies/enemy-sand-shark.md) | 砂の中へ潜って背びれだけを出し、床に砂の波紋が広がる。 |
| [霜の大猿人](../enemies/enemy-frost-ape.md) | 両腕を振り上げ、体の縁が水色に光る |
| [フジツボ大ガニ](../enemies/enemy-barnacle-crab.md) | 両方のはさみを高く振り上げて打ち鳴らし、小さな泡が昇る |
| [骸骨兵](../enemies/enemy-skeleton-soldier.md) | 顎をカタカタ鳴らして剣を振りかぶり、目のくぼみの橙の光が強まる |
| [かぼちゃ頭の案山子](../enemies/enemy-pumpkin-scarecrow.md) | 藁の腕をぐるぐる回し、麦わら色の藁の粒が渦を巻く |

## 未決事項

| 項目 | 決める内容・先に必要な判断 |
|---|---|
| 数値 | 割合と持続ターンは仮。[戦闘の未決事項](../../features/combat.md#未決事項)の数値が決まったら見直す |
| 実装 | 未実装。実装するときは[敵の挙動デバッグルーム](../../features/screens/debug-rooms.md#敵の挙動デバッグルーム)の行動にも加える |

## 変更と判断の記録

| 日付 | 変更・判断 | 理由・根拠 | 影響するID・仕様 |
|---|---|---|---|
| 2026-10-04 | 共通技として定義を作った | ユーザーの依頼 | 使う敵：なし（今の敵は使わない） |
| 2026-10-19 | 使う敵に、新しく作った敵を加えた | ユーザーの依頼（敵を50体に増やし、文書カタログ・クライアント・サーバーにそろえる） | 使う敵：[水晶角うさぎ](../enemies/enemy-crystal-hare.md)、[切り株猪](../enemies/enemy-stump-boar.md)、[古王国の空鎧](../enemies/enemy-hollow-armor.md)、[人食い宝箱](../enemies/enemy-chest-mimic.md)、[真鍮の番人](../enemies/enemy-brass-warden.md)、[鉱石喰いモグラ](../enemies/enemy-ore-mole.md)、[砂サソリ](../enemies/enemy-sand-scorpion.md)、[砂鮫](../enemies/enemy-sand-shark.md)、[霜の大猿人](../enemies/enemy-frost-ape.md)、[フジツボ大ガニ](../enemies/enemy-barnacle-crab.md)、[骸骨兵](../enemies/enemy-skeleton-soldier.md)、[かぼちゃ頭の案山子](../enemies/enemy-pumpkin-scarecrow.md) |
