# Parallel References Transfer - Paratext 9.5 Native Plugin Architecture

**Target Platform**: Paratext 9.5.110.11 (.NET Framework 4.8 / Windows x64)  
**Target Architecture**: Native Standalone Plugin (`.ptxplg`)  
**Plugin Identifier**: `ReferenceTransfer`  

---

## 1. Executive Overview

The **Parallel References Transfer** plugin is an enterprise-grade scripture editing tool engineered directly for **Paratext 9.5**. It automates the extraction, translation, and insertion of parallel passage references (`\r`) into target Bible translation projects while guaranteeing complete structural immunity and translator safety.

### Key Capabilities:
1. **Multi-Source Flexibility**:
   - Built-in Presets (Standard Bible Parallels 600 refs, Gospels Synoptic 328 refs, GNTD OT 272 refs, HINOVBSI, ESVUS16, IRVHin).
   - Project-to-Project Extraction (Directly reads references from another Paratext project).
   - Custom Files (Plain text or SFM extracts).
2. **Vernacular Translation via BookNames.xml**:
   - Translates English canonical book citations into the target project's vernacular abbreviations (e.g. `(1 Chronicles 1.1-4)` $\rightarrow$ `(1நாளா 1:1-4)`).
   - Standardizes verse notation (periods to colons: `1.1-4` $\rightarrow$ `1:1-4`).
3. **Translator Review & Agency**:
   - Interactive review DataGridView with bilingual headers.
   - Verse-by-verse diff comparison with accept/reject checkboxes.
   - Amber warnings for missing verses or duplicate citations.
4. **Paratext History & Safety**:
   - Automatically creates Paratext History / Undo points prior to writing.
   - Transactional writing via `IWriteLock`.
   - Never modifies `My Paratext 9 Projects` on disk directly.
5. **Turnkey Installation & Uninstallation**:
   - 1-Click `install-plugin.bat` with automatic `Zone.Identifier` unblocking.
   - In-app "Uninstall Plugin..." button with detached background worker.

---

## 2. Solution Structure

```
ReferenceTransfer/
├── src/
│   ├── ReferenceTransfer.Core/     # Multi-targeted (netstandard2.0; net48)
│   │   ├── Models/
│   │   │   ├── ParallelReferenceEntry.cs
│   │   │   ├── ReferenceTransferProposal.cs
│   │   │   ├── BookScope.cs
│   │   │   └── TransferOptions.cs
│   │   ├── Engine/
│   │   │   ├── ReferenceParser.cs
│   │   │   ├── VernacularBookNameManager.cs
│   │   │   ├── ReferenceTransferEngine.cs
│   │   │   └── PresetsCatalog.cs
│   │   └── Presets/                # Embedded resource reference databases
│   └── ReferenceTransfer.Plugin/   # net48 (outputs ReferenceTransfer.ptxplg)
│       ├── ReferenceTransferPlugin.cs (IParatextStandalonePlugin)
│       ├── ParatextScriptureService.cs
│       ├── PluginUninstaller.cs
│       └── UI/
│           └── TransferMainWindow.cs (WinForms Interactive Review UI)
├── tests/
│   └── ReferenceTransfer.Tests/    # net6.0 xUnit tests
├── scripts/
│   ├── install-plugin.bat          # 1-Click installer wrapper
│   ├── install-plugin.ps1          # Unblocks files, detects process, registers plugin
│   ├── uninstall-plugin.bat        # 1-Click uninstaller
│   ├── uninstall-plugin.ps1        # Background cleaner
│   └── package-release.ps1         # Release zip packager
└── dist/
    └── ReferenceTransfer-Paratext9-Plugin-v2.0.0.zip
```
