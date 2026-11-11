"""Regression checks for the IRV Tamil GNTD reference importer."""
import importlib.util
import pathlib
import re
import shutil
import tempfile
import unittest


ROOT = pathlib.Path(__file__).resolve().parent
MODULE_PATH = ROOT / "IRVTam_Reference_Separator.py"
SPEC = importlib.util.spec_from_file_location("reference_importer", str(MODULE_PATH))
APP = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(APP)
BOOK_DATA = APP.load_booknames(str(ROOT / "IRVTamBooknames.xml"))
GNTD_TEXT = (ROOT / "Pre-Requisites" / "GNTD References.txt").read_text(encoding="utf-8-sig")
ROWS, PARSE_ERRORS = APP.parse_gntd_references(GNTD_TEXT)


def rows_at(chapter, verse):
    return [row for row in ROWS if row["book"] == "GEN" and row["chapter"] == chapter and row["verse"] == verse]


class GntdImporterTests(unittest.TestCase):
    def test_all_gntd_rows_parse(self):
        self.assertEqual([], PARSE_ERRORS)
        self.assertEqual(272, len(ROWS))
        self.assertEqual(1, next(row for row in ROWS if row["book"] == "GEN" and row["chapter"] == 10)["verse"])
        chapter_start = next(row for row in ROWS if row["book"] == "GEN" and row["chapter"] == 10)
        self.assertEqual("0", chapter_start["source_location"])

    def test_english_names_convert_to_xml_abbreviations(self):
        converted = APP._english_reference_to_tamil(
            "(Exodus 34.18-26; Deuteronomy 16.1-17)", BOOK_DATA
        )
        self.assertEqual("யாத் 34:18-26; உபா 16:1-17", converted)
        self.assertEqual(
            APP.canonical_reference(converted, BOOK_DATA),
            APP.canonical_reference("\\r (Exodus 34.18-26; Deuteronomy 16.1-17)", BOOK_DATA),
        )

    def test_chapter_start_and_missing_paragraph(self):
        source = "\\id GEN\n\\c 10\n\\s Existing title\n\\v 1 First verse\n"
        transformed, changes, skipped, errors = APP.import_gntd_references(source, "GEN", rows_at(10, 1), BOOK_DATA)
        self.assertFalse(errors)
        self.assertEqual(0, skipped)
        self.assertIn("\\s Insert Heading\n\\r (1நாளா 1:5-23)\n\\p\n\\v 1 First verse", transformed)
        self.assertEqual("GEN 10:0", next(item for item in changes if item.get("kind") == "reference")["gntd_location"])
        self.assertEqual(1, sum(item.get("kind") == "paragraph" for item in changes))

    def test_direct_verse_and_wrong_existing_reference_is_preserved(self):
        source = "\\id GEN\n\\c 11\n\\s Existing title\n\\r (ஆதி 11:1)\n\\v 9 Target verse\n"
        transformed, changes, skipped, errors = APP.import_gntd_references(source, "GEN", rows_at(11, 9), BOOK_DATA)
        self.assertFalse(errors)
        self.assertEqual(0, skipped)
        self.assertIn("\\r (ஆதி 11:1)\n\\s Insert Heading\n\\r (1நாளா 1:24-27)\n\\p\n\\v 9 Target verse", transformed)
        self.assertEqual(1, sum(item.get("kind") == "reference" for item in changes))

    def test_exact_reference_is_not_duplicated(self):
        source = "\\id GEN\n\\c 11\n\\s Existing title\n\\r (1நாளா 1:24-27)\n\\p\n\\v 9 Target verse\n"
        transformed, changes, skipped, errors = APP.import_gntd_references(source, "GEN", rows_at(11, 9), BOOK_DATA)
        self.assertEqual(source, transformed)
        self.assertEqual([], changes)
        self.assertEqual(1, skipped)
        self.assertFalse(errors)

    def test_book_id_mismatch_is_rejected(self):
        with self.assertRaises(ValueError):
            APP.import_gntd_references("\\id EXO\n", "GEN", ROWS, BOOK_DATA)

    def test_write_preserves_bom_and_crlf_and_backup_keeps_original(self):
        source_sample = ROOT / "Pre-Requisites" / "SFM Samples" / "01GENIRVTam.SFM"
        with tempfile.TemporaryDirectory() as directory:
            selected = pathlib.Path(directory) / "GEN.SFM"
            unselected = pathlib.Path(directory) / "EXO.SFM"
            shutil.copy2(str(source_sample), str(selected))
            exo_sample = ROOT / "Pre-Requisites" / "SFM Samples" / "02EXOIRVTam.SFM"
            shutil.copy2(str(exo_sample), str(unselected))
            original_bytes = selected.read_bytes()
            unselected_bytes = unselected.read_bytes()
            original_text, has_bom = APP.read_usfm(selected)
            transformed, changes, _skipped, errors = APP.import_gntd_references(
                original_text, "GEN", ROWS, BOOK_DATA
            )
            self.assertTrue(changes)
            self.assertEqual([], errors)
            backup = APP.unique_backup_path(selected, "test")
            shutil.copy2(str(selected), str(backup))
            APP.write_usfm(selected, transformed, has_bom)
            self.assertEqual(original_bytes, backup.read_bytes())
            self.assertEqual(unselected_bytes, unselected.read_bytes())
            self.assertEqual(original_bytes.startswith(b"\xef\xbb\xbf"), selected.read_bytes().startswith(b"\xef\xbb\xbf"))
            self.assertEqual(b"\r\n" in original_bytes, b"\r\n" in selected.read_bytes())

    def test_full_apply_on_temporary_copies_and_backups(self):
        sample_dir = ROOT / "Pre-Requisites" / "SFM Samples"
        reference_count = backup_count = 0
        with tempfile.TemporaryDirectory() as directory:
            target_dir = pathlib.Path(directory)
            for source in sample_dir.glob("*.SFM"):
                target = target_dir / source.name
                shutil.copy2(str(source), str(target))
                before, has_bom = APP.read_usfm(target)
                code = APP._find_book_file_id(before)
                book_rows = [row for row in ROWS if row["book"] == code]
                transformed, changes, _skipped, errors = APP.import_gntd_references(
                    before, code, book_rows, BOOK_DATA
                )
                self.assertEqual([], errors, source.name)
                if changes:
                    backup = APP.unique_backup_path(target, "regression")
                    shutil.copy2(str(target), str(backup))
                    APP.write_usfm(target, transformed, has_bom)
                    self.assertEqual(source.read_bytes(), backup.read_bytes())
                    backup_count += 1
                    reference_count += sum(item.get("kind") == "reference" for item in changes)
                after, _ = APP.read_usfm(target)
                again, repeated_changes, _skipped, repeated_errors = APP.import_gntd_references(
                    after, code, book_rows, BOOK_DATA
                )
                self.assertEqual(after, again, source.name)
                self.assertEqual([], repeated_changes, source.name)
                self.assertEqual([], repeated_errors, source.name)
        self.assertEqual(261, reference_count)
        self.assertEqual(20, backup_count)

    def test_all_39_samples_preview_without_errors_and_are_idempotent(self):
        sample_dir = ROOT / "Pre-Requisites" / "SFM Samples"
        sample_files = list(sample_dir.glob("*.SFM"))
        self.assertEqual(39, len(sample_files))
        codes = set()
        reference_insertions = exact_matches = second_pass_changes = 0
        for path in sample_files:
            content, _bom = APP.read_usfm(path)
            code = APP._find_book_file_id(content)
            codes.add(code)
            transformed, changes, skipped, errors = APP.import_gntd_references(content, code, ROWS, BOOK_DATA)
            self.assertEqual([], errors, path.name)
            exact_matches += skipped
            reference_insertions += sum(item.get("kind") == "reference" for item in changes)
            output_lines = transformed.splitlines()
            for index, line in enumerate(output_lines):
                if line.startswith("\\r (") and index and output_lines[index - 1] == "\\s Insert Heading":
                    self.assertNotRegex(line, r"Genesis|Exodus|Leviticus|Numbers|Deuteronomy")
            again, changes_again, _skipped_again, errors_again = APP.import_gntd_references(
                transformed, code, ROWS, BOOK_DATA
            )
            self.assertEqual(transformed, again)
            self.assertEqual([], errors_again, path.name)
            second_pass_changes += len(changes_again)
        self.assertEqual(set(APP.BOOK_ORDER), codes)
        self.assertEqual(261, reference_insertions)
        self.assertEqual(11, exact_matches)
        self.assertEqual(0, second_pass_changes)


if __name__ == "__main__":
    unittest.main(verbosity=2)
