---
id: plan-ui-design-system-20261101
type: reference
status: 候補
updated: 2026-11-01
---

# 画面のデザインシステムとテンプレートで、似た画面を共通化する

状態：進行中（2026-11-01に第0段から第3段までを実装した。commitはまだ。第4段は地図の作業のcommit待ち、第5段は一部）

[計画索引](README.md) / [UI設計ルールの共通の部品](../rules/ui-design.md#共通の部品) / [画面一覧と共通UI](../features/screens.md) / [ui-tone-manner](../../.agents/skills/ui-tone-manner/SKILL.md)

この計画は、会話の記録がなくても続けられるように書いている。
続きから始めるときは、まず[再開するときの手引き](#再開するときの手引き)を読む。

## 目的

画面ごとに作り分けている窓・ボタン・分類・持ち物の札・パネルの並びを、**基本の値・部品・テンプレート**の3つの層にまとめる。
見た目が少し違うだけの画面と、同じ見せ方なのに並びが違う画面を、同じテンプレートから組む。
新しい画面を足すときに、テンプレートを選んで中身を入れるだけで、ほかの画面と同じトンマナになるようにする。

## この計画の意義

### 遊ぶ人にとって

- **一度覚えれば、どの画面でも同じに使える。** 閉じる×、決めるボタン、分類の切り替えが、画面ごとに場所や形を変えないため、押す前に迷わない。今は、決めるボタンだけで6通りの形がある。
- **画面の雰囲気がそろい、1つのゲームとして信頼できる。** 起動直後の連携のモーダルだけ青緑で、ほかの窓と別のゲームのように見えていた（第0段で直した）。
- **読みやすさを保つ。** 文字の大きさが27通りに散らばり、同じ役割の文字が画面ごとに少しずつ違う。段階を決めると、見出し・本文・注記の区別が画面をまたいでも同じになる。
- **押しやすさ。** 横のタブは押しづらいとユーザーが指摘した（装備の画面で外した経緯がある）。分類は縦に並べる決まりにして、どの画面でも同じ押し方にする。

### 作る側にとって

- **これから足す画面を、早く同じトンマナで作れる。** 設定、お知らせ、初回の案内、実績、報酬の受け取り、合成の記録、ライブラリのアクセサリー、地図の出来事の設定など、まだ作っていない画面が多い（[画面一覧の未決](../features/screens/common.md)）。テンプレートを選んで中身を入れる形にしておけば、作るたびに見た目を考え直さなくてよい。
- **直す場所が1つになる。** 同じ組み立てが画面ごとに書かれている（×6か所、状態の文字6か所、ルーンの数9か所、持ち物の札の型6種類、色の定数73個）。共通の部品にすれば、色や余白を変えるときに1か所を直せば全画面に効く。
- **並行して作業しても、ずれにくい。** このリポジトリでは、複数のチャットが同じフォルダーで画面を足している。決まりと部品がないと、チャットごとに少しずつ違う形が増える。今回のばらつきの多くは、そうして生まれた。
- **不具合を減らす。** 画面ごとに組み立てを書くと、同じ種類の不具合が画面ごとに起きる。たとえば、組み直したPrefabにシーンの古い上書きが残り、冒険の結果の窓の高さが0になった（2026-11-01）。窓をテンプレートから作り、テストと撮影で確かめる流れにすれば、まとめて防げる。

### プロジェクトの方針との関係

- [AGENTS.md](../../AGENTS.md) の「不要なセカンダリラベルや説明を避ける。レイアウトに説明させる」を、決まった配置（どの窓でも右上に×、右下に決めるボタンなど）で支える。
- [クライアントアセット展示室](../features/showcase.md)で、部品とテンプレートを1か所で確かめられる。
- 規則と数値の正本は文書（[共通の部品](../rules/ui-design.md#共通の部品)）に置き、作り方の手順はスキル（[ui-tone-manner](../../.agents/skills/ui-tone-manner/SKILL.md)）に置く（[docとスキルの使い分け](../rules/documentation-policy.md#docとスキルの使い分け)）。

## ここまでの流れ

2026-11-01の1日の会話で、次の順に進んだ。

1. **冒険の結果画面を作り直した。** ダンジョンを終えたあとの結果画面を、5案→3案→5案の見本の画像から選んで作り、クリア回数の報酬（1〜5回目、毎回300ルーン、受け取るまで影と「？？？」で伏せる）を足した。2つのcommitにして `feat/0929` へpushし、Dev向けのAPKをDriveに置いた（`2d690dd6`・`4359765c`。前者は並行していた別のチャットの地図の作業）。
2. **全画面を撮った。** ユーザーが「ラボ以外のシーン、モーダル、ダイアログ、タブを全て撮って、共通化を検討して」と依頼した。停止中の表示の切り替えのすべての状態（182）と、Play中の世界地図（11）の193枚を撮り、シーンごとの一覧と、窓・パネル・決めるボタンの見比べ用の画像を作った。Webで画面の型と共通化の考え方を調べた。
3. **第0段を実装した。** ユーザーが「全体的に共通化して、トンマナのスキルを作って」と依頼した。途中で「どの画面をどの画面に合わせるか、先に教えて」と言われ、対応表を見せてから進めた。窓・暗い幕・縦の分類・札の位置をそろえ、正本の節とスキル `ui-tone-manner` を作った。
4. **編成のスキルを戻した。** 縦の分類にしたところ「めっちゃ見づらくなってる」と言われたため、横のタブに戻し、例外として正本に書いた。
5. **層とテンプレートの考えを決めた。** ユーザーが「コードも共通化されている？」「微妙に変えてよければ、もっと共通化できない？」「Atomic Designのテンプレートのような考えで、モーダルや背景色の違いまで広げれば？」と尋ねた。数えたばらつきを示し、層を3つに絞る案を出した。ユーザーは「完全なatomicは不便。デザインシステム・テンプレート・部品の概念は欲しい。構造的な変更と、微妙に異なる画面、同じ見せ方で並びが違う画面も対象に」と決めた。
6. **対応表を了承した。** テンプレートA〜Fと、合わせる画面の表をユーザーが了承し、「方針良さそう」とした。
7. **この計画を書いた。** ユーザーが「やるべきことを事細かに計画にし、最後に学びをスキル化する（既存のスキルを直す）作業も入れて」と依頼した。続けて「意義・目的・流れも書き、会話の記録を消しても辿れるように」と依頼した。

## 再開するときの手引き

### 最初に確かめること

1. `git status` で、下の[作業中の変更](#作業中の変更2026-11-01の終わりの時点)が残っているか、commitされたかを確かめる。
2. `ls -lt ~/.claude/projects/-Users-natani-work-baryonyx/*.jsonl | head` と `python3 client/ci/editor-lock.py status` で、ほかのチャットが動いていないかを確かめる。地図の周りの作業がcommitされたかも見る。
3. この計画の[未決事項](#未決事項)と[実装の記録](#実装の記録)を読み、どの段から続けるかを決める。
4. 段を始める前に、その段の対応表（変える画面と合わせる先）をユーザーに見せる（ユーザーの希望）。

### 作業中の変更（2026-11-01の終わりの時点）

ブランチは `feat/0929`。
最新のcommitは `6d53667e`（別のチャットの地図の背景の圧縮）で、第0段と計画はまだcommitしていない。

**この計画の変更（第0段・文書・スキル）**

| 種類 | ファイル |
|---|---|
| 共通の部品 | `client/Assets/Baryonyx/Shared/UI/UiPalette.cs`（`Veil`）、`Shared/UI/GuideMenu/Editor/GuideFrameAssets.cs`（`ListWindow`・`WindowButton`・`SideTabs`・`SideTab` の錠前）、`Shared/UI/Dialog/Editor/GameDialogAssets.cs`（幕） |
| 画面の生成処理 | `Features/Health/Editor/HealthLinkModalAssets.cs`、`Features/Training/Editor/TrainingAssets.cs`、`Features/Temple/Editor/TempleSummonAssets.cs`、`Features/TravelOffice/Editor/TravelOfficeScreenAssets.cs`、`Features/Library/Editor/LibraryScreenAssets.cs`、`Features/Workshop/Editor/WorkshopPanelAssets.cs`、`Features/Adventure/Editor/AdventureResultAssets.cs`、`Features/Adventure/Editor/ExplorationCampfireAssets.cs` |
| 生成したPrefab・シーン | `HealthLinkModal.prefab`、`Top.unity`（モーダルを置き直した）、`TavernScreen.prefab`、`TempleScreen.prefab`、`TempleSummonShow.prefab`、`TempleSummonTenShow.prefab`、`TravelOfficeScreen.prefab`、`LibraryScreen.prefab`、`WorkshopScreen.prefab`、`AdventureOverlay.prefab`、`ExplorationScreen.prefab`、`TempleGuideMenu.asset`（生成時に新しい項目が書かれた） |
| フォント | `Shared/UI/Fonts/KiwiMaru.asset`（新しく出た字が足された。正しい変化） |
| 文書 | `doc/rules/ui-design.md`（共通の部品）、`doc/features/library.md`、`doc/features/screens/adventure.md`・`loadout.md`・`temple.md`、`doc/features/startup-sync.md`、`doc/plans/2026-11-01-ui-design-system.md`、`doc/plans/README.md`、`doc/heading-index.md` |
| スキル | `.agents/skills/ui-tone-manner/`（新規。`scripts/` に撮影の台本）、`.agents/skills/README.md`、`.agents/skills/shared/unity-editor-notes.md` |

**別のチャットの変更（混ぜない）**

地図の左上の戻るボタンを家のアイコンにする作業で、ユーザーが「ステイ」と言い、commitされずに止まっている。
`WorldAtlas.unity`、`Features/WorldMap/` の `Atlas/Resources/WorldAtlas/*.prefab`・`Editor/WorldAtlasAssets.cs`・`Editor/WorldMapScreenAssets.cs`・`Runtime/WorldAtlasNavigator.cs`・`Tests/PlayMode/WorldAtlasTests.cs`・`UI/Art/IconHome.png`、`doc/features/world-atlas.md`、`doc/features/showcase.md`、`Features/Showcase/Data/ShowcaseCatalog.asset` と `Showcase/Entries/Generated/*IconHome*` がその変更である。
この計画のcommitには入れない。
地図の画面を組み直すと、その変更とこの計画の変更がPrefabの中で混ざるため、第4段はその作業がcommitされてから始める。

### 撮った画像と作業用の置き場

`output/` はgitに入れない作業用の置き場である。

| 場所 | 中身 |
|---|---|
| `output/ui-audit/stage0-before/` | 第0段の前の193枚。画像の名前は「シーン-部品-状態の番号」（例：`Top-HealthLinkModal-00.png`）、世界地図は `w-000-world.png` など。第1段以降の比べる元にはしない（第0段のあとを撮り直して元にする） |
| `output/ui-audit/sheets/` | 第0段の前の、シーンごとの4列の一覧 |
| `output/ui-audit/groups.jpg` | 同じ見せ方なのに並びが違う画面の組（A・B・C） |
| `output/ui-audit/compare-*.jpg` | 窓・パネル・決めるボタンの見比べ |
| `output/ui-audit/before-after/` | 第0段の前と後（16組） |
| `output/ui-audit/work/` | 撮影の台本の作業用の置き場（`inventory.txt`・`specs.txt`・`shots/` など） |

撮影の台本と使い方は [ui-tone-manner の撮影の台本](../../.agents/skills/ui-tone-manner/SKILL.md#撮影の台本scripts) にある。

### 画面の組み直し方

画面の生成処理を変えたら、Editorで次を評価してPrefabとシーンを作り直す（`unity --format json command --caller plugin --skill unity-cli eval_file --file <台本>`、評価の前に札を取る）。
評価の応答は5秒で切れることがあるため、台本の最後で `Debug.Log("[タグ] built")` を出し、`client/Logs/Editor.log` でその行を待つ。

| 画面 | 評価する処理 |
|---|---|
| 編成（育成・装備・スキル・突破・冒険者の一覧） | `Baryonyx.Tavern.Editor.TavernScreenAssets.CreateAssets()` |
| 商会 | `Baryonyx.Workshop.Editor.WorkshopScreenAssets.CreateAssets()` |
| 神殿 | `Baryonyx.Temple.Editor.TempleScreenAssets.CreateAssets()` |
| 旅の案内所 | `Baryonyx.TravelOffice.Editor.TravelOfficeScreenAssets.CreateAssets()` |
| ライブラリ | `Baryonyx.Library.Editor.LibraryScreenAssets.CreateAssets()` |
| 探索と戦闘の重ね（冒険の結果・ダイアログ） | `Baryonyx.Adventure.Editor.AdventureAssets.CreateAssets()` |
| Topの連携モーダル | `Baryonyx.App.Editor.TopStartupSyncSetup.ConnectScenes()` |
| ホーム | `Baryonyx.App.Editor.HomeSceneSetup` のメニュー（`Baryonyx > App > Create Home Scene`） |

組み直したあとは、`git status` で戦闘シーンなど触っていないシーンに差分が出ていないかを確かめる。

### 画面ごとのテスト

| 画面 | テスト |
|---|---|
| 編成 | PlayMode：`TavernTrainingTests`・`TavernEquipmentTests`・`TavernCardLoadoutTests`・`TavernFormationTests`・`TavernGearPickerTests`。EditMode：`TrainingPresenterTests`・`CardLoadoutPresenterTests` |
| 商会 | PlayMode：`WorkshopBuyTests`。EditMode：`WorkshopGoodsTests` |
| 神殿・旅の案内所・ライブラリ | PlayMode：`GuideScenesTests`・`LibraryReadAgainTests` |
| Top | PlayMode：`TopHomeSceneTests`。EditMode：`HealthStartupFlowTests` |
| 冒険 | PlayMode：`AdventureLoopTests`。EditMode：`AdventureStateTests` |
| 案内人の画面の読み込み | EditMode：`GuidePanelLoadTests` |
| 世界地図（第4段） | PlayMode：`WorldAtlasTests`・`WorldAtlasBattleTests`・`WorldEventLabSceneTests` |

実行は `python3 client/ci/run-editor-tests.py <クラス名...> --owner <名前>`（EditModeは `--mode editmode`、C#を変えたら `--recompile`）。
2026-11-01の第0段のあとは、上のEditMode 81件、PlayMode 51件（世界地図を除く）が通った。

### ユーザーの好みと決まり

- 返事・文書は日本語。説明の小見出しや注釈を足さず、配置で意味が分かるようにする。
- 作業の前に「どの画面をどの画面に合わせるか」の対応表を見せる。
- 終わったら、変わった画面の前と後を並べた画像を送る。
- 横のタブは置かない（例外は編成のスキル）。選ぶ画面は、上に小さく並べて押したものを大きく出し、右下のチェックで決める形が好まれる。
- commit・pushは頼まれてから。commitの前に、変更した領域のテストを全件実行する（[commitの手順](../../AGENTS.md#commitの手順)）。
- リモートで操作していることが多い。作業が終わったら通知を送る。

### コードの地図

| 置き場所 | 中身 |
|---|---|
| `client/Assets/Baryonyx/Shared/UI/UiPalette.cs` | 実行時の色（文字・金・幕）。第1段で役割の名前を足す |
| `client/Assets/Baryonyx/Shared/UI/GuideMenu/Editor/GuideFrameAssets.cs` | 枠つきのパネルの帯（`FramedPanel`）、面（`Pane`・`SplitPanes`・`Divider`）、見出し（`Heading`）、窓（`Window`・`WindowFrame`・`ListWindow`・`WindowButton`）、分類（`SideTab`・`SideTabs`・横の `Tabs`） |
| `client/Assets/Baryonyx/Shared/UI/GuideMenu/Editor/GuideMenuAssets.cs` | 案内人の画面、アイコンの読み込み（`Icon`）、文字を縮める `Shrink`、×と戻るのアイコンのパス |
| `client/Assets/Baryonyx/Shared/UI/GuideMenu/Editor/GuidePanelLayout.cs` | 設計座標で置く道具（`Box`・`Text`・`Grid`・`Scroll` など）、`Design`（1920×1080）と `HitSize`（128） |
| `client/Assets/Baryonyx/Shared/UI/GlassCard/Editor/GlassCardAssets.cs` | すりガラスのカード（`Build`・`Choice`・`Plaque`・`AddBack`）と、その色 |
| `client/Assets/Baryonyx/Shared/UI/Dialog/` | 知らせる窓（`GameDialog` と生成処理） |
| `client/Assets/Baryonyx/Editor/UI/UiBuild.cs` | 生成の基本（`Rect`・`Label`・`AddImage`・`SafeArea` など）。`Begin(font, shadow)` の中で使う |
| `client/Assets/Baryonyx/Features/Workshop/Editor/WorkshopPanelAssets.cs` | 商会のパネルの骨組み（`Panel`・`Grid`・`Status`・`MaterialTile`・`PieceCard`）。テンプレートAとBの元にする |
| `client/Assets/Baryonyx/Features/Party/Editor/PartySheetAssets.cs`・`PartyChangeScreenAssets.cs` | 冒険者のシート（テンプレートD）の元 |
| `client/Assets/Baryonyx/Shared/UI/EditModePreview/` | 停止中の表示の切り替え（`IEditModePreview`・`EditModePreviewer`） |

## 依頼と決定した事項

| 日付 | ユーザーの依頼・決定 | この計画での扱い |
|---|---|---|
| 2026-11-01 | 「ラボ以外の全体的なシーン、モーダル、ダイアログ、タブを全て撮って、共通化を検討して」 | 193枚を撮り、[調べたこと](#調べたこと)にまとめた |
| 2026-11-01 | 「全体的に共通化して。情報量を分けたければシーンを分けてよい」「トンマナをまとめるスキルを用意して」 | 第0段を実装した。シーンは分けない（施設ごとに1シーンで、中をパネルで替える今の形で足りる）。スキル `ui-tone-manner` を作った |
| 2026-11-01 | 「どの画面をどの画面に合わせるか、先に教えてほしい」 | 作業の前に対応表を見せて了承を得る。スキルの手順にも入れた |
| 2026-11-01 | 連携のモーダルとレベルアップの確認は了承。編成のスキルは見づらくなった | 編成のスキルは横のタブに戻し、[例外](../rules/ui-design.md#分類の切り替え)として残した |
| 2026-11-01 | 「完全なatomicデザインは不便だが、デザインシステム・テンプレート・細かい部品の概念は欲しい」「構造的な変更と、微妙に異なる画面の共通化。同じ見せ方でレイアウトが異なる画面も対象」 | 層は3つに絞る（[方針](#方針)）。テンプレートA〜Fと[対応表](#対応表)をユーザーが了承した |
| 2026-11-01 | 「最後に、今回のデザインの学びをスキル化するか、既存のスキルを直すタスクを加えて」 | [第5段](#第5段文書とスキル)に入れた |
| 2026-11-01 | 「文字はいまの8パターンでOK。ちょいバラバラだけど仕方ない」 | 段階の間の値は寄せずに残し、新しく作る画面と直す画面から段階を使う（[基本の値](../rules/ui-design.md#基本の値)） |

## 調べたこと

ラボ（デバッグルーム）以外の12シーンと、停止中の表示の切り替えのすべての状態、Play中にだけ作られる世界地図の出来事の窓を撮った。
撮った画像は `output/ui-audit/`（`shots/` に1枚ずつ、`sheets/` にシーンごとの一覧、`groups.jpg` に似た画面の組）にある（gitに入れない作業用の置き場）。

画面の生成処理（`*Assets.cs`）75ファイルを数えた結果は次のとおりである。

| 項目 | ばらつき |
|---|---|
| 文字の大きさ | 27通り（15〜150）。24・26・28・30・32のように、ほとんど差のない大きさが並ぶ |
| 暗い紺の色 | 色が19通り、透け方が21通り（0.22〜0.965）。窓の後ろの幕は0.72・0.78・0.8・0.9が混ざる |
| 生成処理の中で決めた色の定数 | 73個 |
| 窓の幅 | 5通り（1000・1200・1360・1400・1840） |
| パネルの左右の分け目 | 6通り（660・760・860・1160・1180、ほかに全幅） |
| 持ち物の札の部品の型 | 6種類（`WorkshopGoodRow`・`LibraryTileWidget`・`AdventurerTileWidget`・`TempleSummonCard`・`TravelTreasureTile`・`AdventureResultCell`） |
| 画面ごとに作っている部品 | ×のボタン6か所、「取得できませんでした」の文字6か所、ルーンのアイコンと数9か所 |

同じ見せ方なのに並びが違う画面の組は次のとおりである。

| 組 | 画面 | 違い |
|---|---|---|
| 格子と詳細 | 商会の買う・売る、ライブラリの各分類 | 分け目が1160と1180。ライブラリだけ一覧の上に「◆名前＋数」の段がある。詳細の面の並びが違う |
| 行と詳細 | 商会の合成・品質強化 | 合成の詳細は絵が左、買うの詳細は絵が上で中央 |
| 全幅の一覧 | 商会のスキル覚醒、ライブラリの日々の歩数 | 覚醒は全幅、日々の歩数は中央の細い行 |
| 冒険者のシート | 育成・装備・スキル・突破 | 分け目が760と860 |

### Webで調べたこと

- 画面は、画面全体を入れ替える画面・常に出す表示・一時的に重ねる窓の3つに分けて考えるのが一般的である（[韓国のゲーム開発ガイド](https://wikidocs.net/371321)）。スマホのRPGの主な画面は8〜12種類程度である（資料からの推定）。
- 小さな部品を決めて組み合わせ、テンプレートは「部品を置く場所」を決めて中のデータは持たない（[Atomic Design](https://www.uxpin.com/studio/blog/atomic-ui-components/)）。Unityでも、画面ごとに大きく作るより部品を組み合わせるほうがよいとされる（[Sybo Gamesの講演の紹介](https://medium.com/swlh/the-importance-of-component-based-ui-design-666e5dfc7c1a)）。
- 色・余白・文字は、生の値と役割の名前（幕・面・強調など）の2段で持ち、文字の大きさは段階を決めておく（[SAP Fiori の Design Tokens](https://www.sap.com/design-system/fiori-design-ios/v26-4/foundations/design-tokens)、[Penpot の解説](https://penpot.app/blog/what-are-design-tokens-a-complete-guide/)）。
- デレステは基本のルールと最小の部品でUIを組み（[CEDEC 2016](https://gamebiz.jp/news/168065)）、プリコネは画面の階層が深いほど共通の素材を多く使う（[CEDEC 2018](https://gamebiz.jp/news/218678)）。
- UnityではPrefabの入れ子と派生（Variant）で共通の形を作れる（[Unity Learn](https://learn.unity.com/tutorial/challenge-ui-menu-with-prefab-variants-nested-prefabs)）。ただし、このプロジェクトはコードでPrefabを作り直す作りのため、Variantは使わない（[方針](#方針)）。

ゲームのUI向けの公式な決まりは見つからなかった。上は一般的なデザインシステムの考え方と、開発者の講演・記事から読み取った傾向である。

## 方針

### 層を3つに絞る

Atomic Designの5つの層（原子・分子・生体・テンプレート・ページ）は細かすぎるため、次の3つにする。

| 層 | 中身 | 置き場所 | 名前の例 |
|---|---|---|---|
| 基本の値 | 色（役割の名前で持つ）、文字の大きさの段階、余白の段階、押せる大きさ、窓の幅、パネルの分け目 | 実行時の共通の型（`Shared/UI`）。実行時のコードからも読む | `UiPalette`（色）、`UiType`（文字）、`UiSpace`（余白・大きさ） |
| 部品 | 窓のボタン、選択肢、文字の決めるボタン、丸いチェック、×、「◆見出し」、ルーンの数、品質と☆、縦の分類、状態の文字、持ち物の札 | `Shared/UI` の生成処理と、必要なら実行時の部品 | `GuideFrameAssets.WindowButton`、`ItemTileAssets.Build` |
| テンプレート | 画面の骨組み。部品を置き、中身を入れる場所（スロット）を返す | `Shared/UI` の生成処理 | `GuideFrameAssets.ListWindow`、`GuidePanelTemplate.GridDetail` |

### 作り方の決まり

- 画面のPrefabは、今と同じくコードで作る（[停止中の表示の切り替え](../rules/ui-design.md#停止中の表示の切り替え)の仕組みと、生成のメニューを保つ）。
- テンプレートは、画面のコードに「入れる場所」を返す関数にする。第0段の `ListWindow` が見本である。
- 部品とテンプレートは、新しい値を自分で決めず、基本の値を読む。
- 画面ごとの値（その画面だけの絵の大きさなど）は残してよい。ただし色・文字の大きさ・窓の幅・分け目は基本の値から選ぶ。
- 実行時の部品の型（`*Widget` など）を1つにするときは、画面のコード（`*View`）の読み書きもまとめて直し、PlayModeテストで操作を確かめる。
- 共通にしない物：戦闘・探索・ホームの常に出す表示の中身、召喚・合成・戦闘後の強化の演出、山札の一覧（全面の重ね）。これらも、色と幕と決めるボタンは基本の値と部品を使う。

## 第0段（済み）：窓・幕・縦の分類・札の位置

2026-11-01に実装した（commit前）。

| 作ったもの・変えた画面 | 内容 |
|---|---|
| `UiPalette.Veil` | 窓の後ろの暗い幕の色。ダイアログ、一覧の窓、確かめる窓、冒険の結果、焚き火、連携のモーダルで使う |
| `GuideFrameAssets.ListWindow` | 一覧の窓（幕、窓、左上の「◆題」、×、題の下の金の線）。神殿の結果と確率、旅の案内所のお宝を置き換えた |
| `GuideFrameAssets.WindowButton` | 窓のボタン（決める方だけ金の縁で光る）。レベルアップの確認、召喚の「もう一度」 |
| `GuideFrameAssets.SideTabs` | 縦の分類（準備中は錠前）。商会とライブラリの装備 |
| 連携のモーダル | 青緑の窓とボタンを、ほかの窓と同じ紺の窓と金の縁にした |
| 持ち物の札 | 召喚の結果と冒険の結果の札の、品質の文字を左上、数を右下にした |
| 文書とスキル | [共通の部品](../rules/ui-design.md#共通の部品)、スキル [ui-tone-manner](../../.agents/skills/ui-tone-manner/SKILL.md) |

## 対応表

ユーザーが了承した合わせ方である（2026-11-01）。

| テンプレート | 骨組み | 合わせる画面 | 少し変わる所 |
|---|---|---|---|
| A 格子と詳細 | 左に縦の分類（なければ見出し）、中央に札の格子、一覧の上に「◆名前＋数」の段、右に詳細の面、右下に値段と決めるボタン | 商会の買う・売る、ライブラリの7分類 | 分け目を1160にそろえる（ライブラリは1180）。商会にも見出しの段が付く。売るは右の面が「売る物」の一覧のまま |
| B 行と詳細 | Aの格子を行の一覧にしたもの | 商会の合成・品質強化 | 合成の詳細の並びを、買うの詳細と同じ（絵が上で中央）にする。強化の前後の札とゲージは、強化だけの中身として残す |
| C 全幅の一覧 | 中央に行を並べる | 商会のスキル覚醒、ライブラリの日々の歩数 | 日々の歩数の行を覚醒と同じ全幅にする |
| D 冒険者のシート | 左に人の列、左右の面、右にタブの列（すでに共通） | 育成・装備・スキル・突破 | 分け目は760と860の2種類のまま、テンプレートで選ぶ形にする |
| E 窓 | 知らせる・一覧・確かめる・結果 × 大きさ（小1000・中1400・大1840） | ダイアログ（1360を中に）、連携（1200を中に）、冒険の結果（1440を中に）、レベルアップ（小）、お宝（大）、召喚（中）、地図の出来事（第4段） | 窓の幅が少し変わる |
| F 案内人の画面・端の表示つきの画面・全画面の重ね | 今のまま | 案内人の画面、ホーム、戦闘、探索、召喚、戦闘後の強化 | 暗い幕を2段階（窓用と全画面の選択用）にそろえる |

## 各段で共通の進め方

どの段も、次の順で進める。

1. **前を撮る**：その段で変える画面を、停止中の表示の切り替えで撮る。第0段のあとの状態を前とする。
2. **対応表を見せる**：段の中で、合わせ方が対応表から外れる所が出たら、作業の前にユーザーに見せる。
3. **作る**：基本の値・部品・テンプレートを足し、画面の生成処理を置き換える。同じ見た目の生成処理を画面の中に残さない。
4. **組み直す**：変えた画面の生成メニュー（`Baryonyx > ... > Create Screen Assets`）を実行し、Prefabとシーンを作り直す。
5. **後を撮る**：同じ状態を撮り、前と後を並べた画像を作る。変わった画面だけを集めて送る。
6. **テスト**：変えた画面のPlayModeテストと、関係するEditModeテストを実行する（[作業中に実行するPlayModeテスト](../rules/client-testing.md#作業中に実行するplaymodeテスト)）。
7. **副作用を戻す**：`git status` で、撮影や組み直しの副作用を確かめて戻す（[注意すること](#注意すること)）。
8. **記録する**：画面の仕様と[共通の部品](../rules/ui-design.md#共通の部品)を同じ変更で直し、この計画の[実装の記録](#実装の記録)に書く。
9. **報告する**：変えた画面、前後の画像、合わせなかった所と理由、確かめていないことを分けて書く。commitはユーザーに頼まれてから行う。

## 第1段：基本の値と部品

見た目はほぼ変えず、作りを1つにする。

| 作業 | 内容 | 主に触るファイル |
|---|---|---|
| 色の役割の名前 | 幕（窓用・全画面の選択用の2段階）、面（濃い・薄い）、帯、札の板（案内人の画面用・景色の上用）、線（区切り・仕切り・強調）を、役割の名前で `UiPalette` に置く。`GuideFrameAssets`・`GlassCardAssets` の色の定数は、そこを読む形にする | [UiPalette](../../client/Assets/Baryonyx/Shared/UI/UiPalette.cs)、[GuideFrameAssets](../../client/Assets/Baryonyx/Shared/UI/GuideMenu/Editor/GuideFrameAssets.cs)、[GlassCardAssets](../../client/Assets/Baryonyx/Shared/UI/GlassCard/Editor/GlassCardAssets.cs) |
| 文字の大きさの段階 | 全画面の分布から7〜8段階を決め、`UiType` に名前で置く（例：小さい注記、札の名前、本文、見出し、窓の題、大きな題）。段階の表は作る前にユーザーに見せる | 新規：`Shared/UI/UiType.cs` |
| 余白と大きさ | 余白（8・16・24・48）、押せる大きさ（128）、窓の幅（小・中・大）、パネルの分け目（一覧用1160、シート用760・860）を `UiSpace` に置く | 新規：`Shared/UI/UiSpace.cs`、`GuidePanelLayout` |
| ×のボタン | 6か所の×を `GuideFrameAssets.CloseButton` にまとめる | 商会の強化、編成のスキル、冒険者の一覧、戦闘の山札、神殿、旅の案内所 |
| 状態の文字 | 「取得できませんでした」「読み込み中…」を出す6か所を `GuideFrameAssets.ListStatus`（一覧の欄の上の段の中央）にまとめる | 商会の各パネル、ライブラリ、冒険者の一覧と付け替え、装備、編成のスキル |
| ルーンの数 | ルーンのアイコンと数を並べる9か所を `GuideFrameAssets.RuneAmount` にまとめる | 商会、レベルアップ、お宝の窓、冒険の結果、世界地図など |
| 窓のボタン | ダイアログの選択肢と連携のモーダルのボタンを、`WindowButton`（配置を自動で並べる版も足す）で作る | [GameDialogAssets](../../client/Assets/Baryonyx/Shared/UI/Dialog/Editor/GameDialogAssets.cs)、[HealthLinkModalAssets](../../client/Assets/Baryonyx/Features/Health/Editor/HealthLinkModalAssets.cs) |
| 持ち物の札 | 札を1つの部品にする。生成処理 `ItemTileAssets.Build(大きさ, 載せる物)` と、実行時の部品 `ItemTile`（絵、品質、数、☆、名前、右上の印、影と「？」）。大きさは小（商会の格子・冒険の結果）・中（ライブラリ・お宝）・大（召喚の結果）。6種類の部品の型を順に置き換える | 新規：`Shared/UI/ItemTile/`。商会・ライブラリ・お宝・召喚・冒険の結果の生成処理と画面のコード |

完了条件：

- 上の部品の生成処理が1か所にあり、画面の生成処理の中に同じ組み立てが残っていない（`IconClosePath`・`"Status"`・ルーンのアイコンの検索で確かめる）。
- 画面の生成処理の中で決めた色の定数のうち、幕・面・札・線に当たる物が `UiPalette` を読んでいる。
- 持ち物の札の部品の型が `ItemTile` の1つになっている。
- 変えた画面の前後の画像で、見た目の違いが文字の大きさの1段階以内に収まっている。
- 関係するテストが通る。

## 第2段：窓（E）と格子と詳細（A）

| 作業 | 内容 | 主に触るファイル |
|---|---|---|
| 窓のテンプレート | 知らせる・一覧・確かめる・結果の4つを、大きさ（小・中・大）を選んで作る形にする。見出しの段・本文の場所・下のボタンの段を返す。第0段の `ListWindow` を広げる | [GuideFrameAssets](../../client/Assets/Baryonyx/Shared/UI/GuideMenu/Editor/GuideFrameAssets.cs)、[GameDialogAssets](../../client/Assets/Baryonyx/Shared/UI/Dialog/Editor/GameDialogAssets.cs) |
| 窓の置き換え | ダイアログ（1360→中）、連携（1200→中）、冒険の結果（1440→中）、レベルアップ（小）、お宝（大）、召喚の結果と確率（中）をテンプレートから作る | 各画面の生成処理 |
| 格子と詳細のテンプレート | `GuidePanelTemplate.GridDetail`：左の縦の分類か見出し、一覧の上の「◆名前＋数」の段、札の格子、状態の文字、右の詳細の面、右下の値段と決めるボタンの場所を返す。分け目は1160 | 新規：`Shared/UI/GuideMenu/Editor/GuidePanelTemplate.cs` |
| 詳細の面 | `DetailPaneAssets`：上に絵と光、名前、☆と品質、効果の行、本文、下に値段と決めるボタン。実行時の部品 `DetailPane` を足し、商会の買う・合成とライブラリの詳細から使う | 新規：`Shared/UI/GuideMenu/`。商会の買う、ライブラリの詳細 |
| 画面の置き換え | 商会の買う・売る、ライブラリの7分類をテンプレートから作る。ライブラリの分け目を1160にする。商会に見出しの段を足す | [WorkshopBuyAssets](../../client/Assets/Baryonyx/Features/Workshop/Editor/WorkshopBuyAssets.cs)、[WorkshopSellAssets](../../client/Assets/Baryonyx/Features/Workshop/Editor/WorkshopSellAssets.cs)、[LibraryScreenAssets](../../client/Assets/Baryonyx/Features/Library/Editor/LibraryScreenAssets.cs) と各 `*View` |

完了条件：

- 窓の幅が小・中・大の3つだけになっている（地図の出来事の窓は第4段）。
- 商会の買う・売る、ライブラリの各分類が同じテンプレートから作られ、分け目・見出しの段・詳細の面の並びがそろっている。
- 前後の画像をユーザーに見せ、了承を得た。
- 商会・ライブラリ・Top・冒険のPlayModeテストが通る。

## 第3段：行と詳細（B）、全幅の一覧（C）、冒険者のシート（D）

| 作業 | 内容 | 主に触るファイル |
|---|---|---|
| 行と詳細 | `GuidePanelTemplate.RowsDetail`：Aの格子を行の一覧にしたもの。商会の合成・品質強化を置き換える。合成の詳細は `DetailPane` の並びにそろえる。強化の右の面（前後の札・ゲージ・石）は、詳細の面の本文の場所に入れる強化だけの中身として残す | [WorkshopCraftAssets](../../client/Assets/Baryonyx/Features/Workshop/Editor/WorkshopCraftAssets.cs)、[WorkshopEnhanceAssets](../../client/Assets/Baryonyx/Features/Workshop/Editor/WorkshopEnhanceAssets.cs) |
| 全幅の一覧 | `GuidePanelTemplate.FullList`：見出しの段、全幅の行、状態の文字。スキル覚醒とライブラリの日々の歩数を置き換え、日々の歩数の行を全幅にする | [WorkshopAwakenAssets](../../client/Assets/Baryonyx/Features/Workshop/Editor/WorkshopAwakenAssets.cs)、ライブラリの日々の歩数 |
| 冒険者のシート | 分け目を `UiSpace` の2種類（760・860）から選ぶ形にし、育成・装備・スキル・突破の生成処理が同じ値を読むようにする | [PartySheetAssets](../../client/Assets/Baryonyx/Features/Party/Editor/PartySheetAssets.cs)、[PartyChangeScreenAssets](../../client/Assets/Baryonyx/Features/Party/Editor/PartyChangeScreenAssets.cs)、育成・装備・スキル・突破の生成処理 |
| 全画面の重ねの幕 | 戦闘後の強化など、全画面で選ばせる画面の幕を `UiPalette` の全画面用の幕にそろえる | [AdventureBoonPickerAssets](../../client/Assets/Baryonyx/Features/Adventure/Editor/AdventureBoonPickerAssets.cs) ほか |

完了条件：

- 合成・品質強化・スキル覚醒・日々の歩数がテンプレートから作られている。
- 冒険者のシートの分け目が、テンプレートの2種類のどちらかになっている。
- 幕の色が `UiPalette` の2段階だけになっている。
- 前後の画像をユーザーに見せ、了承を得た。関係するPlayModeテストが通る。

## 第4段：地図の出来事の窓

別のチャットの地図の作業（家のボタン）が、2026-11-01の時点でcommitされずに止まっている。
地図の出来事の窓は地図のPrefabの中で作られるため、その作業がcommitされてから始める。

| 作業 | 内容 |
|---|---|
| 窓 | 出来事の窓の縁・×・ルーンの札を、窓のテンプレートの部品にそろえる。絵の舞台は出来事ごとの中身として残す |
| ボタン | 「たたかう」「100ルーンで遊ぶ」の中央の暗いカプセルと、選択の出来事の答えのボタンを、窓のボタンにする |
| 通行料の確認 | 複数マスの支払いを確かめる窓を、確かめる窓のテンプレートにする（第0段の撮影では出せなかったため、Play中に出して撮る） |

完了条件：地図の出来事の7種類と通行料の確認の前後の画像をユーザーに見せ、了承を得た。世界地図のPlayModeテストが通る。

## 第5段：文書とスキル

今回のデザインの学びを、文書の正本とスキルに残す。

| 作業 | 内容 | 触るファイル |
|---|---|---|
| 正本の書き直し | [共通の部品](../rules/ui-design.md#共通の部品)を、基本の値・部品・テンプレートの3つの層と、テンプレートA〜Fの一覧を軸に書き直す。各テンプレートの骨組みの図（画像）と、合わせた画面を載せる | [ui-design.md](../rules/ui-design.md) |
| ui-tone-manner の更新 | 新しい画面を作るときに、テンプレートを選んで中身を入れる手順にする。部品の表を、第1段から第3段で作った部品とテンプレートに合わせる | [ui-tone-manner](../../.agents/skills/ui-tone-manner/SKILL.md) |
| 撮影の仕組みを仕上げる | 撮影の台本は、2026-11-01に会話の作業用の置き場から `scripts/` へ移した（状態の一覧、撮る状態の一覧、状態ごとの撮影、Play中の世界地図、シーンごとの一覧、前後の画像）。第1段〜第4段で使って見つけた直しを入れ、世界地図の撮影を1つの台本にまとめる | `.agents/skills/ui-tone-manner/scripts/` |
| 学びの記録 | 次を `references/lessons.md` にまとめる。合わせ方の表を先に見せること。縦の分類を2本並べると読みにくいこと。行の説明が2行に収まる幅を保つこと。撮影の副作用（戦闘シーンに古い上書きが入る、フォントの字が化ける、`runInBackground` が `ProjectSettings` に保存される）とその戻し方 | 新規：`.agents/skills/ui-tone-manner/references/lessons.md` |
| ui-advisor の見直し | 画面構成の判断で、テンプレートの一覧と ui-tone-manner を先に見る手順を足す。重なる記述は ui-tone-manner へのリンクにする | [ui-advisor](../../.agents/skills/ui-advisor/SKILL.md) |
| 撮影の知見 | 停止中の画面の撮り方と字化けの直し方を、[Unity EditorとCLIの知見](../../.agents/skills/shared/unity-editor-notes.md)で最新にする（第0段で一部を足した） | 同左 |
| 索引 | [スキル索引](../../.agents/skills/README.md)と[計画索引](README.md)を更新し、この計画を「完了」にして反映先を書く | 同左 |

完了条件：

- 新しい画面の作り方が「テンプレートを選ぶ→部品を置く→撮って見比べる」の順で ui-tone-manner に書かれ、撮影の台本がそこから使える。
- 正本とスキルの間で、規則・数値を写していない（[docとスキルの使い分け](../rules/documentation-policy.md#docとスキルの使い分け)）。
- 文書の検査が通る。

## 注意すること

- **並行する作業**：同じフォルダーで別のチャットが作業することがある。触るファイルが重なる段は、その作業の状態を確かめてから始める。地図の周りは第4段まで触らない。
- **Editorの札**：生成・撮影・テストの前に `client/ci/editor-lock.py` で札を取る。札は15分までしか持てないため、長い撮影は10回ごとに取り直す。
- **生成と撮影の副作用**：
  - 戦闘シーン（`Battle.unity`）やホーム（`Home.unity`）に、停止中の表示の複製から古い上書き（窓の高さ0、アンカー0など）が入ることがある。撮影の台本は、2026-11-01からシーンを保存せずに替える（`eval/prepare.cs`）。それでも差分が出たら `git checkout` で戻す。
  - 名前のないシーン（`NewScene`）に変更があるまま保存を呼ぶと「名前を付けて保存」の窓が開き、Editorが止まる（2026-11-01に2回起きた）。保存は `EditorScenes.SaveNamed` で行い、評価の終わりには名前のあるシーンを開いておく（[client/AGENTS.md](../../client/AGENTS.md#unityの操作と検証)）。
  - APKのビルドや全アセットの読み込み直しのあと、Editorのメモリの中だけでフォントの字が化ける。撮る前にフォントと影付きの材質を読み直す（[知見](../../.agents/skills/shared/unity-editor-notes.md#フォントのメモリだけの字化け)）。
  - Play中に `Application.runInBackground` を変えると、`ProjectSettings.asset` に保存されることがある。終わったら戻す。
  - フォント（`KiwiMaru.asset`）に新しく出た字が足されるのは正しい変化である。字の表が大きく消えていたら戻す。
- **読みやすさ**：分類やテンプレートに合わせて一覧の幅が狭くなるときは、行の説明が決まった行数に収まるかを撮って確かめる。収まらないときは、合わせずに例外として正本に書く（編成のスキルの例）。

## 完了条件

- テンプレートA〜Fと部品で、対応表のすべての画面が作られている。
- 第1段の完了条件の検索で、同じ組み立てが画面の生成処理に残っていない。
- 各段の前後の画像をユーザーに見せ、了承を得た。
- commitの前に、clientのEditMode・PlayModeの全件と、文書の検査が通る。
- 第5段の文書とスキルが更新されている。
- 端末（エミュレーター）での文字の読みやすさと押しやすさは、ユーザーに確認を頼む。確かめていなければ未確認として残す。

## 未決事項

| 項目 | 決めること |
|---|---|
| 編成のスキルの分類 | 横のタブのままにするか、一覧の行の作りを変えて縦の分類に合わせるか |
| 読み込み中の表示 | 装備・育成・ライブラリの日々の歩数が「読み込み中…」の文字を出している。ほかの一覧のように札の裏面で伏せるか（[loading-concealment](../../.agents/skills/loading-concealment/SKILL.md) の担当） |

## 実装の記録

- 2026-11-01：第0段を実装した。関係するEditModeテスト81件とPlayModeテスト51件が通った。変えた画面の前後の画像16組をユーザーに送った。
- 2026-11-01：編成のスキルを縦の分類にしたが、読みにくいとの指摘で横のタブに戻した（例外として正本に記録）。
- 2026-11-01：この計画を書いた。撮影の台本を `ui-tone-manner/scripts/` へ移し、画像の名前を撮り直しても変わらない形にした。第0段の前の撮影を `output/ui-audit/stage0-before/` にその名前で写した。
- 2026-11-01：ユーザーの「doc/planの最新の内容を実装して」を受け、第1段から第3段を実装した。
  - 第1段：基本の値（`UiPalette` の役割の名前、`UiType`、`UiSpace`）を足し、`GuideFrameAssets`・`GlassCardAssets` の色を読ませた。部品 `CloseButton`・`CloseMark`・`CrossIcon`・`RemoveFace`・`ListStatus`・`RuneAmount`（アイコンの大きさと色を選べる）・並べる窓の `WindowButton` を足し、画面ごとの組み立てを置き換えた。持ち物の札の型を `ItemTile`（`WorkshopGoodRow` のファイルを移して同じGUIDのまま）の1つにし、`LibraryTileWidget`・`TempleSummonCard`・`AdventureResultCell` を消した。お宝の札は `TravelTreasureTile` が `ItemTile` を持つ形にした（分類とIDを持つため）。品質・数・右上の印・「？」は `ItemTileAssets` で置く。
  - 第2段：窓の幅を `WindowSize`（小・中・大）にし、`MessageWindow`・`ButtonRow`・`CenteredWindow` を足した。ダイアログ（1360）・連携（1200）・冒険の結果（1440）を中（1400）にした。`GuidePanelTemplate.GridDetail` を足し、商会の買う・売る、ライブラリの各分類を作り直した。ライブラリの分け目を1160にし、商会に見出しの段（分類の名前と「N件」）を足した。ライブラリの「NEW」を札の右上へ移した。詳細の面の実行時の部品（`DetailPane`）は作っていない（買うとライブラリの詳細の中身が違い、置き換えると見た目が変わるため）。
  - 第3段：`RowsDetail` で合成・品質強化、`FullList` でスキル覚醒・日々の歩数を作り直した。合成の詳細は絵を上の中央に置く並びにし、日々の歩数の行を全幅にした。冒険者のシートの分け目を `UiSpace.SheetSplit`・`SheetSplitWide` にした。山札の一覧・戦闘後の強化・覚醒の場面の幕を `VeilDeep` にした（戦闘後の強化は0.78から0.93へ濃くなった）。
  - 残したもの：ヘッダーとホームの所持ルーン（端の表示の部品）、商会の強化の材料の赤い丸の×（外す印で、閉じる×ではない）、冒険者の一覧の分け目660（シートではない）、お宝の王冠の位置。
  - 確かめたこと：変わった画面146組の前後を1枚ずつ見て、レイアウト崩れを探した。見つけた3つ（品質のない札で絵と数が消える、準備中のタブの錠前が文字に重なる、ホームの「出発」のカードが崩れる）を直した。ホームは、Prefabだけを組み直すとシーンの上書きと合わなくなるため、`HomeSceneSetup.CreateHomeScene` でシーンごと作り直した。EditModeテスト723件と、関係するPlayModeテスト75件（17クラス）が通った。
  - 撮影の途中で「名前を付けて保存」の窓が開き、Editorが2回止まった。保存は名前のあるシーンだけを保存する `EditorScenes.SaveNamed` にまとめ、撮影の台本はシーンを保存せずに替えるようにした（[client/AGENTS.md](../../client/AGENTS.md#unityの操作と検証)）。
- 2026-11-01：文字の大きさは8段階（`UiType`：22・26・30・34・40・44・56・64）でユーザーが了承した。段階の間の値（24・28・32・36など）は寄せずに残す（「少しばらばらだが仕方ない」）。新しく作る画面と直す画面から段階を使う。
