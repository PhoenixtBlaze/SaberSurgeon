using BS_Utils.Gameplay;
using System;
using System.Linq;
using UnityEngine;
using BS_Utils.Utilities;
using System.Reflection;

namespace SaberSurgeon.Gameplay
{
    public class PlayFirstSubmitLaterManager : MonoBehaviour
    {
        private static PlayFirstSubmitLaterManager _instance;
        private static GameObject _go;
        private const string SubmissionKey = "SaberSurgeon: SubmitLater";

        // Feature state
        private bool _submissionDisabled = false;
        private bool _autoPauseTriggered = false;

        public static bool SubmissionDisabled =>
            _instance != null && _instance._submissionDisabled;

        public static bool IsFeatureEnabled =>
            Plugin.Settings?.PlayFirstSubmitLaterEnabled ?? true;

        public static PlayFirstSubmitLaterManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _go = new GameObject("SaberSurgeon_PlayFirstSubmitLater");
                    DontDestroyOnLoad(_go);
                    _instance = _go.AddComponent<PlayFirstSubmitLaterManager>();
                    Plugin.Log.Info("PlayFirstSubmitLaterManager: Initialized as standalone module");
                }
                return _instance;
            }
        }

        /// <summary>
        /// Robustly detects if BeatSaberPlus Multiplayer is active.
        /// </summary>
        public static bool IsBSPlusMultiplayerActive()
        {
            try
            {
                // 1. Check BS_Utils Isolation (Fastest)
                if (BS_Utils.Gameplay.Gamemode.IsIsolatedLevel)
                {
                    var mod = BS_Utils.Gameplay.Gamemode.IsolatingMod ?? "";
                    if (mod.IndexOf("BeatSaberPlus", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }

                // 2. Check for the specific BS+ MP Assembly
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var mpAssembly = assemblies.FirstOrDefault(a => a.GetName().Name == "BeatSaberPlus_Multiplayer");

                if (mpAssembly != null)
                {
                    // If the assembly exists, check if we are in a networked state
                    // The safest way is to check for the singleton "Instance" of the MultiplayerController or NetworkManager
                    var controllerType = mpAssembly.GetType("BeatSaberPlus.Multiplayer.MultiplayerController");

                    if (controllerType != null)
                    {
                        // Check static 'Instance'
                        var instanceProp = controllerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                        if (instanceProp != null)
                        {
                            var instance = instanceProp.GetValue(null);
                            if (instance != null)
                            {
                                // Check if the instance is active/enabled (it's a MonoBehaviour)
                                var mb = instance as MonoBehaviour;
                                if (mb != null && mb.isActiveAndEnabled)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"PlayFirstSubmitLater: Error checking for BS+ MP: {ex.Message}");
            }

            return false;
        }

        private void OnEnable()
        {
            try
            {
                BSEvents.gameSceneLoaded += HandleGameSceneLoaded;
                BSEvents.levelRestarted += HandleLevelRestarted;
                Plugin.Log.Info("PlayFirstSubmitLater: BS_Utils events hooked");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"PlayFirstSubmitLater: Error hooking events: {ex}");
            }
        }

        private void OnDisable()
        {
            BSEvents.gameSceneLoaded -= HandleGameSceneLoaded;
            BSEvents.levelRestarted -= HandleLevelRestarted;
        }

        public static void DisableSubmission()
        {
            if (!IsFeatureEnabled) return;
            if (_instance == null) return;

            // Block disabling if in any Multiplayer mode
            if (BS_Utils.Plugin.LevelData.Mode == Mode.Multiplayer || IsBSPlusMultiplayerActive())
            {
                Plugin.Log.Warn("PlayFirstSubmitLater: DisableSubmission ignored (Multiplayer detected)");
                return;
            }

            _instance._submissionDisabled = true;
            Plugin.Settings.ScoreSubmissionEnabled = false;
            ScoreSubmission.ProlongedDisableSubmission(SubmissionKey);
            Plugin.Log.Info("PlayFirstSubmitLater: Score submission DISABLED (prolonged)");
        }

        public static void EnableSubmission()
        {
            if (!IsFeatureEnabled) return;
            if (_instance == null) return;

            _instance._submissionDisabled = false;
            Plugin.Settings.ScoreSubmissionEnabled = true;
            ScoreSubmission.RemoveProlongedDisable(SubmissionKey);
            Plugin.Log.Info("PlayFirstSubmitLater: Score submission ENABLED (removed prolonged disable)");
        }

        public static void ToggleSubmission()
        {
            if (!IsFeatureEnabled) return;
            if (SubmissionDisabled) EnableSubmission();
            else DisableSubmission();
        }

        public void OnMapStarted()
        {
            if (!IsFeatureEnabled) return;

            // Debug Logging for troubleshooting
            bool isNative = BS_Utils.Plugin.LevelData.Mode == Mode.Multiplayer;
            bool isBSPlus = IsBSPlusMultiplayerActive();
            Plugin.Log.Info($"PlayFirstSubmitLater: Map Started. Mode Check -> NativeMP: {isNative}, BS+MP: {isBSPlus}");

            // FORCE ENABLE if in ANY Multiplayer mode
            if (isNative || isBSPlus)
            {
                Plugin.Log.Info("PlayFirstSubmitLater: Multiplayer detected. Forcing submission ON.");
                if (_submissionDisabled || !Plugin.Settings.ScoreSubmissionEnabled)
                {
                    EnableSubmission();
                }
                return;
            }

            _autoPauseTriggered = false;

            // Standard Solo behavior
            if (Plugin.Settings.ScoreSubmissionEnabled && _submissionDisabled)
                EnableSubmission();
        }

        public static void ResetState()
        {
            if (_instance == null) return;
            _instance._autoPauseTriggered = false;
        }

        public void OnDestroy() { _instance = null; }
        private void HandleGameSceneLoaded() { OnMapStarted(); }
        private void HandleLevelRestarted(StandardLevelScenesTransitionSetupDataSO data, LevelCompletionResults results) { OnMapStarted(); }
    }
}
