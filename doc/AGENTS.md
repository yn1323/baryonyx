# 文書作業の入口

[文書索引](README.md)から対象分野を選び、対応する仕様と参照先だけを読む。
文書の配置・形式・節の区分・更新方法・仕様追加時の矛盾の扱いは[文書管理方針](rules/documentation-policy.md)に従う。

- 「候補」「未決事項」を推測で採用済みの仕様へ変えない。企画の確定と実装完了を混同せず、現行の動作は各仕様の「実装との対応」とコードで確かめる。
- 生成領域（`<!-- entries:start/end -->`・`<!-- visual-index:start/end -->`）と[見出しの索引](heading-index.md)を手で編集しない。`doc/` を変えたら `python -B doc/tools/check_docs.py --write-index` で作り直し、[文書検査](tools/README.md)を通す。
