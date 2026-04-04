using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace Assembly_CSharp.TasInfo.mm.Source {
    internal static class SplitReader {
        public static List<Split> SplitList { get; } = new();
        public static bool ReadSplits { get; private set; }
        public static int Generation { get; private set; }

        private static string loadedPath = string.Empty;
        private static DateTime loadedWriteTime = DateTime.MinValue;

        public static void OnInit() {
            TryReload(force: true);
        }

        public static void OnPreRender() {
            TryReload(force: false);
        }

        private static void TryReload(bool force) {
            string splitPath = ConfigManager.SplitFileLocation ?? string.Empty;
            DateTime writeTime = File.Exists(splitPath) ? File.GetLastWriteTime(splitPath) : DateTime.MinValue;

            if (!force && splitPath == loadedPath && writeTime == loadedWriteTime) {
                return;
            }

            loadedPath = splitPath;
            loadedWriteTime = writeTime;
            SplitList.Clear();

            if (!File.Exists(splitPath)) {
                ReadSplits = false;
                Generation++;
                return;
            }

            XmlDocument doc = new();
            using (XmlTextReader reader = new(splitPath)) {
                doc.Load(reader);
            }

            XmlNodeList segmentNames = doc.SelectNodes("Run/Segments/Segment/Name");
            XmlNodeList segmentTriggers = doc.SelectNodes("Run/AutoSplitterSettings/Splits/Split");

            List<SplitName> splitNames = new();

            if (segmentTriggers != null) {
                foreach (XmlNode node in segmentTriggers) {
                    try {
                        splitNames.Add((SplitName)Enum.Parse(typeof(SplitName), node.InnerText));
                    } catch {
                        splitNames.Add(SplitName.ManualSplit);
                    }
                }
            }

            if (segmentNames != null) {
                for (int i = 0; i < segmentNames.Count; i++) {
                    if (i >= splitNames.Count) {
                        SplitList.Add(new Split(segmentNames[i].InnerText, SplitName.ManualSplit));
                    } else {
                        SplitList.Add(new Split(segmentNames[i].InnerText, splitNames[i]));
                    }
                }
            }

            ReadSplits = SplitList.Count > 0;
            Generation++;
        }
    }
}
