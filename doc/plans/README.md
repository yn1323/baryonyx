# 変更計画と検証記録の索引

[全体索引](../README.md) → 計画と記録

変更ごとの提案、判断の理由、実装手順、検証の記録を置く。
計画には提案した時点の仕様や、その後に置き換わった方針を含むため、今の動作は「後継・反映先」の文書で確かめる。
状態の区分と書き方は[文書管理方針](../rules/documentation-policy.md#現行仕様と計画)に従い、実装の完了と実環境での確認を分けて読む。

| 日付 | 計画・記録 | 状態 | 後継・反映先 |
|---|---|---|---|
| 2026-09-09 | [serverのHono開発環境とCIの導入](2026-09-09-server-bootstrap.md) | 完了 | [バックエンドの開発環境](../rules/backend-design.md) |
| 2026-09-10 | [Unityの整形と静的解析の導入](2026-09-10-client-code-quality.md) | 完了 | [整形と静的解析](../rules/client-code-quality.md) |
| 2026-09-10 | [UnityテストとWebビルドの初期設定](2026-09-10-client-tests-web-preview.md) | 完了（Webビルドは同日に廃止） | 後継：[クライアントCIの再構成](2026-09-10-client-ci-backlog.md)。反映先：[UnityのテストとCI](../rules/client-testing.md) |
| 2026-09-10 | [クライアントCIの再構成](2026-09-10-client-ci-backlog.md) | 進行中 | [UnityのテストとCI](../rules/client-testing.md)・[Androidビルドと実機確認](../rules/client-android-testing.md) |
| 2026-09-10 | [Health Connect連携とiOS拡張](2026-09-10-health-connect-integration.md) | 進行中 | [健康データの読み取りと保存](../features/health-data.md) |
| 2026-09-13 | [健康データの一覧・JSON詳細の表示](2026-09-13-health-connect-local-display.md) | 完了（画面は削除済み） | 後継：[起動時の連携と歩数の同期](2026-09-24-startup-health-sync.md)。記録：[削除した歩数画面](../archive/health-data-local-screen.md) |
| 2026-09-13 | [縦画面と機種差に対応するUI](2026-09-13-portrait-ui.md) | 完了（縦画面は横画面へ改めた） | 後継：[横画面の可変レイアウト](2026-09-22-responsive-landscape-layout.md)。反映先：[UI設計ルール](../rules/ui-design.md) |
| 2026-09-13 | [Health Connectの利用条件の案内](2026-09-13-health-connect-requirement-notice.md) | 完了（画面は削除済み） | 記録：[削除した歩数画面](../archive/health-data-local-screen.md#起動時と設定画面からの復帰時の案内)。QA：[歩数の利用条件と起動時の案内](../qa/health-connect-requirements.md) |
| 2026-09-18 | [主要画面の遷移とUnityワイヤー](2026-09-18-game-wireframe.md) | 完了（試作は削除済み） | 記録：[操作試作](../archive/game-wireframe.md)。Topの動作：[画面一覧のTop](../features/screens.md#top) |
| 2026-09-20 | [運動量とルーン変換のDB](2026-09-20-exercise-rune-db.md) | 完了（請求の契機は置き換え） | [運動データとルーン換算](../features/exercise-rewards.md)・[HomeのACTパネル](../features/startup-sync.md#homeのactパネル) |
| 2026-09-22 | [横画面の可変レイアウト](2026-09-22-responsive-landscape-layout.md) | 完了 | [UI設計ルール](../rules/ui-design.md) |
| 2026-09-24 | [ホーム画面「野営地」の見た目モック](2026-09-24-home-camp-mock.md) | 完了 | [画面一覧のホーム画面](../features/screens.md#ホーム画面の見た目モック) |
| 2026-09-24 | [起動時の連携と歩数の同期](2026-09-24-startup-health-sync.md) | 完了 | [起動時の連携と歩数の同期](../features/startup-sync.md) |
| 2026-09-27 | [案内人がいる画面（当時の名前は酒場・装備・神殿・旅の案内所）](2026-09-27-guide-menu-screens.md) | 完了 | [編成・商会・神殿・旅の案内所の画面](../features/screens.md#編成商会神殿旅の案内所の画面) |
| 2026-10-02 | [Top・Home・戦闘画面の3Dの舞台（HD-2D）](2026-10-02-hd2d-3d-stage.md) | 完了 | [3Dの舞台](../art/hd2d-stage.md) |
| 2026-10-02 | [Homeの昼の森と、HD-2Dのレンズ・色調・カメラ](2026-10-02-hd2d-glade-look.md) | 完了 | [3Dの舞台](../art/hd2d-stage.md) |
| 2026-10-02 | [Topと戦闘画面の屋外の舞台](2026-10-02-hd2d-outdoor-stages.md) | 完了 | [3Dの舞台](../art/hd2d-stage.md) |
| 2026-10-02 | [停止中とSceneタブの見た目をPlay Mode中とそろえる](2026-10-02-hd2d-edit-view.md) | 完了 | [3Dの舞台のSceneタブでの表示](../art/hd2d-stage.md#sceneタブでの表示) |
| 2026-10-04 | [編成・スキル・レベルのサーバー保存（当時の名前は酒場）](2026-10-04-tavern-party-save.md) | 完了 | [キャラと編成の保存](../features/party.md#キャラと編成の保存) |
| 2026-10-04 | [冒険の1周（出発・探索・戦闘・報酬・復活・結果・中断と再開）](2026-10-04-adventure-loop.md) | 完了（探索の部分は置き換え） | 後継：[探索の地図](2026-10-04-exploration-route-map.md)。反映先：[探索とダンジョンの進行](../features/stage-progression.md)・[冒険の画面](../features/screens.md#冒険の画面) |
| 2026-10-04 | [探索を森を見下ろす地図にし、分岐ルートを冒険ごとに作る](2026-10-04-exploration-route-map.md) | 完了 | [探索とダンジョンの進行](../features/stage-progression.md)・[冒険の画面](../features/screens.md#冒険の画面) |

Google認証をHealth Connect接続の前提とする古い計画がある。
今は、Health Connectの連携とGoogle接続を独立して扱う（[健康データの読み取りと保存](../features/health-data.md)）。
