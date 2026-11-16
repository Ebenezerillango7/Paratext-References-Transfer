using System;
using System.Collections.Generic;

namespace ReferenceTransfer.Core.Models
{
    public enum BookScopeType
    {
        AllOT,
        AllNT,
        All66,
        SingleBook
    }

    public static class CanonicalBookLists
    {
        public static readonly List<string> OldTestament = new List<string>
        {
            "GEN", "EXO", "LEV", "NUM", "DEU", "JOS", "JDG", "RUT", "1SA", "2SA",
            "1KI", "2KI", "1CH", "2CH", "EZR", "NEH", "EST", "JOB", "PSA", "PRO",
            "ECC", "SNG", "ISA", "JER", "LAM", "EZK", "DAN", "HOS", "JOL", "AMO",
            "OBA", "JON", "MIC", "NAM", "HAB", "ZEP", "HAG", "ZEC", "MAL"
        };

        public static readonly List<string> NewTestament = new List<string>
        {
            "MAT", "MRK", "LUK", "JHN", "ACT", "ROM", "1CO", "2CO", "GAL", "EPH",
            "PHP", "COL", "1TH", "2TH", "1TI", "2TI", "TIT", "PHM", "HEB", "JAS",
            "1PE", "2PE", "1JN", "2JN", "3JN", "JUD", "REV"
        };

        public static readonly List<string> All66Books = new List<string>(OldTestament)
        {
            Capacity = 66
        };

        static CanonicalBookLists()
        {
            All66Books.AddRange(NewTestament);
        }

        public static List<string> GetBooksForScope(BookScopeType scopeType, string? singleBookCode = null)
        {
            switch (scopeType)
            {
                case BookScopeType.AllOT:
                    return new List<string>(OldTestament);
                case BookScopeType.AllNT:
                    return new List<string>(NewTestament);
                case BookScopeType.All66:
                    return new List<string>(All66Books);
                case BookScopeType.SingleBook:
                    if (!string.IsNullOrEmpty(singleBookCode))
                        return new List<string> { singleBookCode.ToUpperInvariant() };
                    return new List<string>();
                default:
                    return new List<string>(All66Books);
            }
        }
    }
}
