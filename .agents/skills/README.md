# スキル索引

このリポジトリのスキル（AIが特定の作業を行うときの進め方）の一覧である。
スキルは `.agents/skills/<スキル名>/SKILL.md` に置き、Claude Code は `.claude/skills`（`.agents/skills` へのシンボリックリンク）から読む。
このファイルと `shared/` はスキルではない（`SKILL.md` を持たないため、スキルとしては読み込まれない）。

規則・数値・配置はdocを正本にし、スキルはその作業の進め方とdocへのリンクだけを持つ（[docとスキルの使い分け](../../doc/rules/documentation-policy.md#docとスキルの使い分け)）。
どの作業でも守る制約はルートの [AGENTS.md](../../AGENTS.md) にある。

## 呼び出し方

| 種類 | 発動 | 設定 |
|---|---|---|
| 自動 | 依頼が `description` に当てはまると、AIが自分で読み込む。名前を指定して呼んでもよい | 特になし（Codex の `agents/openai.yaml` は `allow_implicit_invocation: true`） |
| 手動 | ユーザーが名前で呼んだときだけ使う。Claude Code では `/スキル名`、Codex では `$スキル名` | `SKILL.md` の先頭設定に `disable-model-invocation: true`（Claude Code）、`agents/openai.yaml` に `allow_implicit_invocation: false`（Codex）。両方そろえる |

## Git・PR

| スキル | 使う場面 | 呼び出し | 正本のdoc | 担当外・一緒に使うスキル |
|---|---|---|---|---|
| [commit](commit/SKILL.md) | 依頼範囲の変更だけを論理単位に分けてコミットする | 手動 | [commitの手順](../../AGENTS.md#commitの手順)（テストの全件実行）、[commitの分け方とメッセージ](../../doc/rules/git-commit.md) | テストの実行方法・分け方・メッセージの形式は持たず、正本のdocに従う |
| [create-pr](create-pr/SKILL.md) | コミット済みのブランチをpushし、日本語のPRを作る | 手動 | — | ベースブランチの取得・既存PRの再利用・PR本文の形式の正本。[japanese-tech-writing](japanese-tech-writing/SKILL.md) |
| [babysit-pr](babysit-pr/SKILL.md) | commit・push・PR作成から、GHAと自動レビューの指摘を最新headで収束させるまで | 手動 | [commitの手順](../../AGENTS.md#commitの手順)、[commitの分け方とメッセージ](../../doc/rules/git-commit.md) | commit と create-pr の手順に従う |

ふつうの「コミットして」という依頼では commit スキルを使わず、AGENTS.md の[commitの手順](../../AGENTS.md#commitの手順)に従う。

## 文章

| スキル | 使う場面 | 呼び出し | 正本のdoc | 担当外・一緒に使うスキル |
|---|---|---|---|---|
| [japanese-tech-writing](japanese-tech-writing/SKILL.md) | 日本語の技術文書（`doc/`・README・スキルの SKILL.md）、PR本文、記事を書く・推敲する | 自動 | [文書管理方針](../../doc/rules/documentation-policy.md) | 長い読み物では cognitive-rhythm-writing を併用し、予告・問答・境界の語りはそちらを優先。カードの説明文は card-skill-authoring |
| [cognitive-rhythm-writing](cognitive-rhythm-writing/SKILL.md) | 章・記事・長い解説の緩急、問いの回収、文の拍を整える | 自動 | — | japanese-tech-writing を土台にして併用する。`doc/` の仕様・ルール、AGENTS.md、PR本文、短い手順、箇条書き中心の文書には使わない |

## 保存と設計

| スキル | 使う場面 | 呼び出し | 正本のdoc | 担当外・一緒に使うスキル |
|---|---|---|---|---|
| [data-storage](data-storage/SKILL.md) | 保存するデータを端末とサーバーのどちらに置くか決め、作り、仕様へ記録する | 自動 | [保存先の決め方](../../doc/rules/data-storage.md) | 保存の待ちの見せ方は loading-concealment、保存中・失敗の画面状態は ui-advisor。サーバーのAPIの書き方は server-api-authoring |

## サーバー

| スキル | 使う場面 | 呼び出し | 正本のdoc | 担当外・一緒に使うスキル |
|---|---|---|---|---|
| [server-api-authoring](server-api-authoring/SKILL.md) | `server/` のAPI・repository・D1の読み書きを足す・直す・レビュー・リファクタする。APIの待ち時間とD1との往復を減らす | 自動 | [バックエンドの開発環境](../../doc/rules/backend-design.md)（[D1との往復と文の数](../../doc/rules/backend-design.md#d1との往復と文の数)） | 保存先を決めるのは data-storage |

## Unity C#

| スキル | 使う場面 | 呼び出し | 正本のdoc | 担当外・一緒に使うスキル |
|---|---|---|---|---|
| [unity-csharp-differences](unity-csharp-differences/SKILL.md) | Unity 6のC#を書く・直す・リファクタ・レビューする。Unity固有のnull判定・実行順・シリアル化・アナライザーの診断、重複の共通化と、生成スクリプトの結果が変わらないことの確かめ方 | 自動 | [クライアントの構成と依存関係](../../doc/rules/frontend-design.md)、[Unityの静的解析](../../doc/rules/client-code-quality.md#unityの静的解析) | テストの実行は [UnityのテストとCI](../../doc/rules/client-testing.md)。`server/` のTypeScriptは server-api-authoring、画面の構成やUXは ui-advisor |

## 録画

| スキル | 使う場面 | 呼び出し | 正本のdoc | 担当外・一緒に使うスキル |
|---|---|---|---|---|
| [unity-video-recording](unity-video-recording/SKILL.md) | 頼まれた場面をEditorのPlay中にUnity RecorderでMP4に録画し、確かめてから送る | 自動 | [動画の録画](../../doc/rules/client-testing.md#動画の録画) | 演出を作る・直すのは vfx-authoring。静止画の撮影は [unity-editor-notes.md](shared/unity-editor-notes.md) |

## Android

| スキル | 使う場面 | 呼び出し | 正本のdoc | 担当外・一緒に使うスキル |
|---|---|---|---|---|
| [unity-android-ci-distribution](unity-android-ci-distribution/SKILL.md) | 頼まれたAPK/AABのビルド（ショートカット・開いているEditor）、署名、artifact、Dev/Prod/Previewの配布、Driveへの配置 | 自動 | [手動実行用ショートカットの動作](../../doc/rules/local-shortcuts.md)、[Androidビルドと実機確認](../../doc/rules/client-android-testing.md) | 端末での確認は device-validation、APKが重いときの画像は image-compression |
| [unity-android-device-validation](unity-android-device-validation/SKILL.md) | ユーザーが起動したAVD・実機へAPKを入れて確かめる | 自動 | [WindowsでのUnityとAndroidエミュレーター](../../doc/rules/client-android-emulator.md) | 完了報告の4区分の正本。エミュレーターは起動しない。APKを作るのは ci-distribution、Health Connectの実装は health-connect |
| [unity-android-health-connect](unity-android-health-connect/SKILL.md) | Health Connectの権限と歩数の取得、Android bridge、Manifest、Gradle | 自動 | [健康データ](../../doc/features/health-data.md)、[起動時の連携と歩数の同期](../../doc/features/startup-sync.md) | 保存先は data-storage、端末での確認は device-validation、APKのビルドは ci-distribution |

## UI

| スキル | 使う場面 | 呼び出し | 正本のdoc | 担当外・一緒に使うスキル |
|---|---|---|---|---|
| [ui-advisor](ui-advisor/SKILL.md) | スマホゲームのUI、画面構成、UX、報酬表示、保守を設計・実装・レビューする | 自動 | [UI設計ルール](../../doc/rules/ui-design.md)、[ゲーム概要](../../doc/game/overview.md) | 見た目のそろえ方は ui-tone-manner、読み込みの待ちは loading-concealment、保存は data-storage、演出は vfx-authoring、案を選んでもらう進め方は creative-direction |
| [ui-tone-manner](ui-tone-manner/SKILL.md) | 画面・窓・決めるボタン・分類・装備の見せ方・操作（家・×・←・戻るキー）を、共通の部品と決まりに合わせて作る・直す。全画面を撮って見比べ、同じ物と同じ操作のずれを点検して直す。画面を線のない形にする | 自動 | [共通の部品](../../doc/rules/ui-design.md#共通の部品) | 画面構成・UX全般は ui-advisor、待ちの見せ方は loading-concealment |
| [loading-concealment](loading-concealment/SKILL.md) | 画面の切り替え・画面を開く・操作したときの読み込みの待ち時間を、楽観的更新・前回の内容の先出し・幕を閉じたまま待つ・カードを伏せる形で見せないようにする。「一瞬初期値が見える」などの指摘 | 自動 | [読み込みの待ち時間を見せない](../../doc/rules/ui-design.md#読み込みの待ち時間を見せない) | 保存先は data-storage、サーバーの往復を減らすのは server-api-authoring、演出の見た目は vfx-authoring |

## 戦闘のカードとパッシブ

| スキル | 使う場面 | 呼び出し | 正本のdoc | 担当外・一緒に使うスキル |
|---|---|---|---|---|
| [card-skill-authoring](card-skill-authoring/SKILL.md) | カードスキル（覚醒版・固有スキルの段階を含む）の効果と説明文を足す・直す・レビューする | 自動 | [戦闘システム](../../doc/features/combat.md)（[カードの説明文に書くこと](../../doc/features/combat/cards.md#カードの説明文に書くこと)） | 挿絵は pixel-art-standards、演出は vfx-authoring、強さの調べ直しは battle-balance |
| [passive-skill-authoring](passive-skill-authoring/SKILL.md) | キャラのパッシブスキルを考える・足す・直す・レビューする | 自動 | [パッシブスキル](../../doc/features/passives.md)（[設計方針](../../doc/features/passives.md#設計方針)） | 説明文の書き方は card-skill-authoring に従う。アイコンの絵は pixel-art-standards |

## 地図

| スキル | 使う場面 | 呼び出し | 正本のdoc | 担当外・一緒に使うスキル |
|---|---|---|---|---|
| [map-event-authoring](map-event-authoring/SKILL.md) | 入れ子の地図のマスの出来事・遊び方・マスの設定を足す・変える・消す。どのマスに何を置くかを変える。地図エディターを直す | 自動 | [出来事を足す・変える・消すとき](../../doc/features/world-atlas/square-events.md#出来事を足す変える消すとき)、[地図エディター](../../doc/features/screens/debug-rooms.md#地図エディター) | 窓の演出は vfx-authoring、台の絵は creative-direction と image-compression、戦闘の強さの調べ直しは battle-balance |

## 戦闘のバランス

| スキル | 使う場面 | 呼び出し | 正本のdoc | 担当外・一緒に使うスキル |
|---|---|---|---|---|
| [battle-balance](battle-balance/SKILL.md) | 戦闘の自動対戦で勝率とターン数を測り、目安と比べ、原因の場所と数字の見込みつきの直し方を報告する。倍率・敵・カード・パッシブ・装備・ステータスを変えたあとの測り直し | 手動 | [戦闘の自動対戦](../../doc/rules/client-testing.md#戦闘の自動対戦)、[戦闘の長さ](../../doc/features/combat/rules.md#戦闘の長さ)、[戦闘の試し](../../doc/features/stage-progression/battle-trials.md#戦闘の試し) | カードの効果と説明文は card-skill-authoring、パッシブは passive-skill-authoring |

## 戦闘の不具合

| スキル | 使う場面 | 呼び出し | 正本のdoc | 担当外・一緒に使うスキル |
|---|---|---|---|---|
| [battle-debugging](battle-debugging/SKILL.md) | 戦闘の効果・状態・敵の技が思った通りに働かない、数が合わない、画面・予測・自動対戦で結果が違うときに、画面なしの戦闘のテストで再現してから直す。足した効果が狙った場面で働くかを確かめる | 自動 | [画面なしの戦闘で確かめる](../../doc/rules/client-testing.md#画面なしの戦闘で確かめる)、[効果の部品](../../doc/features/combat/effects.md) | 強さの調べ直しは battle-balance、カードは card-skill-authoring、パッシブは passive-skill-authoring、演出は vfx-authoring |

## ドット絵と画像生成

| スキル | 使う場面 | 呼び出し | 正本のdoc | 担当外・一緒に使うスキル |
|---|---|---|---|---|
| [pixel-art-standards](pixel-art-standards/SKILL.md) | ドット絵（キャラ・敵・アイテム・UIアイコン・カードの挿絵・3Dの舞台のテクスチャ）を作る・清書する・採用する・Unityへ取り込むとき、規格を指示文と出来上がりに当て、`.aseprite` にする | 自動 | [アート方針のドット絵の制作規格](../../doc/art/direction.md#ドット絵の制作規格) | 下の生成スキルと一緒に使う。ドット絵にしない画像をゲームに入れるのは image-compression |
| [rpg-character-profile](rpg-character-profile/SKILL.md) | 新しい仲間・キャラの設定（二つ名と名前、フレーバー、外見と残す特徴、動作設計、イラストの構図）を考える・作り直す | 自動 | [アート方針](../../doc/art/direction.md)（寸法） | 次の工程は character-sprite-sheet・character-illustration。イラストのポーズと表情の規則の正本。スキルの性能は card-skill-authoring、パッシブは passive-skill-authoring |
| [character-sprite-sheet](character-sprite-sheet/SKILL.md) | キャラ設定から全動作のドット絵スプライトシートをCodex CLIで生成する | 手動 | [アート方針](../../doc/art/direction.md) | [キャラ制作の共通手順](shared/character-pipeline.md)、pixel-art-standards |
| [character-illustration](character-illustration/SKILL.md) | キャラ設定から背景付きのイラストをCodex CLIで生成する | 手動 | — | [キャラ制作の共通手順](shared/character-pipeline.md) |
| [item-icon](item-icon/SKILL.md) | アイテム・武器・防具・素材のアイコン候補をCodex CLIで作り、規格の寸法に直した一覧から選ばせる | 手動 | [アート方針](../../doc/art/direction.md) | pixel-art-standards |
| [image-compression](image-compression/SKILL.md) | 画像をUnityへ追加・差し替えるとき、元の画像を残してスマホ向けにASTCで圧縮し、容量を確かめる。APKが重いとき | 自動 | [画像の圧縮](../../doc/rules/frontend-design.md#画像の圧縮) | `.aseprite` のドット絵は pixel-art-standards、APKのビルドは unity-android-ci-distribution |

## HD-2DとVFX

| スキル | 使う場面 | 呼び出し | 正本のdoc | 担当外・一緒に使うスキル |
|---|---|---|---|---|
| [hd2d-stage-set](hd2d-stage-set/SKILL.md) | 3Dの舞台のカメラ・キャラの板・光・霧・光芒・松明や焚き火の炎と煙・Topに重ねる2Dの塵ときらめき・レンズと色・停止中の表示と、地面・遠景・小物・草・汚し・マテリアル・テクスチャ。舞台の重さ | 自動 | [3Dの舞台](../../doc/art/hd2d-stage.md) | 技の演出と報酬を受け取る瞬間の演出は vfx-authoring、描いた2D背景の常設の演出は hd2d-lighting-vfx、描いた背景の絵そのものは creative-direction。テクスチャは pixel-art-standards と一緒に使う |
| [vfx-authoring](vfx-authoring/SKILL.md) | 技・溜め・被弾・撃破・カットイン・覚醒・報酬を受け取る瞬間やUIの演出を、外部画像なしの計算で作る・直す・レビューする。用語・計算式・属性別レシピ・Claudeへの指示文・制作のコツを持つ | 自動 | [エフェクトの描き方](../../doc/art/direction.md#エフェクトの描き方)、[画面一覧と操作](../../doc/features/screens.md) | 常設の光・霧・粒は hd2d-stage-set（3D。Topに重ねる2Dの塵ときらめきを含む）か hd2d-lighting-vfx（2D）。カード・装備の絵に重ねるキラキラと枠の光は card-foil-sparkle。案を選んでもらう進め方は creative-direction、技の音は sound-authoring |
| [card-foil-sparkle](card-foil-sparkle/SKILL.md) | カード・装備のアイコン・強化の札の絵に、品質に応じたラメ・光の帯・漂う光の粒・ホロの虹色を重ね、枠の金属の色を変える | 自動 | [カードのキラキラ](../../doc/art/direction.md#カードのキラキラ) | スキルを使ったとき・覚醒・報酬を受け取る瞬間の演出は vfx-authoring、画面に漂うキラキラの粒は hd2d-lighting-vfx、ドット絵そのものは pixel-art-standards |
| [hd2d-lighting-vfx](hd2d-lighting-vfx/SKILL.md) | 描いた2D背景に常に出す光芒・霧・揺らぐ光・火の粉・ポストプロセスと、画面に常に漂うキラキラ粒子 | 手動 | [展示室の仕様](../../doc/features/showcase.md)（部品の調整項目）、[描いた2D背景のHD-2D演出](../../doc/art/direction.md#描いた2d背景のhd-2d演出)（技法の扱い） | 3Dの舞台（Topに重ねる2Dの塵ときらめきを含む）は hd2d-stage-set、報酬を受け取る瞬間の演出は vfx-authoring。HD-2Dの調査資料と実装の知見は shared/ にあり、両方のスキルが使う |

## 音

| スキル | 使う場面 | 呼び出し | 正本のdoc | 担当外・一緒に使うスキル |
|---|---|---|---|---|
| [sound-authoring](sound-authoring/SKILL.md) | 効果音とBGMをフリー素材から選び、演出に合わせて伸ばす・切る・重ねる加工をして、Unityで鳴らす・直す・レビューする。音は必ず、鳴らす場面の動画に重ねてユーザーに聞かせてから決める。音を測る・加工する・動画に重ねるスクリプトを持つ | 自動 | [効果音とBGM](../../doc/rules/sound.md)、[外部素材のクレジット](../../doc/rules/credits.md) | 演出の見た目は vfx-authoring。場面の録画は unity-video-recording |

## 見た目と音の決め方

| スキル | 使う場面 | 呼び出し | 正本のdoc | 担当外・一緒に使うスキル |
|---|---|---|---|---|
| [creative-direction](creative-direction/SKILL.md) | 演出・画像・アセット・効果音・BGMを、ユーザーと一緒に決める・作り直す。今の状態を撮って課題を挙げ、今の画面を土台に方向性の違う案を作って選んでもらい、作って動画や画像で確かめて送り、指摘を1つずつ直す | 自動 | [アート方針](../../doc/art/direction.md)、[効果音とBGM](../../doc/rules/sound.md) | 作る技法は vfx-authoring・pixel-art-standards・sound-authoring・ui-advisor・ui-tone-manner。録画は unity-video-recording |

## 迷ったとき

| 依頼の例 | 使うスキル |
|---|---|
| Top・Home・戦闘の背景の光が足りない、霧・光芒・ぼけ・カメラの動き・松明や焚き火の炎・Topの2Dの塵ときらめきを直す、舞台が重い（3Dの舞台に常設する光と空気） | hd2d-stage-set |
| 3Dの舞台の背景が寂しい・平ら、地面・遠景・小物・草・テクスチャを足す | hd2d-stage-set |
| 技の攻撃・回復・防御、溜め、被弾、撃破、カットイン、覚醒、報酬を受け取る瞬間の演出 | vfx-authoring |
| 演出・画像・音が「ダサい」「作り直して」「何パターンか見せて」、見た目や音をユーザーと選びながら決めたい | creative-direction と、対象を作るスキル（vfx-authoring・pixel-art-standards・sound-authoring など） |
| カードやキャラの絵をレアっぽくキラキラさせる（ラメ・光の帯・ホロ）、品質で枠を金や虹にする | card-foil-sparkle |
| ボタン・技・被弾・報酬に効果音を付ける、BGMを付ける、音が短い・長い・遅れる | sound-authoring。技の音は vfx-authoring の演出の拍に合わせる |
| 3Dの舞台がない画面（描いた2D背景）の光・霧・火の粉、キャラ紹介など画面に常に漂うキラキラ | hd2d-lighting-vfx（手動専用。使ってよいかユーザーに確認する） |
| 2Dの背景画像を新しく描く | 専用のスキルはない。Codex CLIの画像生成（shared/scripts/codex_image.py）で作り、案を選ぶなら creative-direction、ゲームに入れるときは image-compression。ドット絵の規格は当てない（[アート方針](../../doc/art/direction.md#例外)） |
| キャラ・敵・アイテムのドット絵を作る・採用する | pixel-art-standards と、対象の生成スキル（キャラは character-sprite-sheet、アイテムは item-icon。敵の専用のスキルはなく、pixel-art-standards の[敵の絵の一括試作](pixel-art-standards/references/enemy-art.md)に従う） |
| 新しいキャラを考える → 絵にする | rpg-character-profile → character-sprite-sheet・character-illustration |
| カードスキルを足す・文言を直す | card-skill-authoring |
| キャラのパッシブスキルを考える・直す | passive-skill-authoring |
| 敵が硬い・弱い、戦闘が長い・危ない、倍率や数値を変えたあとに測り直す | battle-balance（手動専用。使ってよいかユーザーに確認する） |
| 戦闘の効果・状態・敵の技が思った通りに働かない、数が合わない、戦闘が止まる、画面と予測・自動対戦で結果が違う | battle-debugging |
| 地図のマスの出来事・賭けの遊び方・戦闘の敵やLvの設定を足す・消す、どのマスに何を置くかを変える、地図エディターを直す | map-event-authoring |
| 画面を作る・直す | ui-advisor と ui-tone-manner。保存を伴えば data-storage、演出を伴えば vfx-authoring も |
| 画面の見た目がばらばら、統一感がない、窓・ボタン・タブ・札・戻り方をそろえたい、全画面を撮って見比べて、枠線が多い・ボーダーをなくして | ui-tone-manner |
| 画面の切り替えで一瞬別の姿が見える、開くのが遅い、「読み込み中」を出したくない | loading-concealment。サーバーが遅いなら server-api-authoring も |
| この場面・動き・スキルの演出を動画で撮って見せて | unity-video-recording |
| Unity C#を書く・直す、重複を共通化する、Analyzerのエラーを直す、生成スクリプトを直して結果が同じか確かめる | unity-csharp-differences。テストの実行はスキルを使わず[作業中に実行するPlayModeテスト](../../doc/rules/client-testing.md#作業中に実行するplaymodeテスト)に従う |
| 保存する処理を足す、保存先を聞かれた | data-storage |
| サーバーのAPIを足す・直す、APIが遅い | server-api-authoring。保存先が未決なら先に data-storage |
| APKを作る・配る → 端末で確かめる | unity-android-ci-distribution → unity-android-device-validation |
| 歩数が取れない・0のまま、Health Connectの権限や連携の流れを直す | unity-android-health-connect。端末に入れて確かめるなら unity-android-device-validation も |
| APKが大きい・重い、画像をゲームに入れる | image-compression |
| 文書・PR本文を書く。長い読み物を書く | japanese-tech-writing。長い読み物では cognitive-rhythm-writing も |

## shared/ の役割

複数のスキルが使う道具と手順を置く。スキルではないため、単独では読み込まれない。

| ファイル | 内容 | 使うスキル |
|---|---|---|
| [scripts/codex_image.py](shared/scripts/codex_image.py) | Codex CLIの画像生成で1枚作り、加工せずに回収する。どの依頼にも「細碎な点状テクスチャが多すぎるので抑える」と、その具体的な指示を足す（[AGENTS.md](../../AGENTS.md#作業の基本)） | character-sprite-sheet、character-illustration、item-icon、hd2d-stage-set、creative-direction |
| [prompts/pixel-style.ja.txt](shared/prompts/pixel-style.ja.txt) | ドット絵の規格を指示文にした固定文。正本は[ドット絵の制作規格](../../doc/art/direction.md#ドット絵の制作規格) | pixel-art-standards、character-sprite-sheet、item-icon、hd2d-stage-set |
| [scripts/pixel_style.py](shared/scripts/pixel_style.py) | 上の固定文を部品ごとに読み、組み合わせて出力する | pixel-art-standards、character-sprite-sheet、item-icon、hd2d-stage-set |
| [character-pipeline.md](shared/character-pipeline.md) | キャラ制作の共通手順（出力先、キャラ設定の探し方、必ず残す特徴の1行、Codexへの依頼、見せ方） | character-sprite-sheet、character-illustration、rpg-character-profile |
| [unity-editor-notes.md](shared/unity-editor-notes.md) | Unity EditorとCLIの操作・撮影で得た知見 | hd2d-stage-set、vfx-authoring、hd2d-lighting-vfx、unity-video-recording、creative-direction、sound-authoring、ui-tone-manner、unity-csharp-differences |
| [hd2d-research-sources.md](shared/hd2d-research-sources.md) | HD-2Dの調査資料（開発者の発言、再現例、Unityの資料、光芒・小物・炎の調べ物と本作での判断）。[3Dの舞台](../../doc/art/hd2d-stage.md)からもリンクする | hd2d-lighting-vfx、hd2d-stage-set、vfx-authoring |
| [hd2d-implementation-notes.md](shared/hd2d-implementation-notes.md) | 描いた2D背景のHD-2D演出を実装して得た知見（描画・保存・C#のつまずき、部品設計の工夫） | hd2d-lighting-vfx、hd2d-stage-set、vfx-authoring |

## スキルを足す・直すとき

- フォルダー名をスキル名にし、`SKILL.md` の先頭設定に `name` と `description` を書く。`description` は発動の判定に使われるため、数値を書かなくても何のスキルか分かる言葉で、使う場面と使わない場面を書く。Codex 用に `agents/openai.yaml` も置く。
- 手動専用にするときは、上の「呼び出し方」の2つの設定をそろえる。
- docにある規則・数値・配置を写さず、リンクにする。スキル同士で同じ手順が重なったら、一方を正本にしてもう一方から呼ぶ。複数のスキルが使う道具は `shared/` に置く。
- 名前を変える・ファイルを移すときは、ほかのスキル・`AGENTS.md`・`doc/` からの参照を検索して直す。
- 同じ変更でこの索引を直す。
