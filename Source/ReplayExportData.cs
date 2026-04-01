using System.Collections.Generic;

namespace Assembly_CSharp.TasInfo.mm.Source {
    internal struct ReplayExportFrame {
        public float x;
        public float y;
        public bool facingRight;
        public string animClip;
        public int animFrame;
    }

    internal sealed class ReplayExportRoomKey {
        public ReplayExportRoomKey(string sceneName, string entryFromScene, string exitToScene) {
            SceneName = sceneName;
            EntryFromScene = entryFromScene;
            ExitToScene = exitToScene;
        }

        public string SceneName { get; private set; }

        public string EntryFromScene { get; private set; }

        public string ExitToScene { get; private set; }
    }

    internal sealed class ReplayExportRoom {
        public ReplayExportRoom(ReplayExportRoomKey key, float totalTime, IList<ReplayExportFrame> frames) {
            Key = key;
            TotalTime = totalTime;
            Frames = new List<ReplayExportFrame>(frames).ToArray();
        }

        public ReplayExportRoomKey Key { get; private set; }

        public float TotalTime { get; private set; }

        public ReplayExportFrame[] Frames { get; private set; }

        public int FrameCount => Frames.Length;
    }
}
