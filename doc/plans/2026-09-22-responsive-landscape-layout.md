---
id: plan-responsive-landscape-layout-20260922
type: reference
status: 記録
updated: 2026-09-22
---

# 横画面の可変レイアウト

画面比率が変わっても、背景、ゲーム本体、操作UIの役割を分けて表示を保つ。

## 方針

- `1920×1080`の設計座標と中央の16:9 `CoreArea`をゲーム本体の基準にする。
- 背景は元画像の比率を保って画面全体を覆い、画面外へはみ出す部分を切り取る。
- ボタン、文字、ナビゲーションはSafe Area配下のアンカーを基準に配置する。
- 19.5:9と20:9ではCore Area外の左右を背景・環境演出へ使い、4:3ではCore Areaを保って上下の余白を許容する。

## 実装範囲

`ResponsiveBackground`は親RectTransformを覆う表示矩形を計算する。
`SafeAreaFollower`は端末の`Screen.safeArea`または指定されたSafe Areaのアンカーを操作UIへ反映する。
タイトル画面、ワイヤー画面の背景、生成元のEditorスクリプト、生成済みシーンとPrefabへ同じルールを適用する。

## 検証

EditModeで背景の比率維持、Safe Areaのアンカーコピー、生成済みワイヤーPrefabの背景コンポーネントを検査する。
Unity EditorのGame Viewで16:9、19.5:9、20:9、4:3、非対称Safe Areaを確認し、Android実機ではタップ位置、システムバー、画面サイズ変更を確認する。
Unity EditorとAndroid実機の確認結果は別々に記録する。
