---
name: babysit-pr
description: ユーザーが`$babysit-pr`を明示したとき、現在のcheckoutの依頼範囲にある未push変更をcommit・pushしてPull Requestを作成し、GHAと自動レビューを最新headまで監視する。自動レビューの全指摘を優先度にかかわらず検証し、有効なら修正・対象確認・再commit・pushして監視をやり直す。通常の実装、テスト実行、commit、PR相談では自動的に使わない。
---

# PRチェックと自動レビューを最新headで完遂する

このSkillの明示的な呼び出しを、依頼範囲の修正、テスト更新、commit、push、Pull Request作成、自動レビューの再依頼と指摘への返信、CI再実行の許可として扱う。
新しいbranchやworktreeの作成、依頼外の変更、secretの変更、デプロイや人手承認は許可に含めない。

## 検証と進捗

最後の関連変更より後に同じ作業ツリーで成功した検証は再利用し、監視段階へ進んだという理由だけで再実行しない。
長い処理では、状態変化、失敗原因、次の対応を簡潔に共有する。

## 最初に読む

1. rootと対象に近い`AGENTS.md`
2. `client/` のUnity設定とテスト設定、`server/` のパッケージ・テスト・DB設定のうち実在し、変更に関係するもの
3. `.github/workflows/` と `docs/` の開発・CI手順があれば読む
4. PR本文を書く場合は [japanese-tech-writing](../japanese-tech-writing/SKILL.md)

コマンド、test project、workflow、check名の現在値はリポジトリを正本とし、このSkillの例より優先する。

## 1. 変更範囲とpushの安全性を確認する

ユーザー指定のbase branch、指定がなければdefault branchの最新remote-tracking refを取得してから、会話上の依頼に属する変更だけを特定する。
bareなlocal branchは更新が遅れている可能性があるため、比較元に使わない。

以下はBashの例であり、変数や引用は実行中のシェルに合わせる。
`git remote -v` でリモート名と接続先を確認し、`origin` は対象リモートに読み替える。base指定があればその値を使う。

```bash
git status --short
git branch --show-current
base_branch="$(gh repo view --json defaultBranchRef --jq '.defaultBranchRef.name')"
git fetch --no-tags origin "refs/heads/${base_branch}:refs/remotes/origin/${base_branch}"
base_ref="origin/${base_branch}"
git rev-parse --verify "$base_ref"
git merge-base "$base_ref" HEAD
git log "$base_ref"..HEAD --oneline
git diff "$base_ref"...HEAD --stat
```

- 現在のcheckoutから移動せず、新しいbranchやworktreeを作らない。
- fetchまたはremote base refの検証に失敗した場合は、古いlocal refへfallbackせず停止する。
- 現在branchが空、base branchそのもの、またはPRに不要なcommitを含む場合はpushしない。
- 既存の未commit変更はユーザーの変更として扱う。依頼範囲だと確認できないファイルを編集、復元、stageしない。
- 依頼範囲に属する既存の未push commitは、現在branchからまとめてpushする。依頼外のcommitが混在する場合は安全に分離できるまでpushしない。
- stage済みの依頼外変更を安全に分離できない場合はcommit前に停止する。
- `.env*`、credential、secret、個人情報をcommit、PR本文、logへ含めない。

同じhead branchのopen Pull Requestがある場合は、commitやpushより前に既存の自動レビューinline commentを取得し、最新コードでも成立する指摘を今回の修正へ含める。
解消すると分かっている指摘を残したまま、まもなく置き換えるhead SHAへ再レビューを依頼しない。

## 2. 変更に必要な確認を選ぶ

変更範囲と現在のリポジトリ設定から必要な検証を選び、対象に近い確認から行う。
ローカル全体テストを毎回必須にせず、実施済みの有効な検証を再利用する。

- `client/` の変更は、設定済みのC#整形・解析、Unityコンパイル、EditMode・PlayModeテストから関係する確認を選ぶ。
- Androidネイティブや歩数取得の変更は、該当プラグインのビルド・テスト、必要に応じて実機で確認する。
- `server/` の変更は、実在するパッケージスクリプトとテスト設定を確認し、API契約、二重付与防止、DBマイグレーションなど変更した境界を検証する。
- 文書・スキル・設定だけの変更では、構文や参照先など対象に適した確認を行う。
- まだ導入されていないツール、テスト、CIを実行済みとして扱わない。必要な実機・認証情報・実行環境がなければ未確認内容と理由を示す。
- 意図した仕様変更で契約が変わった場合、またはテスト自体に欠陥がある場合だけテストを修正する。期待値の緩和、skip、retry増加、assertion削除だけで不具合を隠さない。

## 3. GHA失敗を調べる

- 最初に失敗したstepとlogを確認し、原因をコード、テスト、fixture、設定、runner、外部serviceに分類する。
- コードやテストの問題は、失敗したテストまたは対象に近い最小の確認で再現させて修正する。Unityの起動・ビルド条件や実機依存も確認する。
- 修正後は失敗した境界と変更に直接関係する確認を行い、成功した修正をcommit、pushして新しいhead SHAを監視する。
- ローカルで再現しない場合は、実行履歴とlogから時刻、共有状態、待機、fixture、runnerなどの差を調べる。
- 一時的なrunnerや外部service障害だと確認できた場合だけ、該当jobを再実行する。同じSHA・同じ失敗のrerunは最大2回までとし、繰り返す場合は根拠と必要な対応を報告する。
- 再実行で成功しただけでは、コード修正済みとも原因解消とも扱わない。

## 4. 自己レビューしてcommitする

初回push前、またはGHA失敗への修正後に差分を読み直し、不要な複雑さ、重複、弱めた検証、依頼外変更がないことを確認する。

1. `git status`、unstaged diff、staged diffを確認する。
2. 今回の依頼に属するファイルだけを個別に`git add <path>`する。`git add .`と`git add -A`は使わない。
3. 意味のあるrevert単位へ分け、日本語のConventional Commitでcommitする。
4. `--amend`、`--no-verify`、対話的Git commandを使わない。
5. hookがファイルを変更した場合は差分を確認し、修正に対応する対象限定のローカル確認を再実行してから新しいcommitを作る。

## 5. pushしてPull Requestを作成する

push直前に上記のfetchとremote base refの解決をもう一度行い、`origin/<base>..HEAD`のcommit、
`origin/<base>...HEAD`のdiff、merge-baseを再確認する。
依頼外の履歴がなく、対象変更がすべてcommit済みの場合だけ現在branchをpushする。

同じhead branchのopen Pull Requestがあれば重複作成せず再利用する。
なければ、変更の目的、利用者に見える差分、実施した確認を日本語で記載し、ユーザー指定がなければ非draftのPull Requestを作成する。
PR URL、number、base、head branch、head SHAを記録する。

自動レビューの起動条件は現在のGitHub連携設定と実際の応答で確認する。新規PRで自動レビューが開始済みなら、直後に重複依頼しない。自動開始が確認できない場合は、そのSHAへ一度だけ明示的に依頼する。
既存Pull Requestへのpushと、自動レビューまたはGHAの指摘を直した後のpushでは、pushだけを再レビュー開始の証拠にせず、最新head SHAにつき一度だけ`@codex review`をコメントして再レビューを依頼する。
commentには`<!-- babysit-pr:review-head=<full-head-sha> -->`を含める。再開時はこのmarkerを検索してから依頼し、同じSHAのmarkerがあれば重複投稿しない。
依頼時のhead SHA、comment ID、作成時刻と、依頼前から存在するCodexのreview・reaction IDを記録する。同じSHAへ待ち時間を理由に再依頼を重ねない。

レビュー依頼本文はUTF-8の一時ファイルに保存し、`gh pr comment <pr> --body-file <本文ファイル>` などで改行を保って投稿する。
投稿したcommentを取得してIDと作成時刻を記録する。

## 6. 最新SHAのcheckと自動レビューを並行監視する

60秒を超えて無言で待たず、30から60秒間隔の短いpollで状態変化を確認する。

```bash
gh pr view <pr> --json headRefOid,url
gh pr checks <pr> --json name,workflow,bucket,state,link
gh api --paginate repos/<owner>/<repo>/pulls/<pr>/reviews
gh api --paginate repos/<owner>/<repo>/pulls/<pr>/comments
gh api --paginate repos/<owner>/<repo>/issues/<pr>/comments
gh api --paginate repos/<owner>/<repo>/issues/<pr>/reactions
gh api --paginate repos/<owner>/<repo>/issues/comments/<review-request-comment-id>/reactions
```

- PRの`headRefOid`がpushしたcommit SHAと一致することを確認する。
- 期待するworkflow・checkを現在の設定から特定する。checkが0件なら未起動かCI未導入かを確認する。未起動・pendingは成功扱いせず待ち、CI未導入や実行権限不足なら未完了条件として報告する。
- 失敗時はcheckのlinkから該当runを特定し、最初に失敗したstepとlogを確認する。
- コードまたはテストの失敗は、まず対象をローカルで確認する。修正した場合は対象限定のローカル確認を通し、新しいcommitをpushする。結合・実機テストは失敗したケースと必要な環境を特定し、Flakyや環境要因の可能性を評価してから修正要否を決める。
- 一時的なrunnerまたは外部service障害だとlogで確認できた場合だけfailed jobを再実行する。コード失敗をrerunで通そうとしない。
- flaky testは成功するまで無制限に再実行せず、共有状態、時刻、待機、selector、fixture、runnerの原因を切り分ける。再実行で成功しただけの場合は、修正済みとも失敗原因解消とも扱わない。
- workflowやテストを無効化し、必須checkを減らして成功させない。
- 新しいpush後は古いrunと古い自動レビュー完了を捨て、最新head SHAのcheckと自動レビューを最初から確認する。

### 自動レビューの完了を最新SHAへ結び付ける

GHA checkだけではレビュー完了を判定できない。Codex reviewのactorはGitHub APIで確認する。`chatgpt-codex-connector[bot]` などの表示名だけで判断せず、botのIDと種類を照合する。RESTとGraphQLでは確認済みの同一botを追跡する。

自動レビューの完了は、head SHAが監視開始時から変わっておらず、次のいずれかを確認できた場合だけ成立する。明示的な依頼commentへのCodexの👀は受付または開始のsignalであり、完了ではない。

1. **指摘あり:** Codexのsubmitted reviewの`commit_id`が最新head SHAと一致する。review本体だけで終えず、そのreviewに属するinline commentをすべて取得する。
2. **SHA付きの指摘なし:** Codexのissue commentが`Reviewed commit`、`headSha`、または同等のfieldで最新head SHAを示し、`Codex Review: Didn't find any major issues`や`<!-- codex-pull-request-review-summary -->`の成功statusなどで指摘なしを明示する。
3. **reactionによる指摘なし:** 新規Pull Requestでは、作成後にCodexの新しい👍 reactionが作られ、その間headが変わっていない。既存Pull Requestへのpush後は、最新SHAのmarker付き`@codex review`依頼より後に、依頼前のsnapshotにはなかったCodexの👍 reactionが作られている。PR本体と再レビュー依頼commentの両方のreactionを確認する。既存Pull Requestで依頼前からあるreactionは、作成時刻にかかわらず最新SHAの証拠に使わない。

review threadの`isResolved`や`isOutdated`が必要な場合はGraphQLの`reviewThreads`も取得する。`outdated`は行位置が古いことしか示さないため、指摘内容が最新headにも成立するかをコードで確認する。
reviewが遅い間にGHAが完了しても待機を続ける。固定timeoutは設けず、経過時間だけを理由にレビュー完了、指摘なし、失敗とは判断しない。同じSHAへ再依頼を連打せず、poll間隔を維持して状態を共有する。
ただし、marker付きの明示依頼後10分を超えても👀、review、Codex comment、reactionのいずれも新しく現れない場合は、完了待ちではなく依頼未受付として扱う。
repositoryのCode review接続、Automatic reviews設定、正確な`@codex review` trigger、GitHub API取得権限を一度確認する。
確認後も受付signalがなく、再依頼以外に安全な回復手段がなければ、空のpollを続けず外部integration blockerとして未完了条件と必要な人手確認を報告する。
GitHubが明示的な失敗を返す、PRが閉じる、headが第三者により変わる、または認証・権限不足で結果を取得できない場合は、事実を確認してから監視をやり直すかユーザーへ必要な対応を求める。
botが応答したのに既存reactionと区別できないなど、最新SHAへ結び付く完了証拠をGitHub APIから得られない場合は、推測で完了にせず、観測できない証拠と必要な人手確認を示す。

### 自動レビューの全指摘を判断する

最新reviewだけでなく、以前のheadに付いた未判断の自動レビュー指摘も列挙する。古い指摘は、最新headですでに直ったか、まだ成立するかを確認する。

| 判定 | 対応 |
|---|---|
| 最新headでも成立する | 優先度にかかわらず修正し、変更契約に対応する対象限定のローカル確認を行う |
| すでに修正済み | 修正されたコードと必要なテストを確認し、追加変更が不要な根拠を記録する |
| false positive、重複、またはPR変更と無関係 | コード、テスト、仕様の具体的な根拠を確認し、簡潔に返信する |
| 正当だが新しい権限や独立した製品判断が必要 | 自分で先送りして完了にせず、未達条件と必要な判断をユーザーへ示す |

P0からP3などのpriorityは影響度の情報であり、修正不要の理由ではない。PRの変更で生じる、または変更契約に含まれる有効な指摘は、P2やP3でも修正する。
「軽微」「今回は見送る」「別PRで対応する」だけで不要判定にしない。修正しない判断には、誤検知、最新headで解消済み、重複、PRと無関係、または新しい権限・製品判断が必要であることの具体的な根拠を要求する。

有効な指摘を修正した場合は、自己レビューと対象限定のローカル確認を通し、意味のある単位でcommit、pushする。その時点で前のCIとレビュー完了は無効となるため、新しいhead SHAへ一度だけ`@codex review`を依頼し、GHAと自動レビューを両方最初から監視する。

### checkの成功条件

最新head SHAに対する必要なcheckがすべて成功していることを確認する。
cancelled、failure、pending、想定外のskippedまたはmissingを成功と数えない。
人手承認や保護されたenvironmentが必要な場合は、承認を代行したり保護を回避したりしない。

## 7. 完了を判定する

次のすべてを満たした場合だけ完了とする。

- 変更に必要な検証が成功している。CIやレビューの指摘を修正した場合は、該当する対象限定の確認が成功している。
- 再現しなかった失敗は、環境要因などの根拠と修正不要と判断した理由を確認している。
- 依頼範囲の変更がcommit、push済みで、Pull Requestのhead SHAと一致する。
- 最新head SHAに対する必要なcheckがすべて成功した。
- 最新head SHAに結び付くCodex review、指摘なしcomment・summary、またはPull Request作成・レビュー依頼後の新しい👍を確認している。
- 新旧headの自動レビューinline commentをすべて列挙し、有効な指摘は修正済み、それ以外は具体的な根拠を記録済みで、未判断の指摘がない。
- 依頼外の既存変更を編集、stage、commitしていない。

PR URL、最新SHA、ローカル検証結果、PR check結果、自動レビューの完了根拠と各指摘の判断、残した依頼外変更を報告する。

現在branchの履歴が安全にPR化できない、必要なserverやcredentialがない、人手承認が必要、または外部障害が続く場合は、勝手にbranch作成、secret変更、check回避を行わない。
確認済みの事実と必要な対応を示し、完了条件を未達のまま報告する。
