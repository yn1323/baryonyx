---
name: pixel-art-standards
description: >
  このゲームのドット絵を生成・描画・修正・採用するとき、またドット絵をUnityへ取り込んで画面に置くときに必ず使う。
  PixelLab、Codex・ChatGPTの画像生成、Asepriteスクリプト、手作業のどれで作る場合も、
  アート方針のドット絵の制作規格（素材ごとの寸法、色数、光源、セルアウト、ディザ、輪郭の内側だけのアンチエイリアス）を
  指示文と出来上がりに当てはめ、規格との差を検査して報告し、採用した絵を `.aseprite` にする。
  ドット絵ではないイラストや、画像を扱わないUIレイアウトだけの作業には使わない。
---

# ドット絵の制作規格を当てはめる

このゲームのドット絵を、どの方法で作っても同じ規格にそろえるためのスキルである。
規格の正本は [アート方針のドット絵の制作規格](../../../doc/art/direction.md#ドット絵の制作規格) とし、このスキルは、規格を指示文に入れる方法、出来上がりの検査、`.aseprite` への変換、Unityでの使い方を扱う。
ドット絵は1枚ずつの出来より、全素材が同じ規則で描かれていることで見栄えが決まる。
規格からの例外は素材ごとにユーザーが決め、正本の「例外」に記録する。決めないまま、規格から外れた素材を黙って採用しない。

## 使う場面

- PixelLab（MCP・API）、Codex CLIやChatGPTの画像生成、Asepriteスクリプトでドット絵を作るとき
- 生成した候補や手描きの画像を、採用するか判断するとき・仕上げるとき
- ドット絵をUnityへ取り込み、画面や展示室に置くとき

ほかの生成スキル（[character-sprite-sheet](../character-sprite-sheet/SKILL.md)、[pixellab-item-prompt](../pixellab-item-prompt/SKILL.md)、[pixellab-background-prompt](../pixellab-background-prompt/SKILL.md)、[hd2d-stage-set](../hd2d-stage-set/SKILL.md) のテクスチャ）と一緒に使う。
それらのスキルの固定文は、下の「指示文に入れる規格」と同じ共通の文（[shared/prompts/](../shared/prompts/)）を使っている。
固定文があるスキルでは固定文を使い、このスキルの指示文を重ねて足さない。
固定文と規格の食い違いに気づいたら、生成する前にユーザーへ伝える。

## 保存形式

リポジトリに置くドット絵は `.aseprite` だけにする。置き場所と読み込みの規則は[ゲーム内の対象の画像](../../../doc/rules/frontend-design.md#ゲーム内の対象の画像)とルートの [AGENTS.md](../../../AGENTS.md) の「Asepriteの正本とタブレットの受け渡し」に従う。

- 生成ツールが出力したPNGは、採用前の候補として `output/`（Git対象外）に置く。採用が決まったら `.aseprite` へ変換し、変換後のファイルだけをリポジトリに置く。
- 既存のドット絵がPNGのまま置かれていたら、変更するときに `.aseprite` へ置き換え、参照するコード・データ・文書・展示室の登録を同じ変更で直す。
- PNGで置く必要があると考えたら、置く前に理由を添えてユーザーに確認する。

### PNGから変換する

[scripts/png_to_aseprite.lua](scripts/png_to_aseprite.lua) で、1ドット1ピクセルのPNGを、インデックスカラーの `.aseprite` に変換する。
パレットは0番を透明にし、1番以降に不透明な色を暗い順に並べる。画素は変えない。
Aseprite本体のCLIで、リポジトリ直下から次のように実行する。macOSの本体は `/Applications/Aseprite.app/Contents/MacOS/aseprite` にある。拡張機能（pixellab）の読み込みエラーが毎回表示されるが、変換には影響しない。

```bash
Aseprite.exe -b --script-param src=<PNG> --script-param ase=<保存先の.aseprite> --script .agents/skills/pixel-art-standards/scripts/png_to_aseprite.lua
```

変換する前に、下の検査スクリプトで規格との差を確かめておく。
拡大済みのPNGや、格子を復元していない下書きは変換しない。

## 手順

1. **規格を読む**：正本の「ドット絵の制作規格」と「例外」「既存素材との差」を読む。正本の未決事項（アニメーション規格、背景・タイルの寸法と表示倍率、アイテム・UIアイコンの色味など）は推測で決めず、必要ならユーザーに確認する。
2. **種類と寸法を決める**：作る素材を正本の[画面と寸法](../../../doc/art/direction.md#画面と寸法)の種類に当てはめ、画像の大きさを決める。
3. **指示文に規格を入れる**：固定文を持つスキルを使わない場合は、下の「指示文に入れる規格」を指示文に入れる。
4. **出来上がりを検査する**：採用前の候補は `output/`（Git対象外）に置き、検査スクリプトと目視で確かめる。
5. **差を報告する**：規格との差を、ファイルごとに具体的に報告する。正本の「例外」に記録済みの差は、例外の内容と一致していれば問題として扱わない。規格どおりにしないほうが見栄えや役割に合うと考えたら、理由を添えて例外を提案してよい。例外にするかどうかはユーザーが決め、決まったら正本の「例外」に記録する。例外にせず差を残したまま使う場合は「既存素材との差」に記録する。
6. **`.aseprite` にする**：採用した素材がPNGなら、上の「PNGから変換する」に従って `.aseprite` へ変換する。
7. **Unityで使う**：変換した `.aseprite` を、下の「Unityで使うとき」に従って取り込む。

## 種類と検査の指定

素材の種類ごとの寸法・配置・表示倍率は、正本の[画面と寸法](../../../doc/art/direction.md#画面と寸法)を読んで当てはめる。
このスキルには寸法を写さない。
検査スクリプトは寸法を `KINDS` に持つため、正本を変えたら同じ変更で `KINDS` を直す。

| 正本の対象 | 検査の `--kind` |
|---|---|
| 味方キャラ | `character`（立ち姿は `--standing` も付ける） |
| 敵の小・中・大 | `enemy-small`・`enemy-medium`・`enemy-large` |
| アイテム | `item` |
| UIアイコン | `ui-icon` |
| 背景・タイル | `background`（寸法は検査しない） |

どの素材も、1ドットを1ピクセルとして書き出す。

## 指示文に入れる規格

規格を指示文にした固定文は、[shared/prompts/](../shared/prompts/) の `pixel-style.ja.txt`（日本語）と `pixel-style.en.txt`（英語）の1か所にだけ置く。
ファイルは「[部品名]」の行で区切った部品の集まりで、素材に合わせて部品を選んで並べる。
並べた文は [pixel_style.py](../shared/scripts/pixel_style.py) で出力できる。

### 日本語（Codex CLI・ChatGPTの画像生成）

依頼文に「■ドット絵の規格」の見出しを置き、その下に次の部品を1行ずつ入れる。

| 素材 | 部品（この順） |
|---|---|
| キャラ・敵 | `touch`・`light`・`colors`・`detail`・`character` |
| 背景 | `touch`・`light`・`colors`・`detail`・`background` |
| そのほか（アイテム、UIアイコンなど） | `touch`・`light`・`colors`・`detail` |

```bash
python3 .agents/skills/shared/scripts/pixel_style.py ja touch light colors detail character
```

### 英語（PixelLabのDescription）

`touch`・`core`・`limits` を「, 」でつなぎ、キャラ・敵には `character`、背景には `background` を足す。

```bash
python3 .agents/skills/shared/scripts/pixel_style.py en touch core limits character
```

PixelLabの `outline` 設定を選べる場合は `selective outline` にする。
PixFluxでは、`highly detailed` のような描き込みを増やす語を足すと画風が崩れやすいため、この文も短く縮めて使う。

## 出来上がりの検査

### 検査スクリプト

[scripts/check_pixel_art.py](scripts/check_pixel_art.py) で、機械的に判定できる項目を検査する。
Pillowを使うので、リポジトリ直下から次のように実行する。

```bash
uv run --no-project --with pillow python .agents/skills/pixel-art-standards/scripts/check_pixel_art.py --kind character --standing <PNG>
```

同じ種類のPNGは、まとめて渡せる。
検査スクリプトはPNGだけを読む。リポジトリの `.aseprite` を検査するときは、`Aseprite.exe -b <.aseprite> --save-as output/<名前>.png` で `output/` へ書き出して渡し、書き出したPNGはリポジトリに置かない。
味方キャラの複数フレームをまとめて渡すと、足元の高さが揃っているかも確かめる。

| 検査項目 | 差として扱う条件 |
|---|---|
| 大きさ | 種類ごとの規格と異なる（背景・タイルは表示だけ） |
| 半透明 | 透明度が0と255以外のピクセルがある。背景側へのアンチエイリアスやぼかしの跡である |
| 色数 | 不透明な色が規格の色数を超える |
| 拡大済み | 同じ色の並びがすべてk倍の長さになっている。k倍に拡大して書き出した画像と考えられる |
| 立ち姿 | 規格の立ち姿の範囲からはみ出す、または左右中央から1ドットを超えてずれる |
| 足元 | 複数フレームで足元の行が異なる（注意として表示） |
| 敵の余白 | 本体が画像より2ドット以上小さい（注意として表示） |

Codex CLIやChatGPTが描いた画像は、高解像度の「ドット絵風のイラスト」で出力され、格子も色数も規格に合わない。
この場合は下書きとして扱い、格子を復元して減色してから検査する。

### 目で確かめる項目

検査スクリプトでは判定できないため、拡大表示（最近傍補間）で見て確かめる。

- 光が左上から当たり、影が右下にできているか
- 外周の輪郭が、影側で暗く、光が当たる側で明るいか（セルアウト）
- 内側の線が外周の輪郭より明るいか
- 色の境目にディザが入っているか
- アンチエイリアスの中間色が輪郭の内側にだけあり、背景側にないか
- キャラ・敵が鮮やかな色、背景が落ち着いた色になっているか。背景の輪郭が弱いか
- しわや細部まで描き込まれているか。キャラ・敵の目が点や短い線か

## Unityで使うとき

- 置き場所、`.aseprite` の読み込み、生成スクリプトからの読み込み方、取り込み設定は[ゲーム内の対象の画像](../../../doc/rules/frontend-design.md#ゲーム内の対象の画像)に従う。PNGへ書き出して並べて置かない。
- 表示倍率は正本の[画面と寸法](../../../doc/art/direction.md#画面と寸法)に従う。UIでドット絵を整数倍に保つ部品（`PixelPerfectRawImage`）は[クライアントの構成と依存関係](../../../doc/rules/frontend-design.md)にある。正本の「例外」にない回転や半端な倍率の拡縮をしない。
- Pixel Perfect Cameraを導入する場合、画面全体を基準解像度に描いてから拡大する設定（Upscale Render Texture）にすると背景の細かいドットも粗くなるため、背景の見え方を確かめる（[Unity公式](https://docs.unity3d.com/6000.1/Documentation/Manual/urp/2d-pixelperfect-ref.html)）。
- 画像を追加・変更したら、ルートの AGENTS.md の[クライアントアセット展示室](../../../AGENTS.md#クライアントアセット展示室)に従って展示室の登録とプレビューを更新する。

## 規格を変えるとき

ユーザーが規格を変えたら、同じ変更で次を直す。

1. 正本の [ドット絵の制作規格](../../../doc/art/direction.md#ドット絵の制作規格) と「変更と判断の記録」
2. このスキルの「種類と検査の指定」と、検査スクリプトの `KINDS`・`MAX_COLORS`・`STANDING_BODY`
3. 共通の固定文：[pixel-style.ja.txt](../shared/prompts/pixel-style.ja.txt)・[pixel-style.en.txt](../shared/prompts/pixel-style.en.txt)
4. 共通の固定文とは別に、規格の値や言い換えを持つ所：[character-sprite-sheet](../character-sprite-sheet/SKILL.md) の「■画風」の1〜2行目と「■大きさ」、[item_icon.py](../pixellab-item-prompt/scripts/item_icon.py) の `SIZE` と `STYLE_TAIL` の前後の語、[stage_requests.py](../hd2d-stage-set/scripts/stage_requests.py) の色数の行、[pixellab-background-prompt](../pixellab-background-prompt/SKILL.md) の「描き方（Rendering）」
