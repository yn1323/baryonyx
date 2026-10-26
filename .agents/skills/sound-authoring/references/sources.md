# 素材の探し方と参考資料

入手先と規約の正本は[効果音とBGM](../../../../doc/rules/sound.md)と[外部素材のクレジット](../../../../doc/rules/credits.md)にある。
ここには、サイトから候補を探して落とすときの実際のやり方を置く（2026-10-26に確かめた）。

## 効果音ラボ

- 分類ごとのページに、音の一覧が並ぶ。例：[戦闘](https://soundeffect-lab.info/sound/battle/)（剣・刀・ナイフ・斧・鞭・弓・ドラゴン）、[ボタン・システム音](https://soundeffect-lab.info/sound/button/)（決定ボタンを押す、など）。
- 配布はmp3だけである。ページのHTMLにある `href="mp3/<英語の名前>.mp3" download="<日本語の名前>.mp3"` から、ファイルと音の名前の組を読み取れる。
- ファイルのURLは `https://soundeffect-lab.info/sound/<分類>/mp3/<英語の名前>.mp3` である。

```bash
curl -sL -A "Mozilla/5.0" https://soundeffect-lab.info/sound/battle/ | grep -o 'href="[^"]*\.mp3"[^>]*>'
```

## 魔王魂

- 分類ごとのページに音が並び、2ページ目以降は `page/<番号>/` で開く。例：[戦闘効果音](https://maou.audio/category/se/se-battle/)（斬る、殴る、刺す、爆発、銃）、[システム音](https://maou.audio/category/se/se-system/)。
- 1つの音にmp3・ogg・wavがある。Unityへ入れる音はwavを落とす。
- ファイルのURLは `https://maou.audio/sound/se/<名前>.<拡張子>`（例：`maou_se_battle01.wav`）である。音の説明（「斬る音。」など）は、ページの本文で各音の再生部分の直前にある。
- 魔王魂のページから来たことを示さないと、ファイルは403で拒否される。curlでは `-e "https://maou.audio/category/se/se-battle/"` のように、そのページを参照元として付ける。

## 参考資料

| 資料 | 使う場面 |
|---|---|
| [AudioClipの取り込み設定（Unity 6.3）](https://docs.unity3d.com/6000.3/Documentation/Manual/class-AudioClip.html) | Load Type、Compression Format、Force To Mono、Preload Audio Dataの意味 |
| [Audio Random Container（Unity）](https://docs.unity3d.com/Manual/AudioRandomContainer-UI.html) | 数種類の音から選んで鳴らす。同じ音の繰り返しを避ける |
| [Audio Manager（Unity 6.3）](https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/editor-settings-reference/comp-manager-group/class-audio-manager) | DSP Buffer Size（遅れと処理の重さのつり合い）、同時に鳴らせる数 |
| [About sound（Material Design 2）](https://m2.material.io/design/sound/about-sound.html) | UIの音の考え方。まれな大事な場面の音（hero sound）と、鳴らさない選択 |
| [Approaching UI Audio from a UI Design Perspective（Audiokinetic）](https://www.audiokinetic.com/en/approaching-ui-audio-ui-design-perspective-2) | UIの音を、押す・離す・取り消すなどの操作の状態から考える |
