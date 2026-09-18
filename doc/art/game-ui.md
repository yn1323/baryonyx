---
id: art-game-ui
type: specification
status: 一部確定
updated: 2026-09-19
---

# 体験版のゲームUIと生成素材

[アート索引](README.md) / [画面の操作仕様](../features/game-wireframe.md)

## 対象と確認状況

2026-09-19の「全画面のワイヤーをUIに置き換える」という依頼に対応する。
ホーム、冒険先、探索、仲間・編成、装備、戦闘、敗北、結果、運動目標、設定の10画面と、初回案内・獲得・確認・目標設定などのダイアログが対象である。
状態遷移とサンプルデータを維持し、見た目と探索入口への短い移動を追加する。

参照タスク「UIの枠をドット絵で作るか検討」は、アプリの読み取りで対象を取得できなかった。
その議論・サンプルとの一致は未確認である。
現段階の配色と素材は、既存のアート方針に基づく実装案として扱う。
Unityでアセットを再生成し、操作・表示を検証した。
UIのPrefabと素材は元のプロジェクトへ反映済みである。

## 見た目と配置

濃い青緑の背景と枠、淡い生成りの日本語、金色の主要操作・選択表示を組み合わせる。
枠は24×24ピクセルでUnityから生成し、6ピクセルの境界を持つ9-sliceで角を保つ。
本文は既存の日本語フォントを使い、自動縮小で押し込まない。
360×640の設計座標、SafeArea、最大幅と縦スクロールは[UI設計ルール](../rules/ui-design.md)に従う。
スクロール範囲はRectMask2Dで切り抜き、ダイアログの「閉じる」は範囲外の上部に固定する。
画面全体がOverlay Canvasであるため、Pixel Perfect Cameraは追加しない。

| 画面 | 主な表示 |
|---|---|
| ホーム | 森と現在の4人、冒険への主要操作、ルーン、日次目標 |
| 冒険先 | 森・坑道の背景付き選択カードと短いヒント |
| 探索 | 部屋の背景、パーティ、絵の入口に重ねた行き先操作、獲得案内 |
| 仲間・編成 | 4枠のキャラ画像、候補の全身像、役割と技、所持仲間の一覧 |
| 装備 | 対象キャラ、装備中と候補の性能、所持武器と選択状態 |
| 戦闘 | 冒険先に対応した背景、左の味方4人、右の敵、予告、HP・技の操作 |
| 敗北・結果 | パーティと背景、保護される成果、再戦・帰還の主要操作 |
| 運動目標 | 日次・週次のカードと進捗。未設定・未確認には進捗バーを出さない |
| 設定 | 音量の表示とゲージ、運動データ連携案内 |

敵HPのバーとダウン状態の濃淡は別に表す。
ダウン残量のゲージやリングは追加しない。
味方を入れ替えると、ホーム・探索・編成・装備・戦闘の画像も更新する。
探索入口を選ぶと0.28秒の歩行表示後に既存の状態遷移を実行する。
移動中の二重操作を抑止し、戻る操作で確認ダイアログを開いた場合は移動を取り消す。

## 素材と生成記録

組み込みのimage_genで、今回のUI用に以下のPNGを生成した。
キャラは既存サンプルのアリア、トーマ、ルカ、ミナ、ノエルを区別するための見た目であり、正式なキャラ設定・マスタの採用を意味しない。
原指示は[生成指示の記録](../../client/ArtSource/UI/prompts.json)に保存する。

| 素材 | 内容・扱い |
|---|---|
| [Adventurers.png](../../client/Assets/Baryonyx/Features/Wireframe/UI/Art/Adventurers.png) | 味方5人、狼、スライム、森の守り手の4×2シート。生成後に背景の透過を指示し、アルファを保持する |
| [Forest.png](../../client/Assets/Baryonyx/Features/Wireframe/UI/Art/Forest.png) | 石のアーチと脇道がある森。背景、ホーム、探索、戦闘に利用する |
| [Mine.png](../../client/Assets/Baryonyx/Features/Wireframe/UI/Art/Mine.png) | 中央と右側に入口がある坑道。冒険先、探索、戦闘に利用する |

PNGはPoint、MipMapなし、非圧縮で読み込む。
キャラはRawImageのUVでシートを参照する。
画像内に文字は焼き込まず、ゲームの状態に応じた文字はTextMeshProで表示する。
枠と小さな武器記号はUnityで生成し、生成PNGの画像編集には使わない。

## 実装と再生成

- [画面生成](../../client/Assets/Baryonyx/Features/Wireframe/Editor/WireframeScreenAssets.cs)：Prefab、フォント、SafeArea、操作部品。
- [画面別の見た目](../../client/Assets/Baryonyx/Features/Wireframe/Editor/WireframeScreenArt.cs)：枠の生成、画像の読み込み、10画面の組み立て。
- [状態と画像の対応](../../client/Assets/Baryonyx/Features/Wireframe/Runtime/WireframeArt.cs)：編成・敵状態・背景・進捗表示と探索移動。

Unityで再生を停止し、`Baryonyx/Wireframe/Create Screen Assets` を実行する。
生成後は `Baryonyx/Wireframe/Open Scene` から画面を開き、PlayModeで確認する。
画像の追加生成は再生成処理に含まれず、保存済みPNGを利用する。
正式な認証、健康データ取得、保存、戦闘計算はこのUI更新に含まれない。

## 2026-09-19の検証結果

元のEditorへの操作要求がタイムアウトしたため、[テスト手順](../rules/client-testing.md)に従い、`client/Temp/PixelUiVerification` の検証コピーをUnity 6000.6.0f1で開いた。
元のEditorを終了・再起動せず、コピー側で実際に生成したPrefab・フォント・追加素材のメタデータを元のプロジェクトへ反映した。
既存アセットのGUIDは維持している。

- 再コンパイル：完了、エラーなし。
- EditMode：Wireframe対象20件成功。SafeArea、5種類の画面寸法、戦闘の主要操作、編成と画像の連動、敵HPと濃淡の分離を含む。
- PlayMode：Wireframe対象5件成功。実Prefabへの入力、獲得・装備・編成・敗北・帰還・目標ダイアログ、探索移動の取り消しを含む。
- Gameビュー：10画面と初回案内・目標設定・坑道・ボス表示を撮影し、画像を開いて確認した。仲間・装備・目標・敗北はスクロール下部も確認した。
- 追加の表示確認：720×1560の戦闘と、1024×768の目標設定。横画面でスクロール後も「閉じる」と確定・取消の操作へ到達できることを確認した。
- 画面キャプチャと一覧：`client/Assets/DevCaptures/pixel-ui-*.png`、`pixel-ui-gallery.html`。検証用のためGit管理外。

Android上のタップ・戻る・システムバーと、参照タスクのサンプルとの一致は未確認である。

## APKのビルドと配置

2026-09-19に、検証コピーで `Baryonyx.App.Editor.WireframeSceneSetup.BuildAndroid` を実行した。
この入口から共通の `AndroidBuild.Build` を呼び、Wireframeシーンだけを起動対象にしてARM64・IL2CPPのAPKを生成した。
ビルド中に元のWireframe実装・素材が変更されていないことも確認した。

生成物は `client/Builds/Android/baryonyx.apk` に保存し、`G:\マイドライブ\71_プロジェクト\baryonyx\baryonyx.apk` へ上書きコピーした。
サイズは93,520,113バイトで、APKの署名、アーカイブのCRC、起動対象のシーン、ARM64ライブラリを検査した。
生成物・ローカル配置先・Drive配置先のSHA-256はすべて一致した。
ビルドと配置の記録は `client/Logs/pixel-ui-android-build.log` と `client/Logs/pixel-ui-apk-delivery.json` に保存している。

Google Drive for desktopによるクラウド同期の完了と、Android端末での起動は未確認である。
