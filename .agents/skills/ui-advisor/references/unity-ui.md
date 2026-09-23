# Unity UIの実装判断

この資料は、`ui-advisor` がUnityのバージョンや既存コードを確認したあとに使う実装用メモである。
プロジェクト固有のUIシステム、画面方向、基準解像度、安全領域は[UI設計ルール](../../../../doc/rules/ui-design.md)を正本とし、API名やパッケージ版はプロジェクトの `ProjectVersion.txt` と `Packages/manifest.json` を優先する。

## UI ToolkitとuGUIの選び方

Unity 6の公式比較表は、ランタイムの推奨をUnity UI（uGUI）、代替をUI Toolkitとし、エディターの推奨をUI Toolkitとしている。
このプロジェクトでの採用方針は[UI設計ルール](../../../../doc/rules/ui-design.md#現行のui構成)に従う。
画面空間のメニュー・HUD・一覧を既存UIから移行する場合は、一般的な推奨だけでなく、移行範囲と入力・テストの境界を記録する。

UI Toolkitを採用する場合は、次の責務を固定する。

- UXML：階層、名前、テンプレート、データバインディングの宣言。
- USS：色、余白、サイズ、フォント、状態クラス、テーマ変数。画面固有の一回限りの試作以外はインラインスタイルへ逃がさない。
- C#：状態購読、イベント、フォーカス、データ変換、非同期ロード。`OnEnable`・`OnDisable`や画面ライフサイクルに合わせて購読を管理する。
- UI Builder：構造とスタイルの確認。巨大な一枚のUXMLにせず、画面・テンプレート・セル・ダイアログを分割する。

uGUIを採用する場合は、Canvas、Prefab、Layout、Animator、Presenterの責務を分ける。頻繁に変わるHUDと静的な背景を同じCanvasへ置かず、変更範囲を小さくしてCanvas再構築を抑える。Layout GroupやContent Size Fitterを深く連鎖させず、リストセルは再利用する。

## データバインディングと一覧

Unity 6のランタイムデータバインディングは、C#オブジェクトのプロパティとUIを接続する。一覧には `ListView` の `itemsSource`、`makeItem`、`bindItem`、必要なら `unbindItem`、`destroyItem`、アイテム高さを定義し、表示件数以上のVisualElementを作らない。データ型の変更やテンプレート構造変更には `Rebuild` が必要だが、通常の値更新は `RefreshItem` や `RefreshItems` を優先する。

バインディングへ複雑な計算、通信、インスタンス生成を入れない。表示用の文字列、割合、比較結果、状態クラスをViewModelやデータソース側で準備し、頻繁な変換では割り当てとGCを抑える。バインディングが便利でも、報酬の付与や戦闘の解決をUI側へ移さない。

## 入力とフォーカス

新規プロジェクトではInput Systemを候補にし、UI操作とゲーム操作のAction Mapを分ける。タッチ、マウス、キーボード、ゲームパッドが同じ意味のActionへ入るようにし、コマンド戦闘や設定画面ではフォーカス移動、決定、キャンセル、戻るを明示する。

UI ToolkitとuGUIを混在させる場合は、EventSystem、Input System UI Input Module、Panel Settings、描画順を一つずつ確認する。画面を開くたびにEventSystemを生成せず、入力を二重に処理しない。タッチ位置をワールド座標へ変換する処理がある場合は、スケール、Safe Area、Canvas座標系を同じテストケースで確認する。

## Safe Areaと解像度

`Screen.safeArea` はPlayerウィンドウを基準にしたピクセル矩形で、Unity UIは左下原点、UI Toolkitは左上原点を使う。UI Toolkitへ値を渡す場合はY反転を含めて変換し、端末のカットアウト・ステータスバー・ナビゲーションバー・ホームジェスチャーを実機で確かめる。

Canvas ScalerやPanel Settingsは、基準解像度だけでなく縦横比の異なる端末で検証する。横幅だけに合わせて拡大すると、縦画面や横画面でボタンが過大・過小になる。重要情報はアンカーと余白で安全領域へ固定し、装飾背景だけを全画面へ伸ばす。

## 描画・アセット・性能

- Sprite Atlasは同時表示するUI・キャラクター・報酬アイコンなどのまとまりごとに分け、異なる圧縮・フィルター・最大サイズを混在させない。
- UI Toolkitの動的アトラスは便利だが、表示中の画像が同じアトラスへ入るかFrame Debuggerで確認する。頻繁に変化する色・位置・サイズを一度に多数アニメーションしない。
- 角丸・枠・影を大量の透明テクスチャで重ねるとオーバードローが増える。Pixel Artの見た目を保つために必要なテクスチャと、USSの単純な背景・境界をプロファイラーで比較する。
- Render Textureをミニマップやキャラクター表示に使う場合は、更新頻度と解像度を下げられるか調べる。常時更新する大きなRender Textureを画面ごとに増やさない。
- Addressablesを導入する場合は、画面・イベント・言語・報酬アイコンなどのライフサイクル単位でグループ化し、非同期ロード、失敗、解放、キャッシュ、ラベルを設計する。UIを閉じても参照が残っていないか確認する。

性能はエディターだけで合格とせず、目標端末のCPU、GPU、メモリ、入力遅延、ロード時間を測る。Frame Debugger、Profiler、Memory Profilerを、画面表示中、リストスクロール中、報酬演出中、画面遷移中に使う。

Device Simulatorは、エディター内で端末の解像度、Safe Area、回転、タッチ入力を切り替える初期確認に使う。Simulatorで崩れがないことは実機合格を意味しないため、最終的には実機でタッチ、フォント、性能、OSジェスチャーを確認する。

## ローカライズ

Localization packageのString Table・Asset Table・Smart Strings・疑似ローカライズを画面の初期設計から使う。キーは表示文そのものではなく意味と画面の所有者が分かる名前にする。単位、日時、複数形、性別、名前差し込み、右から左の言語を想定し、ボタン幅を日本語だけで決めない。

翻訳でレイアウトが伸びたときの優先順位を決める。通常は行数増加、折り返し、スクロール、情報の要約、アイコンの補助説明の順で検討し、文字を極端に縮小して解決しない。

## テストの粒度

Unity Test Frameworkで次を分ける。

- Edit Mode：報酬の集計、比較値、状態遷移、ローカライズキー、入力Actionの意味変換など、Unityシーンに依存しない処理。
- Play Mode：ボタン操作、フォーカス、画面遷移、非同期ロード、キャンセル、Safe Area適用、アプリ中断・復帰、演出の終了後状態。
- 実機・手動：タッチ誤操作、フレーム落ち、フォントの見え方、ノッチ、触覚・音、OSの戻る、通信遅延、メモリ警告。

参照URL：

- [Unity UI systems comparison](https://docs.unity3d.com/6000.0/Documentation/Manual/UI-system-compare.html)
- [UI Toolkit](https://docs.unity3d.com/6000.0/Documentation/Manual/UIElements.html)
- [Runtime data binding](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-runtime-binding.html)
- [ListView](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-uxml-element-ListView.html)
- [Runtime UI performance](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-performance-consideration-runtime.html)
- [Screen.safeArea](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Screen-safeArea.html)
- [Device Simulator introduction](https://docs.unity3d.com/ja/current/Manual/device-simulator-introduction.html)
- [Sprite Atlas](https://docs.unity3d.com/6000.0/Documentation/Manual/sprite/atlas.html)
- [Addressables](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.addressables.html)
- [Localization](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.localization.html)
- [Input System](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.inputsystem.html)
- [Unity Test Framework](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.test-framework.html)
