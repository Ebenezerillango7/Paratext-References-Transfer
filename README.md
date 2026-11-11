# IRV Tamil GNTD Parallel Reference Importer

A standalone Python/Tkinter application that imports the supplied GNTD parallel passage references into IRV Tamil SFM/USFM files for GEN–MAL. It uses only the Python standard library.

## Run on Windows

1. Install Python 3.7 or later from python.org.
2. Keep `IRVTam_Reference_Separator.py`, `IRVTamBooknames.xml`, and `GNTD References.txt` together in one folder.
3. Open PowerShell in that folder and run:

   ```powershell
   python IRVTam_Reference_Separator.py
   ```

4. Confirm the Book names XML and GNTD reference file paths. Choose the folder containing your SFM/USFM files.
5. Select books, click **Preview GNTD import**, inspect or export the report, then tick **Apply changes** and click **Import / Apply** to write changes.
6. Backups are enabled by default and are created beside modified files. The app identifies each book from its `\\id` marker.

## Import behavior

- Reads GNTD rows such as `GEN 11:9: \\r (1 Chronicles 1.24-27)` and anchors each insertion point to the following verse (`location + 1`, e.g. `GEN 11:9` inserts at verse 10; chapter start `:0` inserts at verse 1).
- Inserts a `\\s Insert Heading` line only if an `\\s` heading is not already available for that section.
- Ensures a `\\p` marker directly precedes the target verse. Adds it when missing.
- Removes all opening and closing brackets from `\\r` references and removes any trailing full stop or punctuation.
- Converts English source book names to the exact `abbr` attribute from `IRVTamBooknames.xml`; chapter/verse separators are written with colons.
- Leaves an exact existing reference alone. If the existing reference differs, preserves it and adds the GNTD reference below it.
- Does not modify unselected books. Preview never writes to source files. Applying changes makes timestamped backups by default.
- Retains UTF-8 BOM state and original newline style when writing.

The supplied XML uses `<book code="..." abbr="..." short="..." long="..." />`; `abbr` is the output TOC3 abbreviation. The app’s English-name map is used only to interpret GNTD’s English source names.

## Tests and supplied data

Run the regression suite from the project folder:

```powershell
python -m unittest -v test_gntd_import.py
```

`Pre-Requisites` contains the original GNTD file and all 39 original sample SFM files. `GNTD Import Preview.txt` lists each proposed insertion and exact match. `Imported SFM` contains processed copies of the 39 samples; the original samples remain unchanged. Tests and preview do not modify the supplied SFM files.
