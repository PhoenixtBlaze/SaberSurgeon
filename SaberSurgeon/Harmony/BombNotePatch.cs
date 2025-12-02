using HarmonyLib;
using HMUI;
using SaberSurgeon.Gameplay;
using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;

namespace SaberSurgeon.HarmonyPatches
{
    
    [HarmonyPatch(typeof(ColorNoteVisuals), "HandleNoteControllerDidInit")]
    [HarmonyPriority(Priority.Low)] // Run after other mods
    internal static class BombNotePatch
    {
        private static BombNoteController _bombPrefab;

        private static void Postfix(ColorNoteVisuals __instance, NoteControllerBase noteController)
        {
            if (!BombManager.BombArmed)
                return;

            var noteData = noteController.noteData;
            if (noteData == null || noteData.colorType == ColorType.None)
                return;

            var gameNote = __instance.GetComponentInParent<GameNoteController>();
            if (gameNote == null)
            {
                Plugin.Log.Warn("BombNotePatch: No GameNoteController parent found");
                return;
            }

            BombManager.Instance.MarkNoteAsBomb(noteData);

            // 1) Hide the normal note visuals (cube + arrows)
            foreach (var r in gameNote.GetComponentsInChildren<MeshRenderer>())
                r.enabled = false;

            // 2) Add a simple bomb “sphere” so it doesn’t look like an invisible/dot note
            AddSimpleBombVisual(gameNote);

        }

        private static void HideNoteVisuals(GameNoteController gameNote)
        {
            // Fallback: just hide if we can't find bomb prefab
            foreach (var r in gameNote.GetComponentsInChildren<MeshRenderer>())
                r.enabled = false;
            gameNote.transform.localScale *= 1.2f;
        }

        private static void AddSimpleBombVisual(GameNoteController gameNote)
        {
            // Prevent duplicates if pooled object re-inits
            var existing = gameNote.transform.Find("SaberSurgeon_BombVisual");
            if (existing != null)
                return;

            var bombGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bombGo.name = "SaberSurgeon_BombVisual";
            bombGo.transform.SetParent(gameNote.transform, false);
            bombGo.transform.localPosition = Vector3.zero;
            bombGo.transform.localScale = Vector3.one * 0.45f;

            var mr = bombGo.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                // Dark body
                mr.material = new Material(Shader.Find("Standard"));
                mr.material.color = new Color(0.1f, 0.1f, 0.1f, 1f);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            // Optional: small “fuse”
            var fuse = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fuse.name = "SaberSurgeon_BombFuse";
            fuse.transform.SetParent(bombGo.transform, false);
            fuse.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            fuse.transform.localScale = new Vector3(0.15f, 0.4f, 0.15f);

            var fuseMr = fuse.GetComponent<MeshRenderer>();
            if (fuseMr != null)
            {
                fuseMr.material = new Material(Shader.Find("Standard"));
                fuseMr.material.color = Color.yellow;
                fuseMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                fuseMr.receiveShadows = false;
            }

            // Remove colliders so we don’t mess with hitbox
            //UnityEngine.Object.DestroyImmediate(bombGo.GetComponent<Collider>(), true);
            //UnityEngine.Object.DestroyImmediate(fuse.GetComponent<Collider>(), true);

            Plugin.Log.Info("BombNotePatch: Added simple bomb visual to note");
        }
    }
    

    [HarmonyPatch(typeof(GameNoteController), "HandleCut")]
    [HarmonyPriority(Priority.High)] // Run before normal processing
    internal static class BombCutPatch
    {
        private static NoteCutCoreEffectsSpawner _effectsSpawner;
        private static CurvedTextMeshPro _flyingTextPrefab;

        private static void Postfix(
            GameNoteController __instance,
            Saber saber,
            Vector3 cutPoint,
            Quaternion orientation,
            Vector3 cutDirVec,
            bool allowBadCut)
        {
            var noteData = __instance.noteData;
            if (noteData == null) return;

            if (!BombManager.Instance.TryConsumeBomb(noteData, out var bomber)) return;

            Plugin.Log.Info($"BombCutPatch: Bomb cut by {bomber}");

            EnsureRefs();

            // Spawn explosion particles
            SpawnBombExplosion(__instance, cutPoint, orientation, saber, cutDirVec);

            // Spawn flying username text
            SpawnFlyingUsername(bomber, cutPoint);
        }

        private static void EnsureRefs()
        {
            if (_effectsSpawner == null)
            {
                _effectsSpawner = Resources.FindObjectsOfTypeAll<NoteCutCoreEffectsSpawner>().FirstOrDefault();
                if (_effectsSpawner != null)
                    Plugin.Log.Info("BombCutPatch: Cached NoteCutCoreEffectsSpawner");
            }

            if (_flyingTextPrefab == null)
            {
                // Find game's flying score text prefab
                var flyingScores = Resources.FindObjectsOfTypeAll<FlyingScoreEffect>();
                if (flyingScores != null && flyingScores.Length > 0)
                {
                    _flyingTextPrefab = flyingScores[0].GetComponentInChildren<CurvedTextMeshPro>(true);
                    if (_flyingTextPrefab != null)
                        Plugin.Log.Info("BombCutPatch: Cached CurvedTextMeshPro from FlyingScoreEffect");
                }
            }
        }

        private static void SpawnBombExplosion(
            GameNoteController noteController,
            Vector3 cutPoint,
            Quaternion orientation,
            Saber saber,
            Vector3 cutDirVec)
        {
            if (_effectsSpawner == null) return;

            try
            {
                // Access particles via reflection
                var particlesField = AccessTools.Field(typeof(NoteCutCoreEffectsSpawner), "_noteCutParticlesEffect");
                var particles = particlesField?.GetValue(_effectsSpawner) as NoteCutParticlesEffect;

                if (particles == null)
                {
                    Plugin.Log.Warn("BombCutPatch: _noteCutParticlesEffect was null");
                    return;
                }

                // Bright yellow explosion
                Color color = Color.Lerp(Color.yellow, Color.white, 0.5f); // very bright
                color.a = 1.0f;

                Vector3 cutNormal = orientation * Vector3.up;
                Vector3 saberDir = cutDirVec.normalized;
                float saberSpeed = saber.bladeSpeedForLogic;
                Vector3 moveVec = noteController.moveVec;

                float lifetimeMultiplier = Mathf.Clamp(noteController.noteData.timeToNextColorNote + 0.2f, 0.7f, 3f) * 3.0f;

                particles.SpawnParticles(
                    cutPoint,
                    cutNormal,
                    saberDir,
                    saberSpeed,
                    moveVec,
                    (Color32)color,
                    200, // sparkleCount
                    900, // explosionCount
                    lifetimeMultiplier);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"BombCutPatch: Error spawning explosion: {ex}");
            }
        }

        private static void SpawnFlyingUsername(string username, Vector3 cutPoint)
        {
            if (string.IsNullOrEmpty(username)) return;

            try
            {
                // Use game's flying text if available, otherwise create simple text
                if (_flyingTextPrefab != null)
                {
                    SpawnCurvedFlyingText(username, cutPoint);
                }
                else
                {
                    SpawnSimpleFlyingText(username, cutPoint);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"BombCutPatch: Error spawning username text: {ex}");
            }
        }

        private static void SpawnCurvedFlyingText(string username, Vector3 cutPoint)
        {
            var textGo = new GameObject("BombUsername_CurvedText");
            textGo.transform.position = cutPoint + Vector3.up * 0.5f;

            // Copy the curved text component
            var curvedText = textGo.AddComponent<CurvedTextMeshPro>();

            // Get font from prefab
            if (_flyingTextPrefab.font != null)
                curvedText.font = _flyingTextPrefab.font;

            curvedText.text = username;
            curvedText.fontSize = 4f;
            curvedText.alignment = TextAlignmentOptions.Center;
            curvedText.color = Color.yellow;
            curvedText.outlineWidth = 0.2f;
            curvedText.outlineColor = Color.black;

            // Start animation coroutine
            CoroutineHost.Instance.StartCoroutine(AnimateFlyingText(textGo, cutPoint));
        }

        private static void SpawnSimpleFlyingText(string username, Vector3 cutPoint)
        {
            var textGo = new GameObject("BombUsername_Text");
            textGo.transform.position = cutPoint + Vector3.up * 0.5f;

            var tmp = textGo.AddComponent<TextMeshPro>();

            // Try to find game's font
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            var tekoFont = fonts.FirstOrDefault(f => f.name.Contains("Teko"));
            if (tekoFont != null)
                tmp.font = tekoFont;

            tmp.text = username;
            tmp.fontSize = 4f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.yellow;
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = Color.black;

            // Make text face camera
            var lookAt = textGo.AddComponent<LookAtCamera>();

            // Start animation coroutine
            CoroutineHost.Instance.StartCoroutine(AnimateFlyingText(textGo, cutPoint));
        }

        private static IEnumerator AnimateFlyingText(GameObject textGo, Vector3 startPos)
        {
            float duration = 2.0f;
            float elapsed = 0f;

            // Find note spawn position (forward from player)
            Vector3 targetPos = startPos + Camera.main.transform.forward * 10f + Vector3.up * 2f;

            var tmp = textGo.GetComponent<TextMeshPro>();
            Color startColor = tmp != null ? tmp.color : Color.yellow;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Move forward and up
                textGo.transform.position = Vector3.Lerp(startPos + Vector3.up * 0.5f, targetPos, t);

                // Fade out
                if (tmp != null)
                {
                    Color c = startColor;
                    c.a = Mathf.Lerp(1f, 0f, t);
                    tmp.color = c;
                }

                // Scale up slightly then down
                float scale = Mathf.Sin(t * Mathf.PI) * 0.3f + 1f;
                textGo.transform.localScale = Vector3.one * scale;

                yield return null;
            }

            UnityEngine.Object.Destroy(textGo);
        }
    }
}
