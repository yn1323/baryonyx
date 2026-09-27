---
name: pixellab-item-prompt
description: >
  手動呼び出し専用。アイテム・武器・防具の説明から、確定したタッチ（スーファミ後期風の描き込みの多いドット絵）で
  32x32のアイコン候補64枚をPixelLab Proで生成し、一覧で見せてユーザーに選ばせる。選ばれた候補を切り出すところまで扱う。
  PixelLab、ドット絵、アイテムの話題というだけでは自動で使わない。
---

# PixelLabでアイテム・武器・防具のアイコンを作る

アイテム、武器、防具のアイコンを、PixelLab Proで32x32の候補64枚として作る。
タッチは、2026-09-25にユーザーが確定したもので、スーファミ後期のような、描き込みと影が多いドット絵である。
輪郭・ディザ・アンチエイリアスは、2026-09-27に決めた[ドット絵の制作規格](../../../doc/art/direction.md#ドット絵の制作規格)に合わせ、セルアウト・ディザあり・輪郭の内側だけの手置きとする。
候補から1つを選ぶのはユーザーであり、このスキルは一覧を見せたところで止める。

## 呼び出し

- ユーザーがこのスキルを明示したときだけ使う。
- PixelLab、ドット絵、アイテムの話題になっただけでは使わない。
- `agents/openai.yaml` の `allow_implicit_invocation: false` を保つ。

## 固定の生成設定

次の設定は、PixFluxとProで比較を重ねてユーザーが採用したものである。
ユーザーの指示がない限り変えない。

| 項目 | 値 |
|---|---|
| 生成方法 | Pro。API `POST /v2/generate-image-v2` |
| サイズ | 32x32。UIでは4倍（128px）で表示する想定（[ドット絵の制作規格](../../../doc/art/direction.md#ドット絵の制作規格)） |
| 候補数 | 64枚（Proは42px以下のサイズで64候補を返す） |
| 画風の見本 | [assets/style-reference.png](assets/style-reference.png)（168x64。キャラクターの立ち絵と、PixFluxで作った鎧・盾・杖を並べたもの） |
| 見本から写す要素 | 色（color_palette）、輪郭（outline）、描き込み（detail）、影（shading）のすべて |
| 背景 | 透明 |
| 生成枠 | 1回につき20〜40回分 |
| 時間の目安 | 2〜4分 |

画風の見本は、以前のキャラクター用スキル（PixelLab版、2026-09-27に削除）の参照画像だった青いローブの魔法使いの立ち絵に、銀・金・赤の色を含むアイテムを並べて作った。
キャラクターの色だけを見本にすると、銀や鮮やかな赤が足りず、刃がクリーム色に、赤い盾が茶色にくすむ。
並べたアイテムが、その不足を補っている。

PixelLab MCPのツールでは、候補の画像ファイルを取り出せない。
そのため [scripts/item_icon.py](scripts/item_icon.py) からREST APIを直接呼ぶ。
スクリプトはPillowを使うので、`uv run --no-project --with pillow python` で実行する。
トークンは環境変数 `PIXELLAB_API_TOKEN`、なければ `~/.claude.json` に登録された `pixellab` MCPサーバーの `Authorization` ヘッダーから読む。
トークンを表示・記録・コミットしない。

## Descriptionの書き方

Descriptionは英語で、次の順に1〜3文で書く。

1. **名前と種類**：`<英語の名前>, <段階> <種類> RPG item icon`。段階は `ultimate late-game`、`mid-game`、`basic early-game` のように書く。
2. **向き**：種類ごとに次の表から選ぶ。
3. **材質・色・装飾**：3〜5個に絞る。色は `silver`、`gold`、`bright red`、`royal blue` のように具体的に書く。

スクリプトは、Descriptionの末尾に次の文を自動で足す。

```text
Richly detailed, densely shaded late SNES era JRPG item sprite, light from the top-left, selective outline (dark on the shadow side, lighter on the lit side), dithered shading transitions, hand-placed anti-aliasing only inside the outline, filling the whole canvas, clear silhouette.
```

| 種類 | 向き |
|---|---|
| 剣、斧、槍、杖、弓などの細長い武器 | `drawn diagonally from bottom-left to top-right`。柄や握りを左下に置く |
| 鎧、盾、兜、服 | `front view` |
| 指輪、首飾り、薬、素材などの小物 | `front view`。小さい物でも、キャンバスいっぱいに描かせる |

細長い武器を縦に描かせると、32x32では幅が数ドットしか残らず、拡大表示すると細く頼りない。
斜めにすると、対角線の長さを使えるので、装飾を大きく描ける。

細かい装飾は、刃や柄ではなく、鍔、杖の頭、柄頭、胸の中央のような広い場所に集める。
斜めに描いた刃の幅は3〜4ドットほどしかなく、象嵌のような模様は描かれない。

例（終盤の杖）：

```text
archmage staff, ultimate late-game magic staff RPG item icon, drawn diagonally from bottom-left to top-right. Dark wood shaft wrapped with gold bands, ornate golden head shaped like crescent wings holding a large glowing blue crystal orb, small hanging charms.
```

## 手順

1. **アイテムを確認する**：名前、種類、入手する段階、見た目の特徴を確認する。足りない特徴は、名前と段階に沿う最小限だけ補う。
2. **Descriptionを作る**：作ったDescriptionを短く示す。ユーザーが生成まで依頼している場合はそのまま進め、Descriptionの相談だけを求めている場合は止める。
3. **残高を確認する**：PixelLab MCPの `get_balance` で残りの生成枠を確認する。40回分に満たなければ、送らずにユーザーへ伝える。
4. **生成を依頼する**：出力先は作業用の一時フォルダー（セッションのscratchpadなど）にし、リポジトリには置かない。アイテムごとに出力先を分ける。

   ```bash
   uv run --no-project --with pillow python .agents/skills/pixellab-item-prompt/scripts/item_icon.py submit \
     --description "<Description>" --out <出力先フォルダー>
   ```

   PixelLabは同時に8件までしかジョブを受け付けない（Tier 1）。
   複数のアイテムをまとめて送るときは、ほかの作業のジョブも数に入るので、`list_jobs` で空きを確かめる。
5. **完成を待つ**：PixelLab MCPの `wait_for_jobs` を `timeout_seconds` 240以下で呼んで待つ（費用はかからない）。300秒を超えて応答がないと、Claude側でツール呼び出しが打ち切られる。MCPを使えない場合は、次の `fetch` が完成まで待つ（標準で540秒）。
6. **候補を取り出す**：

   ```bash
   uv run --no-project --with pillow python .agents/skills/pixellab-item-prompt/scripts/item_icon.py fetch --out <出力先フォルダー>
   ```

   出力先に、候補 `cand-01.png`〜`cand-64.png`、切り分け前の並びに戻した `sheet.png`（256x256）、一覧ページ `index.html` ができる。
7. **一覧を見せて止める**：`index.html` をユーザーが画像を見られる方法で表示する（Claudeのデスクトップアプリでは `SendUserFile` の `display: "render"`）。候補の傾向（色違い、形違い）と、割れた可能性のあるマスを短く伝え、ユーザーの選択を待つ。候補を選んだり、Unityへ取り込んだりしない。
8. **選ばれた候補を切り出す**：

   ```bash
   uv run --no-project --with pillow python .agents/skills/pixellab-item-prompt/scripts/item_icon.py crop \
     --out <出力先フォルダー> --cell <番号> --name <ファイル名>
   ```

   32x32の透明キャンバスの中央に置いた `<ファイル名>.png` と、4倍に拡大した確認用の画像ができる。
   切り出した画像をユーザーに見せ、PNGの場所を伝える。

## 割れた候補の扱い

Proは、64候補を8x8のマス目に描いた1枚の画像を切り分けて返す。
絵がマス目からずれて描かれると、1つの候補が上下や左右の2マスに割れる。
実際に、盾では7段しか描かれず、描き始めも16ドット下にずれたため、64マスすべてが割れた。
鎧では、4段目の8つが縦2マス分の全身鎧として描かれた。

このため、一覧は切り分け前の並び（`sheet.png`）で表示する。
`fetch` は、隣のマスと絵がつながっているマスを赤枠で示す。
赤枠の候補が選ばれたら、`sheet.png` を見て絵の範囲を決め、`crop --box <X> <Y> <幅> <高さ>` で切り出す。
隣の絵と接している場合は、隣の絵の一部が入っていないかを確認用の画像で確かめる。
32x32に収まらない絵（全身鎧など）は、そのままの大きさで保存し、ユーザーに伝える。

## 試して採用しなかった方法

| 方法 | 結果 |
|---|---|
| PixFluxで1枚ずつ作る | 輪郭が途切れやすく、ざらついた。別のアイテム（杖の依頼で盾）が描かれることもあった |
| PixFluxで、キャラクターの色だけをパレットとして強制する | 色の統一感は出たが、銀の刃がクリーム色に、赤い盾が茶色にくすんだ |
| 装飾の少ない基本の武器で画風を比べる | 刃が1〜2ドット幅の線になり、画風の差を判断できなかった。比べるときは装飾の多い終盤の装備を使う |
| 細長い武器を縦に描かせる | 幅が数ドットになり、96pxで見ると細く弱い |
| APIへ渡す画像のbase64に `data:image/png;base64,` を付ける | エラーにならないまま、パレットや見本が無視されることがあった。付けずに `type`・`format` を添えて渡す |
