# UnityテストとWebビルドの初期設定

状態：完了（初期設定・ローカル検証）
作成日：2026-09-10
更新日：2026-09-10

既存のCSharpier・Microsoft.Unity.Analyzersに、EditMode・PlayModeテストとWebビルドを追加する。
PR Previewの公開先はCloudflare Pagesとし、利用者の指定により公開jobをコメントアウトして準備する。
公開先やSecretsの登録は実施していない。
commit・pushとPR作成は後続の依頼で実施し、GitHubの初回実行結果を以下に記録した。

## 実装内容

- Runtime・Editor・EditModeテスト・PlayModeテストのasmdefを分け、後からゲームロジックとテストを追加できる構成にした。
- シーン選択のEditModeテスト5件と、SampleSceneの読み込み・カメラ確認のPlayModeテスト1件を追加した。
- Unityのビルド設定からWeb版を作成するEditorメソッドを追加した。
- GitHub CIへ2種類のテストとWebビルドを追加し、結果XML・Web成果物を7日間保存する設定にした。
- テスト0件や失敗、Web成果物の欠落を検査するスクリプトを追加した。
- Cloudflare Pages向けPR Preview jobを、全体をコメントアウトした状態で追加した。

日常の手順と公開時の設定は [UnityのテストとWebプレビュー](../rules/client-testing-and-preview.md) にまとめる。

## 検証結果

| 項目 | 結果 |
|---|---|
| CSharpier | 追加コードを含む6ファイルで成功 |
| EditMode | Unity 6000.6.0f1・Windowsで5件成功 |
| PlayMode | 同環境で1件成功 |
| Analyzer | 新しいテスト用asmdefを含む83アセンブリの確認で成功 |
| 結果検証スクリプト | 実際のNUnit結果を受理し、結果なし・失敗・全件Skipを拒否 |
| Web成果物検証スクリプト | 必須ファイル欠落、wasm本体なし、Cloudflareのサイズ上限超過を拒否 |
| workflow | 現在の設定とPreviewを有効化した場合の両方で、actionlint 1.7.12の構文検査成功 |
| Web Build Support | ローカルの6000.6.0f1へHub経由で追加済み |
| Webプレイヤーの実ビルド | 正常終了。約13 MB、最大ファイル約9.4 MB。Cloudflare Pagesのサイズ検査も成功 |
| ブラウザでの起動 | ローカルHTTP経由でWebプレイヤーを起動し、SampleSceneの表示を確認 |
| GitHub上の実行 | [PR #1の初回CI](https://github.com/yn1323/baryonyx/actions/runs/34379926795) で整形はWindows・Macとも成功。Analyzerの前提確認がUnity用Secrets不足で失敗し、後続のテスト・WebビルドはSkip |
| Cloudflare公開 | 指定により無効化。コメントアウトした設定のみ準備 |

検証は、開いている `client/` とは別の一時コピーで行う。
ブラウザではURPのFSR用シェーダーが利用できず、ポストプロセスを実行しない旨の警告が1件出た。
初期シーンの表示は確認できたが、今後ポストプロセスを使う場合はWebの描画設定も確認する。
この計画の完了は、依頼された初期設定とローカル検証の完了を示す。
Cloudflareの公開と、GitHub上のUnityテスト・Webビルドの成功確認は残っている。
既存の整形・Analyzer導入についてのGitHub初回確認待ちは [先行計画](2026-09-10-client-code-quality.md) に引き継ぐ。
