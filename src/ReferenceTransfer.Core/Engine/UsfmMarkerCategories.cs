using System;
using System.Collections.Generic;

namespace ReferenceTransfer.Core.Engine
{
    /// <summary>
    /// Categorizes USFM markers according to official USFM structural hierarchy.
    /// In USFM, \r (parallel reference) is a heading-level element.
    /// It must be placed BELOW section headings (\s) and ABOVE paragraph/block markers (\p, \q, etc.).
    /// </summary>
    public static class UsfmMarkerCategories
    {
        /// <summary>
        /// All paragraph, poetic line, stanza, and list markers.
        /// \r must always be inserted ABOVE these markers.
        /// </summary>
        public static readonly HashSet<string> ParagraphAndBlockMarkers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Paragraphs
            "p", "m", "po", "pr", "cls", "pmo", "pm", "pmc", "pmr",
            "pi", "pi1", "pi2", "pi3", "mi", "mi1", "mi2", "nb", "pc",
            // Poetry & stanzas
            "q", "q1", "q2", "q3", "q4", "qr", "qc", "qa", "qm", "qm1", "qm2", "qm3", "b",
            // Lists
            "li", "li1", "li2", "li3", "li4"
        };

        /// <summary>
        /// Section and major heading markers.
        /// \r belongs directly BELOW these markers.
        /// </summary>
        public static readonly HashSet<string> HeadingMarkers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "s", "s1", "s2", "s3", "s4",
            "ms", "ms1", "ms2", "ms3",
            "mr", "sr", "d", "sp"
        };

        public static bool IsParagraphOrBlock(string? marker)
        {
            if (string.IsNullOrWhiteSpace(marker)) return false;
            return ParagraphAndBlockMarkers.Contains(marker.TrimStart('\\').Trim());
        }

        public static bool IsHeading(string? marker)
        {
            if (string.IsNullOrWhiteSpace(marker)) return false;
            return HeadingMarkers.Contains(marker.TrimStart('\\').Trim());
        }
    }
}
