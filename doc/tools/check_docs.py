"""Validate repository Markdown and rebuild catalog indexes, without dependencies."""

from __future__ import annotations

import argparse
import html
import re
from collections import Counter
from datetime import date
from pathlib import Path
from urllib.parse import unquote, urlsplit

TYPES = {"specification", "reference", "source", "definition", "visual-entity", "template"}
STATUSES = {"未決", "候補", "一部確定", "確定", "見送り", "記録", "運用中", "テンプレート"}
ENTITY_TYPES = {"definition", "visual-entity"}
VISUAL_KEYS = ("silhouette", "palette", "motifs", "personality", "art_status")
LEGACY_FEATURES = {"health-data.md", "server-health.md"}
LINK = re.compile(r"!?\[[^\]\n]*\]\((<[^>\n]+>|[^)\n]+)\)")
ID = re.compile(r"[a-z][a-z0-9]*(?:-[a-z0-9]+)*")


def prose(text: str) -> str:
    """Remove fenced examples, keeping actual headings and inline Markdown links."""
    lines = []
    fence = ""
    for line in text.splitlines():
        marker = re.match(r"^\s*(\x60{3,}|~{3,})", line)
        if marker:
            run = marker.group(1)
            if not fence:
                fence = run
            elif run[0] == fence[0] and len(run) >= len(fence):
                fence = ""
            continue
        if not fence:
            lines.append(line)
    return "\n".join(lines)


def anchors(text: str) -> set[str]:
    result = set()
    counts: Counter[str] = Counter()
    for line in prose(text).splitlines():
        match = re.match(r"^#{1,6}\s+(.+?)\s*#*\s*$", line)
        if not match:
            continue
        title = re.sub(r"\[([^\]]+)\]\([^)]+\)", r"\1", match.group(1))
        slug = re.sub(r"[^\w\- ]", "", title.lower()).replace(" ", "-")
        occurrence = counts[slug]
        counts[slug] += 1
        result.add(slug if occurrence == 0 else f"{slug}-{occurrence}")
    result.update(re.findall(r'<a\s+(?:id|name)="([^"]+)"', text))
    return result


def metadata(text: str, label: str, errors: list[str]) -> dict[str, str]:
    if not text.startswith("---\n"):
        return {}
    lines = text.splitlines()
    try:
        end = lines.index("---", 1)
    except ValueError:
        errors.append(f"{label}: メタデータの終端がない")
        return {}
    values = {}
    for line in lines[1:end]:
        if not line.strip():
            continue
        match = re.fullmatch(r"([a-z_]+): ([^\r\n]+)", line)
        if not match:
            errors.append(f"{label}: メタデータは1行1項目の文字列にする: {line}")
            continue
        key, value = match.groups()
        if value[0] in "[]{}>|&*!'\"" or ": " in value or " #" in value or value.startswith("- "):
            errors.append(f"{label}: メタデータは引用符・構造・コメントのない文字列にする: {key}")
        if key in values:
            errors.append(f"{label}: メタデータ項目が重複: {key}")
        values[key] = value.strip()
    return values


def needs_metadata(path: Path, docs: Path) -> bool:
    relative = path.relative_to(docs)
    if path.name in {"README.md", "AGENTS.md"} or relative.as_posix() == "art/visual-index.md":
        return False
    return (
        relative.parts[0] in {"game", "art", "catalog"}
        or (relative.parts[0] == "features" and path.name not in LEGACY_FEATURES)
        or relative.as_posix() == "architecture.md"
    )


def cell(value: str) -> str:
    return html.escape(value, quote=False).replace("|", "&#124;").replace(chr(96), "&#96;")


def replace_region(text: str, marker: str, body: str) -> str | None:
    start = f"<!-- {marker}:start -->"
    end = f"<!-- {marker}:end -->"
    if text.count(start) != 1 or text.count(end) != 1:
        return None
    before, tail = text.split(start, 1)
    if end not in tail:
        return None
    _, after = tail.split(end, 1)
    return f"{before}{start}\n{body}\n{end}{after}"


def check(root: Path, write_index: bool = False) -> list[str]:
    root = root.resolve()
    docs = root / "doc"
    contents = {path: path.read_text(encoding="utf-8-sig") for path in sorted(docs.rglob("*.md"))}
    errors: list[str] = []
    identifiers = {}
    entities = []
    for path, content in contents.items():
        label = path.relative_to(root).as_posix()
        data = metadata(content, label, errors)
        if not data and not needs_metadata(path, docs):
            continue
        for key in ("id", "type", "status", "updated"):
            if not data.get(key):
                errors.append(f"{label}: 必須メタデータがない: {key}")
        identifier = data.get("id", "")
        if identifier:
            if not ID.fullmatch(identifier):
                errors.append(f"{label}: IDの形式が不正: {identifier}")
            if identifier in identifiers:
                errors.append(f"{label}: IDが重複: {identifier} ({identifiers[identifier]})")
            identifiers[identifier] = label
        if data.get("type") not in TYPES:
            errors.append(f"{label}: typeが不正")
        if data.get("status") not in STATUSES:
            errors.append(f"{label}: statusが不正")
        try:
            value = data.get("updated", "")
            if not re.fullmatch(r"\d{4}-\d{2}-\d{2}", value):
                raise ValueError
            date.fromisoformat(value)
        except ValueError:
            errors.append(f"{label}: updatedはYYYY-MM-DDの実在日付にする")
        relative = path.relative_to(docs)
        in_catalog = relative.parts[0] == "catalog" and path.name != "README.md"
        in_templates = len(relative.parts) > 1 and relative.parts[1] == "templates"
        if in_catalog and not in_templates and data.get("type") not in ENTITY_TYPES:
            errors.append(f"{label}: 個別データのtypeはdefinitionかvisual-entityにする")
        if data.get("type") not in ENTITY_TYPES:
            continue
        if len(relative.parts) != 3 or relative.parts[0] != "catalog" or relative.parts[1] == "templates":
            errors.append(f"{label}: 個別データはcatalog/<分類>/<ID>.mdへ置く")
            continue
        for key in ("name", "category"):
            if not data.get(key):
                errors.append(f"{label}: 個別データの必須項目がない: {key}")
        if data.get("category") != path.parent.name:
            errors.append(f"{label}: categoryと配置が一致しない")
        if path.stem != identifier:
            errors.append(f"{label}: IDとファイル名が一致しない")
        if path.parent / "README.md" not in contents:
            errors.append(f"{label}: 分類のREADMEがない")
        if data["type"] == "visual-entity":
            for key in VISUAL_KEYS:
                if not data.get(key):
                    errors.append(f"{label}: 比較用項目がない: {key}")
        entities.append((path, data))

    generated = {}
    for path, content in contents.items():
        relative = path.relative_to(docs)
        if len(relative.parts) != 3 or relative.parts[0] != "catalog" or path.name != "README.md":
            continue
        selected = [(p, d) for p, d in entities if p.parent == path.parent]
        body = "登録された個別データはまだない。"
        if selected:
            lines = ["| ID | 名称 | 状態 | 詳細 |", "|---|---|---|---|"]
            for entity, data in sorted(selected, key=lambda row: row[1].get("id", "")):
                lines.append(
                    f"| {cell(data.get('id', ''))} | {cell(data.get('name', ''))} | "
                    f"{cell(data.get('status', ''))} | [定義]({entity.name}) |"
                )
            body = "\n".join(lines)
        expected = replace_region(content, "entries", body)
        if expected is None:
            errors.append(f"{relative}: entries生成領域がない、または重複している")
        else:
            generated[path] = expected

    visual_path = docs / "art" / "visual-index.md"
    if visual_path in contents:
        visual_entities = [(p, d) for p, d in entities if d["type"] == "visual-entity"]
        body = "登録された画像対象はまだない。"
        if visual_entities:
            lines = [
                "| ID・詳細 | 名称・状態 | 輪郭 | 配色 | モチーフ | 性格・行動 | 画像の状態 |",
                "|---|---|---|---|---|---|---|",
            ]
            for path, data in sorted(visual_entities, key=lambda row: row[1].get("id", "")):
                link = "../" + path.relative_to(docs).as_posix()
                values = " | ".join(cell(data.get(key, "")) for key in VISUAL_KEYS)
                lines.append(
                    f"| [{data.get('id', '')}]({link}) | "
                    f"{cell(data.get('name', ''))}（{cell(data.get('status', ''))}） | {values} |"
                )
            body = "\n".join(lines)
        expected = replace_region(contents[visual_path], "visual-index", body)
        if expected is None:
            errors.append("art/visual-index.md: visual-index生成領域がない、または重複している")
        else:
            generated[visual_path] = expected
    elif any(d["type"] == "visual-entity" for _, d in entities):
        errors.append("art/visual-index.md: 比較索引がない")

    # Invalid metadata must never rewrite indexes.
    may_write = write_index and not errors
    for path, expected in generated.items():
        if contents[path] == expected:
            continue
        if may_write:
            path.write_text(expected, encoding="utf-8", newline="\n")
            contents[path] = expected
        else:
            errors.append(f"{path.relative_to(root)}: 生成索引が古い。--write-indexを実行する")

    edges = {path: set() for path in contents}
    for path, content in contents.items():
        for match in LINK.finditer(prose(content)):
            target = match.group(1).strip()
            if target.startswith("<") and target.endswith(">"):
                target = target[1:-1]
            else:
                target = re.sub(r'\s+"[^"]*"$', "", target)
            parsed = urlsplit(target)
            if parsed.scheme or parsed.netloc:
                continue
            raw_path = unquote(parsed.path)
            if raw_path.startswith("/"):
                destination = root / raw_path.lstrip("/")
            else:
                destination = path.parent / raw_path if raw_path else path
            destination = destination.resolve()
            label = path.relative_to(root).as_posix()
            if not destination.is_relative_to(root):
                errors.append(f"{label}: ローカルリンクがリポジトリ外を指す: {target}")
                continue
            if not destination.exists():
                errors.append(f"{label}: リンク先がない: {target}")
                continue
            if destination in contents:
                edges[path].add(destination)
            if parsed.fragment and destination.suffix.lower() == ".md" and destination.is_file():
                text = contents.get(destination)
                if text is None:
                    text = destination.read_text(encoding="utf-8-sig")
                if unquote(parsed.fragment) not in anchors(text):
                    errors.append(f"{label}: 見出しがない: {target}")

    entry = docs / "README.md"
    if entry not in contents:
        errors.append("doc/README.md: 全体索引がない")
    visited = set()
    queue = [entry]
    while queue:
        current = queue.pop()
        if current in visited:
            continue
        visited.add(current)
        queue.extend(edges.get(current, ()))
    for path in contents:
        if path not in visited:
            errors.append(f"{path.relative_to(root)}: 全体索引から到達できない")
    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write-index", action="store_true", help="個別MDから生成索引を更新する")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    errors = check(root, args.write_index)
    for error in errors:
        print(error)
    if errors:
        print(f"文書検査: {len(errors)}件のエラー")
        return 1
    print("文書検査: リンク・ID・メタデータ・索引の検査に成功")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
