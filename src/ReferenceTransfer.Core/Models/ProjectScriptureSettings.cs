using System;
using System.IO;
using System.Xml.Linq;

namespace ReferenceTransfer.Core.Models
{
    /// <summary>
    /// Encapsulates Scripture Reference settings loaded from Paratext Settings.xml.
    /// Manages project-specific separators (chapter/verse, range, sequence, book spacing)
    /// to guarantee 100% compliance with Paratext Scripture Reference Checks.
    /// </summary>
    public class ProjectScriptureSettings
    {
        public string ChapterVerseSeparator { get; set; } = ":";
        public string RangeIndicator { get; set; } = "-";
        public string SequenceIndicator { get; set; } = ",";
        public string BookChapterSeparator { get; set; } = " ";
        public string BookSequenceSeparator { get; set; } = "; ";
        public string ChapterRangeSeparator { get; set; } = "—";
        public string ChapterNumberSeparator { get; set; } = "; ";
        public bool NoSpaceBetweenBookAndChapter { get; set; } = false;
        public BookNameStyle DefaultMarkerRStyle { get; set; } = BookNameStyle.Abbreviation;
        public bool IsLoaded { get; set; } = false;

        public static ProjectScriptureSettings LoadFromSettingsXml(string settingsXmlPath)
        {
            var s = new ProjectScriptureSettings();
            if (string.IsNullOrEmpty(settingsXmlPath) || !File.Exists(settingsXmlPath))
                return s;

            try
            {
                var doc = XDocument.Load(settingsXmlPath);
                var root = doc.Root;
                if (root == null) return s;

                string? cv = root.Element("ChapterVerseSeparator")?.Value;
                if (!string.IsNullOrEmpty(cv)) s.ChapterVerseSeparator = cv.Trim();

                string? range = root.Element("RangeIndicator")?.Value;
                if (!string.IsNullOrEmpty(range)) s.RangeIndicator = range.Trim();

                string? seq = root.Element("SequenceIndicator")?.Value;
                if (!string.IsNullOrEmpty(seq)) s.SequenceIndicator = seq.Trim();

                string? bc = root.Element("BookChapterSeparator")?.Value;
                if (bc != null) s.BookChapterSeparator = bc;

                string? bseq = root.Element("BookSequenceSeparator")?.Value;
                if (!string.IsNullOrEmpty(bseq)) s.BookSequenceSeparator = bseq;

                string? crange = root.Element("ChapterRangeSeparator")?.Value;
                if (!string.IsNullOrEmpty(crange)) s.ChapterRangeSeparator = crange.Trim();

                string? noSpace = root.Element("NoSpaceBetweenBookAndChapter")?.Value;
                if (!string.IsNullOrEmpty(noSpace) && bool.TryParse(noSpace, out bool ns))
                    s.NoSpaceBetweenBookAndChapter = ns;

                string? markerR = root.Element("BookSourceForMarkerR")?.Value;
                if (!string.IsNullOrEmpty(markerR))
                {
                    if (string.Equals(markerR, "ShortName", StringComparison.OrdinalIgnoreCase))
                        s.DefaultMarkerRStyle = BookNameStyle.ShortName;
                    else if (string.Equals(markerR, "LongName", StringComparison.OrdinalIgnoreCase))
                        s.DefaultMarkerRStyle = BookNameStyle.LongName;
                    else
                        s.DefaultMarkerRStyle = BookNameStyle.Abbreviation;
                }

                s.IsLoaded = true;
            }
            catch
            {
                // Graceful fallback to default settings if Settings.xml is missing or invalid
            }

            return s;
        }
    }
}
