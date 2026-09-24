---
name: pixellab-background-prompt
description: >
  手動呼び出し専用。場面の説明から、HD-2D風の2D背景（奥行きと光のあるドット絵の背景）をPixelLabで作る。
  PixFluxで400x224の下書きを4案出してユーザーに選ばせ、選んだ案を構図の参考にしてProで640x360に仕上げる。
  PixelLab、ドット絵、背景の話題というだけでは自動で使わない。
---

# PixelLabでHD-2D風の2D背景を作る

ゲームの背景画像を、PixelLabで下書きから仕上げまで作る。
目指す見た目は、HD-2D（オクトパストラベラーのような、ドット絵に奥行きと光を足した表現）の背景を、1枚のドット絵として描いたものである。
下書きの段階と仕上げの段階で、それぞれユーザーが結果を見て判断する。

## 呼び出し

- ユーザーがこのスキルを明示したときだけ使う。
- PixelLab、ドット絵、背景の話題になっただけでは使わない。
- `agents/openai.yaml` の `allow_implicit_invocation: false` を保つ。

## 流れと固定の設定

安いPixFluxで構図を4案試し、ユーザーが選んだ1案だけを高品質なProで描き直す。
Proは1回で40回分の生成枠を使うため、構図の当たり外れをPixFluxで先に見ておく。

| 段階 | 下書き | 仕上げ |
|---|---|---|
| モデル | PixFlux（API `POST /v2/create-image-pixflux-background`） | Pro（API `POST /v2/generate-image-v2`） |
| サイズ | 400x224（PixFluxは1辺400まで、縦横とも4の倍数） | 640x360 |
| 枚数 | 4案（seedを変える） | 1枚 |
| 生成枠 | 1案1回分、計4回分 | 40回分 |
| 時間の目安 | 1案20〜45秒 | 1〜3分 |
| 固定の指定 | shading `highly detailed shading`、detail `highly detailed`、outline `selective outline`、view `side`、背景あり | 背景あり。選んだ下書きを参考画像として渡し、構図・視点・層の配置を保つよう指示する |

PixelLab MCPのツールでは、画像のファイルを取り出せず、Proに下書きを参考画像として渡すのも難しい。
そのため [scripts/background.py](scripts/background.py) からREST APIを直接呼ぶ。
トークンは環境変数 `PIXELLAB_API_TOKEN`、なければ `~/.claude.json` に登録された `pixellab` MCPサーバーの `Authorization` ヘッダーから読む。
トークンを表示・記録・コミットしない。

## Descriptionの書き方

Descriptionは英語で書き、下書き用の短縮版と仕上げ用の詳細版の2つを作る。
PixFluxは長い指示を処理しきれず、細部を詰め込むと構図が崩れやすいため、短縮版は900文字程度に収める。
詳細版は2000文字以内とする。

詳細版は次の5つのまとまりで書く。
短縮版も同じ順序で、各まとまりを1文に縮める。

1. **画風と場面**：`Highly detailed 2D pixel art game background in the style of HD-2D JRPG scenery (Octopath Traveler-like), rendered fully in pixel art. Side view, wide 16:9 scene.` に続けて、場所と時間帯を1文で書く。
2. **手前（Foreground）**：足元の地面、草花、柵、岩など。画面を縁取るので、やや暗くすると書く。
3. **中間（Midground (focus)）**：見せ場となる主題。門、扉、階段、灯り、蔦、旗、木箱のように、形の分かる具体物を並べる。灯りは「床や壁に落ちる暖色の光だまり」として書く。
4. **奥（Background）**：崖や森などの層を重ね、遠くほど青紫のかすみに溶けると書く。遠景の山と空の様子も書く。
5. **描き方（Rendering）**：次の内容を入れる。
   - 暖色の灯りと寒色の影の対比
   - 遠くの層ほど明るく青くなる空気遠近
   - 光とかすみは、ぼかしではなくディザリング（点の混ぜ方で中間色を表す技法）とドットの塊で表す
   - くっきりしたドット、アンチエイリアスなし、文字なし

「光る」「きらめく」のような語を主題に足すと、画面全体が光りやすい。
光は灯りの周りの光だまりとして、場所を限って書く。

場面の説明が短い場合は、場所と時間帯から、各層に置く具体物を補う。
補った具体物は、生成前にユーザーへ示す。

## 手順

1. **場面を確認する**：場所、時間帯、見せ場を確認する。足りない要素は、場面に沿う最小限だけ補う。
2. **Descriptionを作る**：短縮版と詳細版を作り、ユーザーに示す。ユーザーが生成まで依頼している場合はそのまま進め、Descriptionの相談だけを求めている場合は止める。
3. **残高を確認する**：PixelLab MCPの `get_balance` で残りの生成枠を確認する。下書きと仕上げで計44回分に満たなければ、送らずにユーザーへ伝える。
4. **下書きを作る**：出力先は作業用の一時フォルダー（セッションのscratchpadなど）にし、リポジトリには置かない。Descriptionはファイルに書いて渡す。

   ```bash
   python3 .agents/skills/pixellab-background-prompt/scripts/background.py drafts \
     --description-file <短縮版のファイル> --out <出力先フォルダー>
   ```

   4案がそろうまで待ち、出力先に `draft-01.png`〜`draft-04.png` と一覧ページ `index.html` を作る。
   一覧は各案を3倍に拡大し、ドットがぼけないよう最近傍補間で表示する。
   5分を超えて未完成の案があれば終了コード2で終わるので、`fetch --out <出力先フォルダー>` で取り出し直す。
5. **下書きを見せて止める**：`index.html` をユーザーが画像を見られる方法で表示する（Claudeのデスクトップアプリでは `SendUserFile` の `display: "render"`）。各案の構図の違いを短く伝え、ユーザーの選択を待つ。案を選んだり、Proへ進んだりしない。
6. **仕上げる**：ユーザーが選んだ案で、Proに描き直させる。

   ```bash
   python3 .agents/skills/pixellab-background-prompt/scripts/background.py final \
     --description-file <詳細版のファイル> --draft <出力先フォルダー>/draft-02.png --out <出力先フォルダー>
   ```

   出力先に `final.png` ができ、`index.html` の先頭に2倍表示で加わる。
7. **仕上げを見せて止める**：仕上げと、参考にした下書きを見せる。構図がどこまで保たれたかを短く報告する。ユーザーの指示がない限り、Unityへの取り込み、層への分割、展示室への登録へ進まない。

気に入る案がなければ、同じDescriptionで下書きを作り直すか、Descriptionを直すかをユーザーに尋ねる。

## 生成結果の注意

- PixFluxの下書きに、署名のような短い文字（例：右下の「dpvs」）が描き込まれることがある。見つけたら一覧を見せるときに伝える。Proで仕上げる際は、Descriptionの `no text` で消えるかを確認する。
- Proは、Descriptionで色数を増やすよう求めても、30〜40色程度に絞って描く。

## 試して採用しなかった方法

| 方法 | 結果 |
|---|---|
| ChatGPTのImagegen（Codex CLI経由）で描く | 詳細版と同じDescriptionでも、1672x941程度の高解像度で出力され、色数は17万〜40万色になった。640x360に縮小しても約12万色が残り、ドットの境目がにじみ、格子もそろわない。構図と描き込みは優れるが、ドット絵風のイラストであり、ピクセルパーフェクトな背景には使えない |
| Imagegenの指示に「HD-2D」「火の粉」を入れる | 光のにじみや粒子が画像に描き込まれ、全体がきらつく。これらはUnity側の演出で重ねる表現である |
| PixFluxに詳細版のDescriptionで直接仕上げさせる | 大きな黒い塊ができるなど、構図がまとまらなかった |
