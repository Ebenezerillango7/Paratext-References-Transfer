using System;
using System.Collections.Generic;
using Paratext.PluginInterfaces;
using ReferenceTransfer.Plugin.UI;

namespace ReferenceTransfer.Plugin
{
    public class ReferenceTransferPlugin : IParatextStandalonePlugin
    {
        public const string PluginName = "Parallel References Transfer";

        public string Name => PluginName;
        public Version Version => new Version(2, 0, 0);
        public string VersionString => "2.0.0";
        public string Publisher => "Ebenezer Illango";

        public string GetDescription(string locale)
        {
            return "Transfers parallel passage references into target projects with vernacular book name translation and safe USFM integration.";
        }

        public IDataFileMerger? GetMerger(IPluginHost host, string dataIdentifier) => null;

        public IEnumerable<PluginMenuEntry> PluginMenuEntries
        {
            get
            {
                yield return new PluginMenuEntry(
                    "Transfer Parallel Passage References...",
                    RunPlugin,
                    PluginMenuLocation.ScrTextTools,
                    imagePath: null,
                    customLocation: null);
            }
        }

        private void RunPlugin(IPluginHost host, IParatextChildState? childState)
        {
            var window = new TransferMainWindow(host, childState, this);
            window.Show();
        }
    }
}
