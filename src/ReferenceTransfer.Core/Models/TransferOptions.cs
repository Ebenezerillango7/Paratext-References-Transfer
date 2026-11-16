using System;

namespace ReferenceTransfer.Core.Models
{
    /// <summary>
    /// Style of vernacular book names applied to parallel passage citations.
    /// Derived from Paratext BookNames.xml attributes (abbr, short, long).
    /// </summary>
    public enum BookNameStyle
    {
        Abbreviation,
        ShortName,
        LongName
    }

    /// <summary>
    /// User configurable options for parallel reference transfer.
    /// </summary>
    public class TransferOptions
    {
        /// <summary>
        /// Vernacular book naming format: Abbreviation, Short Name, or Long Name.
        /// </summary>
        public BookNameStyle NameStyle { get; set; } = BookNameStyle.Abbreviation;

        /// <summary>
        /// Wrap references in parentheses, e.g. (1Chr 1:1-4) vs 1Chr 1:1-4
        /// </summary>
        public bool UseParentheses { get; set; } = true;

        /// <summary>
        /// Ensure references follow a section heading (\s). If no heading exists, inserts '\s Insert Heading'.
        /// </summary>
        public bool InsertHeadingIfMissing { get; set; } = true;

        /// <summary>
        /// Clean trailing punctuation from raw reference strings.
        /// </summary>
        public bool CleanTrailingPunctuation { get; set; } = true;

        /// <summary>
        /// Replace existing \r references instead of duplicating them.
        /// </summary>
        public bool RemoveExistingReferences { get; set; } = true;

        /// <summary>
        /// Paratext Scripture Reference settings (separators for chapter/verse, ranges, sequences, etc.)
        /// </summary>
        public ProjectScriptureSettings ScriptureSettings { get; set; } = new ProjectScriptureSettings();
    }
}
