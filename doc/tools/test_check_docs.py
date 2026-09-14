"""Exercise document invariants using temporary files, never the working documents."""

import tempfile
import unittest
from pathlib import Path

from check_docs import check


class DocumentationChecks(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.write("doc/README.md", "# Index\n\n[catalog](catalog/characters/README.md)\n[art](art/visual-index.md)\n")
        self.write("doc/catalog/characters/README.md",
                   "# Characters\n\n説明を保持する。\n\n<!-- entries:start -->\n"
                   "登録された個別データはまだない。\n<!-- entries:end -->\n\n末尾も保持する。\n")
        self.write("doc/art/visual-index.md",
                   "# Visuals\n\n<!-- visual-index:start -->\n"
                   "登録された画像対象はまだない。\n<!-- visual-index:end -->\n")

    def write(self, relative, text):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8", newline="\n")
        return path

    def entity(self, identifier="character-test", visual=True):
        fields = [
            "---", f"id: {identifier}", f"type: {'visual-entity' if visual else 'definition'}",
            "status: 候補", "updated: 2026-09-13", "name: テスト用", "category: characters",
        ]
        if visual:
            fields += [
                "silhouette: 細身", "palette: 青 | 白", "motifs: 月",
                "personality: 慎重", "art_status: 未着手",
            ]
        return "\n".join(fields) + "\n---\n\n# テスト用\n\n[分類](README.md)\n"

    def test_empty_catalog_is_valid(self):
        self.assertEqual([], check(self.root))

    def test_stale_index_fails_then_regenerates_and_preserves_prose(self):
        self.write("doc/catalog/characters/character-test.md", self.entity())
        self.assertTrue(any("生成索引が古い" in e for e in check(self.root)))
        self.assertEqual([], check(self.root, write_index=True))
        catalog = (self.root / "doc/catalog/characters/README.md").read_text(encoding="utf-8")
        visual = (self.root / "doc/art/visual-index.md").read_text(encoding="utf-8")
        self.assertIn("説明を保持する。", catalog)
        self.assertIn("末尾も保持する。", catalog)
        self.assertIn("[定義](character-test.md)", catalog)
        self.assertIn("青 &#124; 白", visual)
        self.assertEqual([], check(self.root))

    def test_metadata_change_invalidates_visual_index(self):
        path = self.write("doc/catalog/characters/character-test.md", self.entity())
        self.assertEqual([], check(self.root, write_index=True))
        path.write_text(self.entity().replace("personality: 慎重", "personality: 快活"), encoding="utf-8")
        self.assertTrue(any("visual-index.md: 生成索引が古い" in e for e in check(self.root)))

    def test_definition_is_excluded_from_visual_index(self):
        self.write("doc/catalog/characters/character-test.md", self.entity(visual=False))
        self.assertEqual([], check(self.root, write_index=True))
        visual = (self.root / "doc/art/visual-index.md").read_text(encoding="utf-8")
        self.assertIn("登録された画像対象はまだない。", visual)

    def test_duplicate_id_and_filename_mismatch_fail_without_writing(self):
        self.write("doc/catalog/characters/character-test.md", self.entity())
        self.write("doc/catalog/characters/character-other.md", self.entity())
        before = (self.root / "doc/catalog/characters/README.md").read_bytes()
        errors = check(self.root, write_index=True)
        self.assertTrue(any("IDが重複" in e for e in errors))
        self.assertTrue(any("IDとファイル名" in e for e in errors))
        self.assertEqual(before, (self.root / "doc/catalog/characters/README.md").read_bytes())

    def test_missing_visual_field_and_wrong_category_fail(self):
        content = self.entity().replace("palette: 青 | 白\n", "").replace("category: characters", "category: enemies")
        self.write("doc/catalog/characters/character-test.md", content)
        errors = check(self.root)
        self.assertTrue(any("比較用項目がない: palette" in e for e in errors))
        self.assertTrue(any("categoryと配置" in e for e in errors))

    def test_invalid_date_status_and_missing_metadata_fail(self):
        self.write("doc/catalog/characters/character-test.md",
                   self.entity().replace("2026-09-13", "2026-02-31").replace("status: 候補", "status: unknown"))
        self.write("doc/game/missing.md", "# Missing\n")
        errors = check(self.root)
        self.assertTrue(any("updatedは" in e for e in errors))
        self.assertTrue(any("statusが不正" in e for e in errors))
        self.assertTrue(any("必須メタデータがない" in e for e in errors))

    def test_broken_file_and_heading_links_fail(self):
        self.write("doc/README.md", "# Index\n\n[missing](absent.md)\n[heading](#absent)\n")
        errors = check(self.root)
        self.assertTrue(any("リンク先がない: absent.md" in e for e in errors))
        self.assertTrue(any("見出しがない: #absent" in e for e in errors))

    def test_japanese_anchor_and_external_urls_and_fenced_examples(self):
        path = self.root / "doc/README.md"
        content = path.read_text(encoding="utf-8")
        content += "\n[local](#google認証の設定)\n[web](https://example.invalid/absent)\n"
        content += "\n## Google認証の設定\n\n"
        content += chr(96) * 3 + "md\n[example](absent.md)\n" + chr(96) * 3 + "\n"
        path.write_text(content, encoding="utf-8")
        self.assertEqual([], check(self.root))

    def test_unreachable_document_fails(self):
        self.write("doc/orphan.md", "# Orphan\n")
        self.assertTrue(any("orphan.md: 全体索引から到達できない" in e for e in check(self.root)))

    def test_missing_generation_markers_fail(self):
        self.write("doc/catalog/characters/README.md", "# Characters\n")
        self.assertTrue(any("entries生成領域" in e for e in check(self.root)))

    def test_template_type_left_in_catalog_fails(self):
        self.write("doc/catalog/characters/character-test.md",
                   self.entity().replace("type: visual-entity", "type: template"))
        self.assertTrue(any("個別データのtype" in e for e in check(self.root)))

    def test_structured_metadata_is_rejected(self):
        self.write("doc/catalog/characters/character-test.md",
                   self.entity().replace("palette: 青 | 白", "palette: [青, 白]"))
        self.assertTrue(any("引用符・構造・コメント" in e for e in check(self.root)))


if __name__ == "__main__":
    unittest.main()
