# Web調査の根拠

このスキルの初期調査は2026-09-21に行った。Webページは更新されるため、Unityのバージョン、OS、ストアポリシー、対象地域を実装時に再確認する。以下は仕様の丸写しではなく、`ui-advisor` が判断を始めるための一次資料である。

## Unity公式

- [UI systems comparison](https://docs.unity3d.com/6000.0/Documentation/Manual/UI-system-compare.html)：UI Toolkit、uGUI、IMGUIの用途別比較。掲載表ではUnity 6.6のランタイム推奨がuGUI、代替がUI Toolkitで、画面空間の大量UIやテクスチャレス表示はUI Toolkit、ワールド空間UIやカスタムシェーダー・MonoBehaviour参照はuGUIが向くと整理されている。プロジェクトの必要機能と既存資産を優先して選ぶ。
- [UI Toolkit](https://docs.unity3d.com/6000.0/Documentation/Manual/UIElements.html)：UXML、USS、UI Builder、UI Debugger、ランタイムUIの基本。
- [Runtime data binding](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-runtime-binding.html)：ランタイムのデータソース、バインディングモード、変換、ログの考え方。
- [ListView](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-uxml-element-ListView.html)：リスト要素の再利用、仮想化、`RefreshItems` と `Rebuild` の使い分け。
- [Runtime UI performance](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-performance-consideration-runtime.html)：使用ヒント、動的アトラス、テクスチャ、メッシュ再生成、端末プロファイル。
- [Screen.safeArea](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Screen-safeArea.html)：端末の非表示領域とUI Toolkitの座標原点の注意点。
- [Device Simulator](https://docs.unity3d.com/ja/current/Manual/device-simulator-introduction.html)：エディター内で端末の解像度、Safe Area、回転、タッチ入力を初期確認する機能。実機確認の代替にはしない。
- [Graphic and font assets preparation](https://docs.unity3d.com/6000.0/Documentation/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/graphic-and-font-assets-preparation.html)：Sprite Atlas、動的アトラス、フォントアトラス、Pixel Art資産、PSD、9-sliceの考え方。
- [Addressables](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.addressables.html)：非同期ロード、依存関係、ローカル・リモート資産のアドレス管理。
- [Localization](https://docs.unity3d.com/ja/6000.0/Manual/com.unity.localization.html)：String/Asset Table、Smart Strings、疑似ローカライズ、Unity 6のパッケージ版。
- [Input System](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.inputsystem.html)：旧Input Managerとの位置付けと新Input Systemの拡張性。
- [Runtime UI event system](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-Runtime-Event-System.html)：UI Toolkitと入力システム、Navigation、Pointer、Submit、Cancelのイベント。
- [Unity Test Framework](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.test-framework.html)：Edit ModeとPlay Modeテスト。

## Appleのゲーム・アクセシビリティ資料

- [Onboarding for Games](https://developer.apple.com/app-store/onboarding-for-games/)：短く段階的なコア・ループ説明、スキップ、再チュートリアル、日次報酬、継続率の測定。
- [HIG: Onboarding](https://developer.apple.com/design/human-interface-guidelines/onboarding)：体験しながら学ぶ、任意化、文脈に近いヒント、初回に不要な要求を遅らせる方針。
- [HIG: Designing for games](https://developer.apple.com/design/human-interface-guidelines/designing-for-games/)：プラットフォーム入力、ゲーム向けアクセシビリティ、タッチ領域、文字サイズ、安全領域。
- [HIG: Game controls](https://developer.apple.com/design/human-interface-guidelines/game-controls)：親指が届く配置、Safe Area、頻用操作44pt、押下状態、ジェスチャーと仮想コントロール。
- [App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/)：有料のランダム仮想アイテムで確率開示を求めるストア要件を含む。対象地域・最新版を都度確認する。
- [Rewarding players with achievements](https://developer.apple.com/documentation/GameKit/rewarding-players-with-achievements)：達成進捗、隠し実績、ローカライズ、プレイヤーが確認できる成果の考え方。

## Androidのゲーム・アクセシビリティ資料

- [Make apps more accessible](https://developer.android.com/guide/topics/ui/accessibility/views/apps-views)：インタラクティブ要素の48dpタッチ領域、説明可能なUI、視覚・聴覚・運動への配慮。
- [Layout basics](https://developer.android.com/design/ui/mobile/guides/layout-and-content/layout-basics)：Safe Area、Insets、端末サイズ・縦横・折りたたみ、主要操作の届きやすさ。
- [Games guidelines](https://developer.android.com/games/guidelines)：システムバー・カットアウトのInsets、画面サイズ変更、コントローラー・キーボード・マウス対応。
- [Gesture navigation](https://developer.android.com/develop/ui/views/touch-and-input/gestures/gesturenav)：ゲームがシステムジェスチャー領域を必要とする場合の扱いと、必要最小限の除外。

## 使い方と限界

Unity公式資料はAPI・パッケージ・性能上の技術判断に使い、AppleとAndroidの資料は各プラットフォームの入力・アクセシビリティ・ストア要件に使う。RPGの報酬量、運動の目標値、ドット絵のパレット、コマンド順の最適値はプロジェクト固有であり、一般論として固定しない。ゲーム内計測、プレイテスト、実機プロファイルで仮説を検証する。

## 業界・研究・実務記事

公式仕様を補う設計の観点として、次の資料を使う。ここにある数値、画面パターン、経済曲線は普遍的な規則ではなく、対象ユーザーとゲームループに合わせて検証する。

- [Unity: UI interface design and implementation](https://unity.com/blog/games/ultimate-guide-to-creating-ui-interfaces)：ワイヤーフレーム、グレーボックス、フォント、uGUI・UI Toolkitの実装工程。
- [Unity: Effective UI and game design](https://unity.com/blog/games/how-to-immerse-your-players-through-effective-ui-and-game-design)：ジャンルの慣習とアート方針をUIへつなぐ考え方。
- [Unity: Balanced in-game economy](https://unity.com/how-to/design-balanced-in-game-economy-guide-part-3)：source/sink、動機、初期報酬、テストとロールアウト。
- [First Time User Experiences in mobile games](https://doi.org/10.1016/j.entcom.2018.04.004)：初回体験のユーザビリティ評価。
- [Smartphone Games Heuristics](https://doi.org/10.1080/10447318.2024.2356911)：スマホゲームの技術・非技術・ゲームプレイを点検するヒューリスティクス。
- [GameDeveloper: Gameplay progression](https://www.gamedeveloper.com/design/gameplay-design-fundamentals-gameplay-progression)：報酬を理解可能にし、プレイヤーが行動を計画できるようにする考え方。
- [Game Accessibility Guidelines](https://gameaccessibilityguidelines.com/)、[Xbox Accessibility Guidelines](https://learn.microsoft.com/en-us/xbox/accessibility/guidelines)：コントラスト、入力、フォーカス、時間、モーション、字幕などの点検観点。

## コミュニティ・参考例

現場の失敗例や発想を探す入口として使う。個人記事や掲示板の意見は、著者の端末、ジャンル、制作規模に依存するため、採用したら実機とプレイテストで再現性を確認する。

- [Procreator: Best practices for game UI design](https://procreator.design/blog/best-practices-for-game-ui-design/)：視覚的階層、プロトタイプ、プレイヤーが知るべき情報の整理。
- [DEV Community: Pixel-perfect UI in Unity](https://dev.to/niraj_gaming/pixel-perfect-ui-in-unity-a-complete-guide-for-designers-and-developers-1l9c)：PPU、基準解像度、書き出し、9-sliceの実務メモ。
- [Reddit: Quick fixes to improve a game HUD](https://www.reddit.com/r/gamedev/comments/1sjlq8l/4_very_quick_fixes_to_improve_your_games_hud/)：最大混乱条件でHUDを見直す、不要要素を減らす、実機距離で読むという経験談。
- [Reddit: Beginner mistakes with a game HUD](https://www.reddit.com/r/userexperience/comments/1q8j1jn/7_obvious_beginner_mistakes_with_your_games_hud/)：情報の優先順位、親指による遮蔽、スマホ実機モックの経験談。
- [Gamedexy mobile UI screens](https://www.gamedexy.com/screens?category=daily_reward)：オンボーディング、進行、デイリー報酬などの画面例を比較するアーカイブ。要件や品質保証の根拠にはしない。

調査日以降にページが更新・削除される可能性がある。リンクが読めない場合は、同じ主張を公式資料、一次研究、実機テストで置き換える。
