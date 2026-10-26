# UIの音

どの画面でも、ボタンを押したときに鳴らす音を置く。
いつどの音を鳴らすかは[押したときの音](../../../../../../doc/rules/sound.md#押したときの音)にある。

2026-10-26に、ユーザーが候補を聞いて選んだ。
決定・戻る・選ぶは同じ音とし、押せないボタンだけ別の音にした。
ユーザーの「少し小さめ」で、再生の音量を `UiSoundBank.asset` で下げている（押した音0.5、押せない音0.3）。

| ファイル | 配布元 | 元の名前 | 加工 | 取り込み日 |
|---|---|---|---|---|
| `UiPress.wav` | [効果音ラボ（ボタン・システム音）](https://soundeffect-lab.info/sound/button/) | カーソル移動1（`cursor1.mp3`） | `sfx.py fit cursor1.mp3 UiPress.wav --mono --peak -1` | 2026-10-26 |
| `UiDenied.wav` | [効果音ラボ（ボタン・システム音）](https://soundeffect-lab.info/sound/button/) | ビープ音2（`beep2.mp3`） | `sfx.py fit beep2.mp3 UiDenied.wav --mono --peak -1` | 2026-10-26 |

`sfx.py` は [sound-authoring](../../../../../../.agents/skills/sound-authoring/SKILL.md) のスクリプトである。
取り込み設定と `UiSoundBank.asset` は、`Baryonyx > Audio > Create UI Sound Assets` で作り直す。
