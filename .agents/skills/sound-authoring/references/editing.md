# 音の測り方と加工

`scripts/sfx.py` は ffmpeg と ffprobe を呼ぶ。
`python3 -I` で、スキルのフォルダーの外から呼ぶ。
加工した音は16bitのWAVで書き出し、最後にピークを−1dBFS（`--peak` で変更）にそろえる。

## 測る

```bash
python3 -I .agents/skills/sound-authoring/scripts/sfx.py inspect output/sound/slash/*.mp3 --png output/sound/slash/png
```

長さ、サンプリング周波数、チャンネル数、ピーク、RMS、ラウドネス（0.4秒未満の音は測れない）、頭と終わりの無音（−50dB以下）を出す。
`--png` を付けると、上に波形、下に周波数の画像を書き出す。

画像の読み方は次のとおり。
見立ては目安であり、ユーザーが聞いた感想を優先する。

| 見るところ | 読み取れること |
|---|---|
| 波形の左端 | 立ち上がりの鋭さ。左端からすぐ太いほど、押した瞬間に鳴る |
| 波形の右へ細くなる形 | 余韻の長さ。長く尾を引くほど、連打で重なりやすい |
| 周波数の画像の上のほう（高い音）が明るい | シャリッとした鋭い音、金属の音 |
| 周波数の画像の下のほう（低い音）が明るい | 重い音、こもった音、ドンという衝撃 |

## 整える（fit）

```bash
python3 -I .agents/skills/sound-authoring/scripts/sfx.py fit in.mp3 out.wav --fade-out 40
```

頭と終わりの無音を切り、頭に3msのフェードを入れて切り口の「プツッ」を防ぎ、ピークをそろえる。
`--fade-out` は終わりを短く消すミリ秒、`--mono` はモノラルにする。
無音を残したいときは `--keep-start`・`--keep-end` を付ける。
加工の最初にも最後にも使う。

## 長さを演出に合わせる

長い技に合わせるときは、まず拍ごとに音を組み立てる（layer）方法を考える。
1つの音を引き伸ばす方法は、元の長さの1.5倍くらいまでを目安にする。
それより伸ばすと、音がにじんだり、別の音に聞こえたりしやすい。

| やりたいこと | 方法 | 向く音 | 気をつけること |
|---|---|---|---|
| 溜め・当たり・余韻を1つの音にする | `layer` | 長い技、大技 | 拍の時刻は演出から測る。重ねた音の配布元はすべて記録する |
| 重く、低く、長くする | `extend --method slow` | 大剣、ボスの一撃、地響き | 音の高さも下がる。軽い技には使わない |
| 音の高さを保って長くする | `extend --method tempo` | 風、魔法のうなり、続く音 | 斬撃や打撃のような鋭い立ち上がりは、にじみやすい |
| 余韻を足す | `extend --method echo` | 魔法の締め、広い場所の大技 | 連打する攻撃には付けない。前の音と重なって濁る |
| 短くする | `fit --fade-out` | 余韻が長すぎる音 | 消し方が急だと途切れて聞こえる |

```bash
# 2秒に伸ばす（3通り）
python3 -I .agents/skills/sound-authoring/scripts/sfx.py extend fit.wav slow.wav --method slow --to 2
python3 -I .agents/skills/sound-authoring/scripts/sfx.py extend fit.wav tempo.wav --method tempo --to 2
python3 -I .agents/skills/sound-authoring/scripts/sfx.py extend fit.wav echo.wav --method echo --to 2

# 溜めの音に、0.6秒後の斬撃（-3dB）と0.75秒後の衝撃を重ねる
python3 -I .agents/skills/sound-authoring/scripts/sfx.py layer combo.wav charge.wav slash.wav@600:-3 impact.wav@750
```

`layer` の指定は `ファイル@遅らせるミリ秒:音量のdB` で、遅らせる時間と音量は省ける。
伸ばす・重ねる方法を決めかねるときは、2〜3通りを作ってチャットに送り、ユーザーに選んでもらう。

## 動画に重ねる

録画した場面に候補の音を重ねるときは `mux`、動作の時刻を探すときは `frames` を使う。
見せ方の手順は [SKILL.md の音の見せ方](../SKILL.md#音の見せ方) にある。

```bash
# 0.5〜1.5秒を0.1秒おきに並べ、各コマの時刻を出す
python3 -I .agents/skills/sound-authoring/scripts/sfx.py frames rec.mp4 frames.png --start 0.5 --end 1.5 --step 0.1

# 0.8秒に斬撃、1.0秒に衝撃（-3dB）を重ねる。動画の長さは変わらない
python3 -I .agents/skills/sound-authoring/scripts/sfx.py mux rec.mp4 A-slash.mp4 slash.wav@800 impact.wav@1000:-3
```

## 記録する

採用した音は、置いたフォルダーの `README.md` に、元のファイル名と、上のコマンドを引数ごと書く（[置き場所と名前](../../../../doc/rules/sound.md#置き場所と名前)）。
同じ音を作り直すときは、元のファイルを配布元から取り直し、記録したコマンドを順に実行する。
