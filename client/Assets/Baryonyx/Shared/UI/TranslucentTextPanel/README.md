# TranslucentTextPanel

`TranslucentTextPanel.prefab` は、TextMeshProの文字と、灰色の透過グラデーションを重ねたuGUIの共通プレハブです。

## 使い方

1. `TranslucentTextPanel.prefab` をCanvasの子へ配置する。
2. ルートの `TranslucentTextPanel` コンポーネントをInspectorで開き、表示設定を調整する。
3. `RectTransform` で位置と大きさを画面ごとに調整する。
4. 背景の大きさを文字に合わせる場合は、`BackdropCanvas` のサイズをルートより少し大きくする。
5. `PulseEnabled` を有効にすると、Labelだけが約2.4秒周期でゆっくり薄くなり、元の濃さへ戻る。

Inspectorの表示設定では、`Font Size`、`Backdrop Size`、`Backdrop Alpha`を変更できます。点滅設定では、`Pulse Duration Seconds`で1周期の長さ、`Pulse Minimum Alpha`で最も薄いときの濃さを調整できます。

ルート、`Backdrop`、`Label` は入力を受けない設定です。
背面のボタンや画面全体の入力をこのプレハブで遮らないためです。

Top画面では同じプレハブをタイトルと `TAP TO START` に使い、文言、フォントサイズ、位置、点滅設定だけをPrefab Instanceの上書きにしています。タイトルの点滅は無効、`TAP TO START` の点滅は有効です。
