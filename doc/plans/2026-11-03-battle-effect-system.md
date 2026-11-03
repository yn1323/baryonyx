---
id: plan-battle-effect-system-20261103
type: reference
status: 一部確定
updated: 2026-11-03
---

# 戦闘の効果を「きっかけ・条件・働き」の部品で書けるようにする

状態：完了（2026-11-03に第1段から第8段を実装。第8段の敵の技のうち、仲間を呼ぶ・逃げる・復活など23個は、画面の扱いと状態の働きが決まっていないため、[効果の部品](../features/combat/effects.md#未決事項)の未決事項に移した）

[計画索引](README.md) / [戦闘システム：効果の部品](../features/combat/effects.md)（この計画の反映先） / [戦闘システム](../features/combat.md) / [パッシブスキル](../features/passives.md) / [装備](../features/equipment.md) / [passive-skill-authoring](../../.agents/skills/passive-skill-authoring/SKILL.md)

この計画は、会話の記録がなくても続けられるように書いている。
続きから始めるときは、まず[再開するときの手引き](#再開するときの手引き)を読む。

## 目的

カード・キャラのパッシブ・装備・冒険の強化・状態・敵の特性に、固有の効果がこれから大量に増える。
「戦闘中に◯◯したら△△する」「ステータスや状態が◯◯なら△△する」という効果を、どれも同じ形の部品で書けるようにする。
1つの効果を足すときに、定義を1か所に書き、EditModeのテストを足すだけで済む状態を目指す。

ユーザーの依頼（2026-11-03）は次のとおり。

- 固有の効果が大量に出てくるので、耐えられるコードにしてほしい。
- 今例に挙げた効果に限らず、ありとあらゆる効果を足したいので、汎用的に対応できる形にしてほしい。
- 計画書を作り、実装まで進めてほしい。並行する作業と重なる可能性があるので注意してほしい。

## 調べたこと

### 今のコードの問題

2026-11-03の時点で、戦闘のコードを読んで見つけた問題を挙げる。

1. **戦闘のルールが画面のクラスの中にある。**
   戦闘画面の `BattleInspectView`（MonoBehaviour。partialで約1万行）が、計算・戦闘の状態（約160個の変数）・演出をまとめて持つ。
   `Hit()` は敵のHPを減らす行と、数字を出す・揺らす・弱点を光らせる行が同じ関数にある。
   効果のテストはシーンを読むPlayModeでしか書けず、自動対戦も戦闘画面を速回しして1戦に6〜9秒かかる。
2. **パッシブは種類ごとのif文を、戦闘の流れのあちこちに差し込んでいた。**
   18種のために、呼ぶ所が14個あった（`BeginCardPassives`・`AfterCardBlow`・`CoverTarget`・`Purified` など）。
   「1ターンに1回」の旗（`coverUsed`・`iaiUsed` など）も画面のクラスの変数だった。
   1つ足すと、列挙・一覧・数値・差し込み・予測の写しの4〜5か所を直す必要があった。
3. **HPバーの予測が、本番の計算の写しだった。**
   `ForecastDamage` は `Damage` の、予測の中の条件の判定は本番の `Holds` の写しだった。
   傷口を開く・魔力の盾・あふれる祈りは本番と予測の両方に書かれていた。
   写しのずれも1つあった（シールドバッシュのブロックの加算が予測になかった）。
4. **効果の書き方が、決まった選択肢と1枚のための欄でできている。**
   カードの条件 `CardCondition` は「対象がやけど」のような1つずつの値で、組み合わせや数値付きの条件を書けない。
   `CardAction` には、シールドバッシュの `BlockPercent` や千刃乱舞の `PerDiscarded` のような、特定のカードのための欄が増えている。
   装備の効果は「ステータス+%」と「2回発動」だけで、2回発動は `Stat = -1` の目印で表している。
   冒険の強化（背水・先手・弔い）は `BattleStatBoost` という別の条件の仕組みで動く。
   敵の技は約50個が `Other(...)`（戦闘でまだ使えない技）のままである。
5. **ダメージの増減の順番が、関数の行の順でしか決まっていない。**
   %を合計して1回掛けるもの（パッシブ）と、順に掛けるもの（攻撃力アップ・脆弱）の区別が規則になっていない。
6. **効果から別の効果が起きる連鎖が、関数の直接の呼び出しだった。**
   ブロックを足す関数の中から帯電の盾を呼んでいたため、魔力の盾のブロックから帯電の盾の雷が起きていた。
   連鎖の順番と上限の決まりがなかった。

### Webで調べたこと

- [Gameplay Effects（Epic公式）](https://dev.epicgames.com/documentation/en-us/unreal-engine/gameplay-effects-for-the-gameplay-ability-system-in-unreal-engine)：効果を、持続（その場だけ・期間・ずっと）、数値の変え方（Modifier）、付く条件（タグの要件、確率）、重ね方（Stacking）の組み合わせで表す。組み合わせで書けない計算だけを専用のクラス（[Execution Calculation](https://dev.epicgames.com/documentation/unreal-engine/API/Plugins/GameplayAbilities/UGameplayEffectExecutionCalculat-?application_version=5.5)）にする。
- [Type Object（Game Programming Patterns）](https://gameprogrammingpatterns.com/type-object.html)：種類を、クラスの継承ではなく1つのクラスの値で増やす。固有の動きは値で書きにくいため、決まった動きから選ぶか、コードで書く。
- [Event Queue（Game Programming Patterns）](https://gameprogrammingpatterns.com/event-queue.html)：起きたことを順に処理して連鎖を制御する。ループすると止まらずに回り続けるため、出来事の処理の中から出来事を送る所には注意が要る。
- [Level up your code with design patterns and SOLID（Unity公式e-book）](https://unity.com/resources/design-patterns-solid-ebook)：Observer・MVPで計算と表示を分ける。

## 方針

### 効果の部品

効果は、名前の付いた**効果**（`BattleEffect`）が、1つ以上の**規則**（`BattleEffectRule`）を持つ形で書く。
規則は次の組み合わせでできている。

| 部品 | 型 | 役目 |
|---|---|---|
| きっかけ | `BattleTrigger` | 何が起きたときに働くか（カードを使う前、ダメージを与えた、ブロックを得た…） |
| 値 | `BattleValue` | どの数を変えるか（与えるダメージ、回復量、コスト、配る手札…）。きっかけの代わりに使う |
| 条件 | `BattleCondition` | 働くかどうか。`&`・`\|`・`!` で組み合わせる |
| 量 | `BattleAmount` | 数の出どころ（固定値、持ち主のステータスの%、起きたことの量の%、重ねた数、今日のACT…） |
| 働き | `BattleAct` | 何をするか（攻撃する、ブロックを得る、状態を付ける、エネルギー、取り消す、代わりに受ける…） |
| 回数 | 規則の設定 | 1ターンに1回、1戦闘に1回、1ターンにN回 |
| 重ね | 効果の設定 | 効果ごとの数（見よう見まねの観た回数など）と上限 |
| 見せ方 | 規則の設定 | 働いたときに名前を出すか、量を添えるか、1枚のカードで1回だけにするか |
| 予測 | 規則の設定 | 確率やランダムの規則は、HPバーの予測に入れない |

組み合わせで書けない効果は、`BattleAct.Custom` と `BattleCondition.Of`・`BattleAmount.Of` にその効果だけの処理を書く。
専用の処理も同じ規則の中に置くので、回数・見せ方・予測の扱いは部品と同じに決まる。

### 持ち主と効く範囲

効果は戦闘の1人（味方または敵）が持つ。
持ち主が倒れている間は働かない。
誰の行動に効くかは条件で決める（`ActorIsOwner`：持ち主の行動だけ、`ActorIsAlly`：味方の誰の行動でも、`ActorIsOtherAlly`：持ち主以外の味方）。

効果の出どころは次のとおり。

| 出どころ | 渡し方 |
|---|---|
| パッシブ | `CombatPassive.Effect`。Lvで解放したものを、戦闘の始めに持ち主へ付ける |
| 装備 | `EquipmentDefinition.WithBattle`（説明文と、倍率を受け取って効果を書く規則）。冒険の戦闘は `EquipmentStats.BattleEffects` で着けている人へ付ける |
| 編成の1人に渡すほかの効果 | `BattlePartyMember.Effects` |
| カード | `CardSkill.Effects`。そのカードを使っている間だけ、使い手が持つ |
| 状態・冒険の強化・敵の特性 | 第5段・第8段で移す |

### 本番と予測が同じ部品を通る

効果は、戦闘の状態を `IBattleWorld` だけを通して読み書きする。
本番の戦闘画面と、HPバーの予測が、それぞれ `IBattleWorld` を持つ。
予測の `IBattleWorld` は、HP・ブロック・付いた状態の写しを読み書きし、名前を出さず、回数を数えない。
そのため、効果は1か所に書けば本番と予測の両方で働く。
確率とランダムの規則は予測で飛ばす（今の予測と同じ扱い）。

### 数を変える順番

値（`BattleValue`）は、固定の量を足してから、%の量を合計して1回だけ掛け、そのあと値ごとの上限・下限で止める。
カードの攻撃のダメージは、次の順に決まる。
効果が入るのは4の段だけで、効果が増えても順番は変わらない。

| 順 | 段 | 掛け方 |
|---|---|---|
| 1 | 威力（ステータス×割合、今日のACT、次のカードの上乗せ・眠り札の段） | |
| 2 | シールドバッシュのブロック | 足す |
| 3 | カードの条件の上乗せ（`BonusWhen`） | 掛ける |
| 4 | 値 `Damage`（パッシブ・装備・カードの効果、居合の型の上乗せ） | 固定を足し、%を合計して1回掛ける |
| 5 | 攻撃力アップ | 掛ける |
| 6 | 脆弱 | 掛ける |
| 7 | 敵の防御 | |
| 8 | 会心（本番だけ） | 掛ける |
| 9 | 弱点 | 掛ける |
| 10 | プロテクト | 掛ける |
| 11 | ブロック | 引く |

効果が起こす攻撃（帯電の盾・切り返し）は、1（持ち主のステータス×割合）、4、7、9、10、11の順に決まる。
5・6・10は、第5段で状態を効果の部品へ移すときに、値 `Damage`・`DamageTaken` の段へ寄せる。

### 連鎖

働きの中で起きたことは、その場できっかけとして呼ぶ（入れ子に呼ぶ）。
入れ子が8段を超えたら、それより深いきっかけは呼ばずに警告を出す。
同じきっかけで働く効果は、味方の場所の順、敵の順、効果を付けた順、規則を書いた順に呼ぶ。

### 定義はC#のコードに書く

今のカード（`CardSkills.cs`）と同じく、効果の定義もC#のコードに書く。
ScriptableObjectに移すと固有の処理を書けず、組み合わせの部品を足すたびにInspectorの対応が要るためである。

## きっかけと値の一覧

### きっかけ（`BattleTrigger`）

| きっかけ | いつ | 起きたことに入るもの |
|---|---|---|
| `BattleStart` | 戦闘の手札を配り直すとき | |
| `PartyTurnStart` | 味方のターンの始め（エネルギーを満たしたあと） | |
| `PartyTurnEnd` | 味方のターンの終わり（残りのエネルギーを数える前） | 量：残りのエネルギー |
| `EnemyTurnStart`・`EnemyTurnEnd` | 敵の行動の始めと終わり | |
| `CardPlaying` | カードの働きの前 | 使い手、カード、そのカードの記録（`BattleCardPlay`） |
| `CardPlayed` | カードの働きのあと | 量：そのカードで与えたダメージの合計 |
| `DealtDamage` | 味方の攻撃・効果・継続ダメージが敵に当たったあと | 与えた人、敵、量、種類、属性、カード |
| `EnemyDefeated` | 敵が倒れたとき | 同上 |
| `TookBlow` | 敵の攻撃が味方に当たったあと | 敵、味方、量 |
| `AllyFell` | 味方が倒れたとき | 敵、味方 |
| `StatusApplying` | 状態が付く前。`Cancel` で取り消せる | 付ける人、付く人、状態 |
| `StatusApplied` | 状態が付いたあと | 同上 |
| `GainedBlock` | ブロックが実際に増えたとき | 得た人、量 |
| `Healed` | 回復したとき（カードの回復とリジェネ） | 回復した人、された人、量、最大HPを超えた分 |
| `EnemyTargeting` | 敵の単体攻撃の向かう先を決めるとき。`TakeInstead` で持ち主に変えられる | 敵、狙われた味方 |

### 値（`BattleValue`）

| 値 | 何の数か | 下限・上限 |
|---|---|---|
| `Damage` | 味方の攻撃のダメージ（敵の防御の前） | 0以上 |
| `DamageTaken` | 敵の攻撃で味方が受けるダメージ（味方の防御と会心のあと） | 0以上 |
| `Heal` | 回復量（カードの回復、リジェネ） | 0以上 |
| `Block` | カードで得るブロック | 0以上 |
| `Cost` | 手札のカードのコスト | 0以上 |
| `Hits` | ×回数の攻撃の回数 | 1以上 |
| `OpeningDraw` | 戦闘の始めに配る手札 | 0以上 |
| `TurnDraw` | ターンの始めに配る手札 | 0以上 |
| `TurnEnergy` | ターンの始めのエネルギー | 0以上 |
| `TwiceChance` | 攻撃のカードがもう一度働く確率（%） | 0〜100 |

## 18個のパッシブの書き方

| パッシブ | きっかけ・値 | 条件 | 働き |
|---|---|---|---|
| 星図の計算 | `PartyTurnEnd` | | エネルギーを1まで持ち越す |
| 寒暖の理 | 値 `Damage` +30% | 味方の行動、カードか効果の攻撃、敵にやけどと凍え | 1枚のカードで1回名前を出す |
| 早駆け | 値 `OpeningDraw` +1 | | |
| 連ね矢 | 値 `Hits` +1 | 持ち主のカード、回数が2以上 | |
| かばう | `EnemyTargeting` | 狙われた味方のHPが30%以下、持ち主は30%より上、1ターンに1回 | 代わりに受ける |
| 居合の型 | `CardPlaying` | ダメージを与える斬属性のカード、1ターンに1回 | そのカードの値 `Damage` +40% |
| あふれる祈り | `Healed` | 持ち主の回復、最大HPを超えた分がある | 超えた分を、回復された人のブロックにする |
| 歩み手の加護 | 値 `Heal` +ACTの% | 持ち主の回復 | |
| 聖火の鉄槌 | `DealtDamage` | 持ち主の打属性のカード、敵が倒れていない | 60%でやけど2ターン（予測に入れない） |
| 浄めの誓い | `StatusApplying` | 味方に付く悪い状態、1ターンに1回 | 取り消す |
| 急所狙い | 値 `Damage` +30% | 持ち主のカード、敵のHPが半分以下 | |
| 影の一刺し | `EnemyDefeated` | 持ち主のカード、1ターンに1回 | エネルギー+1 |
| 帯電の盾 | `GainedBlock` | 持ち主がブロックを得た | ランダムな敵へ属攻20%の雷（予測に入れない） |
| 魔力の盾 | `CardPlayed` | 持ち主の雷属性のカード、ダメージを与えた | 与えたダメージの20%をブロックに |
| 切り返し | `TookBlow` | 持ち主が攻撃を受け、敵が倒れていない | その敵へ物攻30%の斬 |
| 傷口を開く | `DealtDamage` | 持ち主のカードか効果の攻撃、敵が出血中で倒れていない | 出血のダメージをもう1回 |
| 見よう見まね | `CardPlaying`・値 `Damage` | ほかの味方の氷・雷のカードで重ねる（上限5）。持ち主の氷・雷のカードに重ね×10% | |
| 独り立ち | 値 `Cost` −1・`CardPlaying` | 見よう見まねが上限。持ち主の氷・雷のカード | 上限になったときに1回名前を出す |

## 段階

各段は、それまでの動作と強さを変えずに進める。
強さを変える変更（敵・カード・数値）は、この計画に含めない。

### 第1段（済み）：効果の部品

`Runtime/Effects/` に、きっかけ・値・条件・量・働き・効果・効果の一覧（呼び出しと回数）・戦闘の窓口（`IBattleWorld`）を置く。
EditModeの `BattleEffectsTests` で、偽の戦闘を相手に、部品の組み合わせ・回数・重ね・予測での扱い・連鎖の上限を確かめる。

完了条件：EditModeのテストが通る。画面のコードは変えない。

### 第2段（済み）：パッシブ18種を部品で書き直す

`CombatPassives` の各定義に効果を持たせ、戦闘画面はきっかけと値を呼ぶだけにする。
パッシブ専用の呼ぶ所14個と、「1ターンに1回」の旗を消す。
画面の公開の口（`HasPassive`・`PassiveHolder`・`CarriedEnergy`・`CopycatSeen`）は残す。

完了条件：`BattlePassivesSceneTests`・`AdventurePartyTests` と、関係するPlayModeテストが変更なしで通る。

### 第3段（済み）：予測を同じ計算・同じ効果で動かす

カードの攻撃のダメージ（上の表の1〜7）と、カードの条件の判定を、本番と予測で同じ関数にする。
予測の `IBattleWorld` を作り、予測でも効果をきっかけで呼ぶ。
`forecastingCard` の旗と、予測の中のパッシブの写しを消す。

完了条件：予測のテスト（`BattleInspectSceneTests` の予測の項目）が通る。

### 第4段（済み）：装備・編成・カードから効果を渡す口

`BattlePartyMember.Effects`、`EquipmentDefinition.WithBattle`（倍率を受け取って効果を書く）、`CardSkill.Effects` を足す。
冒険の戦闘は、着けている装備の効果を編成の1人に渡す。
2回発動は、今の確率を土台にして値 `TwiceChance` を通す。

完了条件：装備の効果を1つ仮に足すテストが通る（実データの装備には足さない）。

### 第5段（済み）：状態と冒険の強化を部品へ

攻撃力アップ・防御ダウン・プロテクト・無敵・反射を、状態の効果（`BattleStatuses`）にした。
値に `Multiply`（1つずつ掛けて丸める、`Order` の順）と、値 `Incoming`・`DamageTaken`、きっかけ `BlowLanding` を足した。
冒険の強化（背水・先手・弔い）を、値 `StatPercent` の規則（`BattleStatBoosts.EffectOf`）にし、強化が働き始めたときに名前を出す見せ方（`WhenGrows`）を足した。
凍結・凍え・速度アップ・挑発・炎の加護と、ターンごとのダメージの状態は、ターン順・狙い・ターンの処理が働きを持つまま残した。

### 第6段（済み）：カードの条件と働きを部品へ寄せる

`CardCondition` を部品の条件に置き換えて判定する（`CardRules.ConditionOf`）。
`CardAction` に `Only(BattleCondition)`・`Bonus(BattleCondition, %)`・`Adding(BattleAmount)` を足し、組み合わせの条件と量を書けるようにした。
カードの1回の当たりの処理（`ApplyHit` の分岐と、予測の写し）を `CardHits` にまとめ、本番・予測・画面なしの戦闘が `ICardWorld` を通して同じ処理を使う。
`BattleCardPlan` を `Runtime/Battle/` へ移した。
千刃乱舞の `PerDiscarded` は、当たりの組み立て（`BattleCardPlan`）で数えるまま残した。

### 第7段（一部済み）：戦闘のルールを画面から分ける

カードの使い始めと終わり（`CardPlays`）、カードの当たり（`CardHits`）、敵の攻撃の当たり方（`EnemyBlows`）、状態の重ね方・ダメージ・数え方（`BattleStatusRules`、状態の型 `BattleStatusEntry`）を `Runtime/Battle/` に移し、戦闘画面はそれを呼ぶだけにした。
画面なしの戦闘（`BattleState`）を足し、カードを使う・敵が攻撃する・敵が技を使う・ターンを進めることを、同じルールで一度に行えるようにした。
戦闘画面の味方と敵のHPは、Prefabに保存する欄でテストも直接書き換えるため、状態を画面から移すことはしていない（ルールだけを移した）。
手札・山札・敵の技の選び方を持つ画面なしの自動対戦は未着手である。

### 第8段（一部済み）：敵の特性と技

敵の種類が効果を持てるようにし（`EnemyBehavior.Effects`）、敵の技の働きを効果で書けるようにした（`EnemyMove.Effect`、きっかけ `Used`）。
味方に付く麻痺・防御ダウン・毒・やけどの働きを仮に決め（[仮設定](../features/combat/effects.md#仮設定敵が味方に付ける状態)）、状態を付ける敵の技20個（7種類）を `Other(...)` から実際の技にした。
仲間を呼ぶ・逃げる・復活など33個は `Other(...)` のままである。

## 注意すること

- **並行作業と重なる。** 戦闘のファイル（`Features/Combat/`）は、ほかのWorkspaceの作業でも触ることがある。各段の前に `git status` と最近の変更を確かめ、ほかの作業の変更があるファイルを書き換えない。重なったら、その段を止めてユーザーに知らせる。
- **見え方を変えない。** パッシブの名前を出す順番が、働きの前から後ろへ変わったものがある（切り返し・聖火の鉄槌など）。出るかどうかと量は変えない。
- **乱数を引く順番を変えない。** 自動対戦は乱数の種で同じ戦闘を繰り返すため、引く順番が変わると結果が変わる。第1段から第7段は引く順番を変えていない。第8段は、敵が新しい技を使うため結果が変わる。

## 再開するときの手引き

1. 決まりの正本は[戦闘システム：効果の部品](../features/combat/effects.md)、コードを書くときの注意は `client/Assets/Baryonyx/Features/Combat/Runtime/Effects/AGENTS.md` にある。
2. 残りは[効果の部品の未決事項](../features/combat/effects.md#未決事項)にある（画面なしの自動対戦、敵の技の残り、敵の攻撃の威力の値など）。

## 未決事項

[効果の部品の未決事項](../features/combat/effects.md#未決事項)へ移した。

## 実装の記録

- 2026-11-03：問題を調べ、ユーザーの「計画書を作って実装まで」を受けてこの計画を書いた。
- 2026-11-03：第1段から第4段を実装した。
  - 第1段：`Runtime/Effects/` に `BattleUnit`・`BattleTrigger`（きっかけ・値・ダメージの種類）・`BattleEvent`（起きたこと）と `BattleCardPlay`（使っている1枚）・`BattleCondition`・`BattleAmount`・`BattleAct` と `BattleWho`・`BattleEffect`（定義と規則）・`BattleEffects`（持ち主ごとの効果、回数、呼び出し）・`IBattleWorld`・`BattleMath` を置いた。`Baryonyx.Combat` のアセンブリ（UnityEngineを参照しない）に入る。Presentationに同じ名前の列挙 `BattleMoment`（演出の場面）があったため、起きたことの型は `BattleEvent` にした。
  - 第2段：`CombatPassives` の18個に効果を書き、`BattleInspectView.Passives` の呼ぶ所14個と旗（`coverUsed`・`iaiUsed`・`killEnergyUsed`・`vowUsed`・`copycatSeen`・`standingAlone`・`cardDealt` など）を消した。戦闘画面の窓口は `BattleInspectView.Effects`（`IBattleWorld` の本番の実装）で、きっかけはターン・手札・カード・攻撃・状態・ブロック・回復・敵の狙いの各所から呼ぶ。手札のコストは、効果が変えた分だけを追う（`CostByEffects`）ため、コストを下げるカードで下げた分は残る（前は独り立ちが働いたときに戻っていた）。
  - 第3段：カードの攻撃の段1〜7を `CardBlowPower`、カードの条件の判定を `Holds(IBattleWorld, ...)` にまとめ、本番と予測で同じ関数を使う。予測は `ForecastWorld` の上で同じ効果を呼ぶ。`ForecastPassives`・`ForecastDamage`・`forecastingCard` を消した。シールドバッシュのブロックの加算が予測にも入った。
  - 第4段：`BattlePartyMember.Effects`、`EquipmentDefinition.WithBattle`・`BattleEffectsAt`、`EquipmentStats.BattleEffects`、`CardSkill.Effects` を足し、冒険の戦闘（`AdventureParty`）が装備の効果を渡す。2回発動は値 `TwiceChance` を通す。今の装備とカードには効果を足していない。
  - 継続ダメージ（やけど・毒・出血・爆炎の刻印・雷雲）も `DealtDamage`・`EnemyDefeated` を種類 `Lasting` で呼ぶ。今のパッシブは種類で外すため、働き方は変わらない。
  - 乱数を引く順番は変えていない（会心・2回発動・帯電の盾の的・聖火の鉄槌の確率）。
  - 確かめたこと：EditModeの全件のうち、ほかの作業が編集中の装備の画面のテスト（`EquipmentPreviewTests` の1件、`EquipmentView.cs` などがほかの作業で変更中）を除いて通った。`BattleEffectsTests` 14件を足した。PlayModeの戦闘まわり10クラス92件（`BattlePassivesSceneTests`・`BattleInspectSceneTests`・`AdventurePartyTests`・`BattlePilesSceneTests` ほか）と、自動対戦の `BattleBalanceTests` 3件が通った。自動対戦での勝率とターン数の測り直しはしていない（強さを変えない移し替えで、乱数を引く順番も変えていないため）。
  - 並行作業：Workspace3が同じ時期に戦闘画面に戻るキーの口（`BattleInspectView.Back.cs`）と行動順の列の位置（`BattleInspectAssets.cs`）を変えた。触るファイルを分けて進めた。
- 2026-11-03：ユーザーの「他の状況も見ながら、第5、6、7、8段を一斉に」を受け、第5段から第8段を実装した。4つとも戦闘画面の同じファイルを触るため、並行のセッションには分けず、このセッションで1段ずつテストを通しながら進めた。Workspace2・Workspace3に触るファイルを先に伝えた。
  - 第5段から第7段のあとに、2026-11-01の自動対戦の依頼（forest-ruins・冒険5回・装備8個）を流し直し、26戦の勝率・ターン数・HPまで、変更の前と全項目が同じだった。
  - 第8段のあとに同じ依頼を流した結果（敵が状態を付ける技を使うようになった分）：

    | 組 | 戦闘 | 勝率 | ターン平均 | 勝ったときのHP |
    |---|---|---|---|---|
    | 変更の前（第1〜7段のあとも同じ） | 26 | 96% | 5.1 | 71% |
    | 第8段のあと | 25 | 92% | 5.1 | 73% |

    強い魔物のターン平均は7.3から9.3に、最奥の間の勝率は80%から75%になり、冒険5回の踏破は4回から3回になった（1回は7階の魔物の気配で負けた）。強さの調整はしていない。
  - そのあと、8つの行き先を推奨Lvで10回ずつ、技の有無だけを変えて比べた（約450戦ずつ、[敵の状態の技の試し](../features/stage-progression/battle-trials.md#敵の状態の技の試し2026-11-03)）。全体では勝率93.0%→93.9%、受けたダメージ953→935で、難しくならなかった。上の5回の差はばらつきの範囲だった。
  - 書きかけのC#の1つ（テストのファイル）がEditorに読み込まれ、Workspace3のビルドを一度止めた。すぐ直した。整形をフォルダーごとにかけて、触っていない3つのファイル（`CardKeywords.cs`・`CardSkills.cs`・`CardSkillAwakenings.cs`）の改行だけを変えてしまった。中身はHEADと同じで、戻す操作は確認で止められたため、commitに含めないよう残した。
  - ユーザーの依頼で、効果の部品のフォルダーに注意を書いた `AGENTS.md` を置き、決まりの正本として[効果の部品](../features/combat/effects.md)を作った。
- 2026-11-03：ユーザーの「7段階の自動戦闘も仕組みとして実装して」「ほかに残っているものも一緒に」を受け、第7段の残りとして画面なしで1戦を戦う `BattleSimulation` と自動対戦の `engine: headless` を、第8段の残りとして敵の技10種類（呪い・反射・すり抜け・身かわし・霧隠れ・燃えるうろこ・見張り・熱風の羽ばたき・延焼・丸のみ）を足した。しばらく持つ効果（`BattleAct.Hold`）、味方のカードの攻撃を敵の効果で止める口（`BlowLanding`）、状態の残りターン、防御を通さない攻撃、新しい状態の呪いを足した。敵の攻撃・ブロック・回復・狙いの式（`EnemyActs`）と敵の数値（`BattleFighter.OfEnemy`）は、戦闘画面と画面なしの戦闘で同じ関数にした。8つの行き先を回し直した結果は[戦闘の試し](../features/stage-progression/battle-trials.md#敵の状態の技の試し2026-11-03)にある。
