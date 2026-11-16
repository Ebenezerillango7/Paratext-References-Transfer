using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ReferenceTransfer.Core.Models;

namespace ReferenceTransfer.Core.Engine
{
    /// <summary>
    /// Loads and resolves vernacular book names from Paratext BookNames.xml.
    /// Translates citation references (e.g. "1 Chronicles 1.1-4" -> "(1நாளா 1:1-4)").
    /// </summary>
    public class VernacularBookNameManager
    {
        private readonly Dictionary<string, string> _codeToAbbr = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _codeToShort = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _codeToLong = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _englishNameToCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public bool IsLoaded => _codeToAbbr.Count > 0;
        public int BookCount => _codeToAbbr.Count;

        public VernacularBookNameManager()
        {
            InitializeEnglishBookAliases();
        }

        public VernacularBookNameManager(string xmlPath) : this()
        {
            if (File.Exists(xmlPath))
            {
                LoadFromXml(xmlPath);
            }
        }

        public void LoadFromXml(string xmlPath)
        {
            if (!File.Exists(xmlPath)) return;

            try
            {
                var doc = XDocument.Load(xmlPath);
                foreach (var bookElem in doc.Descendants("book"))
                {
                    string code = bookElem.Attribute("code")?.Value?.Trim().ToUpperInvariant() ?? string.Empty;
                    if (string.IsNullOrEmpty(code)) continue;

                    string abbr = bookElem.Attribute("abbr")?.Value?.Trim() ?? string.Empty;
                    string shortName = bookElem.Attribute("short")?.Value?.Trim() ?? string.Empty;
                    string longName = bookElem.Attribute("long")?.Value?.Trim() ?? string.Empty;

                    string bestAbbr = !string.IsNullOrEmpty(abbr) ? abbr : (!string.IsNullOrEmpty(shortName) ? shortName : code);
                    _codeToAbbr[code] = bestAbbr;

                    string bestShort = !string.IsNullOrEmpty(shortName) ? shortName : (!string.IsNullOrEmpty(abbr) ? abbr : code);
                    _codeToShort[code] = bestShort;

                    string bestLong = !string.IsNullOrEmpty(longName) ? longName : (!string.IsNullOrEmpty(shortName) ? shortName : (!string.IsNullOrEmpty(abbr) ? abbr : code));
                    _codeToLong[code] = bestLong;
                }
            }
            catch
            {
                // Graceful fallback to default aliases if XML is invalid
            }
        }

        public string GetVernacularName(string bookCode, BookNameStyle style = BookNameStyle.Abbreviation)
        {
            switch (style)
            {
                case BookNameStyle.ShortName:
                    if (_codeToShort.TryGetValue(bookCode, out string s) && !string.IsNullOrEmpty(s))
                        return s;
                    break;
                case BookNameStyle.LongName:
                    if (_codeToLong.TryGetValue(bookCode, out string l) && !string.IsNullOrEmpty(l))
                        return l;
                    break;
                case BookNameStyle.Abbreviation:
                default:
                    if (_codeToAbbr.TryGetValue(bookCode, out string a) && !string.IsNullOrEmpty(a))
                        return a;
                    break;
            }

            if (_codeToAbbr.TryGetValue(bookCode, out string fbAbbr) && !string.IsNullOrEmpty(fbAbbr))
                return fbAbbr;
            if (_codeToShort.TryGetValue(bookCode, out string fbShort) && !string.IsNullOrEmpty(fbShort))
                return fbShort;

            return bookCode;
        }

        public string GetVernacularAbbreviation(string bookCode)
        {
            return GetVernacularName(bookCode, BookNameStyle.Abbreviation);
        }

        public string ResolveCodeFromEnglishName(string nameOrCode)
        {
            string clean = nameOrCode.Trim();
            if (_englishNameToCode.TryGetValue(clean, out string code))
                return code;

            // Direct check for 3-letter code
            if (clean.Length == 3)
                return clean.ToUpperInvariant();

            return clean;
        }

        /// <summary>
        /// Translates a full citation reference string into target vernacular book names (Abbreviation, Short Name, or Long Name)
        /// using the target project's Scripture Reference settings (chapter/verse separator, range indicator, etc.).
        /// </summary>
        public string TranslateCitation(
            string rawRef, 
            bool useParentheses = true, 
            bool cleanPunctuation = true, 
            BookNameStyle style = BookNameStyle.Abbreviation,
            ProjectScriptureSettings? settings = null)
        {
            if (string.IsNullOrWhiteSpace(rawRef))
                return string.Empty;

            settings = settings ?? new ProjectScriptureSettings();
            string s = rawRef.Trim();

            // Strip enclosing parentheses or brackets if already present
            if ((s.StartsWith("(") && s.EndsWith(")")) || (s.StartsWith("[") && s.EndsWith("]")))
            {
                s = s.Substring(1, s.Length - 2).Trim();
            }

            // Remove leading \r marker if present
            s = Regex.Replace(s, @"^\\r\s*", string.Empty);

            // Clean trailing dot or colon if requested
            if (cleanPunctuation)
            {
                s = s.TrimEnd('.', ':', ';', ',');
            }

            // 1. Cross-chapter ranges (e.g. 35.30-36.1 or 35:30—36:1)
            string chapRange = !string.IsNullOrEmpty(settings.ChapterRangeSeparator) ? settings.ChapterRangeSeparator : settings.RangeIndicator;
            s = Regex.Replace(s, @"(?<=\d[:.]\d+)\s*[-–—]\s*(?=\d+[:.]\d+)", chapRange);

            // 2. Standardize verse ranges (e.g. 1.5-23, 1:5–23 -> range indicator from settings)
            s = Regex.Replace(s, @"(?<=\b\d+)\s*[-–—]\s*(?=\d+\b)", settings.RangeIndicator);

            // 3. Standardize chapter:verse separator (e.g. 1.5 or 1:5 -> ChapterVerseSeparator from settings)
            s = Regex.Replace(s, @"(?<=\b\d+)[:.](?=\d+\b)", settings.ChapterVerseSeparator);

            // 4. Standardize verse sequence indicator (e.g. 1:32,33 -> SequenceIndicator)
            string seqSep = settings.SequenceIndicator;
            s = Regex.Replace(s, @"(?<=\b\d+)\s*,\s*(?=\d+\b)", seqSep);

            // 5. Standardize book sequence separator (e.g. '; ' or ';')
            s = Regex.Replace(s, @";\s*", settings.BookSequenceSeparator);

            // 6. Replace each English book name with target vernacular name according to chosen style
            foreach (var kvp in _englishNameToCode)
            {
                string engName = kvp.Key;
                string bookCode = kvp.Value;
                string vernacular = GetVernacularName(bookCode, style);

                if (string.IsNullOrEmpty(vernacular) || vernacular == engName)
                    continue;

                // Match with word boundary, case-insensitive
                string pattern = $@"\b{Regex.Escape(engName)}\b";
                s = Regex.Replace(s, pattern, vernacular, RegexOptions.IgnoreCase);
            }

            // 7. Handle spacing between book and chapter
            if (settings.NoSpaceBetweenBookAndChapter)
            {
                s = Regex.Replace(s, @"(\p{L})\s+(\d+)", "$1$2");
            }

            s = s.Trim();

            if (useParentheses)
            {
                return $"({s})";
            }
            return s;
        }

        private void InitializeEnglishBookAliases()
        {
            var aliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["GEN"] = new[] { "Genesis", "Gen" },
                ["EXO"] = new[] { "Exodus", "Exod", "Exo", "Ex" },
                ["LEV"] = new[] { "Leviticus", "Lev" },
                ["NUM"] = new[] { "Numbers", "Num" },
                ["DEU"] = new[] { "Deuteronomy", "Deut", "Deu" },
                ["JOS"] = new[] { "Joshua", "Josh", "Jos" },
                ["JDG"] = new[] { "Judges", "Judg", "Jdg" },
                ["RUT"] = new[] { "Ruth", "Rut" },
                ["1SA"] = new[] { "1 Samuel", "1 Sam", "1Sa", "1 S" },
                ["2SA"] = new[] { "2 Samuel", "2 Sam", "2Sa", "2 S" },
                ["1KI"] = new[] { "1 Kings", "1 Kgs", "1Ki", "1 K" },
                ["2KI"] = new[] { "2 Kings", "2 Kgs", "2Ki", "2 K" },
                ["1CH"] = new[] { "1 Chronicles", "1 Chron", "1Ch", "1 Chr" },
                ["2CH"] = new[] { "2 Chronicles", "2 Chron", "2Ch", "2 Chr" },
                ["EZR"] = new[] { "Ezra", "Ezr" },
                ["NEH"] = new[] { "Nehemiah", "Neh" },
                ["EST"] = new[] { "Esther", "Esth", "Est" },
                ["JOB"] = new[] { "Job" },
                ["PSA"] = new[] { "Psalms", "Psalm", "Psa", "Ps" },
                ["PRO"] = new[] { "Proverbs", "Prov", "Pro" },
                ["ECC"] = new[] { "Ecclesiastes", "Eccles", "Ecc" },
                ["SNG"] = new[] { "Song of Solomon", "Song of Songs", "Song", "Sng" },
                ["ISA"] = new[] { "Isaiah", "Isa" },
                ["JER"] = new[] { "Jeremiah", "Jer" },
                ["LAM"] = new[] { "Lamentations", "Lam" },
                ["EZK"] = new[] { "Ezekiel", "Ezek", "Ezk" },
                ["DAN"] = new[] { "Daniel", "Dan" },
                ["HOS"] = new[] { "Hosea", "Hos" },
                ["JOL"] = new[] { "Joel", "Jol" },
                ["AMO"] = new[] { "Amos", "Amo" },
                ["OBA"] = new[] { "Obadiah", "Obad", "Oba" },
                ["JON"] = new[] { "Jonah", "Jon" },
                ["MIC"] = new[] { "Micah", "Mic" },
                ["NAM"] = new[] { "Nahum", "Nah", "Nam" },
                ["HAB"] = new[] { "Habakkuk", "Hab" },
                ["ZEP"] = new[] { "Zephaniah", "Zeph", "Zep" },
                ["HAG"] = new[] { "Haggai", "Hag" },
                ["ZEC"] = new[] { "Zechariah", "Zech", "Zec" },
                ["MAL"] = new[] { "Malachi", "Mal" },
                ["MAT"] = new[] { "Matthew", "Matt", "Mat" },
                ["MRK"] = new[] { "Mark", "Mrk", "Mk" },
                ["LUK"] = new[] { "Luke", "Luk", "Lk" },
                ["JHN"] = new[] { "John", "Jhn", "Jn" },
                ["ACT"] = new[] { "Acts", "Act" },
                ["ROM"] = new[] { "Romans", "Rom" },
                ["1CO"] = new[] { "1 Corinthians", "1 Cor", "1Co" },
                ["2CO"] = new[] { "2 Corinthians", "2 Cor", "2Co" },
                ["GAL"] = new[] { "Galatians", "Gal" },
                ["EPH"] = new[] { "Ephesians", "Eph" },
                ["PHP"] = new[] { "Philippians", "Phil", "Php" },
                ["COL"] = new[] { "Colossians", "Col" },
                ["1TH"] = new[] { "1 Thessalonians", "1 Thess", "1Th" },
                ["2TH"] = new[] { "2 Thessalonians", "2 Thess", "2Th" },
                ["1TI"] = new[] { "1 Timothy", "1 Tim", "1Ti" },
                ["2TI"] = new[] { "2 Timothy", "2 Tim", "2Ti" },
                ["TIT"] = new[] { "Titus", "Tit" },
                ["PHM"] = new[] { "Philemon", "Phlm", "Phm" },
                ["HEB"] = new[] { "Hebrews", "Heb" },
                ["JAS"] = new[] { "James", "Jas" },
                ["1PE"] = new[] { "1 Peter", "1 Pet", "1Pe" },
                ["2PE"] = new[] { "2 Peter", "2 Pet", "2Pe" },
                ["1JN"] = new[] { "1 John", "1Jn" },
                ["2JN"] = new[] { "2 John", "2Jn" },
                ["3JN"] = new[] { "3 John", "3Jn" },
                ["JUD"] = new[] { "Jude", "Jud" },
                ["REV"] = new[] { "Revelation", "Rev" }
            };

            foreach (var kvp in aliases)
            {
                string code = kvp.Key;
                _englishNameToCode[code] = code;
                foreach (string alias in kvp.Value)
                {
                    _englishNameToCode[alias] = code;
                }
            }
        }
    }
}
