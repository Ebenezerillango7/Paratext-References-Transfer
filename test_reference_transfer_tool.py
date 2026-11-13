#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Automated Sandbox Test Suite for Paratext Reference Transfer Tool
================================================================
Strict Isolation:
  - All test files and operations run exclusively inside an isolated temporary directory.
  - Zero touch on real Paratext project folders.
  - 100% standard library compliance.
"""

import unittest
import os
import shutil
import tempfile
from pathlib import Path

from TransferReferencesModern import (
    BookNamesManager,
    canonical_key_for_comparison,
    format_reference_for_target,
    parse_gntd_text,
    parse_reference_payload,
    process_single_sfm_content,
    execute_reference_transfer,
    extract_references_from_project,
    find_sfm_files,
    read_usfm_file,
    write_usfm_file,
)


class TestParatextReferenceTransferTool(unittest.TestCase):
    def setUp(self):
        """Create a completely isolated sandbox directory for each test."""
        self.test_dir = tempfile.TemporaryDirectory()
        self.sandbox_path = Path(self.test_dir.name)
        
        # Create mock target project
        self.target_proj_dir = self.sandbox_path / "MockTarget"
        self.target_proj_dir.mkdir(parents=True, exist_ok=True)

        # Copy IRVTamBooknames.xml as BookNames.xml in MockTarget
        workspace_xml = Path("IRVTamBooknames.xml")
        if workspace_xml.exists():
            shutil.copy2(str(workspace_xml), str(self.target_proj_dir / "BookNames.xml"))

        # Setup mock Genesis SFM file
        self.gen_sfm = self.target_proj_dir / "01GENMockTarget.SFM"
        sample_gen = (
            "\\id GEN - Mock Project\r\n"
            "\\c 5\r\n"
            "\\v 1 This is the book of the generations of Adam.\r\n"
            "\\v 2 Male and female created he them.\r\n"
            "\\c 11\r\n"
            "\\v 9 Therefore is the name of it called Babel.\r\n"
            "\\v 10 These are the generations of Shem.\r\n"
            "\\c 36\r\n"
            "\\s Existing Heading\r\n"
            "\\r யாத் 39:8-21.\r\n"
            "\\p\r\n"
            "\\v 31 And these are the kings that reigned.\r\n"
        )
        write_usfm_file(self.gen_sfm, sample_gen, has_bom=True, newline="\r\n")

    def tearDown(self):
        """Clean up sandbox."""
        self.test_dir.cleanup()

    def test_01_book_names_manager(self):
        """Verify dynamic loading of BookNames.xml with Tamil abbreviations."""
        bm = BookNamesManager(project_dir=self.target_proj_dir)
        self.assertEqual(bm.get_abbr("GEN"), "ஆதி")
        self.assertEqual(bm.get_abbr("1CH"), "1நாளா")
        self.assertEqual(bm.get_abbr("EXO"), "யாத்")
        self.assertEqual(bm.resolve_code("1 Chronicles"), "1CH")
        self.assertEqual(bm.resolve_code("Genesis"), "GEN")
        self.assertEqual(bm.resolve_code("1நாளா"), "1CH")

    def test_02_gntd_parsing_and_target_verse(self):
        """Verify GNTD parsing and target verse rule: end_verse + 1."""
        sample_text = (
            "GEN 5:0:	 \\r (1 Chronicles 1.1-4)\n"
            "GEN 11:9:	 \\r (1 Chronicles 1.24-27)\n"
            "GEN 36:29-30:	 \\r (1 Chronicles 1.43-54)\n"
        )
        rows, errors = parse_gntd_text(sample_text)
        self.assertEqual(len(errors), 0)
        self.assertEqual(len(rows), 3)

        # GEN 5:0 -> verse 1
        self.assertEqual(rows[0]["verse"], 1)
        self.assertEqual(rows[0]["chapter"], 5)
        # GEN 11:9 -> verse 10
        self.assertEqual(rows[1]["verse"], 10)
        self.assertEqual(rows[1]["chapter"], 11)
        # GEN 36:29-30 -> verse 31
        self.assertEqual(rows[2]["verse"], 31)
        self.assertEqual(rows[2]["chapter"], 36)

    def test_03_transfer_gntd_default_options(self):
        """Test default transfer: with parens, heading inserted, and missing paragraph added."""
        options = {
            "use_parentheses": True,
            "insert_heading": True,
            "clean_punctuation": True,
            "create_backups": True,
        }
        res = execute_reference_transfer(
            source_type="GNTD",
            source_val=None,
            target_proj_dir=str(self.target_proj_dir),
            books_scope="SINGLE",
            selected_book="GEN",
            options=options,
            dry_run=False,
        )

        self.assertEqual(res["files_modified"], 1)
        self.assertGreaterEqual(res["refs_added"], 1)

        # Verify content on disk
        updated_content, has_bom, newline = read_usfm_file(self.gen_sfm)
        self.assertTrue(has_bom)
        self.assertEqual(newline, "\r\n")

        # Genesis 5:1 must have \s Insert Heading and \r (1நாளா 1:1-4)
        self.assertIn("\\s Insert Heading", updated_content)
        self.assertIn("\\r (1நாளா 1:1-4)", updated_content)
        self.assertIn("\\p\r\n\\v 1", updated_content)

        # Legacy trailing dot at GEN 36 must be cleaned
        self.assertNotIn("\\r யாத் 39:8-21.", updated_content)
        self.assertIn("\\r யாத் 39:8-21", updated_content)

        # Verify backup was created
        backups = list(self.target_proj_dir.glob("*.bak_*"))
        self.assertEqual(len(backups), 1)

    def test_04_transfer_without_parentheses(self):
        """Verify option: use_parentheses=False produces bare references."""
        options = {
            "use_parentheses": False,
            "insert_heading": True,
            "clean_punctuation": True,
            "create_backups": False,
        }
        execute_reference_transfer(
            source_type="GNTD",
            source_val=None,
            target_proj_dir=str(self.target_proj_dir),
            books_scope="SINGLE",
            selected_book="GEN",
            options=options,
            dry_run=False,
        )

        updated_content, _, _ = read_usfm_file(self.gen_sfm)
        # Should be bare without outer parens: \r 1நாளா 1:1-4
        self.assertIn("\\r 1நாளா 1:1-4", updated_content)
        self.assertNotIn("\\r (1நாளா 1:1-4)", updated_content)

    def test_05_transfer_without_heading_insertion(self):
        """Verify option: insert_heading=False does NOT insert '\\s Insert Heading'."""
        options = {
            "use_parentheses": True,
            "insert_heading": False,
            "clean_punctuation": True,
            "create_backups": False,
        }
        execute_reference_transfer(
            source_type="GNTD",
            source_val=None,
            target_proj_dir=str(self.target_proj_dir),
            books_scope="SINGLE",
            selected_book="GEN",
            options=options,
            dry_run=False,
        )

        updated_content, _, _ = read_usfm_file(self.gen_sfm)
        self.assertNotIn("\\s Insert Heading", updated_content)
        self.assertIn("\\r (1நாளா 1:1-4)", updated_content)

    def test_06_exact_duplicate_prevention(self):
        """Verify running transfer twice skips duplicates and does not corrupt files."""
        options = {
            "use_parentheses": True,
            "insert_heading": True,
            "clean_punctuation": True,
            "create_backups": False,
        }
        # Run 1
        res1 = execute_reference_transfer(
            source_type="GNTD",
            source_val=None,
            target_proj_dir=str(self.target_proj_dir),
            books_scope="SINGLE",
            selected_book="GEN",
            options=options,
            dry_run=False,
        )
        self.assertEqual(res1["files_modified"], 1)

        # Run 2 on same file
        res2 = execute_reference_transfer(
            source_type="GNTD",
            source_val=None,
            target_proj_dir=str(self.target_proj_dir),
            books_scope="SINGLE",
            selected_book="GEN",
            options=options,
            dry_run=False,
        )
        # Zero new references should be added
        self.assertEqual(res2["refs_added"], 0)
        self.assertGreater(res2["duplicates_skipped"], 0)
        self.assertEqual(res2["files_modified"], 0)

    def test_07_dry_run_preview_leaves_files_untouched(self):
        """Verify dry-run returns accurate changes but leaves original files byte-identical."""
        original_bytes = self.gen_sfm.read_bytes()

        options = {
            "use_parentheses": True,
            "insert_heading": True,
            "clean_punctuation": True,
            "create_backups": False,
        }
        res = execute_reference_transfer(
            source_type="GNTD",
            source_val=None,
            target_proj_dir=str(self.target_proj_dir),
            books_scope="SINGLE",
            selected_book="GEN",
            options=options,
            dry_run=True,
        )

        # Expected changes reported
        self.assertGreater(res["refs_added"], 0)
        self.assertEqual(res["files_modified"], 1)

        # File on disk MUST be untouched
        current_bytes = self.gen_sfm.read_bytes()
        self.assertEqual(original_bytes, current_bytes)

    def test_08_project_to_project_transfer(self):
        """Verify extracting from a source project and injecting into target project."""
        # Create a mock source project
        source_proj_dir = self.sandbox_path / "MockSource"
        source_proj_dir.mkdir(parents=True, exist_ok=True)
        shutil.copy2(str(self.target_proj_dir / "BookNames.xml"), str(source_proj_dir / "BookNames.xml"))

        # Create source SFM file with \r lines
        source_gen_sfm = source_proj_dir / "01GENSource.SFM"
        source_content = (
            "\\id GEN Source Project\r\n"
            "\\c 5\r\n"
            "\\s Creation Account\r\n"
            "\\r (1CH 1:1-4)\r\n"
            "\\p\r\n"
            "\\v 1 Adam and his generations.\r\n"
            "\\c 11\r\n"
            "\\r (1CH 1:24-27)\r\n"
            "\\p\r\n"
            "\\v 10 Shem's line.\r\n"
        )
        write_usfm_file(source_gen_sfm, source_content, has_bom=True, newline="\r\n")

        # Test extraction
        rows, errors, src_bm = extract_references_from_project(source_proj_dir, ["GEN"])
        self.assertEqual(len(errors), 0)
        self.assertEqual(len(rows), 2)
        self.assertEqual(rows[0]["verse"], 1)
        self.assertEqual(rows[1]["verse"], 10)

        # Execute project to project transfer
        options = {
            "use_parentheses": True,
            "insert_heading": True,
            "clean_punctuation": True,
            "create_backups": False,
        }
        res = execute_reference_transfer(
            source_type="PROJECT",
            source_val=str(source_proj_dir),
            target_proj_dir=str(self.target_proj_dir),
            books_scope="SINGLE",
            selected_book="GEN",
            options=options,
            dry_run=False,
        )

        self.assertEqual(res["files_modified"], 1)
        self.assertEqual(res["refs_added"], 2)

        updated_content, _, _ = read_usfm_file(self.gen_sfm)
        self.assertIn("\\r (1நாளா 1:1-4)", updated_content)
        self.assertIn("\\r (1நாளா 1:24-27)", updated_content)

    def test_09_poetry_psalms_verse_matching(self):
        """Verify Psalm poetry lines like '\\q1 \\v 1' match correctly without warnings."""
        psa_sfm = self.target_proj_dir / "19PSAMockTarget.SFM"
        psa_content = (
            "\\id PSA Mock\r\n"
            "\\c 14\r\n"
            "\\ms1 Psalm 14\r\n"
            "\\d A Psalm of David.\r\n"
            "\\q1 \\v 1 The fool says in his heart, There is no God.\r\n"
            "\\q2 They are corrupt, they do abominable deeds.\r\n"
        )
        write_usfm_file(psa_sfm, psa_content, has_bom=True, newline="\r\n")

        options = {
            "use_parentheses": True,
            "insert_heading": True,
            "clean_punctuation": True,
            "create_backups": False,
        }
        res = execute_reference_transfer(
            source_type="GNTD",
            source_val=None,
            target_proj_dir=str(self.target_proj_dir),
            books_scope="SINGLE",
            selected_book="PSA",
            options=options,
            dry_run=False,
        )

        self.assertEqual(res["files_modified"], 1)
        self.assertGreaterEqual(res["refs_added"], 1)

        updated_psa, _, _ = read_usfm_file(psa_sfm)
        # Should have parallel reference to Psalm 53 inserted right above \q1 \v 1
        self.assertIn("\\r (சங் 53)", updated_psa)
        self.assertIn("\\q1 \\v 1", updated_psa)
        # Should NOT have inserted redundant \p before poetry marker
        self.assertNotIn("\\p\r\n\\q1", updated_psa)

    def test_10_remove_existing_references(self):
        """Verify remove_existing=True removes old \r lines before inserting new ones."""
        gen_sfm = self.gen_sfm
        dirty_content = (
            "\\id GEN MockTarget\r\n"
            "\\c 5\r\n"
            "\\s1 Old Heading\r\n"
            "\\r (Old Fake Ref 99:99)\r\n"
            "\\p\r\n"
            "\\v 1 This is the book of the generations of Adam.\r\n"
            "\\c 11\r\n"
            "\\r (Another Old Junk Ref)\r\n"
            "\\v 10 These are the generations of Shem.\r\n"
        )
        write_usfm_file(gen_sfm, dirty_content, has_bom=True, newline="\r\n")

        options = {
            "use_parentheses": True,
            "insert_heading": True,
            "clean_punctuation": True,
            "create_backups": False,
            "remove_existing": True,
        }
        res = execute_reference_transfer(
            source_type="GNTD",
            source_val=None,
            target_proj_dir=str(self.target_proj_dir),
            books_scope="SINGLE",
            selected_book="GEN",
            options=options,
            dry_run=False,
        )

        self.assertEqual(res["files_modified"], 1)
        updated_content, _, _ = read_usfm_file(gen_sfm)

        # Confirm old references were wiped out
        self.assertNotIn("Old Fake Ref", updated_content)
        self.assertNotIn("Another Old Junk Ref", updated_content)

        # Confirm new references were inserted
        self.assertIn("\\r (1நாளா 1:1-4)", updated_content)
        self.assertIn("\\r (1நாளா 1:24-27)", updated_content)


if __name__ == "__main__":
    unittest.main()
