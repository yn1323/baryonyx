---
id: art-hd2d-stage-textures
type: specification
status: 一部確定
updated: 2026-11-02
---

# 3Dの舞台：小物とテクスチャ

[3Dの舞台（HD-2D）](../hd2d-stage.md) / 根拠：[アート方針](../direction.md)

## 小物と汚し

オクトパストラベラーの開発者は、大きな画面ではスプライトが寂しく見えたためタイルの種類と色を増やし、ドット絵のゲームに見えるぎりぎりの密度で絵を作ったと述べている（[参考資料](../../../.agents/skills/shared/hd2d-research-sources.md#hd-2dのライティングカメラポストプロセス2回目)）。
2026-10-03、ユーザーの依頼で、3つの舞台に次の4種類の物を足した。

| 種類 | 作り方 | 狙い |
|---|---|---|
| 小物 | 切り抜きの板（`Flat`）と、Homeの木箱だけは箱（`Box`）。法線マップ付きの `Simple Lit` | 野営の荷物、古い像、戦いの跡など、その場所で起きたことを見せる |
| 草の房 | 草用のシェーダーの切り抜きの板を、数m四方ごとに1つのメッシュへまとめる（`Boards`）。草地に散らし、小物と木の足元にも2〜3本ずつ添える | 小物を地面に置いた板ではなく、草の中から生えた物に見せる |
| 地面の汚し | 地面から0.4cm上に寝かせた切り抜きの板（`Decals`）。回転は90度単位にとどめ、地面のドットの格子とそろえる | 落ち葉・苔・砂で、繰り返すタイルの模様を崩す |
| 足元の陰 | 地面に立つ切り抜きの板の足元に敷く柔らかい楕円（`Shades`、接地影のシェーダー）。幅は板の0.75倍、奥行きは幅の0.35倍（0.5〜1.6m） | 空の光が遮られる足元を暗くし、板が地面に線で接して見えないようにする |

| 画面 | 小物 | 草の房・汚し |
|---|---|---|
| Home | テントの横に木箱3つ（上の1つは傾ける）、奥の丸太の前に樽と麻袋と寝袋、右奥に薪の山と斧の刺さった切り株、空き地の縁に切り株と苔むした岩、木の根元と茂みの前にシダとキノコ、焚き火の下に石を並べた炉 | 空き地の外の草地に草の房、空き地の周りに落ち葉 |
| Top | 門の台の左右に騎士の石像、手前の松明の外側に裾の裂けた旗（松明の火で照らす）、道の両脇に崩れた石垣・がれき・3本の石碑 | 道の両端に沿って草の房と苔、草地にも草の房と苔 |
| 戦闘 | 石舞台の奥の縁に獣の骨・地面に刺さった古い剣と槍・ケルン、右の縁に砂岩のがれきと枯れた低木、左の縁に砂岩のがれき | 石舞台の外と石の割れ目に枯れ草、縁に吹き寄せた砂 |

置き方の決まりは次のとおりである。

- 小物・草の房は、キャラの足元から0.8m以上離す。足元はシーンのUIの配置から求めた値（Homeの4人と焚き火、戦闘の7体）を使い、EditModeテストで確かめる。地面の汚しと足元の陰は、焚き火の炉のように足元にあってよい。
- 手前のUIに隠れる場所には置かない。戦闘の手札・コスト・ターン終了のボタンの下、Topのタイトルの文字の後ろには、背の高い物を置かない。
- 戦闘は、キャラと演出を読みやすくするため、石舞台の中央には割れ目の小さな枯れ草だけを置く。砂は風が落とす縁と立石の周りにだけ敷く。中央に置いた橙の砂は、塗料のしみに見えた。
- 足元の陰は、くっきり見える距離の物にだけ付ける（Homeは奥行き13m、Topは20m、戦闘は18mまで）。陰は半透明で、舞台のレンズがぼかしたあとに描くため、遠くでは、ぼけた地面の上にくっきりした細い線として残った。
- 小物は、主光源の影に入らない場所を選ぶ。Homeの奥の帯（奥行き約11.6〜14m）は左の大木の枝葉の影に入り、木箱が黒い箱に見えた。暗い色で描かれた木箱と荷物は、マテリアルの色で明るくした（色の成分ごとに、木箱は1.35〜1.75倍、荷物は1.12〜1.3倍）。

## テクスチャ

| 項目 | 規格 |
|---|---|
| 作り方 | Codex CLIの画像生成で描き、面積平均で目的の大きさへ縮めて格子を作り直し、32色へ減らして `.aseprite` にする |
| 置き場所 | 舞台ごとに `Shared/Art/Stages/<舞台>/`。生成の依頼文は [client/ArtSource/Stages/prompts.json](../../../client/ArtSource/Stages/prompts.json) に記録する |
| 描き方 | [ドット絵の制作規格](../direction.md#ドット絵の制作規格)のタッチ・色数・ディザと、3Dの舞台のテクスチャの色味・輪郭に従う。ゲーム側で光と影を付けるため、セルアウトの輪郭と内部線、輪郭の内側のアンチエイリアスは求めず、強い光源の向き・投げ影・ハイライト・周辺減光も描き込まず、石の段差や葉・幹の凹凸に左上からの控えめな陰影だけを付ける。実物を観察したような自然な形と質感（風化・汚れ・欠け・木目・錆・苔）にし、記号的な形にしない。依頼文の固定の段落は [stage_requests.py](../../../.agents/skills/hd2d-stage-set/scripts/stage_requests.py) が持つ |
| ドットの細かさ | キャラが立つ奥行きで、舞台の1ドットがキャラの1ドット（約4px）に近くなるようにする。Homeでは約45ドット/m。小物も同じく、HomeとTopは45ドット/m、戦闘は38ドット/mにする。ただし、細い葉や刃が縮小でつぶれる草の房（75ドット/m）、シダ（60ドット/m）、枯れた低木と剣（65ドット/m）、木箱（60ドット/m）は細かくする |
| 床・壁 | 真上または正面から見た遠近のないシームレスなタイル。128×128で、床は1枚4m、壁は1枚2.5m、戦闘の草地は1枚3m、Homeの草地は1枚2.75m |
| 切り抜き | 周りと穴をマゼンタ（#FF00FF）で塗らせ、透明にする。門160×200、旗48×96、松明16×56、崩れた柱88×176、古木200×256、Homeの大木216×320、茂み160×70、枝葉336×120、丸太120×37、テント116×60、空き地384×384（9.6m×8.8m） |
| 小物（2026-10-03） | 物の範囲を測り、縦横比を保って縮める。Homeの荷物90×50、薪90×38、シダ108×30、草の房60×27、岩68×31、切り株45×37、木箱の面48×48（0.8m四方の面いっぱいの絵）。Topの石像39×117、がれき135×36、石碑81×49、石垣162×37、草の房60×31、旗48×135。戦闘のがれき84×27、剣と槍84×75、骨84×36、低木98×43、枯れ草60×36、ケルン33×46 |
| 地面の汚し | 真上から見た切り抜き。周りが不規則にまばらになって終わるよう描かせる。落ち葉72×61、焚き火の炉45×42、苔63×62、砂68×68。砂は縁のマゼンタのにじみを消すため、縮める前に縁を10画素削る（`--erode 10`） |
| 法線マップ | 光を受ける `Simple Lit` の面と小物に付ける。テクスチャの隣に `〈名前〉Normal.png` として、舞台を作るたびに絵から計算し直す（[ReliefMap](../../../client/Assets/Baryonyx/Shared/VFX/HD2D/Editor/Hd2dStageKit.cs)）。明るいドットを高く、暗いドットを低くし、切り抜きは透明な縁から内側3ドットで丸く盛り上げる。Simple Litは法線マップの強さの値を無視する（URP 17.6の `BUMP_SCALE_NOT_SUPPORTED`）ため、強さは画像に焼き込む。1ドット1法線のまま、Point・ミップマップなし・非圧縮で読み込む。描いた遠景・自分で光る遠景・草と葉には付けない |
| 1枚絵 | 石舞台の床384×384（17m四方）、森の遠景768×320（戦闘は15.6m×6.5m、Homeは19.2m×8mを3枚並べ、中央を左右反転）、高原の石舞台512×512（17m四方）、夕暮れの遠景768×320（19.2m×8mを3枚並べ、中央を左右反転。下を地面に沈め、メサを地平線に低く見せる） |
| 描いた遠景 | 夜空768×256（120m×40m、74m先に2枚並べる）、山並み768×162（48m×10.1m、58m先に3枚並べる）。夜空は減色のときに明るい画素へ8色を別に確保し、月の色と模様を残す（`stage_texture.py --bright-colors 8 --bright-threshold 200`） |
| 戦闘の背景（2026-10-03） | 地面のタイル128×128（1枚3m）、床512×477〜522（17m）、描いた遠景768×320。切り抜きは置く大きさとドットの細かさから寸法を決める。額縁の大物は20ドット/m（高さ6.5〜9m）、奥の物とその場の物は38ドット/m（祠・像は45）、小物は45ドット/m、草の房は75ドット/m（シダと葦は60）、地面の汚しは38ドット/m（幅2m）、光源の物は45ドット/m。暗い色が大半を占める描いた遠景（坑道・水晶の洞窟・火山・夜の墓地・古城）は、明るい画素に8色を別に確保する。依頼文は、時間帯を問わず昼の明るさで描かせる固定の段落（`stage_requests.py` の `plain`）で作った |
| 読み込み | フィルターはPoint、ミップマップなし。タイルはRepeat、切り抜きと1枚絵はClamp |
| UV | 床・壁・箱は「1枚のタイルが何m」で頂点のUVを作り、どの面でもドットの大きさをそろえる |

| ファイル | 内容 |
|---|---|
| `DungeonHall/DungeonFloor.aseprite` | 青灰色の石畳 |
| `DungeonHall/DungeonWall.aseprite` | 切り石の壁。柱と段にも使う |
| `DungeonHall/DungeonArch.aseprite` | 奥の門の石のアーチ |
| `DungeonHall/DungeonBanner.aseprite` | 門の左右の青い旗 |
| `DungeonHall/DungeonTorch.aseprite` | 柱の松明の金具。炎は計算で描く |
| `ForestRuins/ForestStageFloor.aseprite` | 文様を刻んだ円形の石舞台 |
| `ForestRuins/ForestGround.aseprite` | 石舞台の周りの草地 |
| `ForestRuins/ForestBackdrop.aseprite` | 崩れたアーチと夜の森の遠景 |
| `ForestRuins/ForestRuinPillar.aseprite` | 崩れた柱。かがり火の台にも使う |
| `ForestRuins/ForestTree.aseprite` | 画面の両端の古木 |
| `ForestGlade/GladeGrass.aseprite` | 昼の森の草地 |
| `ForestGlade/GladeClearing.aseprite` | 野営地の踏み固められた空き地 |
| `ForestGlade/GladeBackdrop.aseprite` | 奥へ青緑にかすむ昼の森の遠景 |
| `ForestGlade/GladeTree.aseprite` | 空き地の両脇と奥の大木 |
| `ForestGlade/GladeBush.aseprite` | 空き地の周りの下草の茂み |
| `ForestGlade/GladeCanopy.aseprite` | 画面の上端に垂れる枝葉 |
| `ForestGlade/GladeLog.aseprite` | 焚き火のそばの丸太 |
| `ForestGlade/GladeTent.aseprite` | 野営地のテント |
| `StarlitGate/StarlitSky.aseprite` | 天の川と満月の夜空（描いた遠景） |
| `StarlitGate/StarlitMountains.aseprite` | 月夜の雪の山並み（描いた遠景） |
| `StarlitGate/StarlitGround.aseprite` | 山の上の岩まじりの草地 |
| `StarlitGate/StarlitPath.aseprite` | 石畳の道。段と門の台にも使う |
| `StarlitGate/StarlitArch.aseprite` | 古代の石の門 |
| `StarlitGate/StarlitCliff.aseprite` | 門の左右の崖 |
| `StarlitGate/StarlitRocks.aseprite` | 苔むした岩とシダ |
| `StarlitGate/StarlitTorchPost.aseprite` | 石の松明台。炎は計算で描く |
| `DuskHighland/DuskGround.aseprite` | ひび割れた黄土色の地面と枯れ草 |
| `DuskHighland/DuskArena.aseprite` | 円形の砂岩の石舞台 |
| `DuskHighland/DuskBackdrop.aseprite` | 遠いメサと夕焼けの空（描いた遠景） |
| `DuskHighland/DuskMenhir.aseprite` | 環状列石の立石 |
| `DuskHighland/DuskSpire.aseprite` | 画面の両端の岩の尖塔 |
| `DuskHighland/DuskTree.aseprite` | 枯れ木 |
| `DuskHighland/DuskBrazier.aseprite` | 鉄のかがり火台。炎は計算で描く |
| `ForestGlade/GladeCrate.aseprite` | 鉄の金具で留めた木箱の1面。箱の全部の面に貼る |
| `ForestGlade/GladeSupplies.aseprite` | 樽・麻袋・寝袋・背負い袋・ロープ |
| `ForestGlade/GladeFirewood.aseprite` | 薪の山と斧の刺さった切り株 |
| `ForestGlade/GladeFerns.aseprite` | 木の根元のシダとキノコと青い花 |
| `ForestGlade/GladeTuft.aseprite` | 野の花の混じる草の房 |
| `ForestGlade/GladeRocks.aseprite` | 苔むした丸い岩 |
| `ForestGlade/GladeStump.aseprite` | 古い切り株 |
| `ForestGlade/GladeLitter.aseprite` | 落ち葉・小枝・どんぐり・小石（地面の汚し） |
| `ForestGlade/GladeFirePit.aseprite` | 煤けた石の炉と灰（地面の汚し） |
| `StarlitGate/StarlitStatue.aseprite` | 剣を突き立てた騎士の石像 |
| `StarlitGate/StarlitBanner.aseprite` | 柱に掛かった裾の裂けた青い旗 |
| `StarlitGate/StarlitWall.aseprite` | 崩れかけた低い石垣 |
| `StarlitGate/StarlitRubble.aseprite` | 倒れた円柱と切り石のがれき |
| `StarlitGate/StarlitStele.aseprite` | 傾いて立つ3本の石碑 |
| `StarlitGate/StarlitTuft.aseprite` | 白と淡い青の花の混じる山の草の房 |
| `StarlitGate/StarlitMoss.aseprite` | 苔と小石と雑草のまだら（地面の汚し） |
| `DuskHighland/DuskBones.aseprite` | 砂に半分埋もれた獣の頭骨と肋骨 |
| `DuskHighland/DuskWeapons.aseprite` | 地面に刺さった錆びた剣と槍 |
| `DuskHighland/DuskCairn.aseprite` | 平たい石を積んだケルン |
| `DuskHighland/DuskRubble.aseprite` | 砂岩の切り石のがれき |
| `DuskHighland/DuskShrub.aseprite` | 乾いた低木 |
| `DuskHighland/DuskTuft.aseprite` | 枯れ草の房 |
| `DuskHighland/DuskSand.aseprite` | 吹き寄せた砂と小石（地面の汚し） |

戦闘の背景の10種類は、舞台ごとのフォルダーに同じ役割のテクスチャを置く。
名前は `<接頭辞><役割>.aseprite` で、役割は地面のタイル（`Ground`）、床（`Floor`。古城は `Carpet`）、描いた遠景（`Backdrop`）、汚し（`Litter`。火山は `Cracks`）と、下の表の物である。

| フォルダー（接頭辞） | 額縁の大物 | 奥の物 | その場で起きたことを見せる物 | 小物 | 草の房 | 光源の物 |
|---|---|---|---|---|---|---|
| `MeadowRoad`（`Meadow`） | `Tree` 広葉樹 | `Fence` 柵と道しるべ | `Cart` 壊れた荷車と荷物 | `Stones` 岩と切り株 | `Tuft` 野の花の混じる草 | なし |
| `MistyWoods`（`Woods`） | `Trunk` 苔むした巨木の幹 | `Boulder` 苔むした岩と倒木 | `Shrine` 石の祠 | `Mushrooms` 切り株とキノコ | `Fern` シダ | なし |
| `MineTunnel`（`Mine`） | `Wall` 岩壁と木の支柱 | `Support` 支柱の枠 | `Cart` 鉱石のトロッコと道具 | `Ore` 岩と鉱石 | なし | `Lantern` ランタンの柱 |
| `CrystalCavern`（`Crystal`） | `Spire` 水晶の柱の群れ | `Cluster` 水晶の塊 | `Statue` 水晶に飲まれた石像 | `Shards` 水晶のかけら | なし | なし |
| `RockyShore`（`Shore`） | `Stack` 海食柱 | `Rocks` 岩と流木 | `Wreck` 難破船 | `Pebbles` 岩と貝 | `Grass` 浜の草 | なし |
| `MistySwamp`（`Swamp`） | `Tree` ねじれた枯れ木 | `Stump` 朽ちた切り株と杭 | `Boat` 沈みかけた小舟 | `Shrooms` 光るキノコ | `Reeds` 葦 | なし |
| `SnowField`（`Snow`） | `Pine` 雪の積もったモミ | `Rocks` 雪をかぶった岩と氷 | `Sled` 旅人のそりと荷物 | `Stones` 雪から出た岩 | `Grass` 霜の付いた枯れ草 | なし |
| `VolcanoCrater`（`Volcano`） | `Spire` 玄武岩の岩山 | `Rocks` ひびの光る岩 | `Bones` 竜の頭骨と黒曜石 | `Embers` 小さな溶岩石 | なし | なし |
| `MoonlitGraveyard`（`Grave`） | `Tree` 枯れた大木 | `Stones` 墓石の並び | `Angel` 崩れた天使の像 | `Crosses` 木の十字架と石 | `Grass` 枯れかけた草 | `Lantern` 鉄の街灯 |
| `CastleHall`（`Castle`） | `Pillar` 旗の掛かった石の柱 | `Armor` 甲冑の飾り | `Rubble` 崩れた柱と武具 | `Debris` 石材のがれき | なし | `Brazier` 鉄の燭台 |
