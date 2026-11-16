using System;
using ReferenceTransfer.Core.Engine;
using Xunit;

namespace ReferenceTransfer.Tests
{
    public class ReferenceParserTests
    {
        [Fact]
        public void ParseReferencesText_SingleLineGntdFormat_CalculatesTargetVerse()
        {
            string input = @"GEN 5:0: \r (1 Chronicles 1.1-4)
EXO 18:12: \r (Deuteronomy 1.9-18)
GEN 36:29-30: \r (1 Chronicles 1.43-54)";

            var refs = ReferenceParser.ParseReferencesText(input);

            Assert.Equal(3, refs.Count);

            Assert.Equal("GEN", refs[0].BookCode);
            Assert.Equal(5, refs[0].Chapter);
            Assert.Equal(1, refs[0].TargetVerse); // 0 + 1 = 1
            Assert.Equal("(1 Chronicles 1.1-4)", refs[0].RawReference);

            Assert.Equal("EXO", refs[1].BookCode);
            Assert.Equal(18, refs[1].Chapter);
            Assert.Equal(13, refs[1].TargetVerse); // 12 + 1 = 13

            Assert.Equal("GEN", refs[2].BookCode);
            Assert.Equal(36, refs[2].Chapter);
            Assert.Equal(31, refs[2].TargetVerse); // 30 + 1 = 31
        }

        [Fact]
        public void ParseReferencesText_RegExPalVernacularOutput_ParsesHindiAndMultiReferences()
        {
            string input = @"GEN 5:0: \r (1 इति 1:1-4)
GEN 10:0: \r (1 इति 1:5-23)
GEN 11:9: \r (1 इति 1:24-27)
EXO 17:0: \r (गिन 20:1-13)
EXO 18:12: \r (व्य 1:9-18)
EXO 23:13: \r (निर्ग 34:18-26; व्य 16:1-17)";

            var refs = ReferenceParser.ParseReferencesText(input);

            Assert.Equal(6, refs.Count);

            Assert.Equal("GEN", refs[0].BookCode);
            Assert.Equal(5, refs[0].Chapter);
            Assert.Equal(1, refs[0].TargetVerse);
            Assert.Equal("(1 इति 1:1-4)", refs[0].RawReference);

            Assert.Equal("GEN", refs[2].BookCode);
            Assert.Equal(11, refs[2].Chapter);
            Assert.Equal(10, refs[2].TargetVerse); // 9 + 1 = 10
            Assert.Equal("(1 इति 1:24-27)", refs[2].RawReference);

            Assert.Equal("EXO", refs[3].BookCode);
            Assert.Equal(17, refs[3].Chapter);
            Assert.Equal(1, refs[3].TargetVerse);
            Assert.Equal("(गिन 20:1-13)", refs[3].RawReference);

            Assert.Equal("EXO", refs[5].BookCode);
            Assert.Equal(23, refs[5].Chapter);
            Assert.Equal(14, refs[5].TargetVerse); // 13 + 1 = 14
            Assert.Equal("(निर्ग 34:18-26; व्य 16:1-17)", refs[5].RawReference);
        }

        [Fact]
        public void ParseReferencesText_SplitLineFormat_PairsHeaderAndRef()
        {
            string input = @"GEN 5:0:
(1 Chronicles 1.1-4)
GEN 10:0:
(1 Chronicles 1.5-23)";

            var refs = ReferenceParser.ParseReferencesText(input);

            Assert.Equal(2, refs.Count);
            Assert.Equal("GEN", refs[0].BookCode);
            Assert.Equal(5, refs[0].Chapter);
            Assert.Equal(1, refs[0].TargetVerse);
            Assert.Equal("(1 Chronicles 1.1-4)", refs[0].RawReference);

            Assert.Equal("GEN", refs[1].BookCode);
            Assert.Equal(10, refs[1].Chapter);
            Assert.Equal(1, refs[1].TargetVerse);
        }

        [Fact]
        public void ResourceCatalog_ListsInstalledResourcesWithoutMockCounts()
        {
            var installedSample = new[] { "TAOVBSI", "ESVUS16", "HINOVBSI" };
            var resources = ResourceCatalog.GetAvailableResources(installedSample);
            Assert.Equal(3, resources.Count);

            Assert.Equal("ESVUS16", resources[0].Id);
            Assert.Equal("HINOVBSI", resources[1].Id);
            Assert.Equal("TAOVBSI", resources[2].Id);
            Assert.False(resources[2].HasReferences);
            Assert.Equal(0, resources[2].ReferenceCount);
        }
    }
}
