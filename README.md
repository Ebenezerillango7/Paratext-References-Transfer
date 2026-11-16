# Paratext Parallel References Transfer Plugin (v2.0.0)

A high-performance, native **Paratext 9.5+ Standalone Plugin** (`ReferenceTransfer.ptxplg`) built with **C# / .NET Framework 4.8** and the official **ParatextData API**. 

It enables translation teams and consultants to seamlessly transfer parallel passage references (`\r`) from any installed Paratext text project, installed DBL resource, or bundled DBL reference catalog directly into target translation projects with automated vernacular book name localization, project-specific punctuation/separator adherence, and strict USFM hierarchy compliance.

---

## 🌟 Key Features

1. **Paratext 9.5 Native Integration**:
   - Compiles to a self-contained `.ptxplg` plugin bundle.
   - Runs directly inside Paratext under the **Tools / Plugins** menu (`Parallel Passage References Transfer`).
   - Deep integration with `Paratext.Data.Project` and `ParatextData.dll` transaction management.

2. **Strict USFM Hierarchy Enforcement**:
   - `\r` (Parallel Reference) is strictly placed **above** paragraph/block markers (`\p`, `\m`, `\q`, `\li`, `\nb`, `\b`) and **below** section headings (`\s`, `\ms`).
   - Automatically detects existing section headings and attaches references directly beneath them without inserting duplicate `\s Insert Heading` markers.
   - Modifies verses in reverse sequential order to guarantee token offset stability.

3. **Dynamic Scripture Reference Settings Adherence**:
   - Automatically reads the target project's `Settings.xml` (`<ChapterVerseSeparator>`, `<RangeIndicator>`, `<SequenceIndicator>`, `<BookSequenceSeparator>`, etc.).
   - Dynamically adapts references to project standards (e.g. `.` vs `:` for chapter/verse separators), avoiding Paratext Scripture Reference check errors.

4. **Vernacular Book Name Localization**:
   - Automatically parses the target project's `BookNames.xml` (supports up to 123 biblical and deuterocanonical books).
   - Allows users to choose between **Abbreviation** (TOC3), **Short Name** (TOC2), or **Long Name** (TOC1) on the fly with live preview.

5. **Resource Catalog & Installed Paratext Projects**:
   - Dynamically discovers all installed Paratext projects and standard DBL resources (e.g., `ESVUS16`, `GNTD`, `IRVHIN`, `TAOVBSI`).
   - Also supports custom tab-delimited or SFM-formatted reference text files.

6. **Native Atomic History / Rollback**:
   - Executes all modifications within Paratext's native project write lock.
   - Paratext automatically records a single, clean atomic History Point across all modified books (no per-book history clutter).

7. **Global English UI**:
   - Fully internationalized, intuitive Windows Forms interface suitable for translation teams worldwide.
   - Provides full pre-transfer inspection grid with verse-by-verse selection and diff preview.

---

## 🚀 Quick Start / Installation

### Automated Installation
To install into your local Paratext 9.5 installation:
1. Ensure Paratext is closed.
2. Run `scripts/install-plugin.bat` (or `scripts/install-plugin.ps1` from PowerShell).
3. Start Paratext 9.5. The plugin appears under **Tools > Parallel Passage References Transfer**.

### Automated Uninstallation
To remove the plugin:
1. Ensure Paratext is closed.
2. Run `scripts/uninstall-plugin.bat` (or click **Uninstall Plugin...** in the plugin window header).

---

## 🛠️ Building from Source

### Prerequisites
- Windows 10/11
- .NET Framework 4.8 Developer Pack
- MSBuild (Visual Studio 2019/2022 or Build Tools)
- Paratext 9.5 installed in `C:\Program Files\Paratext 9`

### Build & Package
Open PowerShell and run:
```powershell
# Restore & build Release configuration
msbuild ReferenceTransfer.sln /p:Configuration=Release /t:Rebuild

# Package distributable .ptxplg release
.\scripts\package-release.ps1
```
The output plugin package will be generated at `dist/ReferenceTransfer/ReferenceTransfer.ptxplg` and packaged into `dist/ReferenceTransfer-Paratext9-Plugin-v2.0.0.zip`.

---

## 🧪 Testing

Run the automated NUnit test suite verifying reference parsing, vernacular translation, and USFM token placement:
```powershell
dotnet test tests/ReferenceTransfer.Tests/ReferenceTransfer.Tests.csproj
```

---

## 📁 Repository Structure

```
├── ReferenceTransfer.sln            # Visual Studio Solution (.NET 4.8)
├── NuGet.Config                     # NuGet configuration
├── docs/
│   └── ARCHITECTURE.md              # Technical architecture & USFM specification
├── scripts/
│   ├── install-plugin.bat           # One-click Windows installer
│   ├── install-plugin.ps1           # PowerShell installer script
│   ├── uninstall-plugin.bat         # One-click Windows uninstaller
│   ├── uninstall-plugin.ps1         # PowerShell uninstaller script
│   └── package-release.ps1          # Distributable packaging script
├── src/
│   ├── ReferenceTransfer.Core/      # Platform-agnostic transfer & USFM engine
│   │   ├── Engine/                  # Parser, Catalog, USFM Token Placement, Vernacular Map
│   │   ├── Models/                  # Data structures & ProjectScriptureSettings
│   │   └── ResourceData/            # Embedded DBL parallel reference catalogs
│   └── ReferenceTransfer.Plugin/    # Paratext 9.5 Standalone Plugin
│       ├── ParatextScriptureService # ParatextData API integration
│       ├── ReferenceTransferPlugin  # IParatextStandalonePlugin implementation
│       └── UI/                      # Global WinForms UI with live Preview DataGridView
└── tests/
    └── ReferenceTransfer.Tests/     # Unit & integration test suite
```

---

## 📄 License
This project is licensed under the MIT License - see the LICENSE file for details.
