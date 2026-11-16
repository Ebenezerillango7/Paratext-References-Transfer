using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        public List<IProject> GetAllProjects()
        {
            try
            {
                var projects = _host.GetAllProjects();
                return projects != null ? projects.ToList() : new List<IProject>();
            }
            catch
            {
                return new List<IProject>();
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
                    string? pendingRef = null;

                    foreach (var tok in tokens)
                    {
                        if (tok is IUSFMMarkerToken m)
                        {
                            if (m.Marker == "v" && !string.IsNullOrEmpty(m.Data))
                            {
                                string vStr = m.Data.Split(' ', '-')[0];
                                int.TryParse(vStr, out currentVerse);

                                if (pendingRef != null)
                                {
                                    entries.Add(new ParallelReferenceEntry
                                    {
                                        SourceType = sourceProject.ShortName,
                                        BookCode = bookCode,
                                        Chapter = ch,
                                        TargetVerse = currentVerse > 0 ? currentVerse : 1,
                                        RawReference = pendingRef,
                                        SourceLocation = $"{bookCode} {ch}:{currentVerse}"
                                    });
                                    pendingRef = null;
                                }
                            }
                            else if (m.Marker == "r" && !string.IsNullOrEmpty(m.Data))
                            {
                                pendingRef = m.Data.Trim();
                            }
                        }
                        else if (tok is IUSFMTextToken t && pendingRef != null)
                        {
                            pendingRef += " " + t.Text.Trim();
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

        public VernacularBookNameManager LoadVernacularManager(IProject project)
        {
            return LoadVernacularManager(project?.ShortName ?? string.Empty);
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
