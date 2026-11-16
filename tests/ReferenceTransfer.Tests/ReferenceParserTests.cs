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

        [Fact]
        public void ExtractReferencesFromUsfm_HindiUsfmText_ExtractsReferencesAndAlignsWithUpcomingVerse()
        {
            string hindiUsfm = @"\id GEN 01-GEN-hin-ov.usfm Hindi OV
\c 5
\s1 आदम की वंशावली
\r (1 इति 1:1-4)
\p
\v 1 आदम की वंशावली की पुस्तक यह है: जिस दिन परमेश्वर ने मनुष्य की सृष्टि की...
\v 2 उसने नर और नारी करके उनकी सृष्टि की;
\c 10
\s1 नूह के पुत्रों की वंशावली
\r (1 इति 1:5-23)
\p
\v 1 नूह के पुत्रों, अर्थात् शेम, हाम, और येपेत की वंशावली यह है...
\c 11
\v 9 इस कारण उस नगर का नाम बाबेल पड़ा...
\s1 शेम की वंशावली
\r (1 इति 1:24-27)
\p
\v 10 शेम की वंशावली यह है: जब शेम एक सौ वर्ष का था...";

            var refs = ReferenceParser.ExtractReferencesFromUsfm(hindiUsfm, "GEN", "HINOVBSI");

            Assert.Equal(3, refs.Count);

            // Gen 5:1
            Assert.Equal("GEN", refs[0].BookCode);
            Assert.Equal(5, refs[0].Chapter);
            Assert.Equal(1, refs[0].TargetVerse);
            Assert.Equal("(1 इति 1:1-4)", refs[0].RawReference);
            Assert.Equal("HINOVBSI", refs[0].SourceType);

            // Gen 10:1
            Assert.Equal("GEN", refs[1].BookCode);
            Assert.Equal(10, refs[1].Chapter);
            Assert.Equal(1, refs[1].TargetVerse);
            Assert.Equal("(1 इति 1:5-23)", refs[1].RawReference);

            // Gen 11:10 (upcoming verse after v9)
            Assert.Equal("GEN", refs[2].BookCode);
            Assert.Equal(11, refs[2].Chapter);
            Assert.Equal(10, refs[2].TargetVerse);
            Assert.Equal("(1 इति 1:24-27)", refs[2].RawReference);
        }

        [Fact]
        public void ExtractReferencesFromUsfm_TextWithoutReferences_ReturnsEmpty()
        {
            string taovbsiUsfm = @"\id GEN Tamil Old Version
\c 1
\p
\v 1 ஆதியிலே தேவன் வானத்தையும் பூமியையும் சிருஷ்டித்தார்.
\v 2 பூமியானது ஒழுங்கின்மையும் வெறுமையுமாய் இருந்தது...";

            var refs = ReferenceParser.ExtractReferencesFromUsfm(taovbsiUsfm, "GEN", "TAOVBSI");

            Assert.Empty(refs);
        }
    }
}
