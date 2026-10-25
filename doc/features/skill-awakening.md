---
id: feature-skill-awakening
type: specification
status: 一部確定
updated: 2026-10-24
---

# カスタムスキルの覚醒

[機能索引](README.md) / [固有スキルの覚醒](skill-evolution.md) / [持ち物の分類、レア度と品質](inventory.md) / 根拠：2026-10-24のユーザーとの相談

カスタムスキルの枠に入れる[スキル](party.md#持っているスキル)も、[固有スキル](skill-evolution.md)と同じく**覚醒**して別の段階のカードに変わる。
覚醒は持っている1枚ずつに起き、その1枚だけが変わる。
同じ種類のスキルでも「この1枚はこの枝に育てた」という違いができ、同じスキルを何枚も持つ意味が生まれる。

固有スキルの覚醒の楽しさを、ほかの持ち物にも広げたいというユーザーの意向から作った（2026-10-24）。
参考にしたのは、固有スキルと同じく、カオスゼロナイトメアのカードのひらめきである（[Game8](https://game8.co/games/Chaos-Zero-Nightmare/archives/558115)、2026-10-24確認）。

## 確定している方針

2026-10-24、ユーザーとの相談で次を決めた。

### 対象

- 対象は、持っているスキル1枚ずつである。同じ種類の別の1枚は覚醒しない。
- カスタムスキルの枠に入るアイテム（道具・素材）は、1つずつの個性を持たないため覚醒しない（[数え方](items.md#数え方)）。
- 固有スキルの覚醒は[固有スキルの覚醒](skill-evolution.md)に従う。

### 段階

スキルは、最初の段階から2つの枝に分かれ、それぞれ1段だけ進む。
1つの種類は、最初の段階を含めて3つの段階を持つ。

```text
最初の段階 ─┬→ 枝1
            └→ 枝2
```

- 各段階の効果・名前・挿絵は、スキルの種類ごとに個別に作る。名前は「元の名前・漢字1〜2字」の形にする（例：ファイア・灼）。固有スキルの段階の名前と同じ形である。
- 50種類×2枝で、100の段階を作る。各段階は、スキルの定義文書の「覚醒版」の節に書く（[一覧](#覚醒版の一覧)）。
- 覚醒した1枚は、元の段階に戻せない。別の枝が欲しいときは、同じ種類の別の1枚を覚醒させる。
- 倍率・品質は、覚醒しても引き継ぐ。倍率の上限とレア度は、元の種類のものを使う（[品質](inventory.md#品質)・[レア度](inventory.md#レア度)）。

### 覚醒が起きる場面

| 場面 | 起き方 |
|---|---|
| 戦闘 | 冒険の戦闘で、スキルのカードを手札に引いたときに抽選し、当たったカードを手札で光らせる。光ったカードを使うと覚醒する。固有スキルの[覚醒の予兆](skill-evolution.md#覚醒の予兆)と同じ決まりで、1回の戦闘で1枚1段までも同じである |
| 商会 | 商会の「スキル覚醒」で、覚醒の欠片とルーンを払って覚醒させる（[商会の強化](enhancement.md#覚醒)） |

宝箱と覚醒の祭壇では、スキルを覚醒させない（2026-10-24、ユーザー決定）。
どちらもパーティの固有スキルから1枚を選ぶ場で、スキルまで並べると選ぶ相手が増えすぎるためである。

### 覚醒先を選ぶ

戦闘中の覚醒は、固有スキルと同じ[覚醒先を選ぶ画面](skill-evolution.md#覚醒先を選ぶ画面)で選ぶ。
候補は、まだ覚醒していない1枚の2つの枝である。

スキルの選択画面には、2つの枝のほかに「覚醒しない」を置く（2026-10-24、ユーザー決定。見せ方はこちらで決めた）。
固有スキルは編成で前の段階に戻せるが、スキルは戻せないため、戦闘の途中で戻せない選択を必ずさせないようにする。
固有スキルの選択画面には出さない。

| 項目 | 決まり |
|---|---|
| 置き場所 | 候補の下の「この覚醒にする」の左に並べる。「この覚醒にする」は右へ寄せる。文字は控えめな色にし、決定より目立たせない |
| 押せるとき | 候補を選んでいなくても押せる |
| 押したあと | 選択画面が消え、カードは割れない。魔法陣と光が静まり、ひびの入ったカードが少し沈んでから、淡い光の尾を引いて使い手へ戻る。そのあと、元の段階のまま効果を出す |
| その後 | その1枚は、その戦闘ではもう光らず、抽選もしない。次の戦闘では、また抽選する。サーバーへは何も送らない |

### 保存

覚醒した段階は、ユーザーごとにゲームサーバーのDBへ、持っているスキルの1枚ごとに保存する。
保存の時点と送り方は、固有スキルの[覚醒の保存](skill-evolution.md#覚醒の保存)と同じにする。
戦闘中の覚醒はそのマスの戦闘を終えたときに残し、サーバーは、その1枚がまだ覚醒していないことと、送られた段階がその種類の枝であることを確かめる。
覚醒した1枚は、その冒険のこのあとの戦闘から、覚醒版のカードとしてデッキに入る。

## 設計方針

### 枝の作り方

- 2つの枝は、同じスキルを違う方向へ育てる。元のスキルの使い手・属性・役割は保つ。
- 威力の数字だけを上げる枝は作らない。行う効果の組み合わせ（種類・相手・状態・回数・条件）を変える。[重複を避ける基準](combat.md#スキルの仮設定)を、元の50種類と100の段階のすべてに当てる。
- 枝の方向は、次のどれかにする。
  - 元のスキルの条件を自分で満たす（例：出血中に1.5倍のカードが、自ら出血を付ける）。
  - 対象を変える（1体を全体へ、全体を1体へ集める）。
  - 回数を変える。
  - 手札・エネルギー・守りへの働きを足す。
- コストは、元のスキルと同じか、1つ上下させる。足した働きの重さに合わせる。どの段階も0〜9に収める。

### 演出

100の段階は、それぞれ専用の演出を持つ（2026-10-24、ユーザーの指示）。
元のスキルの演出に近くてよいとされたため、元のスキルの演出を土台にし、段階ごとに色としるしを変えて重ねる。

1. **溜め**：固有スキルの1段目と同じ、カードの属性と使い手で選ぶ溜めを、段階の色で出す（[溜めの割り当て](skill-windups.md)）。
2. **先に付ける状態**：打つ前に状態を付ける段階（大地割り・崩の防御ダウンなど）は、先に相手にしるしを付けて状態を届ける。
3. **元のスキルの演出**：主な当たりから、元のスキルの演出を流す。当たるたびに、固有スキルの1段目と同じ層（足元の衝撃の輪・星・火花・照り返し）を段階の色で重ねる（[固有スキルの設計方針](skill-evolution.md#設計方針)）。
4. **増えた当たり**：元のスキルより当たりの回数が多い段階（ファイア・連の2発目など）は、増えた回の分だけ元の演出を繰り返す。
5. **足した働き**：当たりのあとに足した働き（ブロック・カードを引く・状態など）は、相手ごとにしるしを付けて届ける。

**しるし**は、段階が当たった所や届いた相手に出す、段階ごとの演出である。
次の12種類から段階ごとに1つ選び、段階の色で描く。
同じスキルの2つの枝には、色かしるしの違う組み合わせを当てる。

| しるし | 見せ方 |
|---|---|
| 白熱 | 当たった所に白熱した芯の閃光と光の十字が開く |
| 花びら | 当たった所から花びらのような粒が散り、ゆっくり落ちる |
| 盾 | 相手の足元に光の盾の輪が立ち、光の粒がせり上がる |
| 霜 | 相手の足元に霜の輪が広がり、氷のかけらが舞う |
| 毒霧 | 相手から毒の霧の粒が立ちのぼる |
| 稲妻 | 当たった所で小さな稲妻の火花が弾ける |
| 聖光 | 相手の足元から光の粒が立ちのぼり、柔らかい光で照らす |
| 疾風 | 風の筋が相手を横切って流れる |
| 地鳴り | 足元に大きな土煙の輪が広がり、画面が揺れる |
| 閃き | 使い手の頭上で星が瞬き、光の粒が使い手へ集まる |
| 連なり | 当たった相手の上で光の点が順に瞬き、つながるように見える |
| 火の粉 | 相手から火の粉が舞い上がる |

段階ごとの色としるしは、スキルの定義文書の「覚醒版」の節の「演出」の行に書く。
[スキルのデバッグルーム](screens/debug-rooms.md#スキルのデバッグルーム)の一覧の「覚醒版」のタブから、100の段階を選んで再生できる。

### 挿絵

段階ごとに専用の挿絵を作る（2026-10-24、ユーザーの指示）。
規格は元のスキルの挿絵と同じにする（64×58ドット、32色、下端16行をディザで透明。[技・カードの定義](../catalog/skills/README.md)）。
モチーフは、元のスキルの挿絵と同じ物を、枝の方向に合わせて変えた物にし、元のスキルの覚醒版だと見て分かるようにする。
各段階のモチーフは、スキルの定義文書の「覚醒版」の節に書く。

## 仮設定

数値は、遊んで確かめるための仮の値である。

### 確率

戦闘で引いたときの確率は、固有スキルの[確率の仮設定](skill-evolution.md#確率の仮設定)の半分にする。

| マス | 1回引くごとの基本の確率 |
|---|---|
| 魔物の気配 | 2% |
| 強い魔物の気配・試練の部屋 | 4% |
| 最奥の間 | 5% |

使い手のLvが推奨Lvより高いときに下げる決まりは、固有スキルと同じにする。
デッキのスキルは最大12枚で、固有スキル（最大12枚、仮データの見積もりでは8枚）より多く、1回の冒険で約2回覚醒する見積もりである。
固有スキルの覚醒を主に残すため、確率を低くした。
実測していない。

### 商会での費用

覚醒の欠片1個とルーン1,000（固有スキルの1段目と同じ）。

## 未決事項

| 決める項目 | 状態 |
|---|---|
| 売る・合成 | 覚醒した1枚を売る値段と、合成の材料にしたときの扱い。まずは元のスキルと同じにする |
| ライブラリ | 覚醒した段階を、見つけた記録として載せるか。載せるなら、スキルの個別の画面に段階を並べる |
| 編成での見せ方 | 覚醒した1枚を、スキルの一覧でどう見分けるか（名前・挿絵に加えて印を付けるか） |
| 確率の値 | 固有スキルと合わせて実測し、見直す |

## 覚醒版の一覧

各段階の方向・効果・コスト・挿絵のモチーフは、スキルの定義文書の「覚醒版」の節にある。

| スキル | 枝1 | 枝2 |
|---|---|---|
| [斬り払い](../catalog/skills/skill-slash.md#覚醒版) | 斬り払い・裂 | 斬り払い・構 |
| [ファイア](../catalog/skills/skill-fire.md#覚醒版) | ファイア・灼 | ファイア・連 |
| [アイスランス](../catalog/skills/skill-ice.md#覚醒版) | アイスランス・穿 | アイスランス・霜 |
| [サンダー](../catalog/skills/skill-thunder.md#覚醒版) | サンダー・痺 | サンダー・落雷 |
| [ヒール](../catalog/skills/skill-heal.md#覚醒版) | ヒール・癒 | ヒール・円 |
| [ガード](../catalog/skills/skill-guard.md#覚醒版) | ガード・堅 | ガード・備 |
| [旋風斬](../catalog/skills/skill-whirlwind.md#覚醒版) | 旋風斬・裂 | 旋風斬・嵐 |
| [居合一閃](../catalog/skills/skill-iai.md#覚醒版) | 居合一閃・断 | 居合一閃・燕 |
| [千刃乱舞](../catalog/skills/skill-blade-dance.md#覚醒版) | 千刃乱舞・紅 | 千刃乱舞・閃 |
| [研ぎ澄ます](../catalog/skills/skill-hone.md#覚醒版) | 研ぎ澄ます・冴 | 研ぎ澄ます・刃 |
| [シールドバッシュ](../catalog/skills/skill-shield-bash.md#覚醒版) | シールドバッシュ・震 | シールドバッシュ・砕 |
| [鎧砕き](../catalog/skills/skill-armor-break.md#覚醒版) | 鎧砕き・崩 | 鎧砕き・剥 |
| [大地割り](../catalog/skills/skill-earth-splitter.md#覚醒版) | 大地割り・崩 | 大地割り・岩陣 |
| [ギガントインパクト](../catalog/skills/skill-giant-impact.md#覚醒版) | ギガントインパクト・要塞 | ギガントインパクト・衝 |
| [健脚の一撃](../catalog/skills/skill-stride-strike.md#覚醒版) | 健脚の一撃・駆 | 健脚の一撃・踏 |
| [守護の誓い](../catalog/skills/skill-guardian-oath.md#覚醒版) | 守護の誓い・不動 | 守護の誓い・報復 |
| [鬨の声](../catalog/skills/skill-war-cry.md#覚醒版) | 鬨の声・猛 | 鬨の声・陣 |
| [火の粉](../catalog/skills/skill-embers.md#覚醒版) | 火の粉・散 | 火の粉・灯 |
| [フレイムピラー](../catalog/skills/skill-flame-pillar.md#覚醒版) | フレイムピラー・業火 | フレイムピラー・連柱 |
| [ファイアストーム](../catalog/skills/skill-fire-storm.md#覚醒版) | ファイアストーム・烈 | ファイアストーム・渦 |
| [爆炎の刻印](../catalog/skills/skill-blast-sigil.md#覚醒版) | 爆炎の刻印・連鎖 | 爆炎の刻印・即爆 |
| [メテオ](../catalog/skills/skill-meteor.md#覚醒版) | メテオ・焔 | メテオ・流星 |
| [フレイムエンチャント](../catalog/skills/skill-flame-enchant.md#覚醒版) | フレイムエンチャント・陣 | フレイムエンチャント・猛 |
| [つらら落とし](../catalog/skills/skill-icicles.md#覚醒版) | つらら落とし・凍 | つらら落とし・鋭 |
| [ブリザード](../catalog/skills/skill-blizzard.md#覚醒版) | ブリザード・凍 | ブリザード・氷壁 |
| [アイスミラー](../catalog/skills/skill-ice-mirror.md#覚醒版) | アイスミラー・双 | アイスミラー・氷華 |
| [絶対零度](../catalog/skills/skill-absolute-zero.md#覚醒版) | 絶対零度・氷河 | 絶対零度・氷葬 |
| [詠唱短縮](../catalog/skills/skill-quick-cast.md#覚醒版) | 詠唱短縮・速 | 詠唱短縮・瞬 |
| [急所突き](../catalog/skills/skill-vital-thrust.md#覚醒版) | 急所突き・穿 | 急所突き・連 |
| [毒針](../catalog/skills/skill-poison-needle.md#覚醒版) | 毒針・蝕 | 毒針・散 |
| [影縫い](../catalog/skills/skill-shadow-stitch.md#覚醒版) | 影縫い・縛 | 影縫い・影走 |
| [看破の矢](../catalog/skills/skill-insight-arrow.md#覚醒版) | 看破の矢・射抜 | 看破の矢・千里眼 |
| [アローレイン](../catalog/skills/skill-arrow-rain.md#覚醒版) | アローレイン・毒 | アローレイン・豪 |
| [シャドウスナイプ](../catalog/skills/skill-shadow-snipe.md#覚醒版) | シャドウスナイプ・必中 | シャドウスナイプ・影 |
| [ライトニングボルト](../catalog/skills/skill-lightning-bolt.md#覚醒版) | ライトニングボルト・痺 | ライトニングボルト・迅 |
| [チェインライトニング](../catalog/skills/skill-chain-lightning.md#覚醒版) | チェインライトニング・痺 | チェインライトニング・連鎖 |
| [雷雲](../catalog/skills/skill-thundercloud.md#覚醒版) | 雷雲・嵐 | 雷雲・充 |
| [雷神の槍](../catalog/skills/skill-thunder-spear.md#覚醒版) | 雷神の槍・神鳴 | 雷神の槍・轟 |
| [疾風迅雷](../catalog/skills/skill-gale.md#覚醒版) | 疾風迅雷・迅 | 疾風迅雷・雷纏 |
| [偵察](../catalog/skills/skill-scout.md#覚醒版) | 偵察・看破 | 偵察・待ち伏せ |
| [万歩の祈り](../catalog/skills/skill-step-prayer.md#覚醒版) | 万歩の祈り・清 | 万歩の祈り・守 |
| [リジェネ](../catalog/skills/skill-regen.md#覚醒版) | リジェネ・広 | リジェネ・即 |
| [リザレクション](../catalog/skills/skill-resurrection.md#覚醒版) | リザレクション・加護 | リザレクション・息吹 |
| [聖なる鉄槌](../catalog/skills/skill-holy-hammer.md#覚醒版) | 聖なる鉄槌・裁 | 聖なる鉄槌・祝 |
| [プロテクト](../catalog/skills/skill-protect.md#覚醒版) | プロテクト・清 | プロテクト・祈 |
| [浄化の光](../catalog/skills/skill-purify.md#覚醒版) | 浄化の光・聖 | 浄化の光・巡 |
| [マナの祈り](../catalog/skills/skill-mana-prayer.md#覚醒版) | マナの祈り・満 | マナの祈り・共鳴 |
| [天啓](../catalog/skills/skill-revelation.md#覚醒版) | 天啓・閃 | 天啓・導 |
| [ホーリーライト](../catalog/skills/skill-holy-light.md#覚醒版) | ホーリーライト・浄 | ホーリーライト・陽 |
| [ディバインシールド](../catalog/skills/skill-divine-shield.md#覚醒版) | ディバインシールド・聖域 | ディバインシールド・祝福 |

## 関連する仕様とデータ

[固有スキルの覚醒](skill-evolution.md)・[商会の強化](enhancement.md)・[持ち物の分類、レア度と品質](inventory.md)・[キャラクターとパーティ編成](party.md)・[戦闘システム](combat.md)・[技・カードの定義](../catalog/skills/README.md)

## 実装との対応

2026-10-24に、100の段階の定義・挿絵・演出と、戦闘での覚醒・商会での覚醒・サーバーへの保存を作った。編成でのスキルの一覧での見分け方（印など）とライブラリは未実装である。

| 対象 | 入口 |
|---|---|
| 100の段階の定義（効果・名前・コスト。使い手・属性・倍率の上限・光り方は元のスキルから引き継ぐ） | [CardSkillAwakenings](../../client/Assets/Baryonyx/Features/Combat/Runtime/CardSkillAwakenings.cs)。ID は `<スキルのID>.1`・`.2` |
| 定義の検査（元の50種類と100の段階で効果が重ならない、名前・コスト・説明文・挿絵がそろう） | [CardSkillAwakeningsTests](../../client/Assets/Baryonyx/Features/Combat/Tests/EditMode/CardSkillAwakeningsTests.cs) |
| 挿絵 | `Features/Combat/UI/Art/Card<スキルのID><枝の番号>.aseprite`（例：`CardFire1.aseprite`）。冒険者の画面などが引く絵の一覧（`PartyMockData` の `SkillArts`）に、[PartyAssets](../../client/Assets/Baryonyx/Features/Party/Editor/PartyAssets.cs) が段階のIDで入れる。展示室には画像のカテゴリで自動で載る |
| 演出 | [BattleSkillVfx.Awakened](../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleSkillVfx.Awakened.cs)。段階ごとの色としるしの表（`Looks`）と、溜め・先に付ける状態・元のスキルの演出・増えた当たり・足した働きの順に流す処理。元のスキルの演出は [BattleSkillVfx.Cards](../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleSkillVfx.Cards.cs) の `CardEffect` を使う。当たりの波は、戦闘と同じ [BattleCardPlan](../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleCardPlan.cs) で数える |
| サーバー | `skill_cards` の `awakened_branch`（[0026_skill_awakening.sql](../../server/migrations/0026_skill_awakening.sql)）。`GET /v1/party` の `skillCards[].awakenedBranch`（1・2、覚醒していなければnull）。戦闘の覚醒は `POST /v1/party/skill-cards/:cardId/awaken`（本文 `branch`。同じ枝の再送は何も変えず、別の枝は409 `already_awakened`、冒険の時間外は409 `not_in_adventure`。時間の判定は固有スキルの覚醒と同じ）。商会は `POST /v1/enhancement/awaken` の本文 `{requestId, cardId, branch}`（覚醒の欠片1個とルーン1,000、覚醒済みは409 `already_awakened`）。[party/routes](../../server/src/features/party/routes.ts)・[enhancement/routes](../../server/src/features/enhancement/routes.ts) |
| 持ち物（クライアント） | [PartySkillCard](../../client/Assets/Baryonyx/Features/Party/Runtime/PartyCards.cs) の `AwakenedBranch`・`StageId`。覚醒した1枚は覚醒版のカード（`CardSkills.Find` が `Fire.1` も引く）として、倍率をそのまま持つ。戦闘で覚醒した1枚は、マスを終えたときに [PartySkillAwakenings](../../client/Assets/Baryonyx/Features/Party/Runtime/PartySkillAwakenings.cs) が送る |
| 戦闘 | [BattleInspectView.Evolution](../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleInspectView.Evolution.cs)。デッキのカードに持っているスキルの1枚のID（`OwnedCard`）を持たせ（[AdventureParty](../../client/Assets/Baryonyx/Features/Adventure/Runtime/AdventureParty.cs)）、固有スキルと同じ予兆の抽選に載せる。確率は [AdventureEvolution](../../client/Assets/Baryonyx/Features/Adventure/Runtime/AdventureEvolution.cs) の `AwakenChance`（固有スキルの半分）。「覚醒しない」は [BattleEvolutionChoice](../../client/Assets/Baryonyx/Features/Combat/Presentation/BattleEvolutionChoice.cs) の `DeclineButton` と、演出の `EvolutionSettle` |
| 商会 | 「スキル覚醒」の一覧に、固有スキルの段階のあとへ、覚醒していない1枚ずつの2つの枝を並べる（[EnhancementTargets](../../client/Assets/Baryonyx/Features/Enhancement/Runtime/EnhancementTargets.cs) の `SkillAwakenings`、[WorkshopEnhanceView](../../client/Assets/Baryonyx/Features/Workshop/Runtime/WorkshopEnhanceView.cs)）。2行目は「付けている人｜元の名前から覚醒（元に戻せない）」 |
| 演出の確認 | [スキルのデバッグルーム](screens/debug-rooms.md#スキルのデバッグルーム)の一覧の「覚醒版」のタブ（[CardSkillLab](../../client/Assets/Baryonyx/Features/Combat/Presentation/CardSkillLab.cs)）。PlayModeテストの `CardSkillLabSceneTests` が、しるしの種類と効果の並びが一通りそろう16の段階を最後まで流す |

### 挿絵の作り方

1種類ごとに、2つの枝を左右に並べた1枚をCodex CLIの画像生成で描き、切り分けて規格に直した（2026-10-24）。

1. 依頼文に、元のスキルの名前、2つの枝の名前・挿絵のモチーフ・枝の方向（スキルの定義文書の「覚醒版」の節）、左右2区画と間の黒い帯の構図、[ドット絵の規格](../art/direction.md#ドット絵の制作規格)の固定文（`touch`・`light`・`colors`・`detail`）を入れた。
2. 元のスキルの挿絵を8倍にしたPNGを添え、画風・色の傾向・モチーフの見本にした。
3. 描いた1枚を黒い帯で左右に分け、64:58に中央を切り出し、64×58へ縮めて32色に減らした。下端16行の透け方は、今の挿絵（`CardFire`）と同じ形を写した。
4. [png_to_aseprite.lua](../../.agents/skills/pixel-art-standards/scripts/png_to_aseprite.lua) で `.aseprite` にした。

Codexは [codex_image.py](../../.agents/skills/shared/scripts/codex_image.py) の手順で呼び、推論の強さは `high` にした。
スクリプトが固定しているモデル（`gpt-6.1-sol`）は、今のChatGPTのアカウントでは使えなかった。そのため、スクリプトを手元で複製し、Codexの既定のモデル（`gpt-6-astra`）で描いた。
依頼文と描いた下絵は `output/skill-awakening-art/`（Git対象外）にある。

## 変更と判断の記録

- 2026-10-24：ユーザーの依頼「覚醒版カードを実際の戦闘で使う組み込みもお願いします。覚醒しないも選べるようにしてください。どうやってするのか考えてください。覚醒が起きる場所は戦闘と商会だけ」を受け、戦闘・商会・サーバーへの保存を作った。「覚醒しない」の見せ方（決定の左に置く、割れずに光が静まって使い手へ戻る、その戦闘ではもう光らない）は、こちらで決めた。
- 2026-10-24：ユーザーの指示「エフェクトも専用で作る。元のスキルと近しい感じでも構わない」を受け、100の段階に専用の演出を作った（[演出](#演出)）。元のスキルの演出を土台にし、段階ごとの色と12種類のしるしの組み合わせ、先に付ける状態・増えた当たり・足した働きの見せ方は、こちらで決めた。
- 2026-10-24：ユーザーの指示「挿絵はCodexの画像の作成スキルを使ってよい」を受け、100の段階の挿絵を作った（[挿絵の作り方](#挿絵の作り方)）。候補から選ぶ手順は取らず、1種類につき1回描いたものをそのまま採った。
- 2026-10-24：ユーザーの依頼「カスタムスキルも進化できるような方向性で」を受け、4つの軸（覚醒が残る単位・ツリーの形・起き方・挿絵）の案を示した。ユーザーは、1枚ずつに残る・2つの枝から選ぶ1段・固有スキルと同じ起き方を選び、挿絵は案と違って段階ごとに専用に作ること、名前も段階ごとに変えること、覚醒した1枚は元に戻せないことを決めた。「矛盾はないか」の問いに、戦闘中に戻せない選択を必ずさせる点と、起きる場面をどこまで固有スキルにそろえるかを挙げ、「覚醒しない」を足す案と、戦闘と商会だけで起こす案を示した。ユーザーの返事は「まずは仕様書に追加」で、この2つは提案のまま書いた（[未決事項](#未決事項)）。100の段階の効果・名前・挿絵のモチーフ・確率・費用は、こちらで決めた仮の値である。
