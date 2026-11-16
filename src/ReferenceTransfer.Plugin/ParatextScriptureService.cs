using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Reflection;
using Paratext.PluginInterfaces;
using ReferenceTransfer.Core.Engine;
using ReferenceTransfer.Core.Models;

namespace ReferenceTransfer.Plugin
{
    public class ParatextScriptureService
    {
        private readonly IPluginHost _host;

        public ParatextScriptureService(IPluginHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public List<IProject> GetAllProjects() => GetAllProjects(false);

        public List<IProject> GetAllProjects(bool includeResources)
        {
            try
            {
                var projects = _host.GetAllProjects(includeResources);
                return projects != null ? projects.ToList() : new List<IProject>();
            }
            catch
            {
                try
                {
                    var fallback = _host.GetAllProjects();
                    return fallback != null ? fallback.ToList() : new List<IProject>();
                }
                catch
                {
                    return new List<IProject>();
                }
            }
        }

        public List<IReadOnlyProject> GetAllResources()
        {
            try
            {
                var res = _host.AllResources;
                if (res != null)
                {
                    return res.Where(r => r.IsResource).ToList();
                }
            }
            catch
            {
            }
            return new List<IReadOnlyProject>();
        }

        public List<string> GetInstalledResourceIds()
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var res = _host.AllResources;
                if (res != null)
                {
                    foreach (var r in res)
                    {
                        if (!string.IsNullOrEmpty(r.ShortName))
                            ids.Add(r.ShortName);
                    }
                }
            }
            catch { }

            // Scan standard Paratext _Resources directories
            string[] candidateDirs = {
                @"D:\Transdata\My Paratext 9 Projects\_Resources",
                @"C:\My Paratext 9 Projects\_Resources",
                @"D:\Transdata\Paratext 9\Resources",
                @"C:\Program Files\Paratext 9\Resources"
            };

            foreach (var dir in candidateDirs)
            {
                if (Directory.Exists(dir))
                {
                    try
                    {
                        foreach (var file in Directory.GetFiles(dir))
                        {
                            string ext = Path.GetExtension(file).ToLowerInvariant();
                            if (ext == ".p8z" || ext == ".mbv2z" || ext == ".msv1z" || ext == ".mdv1z")
                            {
                                ids.Add(Path.GetFileNameWithoutExtension(file));
                            }
                        }
                    }
                    catch { }
                }
            }

            return ids.OrderBy(x => x).ToList();
        }

        public IReadOnlyProject? FindProjectOrResource(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || _host == null) return null;

            try
            {
                var allProjs = GetAllProjects(includeResources: true);
                var found = allProjs.FirstOrDefault(p =>
                    string.Equals(p.ShortName, id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.ID, id, StringComparison.OrdinalIgnoreCase));
                if (found != null) return found;
            }
            catch { }

            try
            {
                var allRes = _host.AllResources;
                if (allRes != null)
                {
                    foreach (var r in allRes)
                    {
                        if (string.Equals(r.ShortName, id, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(r.ID, id, StringComparison.OrdinalIgnoreCase))
                        {
                            return r;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Retrieves raw USFM text of a book directly from Paratext's internal ScrText.
        /// Bypasses DBL resource token restrictions (ValidateApiAccess) to enable live reference extraction.
        /// </summary>
        public static string? GetUsfmTextForBook(IReadOnlyProject? project, int bookNum)
        {
            if (project == null || bookNum <= 0) return null;

            // 1. Primary: Access underlying ScrText via reflection from ProjectWrapper (works for all DBL resources & projects)
            try
            {
                var type = project.GetType();
                var prop = type.GetProperty("ScrText", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                object? scrText = prop?.GetValue(project);
                if (scrText == null)
                {
                    var field = type.GetField("scrText", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    scrText = field?.GetValue(project);
                }

                if (scrText != null)
                {
                    var getTextMethod = scrText.GetType().GetMethod("GetText", new[] { typeof(int) });
                    if (getTextMethod != null)
                    {
                        string? text = getTextMethod.Invoke(scrText, new object[] { bookNum }) as string;
                        if (!string.IsNullOrEmpty(text))
                            return text;
                    }
                }
            }
            catch { }

            // 2. Secondary: Access ScrText from ParatextData ScrTextCollection.Find
            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "ParatextData");
                var stcType = asm?.GetType("Paratext.Data.ScrTextCollection");
                if (stcType != null)
                {
                    var findMethod = stcType.GetMethod("Find", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(bool), typeof(bool) }, null);
                    object? scrText = findMethod?.Invoke(null, new object[] { project.ShortName, true, true });
                    if (scrText != null)
                    {
                        var getTextMethod = scrText.GetType().GetMethod("GetText", new[] { typeof(int) });
                        string? text = getTextMethod?.Invoke(scrText, new object[] { bookNum }) as string;
                        if (!string.IsNullOrEmpty(text))
                            return text;
                    }
                }
            }
            catch { }

            // 3. Fallback for editable Paratext projects: Read disk SFM file
            try
            {
                string[] baseDirs = {
                    @"D:\Transdata\My Paratext 9 Projects",
                    @"C:\My Paratext 9 Projects"
                };
                foreach (var dir in baseDirs)
                {
                    string projDir = Path.Combine(dir, project.ShortName);
                    if (Directory.Exists(projDir))
                    {
                        string pattern = $"{bookNum:D2}*.SFM";
                        var files = Directory.GetFiles(projDir, pattern);
                        if (files.Length > 0)
                        {
                            return File.ReadAllText(files[0]);
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        public List<ParallelReferenceEntry> ExtractReferencesFromProject(IReadOnlyProject sourceProject, List<string> bookCodes)
        {
            var entries = new List<ParallelReferenceEntry>();
            if (sourceProject == null || bookCodes == null) return entries;

            foreach (string bookCode in bookCodes)
            {
                int bookNum = GetBookNum(bookCode);
                if (bookNum <= 0) continue;

                // 1. Direct USFM RegEx extraction (bypasses DBL resource token security block)
                string? usfmText = GetUsfmTextForBook(sourceProject, bookNum);
                if (!string.IsNullOrEmpty(usfmText))
                {
                    var bookEntries = ReferenceParser.ExtractReferencesFromUsfm(usfmText!, bookCode, sourceProject.ShortName);
                    entries.AddRange(bookEntries);
                    continue;
                }

                // 2. Fallback token-based extraction for editable projects if USFM text was not directly accessible
                if (!sourceProject.IsResource && sourceProject is IProject normalProject)
                {
                    int lastChap = 0;
                    try { lastChap = normalProject.Versification.GetLastChapter(bookNum); }
                    catch { continue; }

                    for (int ch = 1; ch <= lastChap; ch++)
                    {
                        IEnumerable<IUSFMToken>? tokens;
                        try { tokens = normalProject.GetUSFMTokens(bookNum, ch); }
                        catch { continue; }
                        if (tokens == null) continue;

                        int currentVerse = 0;
                        bool inRef = false;
                        var currentRef = new System.Text.StringBuilder();

                        foreach (var tok in tokens)
                        {
                            if (tok is IUSFMMarkerToken m)
                            {
                                if (m.Marker == "r")
                                {
                                    inRef = true;
                                    currentRef.Clear();
                                    if (!string.IsNullOrWhiteSpace(m.Data))
                                        currentRef.Append(m.Data.Trim());
                                }
                                else if (m.Marker == "v" && !string.IsNullOrEmpty(m.Data))
                                {
                                    inRef = false;
                                    string vStr = m.Data.Split(' ', '-')[0];
                                    int.TryParse(vStr, out currentVerse);

                                    if (currentRef.Length > 0)
                                    {
                                        string refText = currentRef.ToString().Trim();
                                        currentRef.Clear();
                                        entries.Add(new ParallelReferenceEntry
                                        {
                                            SourceType = sourceProject.ShortName,
                                            BookCode = bookCode,
                                            Chapter = ch,
                                            TargetVerse = currentVerse > 0 ? currentVerse : 1,
                                            RawReference = refText,
                                            SourceLocation = $"{bookCode} {ch}:{currentVerse}"
                                        });
                                    }
                                }
                                else
                                {
                                    inRef = false;
                                }
                            }
                            else if (tok is IUSFMTextToken t)
                            {
                                if (inRef && !string.IsNullOrEmpty(t.Text))
                                {
                                    if (currentRef.Length > 0 && !currentRef.ToString().EndsWith(" ") && !t.Text.StartsWith(" "))
                                    {
                                        currentRef.Append(" ");
                                    }
                                    currentRef.Append(t.Text);
                                }
                            }
                        }

                        if (currentRef.Length > 0)
                        {
                            string refText = currentRef.ToString().Trim();
                            entries.Add(new ParallelReferenceEntry
                            {
                                SourceType = sourceProject.ShortName,
                                BookCode = bookCode,
                                Chapter = ch,
                                TargetVerse = currentVerse > 0 ? currentVerse : 1,
                                RawReference = refText,
                                SourceLocation = $"{bookCode} {ch}:{currentVerse}"
                            });
                        }
                    }
                }
            }
            return entries;
        }

        public VernacularBookNameManager LoadVernacularManager(string id)
        {
            var mgr = new VernacularBookNameManager();
            if (string.IsNullOrWhiteSpace(id)) return mgr;

            try
            {
                string[] baseDirs = {
                    @"D:\Transdata\My Paratext 9 Projects",
                    @"C:\My Paratext 9 Projects"
                };

                foreach (var dir in baseDirs)
                {
                    string candidate = Path.Combine(dir, id, "BookNames.xml");
                    if (File.Exists(candidate))
                    {
                        mgr.LoadFromXml(candidate);
                        if (mgr.IsLoaded) return mgr;
                    }
                }
            }
            catch { }

            return mgr;
        }

        public VernacularBookNameManager LoadVernacularManager(IReadOnlyProject? project)
        {
            if (project == null) return new VernacularBookNameManager();

            // 1. Try disk-based BookNames.xml if available (for projects)
            var mgr = LoadVernacularManager(project.ShortName);
            if (mgr.IsLoaded) return mgr;

            // 2. Read BookNames directly from project's underlying ScrText
            try
            {
                var type = project.GetType();
                var prop = type.GetProperty("ScrText", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                object? scrText = prop?.GetValue(project);
                if (scrText == null)
                {
                    var field = type.GetField("scrText", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    scrText = field?.GetValue(project);
                }

                if (scrText == null)
                {
                    var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "ParatextData");
                    var stcType = asm?.GetType("Paratext.Data.ScrTextCollection");
                    var findMethod = stcType?.GetMethod("Find", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(bool), typeof(bool) }, null);
                    scrText = findMethod?.Invoke(null, new object[] { project.ShortName, true, true });
                }

                if (scrText != null)
                {
                    var bnProp = scrText.GetType().GetProperty("BookNames", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    object? bookNames = bnProp?.GetValue(scrText);
                    if (bookNames != null)
                    {
                        var getItem = bookNames.GetType().GetMethod("get_Item", new[] { typeof(int) });
                        var getAbbr = bookNames.GetType().GetMethod("GetAbbreviation", new[] { typeof(int), typeof(bool) });
                        var getShort = bookNames.GetType().GetMethod("GetShortName", new[] { typeof(int) });
                        var getLong = bookNames.GetType().GetMethod("GetLongName", new[] { typeof(int) });

                        for (int b = 1; b <= 66; b++)
                        {
                            string code = CanonicalBookLists.All66Books[b - 1];
                            string? abbr = null;
                            string? shortName = null;
                            string? longName = null;

                            if (getItem != null)
                            {
                                try
                                {
                                    object? info = getItem.Invoke(bookNames, new object[] { b });
                                    if (info != null)
                                    {
                                        var infoType = info.GetType();
                                        code = (string?)infoType.GetProperty("BookCode")?.GetValue(info) ?? code;
                                        abbr = (string?)infoType.GetProperty("Abbreviation")?.GetValue(info);
                                        shortName = (string?)infoType.GetProperty("ShortName")?.GetValue(info);
                                        longName = (string?)infoType.GetProperty("LongName")?.GetValue(info);

                                        string? abbrLwc = (string?)infoType.GetProperty("AbbreviationLWC")?.GetValue(info);
                                        string? shortLwc = (string?)infoType.GetProperty("ShortNameLWC")?.GetValue(info);

                                        if (!string.IsNullOrEmpty(abbrLwc)) mgr.RegisterBookAlias(code, abbrLwc);
                                        if (!string.IsNullOrEmpty(shortLwc)) mgr.RegisterBookAlias(code, shortLwc);
                                    }
                                }
                                catch { }
                            }

                            if (string.IsNullOrEmpty(abbr) && getAbbr != null)
                            {
                                try { abbr = (string?)getAbbr.Invoke(bookNames, new object[] { b, false }); } catch { }
                            }
                            if (string.IsNullOrEmpty(shortName) && getShort != null)
                            {
                                try { shortName = (string?)getShort.Invoke(bookNames, new object[] { b }); } catch { }
                            }
                            if (string.IsNullOrEmpty(longName) && getLong != null)
                            {
                                try { longName = (string?)getLong.Invoke(bookNames, new object[] { b }); } catch { }
                            }

                            if (!string.IsNullOrEmpty(abbr) || !string.IsNullOrEmpty(shortName) || !string.IsNullOrEmpty(longName))
                            {
                                mgr.RegisterBook(code, abbr ?? string.Empty, shortName ?? string.Empty, longName ?? string.Empty);
                            }
                        }

                        if (mgr.IsLoaded) return mgr;
                    }
                }
            }
            catch { }

            // 3. Query Paratext's live IProject API (GetFormattedReference) for all books (works for resources & projects)
            try
            {
                if (project is IProject p && p.Versification != null)
                {
                    for (int b = 1; b <= 66; b++)
                    {
                        string code = CanonicalBookLists.All66Books[b - 1];
                        try
                        {
                            var vRef = p.Versification.CreateReference(b, 1, 1);
                            if (vRef != null)
                            {
                                string abbr = CleanFormattedBookName(p.GetFormattedReference(vRef, BookNameType.Abbreviation));
                                string shortName = CleanFormattedBookName(p.GetFormattedReference(vRef, BookNameType.ShortName));
                                string longName = CleanFormattedBookName(p.GetFormattedReference(vRef, BookNameType.LongName));
                                string parallelForm = CleanFormattedBookName(p.GetFormattedReference(vRef, BookNameType.ParallelPassagesForm));

                                if (!string.IsNullOrEmpty(abbr) || !string.IsNullOrEmpty(shortName) || !string.IsNullOrEmpty(longName))
                                {
                                    mgr.RegisterBook(code, abbr, shortName, longName);
                                    if (!string.IsNullOrEmpty(parallelForm) && !string.Equals(parallelForm, abbr, StringComparison.OrdinalIgnoreCase))
                                    {
                                        mgr.RegisterBookAlias(code, parallelForm);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return mgr;
        }

        private static string CleanFormattedBookName(string? formattedRef)
        {
            if (string.IsNullOrWhiteSpace(formattedRef)) return string.Empty;
            string s = formattedRef.Trim();
            // Remove chapter:verse suffix like " 1:1" or " 1.1"
            s = Regex.Replace(s, @"\s+\d+[:.]\d+$", string.Empty);
            return s.Trim();
        }

        public ProjectScriptureSettings LoadProjectScriptureSettings(string id)
        {
            var settings = new ProjectScriptureSettings();
            if (string.IsNullOrWhiteSpace(id)) return settings;

            try
            {
                string[] baseDirs = {
                    @"D:\Transdata\My Paratext 9 Projects",
                    @"C:\My Paratext 9 Projects"
                };

                foreach (var dir in baseDirs)
                {
                    string candidate = Path.Combine(dir, id, "Settings.xml");
                    if (File.Exists(candidate))
                    {
                        return ProjectScriptureSettings.LoadFromSettingsXml(candidate);
                    }
                }
            }
            catch { }

            return settings;
        }

        public ProjectScriptureSettings LoadProjectScriptureSettings(IReadOnlyProject? project)
        {
            if (project == null) return new ProjectScriptureSettings();

            // 1. Try disk-based Settings.xml
            var settings = LoadProjectScriptureSettings(project.ShortName);
            if (settings.IsLoaded) return settings;

            // 2. Query scrText.Settings via reflection
            try
            {
                var type = project.GetType();
                var prop = type.GetProperty("ScrText", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                object? scrText = prop?.GetValue(project);
                if (scrText == null)
                {
                    var field = type.GetField("scrText", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    scrText = field?.GetValue(project);
                }

                if (scrText == null)
                {
                    var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "ParatextData");
                    var stcType = asm?.GetType("Paratext.Data.ScrTextCollection");
                    var findMethod = stcType?.GetMethod("Find", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(bool), typeof(bool) }, null);
                    scrText = findMethod?.Invoke(null, new object[] { project.ShortName, true, true });
                }

                if (scrText != null)
                {
                    var setProp = scrText.GetType().GetProperty("Settings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    object? ps = setProp?.GetValue(scrText);
                    if (ps != null)
                    {
                        var psType = ps.GetType();
                        string? cv = psType.GetProperty("ChapterVerseSeparator")?.GetValue(ps) as string;
                        string? vr = psType.GetProperty("VerseRangeSeparator")?.GetValue(ps) as string;
                        string? vs = psType.GetProperty("VerseSequenceSeparator")?.GetValue(ps) as string;
                        string? bs = psType.GetProperty("BookSequenceSeparator")?.GetValue(ps) as string;
                        string? cr = psType.GetProperty("ChapterRangeSeparator")?.GetValue(ps) as string;

                        return new ProjectScriptureSettings
                        {
                            ChapterVerseSeparator = !string.IsNullOrEmpty(cv) ? cv! : ":",
                            RangeIndicator = !string.IsNullOrEmpty(vr) ? vr! : "-",
                            SequenceIndicator = !string.IsNullOrEmpty(vs) ? vs! : ",",
                            BookSequenceSeparator = !string.IsNullOrEmpty(bs) ? bs! : "; ",
                            ChapterRangeSeparator = !string.IsNullOrEmpty(cr) ? cr! : "-",
                            IsLoaded = true
                        };
                    }
                }
            }
            catch { }

            return settings;
        }

        public string GetBookCode(int bookNum)
        {
            if (bookNum >= 1 && bookNum <= CanonicalBookLists.All66Books.Count)
            {
                return CanonicalBookLists.All66Books[bookNum - 1];
            }
            return $"BK{bookNum}";
        }

        public int GetBookNum(string bookCode)
        {
            int idx = CanonicalBookLists.All66Books.FindIndex(c => string.Equals(c, bookCode, StringComparison.OrdinalIgnoreCase));
            return idx >= 0 ? idx + 1 : 0;
        }

        public List<ReferenceTransferProposal> ScanBook(
            IProject project,
            int bookNum,
            List<ParallelReferenceEntry> sourceReferences,
            VernacularBookNameManager vernacularMgr,
            TransferOptions options,
            VernacularBookNameManager? sourceVernacularMgr = null,
            Action<int, int>? progressCallback = null)
        {
            var proposals = new List<ReferenceTransferProposal>();
            if (project == null || sourceReferences == null || sourceReferences.Count == 0)
                return proposals;

            string bookCode = GetBookCode(bookNum);
            var engine = new ReferenceTransferEngine(vernacularMgr, options, sourceVernacularMgr);

            // Primary: Fetch the full authentic USFM text directly from project/ScrText (contains all \s, \r, \ms, \p)
            string? fullUsfm = GetUsfmTextForBook(project, bookNum);

            if (string.IsNullOrEmpty(fullUsfm))
            {
                int lastChapter = project.Versification.GetLastChapter(bookNum);
                var sbUsfm = new StringBuilder();

                for (int chapter = 1; chapter <= lastChapter; chapter++)
                {
                    progressCallback?.Invoke(chapter, lastChapter);

                    IEnumerable<IUSFMToken> tokenEnum;
                    try
                    {
                        tokenEnum = project.GetUSFMTokens(bookNum, chapter);
                    }
                    catch
                    {
                        continue;
                    }

                    if (tokenEnum == null) continue;

                    sbUsfm.AppendLine($"\\c {chapter}");
                    foreach (var token in tokenEnum)
                    {
                        if (token is IUSFMMarkerToken markerToken)
                        {
                            sbUsfm.AppendLine($"\\{markerToken.Marker} {markerToken.Data}");
                        }
                        else if (token is IUSFMTextToken textToken)
                        {
                            sbUsfm.AppendLine(textToken.Text);
                        }
                    }
                }

                fullUsfm = sbUsfm.ToString();
            }

            return engine.GenerateProposals(bookCode, fullUsfm, sourceReferences);
        }

        public bool ApplyApprovedReferences(
            IProject project,
            IPluginObject pluginObj,
            int bookNum,
            List<ReferenceTransferProposal> approvedProposals,
            VernacularBookNameManager vernacularMgr,
            TransferOptions options,
            out string statusMessage)
        {
            statusMessage = string.Empty;
            if (project == null)
            {
                statusMessage = "No project selected.";
                return false;
            }

            var toApply = approvedProposals.Where(p => p.IsAccepted && !p.IsDuplicate && !p.HasWarning).ToList();
            if (toApply.Count == 0)
            {
                statusMessage = "No approved references to apply.";
                return true;
            }

            // 1. Permission check
            if (!project.CanEdit(pluginObj, bookNum, 1))
            {
                statusMessage = $"User does not have write permissions for book {GetBookCode(bookNum)}.";
                return false;
            }

            // Group proposals by chapter
            var byChapter = toApply.GroupBy(p => p.Chapter);
            int appliedCount = 0;

            foreach (var chapGroup in byChapter)
            {
                int chapterNum = chapGroup.Key;
                IWriteLock? writeLock = null;

                try
                {
                    writeLock = project.RequestWriteLock(pluginObj, onRelease => { }, bookNum, chapterNum);
                    if (writeLock == null) continue;

                    var tokens = project.GetUSFMTokens(bookNum, chapterNum).ToList();
                    bool modified = false;

                    // Sort descending by verse to keep earlier token indices stable during insertions
                    var chapProposals = chapGroup.OrderByDescending(p => p.Verse).ToList();

                    foreach (var prop in chapProposals)
                    {
                        int targetVerse = prop.Verse;
                        int verseIdx = -1;

                        for (int k = 0; k < tokens.Count; k++)
                        {
                            if (tokens[k].VerseRef != null && tokens[k].VerseRef.VerseNum == targetVerse)
                            {
                                verseIdx = k;
                                break;
                            }
                            if (tokens[k] is IUSFMMarkerToken m && m.Marker == "v" && m.Data != null && m.Data.StartsWith(targetVerse.ToString()))
                            {
                                verseIdx = k;
                                break;
                            }
                        }

                        if (verseIdx < 0) continue;

                        // Step 1: Scan backwards from verseIdx to find introducing paragraph/block marker (\p, \q, etc.)
                        int paraIdx = -1;
                        for (int look = verseIdx - 1; look >= 0; look--)
                        {
                            if (tokens[look] is IUSFMMarkerToken m)
                            {
                                if (UsfmMarkerCategories.IsParagraphOrBlock(m.Marker))
                                {
                                    paraIdx = look;
                                    break;
                                }
                                if (m.Marker == "v" || m.Marker == "c" || UsfmMarkerCategories.IsHeading(m.Marker))
                                {
                                    break;
                                }
                            }
                        }

                        // Insertion point MUST be ABOVE \p or \q if present, or ABOVE \v
                        int insertPoint = (paraIdx >= 0) ? paraIdx : verseIdx;

                        // Step 2: Check if there is an existing heading (\s) above the insertion point
                        bool hasHeading = false;
                        int existingRIdx = -1;

                        for (int look = insertPoint - 1; look >= 0; look--)
                        {
                            if (tokens[look] is IUSFMMarkerToken m)
                            {
                                if (m.Marker == "r")
                                {
                                    existingRIdx = look;
                                }
                                else if (UsfmMarkerCategories.IsHeading(m.Marker))
                                {
                                    hasHeading = true;
                                    break;
                                }
                                else if (m.Marker == "v" || m.Marker == "c" || UsfmMarkerCategories.IsParagraphOrBlock(m.Marker))
                                {
                                    break;
                                }
                            }
                        }

                        // Step 3: Remove existing \r if requested
                        if (existingRIdx >= 0 && options.RemoveExistingReferences)
                        {
                            int removeCount = 1;
                            if (existingRIdx + 1 < tokens.Count && tokens[existingRIdx + 1] is IUSFMTextToken)
                            {
                                removeCount = 2;
                            }
                            tokens.RemoveRange(existingRIdx, removeCount);
                            if (existingRIdx < insertPoint)
                            {
                                insertPoint -= removeCount;
                            }
                        }

                        // Step 4: Build insertion tokens
                        var insertTokens = new List<IUSFMToken>();

                        // If no heading exists and user requested inserting missing heading, insert \s Insert Heading first
                        if (!hasHeading && options.InsertHeadingIfMissing)
                        {
                            var hTokens = project.ConvertToUSFMTokens("\\s Insert Heading", bookNum, chapterNum, targetVerse);
                            if (hTokens != null) insertTokens.AddRange(hTokens);
                        }

                        // Insert \r {citation}
                        string rLine = $"\\r {prop.ProposedReference}";
                        var rTokens = project.ConvertToUSFMTokens(rLine, bookNum, chapterNum, targetVerse);
                        if (rTokens != null) insertTokens.AddRange(rTokens);

                        // Step 5: Insert tokens ABOVE the paragraph/verse boundary
                        if (insertTokens.Count > 0)
                        {
                            tokens.InsertRange(insertPoint, insertTokens);
                            modified = true;
                            appliedCount++;
                        }
                    }

                    if (modified)
                    {
                        project.PutUSFMTokens(writeLock, tokens, bookNum);
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        if (pluginObj != null)
                        {
                            _host.Log(pluginObj, "Error writing references to chapter {0}: {1}", new object[] { chapterNum, ex.Message });
                        }
                    }
                    catch { }
                }
                finally
                {
                    writeLock?.Dispose();
                }
            }

            statusMessage = $"Successfully transferred {appliedCount} parallel references to {GetBookCode(bookNum)}.";
            return true;
        }
    }
}
