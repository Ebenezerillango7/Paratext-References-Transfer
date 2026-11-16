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

        public IProject? FindProjectOrResource(string id)
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
                            if (r is IProject proj) return proj;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        public List<ParallelReferenceEntry> ExtractReferencesFromProject(IProject sourceProject, List<string> bookCodes)
        {
            var entries = new List<ParallelReferenceEntry>();
            if (sourceProject == null || bookCodes == null) return entries;

            foreach (string bookCode in bookCodes)
            {
                int bookNum = GetBookNum(bookCode);
                if (bookNum <= 0) continue;

                int lastChap = 0;
                try { lastChap = sourceProject.Versification.GetLastChapter(bookNum); }
                catch { continue; }

                for (int ch = 1; ch <= lastChap; ch++)
                {
                    IEnumerable<IUSFMToken>? tokens;
                    try { tokens = sourceProject.GetUSFMTokens(bookNum, ch); }
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
                                // Other markers (\s, \p, \m, \q, \c) close text collection for \r,
                                // but do not discard currentRef because paragraph markers (\p, \m)
                                // often sit between \r and \v.
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

        public VernacularBookNameManager LoadVernacularManager(IProject? project)
        {
            if (project == null) return new VernacularBookNameManager();

            // 1. Try disk-based BookNames.xml if available (for projects)
            var mgr = LoadVernacularManager(project.ShortName);
            if (mgr.IsLoaded) return mgr;

            // 2. Query Paratext's live IProject API (GetFormattedReference) for all books (works for resources & projects)
            try
            {
                if (project.Versification != null)
                {
                    for (int b = 1; b <= 66; b++)
                    {
                        string code = CanonicalBookLists.All66Books[b - 1];
                        try
                        {
                            var vRef = project.Versification.CreateReference(b, 1, 1);
                            if (vRef != null)
                            {
                                string abbr = CleanFormattedBookName(project.GetFormattedReference(vRef, BookNameType.Abbreviation));
                                string shortName = CleanFormattedBookName(project.GetFormattedReference(vRef, BookNameType.ShortName));
                                string longName = CleanFormattedBookName(project.GetFormattedReference(vRef, BookNameType.LongName));
                                string parallelForm = CleanFormattedBookName(project.GetFormattedReference(vRef, BookNameType.ParallelPassagesForm));

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

        public ProjectScriptureSettings LoadProjectScriptureSettings(IProject project)
        {
            return LoadProjectScriptureSettings(project?.ShortName ?? string.Empty);
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

            string fullUsfm = sbUsfm.ToString();
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
