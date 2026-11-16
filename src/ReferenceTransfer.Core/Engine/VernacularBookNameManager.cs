using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ReferenceTransfer.Core.Models;

namespace ReferenceTransfer.Core.Engine
{
    /// <summary>
    /// Loads and resolves vernacular book names from Paratext BookNames.xml or live Paratext IProject.
    /// Translates citation references across canonical, source, and target vernacular representations.
    /// </summary>
    public class VernacularBookNameManager
    {
        private readonly Dictionary<string, string> _codeToAbbr = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _codeToShort = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _codeToLong = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _codeToAliases = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
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

                    RegisterBook(code, abbr, shortName, longName);
                }
            }
            catch
            {
                // Graceful fallback to default aliases if XML is invalid
            }
        }

        public void RegisterBook(string code, string? abbr, string? shortName, string? longName)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            code = code.Trim().ToUpperInvariant();

            string bestAbbr = !string.IsNullOrEmpty(abbr) ? abbr!.Trim() : (!string.IsNullOrEmpty(shortName) ? shortName!.Trim() : code);
            _codeToAbbr[code] = bestAbbr;

            string bestShort = !string.IsNullOrEmpty(shortName) ? shortName!.Trim() : (!string.IsNullOrEmpty(abbr) ? abbr!.Trim() : code);
            _codeToShort[code] = bestShort;

            string bestLong = !string.IsNullOrEmpty(longName) ? longName!.Trim() : (!string.IsNullOrEmpty(shortName) ? shortName!.Trim() : (!string.IsNullOrEmpty(abbr) ? abbr!.Trim() : code));
            _codeToLong[code] = bestLong;

            // Automatically register variants: dot-trimmed and Indic-digit normalized
            foreach (var n in new[] { abbr, shortName, longName })
            {
                if (string.IsNullOrWhiteSpace(n)) continue;
                string clean = n!.Trim();
                RegisterBookAlias(code, clean);

                if (clean.EndsWith("."))
                {
                    RegisterBookAlias(code, clean.TrimEnd('.').Trim());
                }

                string norm = NormalizeIndicDigitsToAscii(clean);
                if (norm != clean)
                {
                    RegisterBookAlias(code, norm);
                    if (norm.EndsWith("."))
                    {
                        RegisterBookAlias(code, norm.TrimEnd('.').Trim());
                    }
                }
            }
        }

        public void RegisterBookAlias(string code, string? alias)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(alias)) return;
            code = code.Trim().ToUpperInvariant();
            string cleanAlias = alias!.Trim();

            if (!_codeToAliases.TryGetValue(code, out var list))
            {
                list = new List<string>();
                _codeToAliases[code] = list;
            }

            if (!list.Contains(cleanAlias, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(cleanAlias);
            }
        }

        public IEnumerable<string> GetAllBookCodes() => _codeToAbbr.Keys;

        public List<string> GetAllNamesForCode(string bookCode)
        {
            var list = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_codeToLong.TryGetValue(bookCode, out string l) && !string.IsNullOrEmpty(l)) list.Add(l.Trim());
            if (_codeToShort.TryGetValue(bookCode, out string s) && !string.IsNullOrEmpty(s)) list.Add(s.Trim());
            if (_codeToAbbr.TryGetValue(bookCode, out string a) && !string.IsNullOrEmpty(a)) list.Add(a.Trim());
            if (_codeToAliases.TryGetValue(bookCode, out var aliases))
            {
                foreach (var alias in aliases)
                {
                    if (!string.IsNullOrEmpty(alias)) list.Add(alias.Trim());
                }
            }
            // Always sort longest first to prevent prefix collisions
            return list.OrderByDescending(x => x.Length).ToList();
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
            if (_codeToAbbr.TryGetValue(bookCode, out string a) && !string.IsNullOrEmpty(a))
                return a;
            return bookCode;
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
        /// Normalizes Indic / Devanagari / Tamil / Kannada / Telugu / Malayalam / Bengali / etc.
        /// digits to ASCII Arabic numerals (0-9).
        /// </summary>
        public static string NormalizeIndicDigitsToAscii(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.DecimalDigitNumber)
                {
                    int digit = (int)char.GetNumericValue(c);
                    if (digit >= 0 && digit <= 9)
                    {
                        sb.Append((char)('0' + digit));
                        continue;
                    }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Translates a full citation reference string into target vernacular book names (Abbreviation, Short Name, or Long Name)
        /// using the target project's Scripture Reference settings (chapter/verse separator, range indicator, etc.).
        /// Supports optional source manager to convert source vernacular names (e.g. Hindi -> Tamil).
        /// </summary>
        public string TranslateCitation(
            string rawRef, 
            bool useParentheses = true, 
            bool cleanPunctuation = true, 
            BookNameStyle style = BookNameStyle.Abbreviation,
            ProjectScriptureSettings? settings = null,
            VernacularBookNameManager? sourceMgr = null)
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

            // Normalize Indic digits if present
            s = NormalizeIndicDigitsToAscii(s);

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

            // 6. If source manager is supplied and loaded with vernacular names (e.g. Hindi),
            // replace source vernacular names with target vernacular names
            if (sourceMgr != null && sourceMgr.IsLoaded)
            {
                var allMappings = new List<(string Name, string TargetName)>();
                foreach (var code in sourceMgr.GetAllBookCodes())
                {
                    string targetName = GetVernacularName(code, style);
                    if (string.IsNullOrEmpty(targetName)) continue;

                    foreach (var sName in sourceMgr.GetAllNamesForCode(code))
                    {
                        if (!string.IsNullOrWhiteSpace(sName) && sName != targetName)
                        {
                            allMappings.Add((sName, targetName));
                        }
                    }
                }

                // Match longer names before substrings (e.g. '1 इतिहास' before '1 इति')
                foreach (var (sName, targetName) in allMappings.OrderByDescending(m => m.Name.Length))
                {
                    string cleanBase = sName.TrimEnd('.');
                    string pattern = $@"(?<!\p{{L}}){Regex.Escape(cleanBase)}\.?(?!\p{{L}})";
                    s = Regex.Replace(s, pattern, targetName, RegexOptions.IgnoreCase);
                }
            }

            // 7. Replace each English book name with target vernacular name according to chosen style
            foreach (var kvp in _englishNameToCode.OrderByDescending(k => k.Key.Length))
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

            // 8. Handle spacing between book and chapter
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

        /// <summary>
        /// Converts any citation (English, Source Vernacular, or Target Vernacular)
        /// into standardized canonical 3-letter Bible book codes.
        /// e.g. "(1 Chronicles 1.1-4)" -> "1CH 1:1-4"
        /// e.g. "(1 इतिहास 1:1-4)" -> "1CH 1:1-4"
        /// e.g. "(1 நாளாகமம் 1:1-4)" -> "1CH 1:1-4"
        /// </summary>
        public string ToCanonicalCitation(string rawRef, VernacularBookNameManager? sourceMgr = null)
        {
            if (string.IsNullOrWhiteSpace(rawRef)) return string.Empty;

            string s = rawRef.Trim();
            if ((s.StartsWith("(") && s.EndsWith(")")) || (s.StartsWith("[") && s.EndsWith("]")))
            {
                s = s.Substring(1, s.Length - 2).Trim();
            }
            s = Regex.Replace(s, @"^\\r\s*", string.Empty);
            s = s.TrimEnd('.', ':', ';', ',');

            // Normalize Indic digits
            s = NormalizeIndicDigitsToAscii(s);

            // Standardize chapter/verse separators to : and ranges to -
            s = Regex.Replace(s, @"(?<=\b\d+)[:.](?=\d+\b)", ":");
            s = Regex.Replace(s, @"(?<=\b\d+)\s*[-–—]\s*(?=\d+\b)", "-");

            // 1. Match source vernacular names to canonical codes (if source manager provided)
            if (sourceMgr != null && sourceMgr.IsLoaded)
            {
                var allMappings = new List<(string Name, string Code)>();
                foreach (var code in sourceMgr.GetAllBookCodes())
                {
                    foreach (var sName in sourceMgr.GetAllNamesForCode(code))
                    {
                        if (!string.IsNullOrWhiteSpace(sName))
                            allMappings.Add((sName, code));
                    }
                }

                // Longest names first
                foreach (var (sName, code) in allMappings.OrderByDescending(m => m.Name.Length))
                {
                    string cleanBase = sName.TrimEnd('.');
                    string pattern = $@"(?<!\p{{L}}){Regex.Escape(cleanBase)}\.?(?!\p{{L}})";
                    s = Regex.Replace(s, pattern, code, RegexOptions.IgnoreCase);
                }
            }

            // 2. Match English aliases to canonical codes (longest first)
            foreach (var kvp in _englishNameToCode.OrderByDescending(k => k.Key.Length))
            {
                string engName = kvp.Key;
                string bookCode = kvp.Value;
                string pattern = $@"\b{Regex.Escape(engName)}\b";
                s = Regex.Replace(s, pattern, bookCode, RegexOptions.IgnoreCase);
            }

            // 3. Match this manager's own vernacular names to canonical codes
            if (IsLoaded)
            {
                var allMappings = new List<(string Name, string Code)>();
                foreach (var code in GetAllBookCodes())
                {
                    foreach (var n in GetAllNamesForCode(code))
                    {
                        if (!string.IsNullOrWhiteSpace(n))
                            allMappings.Add((n, code));
                    }
                }

                foreach (var (n, code) in allMappings.OrderByDescending(m => m.Name.Length))
                {
                    string cleanBase = n.TrimEnd('.');
                    string pattern = $@"(?<!\p{{L}}){Regex.Escape(cleanBase)}\.?(?!\p{{L}})";
                    s = Regex.Replace(s, pattern, code, RegexOptions.IgnoreCase);
                }
            }

            return s.Trim();
        }

        private void InitializeEnglishBookAliases()
        {
            var aliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["GEN"] = new[] { "Genesis", "Gen", "Ge", "Gn" },
                ["EXO"] = new[] { "Exodus", "Exod", "Exo", "Ex" },
                ["LEV"] = new[] { "Leviticus", "Lev", "Le", "Lv" },
                ["NUM"] = new[] { "Numbers", "Num", "Nu", "Nm", "Nb" },
                ["DEU"] = new[] { "Deuteronomy", "Deut", "Deu", "Dt" },
                ["JOS"] = new[] { "Joshua", "Josh", "Jos", "Jsh" },
                ["JDG"] = new[] { "Judges", "Judg", "Jdg", "Jg", "Jdgs" },
                ["RUT"] = new[] { "Ruth", "Rth", "Ru" },
                ["1SA"] = new[] { "1 Samuel", "1 Sam", "1Sa", "1S", "I Samuel", "1st Samuel" },
                ["2SA"] = new[] { "2 Samuel", "2 Sam", "2Sa", "2S", "II Samuel", "2nd Samuel" },
                ["1KI"] = new[] { "1 Kings", "1 Kgs", "1Ki", "1K", "I Kings", "1st Kings" },
                ["2KI"] = new[] { "2 Kings", "2 Kgs", "2Ki", "2K", "II Kings", "2nd Kings" },
                ["1CH"] = new[] { "1 Chronicles", "1 Chron", "1Chr", "1Ch", "I Chronicles", "1st Chronicles" },
                ["2CH"] = new[] { "2 Chronicles", "2 Chron", "2Chr", "2Ch", "II Chronicles", "2nd Chronicles" },
                ["EZR"] = new[] { "Ezra", "Ezr" },
                ["NEH"] = new[] { "Nehemiah", "Neh", "Ne" },
                ["EST"] = new[] { "Esther", "Esth", "Est", "Es" },
                ["JOB"] = new[] { "Job", "Jb" },
                ["PSA"] = new[] { "Psalms", "Psalm", "Psa", "Ps", "Psm", "Pss" },
                ["PRO"] = new[] { "Proverbs", "Prov", "Pro", "Pr", "Prv" },
                ["ECC"] = new[] { "Ecclesiastes", "Eccles", "Ecc", "Ec", "Qoh" },
                ["SNG"] = new[] { "Song of Solomon", "Song of Songs", "Song", "Canticles", "Cant" },
                ["ISA"] = new[] { "Isaiah", "Isa", "Is" },
                ["JER"] = new[] { "Jeremiah", "Jer", "Je", "Jr" },
                ["LAM"] = new[] { "Lamentations", "Lam", "La" },
                ["EZK"] = new[] { "Ezekiel", "Ezek", "Eze", "Ezk" },
                ["DAN"] = new[] { "Daniel", "Dan", "Da", "Dn" },
                ["HOS"] = new[] { "Hosea", "Hos", "Ho" },
                ["JOL"] = new[] { "Joel", "Joe", "Jl" },
                ["AMO"] = new[] { "Amos", "Amo", "Am" },
                ["OBA"] = new[] { "Obadiah", "Obad", "Oba", "Ob" },
                ["JON"] = new[] { "Jonah", "Jnh", "Jon" },
                ["MIC"] = new[] { "Micah", "Mic", "Mc" },
                ["NAM"] = new[] { "Nahum", "Nah", "Na" },
                ["HAB"] = new[] { "Habakkuk", "Hab", "Hb" },
                ["ZEP"] = new[] { "Zephaniah", "Zeph", "Zep", "Zp" },
                ["HAG"] = new[] { "Haggai", "Hag", "Hg" },
                ["ZEC"] = new[] { "Zechariah", "Zech", "Zec", "Zc" },
                ["MAL"] = new[] { "Malachi", "Mal", "Ml" },
                ["MAT"] = new[] { "Matthew", "Matt", "Mat", "Mt" },
                ["MRK"] = new[] { "Mark", "Mrk", "Mk", "Mr" },
                ["LUK"] = new[] { "Luke", "Luk", "Lk", "Lu" },
                ["JHN"] = new[] { "John", "Joh", "Jhn", "Jn" },
                ["ACT"] = new[] { "Acts", "Act", "Ac" },
                ["ROM"] = new[] { "Romans", "Rom", "Ro", "Rm" },
                ["1CO"] = new[] { "1 Corinthians", "1 Cor", "1Co", "I Corinthians", "1st Corinthians" },
                ["2CO"] = new[] { "2 Corinthians", "2 Cor", "2Co", "II Corinthians", "2nd Corinthians" },
                ["GAL"] = new[] { "Galatians", "Gal", "Ga" },
                ["EPH"] = new[] { "Ephesians", "Eph", "Ep" },
                ["PHP"] = new[] { "Philippians", "Phil", "Php", "Pp" },
                ["COL"] = new[] { "Colossians", "Col", "Co" },
                ["1TH"] = new[] { "1 Thessalonians", "1 Thess", "1Th", "I Thessalonians", "1st Thessalonians" },
                ["2TH"] = new[] { "2 Thessalonians", "2 Thess", "2Th", "II Thessalonians", "2nd Thessalonians" },
                ["1TI"] = new[] { "1 Timothy", "1 Tim", "1Ti", "I Timothy", "1st Timothy" },
                ["2TI"] = new[] { "2 Timothy", "2 Tim", "2Ti", "II Timothy", "2nd Timothy" },
                ["TIT"] = new[] { "Titus", "Tit", "Ti" },
                ["PHM"] = new[] { "Philemon", "Philem", "Phm", "Pm" },
                ["HEB"] = new[] { "Hebrews", "Heb" },
                ["JAS"] = new[] { "James", "Jas", "Jm" },
                ["1PE"] = new[] { "1 Peter", "1 Pet", "1Pe", "1P", "I Peter", "1st Peter" },
                ["2PE"] = new[] { "2 Peter", "2 Pet", "2Pe", "2P", "II Peter", "2nd Peter" },
                ["1JN"] = new[] { "1 John", "1Jn", "1J", "I John", "1st John" },
                ["2JN"] = new[] { "2 John", "2Jn", "2J", "II John", "2nd John" },
                ["3JN"] = new[] { "3 John", "3Jn", "3J", "III John", "3rd John" },
                ["JUD"] = new[] { "Jude", "Jud", "Jd" },
                ["REV"] = new[] { "Revelation", "Rev", "Re" }
            };

            foreach (var kvp in aliases)
            {
                string code = kvp.Key;
                _englishNameToCode[code] = code;
                foreach (var alias in kvp.Value)
                {
                    _englishNameToCode[alias] = code;
                }
            }
        }
    }
}
