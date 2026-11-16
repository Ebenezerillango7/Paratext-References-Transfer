using System;
using System.IO;
using ReferenceTransfer.Core.Engine;
using Xunit;

namespace ReferenceTransfer.Tests
{
    public class VernacularTranslationTests
    {
        [Fact]
        public void TranslateCitation_StandardEnglishToTamil_TranslatesBookNamesAndPunctuation()
        {
            var mgr = new VernacularBookNameManager();

            string xmlPath = @"D:\Projects\Paratext References Transfer\IRVTamBooknames.xml";
            if (File.Exists(xmlPath))
            {
                mgr.LoadFromXml(xmlPath);
            }

            // Test 1: Simple reference with period in chapter.verse
            string input = "(1 Chronicles 1.1-4)";
            string translated = mgr.TranslateCitation(input, useParentheses: true, cleanPunctuation: true);

            // Should have colon instead of dot: 1:1-4
            Assert.Contains("1:1-4", translated);
            Assert.StartsWith("(", translated);
            Assert.EndsWith(")", translated);

            // If Tamil XML loaded, should translate 1 Chronicles to 1நாளா
            if (mgr.IsLoaded)
            {
                Assert.Contains("1நாளா", translated);
            }
        }

        [Fact]
        public void TranslateCitation_MultipleReferences_ConvertsBothBooks()
        {
            var mgr = new VernacularBookNameManager();
            string xmlPath = @"D:\Projects\Paratext References Transfer\IRVTamBooknames.xml";
            if (File.Exists(xmlPath))
            {
                mgr.LoadFromXml(xmlPath);
            }

            string input = "(Exodus 34.18-26; Deuteronomy 16.1-17)";
            string translated = mgr.TranslateCitation(input, useParentheses: true, cleanPunctuation: true);

            Assert.Contains("34:18-26", translated);
            Assert.Contains("16:1-17", translated);

            if (mgr.IsLoaded)
            {
                Assert.Contains("யாத்", translated);
                Assert.Contains("உபா", translated);
            }
        }

        [Fact]
        public void TranslateCitation_RespectsProjectScriptureSettings_ChapterVerseSeparatorAndRange()
        {
            var mgr = new VernacularBookNameManager();
            string xmlPath = @"D:\Projects\Paratext References Transfer\IRVTamBooknames.xml";
            if (File.Exists(xmlPath))
            {
                mgr.LoadFromXml(xmlPath);
            }

            // Case A: Project with dot (.) separator like MTEP1
            var dotSettings = new ReferenceTransfer.Core.Models.ProjectScriptureSettings
            {
                ChapterVerseSeparator = ".",
                RangeIndicator = "-",
                DefaultMarkerRStyle = ReferenceTransfer.Core.Models.BookNameStyle.ShortName
            };

            string dotResult = mgr.TranslateCitation("(1 Chronicles 1:5-23)", useParentheses: true, cleanPunctuation: true, style: ReferenceTransfer.Core.Models.BookNameStyle.ShortName, settings: dotSettings);
            Assert.Contains("1.5-23", dotResult);
            if (mgr.IsLoaded)
            {
                Assert.Contains("1 நாளாகமம் 1.5-23", dotResult);
            }

            // Case B: Project with colon (:) separator and en-dash (–) range like Bga
            var colonSettings = new ReferenceTransfer.Core.Models.ProjectScriptureSettings
            {
                ChapterVerseSeparator = ":",
                RangeIndicator = "–"
            };

            string colonResult = mgr.TranslateCitation("(1 Chronicles 1.5-23)", useParentheses: true, cleanPunctuation: true, style: ReferenceTransfer.Core.Models.BookNameStyle.Abbreviation, settings: colonSettings);
            Assert.Contains("1:5–23", colonResult);

            // Case C: Real MTEP1 Settings.xml loading
            string mtep1SettingsPath = @"D:\Transdata\My Paratext 9 Projects\MTEP1\Settings.xml";
            if (File.Exists(mtep1SettingsPath))
            {
                var loaded = ReferenceTransfer.Core.Models.ProjectScriptureSettings.LoadFromSettingsXml(mtep1SettingsPath);
                Assert.True(loaded.IsLoaded);
                Assert.Equal(":", loaded.ChapterVerseSeparator);
                Assert.Equal("–", loaded.RangeIndicator);
                Assert.Equal(ReferenceTransfer.Core.Models.BookNameStyle.ShortName, loaded.DefaultMarkerRStyle);
            }
        }
    }
}
