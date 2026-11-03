---
id: feature-combat-effects
type: specification
status: 一部確定
updated: 2026-11-03
---

# 戦闘システム：効果の部品

[戦闘システム](../combat.md) / [計画](../../plans/2026-11-03-battle-effect-system.md) / [パッシブスキル](../passives.md) / [装備](../equipment.md) / [敵の行動](../combat-enemy-behavior.md)

カード・パッシブ・装備・状態・冒険の強化・敵の技と特性の「戦闘中に◯◯したら△△する」「◯◯なら△△する」を、同じ形の部品で書く決まりを置く。
コードは `client/Assets/Baryonyx/Features/Combat/Runtime/Effects/`（部品）と `Runtime/Battle/`（画面から切り離した戦闘のルール）にある。
コードを書くときの注意は、同じフォルダーの [AGENTS.md](../../../client/Assets/Baryonyx/Features/Combat/Runtime/Effects/AGENTS.md) にある。

## 設計方針

- 1つの効果は、定義の1か所に書く。戦闘画面（`BattleInspectView`）に、パッシブや装備ごとの分岐を書かない。
- 効果は、戦闘を `IBattleWorld` だけを通して読み書きする。戦闘画面・HPバーの予測・画面なしの戦闘（`BattleState`）がそれぞれ `IBattleWorld` を持つため、1か所に書いた効果が3つで同じに働く。
- 部品の組み合わせで書けない効果は、その効果だけの処理（`BattleCondition.Of`・`BattleAmount.Of`・`BattleAct.Custom`）を規則の中に書く。2つ目の効果でも要るなら、部品として `Effects/` に足す。
- 定義はC#のコードに書く。ScriptableObjectには移さない（固有の処理を書けないため）。

## 効果の部品

効果（`BattleEffect`）は、規則（`BattleEffectRule`）の並びである。
規則は、きっかけ（`BattleTrigger`）か値（`BattleValue`）のどちらかと、条件・量・働き・回数・見せ方を組み合わせる。

| 部品 | 型 | 役目 |
|---|---|---|
| きっかけ | `BattleTrigger` | 何が起きたときに働くか |
| 値 | `BattleValue` | どの数を変えるか |
| 条件 | `BattleCondition` | 働くかどうか。`&`・`\|`・`!` で組み合わせる |
| 量 | `BattleAmount` | 数の出どころ（固定値、ステータスの%、起きたことの量、重ねた数、今日のACT、状態の量） |
| 働き | `BattleAct` | 何をするか（攻撃、ブロック、回復、状態、エネルギー、持ち越し、取り消す、代わりに受ける、重ねる） |
| 相手 | `BattleWho` | 働きが届く人（持ち主、行った人、受けた人、ランダムな敵・味方、全体） |
| 回数 | `OncePerTurn`・`OncePerBattle`・`Limit` | 条件が成り立った回数で数える |
| 重ね | `Stacks` | 効果ごとの数と上限（見よう見まねの観た回数など） |
| しばらく持つ | `BattleAct.Hold`（`BattleHoldSpan`） | 働きの中で、効果を「次の自分の行動まで」「Nターン」「戦闘の終わりまで」持たせる（すり抜け・霧隠れ） |
| 見せ方 | `Shows`・`ShowsOver` | 名前を出すか、1枚のカードで1回だけにするか、誰の上に出すか |
| 予測 | `AtRandom` | 確率やランダムの規則を、HPバーの予測から外す |

値の変え方は3通りある。
`Add` は固定の量を足し、`AddPercent` は%を合計して1回だけ掛け、`Multiply` は1つずつ順に掛けて丸める（`Order` の小さい順）。

## 効果の出どころ

| 出どころ | 書く所 | 持ち主と働く間 |
|---|---|---|
| パッシブ | `CombatPassives` の各定義の `Effect` | Lvで解放した人。立っている間 |
| 装備 | `EquipmentDefinition.WithBattle`（説明文と、倍率を受け取って効果を書く規則） | 着けている人。冒険の戦闘は `EquipmentStats.BattleEffects` で渡す |
| 冒険の強化（背水・先手・弔い） | `BattleStatBoosts.EffectOf` | 強化を持つ人 |
| カード | `CardSkill.Effects` | 使い手。そのカードの1枚の中で起きたことにだけ働く |
| カードの働きの条件 | `CardAction.Only(BattleCondition)`・`Bonus(BattleCondition, %)`・`Adding(BattleAmount)` | 使い手。旧来の `CardCondition` も同じ部品に置き換えて判定する（`CardRules.ConditionOf`） |
| 状態 | `BattleStatuses` | 状態が付いている人。付いている間 |
| 敵の技 | `EnemyMove.Effect`（きっかけ `Used`） | 使った敵。使ったときに1回 |
| 敵の特性 | `EnemyBehavior.Effects` | その種類の敵。立っている間 |

## きっかけと値

### きっかけ

| きっかけ | いつ | 止められる |
|---|---|---|
| `BattleStart` | 戦闘の手札を配り直すとき | |
| `PartyTurnStart`・`PartyTurnEnd` | 味方のターンの始め（エネルギーを満たしたあと）と終わり（量は残りのエネルギー） | |
| `EnemyTurnStart`・`EnemyTurnEnd` | 敵の行動の始めと終わり | |
| `CardPlaying`・`CardPlayed` | カードの働きの前と後（後の量は、そのカードで与えたダメージの合計） | |
| `DealtDamage`・`EnemyDefeated` | 味方の攻撃・効果・状態のダメージが敵に当たったあと、敵が倒れたとき | |
| `BlowLanding` | 攻撃が当たる前。敵の攻撃が味方に（量は味方の防御の前の威力）、味方のカードの攻撃が敵に（量は敵の防御のあとの威力）。止めると、そのカードのその当たりのほかの働きも届かない | `Cancel` |
| `TookBlow`・`AllyFell` | 敵の攻撃が味方に当たったあと、味方が倒れたとき | |
| `StatusApplying`・`StatusApplied` | 状態が付く前と後 | `Cancel` |
| `GainedBlock` | ブロックが実際に増えたとき | |
| `Healed` | 回復したとき（カードの回復とリジェネ） | |
| `EnemyTargeting` | 敵の単体攻撃の向かう先を決めるとき | `TakeInstead` |
| `Used` | 持ち主が効果を使うとき（敵の技） | |

ダメージには種類（`BattleDamageKind`）があり、条件で見分ける。
`Card` はカードの攻撃、`Effect` は効果が起こす攻撃（切り返し・帯電の盾）、`Status` は状態のダメージ（やけど・毒・出血・刻印・雷雲・反射・傷口を開く・毒まわし）、`Enemy` は敵の攻撃である。

### 値

| 値 | 何の数か | 下限・上限 |
|---|---|---|
| `Damage` | 味方の攻撃のダメージ（敵の防御の前） | 0以上 |
| `Incoming` | 敵の攻撃が味方に与える威力（味方の防御の前） | 0以上 |
| `DamageTaken` | 受ける側の最後の増減。味方は防御と会心のあと、敵は弱点のあと（ブロックの前） | 0以上 |
| `Heal` | 回復量（カードの回復、リジェネ） | 0以上 |
| `Block` | カードで得るブロック | 0以上 |
| `Cost` | 手札のカードのコスト | 0以上 |
| `Hits` | ×回数の攻撃の回数 | 1以上 |
| `OpeningDraw`・`TurnDraw` | 戦闘の始め・ターンの始めに配る手札 | 0以上 |
| `TurnEnergy` | ターンの始めのエネルギー | 0以上 |
| `TwiceChance` | 攻撃のカードがもう一度働く確率（%） | 0〜100 |
| `StatPercent` | ステータスの上がる%（物攻・属攻・素早さ） | なし |

## 数を変える順番

カードの攻撃のダメージは、次の順に決まる（`CardHits.BlowPower`）。

| 順 | 段 | 掛け方 |
|---|---|---|
| 1 | 威力（ステータス×割合、今日のACT、次のカードの上乗せ・眠り札の段） | |
| 2 | シールドバッシュのブロック、カードの働きの `Adding` | 足す |
| 3 | カードの条件の上乗せ | 掛ける |
| 4 | 値 `Damage`：固定、%の合計（パッシブ・装備・居合の型）、攻撃力アップ（掛ける順1）、防御ダウン（2）、味方の麻痺（3） | 部品の掛け方による |
| 5 | 敵の防御 | |
| 6 | 会心（本番だけ） | 掛ける |
| 7 | 弱点 | 掛ける |
| 8 | 値 `DamageTaken`（敵のプロテクト） | 掛ける |
| 9 | ブロック | 引く |

効果が起こす攻撃（帯電の盾・切り返し）は、持ち主のステータス×割合、4、5、7、8、9の順に決まる。
敵の攻撃は、`BlowLanding`（無敵・反射）、値 `Incoming`（味方のプロテクト）、味方の防御、ブロック砕き、会心、値 `DamageTaken`（味方の防御ダウン）、ブロックの順に決まる（`EnemyBlows.Strike`）。
敵の攻撃の威力そのもの（敵の攻撃力アップ・溜め・麻痺）は、まだ値を通さず、敵の攻撃の計算（`BlowPower`）で決める。

## 状態の効果

`BattleStatuses` が、次の状態の働きを効果として持つ。
ここにない状態（凍結・凍え・速度アップ・挑発・炎の加護・やけど・毒・出血・リジェネ・刻印・雷雲）は、ターン順・狙い・ターンごとのダメージの処理が働きを持つ。

| 状態 | 働き |
|---|---|
| 攻撃力アップ | 持ち主のカードのダメージを、付いた%だけ掛けて上げる |
| 防御ダウン | 持ち主が受ける味方のカードのダメージ+25%。味方に付いたら、敵の攻撃で受けるダメージ+25% |
| 麻痺 | 味方に付いたら、そのカードのダメージ-25%（敵の麻痺は敵の攻撃の計算で弱める） |
| プロテクト | 持ち主が受けるダメージ-25%（敵は弱点のあと、味方は防御の前） |
| 無敵 | 持ち主への攻撃を止める。反射より先に働く |
| 反射 | 持ち主への攻撃を1回はね返す。味方なら敵の攻撃を攻撃した敵へ、敵なら単体攻撃のカードを使った味方へ |
| 呪い | 持ち主がカードで得るブロック-50%（敵の技で味方に付く） |

### 仮設定：敵が味方に付ける状態

2026-11-03に、敵の技で味方に状態を付けられるようにした。
次の扱いは仮に決めた。

| 点 | 仮の扱い |
|---|---|
| 付く確率 | 技の確率が0なら必ず付く。確率があれば、敵と味方の幸運で動く |
| 数え方 | 敵が味方に付けた状態は、味方のターンの終わりに1ターン数える。「麻痺2ターン」は、味方の2回のターンのあいだ続く |
| やけど・毒のダメージ | 敵のターンの始めに、付けた敵のステータスから決めた量を、ブロックを通さずに受ける。毒は1回ごとに1重ね減る |
| 防ぐ | 浄めの誓いが、1ターンに1回止める |

### 効果の部品で書いた敵の技

[敵の技の定義](../../catalog/enemy-skills/README.md)のうち、次の17種類を効果の部品で書いた（`CombatEnemyBehaviors` の `Afflict`・`Hold`）。
文書の値をそのまま使い、決まっていない点は次のとおり仮に決めた。

| 技 | 働き |
|---|---|
| にらみつけ | 味方単体に麻痺2ターン |
| 金切り声・墨吐き | 味方全体に麻痺1ターン。墨吐きの「予告を伏せる」分は、予告を出さない戦闘なので入れない |
| 胞子ばらまき | 味方全体に毒2重ね |
| 甘い蜜の香り | 味方全体に防御ダウン1ターン |
| 胞子の芽吹き | 毒のある味方の毒を、今の重なりと同じだけ増やす |
| 毒まわし | 毒の最も多い味方に、毒が尽きるまでのダメージを一度に与えて毒を消す |
| 朽ちの呪い | 味方単体に呪い2ターン |
| 延焼 | やけどのある味方はやけどの残りを2延ばし、ない味方にはやけど1ターン |
| 水晶角の照り返し | 自分に反射（次の単体攻撃のカードを1回、使った味方へはね返す） |
| すり抜け | 次の自分の行動まで、物理（斬・打・貫）の攻撃を止める |
| 耳ざとい身かわし | 次の自分の行動まで、コスト3以上の単体攻撃のカードをかわす（状態も届かない） |
| 霧隠れ | 2ターン、単体攻撃のカードから受けるダメージ-50% |
| 燃えるうろこ | 2ターン、自分に単体の物理の攻撃をしてきた味方にやけど1ターン |
| 畑の見張り番 | 2ターン、自分以外の敵が単体攻撃のカードを受けるたびに、使った味方へ物攻の40%（1ターンに2回まで） |
| 熱風の羽ばたき | 自分と仲間に速度アップ2ターン |
| 丸のみ | 自分以外の敵のうちHPの最も低い1体を倒し、その残りHPだけ回復（属攻の300%まで） |

「2ターン」の数え方は、敵が自分に付ける状態と同じく、味方のターンの始めに1つ数える。

## 連鎖と順番

働きの中で起きたことは、その場できっかけとして呼ぶ（入れ子に呼ぶ）。
入れ子が8段を超えたら、それより深いきっかけは呼ばずに警告を出す。
同じきっかけで働く効果は、味方の場所の順、敵の順（同じ人の中では足した順）、そのあと状態の効果（人ごとに、優先の小さい順）、規則を書いた順に呼ぶ。
一つが取り消したり向きを変えたりすると、そのあとの効果は呼ばない。

## 予測と画面なしの戦闘

HPバーの予測（`ForecastWorld`）は、HP・ブロック・敵に付く状態の写しの上で、本番と同じ部品とカードの処理（`CardHits`・`CardPlays`）を動かす。
名前を出さず、回数を数えず、確率とランダムの規則（`AtRandom`）を飛ばし、会心を入れず、まだ見つけていない弱点を数えない。

画面なしの戦闘（`BattleState`）は、味方・敵・エネルギー・効果を持ち、カードを使う・敵が攻撃する・敵が技を使う・ターンを進めることを、画面の戦闘と同じルール（`CardPlays`・`CardHits`・`EnemyBlows`・`EnemyActs`・`BattleStatusRules`・効果）で一度に行う。
EditModeのテストで、効果と状態を戦闘の形で確かめるのに使う（[画面なしの戦闘で確かめる](../../rules/client-testing.md#画面なしの戦闘で確かめる)）。

`BattleSimulation` は、`BattleState` に山札・手札・速さによるターン順・敵の技の選び方（`EnemyBrain`）を足し、1戦を最初から最後まで一度に戦う。
自動対戦は、依頼の `engine` を `headless` にすると、シーンを読まずにこれで戦う（`BattleBalanceHeadless`）。
敵の数値（`BattleFighter.OfEnemy`）と、敵の攻撃・ブロック・回復・狙いの式（`EnemyActs`）は、戦闘画面と同じものを使う。
次のものは、まだ画面なしの戦闘に入れていない（戦闘画面だけにある）。
連携・合体・眠り札の段、捨てる・拾うカードの選択、次のカードのコスト0と上乗せ、固有スキルの戦闘中の覚醒、弱点の開示（`knowWeaknesses` で狙いだけを変える）、凍結のあとの守り、敵の詠唱の割り込み。

## 未決事項

| 項目 | 決めること |
|---|---|
| 状態の重ね方 | 同じ状態が付き直したときに、ターンを延ばすか、強さを足すか、別々に数えるか |
| 敵の攻撃の威力の値 | 敵の攻撃力アップ・溜め・麻痺を値 `Damage` の段へ寄せるか（丸めが変わるため、強さの測り直しが要る） |
| 回数の決まりのある値 | 手札のコストは表示のために何度も読むため、値の回数を数えない。「1ターンに1回、最初のカードのコスト-1」は、払ったときに数える口を足してから書く |
| 戦闘の始めに引く | `BattleStart` で `Draw` しても、始めの手札を配っている間は引かない。始めの手札を増やすのは値 `OpeningDraw` で書く |
| 敵の技の残り | 仲間を呼ぶ（14個）・逃げる・倒れたあとの復活・倒れた敵を起き上がらせる・無敵と休み・2回行動・惑い・絡みつきの罠・道案内・溶岩のあふれの23個は、まだ `Other(...)` のまま。仲間を呼ぶ・逃げる・復活は敵の立ち位置と落とし物の画面の扱いが、惑い・絡みつきは状態の働きが決まっていない |
| 画面なしの自動対戦のずれ | 画面なしの戦闘に入れていないもの（上の一覧）の分だけ、画面での自動対戦と結果がずれる。強さを決める測定は画面（`engine` を `screen`）で行う |
| 連鎖の上限 | 8段で足りるか |

## 実装との対応

| 対象 | 入口 |
|---|---|
| 部品 | `Runtime/Effects/`（[BattleEffects](../../../client/Assets/Baryonyx/Features/Combat/Runtime/Effects/BattleEffects.cs)・[BattleEffect](../../../client/Assets/Baryonyx/Features/Combat/Runtime/Effects/BattleEffect.cs)・[BattleCondition](../../../client/Assets/Baryonyx/Features/Combat/Runtime/Effects/BattleCondition.cs)・[BattleAct](../../../client/Assets/Baryonyx/Features/Combat/Runtime/Effects/BattleAct.cs)・[IBattleWorld](../../../client/Assets/Baryonyx/Features/Combat/Runtime/Effects/IBattleWorld.cs)・[BattleStatuses](../../../client/Assets/Baryonyx/Features/Combat/Runtime/Effects/BattleStatuses.cs)） |
| 画面から切り離したルール | `Runtime/Battle/`（[CardHits](../../../client/Assets/Baryonyx/Features/Combat/Runtime/Battle/CardHits.cs)・[CardPlays](../../../client/Assets/Baryonyx/Features/Combat/Runtime/Battle/CardPlays.cs)・[EnemyBlows](../../../client/Assets/Baryonyx/Features/Combat/Runtime/Battle/EnemyBlows.cs)・[EnemyActs](../../../client/Assets/Baryonyx/Features/Combat/Runtime/Battle/EnemyActs.cs)・[BattleStatusEntry](../../../client/Assets/Baryonyx/Features/Combat/Runtime/Battle/BattleStatusEntry.cs)・[BattleState](../../../client/Assets/Baryonyx/Features/Combat/Runtime/Battle/BattleState.cs)・[BattleSimulation](../../../client/Assets/Baryonyx/Features/Combat/Runtime/Battle/BattleSimulation.cs)・[BattleCardPlan](../../../client/Assets/Baryonyx/Features/Combat/Runtime/Battle/BattleCardPlan.cs)） |
| 画面なしの自動対戦 | [BattleBalanceHeadless](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/Balance/BattleBalanceHeadless.cs)（依頼の `engine` が `headless` のとき） |
| 戦闘画面の窓口 | [BattleInspectView.Effects](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.Effects.cs)・[BattleInspectView.CardWorld](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.CardWorld.cs) |
| 予測の窓口 | [BattleInspectView.Forecast](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.Forecast.cs) の `ForecastWorld` |
| テスト | EditModeの `BattleEffectsTests`（偽の戦闘で部品を確かめる）・`BattleStateTests`（画面なしの戦闘）・`CombatPassivesTests`、PlayModeの `BattlePassivesSceneTests`・`BattleInspectSceneTests` ほか |

## 変更と判断の記録

- 2026-11-03：ユーザーの依頼「固有の効果が大量に出てくるので、汎用的に書ける形にしてほしい」「ありとあらゆる効果を足したい」により、[計画](../../plans/2026-11-03-battle-effect-system.md)の第1段から第8段を実装し、このページを作った。第1段から第7段は強さを変えず、自動対戦の結果が変更の前と全項目で同じだった。第8段で、状態を付ける敵の技20個（7種類）が使われるようになった。
- 2026-11-03：ユーザーの「7段階の自動戦闘も仕組みとして実装して」「ほかに残っているものも一緒に」により、画面なしで1戦を戦う `BattleSimulation` と自動対戦の `engine: headless` を足し、敵の技10種類（呪い・反射・すり抜けなど）を効果の部品で書いた。敵の攻撃・ブロック・回復・狙いの式と敵の数値を、画面と画面なしで同じ関数（`EnemyActs`・`BattleFighter.OfEnemy`）にした。
