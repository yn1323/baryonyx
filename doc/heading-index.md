# 見出しの索引

[全体索引](README.md)

`doc/` の文書ごとに、見出しとその行番号を並べた生成ファイルである。
分野（`doc/` の直下のフォルダー）ごとに `heading-index/<分野>.md` へ分けてあり、`doc/` の直下の文書は `root.md` にある。
読む節を決めてから、その行から必要な範囲だけを読む（読み方は `doc/rules/documentation-policy.md` の「見出しの索引」、作り方は `doc/tools/README.md`）。
個別データ（`catalog/<分類>/<ID>.md`）と保管庫（`archive/`）は載せない。
手で編集せず、`python -B doc/tools/check_docs.py --write-index` で作り直す。

| 分野 | 文書の数 | 見出しの索引 |
|---|---|---|
| art | 15 | [art.md](heading-index/art.md) |
| catalog | 22 | [catalog.md](heading-index/catalog.md) |
| features | 87 | [features.md](heading-index/features.md) |
| game | 10 | [game.md](heading-index/game.md) |
| plans | 28 | [plans.md](heading-index/plans.md) |
| qa | 3 | [qa.md](heading-index/qa.md) |
| root | 4 | [root.md](heading-index/root.md) |
| rules | 18 | [rules.md](heading-index/rules.md) |
| settings | 1 | [settings.md](heading-index/settings.md) |
| tools | 1 | [tools.md](heading-index/tools.md) |
