#!/usr/bin/env python3
"""
IRV Tamil – Reference Separator
A standalone Tkinter tool for moving scripture references out of USFM \s headings
into a following \r (parallel reference) marker.

Python 3.9+ recommended. Uses only Python standard-library modules.
"""

import re
import shutil
import threading
import tkinter as tk
from tkinter import filedialog, messagebox, ttk
from pathlib import Path
from datetime import datetime
import xml.etree.ElementTree as ET
import os
import tempfile

BOOK_ORDER = [
    "GEN", "EXO", "LEV", "NUM", "DEU", "JOS", "JDG", "RUT",
    "1SA", "2SA", "1KI", "2KI", "1CH", "2CH", "EZR", "NEH",
    "EST", "JOB", "PSA", "PRO", "ECC", "SNG", "ISA", "JER",
    "LAM", "EZK", "DAN", "HOS", "JOL", "AMO", "OBA", "JON",
    "MIC", "NAM", "HAB", "ZEP", "HAG", "ZEC", "MAL"
]

ENGLISH_BOOK_NAMES = {
    "GEN": "Genesis", "EXO": "Exodus", "LEV": "Leviticus", "NUM": "Numbers",
    "DEU": "Deuteronomy", "JOS": "Joshua", "JDG": "Judges", "RUT": "Ruth",
    "1SA": "1 Samuel", "2SA": "2 Samuel", "1KI": "1 Kings", "2KI": "2 Kings",
    "1CH": "1 Chronicles", "2CH": "2 Chronicles", "EZR": "Ezra", "NEH": "Nehemiah",
    "EST": "Esther", "JOB": "Job", "PSA": "Psalms", "PRO": "Proverbs",
    "ECC": "Ecclesiastes", "SNG": "Song of Solomon", "ISA": "Isaiah", "JER": "Jeremiah",
    "LAM": "Lamentations", "EZK": "Ezekiel", "DAN": "Daniel", "HOS": "Hosea",
    "JOL": "Joel", "AMO": "Amos", "OBA": "Obadiah", "JON": "Jonah",
    "MIC": "Micah", "NAM": "Nahum", "HAB": "Habakkuk", "ZEP": "Zephaniah",
    "HAG": "Haggai", "ZEC": "Zechariah", "MAL": "Malachi",
}


def parse_gntd_references(text):
    """Parse GNTD rows as (book code, chapter, verse start, reference text)."""
    rows = []
    errors = []
    for line_number, line in enumerate(text.splitlines(), 1):
        if not line.strip():
            continue
        match = re.match(
            r"^([A-Z0-9]{3})\s+(\d+):(\d+(?:-\d+)?)\s*:\s*\\r\s*(.*?)\s*$", line
        )
        if not match:
            errors.append("Line {}: unrecognized GNTD row".format(line_number))
            continue
        location = match.group(3)
        start_verse = int(location.split("-", 1)[0])
        rows.append({
            "line": line_number,
            "book": match.group(1),
            "chapter": int(match.group(2)),
            "source_location": location,
            # In this GNTD file, chapter:0 means the start of the chapter.
            "verse": 1 if start_verse == 0 else start_verse,
            "reference": match.group(4).strip(),
        })
    return rows, errors


def _english_reference_to_tamil(reference, book_data):
    """Replace each English OT book name with its exact XML abbreviation."""
    english = sorted(ENGLISH_BOOK_NAMES.items(), key=lambda item: len(item[1]), reverse=True)
    english.append(("PSA", "Psalm"))
    english.append(("SNG", "Song of Songs"))
    reference = reference.strip().strip("()[] ")
    parts = []
    current_code = None
    for part in reference.split(";"):
        value = part.strip()
        found = None
        for code, name in english:
            if value[:len(name)].casefold() == name.casefold() and len(value) > len(name) and value[len(name)].isspace():
                found = (code, value[len(name):].strip())
                break
        if found:
            current_code = found[0]
            passage = found[1]
        elif current_code:
            passage = value
        else:
            raise ValueError("Unrecognized English book name in reference: " + value)
        passage = re.sub(r"\s*([.,;])\s*", r"\1", passage)
        passage = re.sub(r"\s*([-–—])\s*", r"\1", passage)
        passage = passage.replace(".", ":")
        parts.append(book_data[current_code][0] + " " + passage)
    return "; ".join(parts)


def canonical_reference(reference, book_data):
    """Canonicalize an English or Tamil \r payload for duplicate comparison."""
    value = re.sub(r"^\\r\s*", "", reference.strip())
    value = value.strip().strip("()[]").strip().rstrip(".")
    aliases = {}
    for code, (abbr, names) in book_data.items():
        for name in [abbr] + names:
            aliases[name] = code
    english_aliases = {name: code for code, name in ENGLISH_BOOK_NAMES.items()}
    english_aliases.update({"Psalm": "PSA", "Song of Songs": "SNG"})
    aliases.update(english_aliases)
    ordered = sorted(aliases, key=len, reverse=True)
    result = []
    for part in value.split(";"):
        part = part.strip()
        code = None
        passage = ""
        for name in ordered:
            if part[:len(name)].casefold() == name.casefold() and len(part) > len(name) and part[len(name)].isspace():
                code, passage = aliases[name], part[len(name):].strip()
                break
        if code is None:
            return None
        passage = re.sub(r"\s+", "", passage).replace(".", ":")
        passage = passage.replace("–", "-").replace("—", "-")
        result.append(code + ":" + passage)
    return ";".join(result)


def _find_book_file_id(content):
    match = re.search(r"^\\id\s+([A-Z0-9]{3})(?:\s|$)", content, re.MULTILINE)
    return match.group(1) if match else None


def import_gntd_references(content, book_code, rows, book_data):
    """Insert GNTD references at their verse locations without replacing old \r lines."""
    actual_code = _find_book_file_id(content)
    if actual_code != book_code:
        raise ValueError("Expected \\id {}, found {}".format(book_code, actual_code or "no \\id marker"))
    lines = content.splitlines(keepends=True)
    newline = "\r\n" if "\r\n" in content else "\r" if "\r" in content and "\n" not in content else "\n"
    changes = []
    skipped_exact = 0
    errors = []

    for row in rows:
        if row["book"] != book_code:
            continue
        try:
            ref_payload = _english_reference_to_tamil(row["reference"], book_data)
        except ValueError as exc:
            errors.append("GNTD line {}: {}".format(row["line"], exc))
            continue
        wanted_key = canonical_reference(ref_payload, book_data)
        chapter_index = None
        for index, line in enumerate(lines):
            match = re.match(r"^\\c\s+(\d+)(?:\s|$)", line)
            if match and int(match.group(1)) == row["chapter"]:
                chapter_index = index
                break
        if chapter_index is None:
            errors.append("GNTD line {}: chapter {} not found in {}".format(row["line"], row["chapter"], book_code))
            continue
        verse_index = None
        for index in range(chapter_index + 1, len(lines)):
            if re.match(r"^\\c\s+", lines[index]):
                break
            verse_match = re.match(r"^\\v\s+(\d+)(?:-\d+)?(?:\s|$)", lines[index])
            if verse_match and int(verse_match.group(1)) == row["verse"]:
                verse_index = index
                break
        if verse_index is None:
            errors.append("GNTD line {}: verse {}:{} not found in {}".format(row["line"], row["chapter"], row["verse"], book_code))
            continue

        # A paragraph marker must immediately precede the target verse.
        paragraph_added = verse_index == 0 or not re.match(r"^\\p(?:\s|$)", lines[verse_index - 1])
        if paragraph_added:
            lines.insert(verse_index, "\\p" + newline)
            verse_index += 1
            changes.append({
                "kind": "paragraph", "gntd_line": row["line"],
                "location": "{} {}:{}".format(book_code, row["chapter"], row["verse"]),
                "gntd_location": "{} {}:{}".format(book_code, row["chapter"], row["source_location"]),
                "reference": "Added \\p immediately before target verse",
            })
        paragraph_index = verse_index - 1

        # Compare existing references in this local heading/reference block.
        scan = paragraph_index - 1
        existing = []
        while scan >= 0 and (re.match(r"^\\(?:r|s\d*)(?:\s|$)", lines[scan]) or not lines[scan].strip()):
            if re.match(r"^\\r(?:\s|$)", lines[scan]):
                existing.append(lines[scan].strip())
            scan -= 1
        if any(canonical_reference(item, book_data) == wanted_key for item in existing):
            skipped_exact += 1
            continue

        insert_at = paragraph_index
        heading = "\\s Insert Heading" + newline
        ref_line = "\\r ({} )".format(ref_payload) + newline
        ref_line = ref_line.replace(" )", ")")
        lines[insert_at:insert_at] = [heading, ref_line]
        changes.append({
            "kind": "reference",
            "gntd_line": row["line"], "location": "{} {}:{}".format(book_code, row["chapter"], row["verse"]),
            "gntd_location": "{} {}:{}".format(book_code, row["chapter"], row["source_location"]),
            "heading": heading.rstrip("\r\n"), "reference": ref_line.rstrip("\r\n"),
            "existing_references": list(reversed(existing)),
        })

    return "".join(lines), changes, skipped_exact, errors


def read_usfm(path):
    """Decode UTF-8 while keeping the original CRLF/LF/CR bytes intact."""
    raw = Path(path).read_bytes()
    has_bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig" if has_bom else "utf-8")
    return text, has_bom


def write_usfm(path, text, has_bom):
    """Write UTF-8 without newline translation, retaining a source BOM if present."""
    payload = text.encode("utf-8")
    if has_bom:
        payload = b"\xef\xbb\xbf" + payload
    target = Path(path)
    fd, temporary = tempfile.mkstemp(prefix=target.name + ".", suffix=".tmp", dir=str(target.parent))
    try:
        with os.fdopen(fd, "wb") as stream:
            stream.write(payload)
        os.replace(temporary, str(target))
    except Exception:
        try:
            os.unlink(temporary)
        except OSError:
            pass
        raise


def unique_backup_path(path, stamp):
    base = Path(str(path) + ".backup_" + stamp)
    candidate = base
    suffix = 1
    while candidate.exists():
        candidate = Path(str(base) + "_" + str(suffix))
        suffix += 1
    return candidate

def load_booknames(xml_path):
    """Return code -> (TOC3 abbreviation, aliases) from this XML's attributes."""
    root = ET.parse(xml_path).getroot()
    result = {}
    for node in root.findall(".//book"):
        code = (node.get("code") or "").strip().upper()
        abbr = (node.get("abbr") or "").strip()
        if not code or not abbr:
            continue
        aliases = []
        for attr in ("abbr", "short", "long"):
            value = (node.get(attr) or "").strip()
            if value and value not in aliases:
                aliases.append(value)

        # Some numbered book names occur with/without a space after the number.
        for value in list(aliases):
            m = re.match(r"^([1-3])\s*(.+)$", value)
            if m:
                spaced = m.group(1) + " " + m.group(2)
                unspaced = m.group(1) + m.group(2)
                for variant in (spaced, unspaced):
                    if variant not in aliases:
                        aliases.append(variant)
        result[code] = (abbr, aliases)
    missing = [code for code in BOOK_ORDER if code not in result]
    if missing:
        raise ValueError("The XML is missing usable book names for: " + ", ".join(missing))
    return result


def make_book_pattern(book_data):
    """Build longest-first alternatives to avoid matching a short name too early."""
    alias_to_code = {}
    for code, (abbr, aliases) in book_data.items():
        for alias in aliases:
            alias_to_code[alias] = code

    aliases = sorted(alias_to_code, key=lambda value: len(value.replace(" ", "")), reverse=True)
    if not aliases:
        raise ValueError("No usable Tamil book names were found in the XML file.")

    # A book name must begin at the start of the heading or after whitespace.
    alternatives = "|".join(re.escape(x) for x in aliases)
    # Examples: 1:35-37, 24:1-4, 1:1, 1:2; 3:4-6
    reference = r"\d+\s*[:.]\s*\d+(?:\s*[-–—]\s*\d+(?:\s*[:.]\s*\d+)?)?(?:\s*[,;]\s*\d+(?:\s*[:.]\s*\d+)?(?:\s*[-–—]\s*\d+(?:\s*[:.]\s*\d+)?)?)*"
    book1 = r"(?P<book1>" + alternatives + r")"
    book2 = r"(?P<book2>" + alternatives + r")"
    book3 = r"(?P<book3>" + alternatives + r")"
    # Only consume matching bracket pairs. Unbracketed references remain valid.
    book_ref = r"(?:\(\s*" + book1 + r"\s*[.,]?\s*(?P<ref1>" + reference + r")\s*\)|\[\s*" + book2 + r"\s*[.,]?\s*(?P<ref2>" + reference + r")\s*\]|" + book3 + r"\s*[.,]?\s*(?P<ref3>" + reference + r"))"
    pattern = re.compile(
        r"(?<!\S)" + book_ref + r"(?!\w)",
        re.UNICODE
    )
    return pattern, alias_to_code


def transform_content(content, book_data):
    """
    Transform only \s heading lines.
    Returns (new_content, changes, skipped_existing_r).
    """
    pattern, alias_to_code = make_book_pattern(book_data)
    lines = content.splitlines(keepends=True)
    output = []
    changes = []
    skipped_existing_r = 0

    for index, line in enumerate(lines):
        # Support \s, \s1, \s2, etc., while preserving the exact marker.
        marker_match = re.match(r"^(\\s\d*)(\s+)(.*?)(\r?\n)?$", line)
        if not marker_match:
            output.append(line)
            continue

        marker = marker_match.group(1) + marker_match.group(2)
        heading = marker_match.group(3)
        newline = marker_match.group(4) or ""

        match = pattern.search(heading)
        if not match:
            output.append(line)
            continue

        # Avoid inserting a second parallel-reference marker.
        if index + 1 < len(lines) and re.match(r"^\\r(?:\s|$)", lines[index + 1]):
            skipped_existing_r += 1
            output.append(line)
            continue

        book_alias = next(match.group(name) for name in ("book1", "book2", "book3") if match.group(name))
        code = alias_to_code[book_alias]
        toc3_abbr = book_data[code][0]
        reference = next(match.group(name) for name in ("ref1", "ref2", "ref3") if match.group(name))
        reference = re.sub(r"\s*([:.,;])\s*", r"\1", reference)
        reference = re.sub(r"\s*([-–—])\s*", r"\1", reference)

        # Remove the book name and reference, then tidy punctuation left at the end.
        new_heading = heading[:match.start()] + heading[match.end():]
        new_heading = re.sub(r"\s{2,}", " ", new_heading).strip()
        new_heading = re.sub(r"[\s,;:]+$", "", new_heading)
        new_heading = re.sub(r"\s*\.+\s*$", "", new_heading)

        new_heading_line = marker + new_heading + newline
        ref_line = "\\r (" + toc3_abbr + " " + reference.rstrip(". ") + ")" + (newline or "\n")

        output.append(new_heading_line)
        output.append(ref_line)
        changes.append({
            "line": index + 1,
            "before": line.rstrip("\r\n"),
            "heading": new_heading_line.rstrip("\r\n"),
            "reference": ref_line.rstrip("\r\n"),
            "book": code,
        })

    return "".join(output), changes, skipped_existing_r


class ReferenceSeparatorApp:
    def __init__(self, root):
        self.root = root
        self.root.title("IRV Tamil – GNTD Parallel References Importer")
        self.root.geometry("1000x720")
        self.root.minsize(820, 580)

        self.folder = None
        self.xml_path = tk.StringVar(value=str(Path(__file__).with_name("IRVTamBooknames.xml")))
        self.gntd_path = tk.StringVar(value=str(Path(__file__).with_name("GNTD References.txt")))
        self.files_by_code = {}
        self.book_data = {}
        self.gntd_rows = []
        self.preview_results = {}
        self.status = tk.StringVar(value="Choose the folder containing your USFM files.")
        self.apply_var = tk.BooleanVar(value=False)
        self.backup_var = tk.BooleanVar(value=True)

        self._build_ui()

    def _build_ui(self):
        outer = ttk.Frame(self.root, padding=12)
        outer.pack(fill="both", expand=True)

        ttk.Label(
            outer, text="IRV Tamil – GNTD Parallel References Importer",
            font=("Segoe UI", 16, "bold")
        ).pack(anchor="w")
        ttk.Label(
            outer,
            text="Import GNTD references at their chapter and verse locations, preview, then apply.",
        ).pack(anchor="w", pady=(2, 12))

        xml_frame = ttk.LabelFrame(outer, text="1. Book names XML", padding=8)
        xml_frame.pack(fill="x", pady=(0, 10))
        ttk.Entry(xml_frame, textvariable=self.xml_path).pack(
            side="left", fill="x", expand=True, padx=(0, 8)
        )
        ttk.Button(xml_frame, text="Browse XML…", command=self.choose_xml).pack(side="right")

        gntd_frame = ttk.LabelFrame(outer, text="2. GNTD parallel references", padding=8)
        gntd_frame.pack(fill="x", pady=(0, 10))
        ttk.Entry(gntd_frame, textvariable=self.gntd_path).pack(
            side="left", fill="x", expand=True, padx=(0, 8)
        )
        ttk.Button(gntd_frame, text="Browse references…", command=self.choose_gntd).pack(side="right")

        folder_frame = ttk.LabelFrame(outer, text="3. USFM project folder", padding=8)
        folder_frame.pack(fill="x", pady=(0, 10))
        self.folder_label = ttk.Label(folder_frame, text="No folder selected")
        self.folder_label.pack(side="left", fill="x", expand=True)
        ttk.Button(folder_frame, text="Choose folder…", command=self.choose_folder).pack(side="right")

        books_frame = ttk.LabelFrame(outer, text="4. Select books (GEN–MAL)", padding=8)
        books_frame.pack(fill="both", expand=True, pady=(0, 10))

        left = ttk.Frame(books_frame)
        left.pack(side="left", fill="both", expand=True)
        self.book_list = tk.Listbox(
            left, selectmode=tk.EXTENDED, height=12,
            font=("Consolas", 10), exportselection=False
        )
        self.book_list.pack(side="left", fill="both", expand=True)
        scrollbar = ttk.Scrollbar(left, orient="vertical", command=self.book_list.yview)
        scrollbar.pack(side="right", fill="y")
        self.book_list.configure(yscrollcommand=scrollbar.set)

        buttons = ttk.Frame(books_frame)
        buttons.pack(side="left", fill="y", padx=(10, 0))
        ttk.Button(buttons, text="Select all GEN–MAL", command=self.select_all).pack(fill="x", pady=3)
        ttk.Button(buttons, text="Clear selection", command=self.clear_selection).pack(fill="x", pady=3)
        ttk.Button(buttons, text="Add individual SFM files…", command=self.add_files).pack(fill="x", pady=3)
        ttk.Label(
            buttons,
            text="The folder scan reads each file's\n\\id marker to identify its book.",
            justify="left", wraplength=220
        ).pack(anchor="w", pady=(12, 0))

        options = ttk.LabelFrame(outer, text="5. Processing options", padding=8)
        options.pack(fill="x", pady=(0, 10))
        ttk.Checkbutton(
            options, text="Create a timestamped backup before modifying files",
            variable=self.backup_var
        ).pack(anchor="w")
        ttk.Checkbutton(
            options, text="Apply changes to the selected SFM files (leave unchecked for preview only)",
            variable=self.apply_var
        ).pack(anchor="w")

        actions = ttk.Frame(outer)
        actions.pack(fill="x", pady=(0, 8))
        ttk.Button(actions, text="Preview GNTD import", command=self.preview).pack(side="left")
        ttk.Button(actions, text="Import / Apply", command=self.run_apply).pack(side="left", padx=8)
        ttk.Button(actions, text="Export preview…", command=self.export_preview).pack(side="left")

        result_frame = ttk.LabelFrame(outer, text="Preview and processing log", padding=6)
        result_frame.pack(fill="both", expand=True)
        self.log = tk.Text(result_frame, height=12, wrap="word", font=("Consolas", 9))
        self.log.pack(side="left", fill="both", expand=True)
        log_scroll = ttk.Scrollbar(result_frame, orient="vertical", command=self.log.yview)
        log_scroll.pack(side="right", fill="y")
        self.log.configure(yscrollcommand=log_scroll.set)

        ttk.Label(outer, textvariable=self.status, relief="sunken", anchor="w").pack(fill="x", pady=(8, 0))

    def write_log(self, text):
        self.log.insert("end", text + "\n")
        self.log.see("end")

    def choose_xml(self):
        path = filedialog.askopenfilename(
            title="Select IRVTamBooknames.xml",
            filetypes=[("XML files", "*.xml"), ("All files", "*.*")]
        )
        if path:
            try:
                self.book_data = load_booknames(path)
                self.xml_path.set(path)
                self.write_log(f"Loaded {len(self.book_data)} book-name entries from XML.")
            except Exception as exc:
                messagebox.showerror("XML error", f"Could not read the XML file:\n{exc}")

    def choose_gntd(self):
        path = filedialog.askopenfilename(
            title="Select GNTD References.txt",
            filetypes=[("Text files", "*.txt"), ("All files", "*.*")]
        )
        if path:
            self.gntd_path.set(path)
            try:
                rows, errors = parse_gntd_references(Path(path).read_text(encoding="utf-8-sig"))
                if errors:
                    raise ValueError("\n".join(errors[:10]))
                self.gntd_rows = rows
                self.write_log("Loaded {} GNTD reference locations.".format(len(rows)))
            except Exception as exc:
                messagebox.showerror("GNTD file error", "Could not read the GNTD reference file:\n{}".format(exc))

    def choose_folder(self):
        path = filedialog.askdirectory(title="Choose the Paratext project folder")
        if not path:
            return
        self.folder = Path(path)
        self.folder_label.config(text=str(self.folder))
        self.scan_folder()

    def scan_folder(self):
        self.files_by_code = {}
        if not self.folder:
            return
        for path in sorted(self.folder.iterdir()):
            if not path.is_file() or path.suffix.lower() not in (".sfm", ".usfm", ".txt"):
                continue
            try:
                with path.open("r", encoding="utf-8-sig", errors="replace") as f:
                    first_lines = [f.readline() for _ in range(5)]
                for line in first_lines:
                    m = re.match(r"^\\id\s+([A-Za-z0-9]{3})", line)
                    if m:
                        code = m.group(1).upper()
                        if code in BOOK_ORDER:
                            self.files_by_code[code] = path
                        break
            except OSError:
                continue
        self.refresh_book_list()
        self.write_log(f"Scanned folder: found {len(self.files_by_code)} of 39 books (GEN–MAL).")
        self.status.set(f"Found {len(self.files_by_code)} books. Select the books to process.")

    def refresh_book_list(self):
        self.book_list.delete(0, "end")
        for code in BOOK_ORDER:
            if code in self.files_by_code:
                label = f"{code}   —   {self.files_by_code[code].name}"
            else:
                label = f"{code}   —   [not found]"
            self.book_list.insert("end", label)
            if code not in self.files_by_code:
                self.book_list.itemconfig("end", foreground="gray")

    def select_all(self):
        self.book_list.selection_clear(0, "end")
        for i, code in enumerate(BOOK_ORDER):
            if code in self.files_by_code:
                self.book_list.selection_set(i)

    def clear_selection(self):
        self.book_list.selection_clear(0, "end")

    def add_files(self):
        paths = filedialog.askopenfilenames(
            title="Add one or more SFM files",
            filetypes=[("USFM/SFM files", "*.sfm *.SFM *.usfm *.txt"), ("All files", "*.*")]
        )
        for raw in paths:
            path = Path(raw)
            try:
                with path.open("r", encoding="utf-8-sig", errors="replace") as f:
                    code = None
                    for _ in range(5):
                        m = re.match(r"^\\id\s+([A-Za-z0-9]{3})", f.readline())
                        if m:
                            code = m.group(1).upper()
                            break
                if code in BOOK_ORDER:
                    self.files_by_code[code] = path
                else:
                    self.write_log(f"Skipped (no GEN–MAL \\id found): {path.name}")
            except OSError as exc:
                self.write_log(f"Could not read {path.name}: {exc}")
        self.refresh_book_list()

    def selected_paths(self):
        selected = []
        for index in self.book_list.curselection():
            code = BOOK_ORDER[index]
            if code in self.files_by_code:
                selected.append((code, self.files_by_code[code]))
        return selected

    def ensure_xml(self):
        if not self.xml_path.get():
            messagebox.showwarning("Book names XML required", "Please select IRVTamBooknames.xml first.")
            return False
        try:
            self.book_data = load_booknames(self.xml_path.get())
            return True
        except Exception as exc:
            messagebox.showerror("XML error", str(exc))
            return False

    def ensure_gntd(self):
        if not self.gntd_path.get():
            messagebox.showwarning("GNTD file required", "Please select GNTD References.txt first.")
            return False
        try:
            self.gntd_rows, errors = parse_gntd_references(Path(self.gntd_path.get()).read_text(encoding="utf-8-sig"))
            if errors:
                raise ValueError("\n".join(errors[:10]))
            if not self.gntd_rows:
                raise ValueError("No GNTD reference rows were found.")
            return True
        except Exception as exc:
            messagebox.showerror("GNTD file error", str(exc))
            return False

    def preview(self):
        if not self.ensure_xml():
            return
        if not self.ensure_gntd():
            return
        selected = self.selected_paths()
        if not selected:
            messagebox.showwarning("No books selected", "Choose one or more books from the list.")
            return
        self.preview_results = {}
        self.log.delete("1.0", "end")
        total = exact = errors_total = 0
        self.write_log("PREVIEW ONLY — no source files have been changed.")
        for code, path in selected:
            try:
                original, _has_bom = read_usfm(path)
                book_rows = [row for row in self.gntd_rows if row["book"] == code]
                transformed, changes, skipped, errors = import_gntd_references(
                    original, code, book_rows, self.book_data
                )
                self.preview_results[path] = (original, transformed, changes, skipped, errors)
                total += len(changes)
                exact += skipped
                errors_total += len(errors)
                self.write_log("\n{} — {}: {} proposed change(s); {} exact references already present.".format(code, path.name, len(changes), skipped))
                for change in changes:
                    self.write_log("  GNTD line {} at {}; target {}".format(
                        change["gntd_line"], change.get("gntd_location", change["location"]), change["location"]
                    ))
                    if change.get("heading"):
                        self.write_log("    {}".format(change["heading"]))
                    for old_reference in change.get("existing_references", []):
                        self.write_log("    Existing (kept): {}".format(old_reference))
                    self.write_log("    {}".format(change["reference"]))
                for error in errors:
                    self.write_log("  ERROR: " + error)
            except Exception as exc:
                self.write_log(f"\nERROR reading {path.name}: {exc}")
        self.status.set("Preview complete: {} proposed changes, {} exact matches, {} errors.".format(total, exact, errors_total))

    def run_apply(self):
        if not self.apply_var.get():
            messagebox.showinfo(
                "Preview mode is enabled",
                "The 'Apply changes' checkbox is not selected.\n\nPreview the GNTD import first. When satisfied, tick Apply changes and click Import / Apply."
            )
            self.preview()
            return
        if not self.ensure_xml():
            return
        if not self.ensure_gntd():
            return
        selected = self.selected_paths()
        if not selected:
            messagebox.showwarning("No books selected", "Choose one or more books from the list.")
            return
        if not messagebox.askyesno(
            "Confirm file modification",
            "Import GNTD parallel references into {} selected SFM file(s)?\n\n"
            "A timestamped backup will be made for every changed file when backups are enabled.".format(len(selected))
        ):
            return

        self.log.delete("1.0", "end")
        total = exact = errors_total = 0
        stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        for code, path in selected:
            try:
                original, has_bom = read_usfm(path)
                book_rows = [row for row in self.gntd_rows if row["book"] == code]
                transformed, changes, skipped, errors = import_gntd_references(
                    original, code, book_rows, self.book_data
                )
                exact += skipped
                errors_total += len(errors)
                if not changes:
                    self.write_log("{}: no changes needed; {} exact reference(s) already present.".format(code, skipped))
                    for error in errors:
                        self.write_log("  ERROR: " + error)
                    continue
                if self.backup_var.get():
                    backup = unique_backup_path(path, stamp)
                    shutil.copy2(path, backup)
                    self.write_log(f"{code}: backup created: {backup.name}")
                write_usfm(path, transformed, has_bom)
                total += len(changes)
                self.write_log("{}: applied {} change(s); {} exact reference(s) already present.".format(code, len(changes), skipped))
                for error in errors:
                    self.write_log("  ERROR: " + error)
            except Exception as exc:
                self.write_log(f"{code}: ERROR — {exc}")
        self.status.set("Finished: {} changes applied, {} exact references skipped, {} errors.".format(total, exact, errors_total))
        messagebox.showinfo("Finished", "Processing completed.\n{} changes applied.\n{} exact references skipped.\n{} errors.".format(total, exact, errors_total))

    def export_preview(self):
        if not self.preview_results:
            messagebox.showinfo("Nothing to export", "Run Preview selected books first.")
            return
        path = filedialog.asksaveasfilename(
            title="Save GNTD import preview",
            defaultextension=".txt",
            filetypes=[("Text report", "*.txt")]
        )
        if not path:
            return
        with open(path, "w", encoding="utf-8") as f:
            f.write("IRV Tamil – GNTD Reference Import preview\n")
            f.write("No source files were modified by this report.\n\n")
            for file_path, (original, transformed, changes, skipped, errors) in self.preview_results.items():
                f.write(f"{file_path.name}: {len(changes)} change(s)\n")
                for change in changes:
                    f.write("GNTD line {} at {}; target {}\n".format(
                        change["gntd_line"], change.get("gntd_location", change["location"]), change["location"]
                    ))
                    if change.get("heading"):
                        f.write(change["heading"] + "\n")
                    for old_reference in change.get("existing_references", []):
                        f.write("Existing (kept): {}\n".format(old_reference))
                    f.write(change["reference"] + "\n\n")
                f.write("Exact references skipped: {}\n".format(skipped))
                for error in errors:
                    f.write("ERROR: {}\n".format(error))
                f.write("\n")
        messagebox.showinfo("Report saved", f"Preview report saved to:\n{path}")


def main():
    root = tk.Tk()
    try:
        style = ttk.Style()
        if "vista" in style.theme_names():
            style.theme_use("vista")
    except Exception:
        pass
    ReferenceSeparatorApp(root)
    root.mainloop()


if __name__ == "__main__":
    main()
