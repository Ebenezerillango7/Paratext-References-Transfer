using System;

namespace ReferenceTransfer.Core.Models
{
    /// <summary>
    /// Represents an individual proposed reference insertion or update in scripture text.
    /// Displayed in the interactive review grid for translator approval.
    /// </summary>
    public class ReferenceTransferProposal
    {
        public string BookCode { get; set; } = string.Empty;
        public int Chapter { get; set; }
        public int Verse { get; set; }

        public string ReferenceLocation => $"{BookCode} {Chapter}:{Verse}";

        /// <summary>
        /// The raw reference as extracted from the source (e.g. English "1 Chronicles 1.1-4")
        /// </summary>
        public string SourceReference { get; set; } = string.Empty;

        /// <summary>
        /// The canonical 3-letter Bible book code reference (e.g. "1CH 1:1-4")
        /// </summary>
        public string CanonicalReference { get; set; } = string.Empty;

        /// <summary>
        /// The vernacular-translated reference ready to insert (e.g. Tamil "(1நாளா 1:1-4)")
        /// </summary>
        public string ProposedReference { get; set; } = string.Empty;

        /// <summary>
        /// Existing section heading above this verse in the target text (if any)
        /// </summary>
        public string ExistingHeading { get; set; } = string.Empty;

        /// <summary>
        /// Existing \r reference already in target text (if any)
        /// </summary>
        public string ExistingReference { get; set; } = string.Empty;

        /// <summary>
        /// Whether a heading (\s) will be automatically added because none exists
        /// </summary>
        public bool NeedsHeading { get; set; }

        /// <summary>
        /// Whether this exact reference already exists in the target text
        /// </summary>
        public bool IsDuplicate { get; set; }

        /// <summary>
        /// Whether the translator has accepted this change (default: true, unless duplicate)
        /// </summary>
        public bool IsAccepted { get; set; } = true;

        /// <summary>
        /// Any warning or validation context (e.g. verse missing in chapter, chapter missing)
        /// </summary>
        public string Warning { get; set; } = string.Empty;

        public bool HasWarning => !string.IsNullOrEmpty(Warning);

        /// <summary>
        /// Whether the target project lacked vernacular mapping for this book and English was used as fallback
        /// </summary>
        public bool IsEnglishFallback { get; set; }

        /// <summary>
        /// Unified status string for display in the grid (e.g. "New", "New  ➕ \s", "Replaces Existing", "Duplicate (Skipped)")
        /// </summary>
        public string Status
        {
            get
            {
                if (IsDuplicate) return "Duplicate (Skipped)";
                if (HasWarning) return Warning;
                if (!string.IsNullOrEmpty(ExistingReference)) return "Replaces Existing";
                if (IsEnglishFallback) return NeedsHeading ? "English Fallback  ➕ \\s" : "English Fallback";
                if (NeedsHeading) return "New  ➕ \\s";
                return "New";
            }
        }

        public string ActionType => Status;
    }
}
