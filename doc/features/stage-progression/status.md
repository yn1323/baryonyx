---
id: feature-stage-progression-status
type: specification
status: 一部確定
updated: 2026-11-04
---

# 探索とダンジョンの進行：未決事項と実装との対応

[探索とダンジョンの進行](../stage-progression.md)

## 未決事項

| 領域 | 決める項目 |
|---|---|
| マップ | 網の目の道筋の数、枝分かれと合流の数。マスの種類のうち出来事・見通しの泉を加えるか（「？」は2026-10-24に加えた）。行商人の値段。[道の決まり](rooms.md#道の決まり仮)と[マスの中身](rooms.md#マスの中身仮)は2026-10-21に仮に決めた。探索画面を地図にすること、道を冒険ごとに作ること、マスの数は2026-10-04に、地図のレイアウトは2026-10-21に決定（[地図の作り直し](run.md#地図の作り直し2026-10-21)） |
| 冒険の長さ | 戦闘と移動の時間を実測したうえでのマスの数の調整。目安の15〜20分と、最初のダンジョンの道中8マスは2026-10-04に決定 |
| 序盤 | 序盤の階が作業にならない工夫（候補：序盤の敵を早く終わらせる、倍速） |
| 戦闘後の強化 | 強化の名前と絵、会心・幸運の強化を足すか（使い道の計算が決まってから）、強化を入れたあとの最奥の主の倍率。冒険のあいだだけ効くこと、飛ばせないこと、敵の強さで割合を変えること、段階を色だけで見せることは2026-10-24に、仲間1人に2つずつ出して1つ選ぶことと、仲間ごとの4つの列の画面は2026-10-31に決めた（[戦闘後の強化](boons.md#戦闘後の強化)） |
| ダンジョン構造 | 最終深度、区間の分け方。行き先の数と敵の組み方は、2026-10-20に[行き先と敵](destinations.md#行き先と敵)のとおり仮に決めた |
| 行き先の見た目 | 探索の地図を行き先ごとに変えるか。王墓の砂漠や白嶺の空の戦闘の背景を作るか |
| クリア回数の報酬 | 各回の報酬の中身（[例](destinations.md#クリア回数の報酬の例)）、ダンジョンでしか手に入らないキャラ、報酬を受け取る画面、お宝の窓を画面いっぱいの大きさにするか。クリア回数と解放の状態はサーバーに置く想定（[保存先の決め方](../../rules/data-storage.md#判断の基準)） |
| 解放 | ランクと最深到達の関係（解放の条件は、2026-11-04に入れ子の地図で町のマスに着くことと決めた。[ダンジョンの解放](destinations.md#ダンジョンの解放)） |
| 推奨Lvとレベルの上限 | 2026-10-23に、レベルの上限を30からLv100へ改め（[育成と強化](../progression.md#変更と判断の記録)）、どの行き先にも推奨Lvで挑めるようにした。上限より手前で行き先が終わるため、Lv40より先の行き先を増やすか、Lvの差を装備やカードの強さで埋めるかは未決（[推奨Lvより低いパーティの試し](battle-trials.md#推奨lvより低いパーティの試し)）。2026-10-23に、街道で最初の街から遠いほど推奨Lvを上げる方向にした |
| 情報 | 地図や場所選択での開示範囲、深度・階層の表記、成長を比較する記録単位 |
| 探索画面 | 地図のカメラの動き、2Dの絵で描くか3Dの舞台で描くか。宝箱・出来事を地図の上で開くことと、マスを押せる範囲は、2026-11-02に今の作りで確定した（[マス単位の探索と操作](run.md#マス単位の探索と操作)） |
| 中断 | 戦闘の状態そのもの（ターン・手札・HP）を保存して途中から再開するか（2026-10-04の決定では、選んだマスから戦闘を始めからやり直す） |
| 報酬 | 抽選・確定・受け取りタイミングと、冒険終了・中断時の未確定報酬の扱い |
| 個別コンテンツ | 通常敵・ボスの行動、狙える装備、地域数と必要アセット数 |

## 確認待ち

既存の仕様と今回の内容のどちらを採るか、開発者の判断が必要な事項である。
回答を得るまで、ほかの仕様や実装へ波及させない。

| 項目 | 既存・旧仕様 | 今回の内容 | 影響する範囲 |
|---|---|---|---|
| 中断による戦闘のやり直し | 戦闘単位の無料再戦は廃止する（[戦闘の確認待ち](../combat/implementation.md#確認待ち)） | 戦闘の途中で中断すると、再開したときにその戦闘を始めからやり直す（2026-10-04） | 負けそうなときに中断すれば、実質的に戦闘をやり直せる。中断を戦闘の途中で認めるか、戦闘の状態を保存して途中から再開するか |
| ダンジョンの規模と長期目標 | 巨大なダンジョンの進捗を持ち越し、短い冒険を積み重ねる。果てしなく遠く感じる規模を目指し、近い目標は次の区間への進行、長期の目標は踏破とする | 1回の冒険で深奥のボスを目指し、負けたら1階から。冒険の長さは15〜20分が目安（2026-10-04） | 長期目標（深奥のボスの撃破か、複数のダンジョンの解放か）、[ホーム](../screens/top-home.md#ホーム画面の見た目モック)のワールドマップと行き先カード。2026-10-23に、街道で街を広げてダンジョンを解放し、街道ではクリアしたマスが残る[構想](destinations.md#街道とダンジョン)を決めた。進み具合の持ち越しは街道に生かせるが、旧方針の巨大なダンジョンの扱いは引き続き確認待ち |
| マスの種類を端末から受け取ること | 書き換えられたら困るデータはサーバーに置く（[保存先の決め方](../../rules/data-storage.md#判断の基準)） | 道は端末が種から作り、マスの階と種類を端末からサーバーへ送る。ユーザーは「途中の進捗はUnity内に保存する形でOK」とした（2026-10-04） | 端末を書き換えると、宝箱や強い魔物のマスを選び続けて報酬を増やせる。サーバーにも同じ道を作る処理を置いて確かめるか、リリース前に決める |
| 再出発と周回の候補 | 帰還後は解放済みの野営地・中継地点から出発する案、同じ敵・場所をリザルトから直接再挑戦する案 | 冒険が終わると1階からやり直す | 両案を廃止するか、別の形で残すか |

## 実装との対応

本ページは企画上の要求・設計方針・未決事項を記す。
2026-10-04に、冒険の1周（出発・マス単位の探索・戦闘・報酬・復活・結果・中断と再開）と、冒険の状態のサーバー保存を実装した（[実装計画](../../plans/2026-10-04-adventure-loop.md)）。
冒険先は、2026-10-20から[行き先と敵](destinations.md#行き先と敵)の8つである（[サーバーの設定値](../../../server/src/features/adventure/catalog.ts)）。
ダンジョンの解放は、行き先の街のマスに着いたかで決め、解放の記録は別に持たない。着いたかは、[入れ子の地図](../world-atlas.md)で開けた町のマス（`world_atlas_squares`）か、[前の街道](../stage-progression-world-map.md)でクリアした街のマス（`world_map_squares`）で見る（2026-11-04に入れ子の地図を足した）。門前町ポルタはいつも解放している。クライアントの判定は [DungeonUnlocks](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/DungeonUnlocks.cs) にまとめ、旅の案内所のダンジョン一覧（[GuideSceneBootstrap](../../../client/Assets/Baryonyx/App/Runtime/GuideSceneBootstrap.cs)、[旅の案内所](../screens/guide.md#編成商会神殿旅の案内所の画面)）と、町に着いたときの知らせ（[WorldAtlasNavigator](../../../client/Assets/Baryonyx/Features/WorldMap/Runtime/WorldAtlasNavigator.cs) の `AnnounceDungeon`）が使う。サーバーは冒険を始めるときに同じことを確かめ、まだなら409（`destination_locked`）で断る（[catalog](../../../server/src/features/adventure/catalog.ts) の `town`、[run.ts](../../../server/src/features/adventure/run.ts)）。
最初の行き先（ミストラ遺跡）は道中のマスが8つで、最奥の間は第10層になる。
出来事を終えたマスでは、行き先の一覧から物が落ちる（[冒険のドロップ](../drops.md#実装との対応)）。
序盤の工夫は未実装である（装備・スキルの入手は[冒険のドロップ](../drops.md#実装との対応)、深さに応じた敵は[行き先と敵](destinations.md#行き先と敵)で実装した）。
戦闘画面は、行き先の敵の絵・狙える範囲・顔・弱点・数値を、モックの3つの場所へ入れ替えて出す（[BattleInspectView.Outcome](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.Outcome.cs)、敵の数値は [CombatEnemyStats](../../../client/Assets/Baryonyx/Features/Combat/Runtime/CombatEnemyStats.cs)）。
2026-10-22に、[網の目の道](rooms.md#冒険の道の作り方)・[新しいマスの種類](rooms.md#マスの種類)・[道の決まり](rooms.md#道の決まり仮)を実装した。道とマスの種類はクライアントの [AdventureRouteMap](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/AdventureRouteMap.cs) が作り、サーバーの [catalog.ts](../../../server/src/features/adventure/catalog.ts) の `canPlace` が、受け取ったマスの種類をその階に置けるかを確かめる。報酬の行はサーバーの [drops.ts](../../../server/src/features/adventure/drops.ts) の `rewardOf` とクライアントの [AdventureDrops](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/AdventureDrops.cs) の `RewardOf` が同じ値を持つ。
同じ日に、[HPの持ち越し](run.md#hpの持ち越し)と休憩のマスを実装した。持ち越すHPは、最大HPに対する割合を編成の枠ごとに、冒険ごとに端末へ保存する（[AdventureVitals](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/AdventureVitals.cs)、`Adventure.Vitals`）。戦闘は保存した割合から始め（[BattleBootstrap](../../../client/Assets/Baryonyx/App/Runtime/BattleBootstrap.cs)）、勝ったときの割合を保存する（[BattleAdventureFlow](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/BattleAdventureFlow.cs)）。戦闘の途中で中断したときは保存しないため、再開した戦闘はその戦闘の始めのHPからになる。休憩のマスは、着いてマスを終えたと保存できてから全員を最大HPの40%回復し、「焚き火で休んで、全員のHPが回復した」と知らせる。探索の地図の上の左寄り（左上のメニューの右）には、2026-10-26から4人のHPと倒れている人を出す（[ExplorationView](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/ExplorationView.cs)）。
同じ日に、残りのマスの中身と[地図の作り直し](run.md#地図の作り直し2026-10-21)を実装した。
- 怪しい宝箱：押すと「開ける」「通り過ぎる」を選ぶ。開けると、サーバーが半分の確率でミミックかを決めて保存する（`POST /v1/adventure/open`）。ミミックなら宝箱ミミックと行き先の小さな敵の戦闘になり、勝つと最大の報酬（主の物を除く）。本物ならその場で強い報酬を1つ多く落とす。通り過ぎると何も落とさずに終える（`clear` の `pass`）。
- 試練：マスに入るとサーバーが条件を選んで保存し、探索と戦闘の始めに知らせる。勝ったときに端末が判定し（倒れた味方がいるか、その戦闘で復活したら「誰かが倒れた」とみなす）、`clear` の `trialMet` で伝える。サーバーは判定せず、端末を信じる。
- 歩数の扉：押すとサーバーが今日の歩数（[起動時の連携と歩数の同期](../startup-sync.md)で送った、その日の歩数）を読んで報酬の段を決め、「今日の歩数 N歩：扉が〜」と知らせてから中身を見せる。その日の歩数をまだ同期していなければ0歩として扱う。
- 行商人：着くと品物の一覧を開く。値段は☆1が1,500ルーン、☆が1つ増えるごとに倍にし、品質の倍率（C1.0・B1.25・A1.5・S2.0）を掛けて10の位で丸める（仮。ユーザーが「おまかせ」とし、初めはLv10→11の費用1,000ルーンに合わせて☆1を300にした。2026-10-26に、ユーザーの依頼で商会とともに5倍にした（[ルーン経済の記録](../economy.md#変更と判断の記録)））。買うと確かめてから買い、持ち物に入れる（`POST /v1/adventure/buy`）。立ち去るとマスを終える。正本はサーバーの [merchant.ts](../../../server/src/features/adventure/merchant.ts)。
- 覚醒の祭壇：押すと、仲間・固有スキル・覚醒先（セットしている段階の次の段階）の順に選び、確かめてから覚醒させる（[AdventureAltar](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/AdventureAltar.cs)）。先にマスを終えたこと（`clear` の `evolved`）を保存し、保存できてから、取得した段階をセットして編成のAPIで保存する（[固有スキルの覚醒](../skill-evolution/rules.md#覚醒の保存)の `PartyUniqueSkills.Evolve`）。覚醒させたら物を落とさない。進められる固有スキルが1つもなければ、宝箱と同じに開けて宝箱と同じ物を落とす。
- 地図：左から右へ進む1枚の板に、行き先ごとの地面（Codexの生成画像を清書せずに使う。2026-10-22にユーザーの判断で[ドット絵の規格の例外](../../art/direction.md#例外)にした）を敷き、マスを光の線で結ぶ。次に進める道は金色で脈打ち、その先の行ける道は淡い青、通った道は暗い金、行けなくなった道は消えかけで描く。パーティの駒は画面の左から約4分の1に立ち、1本指で地図を左右に動かして先を見る。下の細いバーが地図の中の位置を、右下の「？」が凡例を開く。右上に行き先と層、その下に案内を小さく出す。森を見下ろす地図の描き方（ExplorationMapPainter など）と、森の木の画素の写しは削除した。
2026-10-04に、探索を森を見下ろす地図にし、道を冒険ごとに作るようにした（[計画](../../plans/2026-10-04-exploration-route-map.md)、[画面](../screens/adventure.md#冒険の画面)）。森の地図は、2026-10-22に上の地図へ作り直した。
道を作る処理は [AdventureRouteMap](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/AdventureRouteMap.cs)、地図の並べ方と描き方は [ExplorationView](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/ExplorationView.cs)、マスの中身の型は [AdventureContents](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/AdventureContents.cs) にある。
2026-10-19から、戦闘のマスを終える知らせ（`POST /v1/adventure/clear`）と、負けて冒険を終える知らせ（`POST /v1/adventure/end`）に、その戦闘で遭遇した敵の種類と倒した体数を添えて送る。サーバーは[ライブラリ](../library.md#敵の記録)の記録にだけ使い、報酬や進み具合の判定には使わない。
2026-09-19時点の[操作ワイヤー](../../archive/game-wireframe.md)は文字のボタンで戦闘・獲得へ進む試作で、2026-09-24に削除した。
2026-10-24に、[戦闘後の強化](boons.md#戦闘後の強化)を実装した。強化の種類・候補の作り方・ステータスへの掛け方は [AdventureBoons](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/AdventureBoons.cs)、4つの列の窓は [AdventureBoonPicker](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/AdventureBoonPicker.cs)（組み立ては [AdventureBoonPickerAssets](../../../client/Assets/Baryonyx/Features/Adventure/Editor/AdventureBoonPickerAssets.cs)）、選んだ強化・虹の出ない回数・選んでいない強化のマスの保存は [AdventureVitals](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/AdventureVitals.cs) にある。勝った戦闘は、勝ったことを保存する前に選ぶ強化を待つマスを端末に残し、ドロップの札を見せたあとに候補を開く（[BattleAdventureFlow](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/BattleAdventureFlow.cs)）。再開した探索は、今いるマスが終わっていて強化を待っていれば、同じ候補を開く（[ExplorationFlow](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/ExplorationFlow.cs)）。選んだ強化は、休憩の鍛えると足し合わせて、戦闘の前にステータスへ掛ける（`AdventureParty.Hone`）。背水・先手・弔いは、戦闘で味方のステータスを読む所（カードの威力、パッシブの切り返しと帯電の盾、ターン順の速度、受けるダメージの防御）で、その時のHP・ターン・倒れた仲間の数から掛け直す（[BattleStatBoosts](../../../client/Assets/Baryonyx/Features/Combat/Runtime/BattleStatBoosts.cs)、[BattleInspectView.Boosts](../../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.Boosts.cs)）。数値は自動対戦で試していない。
| 対象 | 入口 |
|---|---|
| 冒険の状態の読み書き | [AdventureSource](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/AdventureSource.cs)・[API](../../../server/src/features/adventure/routes.ts)・[DB操作](../../../server/src/features/adventure/repository.ts) |
| 探索と戦闘の流れ | [探索の流れ](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/ExplorationFlow.cs)・[戦闘との受け渡し](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/BattleAdventureFlow.cs)・[旅の案内所の出発](../../../client/Assets/Baryonyx/Features/Adventure/Runtime/TravelDeparture.cs) |
| 画面とシーンの生成 | [画面の生成](../../../client/Assets/Baryonyx/Features/Adventure/Editor/AdventureAssets.cs)・[探索シーンの生成](../../../client/Assets/Baryonyx/App/Editor/ExplorationSceneSetup.cs) |
