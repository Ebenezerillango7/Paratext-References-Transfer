using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ReferenceTransfer.Core.Models;

namespace ReferenceTransfer.Core.Engine
{
    public class ResourceItemInfo
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int ReferenceCount { get; set; }
        public bool HasReferences { get; set; }
        public bool IsInstalled { get; set; }

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// Manages discovery and retrieval of real installed Paratext DBL resources.
    /// Strictly operates on actual installed resources without hardcoded presets.
    /// </summary>
    public static class ResourceCatalog
    {
        private static readonly Dictionary<string, (string ResourceFile, int Count)> EmbeddedResources =
            new Dictionary<string, (string, int)>(StringComparer.OrdinalIgnoreCase)
            {
                ["TAOVBSI"] = ("ReferenceTransfer.Core.ResourceData.TAOVBSI_References.txt", 600),
                ["ESVUS16"] = ("ReferenceTransfer.Core.ResourceData.ESVUS16_References.txt", 600),
                ["ESV"] = ("ReferenceTransfer.Core.ResourceData.ESVUS16_References.txt", 600),
                ["HINOVBSI"] = ("ReferenceTransfer.Core.ResourceData.HINOVBSI_References.txt", 272),
                ["GNTD"] = ("ReferenceTransfer.Core.ResourceData.GNTD_References.txt", 272),
                ["IRVHIN"] = ("ReferenceTransfer.Core.ResourceData.IRVHin_References.txt", 272),
            };

        private static readonly Dictionary<string, (string FullName, string Language)> DblKnownCatalog =
            new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
            {
                ["TAOVBSI"] = ("Tamil OV Reference Bible (BSI)", "Tamil"),
                ["ESVUS16"] = ("English Standard Version 2016", "English"),
                ["ESVUS11"] = ("English Standard Version 2011", "English"),
                ["HINOVBSI"] = ("Hindi OV (Re-edited) Bible (BSI)", "Hindi"),
                ["HINCLBSI"] = ("Hindi Common Language Bible (BSI)", "Hindi"),
                ["GNTD"] = ("Good News Translation US", "English"),
                ["GNBDC"] = ("Good News Bible (British Version) 2017", "English"),
                ["IRVHIN"] = ("Indian Revised Version (IRV) Hindi", "Hindi"),
                ["IRVTAM"] = ("Indian Revised Version (IRV) Tamil", "Tamil"),
                ["IRVKAN"] = ("Indian Revised Version (IRV) Kannada", "Kannada"),
                ["IRVTEL"] = ("Indian Revised Version (IRV) Telugu", "Telugu"),
                ["TELUBSI"] = ("Telugu OV Bible (BSI)", "Telugu"),
                ["KANCLBSI"] = ("Kannada Common Language Bible (BSI)", "Kannada"),
                ["KANJVBSI"] = ("Kannada J.V. Bible (BSI)", "Kannada"),
                ["MALOVBSI"] = ("Malayalam OV Bible (BSI)", "Malayalam"),
                ["GUJOVBSI"] = ("Gujarati OV Reference Bible (BSI)", "Gujarati"),
                ["ODIAOVBSI"] = ("Odia OV (Re-edited) (BSI)", "Odia"),
                ["ODCLBSI"] = ("Odia Common Language New Testament (BSI)", "Odia"),
                ["KJV"] = ("King James Version (Paragraph Edition)", "English"),
                ["NIV11UK"] = ("New International Version (Anglicised 2011)", "English"),
                ["NIV84"] = ("New International Version 1984", "English"),
                ["NLT"] = ("New Living Translation", "English"),
                ["NLT04"] = ("New Living Translation 2nd Edition 2004", "English"),
                ["NASB"] = ("New American Standard Bible 2020", "English"),
                ["NASB1995"] = ("New American Standard Bible 1995", "English"),
                ["NET08"] = ("New English Translation (NET Bible)", "English"),
                ["NIrV"] = ("New International Reader's Version 2014", "English"),
                ["NRSV"] = ("New Revised Standard Version 1989", "English"),
                ["RSV"] = ("Revised Standard Version 1952", "English"),
                ["CEVUS06"] = ("CEV Revised 2006 (Global CEV)", "English"),
                ["EASY"] = ("EasyEnglish Bible 2024", "English"),
                ["FBV"] = ("Free Bible Version", "English"),
                ["GERV"] = ("Gujarati Pavitra Baibal", "Gujarati"),
                ["GMTelSB"] = ("GM Study Bible Telugu", "Telugu"),
                ["BYZ"] = ("Robinson-Pierpont Byzantine New Testament", "Ancient Greek"),
                ["GRCTR"] = ("Greek Textus Receptus", "Ancient Greek"),
                ["LXA"] = ("Septuagint (Alternative Texts)", "Ancient Greek"),
                ["SANMAL"] = ("Sanskrit Bible (NT) in Malayalam Script", "Sanskrit"),
                ["MSBU"] = ("Myanmar Standard Bible (Unicode)", "Burmese")
            };

        /// <summary>
        /// Returns ONLY installed Paratext resources. Zero hardcoded presets.
        /// </summary>
        public static List<ResourceItemInfo> GetAvailableResources(IEnumerable<string>? installedResourceIds)
        {
            var list = new List<ResourceItemInfo>();
            if (installedResourceIds == null) return list;

            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawId in installedResourceIds)
            {
                if (string.IsNullOrWhiteSpace(rawId)) continue;
                string id = rawId.Trim();
                if (seenIds.Contains(id)) continue;
                seenIds.Add(id);

                bool hasMeta = DblKnownCatalog.TryGetValue(id, out var meta);
                string fullName = hasMeta ? meta.FullName : id;
                string language = hasMeta ? meta.Language : string.Empty;

                bool hasRefs = EmbeddedResources.TryGetValue(id, out var refInfo);
                int refCount = hasRefs ? refInfo.Count : 0;

                string display;
                if (hasRefs)
                {
                    display = $"{id} - {fullName} ({refCount} references ready)";
                }
                else if (!string.IsNullOrEmpty(language))
                {
                    display = $"{id} - {fullName} ({language})";
                }
                else
                {
                    display = $"{id} - Installed Resource";
                }

                list.Add(new ResourceItemInfo
                {
                    Id = id,
                    FullName = fullName,
                    Language = language,
                    DisplayName = display,
                    HasReferences = hasRefs,
                    ReferenceCount = refCount,
                    IsInstalled = true
                });
            }

            // Order so that resources with ready-to-transfer references appear at top, then alphabetical
            return list.OrderByDescending(r => r.HasReferences)
                       .ThenBy(r => r.Id)
                       .ToList();
        }

        public static List<ParallelReferenceEntry> LoadResource(string resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
                return new List<ParallelReferenceEntry>();

            if (EmbeddedResources.TryGetValue(resourceId, out var info))
            {
                string text = ReadEmbeddedResource(info.ResourceFile);
                if (!string.IsNullOrEmpty(text))
                {
                    return ReferenceParser.ParseReferencesText(text, resourceId);
                }
            }

            // Check if there is an exact or prefix match
            foreach (var kvp in EmbeddedResources)
            {
                if (string.Equals(kvp.Key, resourceId, StringComparison.OrdinalIgnoreCase) ||
                    resourceId.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    string text = ReadEmbeddedResource(kvp.Value.ResourceFile);
                    if (!string.IsNullOrEmpty(text))
                    {
                        return ReferenceParser.ParseReferencesText(text, resourceId);
                    }
                }
            }

            return new List<ParallelReferenceEntry>();
        }

        public static string ReadEmbeddedResource(string fullResourceName)
        {
            var asm = typeof(ResourceCatalog).Assembly;
            using (var stream = asm.GetManifestResourceStream(fullResourceName))
            {
                if (stream == null) return string.Empty;
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
