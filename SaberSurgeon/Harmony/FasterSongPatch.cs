using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace SaberSurgeon.HarmonyPatches
{
    /// <summary>
    /// Dynamically scales AudioTimeSyncController's _timeScale and AudioSource.pitch.
    /// </summary>
    [HarmonyPatch(typeof(AudioTimeSyncController))]
    internal static class FasterSongPatch
    {
        // 1.0f = normal speed, >1.0f faster, <1.0f slower
        public static float Multiplier { get; set; } = 1.0f;

        // Stores the original timeScale per controller instance
        private class ScaleData
        {
            public bool Initialized;
            public float BaseScale;
        }

        private static readonly ConditionalWeakTable<AudioTimeSyncController, ScaleData> _scaleData
            = new ConditionalWeakTable<AudioTimeSyncController, ScaleData>();

        // Private field refs inside AudioTimeSyncController
        private static readonly AccessTools.FieldRef<AudioTimeSyncController, float> TimeScaleRef =
            AccessTools.FieldRefAccess<AudioTimeSyncController, float>("_timeScale");

        private static readonly AccessTools.FieldRef<AudioTimeSyncController, AudioSource> AudioSourceRef =
            AccessTools.FieldRefAccess<AudioTimeSyncController, AudioSource>("_audioSource");

        /// <summary>
        /// Prefix on Update: ensure _timeScale and pitch are set to baseScale * Multiplier.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch("Update")]
        private static void Prefix_Update(AudioTimeSyncController __instance)
        {
            // Get or create per-instance data
            var data = _scaleData.GetOrCreateValue(__instance);

            // Capture original scale once (from property which exposes _timeScale)
            if (!data.Initialized)
            {
                data.BaseScale = __instance.timeScale;
                data.Initialized = true;
            }

            // Effective scale: base * Multiplier, but fall back to base if Multiplier ~ 1
            float effectiveScale = data.BaseScale;
            if (!Mathf.Approximately(Multiplier, 1.0f) && Multiplier > 0.0f)
                effectiveScale = data.BaseScale * Multiplier;

            // Write back into the private field so all internal math uses it
            TimeScaleRef(__instance) = effectiveScale;

            // Keep audio pitch in sync with timeScale, just like Start() does
            // (Start sets _audioSource.pitch = _initData.timeScale). [file:72]
            var src = AudioSourceRef(__instance);
            if (src != null)
                src.pitch = effectiveScale;
        }
    }
}
