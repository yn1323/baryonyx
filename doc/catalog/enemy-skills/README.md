# 敵の技の定義

[データ索引](../README.md) / [関連仕様](../../features/combat-enemy-behavior.md) / [敵の定義](../enemies/README.md)

敵が使う技を、1技1ファイルで置く。
個別内容と決定状態は、下の索引から各定義を参照する。
定義テンプレートを使う。
IDの接頭辞は `enemy-skill-`、メタデータの `category` は `enemy-skills` とする。

## 個別データの索引

<!-- entries:start -->
| ID | 名称 | 状態 | 詳細 |
|---|---|---|---|
| enemy-skill-acorn-volley | どんぐりつぶて | 候補 | [定義](enemy-skill-acorn-volley.md) |
| enemy-skill-barnacle-crab-crusher | 大ばさみ砕き | 候補 | [定義](enemy-skill-barnacle-crab-crusher.md) |
| enemy-skill-big-ear-dodge | 耳ざとい身かわし | 候補 | [定義](enemy-skill-big-ear-dodge.md) |
| enemy-skill-bite | かみつき | 候補 | [定義](enemy-skill-bite.md) |
| enemy-skill-blind-fishman-ripple-sense | 波紋聞き | 候補 | [定義](enemy-skill-blind-fishman-ripple-sense.md) |
| enemy-skill-blood-leech-open-wound | ふさがらない噛み傷 | 候補 | [定義](enemy-skill-blood-leech-open-wound.md) |
| enemy-skill-bone-croc-blood-scent | 血の匂い | 候補 | [定義](enemy-skill-bone-croc-blood-scent.md) |
| enemy-skill-bone-croc-death-roll | 死の回転 | 候補 | [定義](enemy-skill-bone-croc-death-roll.md) |
| enemy-skill-bone-croc-swallow-whole | 丸のみ | 候補 | [定義](enemy-skill-bone-croc-swallow-whole.md) |
| enemy-skill-brace | 身を固める | 候補 | [定義](enemy-skill-brace.md) |
| enemy-skill-brass-furnace-cannon | 蒼炉の砲撃 | 候補 | [定義](enemy-skill-brass-furnace-cannon.md) |
| enemy-skill-brass-furnace-overdrive | 炉心の全開 | 候補 | [定義](enemy-skill-brass-furnace-overdrive.md) |
| enemy-skill-brass-steam-vent | 蒸気の排気 | 候補 | [定義](enemy-skill-brass-steam-vent.md) |
| enemy-skill-cactus-warrior-thousand-needles | 針千本 | 候補 | [定義](enemy-skill-cactus-warrior-thousand-needles.md) |
| enemy-skill-call-ally | 仲間を呼ぶ | 候補 | [定義](enemy-skill-call-ally.md) |
| enemy-skill-candelabra-spread-fire | 延焼 | 候補 | [定義](enemy-skill-candelabra-spread-fire.md) |
| enemy-skill-coin-pouch-scamper | すたこら逃げ | 候補 | [定義](enemy-skill-coin-pouch-scamper.md) |
| enemy-skill-crystal-hermit-resonance | 水晶の共鳴 | 候補 | [定義](enemy-skill-crystal-hermit-resonance.md) |
| enemy-skill-crystal-horn-glint | 水晶角の照り返し | 候補 | [定義](enemy-skill-crystal-horn-glint.md) |
| enemy-skill-dew-sip | 朝露すすり | 候補 | [定義](enemy-skill-dew-sip.md) |
| enemy-skill-dew-veil | 露の膜 | 候補 | [定義](enemy-skill-dew-veil.md) |
| enemy-skill-double-thrust | 二段突き | 候補 | [定義](enemy-skill-double-thrust.md) |
| enemy-skill-drain | 吸い取り | 候補 | [定義](enemy-skill-drain.md) |
| enemy-skill-earth-fist | 大地の拳 | 候補 | [定義](enemy-skill-earth-fist.md) |
| enemy-skill-fire-breath | 火の息 | 候補 | [定義](enemy-skill-fire-breath.md) |
| enemy-skill-fire-lizard-hot-scales | 燃えるうろこ | 候補 | [定義](enemy-skill-fire-lizard-hot-scales.md) |
| enemy-skill-flame-bolt | 火の玉 | 候補 | [定義](enemy-skill-flame-bolt.md) |
| enemy-skill-flame-tail-feather-drop | 火の尾羽落とし | 候補 | [定義](enemy-skill-flame-tail-feather-drop.md) |
| enemy-skill-flame-tail-updraft | 熱風の羽ばたき | 候補 | [定義](enemy-skill-flame-tail-updraft.md) |
| enemy-skill-forest-blessing | 森の恵み | 候補 | [定義](enemy-skill-forest-blessing.md) |
| enemy-skill-forest-hammer | 森羅の鉄槌 | 候補 | [定義](enemy-skill-forest-hammer.md) |
| enemy-skill-frog-king-chorus | 沼の大合唱 | 候補 | [定義](enemy-skill-frog-king-chorus.md) |
| enemy-skill-frog-king-tongue-snatch | 舌のかすめ取り | 候補 | [定義](enemy-skill-frog-king-tongue-snatch.md) |
| enemy-skill-frost-ape-avalanche | 雪崩おとし | 候補 | [定義](enemy-skill-frost-ape-avalanche.md) |
| enemy-skill-frost-ape-rime-coat | 霜の鎧毛 | 候補 | [定義](enemy-skill-frost-ape-rime-coat.md) |
| enemy-skill-frost-breath | 冷たい息 | 候補 | [定義](enemy-skill-frost-breath.md) |
| enemy-skill-gargoyle-dive | 屋根からの急降下 | 候補 | [定義](enemy-skill-gargoyle-dive.md) |
| enemy-skill-glare | にらみつけ | 候補 | [定義](enemy-skill-glare.md) |
| enemy-skill-glow-worm-snare | 光の釣り糸 | 候補 | [定義](enemy-skill-glow-worm-snare.md) |
| enemy-skill-grimoire-recital | 頁の詠唱 | 候補 | [定義](enemy-skill-grimoire-recital.md) |
| enemy-skill-heavy-blow | 渾身の一撃 | 候補 | [定義](enemy-skill-heavy-blow.md) |
| enemy-skill-hollow-armor-guard | 番兵の盾 | 候補 | [定義](enemy-skill-hollow-armor-guard.md) |
| enemy-skill-hollow-armor-scatter | 空ろの鎧ばらし | 候補 | [定義](enemy-skill-hollow-armor-scatter.md) |
| enemy-skill-hourglass-sprite-rewind | 時の巻き戻し | 候補 | [定義](enemy-skill-hourglass-sprite-rewind.md) |
| enemy-skill-howl | 遠吠え | 候補 | [定義](enemy-skill-howl.md) |
| enemy-skill-ice-shard | 氷のつぶて | 候補 | [定義](enemy-skill-ice-shard.md) |
| enemy-skill-icicle-tail-pin | つらら刺し | 候補 | [定義](enemy-skill-icicle-tail-pin.md) |
| enemy-skill-jelly-phantom-drift | すり抜け | 候補 | [定義](enemy-skill-jelly-phantom-drift.md) |
| enemy-skill-lava-slime-crust-burst | 殻はじけ | 候補 | [定義](enemy-skill-lava-slime-crust-burst.md) |
| enemy-skill-lich-crown-awaken | 王冠の覚醒 | 候補 | [定義](enemy-skill-lich-crown-awaken.md) |
| enemy-skill-lich-decay-curse | 朽ちの呪い | 候補 | [定義](enemy-skill-lich-decay-curse.md) |
| enemy-skill-lich-requiem-chant | 葬送の詠唱 | 候補 | [定義](enemy-skill-lich-requiem-chant.md) |
| enemy-skill-lurk | 草陰に伏せる | 候補 | [定義](enemy-skill-lurk.md) |
| enemy-skill-mammoth-snow-crush | 雪山のしかかり | 候補 | [定義](enemy-skill-mammoth-snow-crush.md) |
| enemy-skill-mammoth-snowdrift | 降り積もる雪 | 候補 | [定義](enemy-skill-mammoth-snowdrift.md) |
| enemy-skill-mend | 傷ふさぎ | 候補 | [定義](enemy-skill-mend.md) |
| enemy-skill-mimic-card-pilfer | 欲張りな舌 | 候補 | [定義](enemy-skill-mimic-card-pilfer.md) |
| enemy-skill-mist-dragon-fog-veil | 霧隠れ | 候補 | [定義](enemy-skill-mist-dragon-fog-veil.md) |
| enemy-skill-mist-dragon-maze-fog | 迷い路の霧 | 候補 | [定義](enemy-skill-mist-dragon-maze-fog.md) |
| enemy-skill-mist-dragon-whiteout-breath | 白霧の大息吹 | 候補 | [定義](enemy-skill-mist-dragon-whiteout-breath.md) |
| enemy-skill-mud-golem-mud-patch | 泥の継ぎ当て | 候補 | [定義](enemy-skill-mud-golem-mud-patch.md) |
| enemy-skill-nectar-lure | 甘い蜜の香り | 候補 | [定義](enemy-skill-nectar-lure.md) |
| enemy-skill-obsidian-meltdown | 溶岩のあふれ | 候補 | [定義](enemy-skill-obsidian-meltdown.md) |
| enemy-skill-obsidian-shard-storm | 黒曜の破片嵐 | 候補 | [定義](enemy-skill-obsidian-shard-storm.md) |
| enemy-skill-octopus-eight-arm-flurry | 八腕の乱れ打ち | 候補 | [定義](enemy-skill-octopus-eight-arm-flurry.md) |
| enemy-skill-octopus-ink-cloud | 墨吐き | 候補 | [定義](enemy-skill-octopus-ink-cloud.md) |
| enemy-skill-octopus-tentacle-grab | 絡め取り | 候補 | [定義](enemy-skill-octopus-tentacle-grab.md) |
| enemy-skill-ore-mole-upheave | 地中からの突き上げ | 候補 | [定義](enemy-skill-ore-mole-upheave.md) |
| enemy-skill-penguin-spear-wall | 槍ぶすま | 候補 | [定義](enemy-skill-penguin-spear-wall.md) |
| enemy-skill-pirate-dirty-trip | 卑怯な足払い | 候補 | [定義](enemy-skill-pirate-dirty-trip.md) |
| enemy-skill-power-up | 力を溜める | 候補 | [定義](enemy-skill-power-up.md) |
| enemy-skill-raven-mimic-echo | 鴉のものまね | 候補 | [定義](enemy-skill-raven-mimic-echo.md) |
| enemy-skill-rend | 引き裂き | 候補 | [定義](enemy-skill-rend.md) |
| enemy-skill-rock-troll-boulder-toss | 肩石投げ | 候補 | [定義](enemy-skill-rock-troll-boulder-toss.md) |
| enemy-skill-rock-troll-cave-in | 洞窟崩し | 候補 | [定義](enemy-skill-rock-troll-cave-in.md) |
| enemy-skill-rock-troll-regrow | 岩肌の再生 | 候補 | [定義](enemy-skill-rock-troll-regrow.md) |
| enemy-skill-root-bind | 根縛り | 候補 | [定義](enemy-skill-root-bind.md) |
| enemy-skill-sand-scorpion-venom-surge | 毒まわし | 候補 | [定義](enemy-skill-sand-scorpion-venom-surge.md) |
| enemy-skill-sand-shark-frenzy | 狩りの狂乱 | 候補 | [定義](enemy-skill-sand-shark-frenzy.md) |
| enemy-skill-sand-shark-sand-wave | 砂の大波 | 候補 | [定義](enemy-skill-sand-shark-sand-wave.md) |
| enemy-skill-scarecrow-field-watch | 畑の見張り番 | 候補 | [定義](enemy-skill-scarecrow-field-watch.md) |
| enemy-skill-scarecrow-seed-spit | 種つぶて | 候補 | [定義](enemy-skill-scarecrow-seed-spit.md) |
| enemy-skill-screech | 金切り声 | 候補 | [定義](enemy-skill-screech.md) |
| enemy-skill-shatter | 打ち砕き | 候補 | [定義](enemy-skill-shatter.md) |
| enemy-skill-shock | 電撃 | 候補 | [定義](enemy-skill-shock.md) |
| enemy-skill-sigil-awaken | 刻印の目覚め | 候補 | [定義](enemy-skill-sigil-awaken.md) |
| enemy-skill-signpost-wayfinding | 道案内 | 候補 | [定義](enemy-skill-signpost-wayfinding.md) |
| enemy-skill-skeleton-reassemble | 組み直し | 候補 | [定義](enemy-skill-skeleton-reassemble.md) |
| enemy-skill-slash | 斬りつけ | 候補 | [定義](enemy-skill-slash.md) |
| enemy-skill-snowball-gather | ころがり太り | 候補 | [定義](enemy-skill-snowball-gather.md) |
| enemy-skill-spark-wave | 放電 | 候補 | [定義](enemy-skill-spark-wave.md) |
| enemy-skill-spore-burst | 胞子ばらまき | 候補 | [定義](enemy-skill-spore-burst.md) |
| enemy-skill-spore-sac-bloom | 胞子の芽吹き | 候補 | [定義](enemy-skill-spore-sac-bloom.md) |
| enemy-skill-stump-tusk-toss | 牙のかち上げ | 候補 | [定義](enemy-skill-stump-tusk-toss.md) |
| enemy-skill-sweep | なぎ払い | 候補 | [定義](enemy-skill-sweep.md) |
| enemy-skill-tackle | たいあたり | 候補 | [定義](enemy-skill-tackle.md) |
| enemy-skill-thorn-coil | とげのとぐろ | 候補 | [定義](enemy-skill-thorn-coil.md) |
| enemy-skill-throat-lunge | 喉笛狙い | 候補 | [定義](enemy-skill-throat-lunge.md) |
| enemy-skill-tremor | 地響き | 候補 | [定義](enemy-skill-tremor.md) |
| enemy-skill-urn-gulp | 壺の吸い込み | 候補 | [定義](enemy-skill-urn-gulp.md) |
| enemy-skill-venom-sting | 毒針 | 候補 | [定義](enemy-skill-venom-sting.md) |
| enemy-skill-will-o-wisp-lure | 迷い火の誘い | 候補 | [定義](enemy-skill-will-o-wisp-lure.md) |
| enemy-skill-wrapped-jackal-tomb-call | 墓守の呼び覚まし | 候補 | [定義](enemy-skill-wrapped-jackal-tomb-call.md) |
<!-- entries:end -->

## 共通技と固有技

技には、**共通技**と**固有技**の2つの区分がある。
共通技は、名前・効果・演出の形と時間を複数の敵で共有する技である。
たいあたりや火の息のように、体の作りが似た敵なら同じように使える。
演出の色・材質・形の細部だけは、使う敵に合わせて変え、その違いを技の定義に書く。
固有技は、1体の敵だけが使う技で、その敵の体・習性・生息地から出てくる。

新しい敵を作るときは、先に共通技から合うものを選び、その敵らしさは固有技で出す。
同じ効果で数値だけが違う技は作らない。
敵ごとの強さの違いは、ステータスとLvで表す（[レベルとステータス](../../features/progression.md#レベルとステータス)）。
技の数は、敵の大きさに合わせて小3つ・中5つ・大8つを目安にする（2026-10-04、ユーザーの依頼）。
攻撃だけで揃えず、防御・強化・弱体・回復・呼び出しを混ぜる。

## 記入する項目

| 項目 | 書き方 |
|---|---|
| 区分 | 共通技か固有技か。固有技は持ち主の敵へリンクする |
| 種類・対象 | 攻撃、防御、強化、弱体、回復、呼び出し、大技のどれかと、対象（味方単体、味方全体、自分、敵全体など） |
| 物理・魔法と属性 | ダメージを与える技は、物理か魔法かと属性を書く。味方は物理の技を物防で、魔法の技を属防で受ける（[使い道の候補](../../features/party.md#ステータスの項目)） |
| 効果 | 味方のカードと同じく、使う敵のステータスの割合で書く（[威力の決め方](../../features/combat.md#威力の決め方)）。回復は属攻、ブロックは物防の割合で仮に書く。状態は[カードの状態の仮設定](../../features/combat.md#カードの状態の仮設定)の名前を使い、敵が味方に付けるときの量と新しい状態は[敵の行動の仮設定](../../features/combat-enemy-behavior.md)に従う |
| 予告 | 攻撃、全体攻撃、防御、強化、弱体、回復、呼び出し、溜めの8種類。1回の行動で複数のことをする技は、予告を並べて出す（Slay the Spireの[予告の出し方](https://sts2.untapped.gg/guides/how-to-read-enemy-intent)を参考にした。2026-10-04確認） |
| 使う敵・似合う敵 | 今使っている敵のID。共通技には、新しい敵へ付けるときの目安も書く |
| 演出 | 感覚の言葉、本体の動き、溜め・発生・余韻の3拍、色、格を書く。敵の絵は攻撃モーションのない1枚絵（[ドット絵の制作規格](../../art/direction.md#画面と寸法)）のため、本体の動きは絵の移動・傾き・伸び縮み・色の変化で表す。形は[エフェクトの描き方](../../art/direction.md#エフェクトの描き方)に従って計算で描く |
| 演出の格 | 小・中・大の3段階。敵の技は、プレイヤーが「だれがどれだけ受けたか」を読み取れるよう、味方のカードの演出より控えめにする。大はボスの大技と激化の合図に限る。どの格でも、標的・開示した弱点・予告を隠さない（[戦闘中の情報の優先順位](../../features/combat.md#戦闘中の情報の優先順位)） |

## 追加方法

[定義テンプレート](../templates/definition.md)をこの分類にコピーし、1技1ファイルで記入する。
既存の技と効果が重ならないことを、この索引と各定義で確かめる。
使う敵の定義の「技」の表にも、技へのリンクを加える。
追加後は[索引を再生成](../../tools/README.md)する。
