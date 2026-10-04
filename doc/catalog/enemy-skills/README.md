# 敵の技の定義

[データ索引](../README.md) / [関連仕様](../../features/combat.md#敵の行動の仮設定) / [敵の定義](../enemies/README.md)

敵が使う技を、1技1ファイルで置く。
個別内容と決定状態は、下の索引から各定義を参照する。
定義テンプレートを使う。
IDの接頭辞は `enemy-skill-`、メタデータの `category` は `enemy-skills` とする。

## 個別データの索引

<!-- entries:start -->
| ID | 名称 | 状態 | 詳細 |
|---|---|---|---|
| enemy-skill-bite | かみつき | 候補 | [定義](enemy-skill-bite.md) |
| enemy-skill-brace | 身を固める | 候補 | [定義](enemy-skill-brace.md) |
| enemy-skill-call-ally | 仲間を呼ぶ | 候補 | [定義](enemy-skill-call-ally.md) |
| enemy-skill-dew-sip | 朝露すすり | 候補 | [定義](enemy-skill-dew-sip.md) |
| enemy-skill-earth-fist | 大地の拳 | 候補 | [定義](enemy-skill-earth-fist.md) |
| enemy-skill-fire-breath | 火の息 | 候補 | [定義](enemy-skill-fire-breath.md) |
| enemy-skill-forest-blessing | 森の恵み | 候補 | [定義](enemy-skill-forest-blessing.md) |
| enemy-skill-forest-hammer | 森羅の鉄槌 | 候補 | [定義](enemy-skill-forest-hammer.md) |
| enemy-skill-frost-breath | 冷たい息 | 候補 | [定義](enemy-skill-frost-breath.md) |
| enemy-skill-howl | 遠吠え | 候補 | [定義](enemy-skill-howl.md) |
| enemy-skill-lurk | 草陰に伏せる | 候補 | [定義](enemy-skill-lurk.md) |
| enemy-skill-power-up | 力を溜める | 候補 | [定義](enemy-skill-power-up.md) |
| enemy-skill-rend | 引き裂き | 候補 | [定義](enemy-skill-rend.md) |
| enemy-skill-root-bind | 根縛り | 候補 | [定義](enemy-skill-root-bind.md) |
| enemy-skill-shock | 電撃 | 候補 | [定義](enemy-skill-shock.md) |
| enemy-skill-sigil-awaken | 刻印の目覚め | 候補 | [定義](enemy-skill-sigil-awaken.md) |
| enemy-skill-spore-burst | 胞子ばらまき | 候補 | [定義](enemy-skill-spore-burst.md) |
| enemy-skill-tackle | たいあたり | 候補 | [定義](enemy-skill-tackle.md) |
| enemy-skill-throat-lunge | 喉笛狙い | 候補 | [定義](enemy-skill-throat-lunge.md) |
| enemy-skill-tremor | 地響き | 候補 | [定義](enemy-skill-tremor.md) |
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
| 効果 | 味方のカードと同じく、使う敵のステータスの割合で書く（[威力の決め方](../../features/combat.md#威力の決め方)）。回復は属攻、ブロックは物防の割合で仮に書く。状態は[カードの状態の仮設定](../../features/combat.md#カードの状態の仮設定)の名前を使い、敵が味方に付けるときの量と新しい状態は[敵の行動の仮設定](../../features/combat.md#敵の行動の仮設定)に従う |
| 予告 | 攻撃、全体攻撃、防御、強化、弱体、回復、呼び出し、溜めの8種類。1回の行動で複数のことをする技は、予告を並べて出す（Slay the Spireの[予告の出し方](https://sts2.untapped.gg/guides/how-to-read-enemy-intent)を参考にした。2026-10-04確認） |
| 使う敵・似合う敵 | 今使っている敵のID。共通技には、新しい敵へ付けるときの目安も書く |
| 演出 | 感覚の言葉、本体の動き、溜め・発生・余韻の3拍、色、格を書く。敵の絵は攻撃モーションのない1枚絵（[ドット絵の制作規格](../../art/direction.md#画面と寸法)）のため、本体の動きは絵の移動・傾き・伸び縮み・色の変化で表す。形は[エフェクトの描き方](../../art/direction.md#エフェクトの描き方)に従って計算で描く |
| 演出の格 | 小・中・大の3段階。敵の技は、プレイヤーが「だれがどれだけ受けたか」を読み取れるよう、味方のカードの演出より控えめにする。大はボスの大技と激化の合図に限る。どの格でも、標的・開示した弱点・予告を隠さない（[戦闘中の情報の優先順位](../../features/combat.md#戦闘中の情報の優先順位)） |

## 追加方法

[定義テンプレート](../templates/definition.md)をこの分類にコピーし、1技1ファイルで記入する。
既存の技と効果が重ならないことを、この索引と各定義で確かめる。
使う敵の定義の「技」の表にも、技へのリンクを加える。
追加後は[索引を再生成](../../tools/README.md)する。
