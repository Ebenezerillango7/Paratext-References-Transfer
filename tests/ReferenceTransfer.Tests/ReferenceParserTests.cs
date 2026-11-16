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
        public void ResourceCatalog_LoadsEmbeddedResources()
        {
            var installedSample = new[] { "TAOVBSI", "ESVUS16", "GNTD", "HINOVBSI" };
            var resources = ResourceCatalog.GetAvailableResources(installedSample);
            Assert.NotEmpty(resources);

            var taov = ResourceCatalog.LoadResource("TAOVBSI");
            Assert.Equal(600, taov.Count);

            var gntd = ResourceCatalog.LoadResource("GNTD");
            Assert.Equal(272, gntd.Count);
        }
    }
}
