# Health画面のフォント

日本語表示にはNoto Sans CJK JP 2.004、JSONにはNoto Sans Monoを使う。
フォント本体は変更せずに同梱し、Unity APIでTMPの動的フォントアセットを作成する。

| ファイル | 配布元 | ライセンス |
|---|---|---|
| `NotoSansCJKjp-Regular.otf` | [Noto CJK Sans 2.004](https://github.com/notofonts/noto-cjk/blob/Sans2.004/Sans/OTF/Japanese/NotoSansCJKjp-Regular.otf) | [LICENSE.txt](LICENSE.txt) |
| `NotoSansMono-Regular.ttf` | [Noto Sans Mono](https://github.com/notofonts/noto-fonts/blob/main/hinted/ttf/NotoSansMono/NotoSansMono-Regular.ttf) | [LICENSE-Mono.txt](LICENSE-Mono.txt) |

取り込み日：2026-09-13。
取得したファイルのSHA-256は以下のとおり。

```text
NotoSansCJKjp-Regular.otf  68a3fc98800b2a27b371f2fb79991daf3633bd89309d4ffaa6946fd587f375b5
NotoSansMono-Regular.ttf  d9e2b23d19f8230be7146f409a52b1d23117e635e28f2e2892cf91b7382f325b
```

再生成には `Baryonyx > Health > Create Screen Assets` を使う。
既存アセットを再利用するため、再生成しても認証設定の値を上書きしない。
