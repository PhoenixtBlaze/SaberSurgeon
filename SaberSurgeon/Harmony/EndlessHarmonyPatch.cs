using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace SaberSurgeon.HarmonyPatches
{
    [HarmonyPatch(typeof(MenuTransitionsHelper))]
    internal static class EndlessHarmonyPatch
    {
        private const float ChainFadeDurationSeconds = 0.6f;

        // ---------- PlayFirstSubmitLater pause-gate state ----------
        private static bool _pauseGateActive;
        private static bool _bypassPauseGateCall;

        private static MenuTransitionsHelper _pendingHelper;
        private static StandardLevelScenesTransitionSetupDataSO _pendingSetup;
        private static LevelCompletionResults _pendingResults;
        private static bool _usedFallbackMenuOnly;


        private static PauseMenuManager _pauseMenuManager;

        private static bool ShouldPauseGate(LevelCompletionResults results)
        {
            var s = Plugin.Settings;
            if (s == null) return false;
            if (!s.PlayFirstSubmitLaterEnabled) return false;
            if (!s.AutoPauseOnMapEnd) return false;

            // Endless mode takes priority
            var gm = Gameplay.GameplayManager.GetInstance();
            if (gm != null && gm.IsPlaying() && gm.GetRemainingTime() > 0f)
                return false;

            // Avoid interfering with explicit Quit/Restart flows
            if (results.levelEndAction == LevelCompletionResults.LevelEndAction.Quit) return false;
            if (results.levelEndAction == LevelCompletionResults.LevelEndAction.Restart) return false;

            return true;
        }

        private static void ClearPauseGate()
        {
            _pauseGateActive = false;
            _pendingHelper = null;
            _pendingSetup = null;
            _pendingResults = null;

            if (_pauseMenuManager != null)
            {
                
                _pauseMenuManager.didPressContinueButtonEvent -= HandleContinuePressed;
                _pauseMenuManager.didFinishResumeAnimationEvent -= HandleResumeFinished;
                _pauseMenuManager.didPressMenuButtonEvent -= HandleAbort;
                _pauseMenuManager.didPressRestartButtonEvent -= HandleAbort;
                _pauseMenuManager = null;
            }
        }


        private static void HandleContinuePressed()
        {
            // If this was a real PauseController pause, let the normal resume animation happen
            // and we’ll proceed in HandleResumeFinished().
            if (!_usedFallbackMenuOnly)
                return;

            // Fallback UI-only pause: do NOT run resume animation (avoids SliceDetails NRE).
            try
            {
                _bypassPauseGateCall = true;

                var method = AccessTools.Method(
                    typeof(MenuTransitionsHelper),
                    "HandleMainGameSceneDidFinish",
                    new[] { typeof(StandardLevelScenesTransitionSetupDataSO), typeof(LevelCompletionResults) });

                method?.Invoke(_pendingHelper, new object[] { _pendingSetup, _pendingResults });
            }
            finally
            {
                _bypassPauseGateCall = false;
                ClearPauseGate();
            }
        }



        private static void HandleAbort()
        {
            // Player chose Menu/Restart: do nothing special.
            ClearPauseGate();
        }

        private static void HandleResumeFinished()
        {
            // Continue was pressed, resume animation finished -> now go to results.
            if (_pendingHelper == null || _pendingSetup == null)
            {
                ClearPauseGate();
                return;
            }

            try
            {
                _bypassPauseGateCall = true;

                // Call original finish method after resume animation.
                var method = AccessTools.Method(
                    typeof(MenuTransitionsHelper),
                    "HandleMainGameSceneDidFinish",
                    new[] { typeof(StandardLevelScenesTransitionSetupDataSO), typeof(LevelCompletionResults) });

                method?.Invoke(_pendingHelper, new object[] { _pendingSetup, _pendingResults });
            }
            finally
            {
                _bypassPauseGateCall = false;
                ClearPauseGate();
            }
        }

        // Intercept the standard-level finish handler.
        [HarmonyPrefix]
        [HarmonyPatch("HandleMainGameSceneDidFinish")]
        private static bool Prefix(
            MenuTransitionsHelper __instance,
            StandardLevelScenesTransitionSetupDataSO standardLevelScenesTransitionSetupData,
            LevelCompletionResults levelCompletionResults)
        {
            // 1) PlayFirstSubmitLater pause gate MUST run FIRST
            if (!_bypassPauseGateCall && !_pauseGateActive && ShouldPauseGate(levelCompletionResults))
            {
                _pauseGateActive = true;

                _pendingHelper = __instance;
                _pendingSetup = standardLevelScenesTransitionSetupData;
                _pendingResults = levelCompletionResults;

                // Hook pause menu: wait for resume animation finish
                _pauseMenuManager = Resources.FindObjectsOfTypeAll<PauseMenuManager>().FirstOrDefault();
                if (_pauseMenuManager != null)
                {
                    _pauseMenuManager.didPressContinueButtonEvent += HandleContinuePressed;
                    _pauseMenuManager.didFinishResumeAnimationEvent += HandleResumeFinished;
                    _pauseMenuManager.didPressMenuButtonEvent += HandleAbort;
                    _pauseMenuManager.didPressRestartButtonEvent += HandleAbort;
                }

                // Real pause path: PauseController.Pause() pauses game + shows menu correctly
                var pauseController = Resources.FindObjectsOfTypeAll<PauseController>().FirstOrDefault();
                if (pauseController != null)
                {
                    Plugin.Log.Info("PlayFirstSubmitLater: Pausing at map end (before results).");
                    pauseController.Pause();
                    // If PauseController actually paused, it would have enabled + shown the menu.
                    
                    _usedFallbackMenuOnly = false; // assume real pause attempted
                    Plugin.Log.Info($"PlayFirstSubmitLater: pauseMenu enabled={_pauseMenuManager != null && _pauseMenuManager.enabled}, active={_pauseMenuManager != null && _pauseMenuManager.gameObject.activeInHierarchy}");

                }

                // If PauseController couldn't pause (common at end-of-level), at least show the pause menu UI
                if (_pauseMenuManager == null)
                    _pauseMenuManager = Resources.FindObjectsOfTypeAll<PauseMenuManager>().FirstOrDefault();

                if (_pauseMenuManager != null && !_pauseMenuManager.isActiveAndEnabled)
                {
                    // It’s often disabled until shown; ShowMenu() enables it internally. 
                    Plugin.Log.Info("PlayFirstSubmitLater: PauseMenuManager found (was disabled), calling ShowMenu fallback.");
                    _pauseMenuManager.ShowMenu(); // UI fallback 
                }
                else if (_pauseMenuManager != null)
                {
                    _usedFallbackMenuOnly = true;
                    Plugin.Log.Info("PlayFirstSubmitLater: Using fallback (UI-only) pause menu.");
                    _pauseMenuManager.ShowMenu(); // UI fallback 
                }
                else
                {
                    Plugin.Log.Warn("PlayFirstSubmitLater: PauseMenuManager not found, letting results continue.");
                    ClearPauseGate();
                    return true;
                }

                return false; // always block results until player interacts


                // Fail-open if PauseController isn't found
                Plugin.Log.Warn("PlayFirstSubmitLater: PauseController not found, letting results continue.");
                ClearPauseGate();
                return true;
            }

            // 2) Existing Endless-mode chaining logic (unchanged idea)
            var gm = Gameplay.GameplayManager.GetInstance();
            float fade = ChainFadeDurationSeconds;

            // Only chain if endless is still running, time remains, and user didn't quit.
            if (gm == null ||
                !gm.IsPlaying() ||
                gm.GetRemainingTime() <= 0f ||
                levelCompletionResults.levelEndAction == LevelCompletionResults.LevelEndAction.Quit)
            {
                return true; // run original
            }

            try
            {
                // Let finished callback run so GameplayManager updates.
                var finishedCBField = AccessTools.Field(typeof(MenuTransitionsHelper), "_standardLevelFinishedCallback");
                var finishedCB = (Action<StandardLevelScenesTransitionSetupDataSO, LevelCompletionResults>)finishedCBField.GetValue(__instance);
                finishedCB?.Invoke(standardLevelScenesTransitionSetupData, levelCompletionResults);

                if (!gm.TryPrepareNextChain(
                        out BeatmapLevel nextLevel,
                        out BeatmapKey nextKey,
                        out GameplayModifiers modifiers,
                        out PlayerSpecificSettings playerSettings,
                        out ColorScheme color,
                        out EnvironmentsListModel envs))
                {
                    Plugin.Log.Warn("EndlessHarmonyPatch: No next level available; allowing normal transition.");
                    return true;
                }

                // Pull required private services/fields from MenuTransitionsHelper
                var audioLoader = (AudioClipAsyncLoader)AccessTools.Field(typeof(MenuTransitionsHelper), "_audioClipAsyncLoader").GetValue(__instance);
                var settingsMgr = (SettingsManager)AccessTools.Field(typeof(MenuTransitionsHelper), "_settingsManager").GetValue(__instance);
                var dataLoader = (BeatmapDataLoader)AccessTools.Field(typeof(MenuTransitionsHelper), "_beatmapDataLoader").GetValue(__instance);
                var entitlement = (BeatmapLevelsEntitlementModel)AccessTools.Field(typeof(MenuTransitionsHelper), "_beatmapLevelsEntitlementModel").GetValue(__instance);
                var levelsModel = (BeatmapLevelsModel)AccessTools.Field(typeof(MenuTransitionsHelper), "_beatmapLevelsModel").GetValue(__instance);
                var scenesMgr = (GameScenesManager)AccessTools.Field(typeof(MenuTransitionsHelper), "_gameScenesManager").GetValue(__instance);

                // Re-init setup data for next map
                standardLevelScenesTransitionSetupData.Init(
                    gameMode: "Solo",
                    beatmapKey: nextKey,
                    beatmapLevel: nextLevel,
                    overrideEnvironmentSettings: null,
                    playerOverrideColorScheme: color,
                    playerOverrideLightshowColors: false,
                    beatmapOverrideColorScheme: null,
                    gameplayModifiers: modifiers,
                    playerSpecificSettings: playerSettings,
                    practiceSettings: null,
                    environmentsListModel: envs,
                    audioClipAsyncLoader: audioLoader,
                    settingsManager: settingsMgr,
                    backButtonText: "Menu",
                    useTestNoteCutSoundEffects: false,
                    startPaused: false,
                    beatmapLevelsModel: levelsModel,
                    beatmapDataLoader: dataLoader,
                    beatmapLevelsEntitlementModel: entitlement,
                    recordingToolData: null
                );

                Plugin.Log.Info($"EndlessHarmonyPatch: Chaining to next map with fade={fade:0.00}s");
                scenesMgr.ReplaceScenes(standardLevelScenesTransitionSetupData, null, fade);

                return false; // skip original finish handling
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"EndlessHarmonyPatch: Exception while chaining:\n{ex}");
                return true; // fail-open
            }
        }
    }
}
