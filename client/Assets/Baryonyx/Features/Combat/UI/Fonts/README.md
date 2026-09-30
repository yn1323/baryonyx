# 戦闘の数字のフォント

戦闘中に浮かぶダメージと回復の数字にだけ、Dela Gothic Oneを使う。
ゲーム全体の文字はDotGothic16で統一しており（[ゲーム画面のフォント](../../../../Shared/UI/Fonts/README.md)）、これはその例外である。
DotGothic16は線が1ドット幅で、戦闘の数字としては細く、迫力が足りなかったため、極太のゴシック体をドット絵の上に重ねる。

| ファイル | 配布元 | ライセンス |
|---|---|---|
| `DelaGothicOne-Regular.ttf` | [Dela Gothic One（Google Fonts）](https://fonts.google.com/specimen/Dela+Gothic+One) | [DelaGothicOne-OFL.txt](DelaGothicOne-OFL.txt)（SIL Open Font License 1.1） |

取り込み日：2026-09-30。
[google/fonts](https://github.com/google/fonts/tree/main/ofl/delagothicone) から取得したファイルのSHA-256は以下のとおり。

```text
DelaGothicOne-Regular.ttf  4ff87a0965f1b0505e5a2c58424bc6ad3cff27e56a82f21c2fc9d6b0e3857ee2
```

TMPのフォントアセット（`DamageNumbers.asset`）には数字0〜9だけを焼き込み、Staticにしている。
このためビルドにはフォント本体（約2.5MB）が入らない。
数字以外の文字を表示すると欠けるため、文字を足すときは [DamageNumberAssets](../../Editor/DamageNumberAssets.cs) の対象文字を変え、フォントアセットを削除してから作り直す。

通常・弱点・回復のマテリアル（`DamageNormal.mat`・`DamageWeak.mat`・`DamageHeal.mat`）は、縁取りの色だけが違う。
グラデーションの色と大きさは、各数字のテンプレート（`BattleInspectScreen.prefab` の `DamageNumber`・`WeakNumber`・`HealNumber`）に持たせる。
どれも `Baryonyx > Combat > Create Battle Inspect Assets` で作り直せる。
