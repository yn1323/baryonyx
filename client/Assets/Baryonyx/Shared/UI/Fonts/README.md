# ゲーム画面のフォント

ゲーム内のTextMeshPro表示にはKiwi Maruを使う。画面、ボタン、戦闘中の数値、JSON表示を同じ書体で統一し、Unity APIでTMPの動的フォントアセットを作成する。
戦闘中に浮かぶダメージと回復の数字だけは例外で、[戦闘の数字のフォント](../../../Features/Combat/UI/Fonts/README.md)を使う。

2026-10-17に、ユーザーの依頼でドット絵のフォントDotGothic16からKiwi Maruへ変えた。
Zen Maru Gothic、BIZ UDPゴシックと比べ、丸みと手書きのような温かみのあるKiwi Maruをユーザーが選んだ。
太さはRegularだけを同梱する。

| ファイル | 配布元 | ライセンス |
|---|---|---|
| `KiwiMaru-Regular.ttf` | [Kiwi Maru（Google Fonts）](https://fonts.google.com/specimen/Kiwi+Maru) | [KiwiMaru-OFL.txt](KiwiMaru-OFL.txt)（SIL Open Font License 1.1） |

取り込み日：2026-10-17。
[google/fonts](https://github.com/google/fonts/tree/main/ofl/kiwimaru) から取得したファイルのSHA-256は以下のとおり。

```text
KiwiMaru-Regular.ttf  b0c3103b2639f690c1fcb44e060058383174bfd2eb72e6635bc9869b374dee87
```

Kiwi Maruにも三角形の記号（▶・◀）はない。矢印はドット絵の画像で描くか、言葉で示す。

Kiwi Maruは字の上に取る余白（Ascent）が大きく、枠の上下中央にそろえた文字が、字の大きさの約7%下に出ていた。
2026-10-17、ユーザーの依頼で、フォントアセットのAscentを「Cap Line − Descent Line」（48ptで55.68から49.82）に下げ、行の中央が字の中央（Cap Lineの半分の高さ）に来るようにした。
行の間隔（Line Height）は変えていない。値は `GameFontAssets` がフォントアセットを読むたびにそろえる。

再生成には `Baryonyx > Health > Create Screen Assets` を使う。
既存アセットを再利用するため、再生成しても認証設定の値を上書きしない。
