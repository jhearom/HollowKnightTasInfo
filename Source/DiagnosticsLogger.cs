using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Assembly_CSharp.TasInfo.mm.Source.Utils;
using UnityEngine;

namespace Assembly_CSharp.TasInfo.mm.Source {
    public class DiagnosticsLogger {
        private const int BlockSize = 10000;

        private static DiagnosticsLogger _instance;

        private List<Data[]> _blocks;
        private int _blockIndex;
        private int _dataIndex;
        private int _estRealFrame;
        private string _lastScene;
        private KeyCode[] _keysToMonitor;
        private string[] _keyStrings;

        public static string LastScene => _instance?._lastScene ?? "";

        public const bool OutputCombinedLog = false;

        public static void OnInit() {
            _instance = new DiagnosticsLogger();
            _instance.Awake();
        }

        private void Awake() {
            _keysToMonitor = new[] {
                KeyCode.LeftArrow,
                KeyCode.RightArrow,
                KeyCode.UpArrow,
                KeyCode.DownArrow,
                KeyCode.Z,
                KeyCode.X,
                KeyCode.C,
                KeyCode.A,
                KeyCode.D,
                KeyCode.F,
                KeyCode.Escape,
                KeyCode.I,
                KeyCode.S,
                KeyCode.Tab,
                KeyCode.Return,
                KeyCode.RightBracket,
                KeyCode.LeftBracket
            };
            _keyStrings = new[] {
                "L",
                "R",
                "U",
                "D",
                "z",
                "x",
                "c",
                "a",
                "d",
                "f",
                "Esc",
                "i",
                "s",
                "Tab",
                "Ent",
                "]",
                "["
            };
            _blocks = new List<Data[]>();
            _blocks.Add(new Data[BlockSize]);
            _blockIndex = 0;
            _dataIndex = 0;
            _estRealFrame = 0;
        }

        public static void OnPreRender() {
            _instance.Update();
        }

        public static int EstRealFrame => _instance._estRealFrame;

        private void Update() {
            if (_estRealFrame == 0) {
                _estRealFrame = Mathf.RoundToInt(Time.unscaledTime/Time.unscaledDeltaTime);
            } else {
                _estRealFrame++;
            }

            RecordData();

            if (Input.GetKeyDown(KeyCode.Equals)) {
                DumpLogFile();
                RandomInjection.DumpLogs();
                InputsLogger.DumpLogs();
                MultiSync.WriteSyncRecording();
                ReplayExport.DumpExports();
            }

            if (Input.GetKey(KeyCode.RightBracket)) {
                RandomInjection.RollRngNextScene();
            }

            if (Input.GetKey(KeyCode.Backslash)) {
                GameManager.instance.FreezeMoment(2);
            }

            if (Input.GetKeyDown(KeyCode.Semicolon)) {
                var player = PlayerData.instance;
                player.gotCharm_6 = true;
                player.equippedCharm_6 = true;
                player.equippedCharms.Add(6);
                player.canOvercharm = true;
                player.overcharmed = true;
                if (player.health > 1) {
                    player.TakeHealth(player.health - 1);
                }
                HeroController.instance.proxyFSM.SendEvent("HeroCtrl-HeroDamaged");
            }
        }

        private void RecordData() {
            if (_dataIndex >= BlockSize) {
                _blockIndex++;
                _blocks.Add(new Data[BlockSize]);
                _dataIndex = 0;
            }
            
            var block = _blocks[_blockIndex];
            block[_dataIndex].realFrame = _estRealFrame;
            block[_dataIndex].fixedFrame = Mathf.RoundToInt(Time.fixedTime/Time.fixedDeltaTime);
            block[_dataIndex].realTime = Time.time;
            block[_dataIndex].fixedTime = Time.fixedTime;

            uint keys = 0;
            for (int i = 0; i < _keysToMonitor.Length; i++) {
                if (Input.GetKey(_keysToMonitor[i])) {
                    keys |= (uint)(1 << i);
                }
            }
            block[_dataIndex].inKeys = keys;

            var hero = GameManager.instance.hero_ctrl;
            if (hero) {
                var flags = DataFlags.None;
                if (hero.CanInput()) flags |= DataFlags.CanInput;
                if (hero.cState.wallSliding) flags |= DataFlags.WallSliding;
                if (hero.cState.dashing) flags |= DataFlags.Dashing;
                if (hero.cState.superDashing) flags |= DataFlags.CDashing;
                if (hero.cState.spellQuake) flags |= DataFlags.Diving;
                block[_dataIndex].flags = flags;

                var hTrans = hero.transform;
                block[_dataIndex].posX = hTrans.position.x;
                block[_dataIndex].posY = hTrans.position.y;
                block[_dataIndex].velX = hero.current_velocity.x;
                block[_dataIndex].velY = hero.current_velocity.y;
                block[_dataIndex].rollTimes = RngInfo.rollTimes;
                block[_dataIndex].groundedTime = HeroInfo.GroundedTime;

                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                block[_dataIndex].scene = scene;
                if (scene != _lastScene) {
                    _lastScene = scene;
                    //RandomInjection.NotifyBeginScene(scene);
                }
            }

            _dataIndex++;
        }

        private void DumpLogFile() {
            if (OutputCombinedLog) {
                using (var stream = File.Open("./HollowKnightSyncLog.csv", FileMode.Create, FileAccess.Write)) 
                using (var writer = new StreamWriter(stream)){
                    writer.WriteLine("RF,PF,RT,FT,InE,WC,DSH,CDSH,DIV,X,Y,VX,VY,RNG_Cnt,Scene");
                    for (int i = 0; i < _blocks.Count; i++) {
                        int dataCount = i < _blocks.Count - 1 ? BlockSize : _dataIndex;
                        for (int k = 0; k < dataCount; k++) {
                            var datum = _blocks[i][k];
                            var canInput = FlagChar(datum, DataFlags.CanInput, '1', '0');
                            var wallSliding = FlagChar(datum, DataFlags.WallSliding, '1', '0');
                            var dashing = FlagChar(datum, DataFlags.Dashing, '1', '0');
                            var cDashing = FlagChar(datum, DataFlags.CDashing, '1', '0');
                            var diving = FlagChar(datum, DataFlags.Diving, '1', '0');
                            writer.WriteLine($"{datum.realFrame},{datum.fixedFrame},{datum.realTime},{datum.fixedTime},{canInput},{wallSliding},{dashing},{cDashing},{diving},{datum.posX},{datum.posY},{datum.velX},{datum.velY},{datum.rollTimes},{datum.scene}");
                        }
                    }
                }
            }

            var diagPath = "./Diagnostics";
            if (!Directory.Exists(diagPath))
                Directory.CreateDirectory(diagPath);

            int sceneIndex = 0;
            int realFrameStart = 0;
            int physFrameStart = 0;
            StreamWriter diagWriter = null;
            try {
                string lastScene = null;
                for (int i = 0; i < _blocks.Count; i++) {
                    int dataCount = i < _blocks.Count - 1 ? BlockSize : _dataIndex;
                    for (int k = 0; k < dataCount; k++) {
                        var datum = _blocks[i][k];
                        if (string.IsNullOrEmpty(datum.scene))
                            continue;

                        if (diagWriter == null || datum.scene != lastScene) {
                            lastScene = datum.scene;

                            diagWriter?.Dispose();
                            var diagFile = File.Open($"{diagPath}/S{sceneIndex:00000}_{lastScene}_Diag.csv", FileMode.Create, FileAccess.Write);
                            diagWriter = new StreamWriter(diagFile);
                            diagWriter.WriteLine("RelFrame,RelPhysFrame,GT,T-FT,InE,WC,DSH,CDSH,DIV,X,Y,VX,VY,InK");
                            realFrameStart = datum.realFrame;
                            physFrameStart = datum.fixedFrame;
                            sceneIndex++;
                        }

                        var canInput = FlagChar(datum, DataFlags.CanInput, '1', '0');
                        var wallSliding = FlagChar(datum, DataFlags.WallSliding, '1', '0');
                        var dashing = FlagChar(datum, DataFlags.Dashing, '1', '0');
                        var cDashing = FlagChar(datum, DataFlags.CDashing, '1', '0');
                        var diving = FlagChar(datum, DataFlags.Diving, '1', '0');
                        diagWriter.WriteLine($"{datum.realFrame - realFrameStart},{datum.fixedFrame - physFrameStart},{datum.groundedTime},{datum.realTime - datum.fixedTime},{canInput},{wallSliding},{dashing},{cDashing},{diving},{datum.posX},{datum.posY},{datum.velX},{datum.velY},{InKeys(datum.inKeys)}");
                    }
                }
            } finally {
                diagWriter?.Dispose();
            }
        }

        private char FlagChar(Data datum, DataFlags flag, char trueChar, char falseChar) {
            return (datum.flags & flag) > 0 ? trueChar : falseChar;
        }

        private string InKeys(uint inKeys) {
            var builder = new StringBuilder();
            for (int i = 0; i < _keysToMonitor.Length; i++) {
                if ((inKeys & (uint) (1 << i)) != 0) {
                    builder.Append(_keyStrings[i]);
                }
            }

            return builder.ToString();
        }
        
        [System.Flags]
        private enum DataFlags {
            None = 0,
            CanInput = 0x01,
            WallSliding = 0x02,
            Dashing = 0x04,
            CDashing = 0x08,
            Diving = 0x10
        }

        private struct Data {
            public DataFlags flags;
            public int realFrame;
            public int fixedFrame;
            public float realTime;
            public float fixedTime;
            public float posX;
            public float posY;
            public float velX;
            public float velY;
            public ulong rollTimes;
            public string scene;
            public float groundedTime;
            public uint inKeys;
        }
    }
}
