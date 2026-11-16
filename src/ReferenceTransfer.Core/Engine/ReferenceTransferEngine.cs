using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ReferenceTransfer.Core.Models;

namespace ReferenceTransfer.Core.Engine
{
    /// <summary>
    /// Core transfer engine for matching, filtering, and inserting parallel references into USFM text.
    /// Operates with strict USFM structural awareness and zero cross-verse contamination.
    /// </summary>
    public class ReferenceTransferEngine
    {
        private readonly VernacularBookNameManager _vernacularMgr;
        private readonly VernacularBookNameManager? _sourceVernacularMgr;
        private readonly TransferOptions _options;

        public ReferenceTransferEngine(
            VernacularBookNameManager vernacularMgr, 
            TransferOptions? options = null,
            VernacularBookNameManager? sourceVernacularMgr = null)
        {
            _vernacularMgr = vernacularMgr ?? new VernacularBookNameManager();
            _options = options ?? new TransferOptions();
            _sourceVernacularMgr = sourceVernacularMgr;
        }

        /// <summary>
        /// Scans target USFM text against a list of references and creates review proposals.
        /// </summary>
        public List<ReferenceTransferProposal> GenerateProposals(
            string bookCode,
            string usfmText,
            IEnumerable<ParallelReferenceEntry> sourceReferences)
        {
            var proposals = new List<ReferenceTransferProposal>();
            if (string.IsNullOrEmpty(usfmText) || sourceReferences == null)
                return proposals;

            // Filter references matching this book
            var bookRefs = sourceReferences
                .Where(r => string.Equals(r.BookCode, bookCode, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (bookRefs.Count == 0)
                return proposals;

            // Parse chapters and verses available in the USFM text
            var availableVerses = ScanUsfmStructure(usfmText);

            foreach (var r in bookRefs)
            {
                var proposal = new ReferenceTransferProposal
                {
                    BookCode = bookCode.ToUpperInvariant(),
                    Chapter = r.Chapter,
                    Verse = r.TargetVerse,
                    SourceReference = r.RawReference,
                    CanonicalReference = _vernacularMgr.ToCanonicalCitation(r.RawReference, _sourceVernacularMgr),
                    ProposedReference = _vernacularMgr.TranslateCitation(
                        r.RawReference, 
                        _options.UseParentheses, 
                        _options.CleanTrailingPunctuation, 
                        _options.NameStyle, 
                        _options.ScriptureSettings,
                        _sourceVernacularMgr)
                };

                // Check if target project had blank scripture settings for any book in this reference (English fallback used)
                if (!string.IsNullOrEmpty(proposal.CanonicalReference))
                {
                    var bookMatches = Regex.Matches(proposal.CanonicalReference, @"\b([1-3]?[A-Z]{3})\b");
                    foreach (Match m in bookMatches)
                    {
                        string cCode = m.Groups[1].Value;
                        if (!_vernacularMgr.HasVernacularMapping(cCode))
                        {
                            proposal.IsEnglishFallback = true;
                            break;
                        }
                    }
                }

                // Check chapter existence
                if (!availableVerses.ContainsKey(r.Chapter))
                {
                    proposal.Warning = $"Chapter {r.Chapter} not found in target text.";
                    proposal.IsAccepted = false;
                    proposals.Add(proposal);
                    continue;
                }

                var chapVerses = availableVerses[r.Chapter];
                if (!chapVerses.Contains(r.TargetVerse))
                {
                    proposal.Warning = $"Verse {r.TargetVerse} not found in Chapter {r.Chapter}.";
                    proposal.IsAccepted = false;
                    proposals.Add(proposal);
                    continue;
                }

                // Check if reference or heading already exists near this verse
                CheckExistingContext(usfmText, r.Chapter, r.TargetVerse, proposal);

                proposals.Add(proposal);
            }

            return proposals;
        }

        /// <summary>
        /// Applies approved reference proposals to the USFM text.
        /// Places \r strictly ABOVE paragraph/poetry markers (\p, \q, etc.) and BELOW section headings (\s).
        /// </summary>
        public string ApplyProposals(string usfmText, IEnumerable<ReferenceTransferProposal> approvedProposals)
        {
            if (string.IsNullOrEmpty(usfmText) || approvedProposals == null)
                return usfmText;

            var active = approvedProposals.Where(p => p.IsAccepted && !p.IsDuplicate && !p.HasWarning).ToList();
            if (active.Count == 0)
                return usfmText;

            // Group proposals by chapter
            var byChapter = active.GroupBy(p => p.Chapter).ToDictionary(g => g.Key, g => g.ToList());

            var lines = usfmText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var outputLines = new List<string>();

            int currentChapter = 0;
            int i = 0;

            while (i < lines.Length)
            {
                string line = lines[i];
                string trimmed = line.Trim();

                // Track chapter
                var cMatch = Regex.Match(trimmed, @"^\\c\s+(\d+)");
                if (cMatch.Success)
                {
                    currentChapter = int.Parse(cMatch.Groups[1].Value);
                    outputLines.Add(line);
                    i++;
                    continue;
                }

                // Check if current line is a paragraph/block marker (\p, \q, etc.) or verse marker (\v)
                string marker = trimmed.Length > 0 ? trimmed.Split(' ')[0] : string.Empty;
                bool isParaOrBlock = UsfmMarkerCategories.IsParagraphOrBlock(marker);
                bool isVerse = trimmed.StartsWith("\\v");

                if ((isParaOrBlock || isVerse) && currentChapter > 0 && byChapter.TryGetValue(currentChapter, out var chapProposals))
                {
                    int targetVNum = -1;
                    if (isVerse)
                    {
                        var vm = Regex.Match(trimmed, @"^\\v\s+(\d+)");
                        if (vm.Success) targetVNum = int.Parse(vm.Groups[1].Value);
                    }
                    else if (isParaOrBlock)
                    {
                        // Look ahead for upcoming \v
                        for (int lookAhead = i + 1; lookAhead < lines.Length && lookAhead <= i + 4; lookAhead++)
                        {
                            string ahead = lines[lookAhead].Trim();
                            var vm = Regex.Match(ahead, @"^\\v\s+(\d+)");
                            if (vm.Success)
                            {
                                targetVNum = int.Parse(vm.Groups[1].Value);
                                break;
                            }
                            if (ahead.StartsWith("\\c") || ahead.StartsWith("\\s"))
                                break;
                        }
                    }

                    if (targetVNum > 0)
                    {
                        var matchProp = chapProposals.FirstOrDefault(p => p.Verse == targetVNum);
                        if (matchProp != null)
                        {
                            // Check if preceding lines in output already contain a heading (\s)
                            bool hasHeading = false;
                            for (int lookBack = outputLines.Count - 1; lookBack >= 0; lookBack--)
                            {
                                string prev = outputLines[lookBack].Trim();
                                string prevMarker = prev.Split(' ')[0];
                                if (UsfmMarkerCategories.IsHeading(prevMarker))
                                {
                                    hasHeading = true;
                                    break;
                                }
                                if (prev.StartsWith("\\v") || prev.StartsWith("\\c") || UsfmMarkerCategories.IsParagraphOrBlock(prevMarker))
                                    break;
                            }

                            // Remove existing \r if RemoveExistingReferences is requested
                            if (_options.RemoveExistingReferences)
                            {
                                for (int lookBack = outputLines.Count - 1; lookBack >= 0; lookBack--)
                                {
                                    string prev = outputLines[lookBack].Trim();
                                    if (prev.StartsWith("\\r"))
                                    {
                                        outputLines.RemoveAt(lookBack);
                                        break;
                                    }
                                    if (prev.StartsWith("\\v") || prev.StartsWith("\\c"))
                                        break;
                                }
                            }

                            // If no heading exists and InsertHeadingIfMissing is true, insert \s Insert Heading first
                            if (!hasHeading && _options.InsertHeadingIfMissing)
                            {
                                outputLines.Add("\\s Insert Heading");
                            }

                            // Insert \r reference strictly ABOVE the paragraph or verse
                            outputLines.Add($"\\r {matchProp.ProposedReference}");

                            // Remove proposal to prevent re-insertion at \v
                            chapProposals.Remove(matchProp);
                        }
                    }
                }

                outputLines.Add(line);
                i++;
            }

            return string.Join("\r\n", outputLines);
        }

        private static Dictionary<int, HashSet<int>> ScanUsfmStructure(string usfmText)
        {
            var dict = new Dictionary<int, HashSet<int>>();
            int currentChapter = 0;

            var lines = usfmText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (string line in lines)
            {
                string s = line.Trim();
                var cMatch = Regex.Match(s, @"^\\c\s+(\d+)");
                if (cMatch.Success)
                {
                    currentChapter = int.Parse(cMatch.Groups[1].Value);
                    if (!dict.ContainsKey(currentChapter))
                        dict[currentChapter] = new HashSet<int>();
                    continue;
                }

                if (currentChapter > 0)
                {
                    var vMatch = Regex.Match(s, @"^\\v\s+(\d+)");
                    if (vMatch.Success)
                    {
                        int v = int.Parse(vMatch.Groups[1].Value);
                        dict[currentChapter].Add(v);
                    }
                }
            }

            return dict;
        }

        private void CheckExistingContext(string usfmText, int chapter, int verse, ReferenceTransferProposal proposal)
        {
            var lines = usfmText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int currentChapter = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                var cMatch = Regex.Match(line, @"^\\c\s+(\d+)");
                if (cMatch.Success)
                {
                    currentChapter = int.Parse(cMatch.Groups[1].Value);
                    continue;
                }

                if (currentChapter == chapter)
                {
                    var vMatch = Regex.Match(line, @"^\\v\s+(\d+)");
                    if (vMatch.Success && int.Parse(vMatch.Groups[1].Value) == verse)
                    {
                        // Scan preceding lines across paragraph and block markers for heading (\s) and reference (\r)
                        for (int k = i - 1; k >= 0; k--)
                        {
                            string prev = lines[k].Trim();
                            string prevMarker = prev.Length > 0 ? prev.Split(' ')[0] : string.Empty;

                            if (prev.StartsWith("\\r"))
                            {
                                proposal.ExistingReference = prev.Substring(2).Trim();
                                string existingCanon = _vernacularMgr.ToCanonicalCitation(proposal.ExistingReference);
                                if (string.Equals(proposal.ExistingReference, proposal.ProposedReference, StringComparison.OrdinalIgnoreCase) ||
                                    (!string.IsNullOrEmpty(existingCanon) && string.Equals(existingCanon, proposal.CanonicalReference, StringComparison.OrdinalIgnoreCase)))
                                {
                                    proposal.IsDuplicate = true;
                                    proposal.IsAccepted = false;
                                }
                            }
                            else if (UsfmMarkerCategories.IsHeading(prevMarker))
                            {
                                proposal.ExistingHeading = prev.Length > prevMarker.Length ? prev.Substring(prevMarker.Length).Trim() : string.Empty;
                                break;
                            }
                            else if (prev.StartsWith("\\v") || prev.StartsWith("\\c"))
                            {
                                break;
                            }
                        }

                        if (string.IsNullOrEmpty(proposal.ExistingHeading))
                        {
                            proposal.NeedsHeading = true;
                        }

                        break;
                    }
                }
            }
        }
    }
}
