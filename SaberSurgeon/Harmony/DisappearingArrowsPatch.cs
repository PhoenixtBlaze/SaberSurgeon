// File: SaberSurgeon/Harmony/DisappearingArrowsPatch.cs
using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SaberSurgeon.HarmonyPatches
{
    [HarmonyPatch(typeof(ColorNoteVisuals))]
    internal static class DisappearingArrowsPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("HandleNoteControllerDidInit")]
        private static void Postfix(ColorNoteVisuals __instance)
        {
            // Only affect notes while our DA effect is active
            if (!Gameplay.DisappearingArrowsManager.DisappearingActive)
                return;

            var type = typeof(ColorNoteVisuals);

            // Private fields on ColorNoteVisuals
            var noteControllerField = AccessTools.Field(type, "_noteController");
            var arrowField = AccessTools.Field(type, "_arrowMeshRenderers");

            if (noteControllerField == null || arrowField == null)
            {
                Plugin.Log.Warn("DisappearingArrowsPatch: Failed to reflect required fields.");
                return;
            }

            var noteController = noteControllerField.GetValue(__instance);
            if (noteController == null)
                return;

            // Get noteData via reflection: noteController.noteData
            var ncType = noteController.GetType();
            var noteProp = ncType.GetProperty("noteData",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var noteData = noteProp?.GetValue(noteController);
            if (noteData == null)
                return;

            // noteData.cutDirection, compare to enum value "Any"
            var ndType = noteData.GetType();
            var cutDirProp = ndType.GetProperty("cutDirection",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var cutDirValue = cutDirProp?.GetValue(noteData);
            if (cutDirValue == null)
                return;

            // If this is an Any‑direction (dot) note, leave it alone
            var anyEnum = Enum.Parse(cutDirValue.GetType(), "Any");
            if (cutDirValue.Equals(anyEnum))
                return;

            // Directional note: hide arrow meshes, don't touch circles
            var arrowRenderers = arrowField.GetValue(__instance) as MeshRenderer[];
            if (arrowRenderers == null)
                return;

            foreach (var mr in arrowRenderers)
                if (mr != null)
                    mr.enabled = false;
        }
    }
}
