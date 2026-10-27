---
name: image-compression
description: >
  画像（PNG・JPEG。背景・イラスト・UIの絵・地図・テクスチャ）をUnityの `client/Assets/Baryonyx/` へ追加・差し替え・移動するとき、
  生成画像を採用してゲームに入れるとき、APKが大きい・重いと言われたとき、画像の取り込み設定（圧縮・ミップマップ・最大サイズ）を変えるときに必ず使う。
  元の画像を残したまま、スマホ向けにはASTCで圧縮して入れ、圧縮されたことと容量を確かめる。
  `.aseprite` のドット絵の制作と取り込みは pixel-art-standards、APKのビルドと配布は unity-android-ci-distribution の担当とする。
---

# 画像を圧縮してゲームに入れる

画像の圧縮は必須である。
決まり（対象、形式、ミップマップ、検査）の正本は[画像の圧縮](../../../doc/rules/frontend-design.md#画像の圧縮)で、このスキルは進め方だけを持つ。

## 進め方

1. **元の画像を残す**：生成・加工した画像は、元の画質のまま `client/Assets/Baryonyx/` の使う場所に置く。圧縮した画像で元を上書きしない。大きすぎて分ける画像（入れ子の地図の背景など）は、分ける前の1枚を `output/`（Git対象外）に残し、作り方を `client/ArtSource/` に記録する。
2. **取り込む**：Unityが読み込むと、[MobileTextureCompression](../../../client/Assets/Baryonyx/Editor/Art/MobileTextureCompression.cs) がAndroidとiOSの圧縮を自動で掛ける。生成スクリプトで取り込み設定を書くときも、スマホの形式は書かない（決まりを1か所に保つ）。2の累乗でない大きさの画像にはミップマップを付けない。
3. **形式を変えたいとき**：画像ごとに例外を作らず、正本の決まりと `MobileTextureCompression` を直し、`GetVersion` を上げる。変える前に、圧縮した見た目を元の画像と並べてユーザーに見せる。
4. **確かめる**：
   - [MobileTextureCompressionTests](../../../client/Assets/Baryonyx/Tests/EditMode/MobileTextureCompressionTests.cs) を実行する。
   - 画面を撮り、にじみやブロックの境目が目立たないかを見る。
   - APKをビルドしたときは、`client/Logs/Editor.log` の「Used Assets and files」で、追加した画像の容量を確かめる。圧縮されていれば、ASTC 6×6で1画素あたり約0.44バイト、8×8で約0.25バイトになる。1画素4バイト前後なら圧縮されていない。
5. **展示室**：画像を追加・変更したら、[クライアントアセット展示室](../../../doc/features/showcase.md)の登録とプレビューも更新する。
