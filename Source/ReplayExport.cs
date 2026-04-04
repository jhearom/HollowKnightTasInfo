using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Assembly_CSharp.TasInfo.mm.Source.Utils;
using GlobalEnums;
using UnityEngine;

namespace Assembly_CSharp.TasInfo.mm.Source {
    internal static class ReplayExport {
        internal const string ExportFolder = "./Recording/ReplayTimerMod";
        private const string MenuTitle = "Menu_Title";
        private const string QuitToMenu = "Quit_To_Menu";
        private const float MaxRoomTime = 180f;
        private static readonly BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo HeroAnimCtrlField = typeof(HeroController).GetField("animCtrl", AnyInstance);
        private static readonly PropertyInfo HeroAnimCtrlAnimatorProperty = typeof(HeroAnimationController).GetProperty("Animator", AnyInstance);
        private static readonly FieldInfo HeroAnimCtrlAnimatorField = typeof(HeroAnimationController).GetField("animator", AnyInstance);

        private static readonly List<ReplayExportRoom> CompletedRooms = new();
        private static readonly List<ReplayExportFrame> ActiveFrames = new();

        private static bool _initialized;
        private static bool _recording;
        private static bool _pendingGateTransition;
        private static bool _pendingDeath;
        private static bool _lookForTeleport;
        private static tk2dSpriteAnimator _cachedAnimator;
        private static string _currentScene = string.Empty;
        private static string _entryFromScene = string.Empty;
        private static string _lastSceneName = string.Empty;
        private static float _roomTime;
        private static float _sampleAccumulator;
        private static GameState _previousGameState = GameState.PLAYING;
        private static int _lastPreRenderFrame = -1;

        public static void Init() {
            if (_initialized) {
                return;
            }

            _initialized = true;
            _lastSceneName = GetCurrentSceneName();
            HookUtils.HookEnter<GameManager, Action<GameManager, float>>(nameof(GameManager.PlayerDead), OnPlayerDead);
#if V1432
            HookUtils.HookEnter<GameManager, Action<GameManager, GameManager.SceneLoadInfo>>(nameof(GameManager.BeginSceneTransition), OnBeginSceneTransition);
#endif
            HookUtils.HookExit<HeroController, Action<HeroController>>("Update", OnHeroUpdate);
        }

        private static void StartRoom(string sceneName, string entryFromScene) {
            _recording = true;
            _currentScene = sceneName;
            _entryFromScene = entryFromScene;
            _roomTime = 0f;
            ActiveFrames.Clear();
            _cachedAnimator = null;
            // Match ReplayTimerMod's "capture immediately on the first counted tick" behavior.
            _sampleAccumulator = ReplayExportCodec.RecordInterval;
        }

        private static void FinishRoom(string exitToScene) {
            _recording = false;
            if (ActiveFrames.Count == 0) {
                ResetActiveRoom();
                return;
            }

            CompletedRooms.Add(new ReplayExportRoom(
                new ReplayExportRoomKey(_currentScene, _entryFromScene, exitToScene),
                _roomTime,
                ActiveFrames));

            ResetActiveRoom();
        }

        private static void DiscardActiveRoom() {
            _recording = false;
            ResetActiveRoom();
        }

        private static void ResetActiveRoom() {
            _currentScene = string.Empty;
            _entryFromScene = string.Empty;
            _roomTime = 0f;
            _sampleAccumulator = 0f;
            _cachedAnimator = null;
            _lastPreRenderFrame = -1;
            ActiveFrames.Clear();
        }

        private static void OnPlayerDead(GameManager gameManager, float waitTime) {
            _pendingDeath = true;
            DiscardActiveRoom();
            _pendingGateTransition = false;
        }

#if V1432
        private static void OnBeginSceneTransition(GameManager gameManager, GameManager.SceneLoadInfo sceneLoadInfo) {
            if (sceneLoadInfo == null) {
                return;
            }

            if (_pendingDeath) {
                _pendingDeath = false;
                return;
            }

            _pendingGateTransition = true;
        }
#endif

        private static void OnHeroUpdate(HeroController heroController) {
            UpdateSceneState();
        }

        public static void OnPreRender() {
            if (_lastPreRenderFrame == Time.frameCount) {
                return;
            }

            _lastPreRenderFrame = Time.frameCount;
            UpdateSceneState();

            if (!_recording) {
                return;
            }

            HeroController heroController = HeroController.instance;
            if (heroController == null || heroController.gameObject == null) {
                return;
            }

            if (!ShouldCountRecordingTime()) {
                return;
            }

            float roomDt = Time.unscaledDeltaTime;
            if (roomDt > 0f) {
                _roomTime += roomDt;
            }

            float sampleDt = Time.deltaTime;
            if (sampleDt <= 0f) {
                return;
            }

            _sampleAccumulator += sampleDt;

            while (_sampleAccumulator >= ReplayExportCodec.RecordInterval) {
                _sampleAccumulator -= ReplayExportCodec.RecordInterval;
                ActiveFrames.Add(CaptureFrame(heroController));
            }
        }

        private static void UpdateSceneState() {
            string currentSceneName = GetCurrentSceneName();
            if (string.IsNullOrEmpty(currentSceneName) || string.Equals(currentSceneName, _lastSceneName, StringComparison.Ordinal)) {
                return;
            }

            string previousSceneName = _lastSceneName;
            _lastSceneName = currentSceneName;
            OnSceneChanged(previousSceneName, currentSceneName);
        }

        private static void OnSceneChanged(string fromName, string toName) {
            bool arrivedViaGate = _pendingGateTransition;
            _pendingGateTransition = false;

            if (fromName == MenuTitle || fromName == QuitToMenu) {
                arrivedViaGate = false;
            }

            bool toMenu = toName == MenuTitle || toName == QuitToMenu;
            bool isOverTime = _roomTime > MaxRoomTime;

            if (_recording) {
                if (arrivedViaGate && !isOverTime && !toMenu) {
                    FinishRoom(toName);
                } else {
                    DiscardActiveRoom();
                }
            }

            if (arrivedViaGate && !toMenu && ConfigManager.RecordReplayExport) {
                StartRoom(toName, fromName);
            }
        }

        private static ReplayExportFrame CaptureFrame(HeroController heroController) {
            Vector3 position = heroController.transform.position;
            bool facingRight = heroController.transform.localScale.x > 0f;

            if (_cachedAnimator == null) {
                _cachedAnimator = ResolveHeroAnimator(heroController);
            }

            string clipName = string.Empty;
            int clipFrame = 0;
            try {
                if (_cachedAnimator != null && _cachedAnimator.CurrentClip != null) {
                    clipName = _cachedAnimator.CurrentClip.name;
                    clipFrame = _cachedAnimator.CurrentFrame;
                } else {
                    _cachedAnimator = ResolveHeroAnimator(heroController);
                    if (_cachedAnimator != null && _cachedAnimator.CurrentClip != null) {
                        clipName = _cachedAnimator.CurrentClip.name;
                        clipFrame = _cachedAnimator.CurrentFrame;
                    }
                }
            } catch {
                _cachedAnimator = null;
            }

            return new ReplayExportFrame {
                x = position.x,
                y = position.y,
                facingRight = facingRight,
                animClip = clipName,
                animFrame = clipFrame
            };
        }

        private static tk2dSpriteAnimator ResolveHeroAnimator(HeroController heroController) {
            if (heroController == null) {
                return null;
            }

            try {
                HeroAnimationController heroAnimationController = HeroAnimCtrlField?.GetValue(heroController) as HeroAnimationController
                                                                  ?? heroController.GetComponent<HeroAnimationController>()
                                                                  ?? heroController.GetComponentInChildren<HeroAnimationController>();
                if (heroAnimationController != null) {
                    tk2dSpriteAnimator heroAnimator = HeroAnimCtrlAnimatorProperty?.GetValue(heroAnimationController, null) as tk2dSpriteAnimator
                                                     ?? HeroAnimCtrlAnimatorField?.GetValue(heroAnimationController) as tk2dSpriteAnimator;
                    if (heroAnimator != null) {
                        return heroAnimator;
                    }
                }
            } catch {
                // Fall through to generic component lookup.
            }

            tk2dSprite heroSprite = heroController.GetComponent<tk2dSprite>()
                                    ?? heroController.GetComponentInChildren<tk2dSprite>();

            if (heroSprite != null) {
                tk2dSpriteAnimator spriteAnimator = heroSprite.GetComponent<tk2dSpriteAnimator>()
                                                  ?? heroSprite.GetComponentInParent<tk2dSpriteAnimator>()
                                                  ?? heroSprite.GetComponentInChildren<tk2dSpriteAnimator>();
                if (spriteAnimator != null) {
                    return spriteAnimator;
                }
            }

            return heroController.GetComponent<tk2dSpriteAnimator>()
                   ?? heroController.GetComponentInChildren<tk2dSpriteAnimator>();
        }

        private static bool ShouldCountRecordingTime() {
            if (ConfigManager.PauseTimer) {
                return false;
            }

            GameManager gameManager = GameManager.instance;
            if (gameManager?.ui == null || gameManager.inputHandler == null) {
                return false;
            }

            UIState uiState = gameManager.ui.uiState;
            string sceneName = gameManager.GetSceneNameString();
            string nextSceneName = gameManager.nextSceneName;
            GameState gameState = gameManager.gameState;

            bool loadingMenu =
                sceneName != MenuTitle && string.IsNullOrEmpty(nextSceneName)
                || sceneName != MenuTitle && nextSceneName == MenuTitle
                || sceneName == QuitToMenu;

            if (gameState == GameState.PLAYING && _previousGameState == GameState.MAIN_MENU) {
                _lookForTeleport = true;
            }

            if (_lookForTeleport && gameState != GameState.PLAYING && gameState != GameState.ENTERING_LEVEL) {
                _lookForTeleport = false;
            }

            bool acceptingInput = gameManager.inputHandler.acceptingInput;

            HeroTransitionState heroTransitionState;
            try {
                heroTransitionState = gameManager.hero_ctrl.transitionState;
            } catch {
                heroTransitionState = HeroTransitionState.WAITING_TO_TRANSITION;
            }

            bool isGameTimePaused =
                _lookForTeleport
                || (gameState == GameState.PLAYING || gameState == GameState.ENTERING_LEVEL) && uiState != UIState.PLAYING
                || gameState != GameState.PLAYING && !acceptingInput
                || gameState == GameState.EXITING_LEVEL || gameState == GameState.LOADING
                || heroTransitionState == HeroTransitionState.WAITING_TO_ENTER_LEVEL
                || uiState != UIState.PLAYING
                   && (loadingMenu || uiState != UIState.PAUSED && !string.IsNullOrEmpty(nextSceneName))
                   && nextSceneName != sceneName;

            _previousGameState = gameState;

            return !isGameTimePaused;
        }

        private static string GetCurrentSceneName() {
            GameManager gameManager = GameManager.instance;
            if (gameManager != null && !string.IsNullOrEmpty(gameManager.sceneName)) {
                return gameManager.sceneName;
            }

            try {
                return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? string.Empty;
            } catch {
                return string.Empty;
            }
        }

        public static void DumpExports() {
            if (CompletedRooms.Count == 0) {
                return;
            }

            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string exportRoot = Path.Combine(ExportFolder, "Dump_" + timestamp);
            try {
                List<ReplayExportRoom> roomsToDump = new List<ReplayExportRoom>(CompletedRooms);

                Directory.CreateDirectory(exportRoot);

                string roomsDir = Path.Combine(exportRoot, "Rooms");
                Directory.CreateDirectory(roomsDir);

                using (var manifest = new StreamWriter(File.Open(Path.Combine(exportRoot, "manifest.txt"), FileMode.Create, FileAccess.Write, FileShare.Read))) {
                    manifest.WriteLine("ReplayTimerMod export dump");
                    manifest.WriteLine("UTC: " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                    manifest.WriteLine("RoomCount: " + roomsToDump.Count.ToString(CultureInfo.InvariantCulture));
                    manifest.WriteLine("RTMDataDir: " + Path.Combine("ReplayMod", "data"));
                    manifest.WriteLine();
                    manifest.Flush();

                    for (int i = 0; i < roomsToDump.Count; i++) {
                        ReplayExportRoom room = roomsToDump[i];
                        string roomFile = string.Format(
                            CultureInfo.InvariantCulture,
                            "{0:D4}_{1}_{2}_to_{3}_{4:0.000}s.rtm3.txt",
                            i + 1,
                            Sanitize(room.Key.SceneName),
                            Sanitize(room.Key.EntryFromScene),
                            Sanitize(room.Key.ExitToScene),
                            room.TotalTime);

                        string encoded = ReplayExportCodec.EncodeRoom(room);
                        File.WriteAllText(Path.Combine(roomsDir, roomFile), encoded);
                        manifest.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "{0:D4}: {1} [{2} -> {3}]  time={4:0.000}s  frames={5}  file={6}",
                            i + 1,
                            room.Key.SceneName,
                            room.Key.EntryFromScene,
                            room.Key.ExitToScene,
                            room.TotalTime,
                            room.FrameCount,
                            roomFile));
                        manifest.Flush();
                    }
                }

                string collection = ReplayExportCodec.EncodeCollection(roomsToDump);
                File.WriteAllText(Path.Combine(exportRoot, "ReplayCollection.rtmc.txt"), collection);

                string rtmDataDir = Path.Combine(Path.Combine(exportRoot, "ReplayMod"), "data");
                Directory.CreateDirectory(rtmDataDir);
                foreach (IGrouping<string, ReplayExportRoom> sceneGroup in roomsToDump.GroupBy(room => room.Key.SceneName)) {
                    string sceneJsonPath = Path.Combine(rtmDataDir, sceneGroup.Key + ".json");
                    File.WriteAllText(sceneJsonPath, ReplayExportCodec.SerializeSceneJson(sceneGroup.ToList()));
                }

                CompletedRooms.Clear();
            } catch (Exception ex) {
                Debug.LogException(ex);
            }
        }

        private static string Sanitize(string value) {
            if (string.IsNullOrEmpty(value)) {
                return "Unknown";
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = value.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(chars);
        }
    }
}
