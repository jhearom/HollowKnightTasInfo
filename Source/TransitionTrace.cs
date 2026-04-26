using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Assembly_CSharp.TasInfo.mm.Source.Utils;
using GlobalEnums;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace Assembly_CSharp.TasInfo.mm.Source {
    internal static class TransitionTrace {
#if V1432
        private const string TracePath = "./Diagnostics/TransitionTrace.log";
        private const int RecentFrameSampleCapacity = 4;
        private const int PostEnterFrameTraceWindow = 180;
        private static readonly KeyCode[] TrackedInputKeys = {
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
        private static readonly string[] TrackedInputKeyStrings = {
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

        private static StreamWriter _writer;
        private static GameManager _subscribedGameManager;
        private static string _lastSceneName;
        private static string _lastNextSceneName;
        private static bool? _lastCanInput;
        private static bool? _lastInSceneTransition;
        private static bool? _lastHasFinishedEnteringScene;
        private static bool? _lastPlayerInvincible;
        private static bool? _lastHeroInvulnerable;
        private static DamageMode? _lastDamageMode;
        private static string _lastHeroState;
        private static string _lastTransitionState;
        private static string _lastUnityActiveSceneName;
        private static int _lastUnitySceneCount = -1;
        private static int _lastObservedFixedFrame = -1;
        private static int _lastSeenFixedFrame = -1;
        private static int _lastRenderFrameWithFixedAdvance = -1;
        private static readonly string[] _recentFrameSamples = new string[RecentFrameSampleCapacity];
        private static int _recentFrameSampleCount;
        private static int _recentFrameSampleStart;
        private static int _currentTransitionId;
        private static string _currentTransitionTargetScene;
        private static int _transitionStartFrame = -1;
        private static int _transitionStartFixedFrame = -1;
        private static int _sceneLoadBeginFrame = -1;
        private static int _sceneLoadBeginFixedFrame = -1;
        private static int _sceneLoadFetchAllowedFrame = -1;
        private static int _sceneLoadFetchAllowedFixedFrame = -1;
        private static int _sceneLoadActivationAllowedFrame = -1;
        private static int _sceneLoadActivationAllowedFixedFrame = -1;
        private static int _unitySceneLoadedFrame = -1;
        private static int _unitySceneLoadedFixedFrame = -1;
        private static int _sceneChangedFrame = -1;
        private static int _sceneChangedFixedFrame = -1;
        private static int _heroEnterSceneFrame = -1;
        private static int _heroEnterSceneFixedFrame = -1;
        private static int _invincibilityEndFrame = -1;
        private static int _invincibilityEndFixedFrame = -1;
        private static int _acceptInputFrame = -1;
        private static int _acceptInputFixedFrame = -1;
        private static int _inputEnabledFrame = -1;
        private static int _inputEnabledFixedFrame = -1;
        private static int _moveEnabledFrame = -1;
        private static int _moveEnabledFixedFrame = -1;
        private static int _jumpEnabledFrame = -1;
        private static int _jumpEnabledFixedFrame = -1;
        private static int _dashEnabledFrame = -1;
        private static int _dashEnabledFixedFrame = -1;
        private static int _attackEnabledFrame = -1;
        private static int _attackEnabledFixedFrame = -1;
        private static int _superDashEnabledFrame = -1;
        private static int _superDashEnabledFixedFrame = -1;
        private static bool _hooksInstalled;
        private static int _lastObservedFrame = -1;
        private static bool _sceneEventsSubscribed;
        private static bool _sceneTransitionBeganSubscribed;
        private static SceneLoad _currentSceneLoad;
        private static readonly MethodInfo CanJumpMethod = typeof(HeroController).GetMethod("CanJump", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo CanDashMethod = typeof(HeroController).GetMethod("CanDash", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo CanAttackMethod = typeof(HeroController).GetMethod("CanAttack", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo CanWallJumpMethod = typeof(HeroController).GetMethod("CanWallJump", BindingFlags.NonPublic | BindingFlags.Instance);
#endif

        public static void Init(GameManager gameManager) {
#if V1432
            if (!_hooksInstalled) {
                _hooksInstalled = true;
                HookUtils.HookEnter<GameManager, Action<GameManager, GameManager.SceneLoadInfo>>(nameof(GameManager.BeginSceneTransition), OnBeginSceneTransition);
                HookUtils.HookEnter<GameManager, Action<GameManager>>(nameof(GameManager.FinishedEnteringScene), OnGameManagerFinishedEnteringScene);
                HookUtils.HookEnter<HeroController, Action<HeroController, TransitionPoint, float>>("EnterScene", OnHeroEnterScene);
                HookUtils.HookEnter<HeroController, Action<HeroController, GatePosition?>>("LeaveScene", OnHeroLeaveScene);
                HookUtils.HookEnter<HeroController, Action<HeroController>>("AcceptInput", OnHeroAcceptInput);
                HookUtils.HookExit<HeroController, Action<HeroController>>("AcceptInput", OnHeroAcceptInputExit);
                HookUtils.HookEnter<HeroController, Action<HeroController, bool, bool>>("FinishedEnteringScene", OnHeroFinishedEnteringScene);
                HookUtils.HookExit<HeroController, Action<HeroController, bool, bool>>("FinishedEnteringScene", OnHeroFinishedEnteringSceneExit);
                HookUtils.HookExit<HeroController, Action<HeroController>>("RegainControl", OnHeroRegainControlExit);
                HookUtils.HookEnter<HeroController, Action<HeroController>>("SetSuperDashExit", OnHeroSetSuperDashExit);
                HookUtils.HookEnter<HeroController, Action<HeroController>>("CancelSuperDash", OnHeroCancelSuperDash);
                HookUtils.HookEnter<HeroController, Action<HeroController>>("HeroDash", OnHeroDash);
                HookUtils.HookEnter<HeroController, Action<HeroController>>("HeroJump", OnHeroJump);
                HookUtils.HookEnter<HeroController, Action<HeroController>>("DoAttack", OnHeroDoAttack);
                HookUtils.HookEnter<SceneLoad, Action<SceneLoad>>("Begin", OnSceneLoadBegin);
                HookUtils.HookEnter<SceneLoad, Action<SceneLoad, bool>>("set_IsFetchAllowed", OnSceneLoadSetFetchAllowed);
                HookUtils.HookEnter<SceneLoad, Action<SceneLoad, bool>>("set_IsActivationAllowed", OnSceneLoadSetActivationAllowed);
            }

            SubscribeToUnitySceneEvents();
            SubscribeToSceneTransitionBegan();
            SubscribeToGameManager(gameManager);
#endif
        }

        public static void OnPreRender(GameManager gameManager) {
#if V1432
            SubscribeToGameManager(gameManager);

            if (!ConfigManager.TraceTransitions) {
                CloseWriter();
                return;
            }

            if (_lastObservedFrame == Time.frameCount) {
                return;
            }

            _lastObservedFrame = Time.frameCount;
            EnsureWriter();
            ObserveManualMarker(gameManager);
            ObserveState(gameManager);
            ObservePostEnterControl(gameManager);
#endif
        }

#if V1432
        private static void SubscribeToGameManager(GameManager gameManager) {
            if (gameManager == null || ReferenceEquals(_subscribedGameManager, gameManager)) {
                return;
            }

            if (_subscribedGameManager != null) {
                _subscribedGameManager.OnFinishedSceneTransition -= OnFinishedSceneTransition;
                _subscribedGameManager.OnFinishedEnteringScene -= OnFinishedEnteringScene;
            }

            _subscribedGameManager = gameManager;
            _subscribedGameManager.OnFinishedSceneTransition += OnFinishedSceneTransition;
            _subscribedGameManager.OnFinishedEnteringScene += OnFinishedEnteringScene;
            ResetObservedState();
        }

        private static void EnsureWriter() {
            if (_writer != null) {
                return;
            }

            string directory = Path.GetDirectoryName(TracePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }

            _writer = new StreamWriter(TracePath, false, Encoding.UTF8) {
                AutoFlush = true
            };
            ResetObservedState();
            WriteLine("TraceStart", "path=" + Sanitize(TracePath));
        }

        private static void CloseWriter() {
            if (_writer == null) {
                return;
            }

            _writer.WriteLine("event=TraceStop|frame=" + Time.frameCount + "|fixedFrame=" + FixedFrameString() + "|time=" + FloatString(Time.time) + "|fixedTime=" + FloatString(Time.fixedTime));
            _writer.Dispose();
            _writer = null;
            ResetObservedState();
        }

        private static void ResetObservedState() {
            _lastSceneName = null;
            _lastNextSceneName = null;
            _lastCanInput = null;
            _lastInSceneTransition = null;
            _lastHasFinishedEnteringScene = null;
            _lastPlayerInvincible = null;
            _lastHeroInvulnerable = null;
            _lastDamageMode = null;
            _lastHeroState = null;
            _lastTransitionState = null;
            _lastUnityActiveSceneName = null;
            _lastUnitySceneCount = -1;
            _lastObservedFixedFrame = -1;
            _lastSeenFixedFrame = -1;
            _lastRenderFrameWithFixedAdvance = -1;
            _recentFrameSampleCount = 0;
            _recentFrameSampleStart = 0;
            _lastObservedFrame = -1;
        }

        private static void SubscribeToUnitySceneEvents() {
            if (_sceneEventsSubscribed) {
                return;
            }

            UnitySceneManager.sceneLoaded += OnUnitySceneLoaded;
            UnitySceneManager.activeSceneChanged += OnUnityActiveSceneChanged;
            UnitySceneManager.sceneUnloaded += OnUnitySceneUnloaded;
            _sceneEventsSubscribed = true;
        }

        private static void SubscribeToSceneTransitionBegan() {
            if (_sceneTransitionBeganSubscribed) {
                return;
            }

            GameManager.SceneTransitionBegan += OnSceneTransitionBegan;
            _sceneTransitionBeganSubscribed = true;
        }

        private static void ObserveManualMarker(GameManager gameManager) {
            if (Input.GetKeyDown(KeyCode.F12)) {
                string extra = "markerKey=F12|label=" + Sanitize(ConfigManager.TraceMarkerLabel);
                if (ConfigManager.TraceMarkerFrame >= 0) {
                    extra += "|markerFrame=" + ConfigManager.TraceMarkerFrame.ToString(CultureInfo.InvariantCulture);
                }

                WriteLine("ManualMarker", extra, gameManager, HeroController.instance ?? gameManager?.hero_ctrl);
            }
        }

        private static void ObserveState(GameManager gameManager) {
            if (gameManager == null) {
                return;
            }

            HeroController hero = HeroController.instance ?? gameManager.hero_ctrl;
            bool? canInput = hero != null ? hero.CanInput() : (bool?) null;
            bool? playerInvincible = PlayerData.instance != null ? PlayerData.instance.isInvincible : (bool?) null;
            bool? heroInvulnerable = hero != null ? hero.cState.invulnerable : (bool?) null;
            DamageMode? damageMode = hero != null ? hero.damageMode : (DamageMode?) null;
            string heroState = hero != null ? hero.hero_state.ToString() : null;
            string transitionState = hero != null ? hero.transitionState.ToString() : null;
            Scene activeScene = UnitySceneManager.GetActiveScene();
            string unityActiveSceneName = activeScene.name;
            int unitySceneCount = UnitySceneManager.sceneCount;

            if (!string.Equals(_lastSceneName, gameManager.sceneName, StringComparison.Ordinal)) {
                string extra =
                    "fromScene=" + Sanitize(_lastSceneName) +
                    "|toScene=" + Sanitize(gameManager.sceneName);
                WriteLine("SceneChanged", extra, gameManager, hero);
            }

            if (_lastPlayerInvincible != playerInvincible) {
                string extra =
                    "playerInvincible=" + NullableBoolString(playerInvincible) +
                    "|heroInvulnerable=" + NullableBoolString(heroInvulnerable) +
                    "|damageMode=" + NullableEnumString(damageMode);
                WriteLine("InvincibilityChanged", extra, gameManager, hero);
                if (_lastPlayerInvincible == true && playerInvincible == false) {
                    MarkTransitionCheckpoint("InvincibilityEnd");
                }
            } else if (_lastHeroInvulnerable != heroInvulnerable || _lastDamageMode != damageMode) {
                string extra =
                    "playerInvincible=" + NullableBoolString(playerInvincible) +
                    "|heroInvulnerable=" + NullableBoolString(heroInvulnerable) +
                    "|damageMode=" + NullableEnumString(damageMode);
                WriteLine("DamageStateChanged", extra, gameManager, hero);
            }

            if (!string.Equals(_lastHeroState, heroState, StringComparison.Ordinal)) {
                string extra =
                    "fromHeroState=" + Sanitize(_lastHeroState) +
                    "|toHeroState=" + Sanitize(heroState);
                WriteLine("HeroStateChanged", extra, gameManager, hero);
            }

            if (!string.Equals(_lastTransitionState, transitionState, StringComparison.Ordinal)) {
                string extra =
                    "fromTransitionState=" + Sanitize(_lastTransitionState) +
                    "|toTransitionState=" + Sanitize(transitionState);
                WriteLine("TransitionStateChanged", extra, gameManager, hero);
            }

            if (!string.Equals(_lastUnityActiveSceneName, unityActiveSceneName, StringComparison.Ordinal) || _lastUnitySceneCount != unitySceneCount) {
                string extra =
                    "unityActiveSceneChanged=" + BoolString(!string.Equals(_lastUnityActiveSceneName, unityActiveSceneName, StringComparison.Ordinal)) +
                    "|unitySceneCountChanged=" + BoolString(_lastUnitySceneCount != unitySceneCount) +
                    "|fromUnityActiveScene=" + Sanitize(_lastUnityActiveSceneName) +
                    "|toUnityActiveScene=" + Sanitize(unityActiveSceneName) +
                    "|unitySceneCount=" + unitySceneCount.ToString(CultureInfo.InvariantCulture);
                WriteLine("UnitySceneStateChanged", extra, gameManager, hero);
            }

            bool changed =
                !string.Equals(_lastSceneName, gameManager.sceneName, StringComparison.Ordinal) ||
                !string.Equals(_lastNextSceneName, gameManager.nextSceneName, StringComparison.Ordinal) ||
                _lastCanInput != canInput ||
                _lastInSceneTransition != gameManager.IsInSceneTransition ||
                _lastHasFinishedEnteringScene != gameManager.HasFinishedEnteringScene ||
                !string.Equals(_lastUnityActiveSceneName, unityActiveSceneName, StringComparison.Ordinal) ||
                _lastUnitySceneCount != unitySceneCount;

            if (changed) {
                string extra =
                    "sceneChanged=" + BoolString(!string.Equals(_lastSceneName, gameManager.sceneName, StringComparison.Ordinal)) +
                    "|nextChanged=" + BoolString(!string.Equals(_lastNextSceneName, gameManager.nextSceneName, StringComparison.Ordinal)) +
                    "|canInputChanged=" + BoolString(_lastCanInput != canInput) +
                    "|inSceneTransitionChanged=" + BoolString(_lastInSceneTransition != gameManager.IsInSceneTransition) +
                    "|hasFinishedEnteringSceneChanged=" + BoolString(_lastHasFinishedEnteringScene != gameManager.HasFinishedEnteringScene) +
                    "|unityActiveSceneChanged=" + BoolString(!string.Equals(_lastUnityActiveSceneName, unityActiveSceneName, StringComparison.Ordinal)) +
                    "|unitySceneCountChanged=" + BoolString(_lastUnitySceneCount != unitySceneCount);
                WriteLine("ObservedStateChange", extra, gameManager, hero);
            }

            _lastSceneName = gameManager.sceneName;
            _lastNextSceneName = gameManager.nextSceneName;
            _lastCanInput = canInput;
            _lastInSceneTransition = gameManager.IsInSceneTransition;
            _lastHasFinishedEnteringScene = gameManager.HasFinishedEnteringScene;
            _lastPlayerInvincible = playerInvincible;
            _lastHeroInvulnerable = heroInvulnerable;
            _lastDamageMode = damageMode;
            _lastHeroState = heroState;
            _lastTransitionState = transitionState;
            _lastUnityActiveSceneName = unityActiveSceneName;
            _lastUnitySceneCount = unitySceneCount;
            _lastObservedFixedFrame = CurrentFixedFrame();
            CaptureRecentFrameSample(gameManager, hero);
        }

        private static void ObservePostEnterControl(GameManager gameManager) {
            if (_transitionStartFrame < 0 || _heroEnterSceneFrame < 0) {
                return;
            }

            HeroController hero = HeroController.instance ?? gameManager?.hero_ctrl;
            if (hero == null) {
                return;
            }

            int framesSinceEnter = Time.frameCount - _heroEnterSceneFrame;
            if (framesSinceEnter < 0 || framesSinceEnter > PostEnterFrameTraceWindow) {
                return;
            }

            bool canMove = CanMove(hero, gameManager);
            bool canJump = InvokeHeroPredicate(hero, CanJumpMethod);
            bool canDash = InvokeHeroPredicate(hero, CanDashMethod);
            bool canAttack = InvokeHeroPredicate(hero, CanAttackMethod);
            bool canWallJump = InvokeHeroPredicate(hero, CanWallJumpMethod);
            bool canSuperDash = hero.CanSuperDash();
            bool canInput = hero.CanInput();
            string superDashFsmState = GetSuperDashStateName(hero);
            InputHandler inputHandler = gameManager != null ? gameManager.GetComponent<InputHandler>() : null;

            if (canMove) {
                MarkTransitionCheckpoint("MoveEnabled");
            }

            if (canJump) {
                MarkTransitionCheckpoint("JumpEnabled");
            }

            if (canDash) {
                MarkTransitionCheckpoint("DashEnabled");
            }

            if (canAttack) {
                MarkTransitionCheckpoint("AttackEnabled");
            }

            if (canSuperDash) {
                MarkTransitionCheckpoint("SuperDashEnabled");
            }

            string extra =
                "framesSinceHeroEnterScene=" + framesSinceEnter.ToString(CultureInfo.InvariantCulture) +
                "|fromAcceptInputFrames=" + RelativeFrameString(_acceptInputFrame) +
                "|fromMoveEnabledFrames=" + RelativeFrameString(_moveEnabledFrame) +
                "|fromJumpEnabledFrames=" + RelativeFrameString(_jumpEnabledFrame) +
                "|fromDashEnabledFrames=" + RelativeFrameString(_dashEnabledFrame) +
                "|fromAttackEnabledFrames=" + RelativeFrameString(_attackEnabledFrame) +
                "|fromSuperDashEnabledFrames=" + RelativeFrameString(_superDashEnabledFrame) +
                "|inKeys=" + Sanitize(CurrentInputKeysString()) +
                "|jumpHeld=" + BoolString(inputHandler != null && inputHandler.inputActions.jump.IsPressed) +
                "|jumpPressed=" + BoolString(inputHandler != null && inputHandler.inputActions.jump.WasPressed) +
                "|dashHeld=" + BoolString(inputHandler != null && inputHandler.inputActions.dash.IsPressed) +
                "|dashPressed=" + BoolString(inputHandler != null && inputHandler.inputActions.dash.WasPressed) +
                "|attackHeld=" + BoolString(inputHandler != null && inputHandler.inputActions.attack.IsPressed) +
                "|attackPressed=" + BoolString(inputHandler != null && inputHandler.inputActions.attack.WasPressed) +
                "|superDashHeld=" + BoolString(inputHandler != null && inputHandler.inputActions.superDash.IsPressed) +
                "|superDashPressed=" + BoolString(inputHandler != null && inputHandler.inputActions.superDash.WasPressed) +
                "|superDashReleased=" + BoolString(inputHandler != null && inputHandler.inputActions.superDash.WasReleased) +
                "|moveEnabled=" + BoolString(canMove) +
                "|jumpEnabled=" + BoolString(canJump) +
                "|dashEnabled=" + BoolString(canDash) +
                "|attackEnabled=" + BoolString(canAttack) +
                "|wallJumpEnabled=" + BoolString(canWallJump) +
                "|superDashEnabled=" + BoolString(canSuperDash) +
                "|dashState=" + BoolString(hero.cState.dashing) +
                "|superDashState=" + BoolString(hero.cState.superDashing) +
                "|exitedSuperDashing=" + BoolString(hero.exitedSuperDashing) +
                "|superDashFsmState=" + Sanitize(superDashFsmState) +
                "|onGround=" + BoolString(hero.cState.onGround) +
                "|heroState=" + Sanitize(hero.hero_state.ToString()) +
                "|transitionState=" + Sanitize(hero.transitionState.ToString()) +
                "|hasFinishedEnteringScene=" + BoolString(gameManager != null && gameManager.HasFinishedEnteringScene) +
                "|canInput=" + BoolString(canInput);
            WriteLine("PostEnterControlFrame", extra, gameManager, hero);
        }

        private static void OnBeginSceneTransition(GameManager gameManager, GameManager.SceneLoadInfo sceneLoadInfo) {
            BeginTransitionTrace(sceneLoadInfo?.SceneName);
            string extra =
                "targetScene=" + Sanitize(sceneLoadInfo?.SceneName) +
                "|entryGate=" + Sanitize(sceneLoadInfo?.EntryGateName) +
                "|leaveDir=" + NullableEnumString(sceneLoadInfo?.HeroLeaveDirection) +
                "|entryDelay=" + FloatString(sceneLoadInfo?.EntryDelay ?? 0f) +
                "|visualization=" + Sanitize(sceneLoadInfo?.Visualization.ToString()) +
                "|forceWaitFetch=" + BoolString(sceneLoadInfo != null && sceneLoadInfo.forceWaitFetch);
            WriteLine("BeginSceneTransition", extra, gameManager, HeroController.instance ?? gameManager?.hero_ctrl);
        }

        private static void OnGameManagerFinishedEnteringScene(GameManager gameManager) {
            WriteLine("GameManager.FinishedEnteringScene", null, gameManager, HeroController.instance ?? gameManager?.hero_ctrl);
        }

        private static void OnSceneTransitionBegan(SceneLoad sceneLoad) {
            if (sceneLoad == null) {
                return;
            }

            _currentSceneLoad = sceneLoad;
            sceneLoad.FetchComplete += OnSceneLoadFetchComplete;
            sceneLoad.WillActivate += OnSceneLoadWillActivate;
            sceneLoad.ActivationComplete += OnSceneLoadActivationComplete;
            sceneLoad.Complete += OnSceneLoadComplete;
            sceneLoad.StartCalled += OnSceneLoadStartCalled;
            sceneLoad.Finish += OnSceneLoadFinish;

            WriteLine("GameManager.SceneTransitionBegan",
                BuildSceneLoadStatusExtra(sceneLoad, "sceneLoadTargetScene=" + Sanitize(sceneLoad.TargetSceneName)),
                GameManager.instance,
                HeroController.instance);
        }

        private static void OnHeroEnterScene(HeroController heroController, TransitionPoint enterGate, float delayBeforeEnter) {
            GatePosition gatePosition = enterGate != null ? enterGate.GetGatePosition() : GatePosition.door;
            string extra =
                "gateName=" + Sanitize(enterGate != null ? enterGate.name : string.Empty) +
                "|gatePosition=" + Sanitize(gatePosition.ToString()) +
                "|verticalEntry=" + BoolString(gatePosition == GatePosition.top || gatePosition == GatePosition.bottom) +
                "|delayBeforeEnter=" + FloatString(delayBeforeEnter) +
                "|gateEntryDelay=" + FloatString(enterGate != null ? enterGate.entryDelay : 0f);
            WriteLine("HeroController.EnterScene", extra, GameManager.instance, heroController);
        }

        private static void OnHeroLeaveScene(HeroController heroController, GatePosition? gate) {
            string extra =
                "gatePosition=" + NullableEnumString(gate) +
                "|verticalLeave=" + BoolString(gate == GatePosition.top || gate == GatePosition.bottom);
            WriteLine("HeroController.LeaveScene", extra, GameManager.instance, heroController);
        }

        private static void OnHeroAcceptInput(HeroController heroController) {
            MarkTransitionCheckpoint("AcceptInput");
            WriteLine("HeroController.AcceptInput", null, GameManager.instance, heroController);
        }

        private static void OnHeroAcceptInputExit(HeroController heroController) {
            MarkActionableInputEnabled(heroController);
            WriteLine("HeroController.AcceptInputExit", BuildInputGateExtra(heroController), GameManager.instance, heroController);
        }

        private static void OnHeroFinishedEnteringScene(HeroController heroController, bool setHazardMarker, bool preventRunBob) {
            string extra =
                "setHazardMarker=" + BoolString(setHazardMarker) +
                "|preventRunBob=" + BoolString(preventRunBob);
            WriteLine("HeroController.FinishedEnteringScene", extra, GameManager.instance, heroController);
        }

        private static void OnHeroFinishedEnteringSceneExit(HeroController heroController, bool setHazardMarker, bool preventRunBob) {
            MarkActionableInputEnabled(heroController);
            string extra =
                "setHazardMarker=" + BoolString(setHazardMarker) +
                "|preventRunBob=" + BoolString(preventRunBob);
            WriteLine("HeroController.FinishedEnteringSceneExit", BuildInputGateExtra(heroController, extra), GameManager.instance, heroController);
        }

        private static void OnHeroRegainControlExit(HeroController heroController) {
            MarkActionableInputEnabled(heroController);
            WriteLine("HeroController.RegainControlExit", BuildInputGateExtra(heroController), GameManager.instance, heroController);
        }

        private static void OnHeroSetSuperDashExit(HeroController heroController) {
            WriteLine("HeroController.SetSuperDashExit", BuildActionGateExtra(heroController), GameManager.instance, heroController);
        }

        private static void OnHeroCancelSuperDash(HeroController heroController) {
            WriteLine("HeroController.CancelSuperDash", BuildActionGateExtra(heroController), GameManager.instance, heroController);
        }

        private static void OnHeroDash(HeroController heroController) {
            WriteLine("HeroController.HeroDash", BuildActionGateExtra(heroController), GameManager.instance, heroController);
        }

        private static void OnHeroJump(HeroController heroController) {
            WriteLine("HeroController.HeroJump", BuildActionGateExtra(heroController), GameManager.instance, heroController);
        }

        private static void OnHeroDoAttack(HeroController heroController) {
            WriteLine("HeroController.DoAttack", BuildActionGateExtra(heroController), GameManager.instance, heroController);
        }

        private static void OnFinishedSceneTransition() {
            WriteLine("GameManager.OnFinishedSceneTransition", null, _subscribedGameManager, HeroController.instance);
        }

        private static void OnFinishedEnteringScene() {
            WriteLine("GameManager.OnFinishedEnteringScene", null, _subscribedGameManager, HeroController.instance);
        }

        private static void OnSceneLoadBegin(SceneLoad sceneLoad) {
            MarkTransitionCheckpoint("SceneLoadBegin");
            WriteLine("SceneLoad.Begin", BuildSceneLoadStatusExtra(sceneLoad), GameManager.instance, HeroController.instance);
        }

        private static void OnSceneLoadSetFetchAllowed(SceneLoad sceneLoad, bool value) {
            if (value) {
                MarkTransitionCheckpoint("SceneLoadFetchAllowed");
            }

            string extra =
                "fromFetchAllowed=" + BoolString(sceneLoad.IsFetchAllowed) +
                "|toFetchAllowed=" + BoolString(value);
            WriteLine("SceneLoad.SetFetchAllowed", BuildSceneLoadStatusExtra(sceneLoad, extra), GameManager.instance, HeroController.instance);
        }

        private static void OnSceneLoadSetActivationAllowed(SceneLoad sceneLoad, bool value) {
            if (value) {
                MarkTransitionCheckpoint("SceneLoadActivationAllowed");
            }

            string extra =
                "fromActivationAllowed=" + BoolString(sceneLoad.IsActivationAllowed) +
                "|toActivationAllowed=" + BoolString(value);
            WriteLine("SceneLoad.SetActivationAllowed", BuildSceneLoadStatusExtra(sceneLoad, extra), GameManager.instance, HeroController.instance);
        }

        private static void OnSceneLoadFetchComplete() {
            SceneLoad sceneLoad = _currentSceneLoad;
            WriteLine("SceneLoad.FetchComplete", BuildSceneLoadStatusExtra(sceneLoad), GameManager.instance, HeroController.instance);
        }

        private static void OnSceneLoadWillActivate() {
            SceneLoad sceneLoad = _currentSceneLoad;
            WriteLine("SceneLoad.WillActivate", BuildSceneLoadStatusExtra(sceneLoad), GameManager.instance, HeroController.instance);
        }

        private static void OnSceneLoadActivationComplete() {
            SceneLoad sceneLoad = _currentSceneLoad;
            WriteLine("SceneLoad.ActivationComplete", BuildSceneLoadStatusExtra(sceneLoad), GameManager.instance, HeroController.instance);
        }

        private static void OnSceneLoadComplete() {
            SceneLoad sceneLoad = _currentSceneLoad;
            WriteLine("SceneLoad.Complete", BuildSceneLoadStatusExtra(sceneLoad), GameManager.instance, HeroController.instance);
        }

        private static void OnSceneLoadStartCalled() {
            SceneLoad sceneLoad = _currentSceneLoad;
            WriteLine("SceneLoad.StartCalled", BuildSceneLoadStatusExtra(sceneLoad), GameManager.instance, HeroController.instance);
        }

        private static void OnSceneLoadFinish() {
            SceneLoad sceneLoad = _currentSceneLoad;
            WriteLine("SceneLoad.Finish", BuildSceneLoadStatusExtra(sceneLoad), GameManager.instance, HeroController.instance);
            _currentSceneLoad = null;
        }

        private static void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode) {
            MarkTransitionCheckpoint("UnitySceneLoaded");
            string extra =
                "unityScene=" + Sanitize(scene.name) +
                "|buildIndex=" + scene.buildIndex.ToString(CultureInfo.InvariantCulture) +
                "|isLoaded=" + BoolString(scene.isLoaded) +
                "|mode=" + Sanitize(mode.ToString()) +
                "|unitySceneCount=" + UnitySceneManager.sceneCount.ToString(CultureInfo.InvariantCulture);
            WriteLine("Unity.SceneLoaded", extra, GameManager.instance, HeroController.instance);
        }

        private static void OnUnityActiveSceneChanged(Scene oldScene, Scene newScene) {
            string extra =
                "fromUnityActiveScene=" + Sanitize(oldScene.name) +
                "|toUnityActiveScene=" + Sanitize(newScene.name) +
                "|unitySceneCount=" + UnitySceneManager.sceneCount.ToString(CultureInfo.InvariantCulture);
            WriteLine("Unity.ActiveSceneChanged", extra, GameManager.instance, HeroController.instance);
        }

        private static void OnUnitySceneUnloaded(Scene scene) {
            string extra =
                "unityScene=" + Sanitize(scene.name) +
                "|buildIndex=" + scene.buildIndex.ToString(CultureInfo.InvariantCulture) +
                "|unitySceneCount=" + UnitySceneManager.sceneCount.ToString(CultureInfo.InvariantCulture);
            WriteLine("Unity.SceneUnloaded", extra, GameManager.instance, HeroController.instance);
        }

        private static void MarkActionableInputEnabled(HeroController heroController) {
            if (_transitionStartFrame < 0 || heroController == null) {
                return;
            }

            if (heroController.CanInput()) {
                MarkTransitionCheckpoint("InputEnabled");
            }
        }

        private static string BuildInputGateExtra(HeroController heroController, string extra = null) {
            StringBuilder builder = new();
            builder.Append("acceptingInput=").Append(BoolString(heroController != null && heroController.CanInput()));
            builder.Append("|heroState=").Append(Sanitize(heroController != null ? heroController.hero_state.ToString() : string.Empty));
            builder.Append("|transitionState=").Append(Sanitize(heroController != null ? heroController.transitionState.ToString() : string.Empty));
            builder.Append("|onGround=").Append(BoolString(heroController != null && heroController.cState.onGround));
            builder.Append("|dashing=").Append(BoolString(heroController != null && heroController.cState.dashing));
            builder.Append("|superDashing=").Append(BoolString(heroController != null && heroController.cState.superDashing));
            builder.Append("|willHardLand=").Append(BoolString(heroController != null && heroController.cState.willHardLand));
            if (!string.IsNullOrEmpty(extra)) {
                builder.Append("|").Append(extra);
            }

            return builder.ToString();
        }

        private static string BuildActionGateExtra(HeroController heroController) {
            if (heroController == null) {
                return "canMove=-|canJump=-|canDash=-|canAttack=-|canWallJump=-|canSuperDash=-|superDashFsmState=-";
            }

            StringBuilder builder = new();
            builder.Append(BuildInputGateExtra(heroController));
            builder.Append("|canMove=").Append(BoolString(CanMove(heroController, GameManager.instance)));
            builder.Append("|canJump=").Append(BoolString(InvokeHeroPredicate(heroController, CanJumpMethod)));
            builder.Append("|canDash=").Append(BoolString(InvokeHeroPredicate(heroController, CanDashMethod)));
            builder.Append("|canAttack=").Append(BoolString(InvokeHeroPredicate(heroController, CanAttackMethod)));
            builder.Append("|canWallJump=").Append(BoolString(InvokeHeroPredicate(heroController, CanWallJumpMethod)));
            builder.Append("|canSuperDash=").Append(BoolString(heroController.CanSuperDash()));
            builder.Append("|superDashFsmState=").Append(Sanitize(GetSuperDashStateName(heroController)));
            return builder.ToString();
        }

        private static void WriteLine(string eventName, string extra, GameManager gameManager = null, HeroController heroController = null) {
            if (!ConfigManager.TraceTransitions) {
                return;
            }

            EnsureWriter();
            if (_writer == null) {
                return;
            }

            GameManager gm = gameManager ?? GameManager.instance;
            HeroController hero = heroController ?? HeroController.instance ?? (gm != null ? gm.hero_ctrl : null);
            UpdateFixedPhaseMarkers();
            Scene unityActiveScene = UnitySceneManager.GetActiveScene();
            int currentFixedFrame = CurrentFixedFrame();
            int fixedStepsSinceLastObserve = _lastObservedFixedFrame >= 0 && currentFixedFrame >= 0 ? currentFixedFrame - _lastObservedFixedFrame : -1;
            bool fixedAdvancedThisRender = currentFixedFrame >= 0 && _lastRenderFrameWithFixedAdvance == Time.frameCount;
            int renderFramesSinceFixedAdvance = _lastRenderFrameWithFixedAdvance >= 0 ? Time.frameCount - _lastRenderFrameWithFixedAdvance : -1;

            StringBuilder builder = new();
            builder.Append("event=").Append(Sanitize(eventName));
            builder.Append("|frame=").Append(Time.frameCount);
            builder.Append("|fixedFrame=").Append(FixedFrameString());
            builder.Append("|time=").Append(FloatString(Time.time));
            builder.Append("|fixedTime=").Append(FloatString(Time.fixedTime));
            builder.Append("|timeMinusFixedTime=").Append(FloatString(Time.time - Time.fixedTime));
            builder.Append("|fixedDeltaTime=").Append(FloatString(Time.fixedDeltaTime));
            builder.Append("|maximumDeltaTime=").Append(FloatString(Time.maximumDeltaTime));
            builder.Append("|fixedStepsSinceLastObserve=").Append(fixedStepsSinceLastObserve >= 0 ? fixedStepsSinceLastObserve.ToString(CultureInfo.InvariantCulture) : "-");
            builder.Append("|fixedAdvancedThisRender=").Append(BoolString(fixedAdvancedThisRender));
            builder.Append("|renderFramesSinceFixedAdvance=").Append(renderFramesSinceFixedAdvance >= 0 ? renderFramesSinceFixedAdvance.ToString(CultureInfo.InvariantCulture) : "-");
            builder.Append("|unityActiveScene=").Append(Sanitize(unityActiveScene.name));
            builder.Append("|unitySceneCount=").Append(UnitySceneManager.sceneCount.ToString(CultureInfo.InvariantCulture));
            builder.Append("|scene=").Append(Sanitize(gm != null ? gm.sceneName : string.Empty));
            builder.Append("|nextScene=").Append(Sanitize(gm != null ? gm.nextSceneName : string.Empty));
            builder.Append("|gameState=").Append(Sanitize(gm != null ? gm.gameState.ToString() : string.Empty));
            builder.Append("|isInSceneTransition=").Append(BoolString(gm != null && gm.IsInSceneTransition));
            builder.Append("|hasFinishedEnteringScene=").Append(BoolString(gm != null && gm.HasFinishedEnteringScene));
            builder.Append("|playerInvincible=").Append(NullableBoolString(PlayerData.instance != null ? PlayerData.instance.isInvincible : (bool?) null));
            builder.Append("|heroInvulnerable=").Append(NullableBoolString(hero != null ? hero.cState.invulnerable : (bool?) null));
            builder.Append("|damageMode=").Append(NullableEnumString(hero != null ? (DamageMode?) hero.damageMode : null));
            builder.Append("|canInput=").Append(NullableBoolString(hero != null ? hero.CanInput() : (bool?) null));
            builder.Append("|heroState=").Append(Sanitize(hero != null ? hero.hero_state.ToString() : string.Empty));
            builder.Append("|transitionState=").Append(Sanitize(hero != null ? hero.transitionState.ToString() : string.Empty));
            builder.Append("|pos=").Append(Vector2String(hero != null ? new Vector2(hero.transform.position.x, hero.transform.position.y) : (Vector2?) null));
            builder.Append("|vel=").Append(Vector2String(hero != null ? (Vector2?) hero.current_velocity : null));

            if (!string.IsNullOrEmpty(extra)) {
                builder.Append("|").Append(extra);
            }

            AppendTransitionSummary(builder, eventName, currentFixedFrame);

            if (ShouldAttachRecentFrames(eventName)) {
                string recentFrames = RecentFrameSamplesString();
                if (!string.IsNullOrEmpty(recentFrames)) {
                    builder.Append("|recentFrames=").Append(recentFrames);
                }
            }

            _writer.WriteLine(builder.ToString());
        }

        private static void CaptureRecentFrameSample(GameManager gameManager, HeroController heroController) {
            string sample =
                Time.frameCount.ToString(CultureInfo.InvariantCulture) + "~" +
                FixedFrameString() + "~" +
                FloatString(Time.time - Time.fixedTime) + "~" +
                BoolString(_lastRenderFrameWithFixedAdvance == Time.frameCount) + "~" +
                (_lastRenderFrameWithFixedAdvance >= 0 ? (Time.frameCount - _lastRenderFrameWithFixedAdvance).ToString(CultureInfo.InvariantCulture) : "-") + "~" +
                Sanitize(UnitySceneManager.GetActiveScene().name) + "~" +
                Sanitize(gameManager != null ? gameManager.sceneName : string.Empty) + "~" +
                Sanitize(gameManager != null ? gameManager.nextSceneName : string.Empty) + "~" +
                Vector2String(heroController != null ? new Vector2(heroController.transform.position.x, heroController.transform.position.y) : (Vector2?) null) + "~" +
                Vector2String(heroController != null ? (Vector2?) heroController.current_velocity : null);

            int insertIndex = (_recentFrameSampleStart + _recentFrameSampleCount) % RecentFrameSampleCapacity;
            _recentFrameSamples[insertIndex] = sample;
            if (_recentFrameSampleCount < RecentFrameSampleCapacity) {
                _recentFrameSampleCount++;
            } else {
                _recentFrameSampleStart = (_recentFrameSampleStart + 1) % RecentFrameSampleCapacity;
            }
        }

        private static string RecentFrameSamplesString() {
            if (_recentFrameSampleCount == 0) {
                return null;
            }

            StringBuilder builder = new();
            for (int i = 0; i < _recentFrameSampleCount; i++) {
                if (i > 0) {
                    builder.Append(";");
                }

                int index = (_recentFrameSampleStart + i) % RecentFrameSampleCapacity;
                builder.Append(_recentFrameSamples[index]);
            }

            return builder.ToString();
        }

        private static bool ShouldAttachRecentFrames(string eventName) {
            return eventName == "Unity.SceneLoaded" ||
                   eventName == "Unity.ActiveSceneChanged" ||
                   eventName == "SceneChanged" ||
                   eventName == "UnitySceneStateChanged" ||
                   eventName == "HeroController.EnterScene" ||
                   eventName.StartsWith("SceneLoad.", StringComparison.Ordinal);
        }

        private static void BeginTransitionTrace(string targetScene) {
            _currentTransitionId++;
            _currentTransitionTargetScene = targetScene;
            _transitionStartFrame = Time.frameCount;
            _transitionStartFixedFrame = CurrentFixedFrame();
            _sceneLoadBeginFrame = -1;
            _sceneLoadBeginFixedFrame = -1;
            _sceneLoadFetchAllowedFrame = -1;
            _sceneLoadFetchAllowedFixedFrame = -1;
            _sceneLoadActivationAllowedFrame = -1;
            _sceneLoadActivationAllowedFixedFrame = -1;
            _unitySceneLoadedFrame = -1;
            _unitySceneLoadedFixedFrame = -1;
            _sceneChangedFrame = -1;
            _sceneChangedFixedFrame = -1;
            _heroEnterSceneFrame = -1;
            _heroEnterSceneFixedFrame = -1;
            _invincibilityEndFrame = -1;
            _invincibilityEndFixedFrame = -1;
            _acceptInputFrame = -1;
            _acceptInputFixedFrame = -1;
            _inputEnabledFrame = -1;
            _inputEnabledFixedFrame = -1;
            _moveEnabledFrame = -1;
            _moveEnabledFixedFrame = -1;
            _jumpEnabledFrame = -1;
            _jumpEnabledFixedFrame = -1;
            _dashEnabledFrame = -1;
            _dashEnabledFixedFrame = -1;
            _attackEnabledFrame = -1;
            _attackEnabledFixedFrame = -1;
            _superDashEnabledFrame = -1;
            _superDashEnabledFixedFrame = -1;
        }

        private static void MarkTransitionCheckpoint(string checkpointName) {
            int currentFixedFrame = CurrentFixedFrame();
            switch (checkpointName) {
                case "UnitySceneLoaded":
                    if (_unitySceneLoadedFrame < 0) {
                        _unitySceneLoadedFrame = Time.frameCount;
                        _unitySceneLoadedFixedFrame = currentFixedFrame;
                    }
                    break;
                case "SceneLoadBegin":
                    if (_sceneLoadBeginFrame < 0) {
                        _sceneLoadBeginFrame = Time.frameCount;
                        _sceneLoadBeginFixedFrame = currentFixedFrame;
                    }
                    break;
                case "SceneLoadFetchAllowed":
                    if (_sceneLoadFetchAllowedFrame < 0) {
                        _sceneLoadFetchAllowedFrame = Time.frameCount;
                        _sceneLoadFetchAllowedFixedFrame = currentFixedFrame;
                    }
                    break;
                case "SceneLoadActivationAllowed":
                    if (_sceneLoadActivationAllowedFrame < 0) {
                        _sceneLoadActivationAllowedFrame = Time.frameCount;
                        _sceneLoadActivationAllowedFixedFrame = currentFixedFrame;
                    }
                    break;
                case "SceneChanged":
                    if (_sceneChangedFrame < 0) {
                        _sceneChangedFrame = Time.frameCount;
                        _sceneChangedFixedFrame = currentFixedFrame;
                    }
                    break;
                case "HeroEnterScene":
                    if (_heroEnterSceneFrame < 0) {
                        _heroEnterSceneFrame = Time.frameCount;
                        _heroEnterSceneFixedFrame = currentFixedFrame;
                    }
                    break;
                case "InvincibilityEnd":
                    if (_invincibilityEndFrame < 0) {
                        _invincibilityEndFrame = Time.frameCount;
                        _invincibilityEndFixedFrame = currentFixedFrame;
                    }
                    break;
                case "AcceptInput":
                    if (_acceptInputFrame < 0) {
                        _acceptInputFrame = Time.frameCount;
                        _acceptInputFixedFrame = currentFixedFrame;
                    }
                    break;
                case "InputEnabled":
                    if (_inputEnabledFrame < 0) {
                        _inputEnabledFrame = Time.frameCount;
                        _inputEnabledFixedFrame = currentFixedFrame;
                    }
                    break;
                case "MoveEnabled":
                    if (_moveEnabledFrame < 0) {
                        _moveEnabledFrame = Time.frameCount;
                        _moveEnabledFixedFrame = currentFixedFrame;
                    }
                    break;
                case "JumpEnabled":
                    if (_jumpEnabledFrame < 0) {
                        _jumpEnabledFrame = Time.frameCount;
                        _jumpEnabledFixedFrame = currentFixedFrame;
                    }
                    break;
                case "DashEnabled":
                    if (_dashEnabledFrame < 0) {
                        _dashEnabledFrame = Time.frameCount;
                        _dashEnabledFixedFrame = currentFixedFrame;
                    }
                    break;
                case "AttackEnabled":
                    if (_attackEnabledFrame < 0) {
                        _attackEnabledFrame = Time.frameCount;
                        _attackEnabledFixedFrame = currentFixedFrame;
                    }
                    break;
                case "SuperDashEnabled":
                    if (_superDashEnabledFrame < 0) {
                        _superDashEnabledFrame = Time.frameCount;
                        _superDashEnabledFixedFrame = currentFixedFrame;
                    }
                    break;
            }
        }

        private static void AppendTransitionSummary(StringBuilder builder, string eventName, int currentFixedFrame) {
            if (_transitionStartFrame < 0) {
                return;
            }

            builder.Append("|transitionId=").Append(_currentTransitionId);
            builder.Append("|transitionTargetScene=").Append(Sanitize(_currentTransitionTargetScene));
            AppendDelta(builder, "fromTransitionStart", _transitionStartFrame, _transitionStartFixedFrame, currentFixedFrame);
            AppendDelta(builder, "fromSceneLoadBegin", _sceneLoadBeginFrame, _sceneLoadBeginFixedFrame, currentFixedFrame);
            AppendDelta(builder, "fromSceneLoadFetchAllowed", _sceneLoadFetchAllowedFrame, _sceneLoadFetchAllowedFixedFrame, currentFixedFrame);
            AppendDelta(builder, "fromSceneLoadActivationAllowed", _sceneLoadActivationAllowedFrame, _sceneLoadActivationAllowedFixedFrame, currentFixedFrame);
            if (eventName == "SceneChanged") {
                MarkTransitionCheckpoint("SceneChanged");
            } else if (eventName == "HeroController.EnterScene") {
                MarkTransitionCheckpoint("HeroEnterScene");
            }
            AppendDelta(builder, "fromUnitySceneLoaded", _unitySceneLoadedFrame, _unitySceneLoadedFixedFrame, currentFixedFrame);
            AppendDelta(builder, "fromSceneChanged", _sceneChangedFrame, _sceneChangedFixedFrame, currentFixedFrame);
            AppendDelta(builder, "fromHeroEnterScene", _heroEnterSceneFrame, _heroEnterSceneFixedFrame, currentFixedFrame);
            AppendDelta(builder, "fromInvincibilityEnd", _invincibilityEndFrame, _invincibilityEndFixedFrame, currentFixedFrame);
            AppendDelta(builder, "fromAcceptInput", _acceptInputFrame, _acceptInputFixedFrame, currentFixedFrame);
            AppendDelta(builder, "fromInputEnabled", _inputEnabledFrame, _inputEnabledFixedFrame, currentFixedFrame);
            AppendDelta(builder, "fromMoveEnabled", _moveEnabledFrame, _moveEnabledFixedFrame, currentFixedFrame);
            AppendDelta(builder, "fromJumpEnabled", _jumpEnabledFrame, _jumpEnabledFixedFrame, currentFixedFrame);
            AppendDelta(builder, "fromDashEnabled", _dashEnabledFrame, _dashEnabledFixedFrame, currentFixedFrame);
            AppendDelta(builder, "fromAttackEnabled", _attackEnabledFrame, _attackEnabledFixedFrame, currentFixedFrame);
            AppendDelta(builder, "fromSuperDashEnabled", _superDashEnabledFrame, _superDashEnabledFixedFrame, currentFixedFrame);
        }

        private static void AppendDelta(StringBuilder builder, string label, int startFrame, int startFixedFrame, int currentFixedFrame) {
            if (startFrame < 0) {
                builder.Append("|").Append(label).Append("Frames=-");
                builder.Append("|").Append(label).Append("Fixed=-");
                return;
            }

            builder.Append("|").Append(label).Append("Frames=").Append((Time.frameCount - startFrame).ToString(CultureInfo.InvariantCulture));
            if (currentFixedFrame >= 0 && startFixedFrame >= 0) {
                builder.Append("|").Append(label).Append("Fixed=").Append((currentFixedFrame - startFixedFrame).ToString(CultureInfo.InvariantCulture));
            } else {
                builder.Append("|").Append(label).Append("Fixed=-");
            }
        }

        private static void UpdateFixedPhaseMarkers() {
            int currentFixedFrame = CurrentFixedFrame();
            if (currentFixedFrame < 0) {
                return;
            }

            if (currentFixedFrame != _lastSeenFixedFrame) {
                _lastSeenFixedFrame = currentFixedFrame;
                _lastRenderFrameWithFixedAdvance = Time.frameCount;
            }
        }

        private static int CurrentFixedFrame() {
            if (Time.fixedDeltaTime <= 0f) {
                return -1;
            }

            return Mathf.RoundToInt(Time.fixedTime / Time.fixedDeltaTime);
        }

        private static string FixedFrameString() {
            int currentFixedFrame = CurrentFixedFrame();
            return currentFixedFrame >= 0 ? currentFixedFrame.ToString(CultureInfo.InvariantCulture) : "-";
        }

        private static string Vector2String(Vector2? value) {
            if (!value.HasValue) {
                return "-";
            }

            Vector2 vector = value.Value;
            return FloatString(vector.x) + "," + FloatString(vector.y);
        }

        private static string FloatString(float value) {
            return value.ToString("0.000000", CultureInfo.InvariantCulture);
        }

        private static string BoolString(bool value) {
            return value ? "1" : "0";
        }

        private static string BuildSceneLoadStatusExtra(SceneLoad sceneLoad, string extra = null) {
            StringBuilder builder = new();
            builder.Append("sceneLoadHash=").Append(SceneLoadHashString(sceneLoad));
            builder.Append("|sceneLoadTargetScene=").Append(Sanitize(sceneLoad?.TargetSceneName));
            builder.Append("|sceneLoadFetchAllowed=").Append(sceneLoad != null ? BoolString(sceneLoad.IsFetchAllowed) : "-");
            builder.Append("|sceneLoadActivationAllowed=").Append(sceneLoad != null ? BoolString(sceneLoad.IsActivationAllowed) : "-");
            builder.Append("|sceneLoadFinished=").Append(sceneLoad != null ? BoolString(sceneLoad.IsFinished) : "-");
            AppendSceneLoadDuration(builder, sceneLoad, "fetchBlockedDuration", SceneLoad.Phases.FetchBlocked);
            AppendSceneLoadDuration(builder, sceneLoad, "fetchDuration", SceneLoad.Phases.Fetch);
            AppendSceneLoadDuration(builder, sceneLoad, "activationBlockedDuration", SceneLoad.Phases.ActivationBlocked);
            AppendSceneLoadDuration(builder, sceneLoad, "activationDuration", SceneLoad.Phases.Activation);
            AppendSceneLoadDuration(builder, sceneLoad, "unloadAssetsDuration", SceneLoad.Phases.UnloadUnusedAssets);
            AppendSceneLoadDuration(builder, sceneLoad, "garbageCollectDuration", SceneLoad.Phases.GarbageCollect);
            AppendSceneLoadDuration(builder, sceneLoad, "startCallDuration", SceneLoad.Phases.StartCall);
            AppendSceneLoadDuration(builder, sceneLoad, "loadBossDuration", SceneLoad.Phases.LoadBoss);
            if (!string.IsNullOrEmpty(extra)) {
                builder.Append("|").Append(extra);
            }

            return builder.ToString();
        }

        private static void AppendSceneLoadDuration(StringBuilder builder, SceneLoad sceneLoad, string label, SceneLoad.Phases phase) {
            float? duration = sceneLoad?.GetDuration(phase);
            builder.Append("|").Append(label).Append("=").Append(duration.HasValue ? FloatString(duration.Value) : "-");
        }

        private static string SceneLoadHashString(SceneLoad sceneLoad) {
            return sceneLoad != null ? sceneLoad.GetHashCode().ToString(CultureInfo.InvariantCulture) : "-";
        }

        private static string NullableBoolString(bool? value) {
            return value.HasValue ? BoolString(value.Value) : "-";
        }

        private static string CurrentInputKeysString() {
            StringBuilder builder = new();
            for (int i = 0; i < TrackedInputKeys.Length; i++) {
                if (Input.GetKey(TrackedInputKeys[i])) {
                    builder.Append(TrackedInputKeyStrings[i]);
                }
            }

            return builder.Length > 0 ? builder.ToString() : "-";
        }

        private static string RelativeFrameString(int checkpointFrame) {
            return checkpointFrame >= 0 ? (Time.frameCount - checkpointFrame).ToString(CultureInfo.InvariantCulture) : "-";
        }

        private static bool CanMove(HeroController heroController, GameManager gameManager) {
            if (heroController == null || gameManager == null || gameManager.isPaused) {
                return false;
            }

            return heroController.CanInput() &&
                   heroController.hero_state != ActorStates.no_input &&
                   !heroController.cState.backDashing &&
                   !heroController.cState.dashing;
        }

        private static bool InvokeHeroPredicate(HeroController heroController, MethodInfo method) {
            if (heroController == null || method == null) {
                return false;
            }

            try {
                object result = method.Invoke(heroController, null);
                return result is bool value && value;
            } catch {
                return false;
            }
        }

        private static string GetSuperDashStateName(HeroController heroController) {
            if (heroController == null) {
                return null;
            }

            PlayMakerFSM superDashFsm = FSMUtility.LocateFSM(heroController.gameObject, "Superdash");
            return superDashFsm != null ? superDashFsm.ActiveStateName : null;
        }

        private static string NullableEnumString<TEnum>(TEnum? value) where TEnum : struct {
            return value.HasValue ? Sanitize(value.Value.ToString()) : "-";
        }

        private static string Sanitize(string value) {
            if (string.IsNullOrEmpty(value)) {
                return "-";
            }

            return value.Replace("|", "/").Replace("\r", " ").Replace("\n", " ");
        }
#endif
    }
}
