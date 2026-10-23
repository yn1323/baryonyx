---
name: item-icon
description: >
  手動呼び出し専用（Claude Code では /item-icon、Codex では $item-icon）。
  アイテム・武器・防具・素材のアイコン候補を、既存のアイテムの絵を画風見本にして Codex CLI の画像生成で作り、
  32×32のドット絵に直した候補の一覧を見せてユーザーに選ばせる。選ばれた候補を `.aseprite` にするところまで扱う。
  ドット絵、アイテム、アイコンの話題というだけでは自動で使わない。
disable-model-invocation: true
---

# Codex CLIでアイテムのアイコンを作る

アイテム・武器・防具・強化素材などのアイコンを、Codex CLIの画像生成で候補として作る。
寸法・色数・輪郭・ディザ・アンチエイリアスは[ドット絵の制作規格](../../../doc/art/direction.md#ドット絵の制作規格)に従い、[pixel-art-standards](../pixel-art-standards/SKILL.md) と一緒に使う。
候補から選ぶのはユーザーであり、このスキルは一覧を見せたところで止める。
PixelLabは使わない（2026-10-24、ユーザーの決定）。

## 固定の設定

2026-10-24、強化素材（磨き石4色と覚醒の欠片）を作ったときに決めた設定である。

| 項目 | 値 |
|---|---|
| 生成 | [codex_image.py](../shared/scripts/codex_image.py)。Codex本体のモデルはスクリプトで固定している。組み込みの画像生成の画像モデルは指定しない |
| 画風の見本 | 既存のアイテム・アクセサリーの `.aseprite` 6枚を4倍にして並べた画像（`item_icons.py style-ref`）。タッチだけを参考にし、形・題材は写さないよう添える |
| 1枚の画像に描く数 | 色違いのシリーズは1枚に横一列（同じ形・同じ向きで色だけを変える）。形の案を比べるときは2×2の格子に4案 |
| 背景 | 単色の明るい灰色。模様・影・床・文字は置かない |
| 仕上げ | `item_icons.py cut` で、マスごとに32×32・透明背景・32色以内にする |
| 時間の目安 | 1枚1〜3分。5枚を並列で送っても、利用上限には当たらなかった |

色違いのシリーズを1色ずつ別の画像で頼むと、形と大きさがそろわない。
1枚に並べて「同じ形・同じ大きさ・同じ向きで、色だけを変える」と書くと、4色とも同じ形で描かれた。
同じ依頼を2〜3回送り、セットごとに選ばせる。

## 依頼文の書き方

依頼文は日本語で、次の順に書く。

1. **何のアイコンを、いくつ、どう並べるか**：「RPGの強化素材『磨き石』のアイテムアイコンを4つ、1枚の画像に横一列に並べて描く」のように書き、ゲームの中での役割を1文添える。
2. **形・色・装飾**：材質と色を具体的に書く（「ロイヤルブルーの石と空色の宝石」）。格が上の物ほど少し豪華にするなら、足す物を1つに絞る（「金だけ、周りに小さな光の粒を2〜3個」）。
3. **大きさと配置**：「1つずつが32×32ドットのアイコンになる大きさと描き込みにする。枠いっぱいに大きく描く」「間を十分に空け、互いに重ねない」。
4. **背景**：「単色の明るい灰色にし、模様・影・床は置かない。文字や番号は入れない」。
5. **「■ドット絵の規格」**：[pixel-art-standards](../pixel-art-standards/SKILL.md#指示文に入れる規格)の「そのほか」の部品を入れる。

```bash
python3 .agents/skills/shared/scripts/pixel_style.py ja touch light colors detail
```

細長い武器は、柄を左下にして右上へ斜めに描かせると、32×32の対角線の長さを使える。
細かい装飾は、刃や柄ではなく、鍔・杖の頭・宝石の周りのような広い場所に集める。

## 手順

1. **アイテムを確認する**：名前、種類、役割、色の違い（シリーズか）を確認する。
2. **作業フォルダーを作る**：`output/<名前>/`（Git対象外）に依頼文を置き、画風見本を作る。

   ```bash
   uv run --no-project --with pillow python .agents/skills/item-icon/scripts/item_icons.py style-ref --out output/<名前>/style-ref.png
   ```

3. **生成を依頼する**：依頼ごとに出力先を分け、同じ依頼を2〜3回、並列で送る。時間がかかるため、裏で動かして完了を待つ。

   ```bash
   uv run --no-project python .agents/skills/shared/scripts/codex_image.py --out-dir output/<名前>/<依頼>-1 --name <依頼> --request output/<名前>/<依頼>.md --image output/<名前>/style-ref.png --image-role "ドット絵の画風見本（このゲームの既存のアイテムのアイコン）。タッチ（ドットの大きさ、塗り、描き込み、セルアウトの輪郭、色の段階）だけを参考にする。形・題材は写さない"
   ```

4. **32×32にする**：生成画像を、描かせた並びの列数×行数で切り分ける。

   ```bash
   uv run --no-project --with pillow python .agents/skills/item-icon/scripts/item_icons.py cut output/<名前>/<依頼>-1/<依頼>.png 4 1 output/<名前>/<依頼>-1/icon
   ```

5. **検査する**：[pixel-art-standards](../pixel-art-standards/SKILL.md#出来上がりの検査)の検査スクリプトを `--kind item` で通し、4倍の一覧を自分で見て、割れたマスや隣の絵が入ったマスがないかを確かめる。
6. **一覧を見せて止める**：候補ページを作り、ユーザーが画像を見られる方法で表示する（Claudeのデスクトップアプリでは `SendUserFile` の `display: "render"`）。色違いのシリーズはセットごとに見出しを分ける。候補の傾向を短く伝え、ユーザーの選択を待つ。

   ```bash
   uv run --no-project --with pillow python .agents/skills/item-icon/scripts/item_icons.py page --out output/<名前>/index.html --group "セットA=output/<名前>/<依頼>-1/icon-1.png,..." --group ...
   ```

7. **選ばれた候補を `.aseprite` にする**：[pixel-art-standards の「PNGから変換する」](../pixel-art-standards/SKILL.md#pngから変換する)で、置き場所（`client/Assets/Baryonyx/Shared/Art/<分類>/<PascalCaseのID>.aseprite`）へ変換する。`cut` の出力（`icon-1.png` など）のように数字で終わるPNGは、Asepriteが連番として隣の画像までコマに読み込む。数字で終わらない名前（`src.png` など）に写してから変換し、書き出して1コマになっていることを確かめる。そのあと、絵を読む生成（`Baryonyx/Inventory/Create Item Art` と、絵を焼き込む画面の生成）を実行し、展示室で確かめる。

## 試して採用しなかった方法

| 方法 | 結果 |
|---|---|
| PixelLab（PixFlux・Pro） | 2026-10-24、ユーザーの決定で使わない。それまでの比較の記録は、git履歴の pixellab-item-prompt スキルにある |
