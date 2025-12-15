using BS_Utils.Gameplay;
using System;
using System.Linq;
using UnityEngine;
using BS_Utils.Utilities;


namespace SaberSurgeon.Gameplay
{
    /// <summary>
    /// Standalone PlayFirst SubmitLater Manager.
    /// Works completely independently from all other Saber Surgeon systems.
    /// Manages score submission state and auto-pause behavior.
    /// </summary>
    public class PlayFirstSubmitLaterManager : MonoBehaviour
    {
        private static PlayFirstSubmitLaterManager _instance;
        private static GameObject _go;
        private const string SubmissionKey = "SaberSurgeon: SubmitLater";


        // Feature state
        private bool _submissionDisabled = false;
        private bool _autoPauseTriggered = false;
        private PauseMenuManager _pauseMenuManager;

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

        private void OnEnable()
        {
            try
            {
                BSEvents.gameSceneLoaded += HandleGameSceneLoaded;

                // Optional: treat restart as a "new start" (usually fine)
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


        /// <summary>
        /// STANDALONE: Disable score submission (works without any other mod).
        /// </summary>
        public static void DisableSubmission()
        {
            if (!IsFeatureEnabled) return;
            if (_instance == null) return;

            _instance._submissionDisabled = true;
            Plugin.Settings.ScoreSubmissionEnabled = false;

            // Reversible disable (needed for a pause-menu toggle)
            ScoreSubmission.ProlongedDisableSubmission(SubmissionKey);
            Plugin.Log.Info("PlayFirstSubmitLater: Score submission DISABLED (prolonged)");
        }

        /// <summary>
        /// STANDALONE: Re-enable score submission.
        /// </summary>
        public static void EnableSubmission()
        {
            if (!IsFeatureEnabled) return;
            if (_instance == null) return;

            _instance._submissionDisabled = false;
            Plugin.Settings.ScoreSubmissionEnabled = true;

            // Re-enable by removing ONLY our disable reason
            ScoreSubmission.RemoveProlongedDisable(SubmissionKey);
            Plugin.Log.Info("PlayFirstSubmitLater: Score submission ENABLED(removed prolonged disable)");
        }

        /// <summary>
        /// STANDALONE: Toggle submission state.
        /// </summary>
        public static void ToggleSubmission()
        {
            if (!IsFeatureEnabled) return;

            if (SubmissionDisabled)
                EnableSubmission();
            else
                DisableSubmission();
        }

        /// <summary>
        /// STANDALONE: Called when map ends to trigger auto-pause if enabled.
        /// </summary>
        

        /// <summary>
        /// STANDALONE: Reset state when new map starts.
        /// </summary>
        public void OnMapStarted()
        {
            if (!IsFeatureEnabled) return;

            _autoPauseTriggered = false;

            // Reset to config setting when new map starts
            if (Plugin.Settings.ScoreSubmissionEnabled && _submissionDisabled)
                EnableSubmission();
        }



        /// <summary>
        /// STANDALONE: Reset all state.
        /// </summary>
        public static void ResetState()
        {
            if (_instance == null) return;

            _instance._autoPauseTriggered = false;
            Plugin.Log.Debug("PlayFirstSubmitLater: State reset");
        }

        public void OnDestroy()
        {
            _instance = null;
        }


        private void HandleGameSceneLoaded()
        {
            // Called when gameplay scene is ready
            OnMapStarted();
        }


        private void HandleLevelRestarted(StandardLevelScenesTransitionSetupDataSO data, LevelCompletionResults results)
        {
            // Treat restart as "new run starting"
            OnMapStarted();
        }


    }
}
