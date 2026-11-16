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
    }
}
