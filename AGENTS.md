# AGENTS.md

このファイルは、baryonyx全体に適用する作業上の制約と参照先を定める。

## IMPORTANT
- 必ず日本語で返答してください
- local開発時の `/server` の `pnpm dev` の起動・起動し直しは、Workspace3 のセッションが行う。ほかのセッションは Workspace3 へ頼む。
- 不要なセカンダリラベルや説明を避ける。レイアウトに説明させる。
- リモートコントロール利用時、処理が完了した段階でスマホのClaudeに通知するようにしてください。

## 作業の基本

- 文書、PR本文、レビューも日本語で書く。コードの識別子や既存の技術用語は維持する。
- 作業対象のコード、設定、関連文書を確認してから変更する。下位に `AGENTS.md` があれば併せて読む。
- 依頼の目的を満たす範囲で変更し、無関係な整理や共通基盤の追加へ広げない。
- 調べものは[コードと文書の読み方](doc/rules/code-reading.md)に従い、当たりを絞ってから必要な範囲だけを読む。
- ユーザーや並行作業の変更を保護し、依頼外の変更を上書き、復元、削除、ステージしない。
- 秘密値や個人情報を文書、ログ、コミットへ含めない。
- 未リリースのため、Migration、既存データとの互換性、旧バージョンとの互換性は考慮しない。DB不整合が発生した場合は、毎回データベースをリセットしてよい。
- 外部の素材（効果音、BGM、フォント、画像など）をゲームに入れるときは、配布元の規約を確かめ、同じ変更で[外部素材のクレジット](doc/rules/credits.md)へ配布元を記録する。
- 画像を生成するときは、世界地図・エリアマップを含むすべての背景と画像で、プロンプトに「細碎な点状テクスチャが多すぎるので抑える」を必ず含める。一文だけでは効きにくいため、点や粒を散らさず色の面となめらかな影で塗る、木はかたまりで描く、という具体的な指示も添える。Codex CLIの画像生成は [codex_image.py](.agents/skills/shared/scripts/codex_image.py) がどの依頼にも自動で足す。
- Unityの端末機能・外部サービス連携は、無料で利用できる既存のUnity用プラグインの利用を基本とする。採用前に必要な機能、Unity・対象OSへの対応、保守状況、ライセンス・費用を確認する。Kotlin・Java・Swiftなどのネイティブコードを自作する場合は、既存プラグインで満たせない要件と理由を明示する。

## ゲーム開発の進め方と提案

- ユーザーはゲーム開発が初めてである。作法・工程・用語を必要に応じて平易に説明し、依頼の順序や前提に問題があれば、理由と適切な進め方を伝える。
- ゲーム開発の作業を提案するときは、都度Web検索で関連する開発手順や推奨事項を確かめる。公式ドキュメントや開発元の資料などの一次情報を優先し、対象のUnityの版・OS・今の実装に当てはまるかを見る。検索できなければその旨を伝え、確認済みの情報と未確認の判断を分ける。
- 提案は、今の開発段階・今やること・その理由・先に決める／確かめる事項と、参照した情報源へのリンクを短く示してから、今回の作業範囲・進める順序・完了条件を出す。一般的な手順とこのプロジェクトでの判断を分け、依頼の目的と合意済みの方針を尊重し、工程の助言を理由に無関係な実装へ広げない。

## クライアントアセット展示室

- `client/` には[クライアントアセット展示室](doc/features/showcase.md)を必ず設け、ゲームを進めずにアセットを一覧・プレビューで確かめられる状態を保つ。
- 画像・キャラクター・UI・Prefab・アニメーション・VFX・音声・シーンなどを足す・変える・移す・消したとき、また表示・操作・再生の結果が変わる実装をしたときは、同じ変更で展示室の登録とプレビューを直す。カタログの更新と個別の登録は展示室の仕様に従い、自動検出だけで終えず、一覧に載って変更後の内容をプレビューで見られることまで確かめる。確かめられなければ未確認として報告する。

## 構成と参照先

| 場所 | 役割 |
|---|---|
| [client/AGENTS.md](client/AGENTS.md) | Unityクライアントの配置と作業ルール |
| [server/AGENTS.md](server/AGENTS.md) | Honoバックエンドの配置と作業ルール |
| [doc/README.md](doc/README.md) | 機能、設計ルール、計画、QAの入口 |
| [文書管理方針](doc/rules/documentation-policy.md) | `doc/` の配置と役割、文書の形式と更新方法 |
| `shortcuts/` | ユーザーが手動実行する、1ファイルで完結するコマンド（[一覧](doc/rules/local-shortcuts.md#ファイルの一覧)） |
| [.agents/skills/README.md](.agents/skills/README.md) | スキル（特定作業の進め方）の索引と呼び出し方 |

技術固有の指示と実行コマンドは、採用技術や実際の設定を確認してから追加する。

## ディレクトリ構成と狙い

構成図と配置の共通方針は[リポジトリの配置](doc/architecture.md#リポジトリの配置)、各実装の具体的な配置はそれぞれの `AGENTS.md` から参照する設計文書に従う。

## Skill

- 作業に適用するSkillの発動条件と本文を確認し、その範囲で使う。スキルの一覧と呼び出し方は[スキル索引](.agents/skills/README.md)にある。
- docとスキルの使い分けは[文書管理方針](doc/rules/documentation-policy.md#docとスキルの使い分け)に従う。
- 日本語の技術文書やPR本文を作成・推敲するときは [japanese-tech-writing](.agents/skills/japanese-tech-writing/SKILL.md) を使う。
- 読み物としての長い文章を扱う場合は、必要に応じて [cognitive-rhythm-writing](.agents/skills/cognitive-rhythm-writing/SKILL.md) を使う。
- 演出・画像・アセット・効果音・BGMの見た目や音をユーザーと決める・作り直すときは [creative-direction](.agents/skills/creative-direction/SKILL.md) を使う。
- Skillの手順や文章規範を、このファイルへ複製しない。

## Gitと環境

- 現在のブランチと作業ディレクトリで作業する。ユーザーの明示指示がなければ、ブランチの作成・切り替えやworktreeの作成を行わない。
- commit、push、PR作成は、ユーザーが依頼した場合に行う。必要な操作が既に許可されていれば、同じ確認を繰り返さない。commitは[commitの手順](#commitの手順)に従う。
- 環境へ接続するときは対象を確認する。参照元の別プロジェクトのURLや権限設定を流用しない。
- Cloudflare（wranglerのログインやダッシュボード）は、ユーザーが依頼したDBのリセットにだけ使う。デプロイ、設定の変更、データの参照や書き込みなど、それ以外には使わない。
- 自動生成ファイルは生成元または生成手順を更新する。依存関係のロックファイルは対応する管理ツールで更新する。

## デバッグ環境

- デバッグにはUnityエディターを利用する。
- 端末環境での確認が必要なデバッグは、Androidエミュレーターで行う。
- エミュレーターの起動は、必ずユーザーが手動で行う。AIは、検証時も含めてエミュレーターを起動しない。
- 調査・実装は、これらのデバッグ環境を前提に進める。
- Unityエディターの操作と、起動済みの端末へのadbでのAPKのインストール・ログの取得は、AIが行ってよい。

## 手動実行用ショートカット

- `shortcuts/` には、1ファイルで完結し実行時の作業ディレクトリに依存しないWindows用 `.bat` とmacOS用 `.command` を置く。一覧・動作・前提・ログは[手動実行用ショートカットの動作](doc/rules/local-shortcuts.md)にあり、ファイルを足すときは同じ変更で[一覧](doc/rules/local-shortcuts.md#ファイルの一覧)を更新する。
- `start-pixel-8a.bat` はAIが実行しない。エミュレーターの構成・初回準備・起動できない場合の確認は[WindowsでのUnityとAndroidエミュレーター](doc/rules/client-android-emulator.md)に従う。
- APKのビルドが成功したら、実行方法にかかわらず、生成したAPKを接続先の環境に対応するファイル名で[Driveの配置先](doc/rules/local-shortcuts.md#google-driveへの配置)へ上書きコピーする。コピー完了までをビルド作業に含め、配置先へアクセスできない場合やコピーに失敗した場合は未完了として報告する。

## Asepriteの正本とタブレットの受け渡し

- ドット絵の正本は `.aseprite` のまま `client/Assets/Baryonyx/` の中の使う場所に置き、PNGへ書き出さない（[ゲーム内の対象の画像](doc/rules/frontend-design.md#ゲーム内の対象の画像)）。
- タブレットとの受け渡しに使うUnityのメニュー `Baryonyx > Art > Choose Aseprite Drive Folder...`・`Copy Aseprite Sources to Drive`・`Copy Aseprite Sources from Drive` は、ユーザーが手動で実行する。AIは、検証時も含めて実行しない（[タブレットとの受け渡し](doc/rules/frontend-design.md#タブレットとの受け渡し)）。

## 検証と報告

- 変更した動作を直接確認できるテストや検査を選ぶ。実行方法はリポジトリ内の設定から確認する。
- 戦闘の強さを変える変更（敵の倍率・敵の定義・カード・装備・ステータス・戦闘の計算）をしたら、[戦闘の自動対戦](doc/rules/client-testing.md#戦闘の自動対戦)で勝率とターン数を測り直す。
- 文書だけの変更では、[文書検査](doc/tools/README.md)でリンク、参照パス、見出しの索引を確かめ、記述の整合を確認する。
- 作業中は、変更した動作に関連するテストだけを実行する。serverの変更は `server/` で `pnpm test` を実行する。
- clientの変更は、`Baryonyx.EditModeTests`・`Baryonyx.PlayModeTests` のテストを、[作業中に実行するPlayModeテスト](doc/rules/client-testing.md#作業中に実行するplaymodeテスト)のスクリプト（run-editor-tests.py）で、再コンパイルと合わせて1回で実行する。Unity PlayModeは関連するテストクラスに絞る。
- 作業中は、同じ変更後に成功した検証を再利用し、追加変更や未解決の懸念がある場合に再実行する。commit前の全件実行には、それまでの結果を再利用しない。
- 実装の失敗と、権限・接続・ツール不足による検証不能を区別する。
- 完了時に差分を見直し、変更内容、検証結果、残っている未確認事項を報告する。

### commitの手順

ユーザーからcommitを指示されたら、次の順に進める。

1. commit対象の変更を含む領域のテストを全件実行する。
   - clientの変更：`Baryonyx.EditModeTests` と `Baryonyx.PlayModeTests` の全件（[実行方法](doc/rules/client-testing.md#実行と結果確認)）。`client/ci` を変えたときは、加えて `python3 -B -m unittest discover -s client/ci -p 'test_*.py'` と `node --test client/ci/resolve-drive-distribution.test.cjs`
   - serverの変更：`server/` で `pnpm test`・`pnpm test:ci`・`pnpm lint`・`pnpm typecheck`
   - 文書だけの変更：[文書検査](doc/tools/README.md)（`python -B doc/tools/check_docs.py --write-index` のあと `python -B doc/tools/check_docs.py`）と、記述の整合の確認
2. 1件でも失敗したら、原因を直してから手順1に戻る。失敗したテストだけでなく、全件を実行し直す。
3. すべて成功したら、変更を意味のまとまりごとに分け、まとまりごとにcommitする。対象の選び方・分け方・メッセージ、並行作業中の控え方は[commitの分け方とメッセージ](doc/rules/git-commit.md)に従う。

テスト0件と全件Skipは成功に含めない。
権限・接続・ツール不足で全件を実行できない場合や、変更と無関係な失敗で修正が依頼の範囲を超える場合は、commitせずに状況を報告し、ユーザーの判断を待つ。
