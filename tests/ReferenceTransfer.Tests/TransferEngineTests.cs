using System;
using System.Collections.Generic;
using ReferenceTransfer.Core.Engine;
using ReferenceTransfer.Core.Models;
using Xunit;

namespace ReferenceTransfer.Tests
{
    public class TransferEngineTests
    {
        [Fact]
        public void GenerateProposals_WhenVersePresent_CreatesProposalWithHeadingCheck()
        {
            string usfm = @"\id GEN
\c 5
\p
\v 1 This is the book of the generations of Adam.
\v 2 Male and female created he them;";

            var sourceRefs = new List<ParallelReferenceEntry>
            {
                new ParallelReferenceEntry { BookCode = "GEN", Chapter = 5, TargetVerse = 1, RawReference = "(1 Chronicles 1.1-4)" }
            };

            var engine = new ReferenceTransferEngine(new VernacularBookNameManager(), new TransferOptions { InsertHeadingIfMissing = true });
            var proposals = engine.GenerateProposals("GEN", usfm, sourceRefs);

            Assert.Single(proposals);
            Assert.Equal("GEN", proposals[0].BookCode);
            Assert.Equal(5, proposals[0].Chapter);
            Assert.Equal(1, proposals[0].Verse);
            Assert.True(proposals[0].NeedsHeading);
            Assert.True(proposals[0].IsAccepted);
            Assert.False(proposals[0].HasWarning);
        }

        [Fact]
        public void GenerateProposals_WhenChapterMissing_FlagsWarning()
        {
            string usfm = @"\id GEN
\c 1
\v 1 In the beginning God created the heaven and the earth.";

            var sourceRefs = new List<ParallelReferenceEntry>
            {
                new ParallelReferenceEntry { BookCode = "GEN", Chapter = 5, TargetVerse = 1, RawReference = "(1 Chronicles 1.1-4)" }
            };

            var engine = new ReferenceTransferEngine(new VernacularBookNameManager());
            var proposals = engine.GenerateProposals("GEN", usfm, sourceRefs);

            Assert.Single(proposals);
            Assert.True(proposals[0].HasWarning);
            Assert.False(proposals[0].IsAccepted);
            Assert.Contains("Chapter 5 not found", proposals[0].Warning);
        }

        [Fact]
        public void ApplyProposals_InsertsHeadingAndReferenceAboveParagraphMarker()
        {
            string usfm = @"\id GEN
\c 5
\p
\v 1 This is the book of the generations of Adam.";

            var proposal = new ReferenceTransferProposal
            {
                BookCode = "GEN",
                Chapter = 5,
                Verse = 1,
                ProposedReference = "(1 Chronicles 1:1-4)",
                IsAccepted = true,
                NeedsHeading = true
            };

            var engine = new ReferenceTransferEngine(new VernacularBookNameManager(), new TransferOptions { InsertHeadingIfMissing = true });
            string result = engine.ApplyProposals(usfm, new[] { proposal });

            Assert.Contains("\\s Insert Heading", result);
            Assert.Contains("\\r (1 Chronicles 1:1-4)", result);
            Assert.Contains("\\p", result);

            // Strict structural order: \s Insert Heading, then \r, then \p, then \v 1
            int sIdx = result.IndexOf("\\s Insert Heading");
            int rIdx = result.IndexOf("\\r (1 Chronicles 1:1-4)");
            int pIdx = result.IndexOf("\\p");
            int vIdx = result.IndexOf("\\v 1");

            Assert.True(sIdx < rIdx, "\\s must come before \\r");
            Assert.True(rIdx < pIdx, "\\r must come before \\p (ABOVE paragraph marker)");
            Assert.True(pIdx < vIdx, "\\p must come before \\v 1");
        }

        [Fact]
        public void ApplyProposals_WhenExistingHeadingPresent_DoesNotAddInsertHeadingAndPlacesRAboveP()
        {
            string usfm = @"\id GEN
\c 10
\s The Descendants of Noah's Sons
\p
\v 1 These are the descendants of Noah's sons...";

            var proposal = new ReferenceTransferProposal
            {
                BookCode = "GEN",
                Chapter = 10,
                Verse = 1,
                ProposedReference = "(1 Chronicles 1:5-23)",
                IsAccepted = true,
                NeedsHeading = false
            };

            var engine = new ReferenceTransferEngine(new VernacularBookNameManager(), new TransferOptions { InsertHeadingIfMissing = true });
            string result = engine.ApplyProposals(usfm, new[] { proposal });

            Assert.DoesNotContain("\\s Insert Heading", result);
            Assert.Contains("\\s The Descendants of Noah's Sons", result);
            Assert.Contains("\\r (1 Chronicles 1:5-23)", result);

            int sIdx = result.IndexOf("\\s The Descendants of Noah's Sons");
            int rIdx = result.IndexOf("\\r (1 Chronicles 1:5-23)");
            int pIdx = result.IndexOf("\\p");
            int vIdx = result.IndexOf("\\v 1");

            Assert.True(sIdx < rIdx, "\\s must come before \\r");
            Assert.True(rIdx < pIdx, "\\r must come before \\p");
            Assert.True(pIdx < vIdx, "\\p must come before \\v 1");
        }

        [Fact]
        public void ApplyProposals_WhenRemoveExistingReferencesTrue_ReplacesOldR()
        {
            string usfm = @"\id GEN
\c 10
\s The Descendants of Noah's Sons
\r (1 Chronicles 1:5)
\p
\v 1 These are the descendants of Noah's sons...";

            var proposal = new ReferenceTransferProposal
            {
                BookCode = "GEN",
                Chapter = 10,
                Verse = 1,
                ProposedReference = "(1 Chronicles 1:5-23)",
                IsAccepted = true,
                NeedsHeading = false
            };

            var engine = new ReferenceTransferEngine(new VernacularBookNameManager(), new TransferOptions { RemoveExistingReferences = true });
            string result = engine.ApplyProposals(usfm, new[] { proposal });

            Assert.DoesNotContain("\\r (1 Chronicles 1:5)\r", result + "\r");
            Assert.Contains("\\r (1 Chronicles 1:5-23)", result);

            int rIdx = result.IndexOf("\\r (1 Chronicles 1:5-23)");
            int pIdx = result.IndexOf("\\p");
            Assert.True(rIdx < pIdx, "\\r must be above \\p");
        }

        [Fact]
        public void CheckExistingContext_WhenExactReferenceExists_MarksDuplicate()
        {
            string usfm = @"\id GEN
\c 5
\s Adam's Descendants
\r (1CH 1:1-4)
\v 1 This is the book of the generations of Adam.";

            var sourceRefs = new List<ParallelReferenceEntry>
            {
                new ParallelReferenceEntry { BookCode = "GEN", Chapter = 5, TargetVerse = 1, RawReference = "(1 Chronicles 1:1-4)" }
            };

            var engine = new ReferenceTransferEngine(new VernacularBookNameManager());
            var proposals = engine.GenerateProposals("GEN", usfm, sourceRefs);

            Assert.Single(proposals);
            Assert.True(proposals[0].IsDuplicate);
            Assert.False(proposals[0].IsAccepted);
            Assert.Equal("Adam's Descendants", proposals[0].ExistingHeading);
            Assert.Equal("(1CH 1:1-4)", proposals[0].ExistingReference);
        }
    }
}
