---
id: feature-combat-implementation
type: specification
status: 一部確定
updated: 2026-11-02
---

# 戦闘システム：未決事項と実装

[戦闘システム](../combat.md)

## 未決事項

未決は採用候補や調整・確認待ちを指し、決定済みとして扱わない。

| 領域 | 決める項目 |
|---|---|
| ターン順 | 連続ターンになるしきい値と、敵が複数いる場合の速度の比べ方は、2026-10-22に[仮設定](rules.md#ターン順の仮設定)を置いた。速度アップ・凍えは2026-10-24に速度へ掛けた。行動を遅らせるカードをターン順に効かせる形 |
| 手札 | 毎ターン手札が流れるようになったため（2026-10-25）、「使用条件を付けない」決まりを外すか。味方が1人だけ残ると、山札がその人の6枚以下になり、同じカードばかり引く。対策（候補：8枚に足りない分を、その人が使えるカードから戦闘の中だけの一時カードで補う）は、試遊で決める（2026-10-23） |
| カードの使用 | 1ターンに使えるカード枚数の制限の有無、エネルギー以外の制約 |
| エネルギー | カードの効果で3より多くなってよいか。なってよいなら上限をいくつにするか（パッシブの星図の計算・影の一刺しも同じ上限に従う）。今の実装は、カードの効果と影の一刺しで増えたエネルギーを仮に9で止める（`BattleInspectView` の `MaxEnergyCap`）。星図の計算の持ち越しは止めない |
| キーワード | 呼び名（消滅・保持・霧散・初手・捨てる・眠り札・歩み・合体・連携は仮）。眠り札の段数と増え方（仮：3段、1段ごとに+50%）。合体カードの持ち主と、どちらかが倒れたときの扱い |
| 防御 | 具体的な形式（仮：Slay the Spire式の「ブロック」を、ターンをまたいで残し、味方のターンの始めに半分にし、最大HPを上限にする形で試している）。残す割合 |
| 属性 | 物理属性ごとのクセ（候補：斬＝全体・連続、打＝一撃が重い、貫＝ブロックを一部無視） |
| 弱点の開示 | 未開示の弱点の見せ方（「？」の枠を出すか、何も出さないか）、開示したときの演出、1体に複数の弱点を持たせるか、亜種と開示を共有するか、突く以外に開示する手段を設けるか |
| 数値 | カード・装備が参照するステータスと割合、回復の計算式、補正の重ね方（候補：同じ種類の補正は足し算、別の種類は掛け算）、バフ・デバフの重ね方と持続ターン、持続ダメージ（やけど・毒・出血）に防御を通すか（モックは通さない） |
| 状態異常の付与率 | 狙いを付けたときに、幸運で補正したあとの付与率を見せるか。基本確率は2026-10-25に決めた（[状態異常の付与率の仮設定](rules.md#状態異常の付与率の仮設定)） |
| ACTで効き目が変わるカード（[歩み]） | 参照するACTと効果量の変え方（仮：今日のACT1,000ごとに+10%、上限+100%） |
| 威力の伸びの見せ方 | 説明文をステータスの割合で書くため、装備やLvで威力がどれだけ伸びたかを付け替えの画面でどう見せるか（[カードの数値の見せ方](cards.md#カードの数値の見せ方)） |
| 敵 | 敵の定義の技・行動パターン・激化は2026-10-22に戦闘へ入れた（[敵の行動の仮設定](../combat-enemy-behavior.md)）。味方に付く状態・呼び出しなど、まだ戦闘にない効果の入れ方と、溜めを崩す仕組み |
| 戦闘をまたぐ状態 | 2026-10-21に、HPと倒れているかをマスの間で持ち越すと決め、2026-10-22に実装した（[HPの持ち越し](../stage-progression/run.md#hpの持ち越し)）。倒れたまま来た味方は灰色で立ち、そのカードは山札に入らない。ルーン復活は全員を満タンにする。バフ・デバフ・ブロックを消すのは仮 |
| ルーン復活 | 費用と回数制限（候補：2回目以降はコスト上昇、または1回まで。冒険の1周では仮に1回目100ルーン、以後100ずつ上げていたが、2026-10-26に1回目300ルーン、以後300ずつ上げる形にした）、復活画面から編成・装備を変えられるか |
| 中断 | 戦闘中の保存・再開、アプリ終了・背面移行・通信断への対応。マスの区切りでの中断とは分けて決める |
| 戦闘の長さ | 通常の敵に激化を付けるか（付けるなら3ターン目に早める案）、1ターンの秒数の実測、倍速の有無、ターン数の目安に合わせた敵のHPと味方の火力 |
| 固有スキル | コストと強さ（2026-10-25に、エネルギー3固定に合わせて0〜3で作り直した。[各段階の効果](../skill-evolution/trees.md#各段階の効果)）。解放前の枠の扱いは[パーティの未決事項](../party.md#未決事項) |
| 個別コンテンツ | 初期キャラのスキル・固有スキル、通常敵とボスの行動。パッシブスキルは[仮設定](../passives.md#仮データの9人のパッシブ)がある |

## 確認待ち

既存の仕様と今回の内容のどちらを採るか、開発者の判断が必要な事項である。
回答を得るまで、ほかの仕様や実装へ波及させない。

| 項目 | 既存・旧仕様 | 今回の内容 | 影響する範囲 |
|---|---|---|---|
| 無料再戦の廃止 | 敗北時に、無料で戦闘を最初からやり直せた | 廃止する（負けたら冒険終了のため）。見直しの資料に「開発者に最終確認」と付記 | 敗北時の選択肢、ルーン復活の位置づけ、[探索の進行](../stage-progression.md) |

## 関連する仕様とデータ

[探索と進行](../stage-progression.md)・[パーティ](../party.md)・[装備](../equipment.md)・[育成](../progression.md)・[ルーン経済](../economy.md)・[画面](../screens.md)・[セーブ](../accounts-save.md)・[試作と評価基準](../../game/mvp.md)・[パッシブスキル](../passives.md)・[固有スキルの覚醒](../skill-evolution.md)・[カスタムスキルの覚醒](../skill-awakening.md)・[スキルの溜めの演出](../skill-windups.md)・[敵の行動の仮設定](../combat-enemy-behavior.md)

[技・カード](../../catalog/skills/README.md)・[パッシブ](../../catalog/passives/README.md)・[効果](../../catalog/effects/README.md)・[属性](../../catalog/attributes/README.md)・[敵](../../catalog/enemies/README.md)

## 実装との対応

本ページは企画上の要求・設計方針・仮設定・未決事項と、実装の入口を記す。

2026-10-25に、エネルギー3固定のルール（[エネルギーとコスト](rules.md#エネルギーとコスト)・[手札](rules.md#手札)・[カードのキーワード](cards.md#カードのキーワード)・[倒れた味方](rules.md#倒れた味方)・[敗北とルーン復活](rules.md#敗北とルーン復活)）と、作り直したカード・固有スキルを実装した（[計画](../../plans/2026-10-25-fixed-energy-cards.md)）。

| 内容 | 場所 |
|---|---|
| 山札・捨て札・消滅の出し入れ（尽きたら捨て札だけを切り混ぜる、初手を上に置く、倒れた味方のカードを抜いて戻す） | [BattleDeck](../../../client/Assets/Baryonyx/Features/Combat/Runtime/BattleDeck.cs)・[BattleDeckTests](../../../client/Assets/Baryonyx/Features/Combat/Tests/EditMode/BattleDeckTests.cs) |
| エネルギー3・5枚引く・ターンの終わりに流す・ルーン復活の配り直し | [BattleInspectView](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.cs)・[BattleInspectView.Moments](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.Moments.cs)。引く枚数と手札の上限は `Features/Combat/UI/BattleInspectHandSettings.asset`（[調整できる値](../screens/battle-deck.md)） |
| 消滅・保持・霧散・初手・眠り札・捨てる（選ぶ）・捨て札から拾う（選ぶ）・連携・合体 | [BattleInspectView.Piles](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.Piles.cs)・[BattlePilesSceneTests](../../../client/Assets/Baryonyx/Features/Combat/Tests/PlayMode/BattlePilesSceneTests.cs) |
| 起き上がった味方が自分のカードを2枚引く | [BattleInspectView.Fallen](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.Fallen.cs) |

効果で手札を捨てるときは、捨てるカードを手札から押して選ぶ（帯に「捨てるカードを選ぶ」と出る）。捨て札から拾うときは、捨て札の新しいほうから最大9枚を画面の中央に1列に並べ、押して選ぶ。選んでいる間はターン終了を押せない。
画面の配置（エネルギー・山札・捨て札・消滅・ターン終了）は[戦闘画面の見た目モック](../screens/battle.md#戦闘画面の見た目モック)にある。

2026-10-04、冒険の1周のため、戦闘画面のモックに勝敗で終わるモードを加え、`Battle.unity` を冒険の戦闘にした（[冒険の画面](../screens/adventure.md#冒険の画面)・[実装計画](../../plans/2026-10-04-adventure-loop.md)）。
冒険から開いた戦闘は、マスの敵の組み合わせ（行き先に棲む敵から選ぶ。[行き先と敵](../stage-progression/destinations.md#行き先と敵)）で戦い、敵をすべて倒すと勝利、味方がすべて倒れると敗北で終わる。
敗北すると[復活画面](rules.md#復活画面)を出す。ルーンで復活したときの扱い（敵の減ったHPはそのまま、ターンは最初からやり直す）は[敗北とルーン復活](rules.md#敗北とルーン復活)のとおりである。
費用はサーバーが計算して残高から引く（[冒険API](../../../server/src/features/adventure/routes.ts)）。
冒険の戦闘の味方と山札は、今の編成の人・Lv・ステータスと、解放済みの固有スキル・カスタムスキルのスキルから作る（[パーティの実装との対応](../party.md#実装との対応)）。
冒険の戦闘の敵は、2026-10-22から、敵の定義の技と行動パターンで動き（[敵の行動の仮設定](../combat-enemy-behavior.md)）、ターン順は速度から決める（[ターン順の仮設定](rules.md#ターン順の仮設定)）。ターン順の計算は [CombatTurnOrder](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatTurnOrder.cs)、敵の技の動きは [BattleInspectView.EnemyMoves](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.EnemyMoves.cs) にある。敵の速度は [CombatEnemyStats](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatEnemyStats.cs) に敵の定義のLv100の値を写した。2026-10-25から、味方のステータスには付けている装備の上昇分が入り、装備の「攻撃スキルが2回発動」の効果も働く（[装備の効果](../equipment.md#装備の効果)）。単体で開く戦闘画面のモックは、モックの4人と16枚のままである。
勝敗の処理は [BattleInspectView.Outcome](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.Outcome.cs)、冒険との受け渡しは [BattleAdventureFlow](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/BattleAdventureFlow.cs) にある。

[スキルの仮設定](cards.md#スキルの仮設定)は、次のコードにある。
戦闘画面のモックは、このうち15枚と試作の蛇牙の連撃の16枚を山札にし、カードを使うとここに書いた効果を実際に処理する（[戦闘画面の見た目モック](../screens/battle.md#戦闘画面の見た目モック)）。

| 内容 | 場所 |
|---|---|
| 50枚の定義（効果・コスト・属性・説明文）。試作（`Prototypes`）と合体カード（`Fusions`）も同じファイル | [CardSkills](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CardSkills.cs)・[CardSkill](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CardSkill.cs) |
| 威力の計算、説明文の組み立て、状態の名前と持続ダメージ | [CardRules](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CardRules.cs) |
| キーワードと説明（[カードのキーワード](cards.md#カードのキーワード)） | [CardKeywords](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CardKeywords.cs) |
| カードの当たる相手の決め方 | [BattleCardPlan](../../../client/Assets/Baryonyx/Features/Combat/Runtime/Battle/BattleCardPlan.cs) |
| 山札（引く・尽きたら作り直す・倒れた味方のカードの出し入れ） | [BattleDeck](../../../client/Assets/Baryonyx/Features/Combat/Runtime/BattleDeck.cs)・[BattleDeckTests](../../../client/Assets/Baryonyx/Features/Combat/Tests/EditMode/BattleDeckTests.cs) |
| 運に任せるところ（山札を混ぜる・相手をランダムに選ぶ・確率で起こす）の乱数。テストは種を決めた乱数を渡す | [BattleDice](../../../client/Assets/Baryonyx/Features/Combat/Runtime/BattleDice.cs)・[BattleDiceTests](../../../client/Assets/Baryonyx/Features/Combat/Tests/EditMode/BattleDiceTests.cs) |
| 敵の一撃の弱め方と狙い（麻痺・ブロック・挑発） | [BattleBlows](../../../client/Assets/Baryonyx/Features/Combat/Runtime/BattleBlows.cs)・[BattleBlowsTests](../../../client/Assets/Baryonyx/Features/Combat/Tests/EditMode/BattleBlowsTests.cs) |
| モックでの効果と状態の処理 | [BattleInspectView.Cards](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.Cards.cs) |
| カードごとの演出 | [BattleSkillVfx.Cards](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleSkillVfx.Cards.cs) |
| 定義の検査（ID・名前・挿絵の重複、説明文の数値、キーワードの説明の有無） | [CardSkillsTests](../../../client/Assets/Baryonyx/Features/Combat/Tests/EditMode/CardSkillsTests.cs) |
| 効果の重複の検査（覚醒の段階を含む） | [CardSkillAwakeningsTests](../../../client/Assets/Baryonyx/Features/Combat/Tests/EditMode/CardSkillAwakeningsTests.cs) |
| 防御の式と成長率 | [CombatFormula](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatFormula.cs)・[CombatFormulaTests](../../../client/Assets/Baryonyx/Features/Combat/Tests/EditMode/CombatFormulaTests.cs) |

2026-10-04、戦闘画面のモックのダメージに[ダメージと防御](rules.md#ダメージと防御)の式を入れた。
モックの味方と敵にLvと物防・属防を持たせ、カードのダメージ、敵の攻撃、反射、雷雲と爆炎の刻印を防御で減らす。
カードにはまだ物理・魔法の区分がないため、属攻から出した威力を魔法（属防で受ける）、物攻・物防から出した威力を物理（物防で受ける）として扱う。
敵の攻撃は、属攻が物攻より高い敵（魔法で攻める敵）なら味方の属防で、ほかは物防で受ける（2026-10-20）。
やけど・毒・出血のダメージは防御を通さない。
カードの説明文は、[カードの数値の見せ方](cards.md#カードの数値の見せ方)のとおりステータスの割合で書き、使い手のステータスや狙った敵の防御は入れない。
2026-10-25に、[会心の一撃](rules.md#会心の一撃)と[状態異常のかかりやすさ](rules.md#状態異常のかかりやすさ)を戦闘に入れた。
敵の会心と幸運は、[敵の定義](../../catalog/enemies/README.md)のLv100の値を [CombatEnemyStats](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatEnemyStats.cs) に写して使う。
式は [CombatFormula](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatFormula.cs)、乱数は同じファイルの `BattleOdds` が持つ。テストでは `BattleOdds` を、会心しない・状態が必ず付く形に固定する。
[歩み]のカードと歩み手の加護が使う今日のACTは、戦闘ではまだ歩数を読まず、仮の値6,000（`BattleInspectView` の `TodayAct`）を使う。

戦闘画面のモック・戦闘の背景・デバッグルームの実装の入口は、[画面一覧の実装との対応](../screens/implementation.md#実装との対応)にある。
