# ゲーム画面のフォント

ゲーム内のTextMeshPro表示にはDotGothic16を使う。画面、ボタン、戦闘中の数値、JSON表示を同じ書体で統一し、Unity APIでTMPの動的フォントアセットを作成する。

| ファイル | 配布元 | ライセンス |
|---|---|---|
| `DotGothic16-Regular.ttf` | [DotGothic16](https://github.com/fontworks-fonts/DotGothic16) | [DotGothic16-OFL.txt](DotGothic16-OFL.txt) |

取り込み日：2026-09-22。
取得したファイルのSHA-256は以下のとおり。

```text
DotGothic16-Regular.ttf  155da8f318553c11d9dffc2affbc7c2114c6a46f9740bcf639ed5568af92be71
```

再生成には `Baryonyx > Health > Create Screen Assets` を使う。
既存アセットを再利用するため、再生成しても認証設定の値を上書きしない。
