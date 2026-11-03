# AGENTS.md

このフォルダー（戦闘の効果の部品）と、隣の `../Battle/`（画面から切り離した戦闘のルール）の作業には、[client/AGENTS.md](../../../../../../AGENTS.md) と以下を適用する。
カード・キャラのパッシブ・装備・状態・冒険の強化・敵の技と特性の「〜したら〜する」「〜なら〜する」を足す・直すときも、ここに従う。
部品の一覧・きっかけと値・数を変える順番・仮設定の正本は [戦闘システム：効果の部品](../../../../../../../doc/features/combat/effects.md) にある。

## 効果を書く場所

- 1つの効果は、その出どころの定義の1か所に書く（[効果の出どころ](../../../../../../../doc/features/combat/effects.md#効果の出どころ)）。パッシブなら `CombatPassives`、装備なら `EquipmentDefinition.WithBattle`、カードなら `CardSkill.Effects` か `CardAction.Only`・`Bonus`・`Adding`、状態なら `BattleStatuses`、敵の技なら `EnemyMove.Effect`、敵の特性なら `EnemyBehavior.Effects`。
- 戦闘画面（`BattleInspectView`）・予測（`ForecastWorld`）・画面なしの戦闘（`BattleState`）に、パッシブ・装備・カードの1枚ごとの `if` を書かない。新しい種類のために `CardAction` や `EnemyMove` に1枚のための欄を足さない。
- 部品の組み合わせで書けないときは、まず `BattleCondition.Of`・`BattleAmount.Of`・`BattleAct.Custom` でその効果の中に書く。2つ目の効果でも同じものが要るとき、部品としてこのフォルダーに足す。

## 部品とルールを直すとき

- 効果は戦闘を `IBattleWorld`（カードと敵の攻撃は `ICardWorld`・`IFightWorld`）だけを通して読み書きする。このフォルダーと `../Battle/` は `Baryonyx.Combat` のアセンブリで、UnityEngineを参照しない。`BattleInspectView` や画面の型を使わない。
- `IBattleWorld` などに口を足したら、実装のすべて（戦闘画面の `BattleInspectView.Effects`・`BattleInspectView.CardWorld`、予測の `ForecastWorld`、`QuietWorld`、画面なしの `BattleState` と中の `Quiet`、テストの `FakeWorld`）を同じ変更で直す。
- 敵の攻撃・ブロック・回復・狙いの式は `../Battle/EnemyActs`、敵の数値は `BattleFighter.OfEnemy` にまとめてある。戦闘画面と画面なしの戦闘（`BattleSimulation`）のどちらかにだけ式を書かない。
- 新しいきっかけ（`BattleTrigger`）や値（`BattleValue`）を足したら、戦闘画面と画面なしの戦闘（`BattleState`・`BattleSimulation`）の両方の、それが起きる所から呼び、[きっかけと値](../../../../../../../doc/features/combat/effects.md#きっかけと値)の表を直す。呼ばれないきっかけを残さない。
- 数の順番（[数を変える順番](../../../../../../../doc/features/combat/effects.md#数を変える順番)）を変えない。`Add`・`AddPercent`・`Multiply` の使い分けと `Order`、丸め（`BattleMath`）で、今の数が1つも変わらないことを確かめる。
- 乱数（`IBattleWorld.Pick`・`BattleOdds`）を引く所を足す・動かすと、自動対戦が同じ種で同じ戦闘にならなくなる。強さを変えない変更では、引く所と順番を変えない。
- 確率やランダムで働く規則には `AtRandom()` を付けて予測から外す。予測（`Preview`）で、重ね・回数・取り消しのような効果の状態を変えない。
- 働きの中で起きたことは入れ子に呼ばれる（8段まで）。自分のきっかけをまた起こす効果は、条件で止める（例：傷口を開くの追加のダメージは種類 `Status` で、傷口を開く自身の条件から外れる）。
- 値の規則の回数（`OncePerTurn` など）は、値が実際に使われたときだけ数える。手札のコストのように表示のために何度も読む値には、回数を付けない。
- カードの条件（`CardAction.If`）には持ち主の効果がない。`m.Effect` を読む条件（`StacksBelowMax` など）をカードに使わない。カードの効果（`CardSkill.Effects`）は、その1枚の中で起きたことにだけ働く。
- `BattleEvent` は起きたことごとに新しく作る。1つを使い回すと、入れ子の呼び出しで中身が混ざる。
- 「次の自分の行動まで」のように、しばらくだけ持つ効果は `BattleAct.Hold` で書く。状態（`CardStatus`）を増やすのは、状態の一覧や説明文に出すものだけにする。
- 型の名前を、`Baryonyx.Combat.Presentation` の型と重ねない（演出の場面の列挙 `BattleMoment` と重なったことがある）。

## 確かめ方

- 部品は、EditModeの `BattleEffectsTests`（偽の戦闘）で確かめる。戦闘の形で確かめるときは `BattleStateTests`（画面なしの戦闘）にテストを足す。
- 戦闘画面での働きは、PlayModeの `BattlePassivesSceneTests`・`BattleInspectSceneTests` ほか、[パッシブの実装との対応](../../../../../../../doc/features/passives.md#実装との対応)の表のテストで確かめる。
- 書きかけのC#をEditorが読み込むと、ほかの作業のビルドまで止まる。ファイルの書き換えはまとめて行い、すぐ `--compile-only` で通す。テストの数が増えないときは、Editorが古いアセンブリのままなので、Editorのログの `error CS` を確かめる。
- 強さを変えない変更でも、終わったら[戦闘の自動対戦](../../../../../../../doc/rules/client-testing.md#戦闘の自動対戦)を同じ依頼で流し、前の結果と全項目が同じかを比べる。強さを変える変更なら、ルートの [AGENTS.md](../../../../../../../AGENTS.md#検証と報告) に従って測り直す。
