using System;

namespace ReferenceTransfer.Core.Models
{
    /// <summary>
    /// Represents a parsed parallel passage reference entry.
    /// </summary>
    public class ParallelReferenceEntry
    {
        public string BookCode { get; set; } = string.Empty;
        public int Chapter { get; set; }
        public int TargetVerse { get; set; }
        public string RawReference { get; set; } = string.Empty;
        public string SourceLocation { get; set; } = string.Empty;
        public string SourceType { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"{BookCode} {Chapter}:{TargetVerse} -> {RawReference}";
        }
    }
}
