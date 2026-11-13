#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Paratext Parallel Passage References Transfer Tool
==================================================
A modern, user-friendly Paratext custom tool to transfer parallel passage
references (\r fields) from:
  1. Resources (e.g. Good News Translation - GNTD, with 272 pre-bundled references)
  2. Paratext Projects (e.g. ABC_BQ1, MTEP1, etc.)
into target Paratext translation projects (e.g. IRVTam).

Version: 1.0
Author: Language Technology Team
License: MIT
"""

import sys
import os
import re
import shutil
import threading
from pathlib import Path
from datetime import datetime
import xml.etree.ElementTree as ET
import tempfile
import argparse

try:
    import tkinter as tk
    from tkinter import ttk, filedialog, messagebox, scrolledtext
    TKINTER_AVAILABLE = True
except ImportError:
    TKINTER_AVAILABLE = False

# ==============================================================================
# CANONICAL BIBLE BOOKS DEFINITIONS (66 Protestant Canon)
# ==============================================================================

CANONICAL_BOOKS = [
    # Old Testament (39)
    ("GEN", "Genesis", "OT"),
    ("EXO", "Exodus", "OT"),
    ("LEV", "Leviticus", "OT"),
    ("NUM", "Numbers", "OT"),
    ("DEU", "Deuteronomy", "OT"),
    ("JOS", "Joshua", "OT"),
    ("JDG", "Judges", "OT"),
    ("RUT", "Ruth", "OT"),
    ("1SA", "1 Samuel", "OT"),
    ("2SA", "2 Samuel", "OT"),
    ("1KI", "1 Kings", "OT"),
    ("2KI", "2 Kings", "OT"),
    ("1CH", "1 Chronicles", "OT"),
    ("2CH", "2 Chronicles", "OT"),
    ("EZR", "Ezra", "OT"),
    ("NEH", "Nehemiah", "OT"),
    ("EST", "Esther", "OT"),
    ("JOB", "Job", "OT"),
    ("PSA", "Psalms", "OT"),
    ("PRO", "Proverbs", "OT"),
    ("ECC", "Ecclesiastes", "OT"),
    ("SNG", "Song of Solomon", "OT"),
    ("ISA", "Isaiah", "OT"),
    ("JER", "Jeremiah", "OT"),
    ("LAM", "Lamentations", "OT"),
    ("EZK", "Ezekiel", "OT"),
    ("DAN", "Daniel", "OT"),
    ("HOS", "Hosea", "OT"),
    ("JOL", "Joel", "OT"),
    ("AMO", "Amos", "OT"),
    ("OBA", "Obadiah", "OT"),
    ("JON", "Jonah", "OT"),
    ("MIC", "Micah", "OT"),
    ("NAM", "Nahum", "OT"),
    ("HAB", "Habakkuk", "OT"),
    ("ZEP", "Zephaniah", "OT"),
    ("HAG", "Haggai", "OT"),
    ("ZEC", "Zechariah", "OT"),
    ("MAL", "Malachi", "OT"),
    # New Testament (27)
    ("MAT", "Matthew", "NT"),
    ("MRK", "Mark", "NT"),
    ("LUK", "Luke", "NT"),
    ("JHN", "John", "NT"),
    ("ACT", "Acts", "NT"),
    ("ROM", "Romans", "NT"),
    ("1CO", "1 Corinthians", "NT"),
    ("2CO", "2 Corinthians", "NT"),
    ("GAL", "Galatians", "NT"),
    ("EPH", "Ephesians", "NT"),
    ("PHP", "Philippians", "NT"),
    ("COL", "Colossians", "NT"),
    ("1TH", "1 Thessalonians", "NT"),
    ("2TH", "2 Thessalonians", "NT"),
    ("1TI", "1 Timothy", "NT"),
    ("2TI", "2 Timothy", "NT"),
    ("TIT", "Titus", "NT"),
    ("PHM", "Philemon", "NT"),
    ("HEB", "Hebrews", "NT"),
    ("JAS", "James", "NT"),
    ("1PE", "1 Peter", "NT"),
    ("2PE", "2 Peter", "NT"),
    ("1JN", "1 John", "NT"),
    ("2JN", "2 John", "NT"),
    ("3JN", "3 John", "NT"),
    ("JUD", "Jude", "NT"),
    ("REV", "Revelation", "NT"),
]

OT_BOOK_CODES = [code for code, name, testament in CANONICAL_BOOKS if testament == "OT"]
NT_BOOK_CODES = [code for code, name, testament in CANONICAL_BOOKS if testament == "NT"]
ALL_BOOK_CODES = [code for code, name, testament in CANONICAL_BOOKS]
BOOK_NAME_TO_CODE = {name.lower(): code for code, name, testament in CANONICAL_BOOKS}
BOOK_CODE_TO_ENGLISH = {code: name for code, name, testament in CANONICAL_BOOKS}

# English name variants & common aliases
ENGLISH_BOOK_ALIASES = {
    # OT
    "genesis": "GEN", "gen": "GEN",
    "exodus": "EXO", "exo": "EXO", "ex": "EXO",
    "leviticus": "LEV", "lev": "LEV",
    "numbers": "NUM", "num": "NUM",
    "deuteronomy": "DEU", "deu": "DEU", "deut": "DEU",
    "joshua": "JOS", "jos": "JOS", "josh": "JOS",
    "judges": "JDG", "jdg": "JDG", "judg": "JDG",
    "ruth": "RUT", "rut": "RUT", "rth": "RUT",
    "1 samuel": "1SA", "1samuel": "1SA", "1sa": "1SA", "1sam": "1SA", "1 sam": "1SA",
    "2 samuel": "2SA", "2samuel": "2SA", "2sa": "2SA", "2sam": "2SA", "2 sam": "2SA",
    "1 kings": "1KI", "1kings": "1KI", "1ki": "1KI", "1kgs": "1KI", "1 kgs": "1KI",
    "2 kings": "2KI", "2kings": "2KI", "2ki": "2KI", "2kgs": "2KI", "2 kgs": "2KI",
    "1 chronicles": "1CH", "1chronicles": "1CH", "1ch": "1CH", "1chr": "1CH", "1 chr": "1CH", "1 chron": "1CH",
    "2 chronicles": "2CH", "2chronicles": "2CH", "2ch": "2CH", "2chr": "2CH", "2 chr": "2CH", "2 chron": "2CH",
    "ezra": "EZR", "ezr": "EZR",
    "nehemiah": "NEH", "neh": "NEH",
    "esther": "EST", "est": "EST",
    "job": "JOB",
    "psalms": "PSA", "psalm": "PSA", "psa": "PSA", "ps": "PSA", "pss": "PSA",
    "proverbs": "PRO", "pro": "PRO", "prov": "PRO",
    "ecclesiastes": "ECC", "ecc": "ECC", "eccl": "ECC",
    "song of solomon": "SNG", "song of songs": "SNG", "songs": "SNG", "sng": "SNG", "canticles": "SNG",
    "isaiah": "ISA", "isa": "ISA",
    "jeremiah": "JER", "jer": "JER",
    "lamentations": "LAM", "lam": "LAM",
    "ezekiel": "EZK", "ezk": "EZK", "ezek": "EZK",
    "daniel": "DAN", "dan": "DAN",
    "hosea": "HOS", "hos": "HOS",
    "joel": "JOL", "jol": "JOL",
    "amos": "AMO", "amo": "AMO",
    "obadiah": "OBA", "oba": "OBA", "obad": "OBA",
    "jonah": "JON", "jon": "JON",
    "micah": "MIC", "mic": "MIC",
    "nahum": "NAM", "nam": "NAM", "nah": "NAM",
    "habakkuk": "HAB", "hab": "HAB",
    "zephaniah": "ZEP", "zep": "ZEP", "zeph": "ZEP",
    "haggai": "HAG", "hag": "HAG",
    "zechariah": "ZEC", "zec": "ZEC", "zech": "ZEC",
    "malachi": "MAL", "mal": "MAL",
    # NT
    "matthew": "MAT", "mat": "MAT", "matt": "MAT", "mt": "MAT",
    "mark": "MRK", "mrk": "MRK", "mk": "MRK",
    "luke": "LUK", "luk": "LUK", "lk": "LUK",
    "john": "JHN", "jhn": "JHN", "jn": "JHN",
    "acts": "ACT", "act": "ACT", "ac": "ACT",
    "romans": "ROM", "rom": "ROM", "ro": "ROM",
    "1 corinthians": "1CO", "1corinthians": "1CO", "1co": "1CO", "1cor": "1CO", "1 cor": "1CO",
    "2 corinthians": "2CO", "2corinthians": "2CO", "2co": "2CO", "2cor": "2CO", "2 cor": "2CO",
    "galatians": "GAL", "gal": "GAL",
    "ephesians": "EPH", "eph": "EPH",
    "philippians": "PHP", "php": "PHP", "phil": "PHP",
    "colossians": "COL", "col": "COL",
    "1 thessalonians": "1TH", "1thessalonians": "1TH", "1th": "1TH", "1thess": "1TH", "1 thess": "1TH",
    "2 thessalonians": "2TH", "2thessalonians": "2TH", "2th": "2TH", "2thess": "2TH", "2 thess": "2TH",
    "1 timothy": "1TI", "1timothy": "1TI", "1ti": "1TI", "1tim": "1TI", "1 tim": "1TI",
    "2 timothy": "2TI", "2timothy": "2TI", "2ti": "2TI", "2tim": "2TI", "2 tim": "2TI",
    "titus": "TIT", "tit": "TIT",
    "philemon": "PHM", "phm": "PHM", "phlm": "PHM",
    "hebrews": "HEB", "heb": "HEB",
    "james": "JAS", "jas": "JAS", "jm": "JAS",
    "1 peter": "1PE", "1peter": "1PE", "1pe": "1PE", "1pet": "1PE", "1 pet": "1PE",
    "2 peter": "2PE", "2peter": "2PE", "2pe": "2PE", "2pet": "2PE", "2 pet": "2PE",
    "1 john": "1JN", "1john": "1JN", "1jn": "1JN", "1 jn": "1JN",
    "2 john": "2JN", "2john": "2JN", "2jn": "2JN", "2 jn": "2JN",
    "3 john": "3JN", "3john": "3JN", "3jn": "3JN", "3 jn": "3JN",
    "jude": "JUD", "jud": "JUD",
    "revelation": "REV", "rev": "REV", "apocalypse": "REV",
}

# Add self mappings for 3-letter codes
for code in ALL_BOOK_CODES:
    ENGLISH_BOOK_ALIASES[code.lower()] = code

# ==============================================================================
# PRE-BUNDLED GNTD PARALLEL PASSAGE REFERENCES (272 Old Testament Entries)
# ==============================================================================
BUILTIN_GNTD_REFERENCES_TEXT = r"""GEN 5:0:	 \r (1 Chronicles 1.1-4)
GEN 10:0:	 \r (1 Chronicles 1.5-23)
GEN 11:9:	 \r (1 Chronicles 1.24-27)
GEN 25:0:	 \r (1 Chronicles 1.32,33)
GEN 25:11:	 \r (1 Chronicles 1.28-31)
GEN 35:21:	 \r (1 Chronicles 2.1,2)
GEN 36:0:	 \r (1 Chronicles 1.34-37)
GEN 36:19:	 \r (1 Chronicles 1.38-42)
GEN 36:29-30:	 \r (1 Chronicles 1.43-54)
EXO 17:0:	 \r (Numbers 20.1-13)
EXO 18:12:	 \r (Deuteronomy 1.9-18)
EXO 20:0:	 \r (Deuteronomy 5.1-21)
EXO 20:17:	 \r (Deuteronomy 5.22-33)
EXO 21:0:	 \r (Deuteronomy 15.12-18)
EXO 23:13:	 \r (Exodus 34.18-26; Deuteronomy 16.1-17)
EXO 25:0:	 \r (Exodus 35.4-9)
EXO 25:9:	 \r (Exodus 37.1-9)
EXO 25:22:	 \r (Exodus 37.10-16)
EXO 25:30:	 \r (Exodus 37.17-24)
EXO 26:0:	 \r (Exodus 36.8-38)
EXO 27:0:	 \r (Exodus 38.1-7)
EXO 27:8:	 \r (Exodus 38.9-20)
EXO 27:19:	 \r (Leviticus 24.1-4)
EXO 28:0:	 \r (Exodus 39.1-7)
EXO 28:14:	 \r (Exodus 39.8-21)
EXO 28:30:	 \r (Exodus 39.22-31)
EXO 29:0:	 \r (Leviticus 8.1-36)
EXO 29:37:	 \r (Numbers 28.1-8)
EXO 30:0:	 \r (Exodus 37.25-28)
EXO 31:0:	 \r (Exodus 35.30—36.1)
EXO 32:0:	 \r (Deuteronomy 9.6-29)
EXO 34:0:	 \r (Deuteronomy 10.1-5)
EXO 34:9:	 \r (Exodus 23.14-19; Deuteronomy 7.1-5; 16.1-17)
EXO 35:3:	 \r (Exodus 25.1-9)
EXO 35:9:	 \r (Exodus 39.32-43)
EXO 35:29:	 \r (Exodus 31.1-11)
EXO 36:7:	 \r (Exodus 26.1-37)
EXO 37:0:	 \r (Exodus 25.10-22)
EXO 37:9:	 \r (Exodus 25.23-30)
EXO 37:16:	 \r (Exodus 25.31-40)
EXO 37:24:	 \r (Exodus 30.1-5)
EXO 37:28:	 \r (Exodus 30.22-38)
EXO 38:0:	 \r (Exodus 27.1-8)
EXO 38:7:	 \r (Exodus 30.18)
EXO 38:8:	 \r (Exodus 27.9-19)
EXO 39:0:	 \r (Exodus 28.1-14)
EXO 39:7:	 \r (Exodus 28.15-30)
EXO 39:21:	 \r (Exodus 28.31-43)
EXO 39:31:	 \r (Exodus 35.10-19)
EXO 40:33:	 \r (Numbers 9.15-23)
LEV 8:0:	 \r (Exodus 29.1-37)
LEV 11:0:	 \r (Deuteronomy 14.3-21)
LEV 23:4:	 \r (Numbers 28.16-25)
LEV 23:14:	 \r (Numbers 28.26-31)
LEV 23:22:	 \r (Numbers 29.1-6)
LEV 23:25:	 \r (Numbers 29.7-11)
LEV 23:32:	 \r (Numbers 29.12-40)
LEV 24:0:	 \r (Exodus 27.20,21)
LEV 25:0:	 \r (Deuteronomy 15.1-11)
LEV 26:0:	 \r (Deuteronomy 7.12-24; 28.1-14)
LEV 26:13:	 \r (Deuteronomy 28.15-68)
NUM 9:14:	 \r (Exodus 40.34-38)
NUM 13:0:	 \r (Deuteronomy 1.19-33)
NUM 14:38:	 \r (Deuteronomy 1.41-46)
NUM 20:0:	 \r (Exodus 17.1-7)
NUM 21:20:	 \r (Deuteronomy 2.26—3.11)
NUM 27:11:	 \r (Deuteronomy 31.1-8)
NUM 28:0:	 \r (Exodus 29.38-46)
NUM 28:15:	 \r (Leviticus 23.5-14)
NUM 28:25:	 \r (Leviticus 23.15-22)
NUM 29:0:	 \r (Leviticus 23.23-25)
NUM 29:6:	 \r (Leviticus 23.26-32)
NUM 29:11:	 \r (Leviticus 23.33-44)
NUM 32:0:	 \r (Deuteronomy 3.12-22)
NUM 35:8:	 \r (Deuteronomy 19.1-13; Joshua 20.1-9)
DEU 1:8:	 \r (Exodus 18.13-27)
DEU 1:18:	 \r (Numbers 13.1-33)
DEU 1:33:	 \r (Numbers 14.20-45)
DEU 2:25:	 \r (Numbers 21.21-30)
DEU 3:0:	 \r (Numbers 21.31-35)
DEU 3:11:	 \r (Numbers 32.1-42)
DEU 5:0:	 \r (Exodus 20.1-17)
DEU 5:22:	 \r (Exodus 20.18-21)
DEU 7:0:	 \r (Exodus 34.11-16)
DEU 7:11:	 \r (Deuteronomy 28.1-14)
DEU 10:0:	 \r (Exodus 34.1-10)
DEU 14:2:	 \r (Leviticus 11.1-47)
DEU 15:0:	 \r (Leviticus 25.1-7)
DEU 15:11:	 \r (Exodus 21.1-11)
DEU 16:0:	 \r (Exodus 12.1-20)
DEU 16:8:	 \r (Exodus 34.22; Leviticus 23.15-21)
DEU 16:12:	 \r (Leviticus 23.33-43)
DEU 19:0:	 \r (Numbers 35.9-34; Joshua 20.1-9)
DEU 28:0:	 \r (Leviticus 26.3-13; Deuteronomy 7.12-24)
DEU 28:14:	 \r (Leviticus 26.14-46)
JOS 15:12:	 \r (Judges 1.11-15)
JDG 1:10:	 \r (Joshua 15.13-19)
1SA 31:0:	 \r (1 Chronicles 10.1-12)
2SA 5:0:	 \r (1 Chronicles 11.1-9; 14.1-7)
2SA 5:16:	 \r (1 Chronicles 14.8-17)
2SA 6:0:	 \r (1 Chronicles 13.1-14; 15.25—16.6,43)
2SA 7:0:	 \r (1 Chronicles 17.1-15)
2SA 7:17:	 \r (1 Chronicles 17.16-27)
2SA 8:0:	 \r (1 Chronicles 18.1-17)
2SA 10:0:	 \r (1 Chronicles 19.1-19)
2SA 12:25:	 \r (1 Chronicles 20.1-3)
2SA 21:14:	 \r (1 Chronicles 20.4-8)
2SA 22:0:	 \r (Psalm 18)
2SA 23:7:	 \r (1 Chronicles 11.10-41)
2SA 24:0:	 \r (1 Chronicles 21.1-27)
1KI 3:0:	 \r (2 Chronicles 1.3-12)
1KI 5:0:	 \r (2 Chronicles 2.1-18)
1KI 6:14:	 \r (2 Chronicles 3.8-14)
1KI 7:12:	 \r (2 Chronicles 2.13,14)
1KI 7:14:	 \r (2 Chronicles 3.15-17)
1KI 7:22:	 \r (2 Chronicles 4.2-5)
1KI 7:39:	 \r (2 Chronicles 4.11—5.1)
1KI 8:0:	 \r (2 Chronicles 5.2—6.2)
1KI 8:13:	 \r (2 Chronicles 6.3-11)
1KI 8:21:	 \r (2 Chronicles 6.12-42)
1KI 8:61:	 \r (2 Chronicles 7.4-10)
1KI 9:0:	 \r (2 Chronicles 7.11-22)
1KI 9:9:	 \r (2 Chronicles 8.1,2)
1KI 9:14:	 \r (2 Chronicles 8.3-18)
1KI 10:0:	 \r (2 Chronicles 9.1-12)
1KI 10:13:	 \r (2 Chronicles 9.13-29)
1KI 11:40:	 \r (2 Chronicles 9.29-31)
1KI 12:0:	 \r (2 Chronicles 10.1-19)
1KI 12:20:	 \r (2 Chronicles 11.1-4)
1KI 14:20:	 \r (2 Chronicles 11.5—12.15)
1KI 15:0:	 \r (2 Chronicles 13.1—14.1)
1KI 15:8:	 \r (2 Chronicles 14.1-5; 15.16—16.6,11-14)
1KI 22:0:	 \r (2 Chronicles 18.2-27)
1KI 22:28:	 \r (2 Chronicles 18.28-34)
1KI 22:40:	 \r (2 Chronicles 20.31—21.1)
2KI 8:15:	 \r (2 Chronicles 21.1-20)
2KI 8:24:	 \r (2 Chronicles 22.1-6)
2KI 11:0:	 \r (2 Chronicles 22.10—23.15)
2KI 11:16:	 \r (2 Chronicles 23.16-21)
2KI 12:0:	 \r (2 Chronicles 24.1-16)
2KI 14:0:	 \r (2 Chronicles 25.1-24)
2KI 14:16:	 \r (2 Chronicles 25.25-28)
2KI 15:0:	 \r (2 Chronicles 26.1-23)
2KI 15:31:	 \r (2 Chronicles 27.1-9)
2KI 16:0:	 \r (2 Chronicles 28.1-27)
2KI 18:0:	 \r (2 Chronicles 29.1,2; 31.1)
2KI 18:12:	 \r (2 Chronicles 32.1-19; Isaiah 36.1-22)
2KI 19:0:	 \r (Isaiah 37.1-7)
2KI 19:7:	 \r (Isaiah 37.8-20)
2KI 19:19:	 \r (Isaiah 37.21-38)
2KI 20:0:	 \r (Isaiah 38.1-8,21,22; 2 Chronicles 32.24-26)
2KI 20:11:	 \r (Isaiah 39.1-8)
2KI 20:19:	 \r (2 Chronicles 32.32,33)
2KI 21:0:	 \r (2 Chronicles 33.1-20)
2KI 21:18:	 \r (2 Chronicles 33.21-25)
2KI 22:0:	 \r (2 Chronicles 34.1,2)
2KI 22:2:	 \r (2 Chronicles 34.8-28)
2KI 23:0:	 \r (2 Chronicles 34.3-7,29-33)
2KI 23:20:	 \r (2 Chronicles 35.1-19)
2KI 23:27:	 \r (2 Chronicles 35.20—36.1)
2KI 23:30:	 \r (2 Chronicles 36.2-4)
2KI 23:34:	 \r (2 Chronicles 36.5-8)
2KI 24:7:	 \r (2 Chronicles 36.9,10)
2KI 24:17:	 \r (2 Chronicles 36.11,12; Jeremiah 52.1-3a)
2KI 25:0:	 \r (2 Chronicles 36.13-21; Jeremiah 52.3b-11)
2KI 25:7:	 \r (Jeremiah 52.12-33)
2KI 25:17:	 \r (Jeremiah 52.24-27)
2KI 25:21:	 \r (Jeremiah 40.7-9; 41.1-3)
2KI 25:26:	 \r (Jeremiah 52.31-34)
1CH 1:0:	 \r (Genesis 5.1-32; 10.1-32; 11.10-26)
1CH 1:27:	 \r (Genesis 25.12-16)
1CH 1:33:	 \r (Genesis 36.1-19)
1CH 1:37:	 \r (Genesis 36.20-30)
1CH 1:38-42:	 \r (Genesis 36.31-43)
1CH 9:34:	 \r (1 Chronicles 8.29-38)
1CH 10:0:	 \r (1 Samuel 31.1-13)
1CH 11:0:	 \r (2 Samuel 5.1-10)
1CH 11:9:	 \r (2 Samuel 23.8-39)
1CH 13:0:	 \r (2 Samuel 6.1-11)
1CH 14:0:	 \r (2 Samuel 5.11-16)
1CH 14:7:	 \r (2 Samuel 5.17-25)
1CH 15:23-24:	 \r (2 Samuel 6.12-22)
1CH 16:7:	 \r (Psalms 105.1-15; 96.1-13; 106.1, 47, 48)
1CH 17:0:	 \r (2 Samuel 7.1-17)
1CH 17:15:	 \r (2 Samuel 7.18-29)
1CH 18:0:	 \r (2 Samuel 8.1-18)
1CH 19:0:	 \r (2 Samuel 10.1-19)
1CH 20:0:	 \r (2 Samuel 12.26-31)
1CH 20:3:	 \r (2 Samuel 21.15-22)
1CH 21:0:	 \r (2 Samuel 24.1-25)
2CH 1:0:	 \r (1 Kings 3.1-15)
2CH 1:12:	 \r (1 Kings 10.26-29)
2CH 2:0:	 \r (1 Kings 5.1-18)
2CH 2:16:	 \r (1 Kings 6.1-38)
2CH 3:14:	 \r (1 Kings 7.15-22)
2CH 4:0:	 \r (1 Kings 7.23-51)
2CH 5:1:	 \r (1 Kings 8.1-9)
2CH 6:0:	 \r (1 Kings 8.12-21)
2CH 6:11:	 \r (1 Kings 8.22-53)
2CH 7:0:	 \r (1 Kings 8.62-66)
2CH 7:10:	 \r (1 Kings 9.1-9)
2CH 8:0:	 \r (1 Kings 9.10-28)
2CH 9:0:	 \r (1 Kings 10.1-13)
2CH 9:12:	 \r (1 Kings 10.14-25)
2CH 9:28:	 \r (1 Kings 11.41-43)
2CH 10:0:	 \r (1 Kings 12.1-20)
2CH 11:0:	 \r (1 Kings 12.21-24)
2CH 12:0:	 \r (1 Kings 14.25-28)
2CH 13:0:	 \r (1 Kings 15.1-8)
2CH 16:0:	 \r (1 Kings 15.17-22)
2CH 16:10:	 \r (1 Kings 15.23,24)
2CH 18:0:	 \r (1 Kings 22.1-28)
2CH 18:27:	 \r (1 Kings 22.29-35)
2CH 20:30:	 \r (1 Kings 22.41-50)
2CH 21:1:	 \r (2 Kings 8.17-24)
2CH 22:0:	 \r (2 Kings 8.25-29; 9.21-28)
2CH 22:9:	 \r (2 Kings 11.1-3)
2CH 23:0:	 \r (2 Kings 11.4-16)
2CH 23:15:	 \r (2 Kings 11.17-20)
2CH 24:0:	 \r (2 Kings 12.1-16)
2CH 25:0:	 \r (2 Kings 14.2-6)
2CH 25:4:	 \r (2 Kings 14.7)
2CH 25:16:	 \r (2 Kings 14.8-20)
2CH 26:0:	 \r (2 Kings 14.21,22; 15.1-7)
2CH 27:0:	 \r (2 Kings 15.32-38)
2CH 28:0:	 \r (2 Kings 16.1-4)
2CH 28:4:	 \r (2 Kings 16.5)
2CH 28:15:	 \r (2 Kings 16.7-9)
2CH 29:0:	 \r (2 Kings 18.1-3)
2CH 32:0:	 \r (2 Kings 18.13-37; 19.14-19,35-37; Isaiah 36.1-22; 37.8-38)
2CH 32:23:	 \r (2 Kings 20.1-3,12-19; Isaiah 38.1-3; 39.1-8)
2CH 32:31:	 \r (2 Kings 20.20,21)
2CH 33:0:	 \r (2 Kings 21.1-9)
2CH 33:17:	 \r (2 Kings 21.17,18)
2CH 33:20:	 \r (2 Kings 21.19-26)
2CH 34:0:	 \r (2 Kings 22.1,2)
2CH 34:7:	 \r (2 Kings 22.3-20)
2CH 34:28:	 \r (2 Kings 23.1-20)
2CH 35:0:	 \r (2 Kings 23.21-23)
2CH 35:19:	 \r (2 Kings 23.28-30)
2CH 36:0:	 \r (2 Kings 23.30-35)
2CH 36:4:	 \r (2 Kings 23.36—24.7)
2CH 36:8:	 \r (2 Kings 24.8-17)
2CH 36:10:	 \r (2 Kings 24.18-20; Jeremiah 52.1-3a)
2CH 36:12:	 \r (2 Kings 25.1-21; Jeremiah 52.3b-11)
2CH 36:21:	 \r (Ezra 1.1-4)
EZR 2:0:	 \r (Nehemiah 7.4-73)
NEH 7:3:	 \r (Ezra 2.1-70)
PSA 14:0:	 \r (Psalm 53)
PSA 18:0:	 \r (2 Samuel 22.1-51)
PSA 40:11:	 \r (Psalm 70)
PSA 53:0:	 \r (Psalm 14)
PSA 70:0:	 \r (Psalm 40.13-17)
PSA 96:0:	 \r (1 Chronicles 16.23-33)
PSA 105:0:	 \r (1 Chronicles 16.8-22)
PSA 108:0:	 \r (Psalm 57.7-11; 60.5-12)
ISA 2:0:	 \r (Micah 4.1-3)
ISA 36:0:	 \r (2 Kings 18.13-27; 2 Chronicles 32.1-19)
ISA 37:0:	 \r (2 Kings 19.1-7)
ISA 37:7:	 \r (2 Kings 19.8-19)
ISA 37:20:	 \r (2 Kings 19.20-37)
ISA 38:0:	 \r (2 Kings 20.1-11; 2 Chronicles 32.24-26)
ISA 39:0:	 \r (2 Kings 20.12-19)
JER 40:6:	 \r (2 Kings 25.22-24)
JER 40:12:	 \r (2 Kings 25.25,26)
JER 52:0:	 \r (2 Kings 24.18—25.7)
JER 52:11:	 \r (2 Kings 25.8-17)
JER 52:23:	 \r (2 Kings 25.18-21,27-30)
EZK 3:15:	 \r (Ezekiel 33.1-9)
EZK 33:0:	 \r (Ezekiel 3.16-21)
EZK 45:17:	 \r (Exodus 12.1-20; Leviticus 23.33-43)
MIC 4:0:	 \r (Isaiah 2.2-4)"""


# ==============================================================================
# BOOK NAMES MANAGER (BookNames.xml Support)
# ==============================================================================


def discover_resource_presets(base_dir=None):
    """
    Discover all available resource reference presets:
    Scans the 'Resource_Presets' subfolder for reference text files.
    """
    presets = {
        "GNTD - Good News Translation (272 OT References)": "BUILTIN_GNTD",
    }
    if not base_dir:
        base_dir = Path(__file__).parent
    else:
        base_dir = Path(base_dir)

    candidate_dirs = [base_dir / "Resource_Presets", Path(r"D:\Projects\Paratext References Transfer\Resource_Presets")]
    seen_files = set()
    for presets_dir in candidate_dirs:
        if presets_dir.exists() and presets_dir.is_dir():
            for f in sorted(presets_dir.glob("*.txt")):
                if f.name in seen_files:
                    continue
                seen_files.add(f.name)
                name = f.stem
                # Human-readable label formatting
                clean_name = name.replace("_", " ")
                try:
                    # Count valid reference entries if possible
                    count = 0
                    for line in f.read_text(encoding="utf-8-sig", errors="ignore").splitlines():
                        if re.match(r"^[A-Z0-9]{3}\s+\d+[:.]\d+", line.strip()):
                            count += 1
                    if count > 0:
                        label = f"{clean_name} ({count} References)"
                    else:
                        label = f"{clean_name} ({f.name})"
                except Exception:
                    label = f"{clean_name} ({f.name})"
                presets[label] = str(f)
    return presets


class BookNamesManager:
    """Handles loading and resolving book names from Paratext BookNames.xml files."""

    def __init__(self, project_dir=None, xml_path=None):
        self.code_to_abbr = {}
        self.code_to_short = {}
        self.code_to_long = {}
        self.alias_to_code = {}
        self.loaded_source = "None"
        
        # Pre-seed with English aliases
        for alias, code in ENGLISH_BOOK_ALIASES.items():
            self.alias_to_code[alias] = code
        for code, eng_name in BOOK_CODE_TO_ENGLISH.items():
            self.code_to_abbr[code] = code
            self.code_to_short[code] = eng_name
            self.code_to_long[code] = eng_name

        if xml_path and Path(xml_path).exists():
            self.load_xml(xml_path)
        elif project_dir:
            proj_p = Path(project_dir)
            candidate_xml = proj_p / "BookNames.xml"
            if candidate_xml.exists():
                self.load_xml(candidate_xml)
            else:
                # Fallback to IRVTamBooknames.xml in script directory if available
                script_xml = Path(__file__).parent / "IRVTamBooknames.xml"
                if script_xml.exists():
                    self.load_xml(script_xml)

    def load_xml(self, xml_file):
        try:
            tree = ET.parse(str(xml_file))
            root = tree.getroot()
            count = 0
            for book in root.findall(".//book"):
                code = book.attrib.get("code")
                abbr = book.attrib.get("abbr", code)
                short = book.attrib.get("short", abbr)
                long_name = book.attrib.get("long", short)
                if code:
                    self.code_to_abbr[code] = abbr
                    self.code_to_short[code] = short
                    self.code_to_long[code] = long_name
                    # Map local names back to code
                    for val in (abbr, short, long_name):
                        if val:
                            self.alias_to_code[val.strip().lower()] = code
                            self.alias_to_code[val.strip()] = code
                    count += 1
            self.loaded_source = f"{Path(xml_file).name} ({count} books)"
            return count
        except Exception as e:
            self.loaded_source = f"Error loading {Path(xml_file).name}: {e}"
            return 0

    def get_abbr(self, code):
        """Get local abbreviation for a book code (e.g. GEN -> 'ஆதி', 1CH -> '1நாளா')."""
        return self.code_to_abbr.get(code, code)

    def get_short(self, code):
        return self.code_to_short.get(code, BOOK_CODE_TO_ENGLISH.get(code, code))

    def resolve_code(self, name_or_alias):
        """Resolve any English or vernacular name/abbr to 3-letter USFM code."""
        if not name_or_alias:
            return None
        cleaned = name_or_alias.strip()
        # Direct lookup
        if cleaned in self.alias_to_code:
            return self.alias_to_code[cleaned]
        if cleaned.lower() in self.alias_to_code:
            return self.alias_to_code[cleaned.lower()]
        if cleaned.upper() in ALL_BOOK_CODES:
            return cleaned.upper()
        return None


# ==============================================================================
# REFERENCE PARSER & CANONICALIZER
# ==============================================================================

def canonicalize_passage_text(text):
    """Normalize colons, en/em-dashes, and remove parens/brackets."""
    if not text:
        return ""
    val = text
    # Remove surrounding or internal parens/brackets
    val = re.sub(r"[()[\]]", "", val)
    # Replace en-dash and em-dash with standard hyphen
    val = re.sub(r"[–—]", "-", val)
    # Replace chapter.verse dot with colon (e.g. 1.1-4 -> 1:1-4)
    # Be careful not to replace dots in book abbreviations! Only dot followed by digits
    val = re.sub(r"(\d+)\.(\d+)", r"\1:\2", val)
    # Normalize spaces around hyphens and colons
    val = re.sub(r"\s*:\s*", ":", val)
    val = re.sub(r"\s*-\s*", "-", val)
    val = re.sub(r"\s*,\s*", ",", val)
    val = re.sub(r"\s*;\s*", "; ", val)
    # Remove trailing punctuation
    val = re.sub(r"[\s.,;:]+$", "", val)
    return val.strip()


def parse_reference_payload(raw_ref_text, book_manager):
    """
    Parse a raw \r reference string (English or vernacular) into normalized parts:
    e.g. '1 Chronicles 1.1-4' -> [('1CH', '1:1-4')]
    e.g. 'Exodus 34.18-26; Deuteronomy 16.1-17' -> [('EXO', '34:18-26'), ('DEU', '16:1-17')]
    e.g. 'Deuteronomy 7.1-5; 16.1-17' -> [('DEU', '7:1-5'), ('DEU', '16:1-17')]
    """
    cleaned = canonicalize_passage_text(raw_ref_text)
    if not cleaned:
        return []

    # Sort known aliases by length descending so multi-word names match first
    known_aliases = sorted(book_manager.alias_to_code.keys(), key=lambda x: len(x), reverse=True)

    items = []
    current_code = None

    for segment in cleaned.split(";"):
        seg = segment.strip()
        if not seg:
            continue

        matched_code = None
        matched_passage = ""

        # Try matching known book names/aliases at start of segment
        for alias in known_aliases:
            if seg[:len(alias)].casefold() == alias.casefold():
                # Check boundary: next char must be space or digit
                rest = seg[len(alias):]
                if not rest or rest[0].isspace() or rest[0].isdigit():
                    matched_code = book_manager.alias_to_code[alias]
                    matched_passage = rest.strip()
                    break

        if matched_code:
            current_code = matched_code
            passage = matched_passage
        elif current_code:
            # Continues previous book (e.g. '16:1-17' after 'Deuteronomy 7:1-5')
            passage = seg
        else:
            # Fallback: couldn't identify book, keep raw
            items.append((None, seg))
            continue

        passage = canonicalize_passage_text(passage)
        items.append((current_code, passage))

    return items


def format_reference_for_target(parsed_items, target_book_mgr, use_parentheses=True):
    """
    Format parsed reference items into target vernacular string:
    e.g. [('1CH', '1:1-4')] with Tamil mgr -> '(1நாளா 1:1-4)' (or '1நாளா 1:1-4')
    """
    parts = []
    for code, passage in parsed_items:
        if code:
            abbr = target_book_mgr.get_abbr(code)
            parts.append(f"{abbr} {passage}".strip())
        else:
            parts.append(passage)

    joined = "; ".join(parts).strip()
    joined = re.sub(r"[\s.,;:]+$", "", joined) # strictly no trailing punctuation

    if use_parentheses:
        return f"({joined})"
    return joined


def canonical_key_for_comparison(parsed_items):
    """Produce a canonical string for duplicate detection: e.g. '1CH:1:1-4'."""
    keys = []
    for code, passage in parsed_items:
        clean_pass = re.sub(r"\s+", "", passage)
        keys.append(f"{code}:{clean_pass}")
    return ";".join(keys)


# ==============================================================================
# SOURCE EXTRACTORS: GNTD & PROJECT
# ==============================================================================

def parse_gntd_text(text):
    """
    Parse GNTD reference rows:
    e.g. 'GEN 5:0: \\r (1 Chronicles 1.1-4)'
    Supports both single-line and split-line (header on line 1, ref on line 2) formats.
    Target verse is calculated as end_verse + 1.
    """
    rows = []
    errors = []
    lines = text.splitlines()
    i = 0
    while i < len(lines):
        line_num = i + 1
        line_s = lines[i].strip()
        i += 1
        if not line_s:
            continue

        # Check for single-line format: BOOK CH:START-END: [\r] (REF)
        match = re.match(r"^([A-Z0-9]{3})\s+(\d+):(\d+(?:-\d+)?)\s*:\s*(?:\\r\s*)?(.*?)\s*$", line_s)
        if match and match.group(4):
            book_code = match.group(1).upper()
            chapter = int(match.group(2))
            loc_str = match.group(3)
            raw_ref = match.group(4)
        else:
            # Check if this line is just the header: BOOK CH:START-END:
            header_match = re.match(r"^([A-Z0-9]{3})\s+(\d+):(\d+(?:-\d+)?)\s*:?\s*$", line_s)
            if header_match and i < len(lines):
                next_line = lines[i].strip()
                ref_clean = re.sub(r"^\\r\s*", "", next_line).strip()
                if ref_clean.startswith("(") and ref_clean.endswith(")"):
                    book_code = header_match.group(1).upper()
                    chapter = int(header_match.group(2))
                    loc_str = header_match.group(3)
                    raw_ref = ref_clean
                    i += 1 # consumed next line
                else:
                    errors.append(f"Line {line_num}: Unrecognized GNTD format: {line_s}")
                    continue
            else:
                errors.append(f"Line {line_num}: Unrecognized GNTD format: {line_s}")
                continue

        # Calculate target verse: end_verse + 1
        end_verse = int(loc_str.split("-")[-1])
        target_verse = end_verse + 1

        rows.append({
            "source_type": "GNTD",
            "line": line_num,
            "book": book_code,
            "chapter": chapter,
            "source_loc": loc_str,
            "verse": target_verse,
            "raw_reference": raw_ref,
        })
    return rows, errors


def extract_references_from_project(source_proj_dir, book_codes=None):
    """
    Extract parallel passage references (\r) from an existing Paratext project.
    Scans SFM files, extracts \r lines and connects them to the following \v verse.
    """
    proj_path = Path(source_proj_dir)
    if not proj_path.exists():
        raise FileNotFoundError(f"Source project folder does not exist: {source_proj_dir}")

    source_book_mgr = BookNamesManager(project_dir=proj_path)
    sfm_files = find_sfm_files(proj_path)
    
    extracted_rows = []
    errors = []

    for book_code, file_path in sfm_files.items():
        if book_codes and book_code not in book_codes:
            continue

        try:
            content, has_bom, newline = read_usfm_file(file_path)
            lines = content.splitlines()
            current_chapter = 0
            pending_refs = []
            last_heading = None

            for line in lines:
                c_match = re.match(r"^\\c\s+(\d+)", line)
                if c_match:
                    current_chapter = int(c_match.group(1))
                    pending_refs = []
                    last_heading = None
                    continue

                s_match = re.match(r"^\\s\d*\s+(.*)", line)
                if s_match:
                    last_heading = s_match.group(1).strip()
                    continue

                r_match = re.match(r"^\\r\s+(.*)", line)
                if r_match:
                    raw_r = r_match.group(1).strip()
                    pending_refs.append((raw_r, last_heading))
                    continue

                v_match = re.match(r"^\\v\s+(\d+)", line)
                if v_match:
                    verse_num = int(v_match.group(1))
                    if pending_refs and current_chapter > 0:
                        for ref_text, heading in pending_refs:
                            extracted_rows.append({
                                "source_type": "Project",
                                "book": book_code,
                                "chapter": current_chapter,
                                "verse": verse_num,
                                "raw_reference": ref_text,
                                "heading": heading,
                                "source_file": file_path.name,
                            })
                        pending_refs = []
                    continue

        except Exception as e:
            errors.append(f"Error reading {file_path.name}: {e}")

    return extracted_rows, errors, source_book_mgr


# ==============================================================================
# FILE SYSTEM UTILITIES
# ==============================================================================

def find_sfm_files(project_dir):
    """
    Discover all USFM / SFM files in a Paratext project directory,
    indexed by their canonical 3-letter book code from \id.
    """
    proj_path = Path(project_dir)
    results = {}
    if not proj_path.exists():
        return results

    seen_paths = set()
    candidate_files = []
    for pat in ("*.SFM", "*.sfm", "*.usfm", "*.USFM"):
        for f in proj_path.glob(pat):
            f_norm = str(f.resolve()).lower()
            if f_norm not in seen_paths:
                seen_paths.add(f_norm)
                candidate_files.append(f)

    for f in sorted(candidate_files, key=lambda x: x.name):
        try:
            raw = f.read_bytes()[:500]
            header = raw.decode("utf-8-sig", errors="ignore")
            id_match = re.search(r"^\\id\s+([A-Z0-9]{3})", header, re.MULTILINE)
            if id_match:
                book_code = id_match.group(1).upper()
                results[book_code] = f
            else:
                # Fallback to filename prefix: e.g. 01GENIRVTam.SFM
                name_match = re.search(r"([A-Z0-9]{3})", f.stem.upper())
                if name_match and name_match.group(1) in ALL_BOOK_CODES:
                    results[name_match.group(1)] = f
        except Exception:
            pass
    return results


def read_usfm_file(path):
    """Read file with UTF-8 BOM detection and keep newline bytes."""
    raw = Path(path).read_bytes()
    has_bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig" if has_bom else "utf-8")
    newline = "\r\n" if b"\r\n" in raw else "\n"
    return text, has_bom, newline


def write_usfm_file(path, content, has_bom=True, newline="\r\n"):
    """Write text preserving UTF-8 BOM and exact newline encoding."""
    # Ensure line endings match target newline
    normalized_lines = content.splitlines()
    final_text = newline.join(normalized_lines) + newline
    raw_bytes = final_text.encode("utf-8")
    if has_bom:
        raw_bytes = b"\xef\xbb\xbf" + raw_bytes
    Path(path).write_bytes(raw_bytes)


def create_backup_file(file_path):
    """Create timestamped backup file: <name>.bak_YYYYMMDD_HHMMSS."""
    fp = Path(file_path)
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    backup_path = fp.parent / f"{fp.name}.bak_{timestamp}"
    shutil.copy2(str(fp), str(backup_path))
    return backup_path


# ==============================================================================
# CORE TRANSFER ENGINE
# ==============================================================================

def process_single_sfm_content(content, book_code, references_for_book,
                               source_book_mgr, target_book_mgr, options):
    """
    Process USFM content for a single book:
      - Clean trailing punctuation from existing \r lines
      - Insert \p before target verse if missing
      - Check for duplicate \r references
      - Insert \s Insert Heading (if missing and enabled)
      - Insert formatted \r reference
    Returns: (new_content, changes, duplicates_skipped, warnings)
    """
    use_parentheses = options.get("use_parentheses", True)
    insert_heading = options.get("insert_heading", True)
    clean_punctuation = options.get("clean_punctuation", True)

    lines = content.splitlines(keepends=True)
    newline = "\r\n" if "\r\n" in content else "\n"

    changes = []
    skipped_duplicates = 0
    warnings = []

    # 1. Remove all existing \r lines first (if option enabled) OR clean trailing punctuation
    remove_existing = options.get("remove_existing", False)
    if remove_existing:
        original_line_count = len(lines)
        lines = [l for l in lines if not re.match(r"^\s*\\r(?:\s|$)", l)]
        removed_count = original_line_count - len(lines)
        if removed_count > 0:
            changes.append({
                "type": "removed_existing",
                "location": f"{book_code}",
                "detail": f"Removed {removed_count} existing \\r references before inserting new ones",
            })
    elif clean_punctuation:
        for idx, line in enumerate(lines):
            r_match = re.match(r"^(\\r\s+)(.*?)(\r?\n)?$", line)
            if r_match:
                prefix = r_match.group(1)
                body = r_match.group(2)
                eol = r_match.group(3) or ""
                # Strip trailing dots, colons, semicolons
                cleaned_body = re.sub(r"[\s.,;:]+$", "", body)
                if cleaned_body != body:
                    lines[idx] = prefix + cleaned_body + eol

    # 2. Group references by chapter and verse
    for ref_row in references_for_book:
        chapter = ref_row["chapter"]
        target_verse = ref_row["verse"]
        raw_ref = ref_row["raw_reference"]

        # Parse reference items using source book manager
        parsed_items = parse_reference_payload(raw_ref, source_book_mgr)
        if not parsed_items:
            warnings.append(f"{book_code} {chapter}:{target_verse} - Could not parse reference: {raw_ref}")
            continue

        target_ref_string = format_reference_for_target(parsed_items, target_book_mgr, use_parentheses)
        target_key = canonical_key_for_comparison(parsed_items)

        # Locate chapter
        chapter_idx = None
        for idx, line in enumerate(lines):
            c_match = re.match(r"^\\c\s+(\d+)(?:\s|$)", line)
            if c_match and int(c_match.group(1)) == chapter:
                chapter_idx = idx
                break

        if chapter_idx is None:
            warnings.append(f"{book_code} - Chapter {chapter} not found (ref: {raw_ref})")
            continue

        # Locate target verse (handles \v 1, \q1 \v 1, \q \v 1, \p \v 1, \m \v 1, \d \v 1, \v 1-2, etc.)
        verse_idx = None
        for idx in range(chapter_idx + 1, len(lines)):
            if re.match(r"^\\c\s+", lines[idx]):
                break
            v_match = re.search(r"\\v\s+(\d+)(?:[a-zA-Z]|-\d+)?(?:\s|$)", lines[idx])
            if v_match and int(v_match.group(1)) == target_verse:
                verse_idx = idx
                break

        if verse_idx is None:
            warnings.append(f"{book_code} {chapter}:{target_verse} - Verse not found in file (ref: {raw_ref})")
            continue

        # Check USFM paragraph or poetry marker (\p, \m, \q, \q1, \q2, \q3, \q4, \b, \d, \nb, \pi, \pc, etc.)
        para_pattern = re.compile(r"^\s*\\(p|m|q\d*|b|d|nb|pi\d*|pc|mi|po|pr|cls|qr|qc|qs)(?:\s|$)")
        starts_with_para = bool(para_pattern.match(lines[verse_idx]))
        prev_idx = verse_idx - 1
        prev_is_para = (prev_idx >= 0 and bool(para_pattern.match(lines[prev_idx].strip())))

        if not starts_with_para and not prev_is_para:
            lines.insert(verse_idx, "\\p" + newline)
            verse_idx += 1
            changes.append({
                "type": "paragraph",
                "location": f"{book_code} {chapter}:{target_verse}",
                "detail": "Added missing \\p before target verse",
            })
            insert_at = verse_idx - 1
        elif starts_with_para:
            insert_at = verse_idx
        else:
            insert_at = prev_idx

        # Scan upward from insertion point to inspect existing heading/reference block
        scan_idx = insert_at - 1
        existing_refs = []
        has_heading = False

        while scan_idx >= 0:
            line_str = lines[scan_idx].strip()
            if re.match(r"^\\r(?:\s|$)", line_str):
                existing_refs.append(line_str)
            elif re.match(r"^\\s\d*(?:\s|$)", line_str):
                has_heading = True
                break
            elif not line_str or re.match(r"^\\(d|ms\d*)(?:\s|$)", line_str):
                pass
            else:
                break
            scan_idx -= 1
        # Check for duplicate
        is_duplicate = False
        for ex in existing_refs:
            ex_body = re.sub(r"^\\r\s*", "", ex)
            ex_items = parse_reference_payload(ex_body, target_book_mgr)
            if canonical_key_for_comparison(ex_items) == target_key:
                is_duplicate = True
                break

        if is_duplicate:
            skipped_duplicates += 1
            continue

        # Build insertion lines
        to_insert = []
        heading_text = None
        if insert_heading and not has_heading:
            heading_text = "\\s Insert Heading"
            to_insert.append(heading_text + newline)

        ref_line = f"\\r {target_ref_string}" + newline
        to_insert.append(ref_line)

        # Insert immediately before paragraph
        lines[insert_at:insert_at] = to_insert

        changes.append({
            "type": "reference",
            "location": f"{book_code} {chapter}:{target_verse}",
            "reference": target_ref_string,
            "heading_added": bool(heading_text),
        })

    return "".join(lines), changes, skipped_duplicates, warnings


def execute_reference_transfer(source_type, source_val, target_proj_dir,
                               books_scope="OT", selected_book=None,
                               options=None, dry_run=False,
                               log_fn=None, progress_fn=None):
    """
    Main transfer execution function.
    Can be called by GUI or CLI/Test scripts.
    """
    if options is None:
        options = {
            "use_parentheses": True,
            "insert_heading": True,
            "clean_punctuation": True,
            "create_backups": True,
        }

    def log(msg, level="INFO"):
        if log_fn:
            log_fn(msg, level)
        else:
            print(f"[{level}] {msg}")

    target_path = Path(target_proj_dir)
    if not target_path.exists():
        raise FileNotFoundError(f"Target project folder does not exist: {target_proj_dir}")

    # Determine books to include
    if books_scope == "OT":
        filter_books = set(OT_BOOK_CODES)
    elif books_scope == "NT":
        filter_books = set(NT_BOOK_CODES)
    elif books_scope == "ALL":
        filter_books = set(ALL_BOOK_CODES)
    elif books_scope == "SINGLE" and selected_book:
        filter_books = {selected_book.upper()}
    else:
        filter_books = set(ALL_BOOK_CODES)

    log(f"Initializing Target Project: {target_path.name}")
    target_book_mgr = BookNamesManager(project_dir=target_path)
    log(f"Target Book Names Source: {target_book_mgr.loaded_source}")

    # Step 1: Collect references from Source
    source_book_mgr = None
    all_references = []

    if source_type in ("GNTD", "RESOURCE_PRESET", "DBL", "PRESET"):
        preset_key = source_val or "GNTD"
        presets_map = discover_resource_presets(Path(__file__).parent)

        content = None
        # 1. Direct file path
        if source_val and Path(source_val).is_file():
            res_file = Path(source_val)
            log(f"Reading references from resource file: {res_file.name}")
            content = res_file.read_text(encoding="utf-8")
        else:
            # 2. Check Resource_Presets/ directory directly for ID (e.g. HINOVBSI -> HINOVBSI_References.txt)
            cand_dirs = [Path(__file__).parent / "Resource_Presets", Path(r"D:\Projects\Paratext References Transfer\Resource_Presets")]
            for presets_dir in cand_dirs:
                if presets_dir.exists():
                    direct_matches = list(presets_dir.glob(f"*{preset_key}*.txt"))
                    if direct_matches:
                        res_file = direct_matches[0]
                        log(f"Reading references from resource file: {res_file.name}")
                        content = res_file.read_text(encoding="utf-8")
                        break

            # 3. Lookup in presets_map by key or substring
            if not content:
                file_path_or_builtin = presets_map.get(preset_key)
                if not file_path_or_builtin:
                    for k, v in presets_map.items():
                        if preset_key and preset_key.upper() in k.upper():
                            file_path_or_builtin = v
                            break

                if file_path_or_builtin and file_path_or_builtin != "BUILTIN_GNTD" and Path(file_path_or_builtin).is_file():
                    res_file = Path(file_path_or_builtin)
                    log(f"Reading references from resource file: {res_file.name}")
                    content = res_file.read_text(encoding="utf-8")

            # 4. Fallback to GNTD
            if not content:
                script_ext = Path(__file__).parent / "GNTD References.txt"
                if script_ext.exists():
                    log(f"Reading references from {script_ext.name}")
                    content = script_ext.read_text(encoding="utf-8")
                else:
                    log("Reading built-in pre-bundled GNTD reference database")
                    content = BUILTIN_GNTD_REFERENCES_TEXT

        rows, parse_errors = parse_gntd_text(content)
        for err in parse_errors:
            log(err, "WARNING")
        all_references = rows
        source_book_mgr = BookNamesManager() # standard English
        log(f"Loaded {len(all_references)} valid reference entries from resource.")

    elif source_type == "PROJECT":
        source_proj_path = Path(source_val)
        log(f"Extracting references from Source Project: {source_proj_path.name}...")
        rows, ext_errors, source_book_mgr = extract_references_from_project(source_proj_path, filter_books)
        for err in ext_errors:
            log(err, "WARNING")
        all_references = rows
        log(f"Extracted {len(all_references)} reference entries from source project.")

    elif source_type == "CUSTOM_FILE":
        file_path = Path(source_val)
        log(f"Loading custom reference file: {file_path.name}...")
        content = file_path.read_text(encoding="utf-8")
        rows, parse_errors = parse_gntd_text(content)
        for err in parse_errors:
            log(err, "WARNING")
        all_references = rows
        source_book_mgr = BookNamesManager()
        log(f"Loaded {len(all_references)} reference entries from custom file.")

    # Filter references by chosen book scope
    filtered_refs = [r for r in all_references if r["book"] in filter_books]
    log(f"References matching book scope ({books_scope}): {len(filtered_refs)}")

    # Group references by book
    refs_by_book = {}
    for r in filtered_refs:
        b = r["book"]
        if b not in refs_by_book:
            refs_by_book[b] = []
        refs_by_book[b].append(r)

    # Step 2: Discover target SFM files
    target_sfm_files = find_sfm_files(target_path)
    log(f"Found {len(target_sfm_files)} SFM files in target project.")

    # Step 3: Process books
    total_refs_added = 0
    total_headings_added = 0
    total_paragraphs_added = 0
    total_duplicates_skipped = 0
    total_warnings = 0
    files_modified = 0

    books_to_process = [b for b in ALL_BOOK_CODES if b in refs_by_book and b in target_sfm_files]
    total_books = len(books_to_process)
    log(f"Books to process: {total_books} matching both references and target project files.")

    mode_label = "[DRY-RUN PREVIEW]" if dry_run else "[APPLYING CHANGES]"
    log(f"Starting execution: {mode_label}", "SUCCESS" if dry_run else "INFO")

    for idx, book_code in enumerate(books_to_process, 1):
        file_path = target_sfm_files[book_code]
        refs_for_this_book = refs_by_book[book_code]

        if progress_fn:
            progress_fn(idx, total_books, f"Processing {book_code} ({idx}/{total_books})...")

        try:
            content, has_bom, newline = read_usfm_file(file_path)
            new_content, changes, skipped_dups, warnings = process_single_sfm_content(
                content, book_code, refs_for_this_book,
                source_book_mgr, target_book_mgr, options
            )

            for w in warnings:
                log(w, "WARNING")
                total_warnings += 1

            ref_changes = [c for c in changes if c["type"] == "reference"]
            p_changes = [c for c in changes if c["type"] == "paragraph"]
            h_changes = [c for c in changes if c.get("heading_added")]

            total_refs_added += len(ref_changes)
            total_paragraphs_added += len(p_changes)
            total_headings_added += len(h_changes)
            total_duplicates_skipped += skipped_dups

            if changes:
                files_modified += 1
                sample_str = ", ".join(c["location"] for c in ref_changes[:3])
                if len(ref_changes) > 3:
                    sample_str += f" and {len(ref_changes)-3} more"
                log(f"{book_code}: +{len(ref_changes)} \\r refs, +{len(h_changes)} headings, +{len(p_changes)} \\p ({sample_str})", "SUCCESS")

                if not dry_run:
                    # Create backup
                    if options.get("create_backups", True):
                        bk_path = create_backup_file(file_path)
                    # Write modified content
                    write_usfm_file(file_path, new_content, has_bom, newline)
            else:
                if skipped_dups > 0:
                    log(f"{book_code}: All {skipped_dups} references were exact duplicates (already up to date).")
                else:
                    log(f"{book_code}: No changes required.")

        except Exception as e:
            log(f"Error processing {book_code} ({file_path.name}): {e}", "ERROR")
            total_warnings += 1

    log("=" * 60)
    summary_header = "DRY-RUN PREVIEW SUMMARY (No files were modified)" if dry_run else "TRANSFER COMPLETED SUCCESSFULLY"
    log(summary_header, "SUCCESS")
    log(f"  • Files modified: {files_modified} of {total_books}")
    log(f"  • Total \\r references inserted: {total_refs_added}")
    log(f"  • Total \\s headings inserted: {total_headings_added}")
    log(f"  • Total \\p paragraph markers added: {total_paragraphs_added}")
    log(f"  • Exact duplicates preserved & skipped: {total_duplicates_skipped}")
    if total_warnings > 0:
        log(f"  • Warnings/issues encountered: {total_warnings}", "WARNING")
    log("=" * 60)

    return {
        "files_modified": files_modified,
        "refs_added": total_refs_added,
        "headings_added": total_headings_added,
        "paragraphs_added": total_paragraphs_added,
        "duplicates_skipped": total_duplicates_skipped,
        "warnings": total_warnings,
    }


# ==============================================================================
# MODERN TKINTER GUI APPLICATION
# ==============================================================================

class ParatextReferenceTransferApp:
    """Modern GUI for Paratext Parallel Passage References Transfer."""

    def __init__(self, root, initial_settings_dir=None, initial_target=None):
        self.root = root
        self.root.title("Paratext Parallel Passage References Transfer Tool - v1.0")
        self.root.geometry("980x750")
        self.root.minsize(800, 600)

        # Set default paratext directory
        self.default_paratext_dir = Path(r"D:\Transdata\My Paratext 9 Projects")
        if not self.default_paratext_dir.exists():
            for alt in [
                Path(r"C:\My Paratext 9 Projects"),
                Path(r"D:\My Paratext 9 Projects"),
                Path.home() / "My Paratext 9 Projects",
            ]:
                if alt.exists():
                    self.default_paratext_dir = alt
                    break

        if initial_settings_dir and Path(initial_settings_dir).exists():
            self.default_paratext_dir = Path(initial_settings_dir)

        # Variables
        self.var_paratext_dir = tk.StringVar(value=str(self.default_paratext_dir))
        self.var_source_type = tk.StringVar(value="RESOURCE_PRESET")
        self.resource_presets = discover_resource_presets(Path(__file__).parent)
        default_preset = list(self.resource_presets.keys())[0] if self.resource_presets else "GNTD"
        self.var_resource_preset = tk.StringVar(value=default_preset)
        self.var_source_project = tk.StringVar()
        self.var_custom_file = tk.StringVar()
        self.var_target_project = tk.StringVar(value=initial_target or "")
        self.var_books_scope = tk.StringVar(value="OT")
        self.var_single_book = tk.StringVar(value="GEN")
        
        # Options
        self.var_opt_parentheses = tk.BooleanVar(value=True)
        self.var_opt_heading = tk.BooleanVar(value=True)
        self.var_opt_clean_punct = tk.BooleanVar(value=True)
        self.var_opt_backup = tk.BooleanVar(value=True)
        self.var_opt_remove_existing = tk.BooleanVar(value=False)

        self.available_projects = []
        self._build_ui()
        self._refresh_projects_list()

    def _build_ui(self):
        self.root.configure(bg="#F1F5F9")
        
        # Configure TTK Styles for a crisp, modern look
        style = ttk.Style()
        try:
            if "vista" in style.theme_names():
                style.theme_use("vista")
            elif "clam" in style.theme_names():
                style.theme_use("clam")
        except Exception:
            pass

        style.configure(".", font=("Segoe UI", 9))
        style.configure("TNotebook", background="#F1F5F9", tabmargins=[10, 5, 10, 0])
        style.configure("TNotebook.Tab", font=("Segoe UI", 10, "bold"), padding=[16, 6])
        style.configure("TLabelframe", background="#FFFFFF", relief="solid", borderwidth=1)
        style.configure("TLabelframe.Label", font=("Segoe UI", 10, "bold"), foreground="#1E293B", background="#FFFFFF")
        style.configure("TFrame", background="#F1F5F9")
        style.configure("Card.TFrame", background="#FFFFFF", relief="solid", borderwidth=1)
        style.configure("CardInner.TFrame", background="#FFFFFF")
        style.configure("TLabel", background="#FFFFFF", foreground="#334155", font=("Segoe UI", 9))
        style.configure("Header.TLabel", font=("Segoe UI", 10, "bold"), foreground="#0F172A", background="#FFFFFF")
        style.configure("Sub.TLabel", font=("Segoe UI", 8), foreground="#64748B", background="#FFFFFF")
        style.configure("TRadiobutton", background="#FFFFFF", foreground="#1E293B", font=("Segoe UI", 9))
        style.configure("TCheckbutton", background="#FFFFFF", foreground="#1E293B", font=("Segoe UI", 9))
        style.configure("TProgressbar", thickness=8)

        # 0. Modern Sleek Header Banner
        banner = tk.Frame(self.root, bg="#0F172A", height=54)
        banner.pack(fill=tk.X, side=tk.TOP)
        banner.pack_propagate(False)

        title_lbl = tk.Label(
            banner, text="Paratext Parallel Passage References Transfer",
            font=("Segoe UI", 13, "bold"), fg="#FFFFFF", bg="#0F172A"
        )
        title_lbl.pack(side=tk.LEFT, padx=16, pady=4)

        sub_lbl = tk.Label(
            banner, text="v1.0  |  Synchronize \\r Parallel Passages in Projects & Resources",
            font=("Segoe UI", 9), fg="#94A3B8", bg="#0F172A"
        )
        sub_lbl.pack(side=tk.LEFT, padx=10, pady=8)

        # Top Notebook for Tabs
        notebook = ttk.Notebook(self.root)
        notebook.pack(fill=tk.BOTH, expand=True, padx=12, pady=(8, 12))

        # TAB 1: Transfer References
        tab_transfer = ttk.Frame(notebook)
        notebook.add(tab_transfer, text="  Transfer References  ")

        # TAB 2: Layman's User Guide
        tab_guide = ttk.Frame(notebook)
        notebook.add(tab_guide, text="  User Guide & Documentation  ")

        self._build_transfer_tab(tab_transfer)
        self._build_guide_tab(tab_guide)

    def _build_transfer_tab(self, parent):
        # 1. Projects Folder Card
        frame_dir_card = ttk.LabelFrame(parent, text="  Paratext Projects Directory  ", padding=10)
        frame_dir_card.pack(fill=tk.X, padx=10, pady=(6, 4))

        frame_dir_inner = ttk.Frame(frame_dir_card, style="CardInner.TFrame")
        frame_dir_inner.pack(fill=tk.X)

        ttk.Label(frame_dir_inner, text="Directory:", font=("Segoe UI", 9, "bold")).pack(side=tk.LEFT, padx=(0, 6))
        entry_dir = ttk.Entry(frame_dir_inner, textvariable=self.var_paratext_dir, font=("Segoe UI", 9))
        entry_dir.pack(side=tk.LEFT, fill=tk.X, expand=True, padx=6)

        btn_browse_dir = tk.Button(
            frame_dir_inner, text="Browse...", font=("Segoe UI", 8, "bold"),
            bg="#F8FAFC", fg="#1E293B", relief="solid", bd=1, padx=10, pady=2,
            cursor="hand2", command=self._browse_paratext_dir
        )
        btn_browse_dir.pack(side=tk.LEFT, padx=3)

        btn_refresh = tk.Button(
            frame_dir_inner, text="Refresh Projects", font=("Segoe UI", 8, "bold"),
            bg="#F8FAFC", fg="#1E293B", relief="solid", bd=1, padx=10, pady=2,
            cursor="hand2", command=self._refresh_projects_list
        )
        btn_refresh.pack(side=tk.LEFT, padx=3)

        # 2. Main 2-Column Section (Source & Target)
        paned = ttk.Frame(parent)
        paned.pack(fill=tk.X, padx=10, pady=4)

        # Column 1: SOURCE
        frame_source = ttk.LabelFrame(paned, text="  1. Select Reference Source  ", padding=10)
        frame_source.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, padx=(0, 5))

        r_preset = ttk.Radiobutton(
            frame_source, text="Resource Preset (Standard References):",
            variable=self.var_source_type, value="RESOURCE_PRESET", command=self._on_source_change
        )
        r_preset.pack(anchor=tk.W, pady=(2, 2))

        frame_preset_box = ttk.Frame(frame_source, style="CardInner.TFrame")
        frame_preset_box.pack(fill=tk.X, padx=22, pady=(0, 6))

        self.combo_resource_preset = ttk.Combobox(
            frame_preset_box,
            textvariable=self.var_resource_preset,
            values=list(self.resource_presets.keys()),
            state="readonly",
            font=("Segoe UI", 9)
        )
        self.combo_resource_preset.pack(side=tk.LEFT, fill=tk.X, expand=True)

        btn_import_res = tk.Button(
            frame_preset_box, text="Add Resource...", font=("Segoe UI", 8, "bold"),
            bg="#F8FAFC", fg="#1E293B", relief="solid", bd=1, padx=6, pady=1,
            cursor="hand2", command=self._import_resource_preset
        )
        btn_import_res.pack(side=tk.LEFT, padx=(4, 0))

        r_proj = ttk.Radiobutton(
            frame_source, text="Paratext Project (Copy \\r from another project):",
            variable=self.var_source_type, value="PROJECT", command=self._on_source_change
        )
        r_proj.pack(anchor=tk.W, pady=(4, 2))

        self.combo_source_proj = ttk.Combobox(
            frame_source, textvariable=self.var_source_project, state="disabled", font=("Segoe UI", 9)
        )
        self.combo_source_proj.pack(fill=tk.X, padx=22, pady=(0, 6))

        r_custom = ttk.Radiobutton(
            frame_source, text="Custom Resource Reference File (.txt):",
            variable=self.var_source_type, value="CUSTOM_FILE", command=self._on_source_change
        )
        r_custom.pack(anchor=tk.W, pady=(4, 2))

        frame_custom_box = ttk.Frame(frame_source, style="CardInner.TFrame")
        frame_custom_box.pack(fill=tk.X, padx=22, pady=(0, 4))
        self.entry_custom = ttk.Entry(frame_custom_box, textvariable=self.var_custom_file, state="disabled", font=("Segoe UI", 9))
        self.entry_custom.pack(side=tk.LEFT, fill=tk.X, expand=True)
        self.btn_browse_custom = tk.Button(
            frame_custom_box, text="...", width=3, state="disabled", font=("Segoe UI", 8, "bold"),
            bg="#F8FAFC", fg="#1E293B", relief="solid", bd=1, padx=4, pady=1, command=self._browse_custom_file
        )
        self.btn_browse_custom.pack(side=tk.LEFT, padx=(4, 0))

        # Column 2: TARGET & SCOPE
        frame_target = ttk.LabelFrame(paned, text="  2. Select Target Project & Scope  ", padding=10)
        frame_target.pack(side=tk.RIGHT, fill=tk.BOTH, expand=True, padx=(5, 0))

        ttk.Label(frame_target, text="Target Project:", font=("Segoe UI", 9, "bold")).pack(anchor=tk.W, pady=(0, 2))
        self.combo_target_proj = ttk.Combobox(
            frame_target, textvariable=self.var_target_project, state="readonly", font=("Segoe UI", 9)
        )
        self.combo_target_proj.pack(fill=tk.X, pady=(0, 4))
        self.combo_target_proj.bind("<<ComboboxSelected>>", self._on_target_change)

        self.lbl_target_info = tk.Label(
            frame_target, text="Select a target project...", font=("Segoe UI", 8),
            fg="#059669", bg="#FFFFFF", anchor=tk.W
        )
        self.lbl_target_info.pack(fill=tk.X, pady=(0, 6))

        ttk.Label(frame_target, text="Books to Process:", font=("Segoe UI", 9, "bold")).pack(anchor=tk.W, pady=(4, 2))
        frame_scope_inner = ttk.Frame(frame_target, style="CardInner.TFrame")
        frame_scope_inner.pack(fill=tk.X)

        ttk.Radiobutton(frame_scope_inner, text="All OT (39)", variable=self.var_books_scope, value="OT").pack(side=tk.LEFT, padx=(0, 8))
        ttk.Radiobutton(frame_scope_inner, text="All NT (27)", variable=self.var_books_scope, value="NT").pack(side=tk.LEFT, padx=8)
        ttk.Radiobutton(frame_scope_inner, text="All 66", variable=self.var_books_scope, value="ALL").pack(side=tk.LEFT, padx=8)

        frame_single = ttk.Frame(frame_scope_inner, style="CardInner.TFrame")
        frame_single.pack(side=tk.LEFT, padx=8)
        ttk.Radiobutton(frame_single, text="Single:", variable=self.var_books_scope, value="SINGLE").pack(side=tk.LEFT)
        combo_single = ttk.Combobox(frame_single, textvariable=self.var_single_book, values=ALL_BOOK_CODES, width=5, state="readonly")
        combo_single.pack(side=tk.LEFT, padx=(4, 0))

        # 3. Transfer Options Card
        frame_opts = ttk.LabelFrame(parent, text="  3. Transfer Options  ", padding=10)
        frame_opts.pack(fill=tk.X, padx=10, pady=4)

        frame_opts_grid = ttk.Frame(frame_opts, style="CardInner.TFrame")
        frame_opts_grid.pack(fill=tk.X)

        ttk.Checkbutton(
            frame_opts_grid, text="Put parentheses around \\r references  (e.g., (1நாளா 1:1))",
            variable=self.var_opt_parentheses
        ).grid(row=0, column=0, sticky=tk.W, padx=(0, 20), pady=3)

        ttk.Checkbutton(
            frame_opts_grid, text="Insert '\\s Insert Heading' if section heading is missing",
            variable=self.var_opt_heading
        ).grid(row=0, column=1, sticky=tk.W, padx=20, pady=3)

        ttk.Checkbutton(
            frame_opts_grid, text="Clean trailing full stops / periods from all \\r lines",
            variable=self.var_opt_clean_punct
        ).grid(row=1, column=0, sticky=tk.W, padx=(0, 20), pady=3)

        ttk.Checkbutton(
            frame_opts_grid, text="Create automatic timestamped backup (.bak_*) before saving",
            variable=self.var_opt_backup
        ).grid(row=1, column=1, sticky=tk.W, padx=20, pady=3)

        ttk.Checkbutton(
            frame_opts_grid, text="Remove all existing \\r references first before inserting",
            variable=self.var_opt_remove_existing
        ).grid(row=2, column=0, columnspan=2, sticky=tk.W, padx=(0, 20), pady=(6, 2))

        # 4. Action Buttons & Progress Bar
        frame_actions = ttk.Frame(parent)
        frame_actions.pack(fill=tk.X, padx=10, pady=(6, 4))

        self.btn_apply = tk.Button(
            frame_actions, text="Transfer References (Apply)",
            font=("Segoe UI", 10, "bold"), bg="#2563EB", fg="#FFFFFF",
            activebackground="#1D4ED8", activeforeground="#FFFFFF",
            relief="flat", padx=20, pady=6, cursor="hand2", command=self._cmd_apply
        )
        self.btn_apply.pack(side=tk.LEFT, padx=(0, 8))

        self.btn_preview = tk.Button(
            frame_actions, text="Preview Changes (Dry Run)",
            font=("Segoe UI", 10), bg="#FFFFFF", fg="#1E293B",
            activebackground="#F8FAFC", activeforeground="#0F172A",
            relief="solid", bd=1, padx=16, pady=6, cursor="hand2", command=self._cmd_preview
        )
        self.btn_preview.pack(side=tk.LEFT, padx=4)

        btn_clear = tk.Button(
            frame_actions, text="Clear Log",
            font=("Segoe UI", 9), bg="#FFFFFF", fg="#64748B",
            activebackground="#F1F5F9", relief="solid", bd=1, padx=12, pady=5,
            cursor="hand2", command=self._clear_log
        )
        btn_clear.pack(side=tk.RIGHT)

        self.progress_bar = ttk.Progressbar(parent, orient=tk.HORIZONTAL, mode="determinate")
        self.progress_bar.pack(fill=tk.X, padx=10, pady=(2, 6))

        # 5. Activity Log Card
        frame_log = ttk.LabelFrame(parent, text="  Activity & Results Log  ", padding=6)
        frame_log.pack(fill=tk.BOTH, expand=True, padx=10, pady=(0, 8))

        self.log_text = scrolledtext.ScrolledText(
            frame_log, wrap=tk.WORD, height=10, font=("Consolas", 9),
            bg="#FFFFFF", fg="#0F172A", relief="flat", bd=0, padx=6, pady=6
        )
        self.log_text.pack(fill=tk.BOTH, expand=True)

        self.log_text.tag_config("INFO", foreground="#2563EB")
        self.log_text.tag_config("SUCCESS", foreground="#059669", font=("Consolas", 9, "bold"))
        self.log_text.tag_config("WARNING", foreground="#D97706")
        self.log_text.tag_config("ERROR", foreground="#DC2626", font=("Consolas", 9, "bold"))

    def _build_guide_tab(self, parent):
        guide_text = scrolledtext.ScrolledText(parent, wrap=tk.WORD, font=("Segoe UI", 10))
        guide_text.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)

        guide_content = """# Paratext Parallel Passage References Transfer Tool - User Guide

Welcome to the Paratext Parallel Passage References Transfer Tool! This tool is designed specifically for translation teams and layman users to easily import and synchronize parallel passage references (\r lines) in Paratext projects.

---

### Key Features at a Glance:
1. **Pre-Bundled GNTD References (Zero Setup)**:
   - Contains all 272 verified Old Testament parallel passage references from the Good News Translation (GNTD).
   - No need to use RegEx Pal or export text files manually!
2. **Resource to Project & Project to Project Support**:
   - Transfer from GNTD Resource directly into your vernacular project (e.g., IRVTam).
   - Or transfer references from another Paratext project (e.g., ABC_BQ1, MTEP1) into your target project.
3. **Automatic Vernacular Book Abbreviations**:
   - Dynamically reads your project's `BookNames.xml` file.
   - Automatically translates English or foreign book names to your local language abbreviations (e.g. "1 Chronicles" -> "1நாளா").
4. **Strict Safety & Automatic Backups**:
   - Creates timestamped backups (e.g., `01GEN...SFM.bak_20261112_143000`) before any file modification.
   - Safe **Dry-Run Preview** allows you to see all changes before anything is touched on disk!
5. **Exact Duplicate Prevention**:
   - Will never insert duplicate references if they already exist in your text.
   - Cleans up trailing periods and brackets according to Paratext standards.

---

### Step-by-Step Workflow:

#### Step 1: Select Your Paratext Directory
- The tool automatically detects your Paratext projects folder (e.g., `D:\Transdata\My Paratext 9 Projects`).
- If your projects are stored in a different location, click **Browse...** to select it.

#### Step 2: Choose Reference Source
- **Resource: GNTD**: Recommended for importing standard Old Testament parallel passages. Pre-bundled with 272 references.
- **Paratext Project**: Select this to copy references directly from an existing translated Paratext project.
- **Custom Text File**: Use this if you have an external reference list.

#### Step 3: Choose Target Project
- Select the project that will receive the references (e.g., `IRVTam`).
- The tool will check for `BookNames.xml` in that project folder and display the detected language.

#### Step 4: Configure Options
- **Put parentheses around \r references**:
  - Checked: inserts `\r (1நாளா 1:1-4)`
  - Unchecked: inserts `\r 1நாளா 1:1-4`
- **Insert '\s Insert Heading' if section heading is missing**:
  - Paratext parallel references should ideally follow a section heading. If your text is missing a heading above the target verse, this option inserts `\s Insert Heading`.
- **Clean trailing full stops/periods**:
  - Removes trailing dots from all `\r` lines (e.g. `\r 1நாளா 1:1.` -> `\r 1நாளா 1:1`).
- **Create automatic timestamped backup**:
  - Highly recommended. Saves a copy of each modified file with a timestamp.

#### Step 5: Preview and Apply
- Click ** Preview Changes (Dry Run)** to see a detailed simulation in the log window without touching any files.
- Click ** Transfer References (Apply)** when you are ready to update the project files!

---

### How Target Verses Are Calculated:
- In GNTD and standard reference tables, passage ranges denote the preceding section (e.g. `GEN 11:1-9`).
- The reference for that section is placed immediately before the **next verse** (Verse 10 = `end_verse + 1`).
- For `GEN 11:0`, `0 + 1 = Verse 1`.
- For `GEN 36:29-30`, `30 + 1 = Verse 31`.

---

### Paratext Custom Tools Integration:
To launch this tool directly from Paratext:
1. Copy `Paratext_Reference_Transfer_Tool.py` and `ParallelPassageReferencesTransfer.cms` into your `My Paratext 9 Projects\cms\` folder.
2. In Paratext, open the menu: **Tools > Custom tools > References > Transfer Parallel Passage References**.
"""
        guide_text.insert(tk.END, guide_content)
        guide_text.configure(state="disabled")

    def _import_resource_preset(self):
        f = filedialog.askopenfilename(
            title="Import Resource Reference File",
            filetypes=[("Text Files", "*.txt"), ("All Files", "*.*")]
        )
        if f:
            src = Path(f)
            dest_dir = Path(__file__).parent / "Resource_Presets"
            dest_dir.mkdir(parents=True, exist_ok=True)
            dest_file = dest_dir / src.name
            shutil.copy2(str(src), str(dest_file))
            # Refresh presets
            self.resource_presets = discover_resource_presets(Path(__file__).parent)
            self.combo_resource_preset["values"] = list(self.resource_presets.keys())
            label = f"{dest_file.stem} ({dest_file.name})"
            if label in self.resource_presets:
                self.var_resource_preset.set(label)
            self._log_msg(f"Imported resource preset: {dest_file.name}", "SUCCESS")

    def _browse_paratext_dir(self):
        folder = filedialog.askdirectory(initialdir=self.var_paratext_dir.get(), title="Select Paratext Projects Folder")
        if folder:
            self.var_paratext_dir.set(folder)
            self._refresh_projects_list()

    def _refresh_projects_list(self):
        p_dir = Path(self.var_paratext_dir.get())
        if not p_dir.exists():
            self._log_msg(f"Directory not found: {p_dir}", "WARNING")
            self.available_projects = []
            return

        # Find all project folders (ignore folders starting with _)
        projs = []
        for item in sorted(p_dir.iterdir()):
            if item.is_dir() and not item.name.startswith("_"):
                # Check if it has SFM files or BookNames.xml
                has_sfm = any(item.glob("*.SFM")) or any(item.glob("*.sfm")) or any(item.glob("*.usfm"))
                if has_sfm or (item / "BookNames.xml").exists() or (item / "Settings.xml").exists():
                    projs.append(item.name)

        self.available_projects = projs
        self.combo_source_proj["values"] = projs
        self.combo_target_proj["values"] = projs

        if projs:
            if not self.var_target_project.get() or self.var_target_project.get() not in projs:
                # Default to IRVTam if present, else first project
                if "IRVTam" in projs:
                    self.var_target_project.set("IRVTam")
                else:
                    self.var_target_project.set(projs[0])
            self._on_target_change()

        self._log_msg(f"Found {len(projs)} Paratext projects in {p_dir.name}", "INFO")

    def _on_source_change(self):
        st = self.var_source_type.get()
        if st in ("RESOURCE_PRESET", "GNTD"):
            self.combo_resource_preset.configure(state="readonly")
            self.combo_source_proj.configure(state="disabled")
            self.entry_custom.configure(state="disabled")
            self.btn_browse_custom.configure(state="disabled")
        elif st == "PROJECT":
            self.combo_resource_preset.configure(state="disabled")
            self.combo_source_proj.configure(state="readonly")
            self.entry_custom.configure(state="disabled")
            self.btn_browse_custom.configure(state="disabled")
            if not self.var_source_project.get() and self.available_projects:
                self.var_source_project.set(self.available_projects[0])
        elif st == "CUSTOM_FILE":
            self.combo_resource_preset.configure(state="disabled")
            self.combo_source_proj.configure(state="disabled")
            self.entry_custom.configure(state="normal")
            self.btn_browse_custom.configure(state="normal")

    def _on_target_change(self, event=None):
        target = self.var_target_project.get()
        if not target:
            self.lbl_target_info.configure(text="No target project selected.")
            return

        target_dir = Path(self.var_paratext_dir.get()) / target
        if target_dir.exists():
            bm = BookNamesManager(project_dir=target_dir)
            sample_abbrs = f"{bm.get_abbr('GEN')}, {bm.get_abbr('EXO')}, {bm.get_abbr('1CH')}"
            self.lbl_target_info.configure(
                text=f"✓ BookNames: {bm.loaded_source} (e.g. {sample_abbrs})",
                foreground="#196f3d"
            )
        else:
            self.lbl_target_info.configure(text="Directory does not exist", foreground="#c0392b")

    def _browse_custom_file(self):
        f = filedialog.askopenfilename(title="Select References File", filetypes=[("Text Files", "*.txt"), ("All Files", "*.*")])
        if f:
            self.var_custom_file.set(f)

    def _clear_log(self):
        self.log_text.delete("1.0", tk.END)

    def _log_msg(self, msg, level="INFO"):
        self.log_text.insert(tk.END, f"[{level}] {msg}\n", level)
        self.log_text.see(tk.END)

    def _set_progress(self, current, total, text=""):
        self.progress_bar["maximum"] = total
        self.progress_bar["value"] = current
        self.root.update_idletasks()

    def _run_worker(self, dry_run=False):
        target_name = self.var_target_project.get()
        if not target_name:
            messagebox.showerror("Error", "Please select a Target Project.")
            return

        target_dir = Path(self.var_paratext_dir.get()) / target_name
        if not target_dir.exists():
            messagebox.showerror("Error", f"Target project folder not found: {target_dir}")
            return

        source_type = self.var_source_type.get()
        if source_type in ("RESOURCE_PRESET", "GNTD"):
            source_type = "RESOURCE_PRESET"
            source_val = self.var_resource_preset.get()
        elif source_type == "PROJECT":
            src_name = self.var_source_project.get()
            if not src_name:
                messagebox.showerror("Error", "Please select a Source Project.")
                return
            source_val = str(Path(self.var_paratext_dir.get()) / src_name)
        elif source_type == "CUSTOM_FILE":
            source_val = self.var_custom_file.get()
            if not source_val or not Path(source_val).exists():
                messagebox.showerror("Error", "Please select a valid custom references file.")
                return

        scope = self.var_books_scope.get()
        single_b = self.var_single_book.get()

        options = {
            "use_parentheses": self.var_opt_parentheses.get(),
            "insert_heading": self.var_opt_heading.get(),
            "clean_punctuation": self.var_opt_clean_punct.get(),
            "create_backups": self.var_opt_backup.get(),
            "remove_existing": self.var_opt_remove_existing.get(),
        }

        self.btn_preview.configure(state="disabled")
        self.btn_apply.configure(state="disabled")

        def worker():
            try:
                execute_reference_transfer(
                    source_type=source_type,
                    source_val=source_val,
                    target_proj_dir=str(target_dir),
                    books_scope=scope,
                    selected_book=single_b,
                    options=options,
                    dry_run=dry_run,
                    log_fn=self._log_msg,
                    progress_fn=self._set_progress
                )
                if not dry_run:
                    messagebox.showinfo("Success", f"References successfully transferred to {target_name}!")
            except Exception as exc:
                self._log_msg(f"Fatal error: {exc}", "ERROR")
                messagebox.showerror("Error", str(exc))
            finally:
                self.btn_preview.configure(state="normal")
                self.btn_apply.configure(state="normal")

        threading.Thread(target=worker, daemon=True).start()

    def _cmd_preview(self):
        self._log_msg("--- STARTING DRY-RUN PREVIEW ---", "INFO")
        self._run_worker(dry_run=True)

    def _cmd_apply(self):
        target = self.var_target_project.get()
        confirm = messagebox.askyesno(
            "Confirm Transfer",
            f"Are you sure you want to transfer references into project '{target}'?\n\n"
            f"Backups will be created automatically before modifying any files."
        )
        if confirm:
            self._log_msg("--- STARTING REFERENCE TRANSFER (APPLY) ---", "INFO")
            self._run_worker(dry_run=False)


# ==============================================================================
# MAIN ENTRY POINT (PARATEXT CMS & STANDALONE SUPPORT)
# ==============================================================================

def main():
    parser = argparse.ArgumentParser(description="Paratext Parallel Passage References Transfer Tool")
    parser.add_argument("--source", choices=["RESOURCE_PRESET", "GNTD", "PROJECT", "CUSTOM_FILE"], default=None, help="Reference source type")
    parser.add_argument("--source-val", default=None, help="Source project name/path or custom file path")
    parser.add_argument("--target", default=None, help="Target project folder name or path")
    parser.add_argument("--paratext-dir", default=None, help="Path to Paratext projects directory")
    parser.add_argument("--books", choices=["OT", "NT", "ALL", "SINGLE"], default="OT", help="Book scope")
    parser.add_argument("--book", default=None, help="Single book code (e.g. GEN)")
    parser.add_argument("--no-parentheses", action="store_true", help="Do not put parentheses around \r references")
    parser.add_argument("--no-heading", action="store_true", help="Do not insert \s Insert Heading")
    parser.add_argument("--no-clean", action="store_true", help="Do not clean trailing punctuation")
    parser.add_argument("--no-backup", action="store_true", help="Do not create backup files")
    parser.add_argument("--dry-run", action="store_true", help="Preview changes without modifying files")
    parser.add_argument("--no-gui", action="store_true", help="Run without graphical interface")

    args, unknown = parser.parse_known_args()

    # Detect if invoked by Paratext CMS environment
    cms_settings_dir = globals().get("SettingsDirectory")
    cms_project = globals().get("Project")
    cms_output_project = globals().get("OutputProject")
    cms_use_parens = globals().get("UseParentheses")

    paratext_dir = args.paratext_dir or cms_settings_dir
    target_project = args.target or cms_output_project

    options = {
        "use_parentheses": not args.no_parentheses if not cms_use_parens else (str(cms_use_parens).strip().lower() in ("yes", "true", "1")),
        "insert_heading": not args.no_heading,
        "clean_punctuation": not args.no_clean,
        "create_backups": not args.no_backup,
    }

    # Headless CLI mode
    if args.no_gui or (args.source and target_project):
        st = args.source or "RESOURCE_PRESET"
        if st in ("RESOURCE_PRESET", "GNTD"):
            st = "GNTD"
        execute_reference_transfer(
            source_type=st,
            source_val=args.source_val or (str(Path(paratext_dir) / cms_project) if cms_project else None),
            target_proj_dir=str(Path(paratext_dir) / target_project) if paratext_dir and not Path(target_project).is_dir() else target_project,
            books_scope=args.books,
            selected_book=args.book,
            options=options,
            dry_run=args.dry_run,
        )
        return

    # GUI Mode
    if not TKINTER_AVAILABLE:
        print("Error: Tkinter is required for GUI mode. Use --no-gui with arguments for command-line mode.")
        sys.exit(1)

    root = tk.Tk()
    app = ParatextReferenceTransferApp(root, initial_settings_dir=paratext_dir, initial_target=target_project)
    root.mainloop()


if __name__ == "__main__":
    main()
