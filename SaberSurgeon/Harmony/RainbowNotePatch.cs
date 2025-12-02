// File: SaberSurgeon/Harmony/RainbowNotePatch.cs
using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SaberSurgeon.HarmonyPatches
{
    [HarmonyPatch(typeof(ColorNoteVisuals))]
    internal static class RainbowNotePatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("HandleNoteControllerDidInit")]
        private static void Postfix(ColorNoteVisuals __instance)
        {
            if (!Gameplay.RainbowManager.RainbowActive)
                return;

            var type = typeof(ColorNoteVisuals);

            // Private instance fields on ColorNoteVisuals
            var mpbField = AccessTools.Field(type, "_materialPropertyBlockControllers");
            var defaultAlphaField = AccessTools.Field(type, "_defaultColorAlpha");

            // Static field
            var colorIdField = AccessTools.Field(type, "_colorId");

            if (mpbField == null || defaultAlphaField == null || colorIdField == null)
            {
                Plugin.Log.Warn("RainbowNotePatch: Failed to reflect ColorNoteVisuals fields.");
                return;
            }

            // Get the array of controllers as System.Array to avoid compile-time dependency
            var controllersObj = mpbField.GetValue(__instance) as Array;
            if (controllersObj == null || controllersObj.Length == 0)
                return;

            float defaultAlpha = (float)defaultAlphaField.GetValue(__instance);
            int colorId = (int)colorIdField.GetValue(null); // static field

            // Bright random color for this note
            var randomColor = UnityEngine.Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.8f, 1f);

            foreach (var ctrlObj in controllersObj)
            {
                if (ctrlObj == null)
                    continue;

                var ctrlType = ctrlObj.GetType();

                // MaterialPropertyBlockController.materialPropertyBlock
                var mpbProp = ctrlType.GetProperty(
                    "materialPropertyBlock",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                // MaterialPropertyBlockController.ApplyChanges()
                var applyMethod = ctrlType.GetMethod(
                    "ApplyChanges",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (mpbProp == null || applyMethod == null)
                    continue;

                var mpb = mpbProp.GetValue(ctrlObj) as MaterialPropertyBlock;
                if (mpb == null)
                    continue;

                mpb.SetColor(colorId, randomColor.ColorWithAlpha(defaultAlpha));
                applyMethod.Invoke(ctrlObj, null);
            }
        }
    }
}
