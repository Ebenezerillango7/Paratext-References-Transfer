using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ReferenceTransfer.Core.Models;

namespace ReferenceTransfer.Core.Engine
{
    /// <summary>
    /// Robust parser for parallel reference text files, GNTD extracts, and scripture data.
    /// Handles both single-line and split-line formats.
    /// </summary>
    public static class ReferenceParser
    {
        private static readonly Regex SingleLineGntdRegex = new Regex(
            @"^([A-Z0-9]{3})\s+(\d+):(\d+(?:-\d+)?)\s*:\s*(?:\\r\s*)?(.*?)\s*$",
            RegexOptions.Compiled);

        private static readonly Regex HeaderOnlyGntdRegex = new Regex(
            @"^([A-Z0-9]{3})\s+(\d+):(\d+(?:-\d+)?)\s*:?\s*$",
            RegexOptions.Compiled);

        /// <summary>
        /// Parses references from raw text content.
        /// </summary>
        public static List<ParallelReferenceEntry> ParseReferencesText(string text, string sourceType = "RESOURCE")
        {
            var results = new List<ParallelReferenceEntry>();
            if (string.IsNullOrWhiteSpace(text))
                return results;

            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int i = 0;

            while (i < lines.Length)
            {
                string line = lines[i].Trim();
                i++;

                if (string.IsNullOrEmpty(line))
                    continue;

                // 1. Try single-line format: BOOK CH:START-END: [\r] (REF)
                var singleMatch = SingleLineGntdRegex.Match(line);
                if (singleMatch.Success && !string.IsNullOrWhiteSpace(singleMatch.Groups[4].Value))
                {
                    string book = singleMatch.Groups[1].Value.ToUpperInvariant();
                    int chapter = int.Parse(singleMatch.Groups[2].Value);
                    string locStr = singleMatch.Groups[3].Value;
                    string rawRef = singleMatch.Groups[4].Value.Trim();

                    int targetVerse = CalculateTargetVerse(locStr);

                    results.Add(new ParallelReferenceEntry
                    {
                        BookCode = book,
                        Chapter = chapter,
                        TargetVerse = targetVerse,
                        RawReference = rawRef,
                        SourceLocation = locStr,
                        SourceType = sourceType
                    });
                    continue;
                }

                // 2. Try split-line format (Header on Line 1, Ref on Line 2)
                var headerMatch = HeaderOnlyGntdRegex.Match(line);
                if (headerMatch.Success && i < lines.Length)
                {
                    string nextLine = lines[i].Trim();
                    string cleanRef = Regex.Replace(nextLine, @"^\\r\s*", string.Empty).Trim();

                    if ((cleanRef.StartsWith("(") && cleanRef.EndsWith(")")) ||
                        (cleanRef.StartsWith("[") && cleanRef.EndsWith("]")))
                    {
                        string book = headerMatch.Groups[1].Value.ToUpperInvariant();
                        int chapter = int.Parse(headerMatch.Groups[2].Value);
                        string locStr = headerMatch.Groups[3].Value;
                        int targetVerse = CalculateTargetVerse(locStr);

                        results.Add(new ParallelReferenceEntry
                        {
                            BookCode = book,
                            Chapter = chapter,
                            TargetVerse = targetVerse,
                            RawReference = cleanRef,
                            SourceLocation = locStr,
                            SourceType = sourceType
                        });

                        i++; // Consumed reference line
                    }
                }
            }

            return results;
        }

        private static int CalculateTargetVerse(string locStr)
        {
            // End verse + 1
            // e.g. "0" -> 1
            // e.g. "12" -> 13
            // e.g. "29-30" -> 31
            string lastPart = locStr;
            int dashIdx = locStr.LastIndexOf('-');
            if (dashIdx >= 0 && dashIdx < locStr.Length - 1)
            {
                lastPart = locStr.Substring(dashIdx + 1);
            }

            if (int.TryParse(lastPart, out int endVerse))
            {
                return endVerse + 1;
            }
            return 1;
        }

        /// <summary>
        /// Extracts parallel passage references (\r) directly from raw USFM text.
        /// Faithfully reproduces Paratext's RegEx Pal extraction behavior with zero reliance on token APIs.
        /// Links section heading \r markers to the upcoming verse they introduce.
        /// </summary>
        public static List<ParallelReferenceEntry> ExtractReferencesFromUsfm(string usfmText, string bookCode, string sourceType = "RESOURCE")
        {
            var results = new List<ParallelReferenceEntry>();
            if (string.IsNullOrWhiteSpace(usfmText))
                return results;

            var lines = usfmText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int currentChapter = 0;
            int currentVerse = 0;
            var pendingRefs = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                // 1. Chapter marker: \c <num>
                var cMatch = Regex.Match(line, @"^\\c\s+(\d+)");
                if (cMatch.Success)
                {
                    // Flush pending refs from previous chapter if any
                    FlushPendingRefs(pendingRefs, results, bookCode, currentChapter, currentVerse > 0 ? currentVerse : 1, sourceType);
                    currentChapter = int.Parse(cMatch.Groups[1].Value);
                    currentVerse = 0;
                    continue;
                }

                // 2. Parallel reference marker: \r <reference>
                var rMatch = Regex.Match(line, @"(?:^|\s)\\r\s+([^\r\n\\]+)");
                if (rMatch.Success)
                {
                    string rawRef = rMatch.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(rawRef))
                    {
                        pendingRefs.Add(rawRef);
                    }
                    continue;
                }

                // 3. Verse marker: \v <num>
                var vMatch = Regex.Match(line, @"(?:^|\s)\\v\s+(\d+)");
                if (vMatch.Success)
                {
                    int vNum = int.Parse(vMatch.Groups[1].Value);
                    currentVerse = vNum;
                    if (pendingRefs.Count > 0)
                    {
                        FlushPendingRefs(pendingRefs, results, bookCode, currentChapter, vNum, sourceType);
                    }
                }
            }

            // Flush any remaining pending refs at end of book
            FlushPendingRefs(pendingRefs, results, bookCode, currentChapter, currentVerse > 0 ? currentVerse : 1, sourceType);

            return results;
        }

        private static void FlushPendingRefs(List<string> pendingRefs, List<ParallelReferenceEntry> results, string bookCode, int chapter, int verse, string sourceType)
        {
            if (pendingRefs.Count == 0 || chapter <= 0)
                return;

            foreach (var r in pendingRefs)
            {
                results.Add(new ParallelReferenceEntry
                {
                    BookCode = bookCode.ToUpperInvariant(),
                    Chapter = chapter,
                    TargetVerse = verse,
                    RawReference = r,
                    SourceLocation = $"{bookCode} {chapter}:{verse}",
                    SourceType = sourceType
                });
            }
            pendingRefs.Clear();
        }
    }
}
