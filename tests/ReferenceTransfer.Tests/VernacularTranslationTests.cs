using System;
using System.IO;
using ReferenceTransfer.Core.Engine;
using ReferenceTransfer.Core.Models;
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

        [Fact]
        public void TranslateCitation_CrossLanguage_HindiSourceToTamilTarget_RefreshesWithSelectedStyle()
        {
            // Source: Hindi Scripture Reference Settings booknames (as configured in Paratext HINDI-BSI)
            var sourceMgr = new VernacularBookNameManager();
            sourceMgr.RegisterBook("1CH", "1 इति", "1 इतिहास", "इतिहास नामक पुस्तक - पहला भाग");
            sourceMgr.RegisterBook("GEN", "उत्प", "उत्पत्ति", "उत्पत्ति");

            // Target: Tamil Scripture Reference Settings booknames (as configured in Paratext MTEP1)
            var targetMgr = new VernacularBookNameManager();
            targetMgr.RegisterBook("1CH", "1நாளா", "1 நாளாகமம்", "1 நாளாகமம்");
            targetMgr.RegisterBook("GEN", "ஆதி", "ஆதியாகமம்", "ஆதியாகமம்");

            var settings = new ReferenceTransfer.Core.Models.ProjectScriptureSettings
            {
                ChapterVerseSeparator = ":",
                RangeIndicator = "-"
            };

            // Test 1: Translating Hindi citation to Tamil Abbreviation
            string abbrResult = targetMgr.TranslateCitation(
                "(1 इतिहास 1:1-4)", 
                useParentheses: true, 
                cleanPunctuation: true, 
                style: ReferenceTransfer.Core.Models.BookNameStyle.Abbreviation, 
                settings: settings, 
                sourceMgr: sourceMgr);

            Assert.Equal("(1நாளா 1:1-4)", abbrResult);

            // Test 2: Switching style to Short Name
            string shortResult = targetMgr.TranslateCitation(
                "(1 इतिहास 1:1-4)", 
                useParentheses: true, 
                cleanPunctuation: true, 
                style: ReferenceTransfer.Core.Models.BookNameStyle.ShortName, 
                settings: settings, 
                sourceMgr: sourceMgr);

            Assert.Equal("(1 நாளாகமம் 1:1-4)", shortResult);

            // Test 3: Translating Hindi Abbreviation citation to Tamil Long Name
            string longResult = targetMgr.TranslateCitation(
                "(1 इति 1:1-4)", 
                useParentheses: true, 
                cleanPunctuation: true, 
                style: ReferenceTransfer.Core.Models.BookNameStyle.LongName, 
                settings: settings, 
                sourceMgr: sourceMgr);

            Assert.Equal("(1 நாளாகமம் 1:1-4)", longResult);
        }

        [Fact]
        public void ToCanonicalCitation_ConvertsEnglishAndVernacularToStandardBibleCodes()
        {
            var targetMgr = new VernacularBookNameManager();
            targetMgr.RegisterBook("1CH", "1நாளா", "1 நாளாகமம்", "1 நாளாகமம்");

            var sourceMgr = new VernacularBookNameManager();
            sourceMgr.RegisterBook("1CH", "1 इति", "1 इतिहास", "इतिहास नामक पुस्तक - पहला भाग");
            sourceMgr.RegisterBook("EXO", "निर्ग", "निर्गमन", "निर्गमन");
            sourceMgr.RegisterBook("DEU", "व्य", "व्यवस्थाविवरण", "व्यवस्थाविवरण");

            // English citation -> Canonical code
            string canon1 = targetMgr.ToCanonicalCitation("(1 Chronicles 1.1-4)", sourceMgr);
            Assert.Equal("1CH 1:1-4", canon1);

            // Hindi vernacular citation -> Canonical code
            string canon2 = targetMgr.ToCanonicalCitation("(1 इतिहास 1:1-4)", sourceMgr);
            Assert.Equal("1CH 1:1-4", canon2);

            // Multi-reference Hindi citation -> Canonical codes
            string canon3 = targetMgr.ToCanonicalCitation("(निर्गमन 34.18-26; व्यवस्थाविवरण 16.1-17)", sourceMgr);
            Assert.Equal("EXO 34:18-26; DEU 16:1-17", canon3);

            // Tamil vernacular citation -> Canonical code
            string canon4 = targetMgr.ToCanonicalCitation("(1 நாளாகமம் 1:1-4)", sourceMgr);
            Assert.Equal("1CH 1:1-4", canon4);

            // RegEx Pal Hindi raw extract with abbreviations -> Canonical codes
            string canon5 = targetMgr.ToCanonicalCitation("(निर्ग 34:18-26; व्य 16:1-17)", sourceMgr);
            Assert.Equal("EXO 34:18-26; DEU 16:1-17", canon5);
        }

        [Fact]
        public void TranslateCitation_TranslatesHindiAbbreviations_ToTamilTarget()
        {
            var targetMgr = new VernacularBookNameManager();
            targetMgr.RegisterBook("EXO", "யாத்", "யாத்திராகமம்", "யாத்திராகமம்");
            targetMgr.RegisterBook("DEU", "உபா", "உபாகமம்", "உபாகமம்");

            var sourceMgr = new VernacularBookNameManager();
            sourceMgr.RegisterBook("EXO", "निर्ग", "निर्गमन", "निर्गमन");
            sourceMgr.RegisterBook("DEU", "व्य", "व्यवस्थाविवरण", "व्यवस्थाविवरण");

            var settings = new ProjectScriptureSettings
            {
                ChapterVerseSeparator = ":",
                RangeIndicator = "-",
                BookSequenceSeparator = "; "
            };

            string result = targetMgr.TranslateCitation(
                "(निर्ग 34:18-26; व्य 16:1-17)",
                useParentheses: true,
                cleanPunctuation: true,
                style: BookNameStyle.ShortName,
                settings: settings,
                sourceMgr: sourceMgr);

            Assert.Equal("(யாத்திராகமம் 34:18-26; உபாகமம் 16:1-17)", result);
        }

        [Fact]
        public void TranslateCitation_KannadaSourceWithIndicDigits_ToTamilTargetAndCanonical()
        {
            // Target: Tamil MTEP1
            var targetMgr = new VernacularBookNameManager();
            targetMgr.RegisterBook("1CH", "1நாளா", "1 நாளாகமம்", "1 நாளாகமம்");
            targetMgr.RegisterBook("NUM", "எண்", "எண்ணாகமம்", "எண்ணாகமம்");
            targetMgr.RegisterBook("EXO", "யாத்", "யாத்திராகமம்", "யாத்திராகமம்");
            targetMgr.RegisterBook("DEU", "உபா", "உபாகமம்", "உபாகமம்");

            // Source: Kannada KANCLBSI
            var sourceMgr = new VernacularBookNameManager();
            sourceMgr.RegisterBook("1CH", "೧ ಪೂರ್ವ.", "೧ ಪೂರ್ವಕಾಲವೃತ್ತಾಂತಗಳು", "ಮೊದಲನೆಯ ಪೂರ್ವಕಾಲವೃತ್ತಾಂತಗಳು");
            sourceMgr.RegisterBook("NUM", "ಸಂಖ್ಯಾ.", "ಸಂಖ್ಯಾಕಾಂಡ", "ಸಂಖ್ಯಾಕಾಂಡ");
            sourceMgr.RegisterBook("EXO", "ವಿಮೋ.", "ವಿಮೋಚನಕಾಂಡ", "ವಿಮೋಚನಕಾಂಡ");
            sourceMgr.RegisterBook("DEU", "ಧರ್ಮೋ.", "ಧರ್ಮೋಪದೇಶಕಾಂಡ", "ಧರ್ಮೋಪದೇಶಕಾಂಡ");

            var settings = new ProjectScriptureSettings
            {
                ChapterVerseSeparator = ":",
                RangeIndicator = "-",
                BookSequenceSeparator = "; "
            };

            // Test 1: Kannada 1 Chronicles with Kannada digit '೧' and trailing dot
            string canon1 = targetMgr.ToCanonicalCitation("(೧ ಪೂರ್ವ. 1:1-4)", sourceMgr);
            Assert.Equal("1CH 1:1-4", canon1);

            string target1 = targetMgr.TranslateCitation(
                "(೧ ಪೂರ್ವ. 1:1-4)",
                useParentheses: true,
                cleanPunctuation: true,
                style: BookNameStyle.LongName,
                settings: settings,
                sourceMgr: sourceMgr);
            Assert.Equal("(1 நாளாகமம் 1:1-4)", target1);

            // Test 2: Kannada Numbers
            string canon2 = targetMgr.ToCanonicalCitation("(ಸಂಖ್ಯಾ. 20:1-13)", sourceMgr);
            Assert.Equal("NUM 20:1-13", canon2);

            string target2 = targetMgr.TranslateCitation(
                "(ಸಂಖ್ಯಾ. 20:1-13)",
                useParentheses: true,
                cleanPunctuation: true,
                style: BookNameStyle.LongName,
                settings: settings,
                sourceMgr: sourceMgr);
            Assert.Equal("(எண்ணாகமம் 20:1-13)", target2);

            // Test 3: Multi-book Kannada reference (Exodus & Deuteronomy)
            string canon3 = targetMgr.ToCanonicalCitation("(ವಿಮೋ. 34:18-26; ಧರ್ಮೋ. 16:1-17)", sourceMgr);
            Assert.Equal("EXO 34:18-26; DEU 16:1-17", canon3);

            string target3 = targetMgr.TranslateCitation(
                "(ವಿಮೋ. 34:18-26; ಧರ್ಮೋ. 16:1-17)",
                useParentheses: true,
                cleanPunctuation: true,
                style: BookNameStyle.LongName,
                settings: settings,
                sourceMgr: sourceMgr);
            Assert.Equal("(யாத்திராகமம் 34:18-26; உபாகமம் 16:1-17)", target3);
        }
    }
}
